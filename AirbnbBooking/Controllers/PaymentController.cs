using System.Text;
using AirbnbBooking.Models;
using AirbnbBooking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 4: Payment & Transaction Management
    /// Secure Mock Gateway Checkout (Card w/ Luhn, FPX, Touch 'n Go eWallet),
    /// Host Payout Accounting Ledger, Settlement Disbursement, Promo Validation & CSV Exports.
    /// </summary>
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly PricingService _pricing;
        private readonly EmailService _email;

        public PaymentController(DB db, Helper helper, PricingService pricing, EmailService email)
        {
            _db = db;
            _helper = helper;
            _pricing = pricing;
            _email = email;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int bookingId)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Photos)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.PromoCode)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(booking.GuestId)) return Forbid();

            if (booking.Status != "Pending")
            {
                TempData["Info"] = $"Booking {booking.BookingReference} is already {booking.Status}.";
                return RedirectToAction("Details", "Booking", new { id = bookingId });
            }

            var vm = new CheckoutViewModel
            {
                BookingId = booking.Id,
                Booking = booking,
                CardHolderName = _helper.GetCurrentUserFullName()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(CheckoutViewModel vm)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Property).ThenInclude(p => p.Photos)
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == vm.BookingId);

            if (booking == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(booking.GuestId)) return Forbid();

            if (booking.Status != "Pending")
            {
                TempData["Info"] = "This reservation has already been processed.";
                return RedirectToAction("Details", "Booking", new { id = booking.Id });
            }

            if (!ModelState.IsValid)
            {
                vm.Booking = booking;
                return View("Checkout", vm);
            }

            string txnRef = $"TXN-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
            string? last4 = null;
            string methodLabel = vm.PaymentMethod;

            if (vm.PaymentMethod == "Credit / Debit Card" && !string.IsNullOrWhiteSpace(vm.CardNumber))
            {
                string digits = new string(vm.CardNumber.Where(char.IsDigit).ToArray());
                last4 = digits.Length >= 4 ? digits[^4..] : digits;
                methodLabel = $"Credit / Debit Card (•••• {last4})";
            }
            else if (vm.PaymentMethod == "FPX Online Banking")
            {
                methodLabel = $"FPX Online Banking ({vm.FpxBank})";
            }
            else if (vm.PaymentMethod == "Touch 'n Go eWallet")
            {
                methodLabel = $"Touch 'n Go eWallet ({vm.WalletPhone})";
            }

            var payment = new Payment
            {
                BookingId = booking.Id,
                PaymentType = "FullPayment",
                Amount = booking.TotalAmount,
                PaymentDate = DateTime.Now,
                PaymentMethod = methodLabel,
                CardLast4 = last4,
                TransactionRef = txnRef,
                Status = "Paid"
            };

            // Compute Host Payout Ledger entry
            decimal hostGross = booking.SubtotalAmount + booking.CleaningFee;
            decimal platformFee = Math.Round(hostGross * PricingService.HostCommissionRate, 2);
            decimal hostNet = Math.Max(0m, Math.Round(hostGross - platformFee, 2));

            var payout = new HostPayout
            {
                HostId = booking.Property.HostId,
                BookingId = booking.Id,
                GrossAmount = hostGross,
                PlatformFee = platformFee,
                NetPayoutAmount = hostNet,
                Status = "Pending",
                PayoutReference = $"PO-{DateTime.Now:yyyy}-{Random.Shared.Next(1000, 9999)}",
                ScheduledDate = booking.CheckOutDate.AddDays(1)
            };

            booking.Status = "Confirmed";
            _db.Payments.Add(payment);
            _db.HostPayouts.Add(payout);
            await _db.SaveChangesAsync();

            // Send in-app notifications & real Gmail confirmation receipt
            _helper.CreateNotification(_db, booking.Property.HostId, "New Confirmed Booking!",
                $"{booking.Guest.FullName} booked {booking.Property.Title} ({booking.CheckInDate:dd MMM} – {booking.CheckOutDate:dd MMM}).",
                "Booking", $"/Booking/Details/{booking.Id}");

            _helper.CreateNotification(_db, booking.GuestId, "Payment Confirmed — QR Pass Ready",
                $"Paid RM {booking.TotalAmount:N2} ({txnRef}). Your check-in QR pass is ready.",
                "Payment", $"/Booking/CheckInPass/{booking.Id}");

            bool emailSent = await _email.SendBookingConfirmationAsync(
                booking.Guest.Email,
                booking.Guest.FullName,
                booking.BookingReference,
                booking.Property.Title,
                booking.TotalAmount,
                booking.CheckInDate.ToString("dd MMM yyyy"),
                booking.CheckOutDate.ToString("dd MMM yyyy"),
                booking.QrCheckInToken);

            _helper.LogAudit(_db, booking.GuestId, booking.Guest.Email, "Payment", "PaymentCaptured",
                $"Captured RM {booking.TotalAmount:N2} via {methodLabel} (Ref: {txnRef}) for {booking.BookingReference}. E-Receipt email sent={emailSent}.",
                "Info", "Payment", payment.Id);

            TempData["Success"] = emailSent
                ? $"Payment successful ({txnRef})! Confirmation & E-Receipt emailed to {booking.Guest.Email}."
                : $"Payment successful ({txnRef})! Your reservation is confirmed and QR Check-In Pass is ready.";

            return RedirectToAction("Receipt", "Booking", new { id = booking.Id });
        }

        // =====================================================================
        // Host Payout Accounting & Earnings Ledger
        // =====================================================================
        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> HostPayouts()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";

            var payouts = await _db.HostPayouts
                .Include(p => p.Host)
                .Include(p => p.Booking).ThenInclude(b => b.Property)
                .Include(p => p.Booking).ThenInclude(b => b.Guest)
                .Where(p => isAdmin || p.HostId == uid)
                .OrderByDescending(p => p.ScheduledDate)
                .ToListAsync();

            var hostUser = await _db.Users.FindAsync(uid);
            ViewBag.BankName = hostUser?.BankName ?? "Not configured";
            ViewBag.MaskedBank = _helper.MaskBankAccount(_helper.Decrypt(hostUser?.BankAccountNumberEncrypted ?? ""));

            return View(payouts);
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> ExportPayoutsCsv()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";

            var payouts = await _db.HostPayouts
                .Include(p => p.Booking).ThenInclude(b => b.Property)
                .Where(p => isAdmin || p.HostId == uid)
                .OrderByDescending(p => p.ScheduledDate)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("PayoutRef,BookingRef,Property,GrossMYR,HostFeeMYR,NetPayoutMYR,Status,ScheduledDate,DisbursedAt");
            foreach (var p in payouts)
            {
                sb.AppendLine($"{p.PayoutReference},{p.Booking.BookingReference},\"{p.Booking.Property.Title.Replace("\"", "'")}\",{p.GrossAmount:F2},{p.PlatformFee:F2},{p.NetPayoutAmount:F2},{p.Status},{p.ScheduledDate:yyyy-MM-dd},{p.DisbursedAt:yyyy-MM-dd}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"StayNest_Payouts_{DateTime.Now:yyyyMMdd}.csv");
        }

        // =====================================================================
        // Admin Platform Transactions & Payout Settlement Console
        // =====================================================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Transactions()
        {
            var payments = await _db.Payments
                .Include(p => p.Booking).ThenInclude(b => b.Property)
                .Include(p => p.Booking).ThenInclude(b => b.Guest)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            ViewBag.PendingPayouts = await _db.HostPayouts
                .Include(hp => hp.Host)
                .Include(hp => hp.Booking).ThenInclude(b => b.Property)
                .OrderBy(hp => hp.Status == "Pending" ? 0 : 1)
                .ThenByDescending(hp => hp.ScheduledDate)
                .ToListAsync();

            return View(payments);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisbursePayout(int id)
        {
            var payout = await _db.HostPayouts.Include(p => p.Host).FirstOrDefaultAsync(p => p.Id == id);
            if (payout == null) return NotFound();

            payout.Status = "Disbursed";
            payout.DisbursedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _helper.CreateNotification(_db, payout.HostId, "Host Payout Disbursed",
                $"Payout {payout.PayoutReference} of RM {payout.NetPayoutAmount:N2} has been transferred to your bank account.",
                "Payment", "/Payment/HostPayouts");

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Payment", "PayoutDisbursed",
                $"Disbursed {payout.PayoutReference} (RM {payout.NetPayoutAmount:N2}) to Host {payout.Host.Email}.",
                "Info", "HostPayout", payout.Id);

            TempData["Success"] = $"Payout {payout.PayoutReference} marked as Disbursed.";
            return RedirectToAction(nameof(Transactions));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDisbursePayouts(int[] payoutIds)
        {
            if (payoutIds == null || payoutIds.Length == 0)
            {
                TempData["Error"] = "Select at least one pending payout.";
                return RedirectToAction(nameof(Transactions));
            }

            var list = await _db.HostPayouts
                .Where(p => payoutIds.Contains(p.Id) && p.Status == "Pending")
                .ToListAsync();

            foreach (var p in list)
            {
                p.Status = "Disbursed";
                p.DisbursedAt = DateTime.Now;
            }
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Payment", "BatchPayoutDisbursed",
                $"Batch-disbursed {list.Count} host payouts totaling RM {list.Sum(x => x.NetPayoutAmount):N2}.", "Info");

            TempData["Success"] = $"Batch-disbursed {list.Count} host payout(s).";
            return RedirectToAction(nameof(Transactions));
        }
    }
}
