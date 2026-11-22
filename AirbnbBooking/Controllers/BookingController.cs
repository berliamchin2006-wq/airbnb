using System.Data;
using AirbnbBooking.Models;
using AirbnbBooking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 3: Booking & Reservation Management
    /// Dynamic Price Breakdown, Availability Calendar, Concurrency & Transaction Locking (Serializable + Timestamp),
    /// State Machine (Pending -> Confirmed -> CheckedIn -> Completed | Cancelled), QR Pass & Printable PDF E-Receipt.
    /// </summary>
    public class BookingController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly PricingService _pricing;
        private readonly EmailService _email;

        public BookingController(DB db, Helper helper, PricingService pricing, EmailService email)
        {
            _db = db;
            _helper = helper;
            _pricing = pricing;
            _email = email;
        }

        // AJAX: Unavailable booked date intervals for interactive availability calendar
        [HttpGet]
        public async Task<IActionResult> GetBookedDates(int propertyId)
        {
            var ranges = await _db.Bookings
                .Where(b => b.PropertyId == propertyId && b.Status != "Cancelled" && b.CheckOutDate >= DateTime.Today)
                .Select(b => new
                {
                    start = b.CheckInDate.ToString("yyyy-MM-dd"),
                    end = b.CheckOutDate.ToString("yyyy-MM-dd"),
                    status = b.Status
                })
                .ToListAsync();

            return Json(ranges);
        }

        // AJAX: Real-time availability check + dynamic seasonal/weekend price breakdown
        [HttpGet]
        public async Task<IActionResult> CalculatePrice(int propertyId, DateTime checkIn, DateTime checkOut, string? promoCode)
        {
            if (checkOut.Date <= checkIn.Date)
                return Json(new { ok = false, error = "Check-out date must be after check-in date." });

            if (checkIn.Date < DateTime.Today)
                return Json(new { ok = false, error = "Check-in date cannot be in the past." });

            var prop = await _db.Properties
                .Include(p => p.SeasonalRates)
                .FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);

            if (prop == null) return Json(new { ok = false, error = "Property not found." });

            bool conflict = await _db.Bookings.AnyAsync(b =>
                b.PropertyId == propertyId &&
                b.Status != "Cancelled" &&
                b.CheckInDate < checkOut.Date &&
                b.CheckOutDate > checkIn.Date);

            if (conflict)
                return Json(new { ok = false, error = "Selected dates overlap with an existing reservation. Please choose different dates." });

            PromoCode? promo = null;
            bool promoNotFound = false;
            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                string code = promoCode.Trim().ToUpperInvariant();
                promo = await _db.PromoCodes.FirstOrDefaultAsync(c => c.Code.ToUpper() == code);
                if (promo == null) promoNotFound = true;
            }

            var calc = _pricing.CalculateBookingPrice(prop, prop.SeasonalRates, checkIn, checkOut, promo);
            string activeCurrency = _helper.GetActiveCurrency();
            string? promoMsg = promoNotFound ? "Promo code not found." : calc.PromoMessage;

            return Json(new
            {
                ok = true,
                available = true,
                currency = activeCurrency,
                nights = calc.Nights,
                avgNightly = calc.AverageNightlyRate,
                subtotal = calc.SubtotalAmount,
                cleaningFee = calc.CleaningFee,
                serviceTax = calc.ServiceTax,
                discount = calc.DiscountAmount,
                total = calc.TotalAmount,
                subtotalFormatted = _helper.FormatMoney(calc.SubtotalAmount, activeCurrency),
                cleaningFeeFormatted = _helper.FormatMoney(calc.CleaningFee, activeCurrency),
                serviceTaxFormatted = _helper.FormatMoney(calc.ServiceTax, activeCurrency),
                discountFormatted = _helper.FormatMoney(calc.DiscountAmount, activeCurrency),
                totalFormatted = _helper.FormatMoney(calc.TotalAmount, activeCurrency),
                promoCode = calc.AppliedPromoCode,
                promoInvalid = promoNotFound,
                promoMessage = promoMsg,
                nightly = calc.NightlyBreakdown.Select(n => new { date = n.Date.ToString("dd MMM"), rate = n.Rate, label = n.RateLabel })
            });
        }

        // =====================================================================
        // Create Reservation with Serializable Transaction Locking (No Double-Booking)
        // =====================================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingCreateViewModel vm)
        {
            var prop = await _db.Properties
                .Include(p => p.SeasonalRates)
                .FirstOrDefaultAsync(p => p.Id == vm.PropertyId && p.IsActive);

            if (prop == null) return NotFound();

            int guestId = _helper.GetCurrentUserId()!.Value;
            if (prop.HostId == guestId && _helper.GetCurrentUserRole() != "Admin")
            {
                TempData["Error"] = "Hosts cannot book their own property listing.";
                return RedirectToAction("Details", "Property", new { id = vm.PropertyId });
            }

            if (vm.GuestCount > prop.MaxGuests)
            {
                TempData["Error"] = $"Maximum allowed guests for this property is {prop.MaxGuests}.";
                return RedirectToAction("Details", "Property", new { id = vm.PropertyId });
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction("Details", "Property", new { id = vm.PropertyId });
            }

            // Execute inside EF Core Execution Strategy + Serializable Transaction to guarantee zero double-bookings
            var strategy = _db.Database.CreateExecutionStrategy();
            Booking? createdBooking = null;
            string? conflictError = null;

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    bool overlaps = await _db.Bookings.AnyAsync(b =>
                        b.PropertyId == vm.PropertyId &&
                        b.Status != "Cancelled" &&
                        b.CheckInDate < vm.CheckOutDate.Date &&
                        b.CheckOutDate > vm.CheckInDate.Date);

                    if (overlaps)
                    {
                        conflictError = "Sorry! Those dates were just booked by another guest. Please choose different dates.";
                        await tx.RollbackAsync();
                        return;
                    }

                    PromoCode? promo = null;
                    if (!string.IsNullOrWhiteSpace(vm.PromoCode))
                    {
                        string code = vm.PromoCode.Trim().ToUpperInvariant();
                        promo = await _db.PromoCodes.FirstOrDefaultAsync(c => c.Code.ToUpper() == code);
                    }

                    var calc = _pricing.CalculateBookingPrice(prop, prop.SeasonalRates, vm.CheckInDate, vm.CheckOutDate, promo);

                    string reference = $"SN-{DateTime.Now:yyyy}-{Random.Shared.Next(10000, 99999)}";
                    string qrToken = $"QR-{reference}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

                    var booking = new Booking
                    {
                        BookingReference = reference,
                        PropertyId = prop.Id,
                        GuestId = guestId,
                        PromoCodeId = calc.AppliedPromoCode != null ? promo?.Id : null,
                        CheckInDate = vm.CheckInDate.Date,
                        CheckOutDate = vm.CheckOutDate.Date,
                        TotalNights = calc.Nights,
                        GuestCount = vm.GuestCount,
                        AverageNightlyRate = calc.AverageNightlyRate,
                        SubtotalAmount = calc.SubtotalAmount,
                        CleaningFee = calc.CleaningFee,
                        ServiceTax = calc.ServiceTax,
                        DiscountAmount = calc.DiscountAmount,
                        TotalAmount = calc.TotalAmount,
                        Status = "Pending",
                        SpecialRequests = vm.SpecialRequests?.Trim(),
                        QrCheckInToken = qrToken,
                        BookingDate = DateTime.Now
                    };

                    if (calc.AppliedPromoCode != null && promo != null)
                    {
                        promo.CurrentUses++;
                    }

                    _db.Bookings.Add(booking);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                    createdBooking = booking;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await tx.RollbackAsync();
                    conflictError = "A concurrency conflict occurred while reserving dates. Please retry.";
                }
            });

            if (createdBooking == null)
            {
                TempData["Error"] = conflictError ?? "Unable to reserve these dates right now.";
                return RedirectToAction("Details", "Property", new { id = vm.PropertyId });
            }

            _helper.LogAudit(_db, guestId, _helper.GetCurrentUserEmail(), "Booking", "ReservationCreated",
                $"Created pending reservation {createdBooking.BookingReference} for Property #{prop.Id} ({createdBooking.TotalNights} nights, RM {createdBooking.TotalAmount:N2}).",
                "Info", "Booking", createdBooking.Id);

            return RedirectToAction("Checkout", "Payment", new { bookingId = createdBooking.Id });
        }

        // =====================================================================
        // Guest "My Trips" & Host "Incoming Reservations"
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyTrips(string? status)
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var query = _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Photos)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Payments)
                .Include(b => b.Review)
                .Where(b => b.GuestId == uid);

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
                query = query.Where(b => b.Status == status);

            ViewBag.SelectedStatus = status ?? "All";
            var list = await query.OrderByDescending(b => b.BookingDate).ToListAsync();
            return View(list);
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> HostBookings(string? status)
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";

            var query = _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Photos)
                .Include(b => b.Guest)
                .Include(b => b.Payments)
                .Include(b => b.HostPayout)
                .Include(b => b.Review)
                .Where(b => isAdmin || b.Property.HostId == uid);

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
                query = query.Where(b => b.Status == status);

            ViewBag.SelectedStatus = status ?? "All";
            var list = await query.OrderByDescending(b => b.BookingDate).ToListAsync();
            return View(list);
        }

        // =====================================================================
        // Booking Details, Digital QR Check-In Pass & Printable E-Receipt
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Photos)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .Include(b => b.PromoCode)
                .Include(b => b.Payments)
                .Include(b => b.HostPayout)
                .Include(b => b.ServiceRequests)
                .Include(b => b.Review)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            int uid = _helper.GetCurrentUserId()!.Value;
            bool allowed = booking.GuestId == uid || booking.Property.HostId == uid || _helper.GetCurrentUserRole() == "Admin";
            if (!allowed) return Forbid();

            decimal paid = booking.Payments.Where(p => p.Status is "Paid" or "PartiallyRefunded").Sum(p => p.Amount);
            ViewBag.RefundPreview = _pricing.CalculateRefund(booking, paid, DateTime.Now);
            return View(booking);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CheckInPass(int id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();
            int uid = _helper.GetCurrentUserId()!.Value;
            if (booking.GuestId != uid && booking.Property.HostId != uid && _helper.GetCurrentUserRole() != "Admin")
                return Forbid();

            return View(booking);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .Include(b => b.Payments)
                .Include(b => b.PromoCode)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();
            int uid = _helper.GetCurrentUserId()!.Value;
            if (booking.GuestId != uid && booking.Property.HostId != uid && _helper.GetCurrentUserRole() != "Admin")
                return Forbid();

            return View(booking);
        }

        // =====================================================================
        // Automated Cancellation Policy & Refund Enforcement
        // =====================================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .Include(b => b.PromoCode)
                .Include(b => b.Payments)
                .Include(b => b.HostPayout)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            int uid = _helper.GetCurrentUserId()!.Value;
            bool allowed = booking.GuestId == uid || booking.Property.HostId == uid || _helper.GetCurrentUserRole() == "Admin";
            if (!allowed) return Forbid();

            if (booking.Status is "Completed" or "Cancelled" or "CheckedIn")
            {
                TempData["Error"] = $"Cannot cancel a booking that is already {booking.Status}.";
                return RedirectToAction(nameof(Details), new { id });
            }

            bool wasPending = booking.Status == "Pending";
            var payment = booking.Payments.FirstOrDefault(p => p.Status == "Paid");
            decimal paidAmount = payment?.Amount ?? 0m;
            var refundCalc = _pricing.CalculateRefund(booking, paidAmount, DateTime.Now);

            string cleanCancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by user" : reason.Trim();
            if (cleanCancelReason.Length > 400) cleanCancelReason = cleanCancelReason[..400];
            booking.Status = "Cancelled";
            booking.CancellationReason = cleanCancelReason;
            booking.CancelledAt = DateTime.Now;
            booking.CancelledByUserId = uid;

            if (wasPending && booking.PromoCode != null && booking.PromoCode.CurrentUses > 0)
            {
                booking.PromoCode.CurrentUses--;
            }

            if (payment != null && refundCalc.RefundAmount > 0)
            {
                payment.RefundAmount = refundCalc.RefundAmount;
                payment.RefundReason = refundCalc.Explanation;
                payment.RefundedAt = DateTime.Now;
                payment.Status = refundCalc.RefundAmount >= payment.Amount ? "Refunded" : "PartiallyRefunded";
            }

            if (booking.HostPayout != null && booking.HostPayout.Status != "Disbursed")
            {
                booking.HostPayout.Status = "Reversed";
            }

            await _db.SaveChangesAsync();

            _helper.CreateNotification(_db, booking.Property.HostId, "Reservation Cancelled",
                $"Booking {booking.BookingReference} ({booking.Property.Title}) was cancelled.", "Booking", $"/Booking/Details/{booking.Id}");
            _helper.CreateNotification(_db, booking.GuestId, "Cancellation & Refund Processed",
                $"{booking.BookingReference}: {refundCalc.Explanation} Refund: RM {refundCalc.RefundAmount:N2}.", "Payment", $"/Booking/Details/{booking.Id}");

            await _email.SendRefundNotificationAsync(
                booking.Guest.Email,
                booking.Guest.FullName,
                booking.BookingReference,
                booking.Property.Title,
                refundCalc.PolicyName,
                refundCalc.RefundAmount,
                refundCalc.Explanation);

            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "Booking", "ReservationCancelled",
                $"Cancelled {booking.BookingReference}. Policy={refundCalc.PolicyName}, Refund=RM {refundCalc.RefundAmount:N2}.",
                "Warning", "Booking", booking.Id);

            TempData["Success"] = $"Reservation cancelled. {refundCalc.Explanation} Refund processed: RM {refundCalc.RefundAmount:N2}.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
