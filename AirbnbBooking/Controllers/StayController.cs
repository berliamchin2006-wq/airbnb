using AirbnbBooking.Models;
using AirbnbBooking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 5: In-Stay & Operations Management
    /// Check-in/Check-out workflow, Webcam QR Verification, Guest Service Requests & Issues,
    /// Real-Time Guest–Host SignalR Chat, Notifications & Automated Arrival/Departure Reminders.
    /// </summary>
    [Authorize]
    public class StayController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly EmailService _email;

        public StayController(DB db, Helper helper, EmailService email)
        {
            _db = db;
            _helper = helper;
            _email = email;
        }

        // In-Stay Operations Hub (Shows Active/Upcoming Stays & Alerts for Guest, Host, or Admin)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            string role = _helper.GetCurrentUserRole();

            var query = _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Photos)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .Include(b => b.ServiceRequests)
                .Include(b => b.Messages)
                .Where(b => b.Status == "Confirmed" || b.Status == "CheckedIn");

            if (role == "Guest")
                query = query.Where(b => b.GuestId == uid);
            else if (role == "Host")
                query = query.Where(b => b.Property.HostId == uid);

            var activeStays = await query.OrderBy(b => b.CheckInDate).ToListAsync();

            var today = DateTime.Today;
            ViewBag.ArrivalsSoon = activeStays.Where(b => b.Status == "Confirmed" && b.CheckInDate <= today.AddDays(3)).ToList();
            ViewBag.DeparturesSoon = activeStays.Where(b => b.Status == "CheckedIn" && b.CheckOutDate <= today.AddDays(2)).ToList();

            return View(activeStays);
        }

        // Check-In Transition (Confirmed -> CheckedIn)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int bookingId)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();
            int uid = _helper.GetCurrentUserId()!.Value;
            if (booking.GuestId != uid && booking.Property.HostId != uid && _helper.GetCurrentUserRole() != "Admin")
                return Forbid();

            if (booking.Status != "Confirmed")
            {
                TempData["Error"] = $"Booking must be Confirmed to check in (current: {booking.Status}).";
                return RedirectToAction(nameof(Index));
            }

            booking.Status = "CheckedIn";
            booking.CheckedInAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _helper.CreateNotification(_db, booking.Property.HostId, "Guest Checked In",
                $"{booking.Guest.FullName} checked in to {booking.Property.Title} ({booking.BookingReference}).",
                "Arrival", $"/Booking/Details/{booking.Id}");

            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "InStay", "GuestCheckedIn",
                $"Checked in booking {booking.BookingReference}.", "Info", "Booking", booking.Id);

            TempData["Success"] = $"Check-in recorded for {booking.BookingReference}!";
            return RedirectToAction("Details", "Booking", new { id = booking.Id });
        }

        // Check-Out Transition (CheckedIn -> Completed) — Unlocks Mutual Blind Reviews!
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int bookingId)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();
            int uid = _helper.GetCurrentUserId()!.Value;
            if (booking.GuestId != uid && booking.Property.HostId != uid && _helper.GetCurrentUserRole() != "Admin")
                return Forbid();

            if (booking.Status != "CheckedIn")
            {
                TempData["Error"] = $"Only Checked-In bookings can be checked out (current: {booking.Status}).";
                return RedirectToAction(nameof(Index));
            }

            booking.Status = "Completed";
            booking.CheckedOutAt = DateTime.Now;
            await _db.SaveChangesAsync();

            // Notify both Guest & Host that the 14-day double-blind review window is now open
            _helper.CreateNotification(_db, booking.GuestId, "Stay Completed — Leave a Review",
                $"Thanks for staying at {booking.Property.Title}! Share your 4-category review within 14 days.",
                "Review", "/Review/Pending");

            _helper.CreateNotification(_db, booking.Property.HostId, "Guest Checked Out — Rate Guest",
                $"{booking.Guest.FullName} checked out of {booking.Property.Title}. Submit your host review!",
                "Review", "/Review/Pending");

            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "InStay", "GuestCheckedOut",
                $"Completed checkout for {booking.BookingReference}. 14-day mutual review window opened.",
                "Info", "Booking", booking.Id);

            TempData["Success"] = $"Check-out completed for {booking.BookingReference}! Post-stay mutual reviews are now unlocked.";
            return RedirectToAction("Pending", "Review");
        }

        // =====================================================================
        // Host Webcam QR Scanner & Manual Check-In Token Verification
        // =====================================================================
        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public IActionResult QrScanner() => View();

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyQr(string qrToken)
        {
            if (string.IsNullOrWhiteSpace(qrToken))
            {
                return Json(new { ok = false, message = "QR token is empty." });
            }

            string clean = qrToken.Trim();
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";

            var booking = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.QrCheckInToken == clean || b.BookingReference == clean);

            if (booking == null)
                return Json(new { ok = false, message = "No reservation matches that QR pass or booking reference." });

            if (!isAdmin && booking.Property.HostId != uid)
                return Json(new { ok = false, message = "Access denied: this booking belongs to a different host." });

            if (booking.Status == "Confirmed")
            {
                booking.Status = "CheckedIn";
                booking.CheckedInAt = DateTime.Now;
                await _db.SaveChangesAsync();

                _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "InStay", "QrCheckInVerified",
                    $"Verified QR pass {clean} and checked in {booking.Guest.FullName} ({booking.BookingReference}).",
                    "Info", "Booking", booking.Id);

                return Json(new
                {
                    ok = true,
                    action = "Checked In",
                    bookingId = booking.Id,
                    reference = booking.BookingReference,
                    guest = booking.Guest.FullName,
                    property = booking.Property.Title,
                    message = $"Verified! {booking.Guest.FullName} is now Checked In."
                });
            }

            return Json(new
            {
                ok = true,
                action = booking.Status,
                bookingId = booking.Id,
                reference = booking.BookingReference,
                guest = booking.Guest.FullName,
                property = booking.Property.Title,
                message = $"Booking {booking.BookingReference} found — current status is {booking.Status}."
            });
        }

        // =====================================================================
        // Guest Service Requests & Issue Tracking
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> ServiceRequests(int? bookingId)
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            string role = _helper.GetCurrentUserRole();

            var query = _db.ServiceRequests
                .Include(sr => sr.Booking).ThenInclude(b => b.Property)
                .Include(sr => sr.Guest)
                .AsQueryable();

            if (role == "Guest")
                query = query.Where(sr => sr.GuestId == uid);
            else if (role == "Host")
                query = query.Where(sr => sr.Booking.Property.HostId == uid);

            if (bookingId.HasValue)
                query = query.Where(sr => sr.BookingId == bookingId.Value);

            ViewBag.GuestBookings = await _db.Bookings
                .Include(b => b.Property)
                .Where(b => b.GuestId == uid && (b.Status == "Confirmed" || b.Status == "CheckedIn" || b.Status == "Completed"))
                .OrderByDescending(b => b.CheckInDate)
                .ToListAsync();

            var list = await query.OrderByDescending(sr => sr.CreatedAt).ToListAsync();
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRequest(ServiceRequestCreateViewModel vm)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == vm.BookingId);

            if (booking == null) return NotFound();
            int uid = _helper.GetCurrentUserId()!.Value;
            if (booking.GuestId != uid && _helper.GetCurrentUserRole() != "Admin")
                return Forbid();
            if (booking.Status is not ("Confirmed" or "CheckedIn" or "Completed"))
            {
                TempData["Error"] = $"Service requests are only allowed for confirmed, checked-in, or completed stays (current: {booking.Status}).";
                return RedirectToAction(nameof(ServiceRequests));
            }

            string? savedPhoto = null;
            if (vm.PhotoProofFile != null)
            {
                var err = _helper.ValidatePhoto(vm.PhotoProofFile);
                if (err != null) ModelState.AddModelError(nameof(vm.PhotoProofFile), err);
                else savedPhoto = _helper.SavePhoto(vm.PhotoProofFile, "issues");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(ServiceRequests), new { bookingId = vm.BookingId });
            }

            var req = new ServiceRequest
            {
                BookingId = vm.BookingId,
                GuestId = uid,
                Category = vm.Category,
                IssueTitle = vm.IssueTitle.Trim(),
                Description = vm.Description.Trim(),
                Priority = vm.Priority,
                PhotoProof = savedPhoto,
                Status = vm.Category == "Safety / Dispute" ? "Escalated" : "Open",
                CreatedAt = DateTime.Now
            };

            _db.ServiceRequests.Add(req);
            await _db.SaveChangesAsync();

            _helper.CreateNotification(_db, booking.Property.HostId, $"New {req.Priority} Service Request",
                $"{booking.Property.Title}: {req.IssueTitle}", "ServiceRequest", "/Stay/ServiceRequests");

            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "InStay", "ServiceRequestCreated",
                $"Logged {req.Priority} request '{req.IssueTitle}' on booking {booking.BookingReference}.",
                req.Priority == "Urgent" ? "Warning" : "Info", "ServiceRequest", req.Id);

            TempData["Success"] = "Service request logged! Your host has been notified.";
            return RedirectToAction(nameof(ServiceRequests));
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRequest(int id, string status, string? hostResponse, decimal repairCost)
        {
            var req = await _db.ServiceRequests
                .Include(sr => sr.Booking).ThenInclude(b => b.Property)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (req == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(req.Booking.Property.HostId)) return Forbid();

            req.Status = status is "Open" or "InProgress" or "Resolved" or "Escalated" ? status : req.Status;
            req.HostResponse = hostResponse?.Trim();
            req.RepairCost = Math.Max(0m, repairCost);
            if (req.Status == "Resolved") req.ResolvedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            _helper.CreateNotification(_db, req.GuestId, $"Service Request {req.Status}",
                $"Update on '{req.IssueTitle}': {req.HostResponse ?? "Status updated."}",
                "ServiceRequest", "/Stay/ServiceRequests");

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "InStay", "ServiceRequestUpdated",
                $"Updated service request #{id} to {req.Status}.", "Info", "ServiceRequest", id);

            TempData["Success"] = $"Request #{id} updated to {req.Status}.";
            return RedirectToAction(nameof(ServiceRequests));
        }

        // =====================================================================
        // Real-Time In-Stay Guest–Host Messaging (SignalR + DB Persistence)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Chat(int id)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .Include(b => b.Messages.OrderBy(m => m.SentAt)).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();
            int uid = _helper.GetCurrentUserId()!.Value;
            if (booking.GuestId != uid && booking.Property.HostId != uid && _helper.GetCurrentUserRole() != "Admin")
                return Forbid();

            // Mark incoming messages read
            foreach (var msg in booking.Messages.Where(m => m.SenderId != uid && !m.IsRead))
            {
                msg.IsRead = true;
            }
            await _db.SaveChangesAsync();

            return View(booking);
        }

        // Fallback non-JS POST for Chat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int bookingId, string messageText)
        {
            var booking = await _db.Bookings.Include(b => b.Property).FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null) return NotFound();
            int uid = _helper.GetCurrentUserId()!.Value;
            if (booking.GuestId != uid && booking.Property.HostId != uid && _helper.GetCurrentUserRole() != "Admin")
                return Forbid();

            if (!string.IsNullOrWhiteSpace(messageText))
            {
                string clean = messageText.Trim();
                if (clean.Length > 1500) clean = clean[..1500];
                _db.BookingMessages.Add(new BookingMessage
                {
                    BookingId = bookingId,
                    SenderId = uid,
                    MessageText = clean,
                    SentAt = DateTime.Now
                });
                int recipientId = uid == booking.GuestId ? booking.Property.HostId : booking.GuestId;
                if (recipientId != uid)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = recipientId,
                        Title = $"New Message from {_helper.GetCurrentUserFullName()}",
                        Message = clean.Length > 80 ? clean[..80] + "..." : clean,
                        Type = "Booking",
                        LinkUrl = $"/Stay/Chat/{bookingId}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Chat), new { id = bookingId });
        }

        // =====================================================================
        // Notification Center & Automated Arrival/Departure Reminders
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var list = await _db.Notifications
                .Where(n => n.UserId == uid)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            int count = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);
            return Json(new { count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var unread = await _db.Notifications.Where(n => n.UserId == uid && !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _db.SaveChangesAsync();
            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction(nameof(Notifications));
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendUpcomingReminders()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";
            var horizon = DateTime.Today.AddDays(5);

            var upcoming = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .Where(b => (isAdmin || b.Property.HostId == uid) &&
                            ((b.Status == "Confirmed" && b.CheckInDate <= horizon) ||
                             (b.Status == "CheckedIn" && b.CheckOutDate <= horizon)))
                .ToListAsync();

            int sentCount = 0;
            foreach (var b in upcoming)
            {
                bool isArrival = b.Status == "Confirmed";
                string evt = isArrival ? "Check-In Arrival" : "Check-Out Departure";
                string dateStr = (isArrival ? b.CheckInDate : b.CheckOutDate).ToString("dd MMM yyyy");

                _helper.CreateNotification(_db, b.GuestId, $"Upcoming {evt}",
                    $"Reminder: {evt} for {b.Property.Title} ({b.BookingReference}) on {dateStr}.",
                    isArrival ? "Arrival" : "Departure", $"/Booking/Details/{b.Id}");

                _helper.CreateNotification(_db, b.Property.HostId, $"Host {evt} Alert",
                    $"{b.Guest.FullName} — {evt} at {b.Property.Title} on {dateStr}.",
                    isArrival ? "Arrival" : "Departure", "/Stay/Index");

                await _email.SendStayReminderAsync(b.Guest.Email, b.Guest.FullName, evt, b.BookingReference, b.Property.Title, dateStr);
                sentCount++;
            }

            TempData["Success"] = $"Dispatched arrival/departure reminders for {sentCount} upcoming stay(s).";
            return RedirectToAction(nameof(Index));
        }
    }
}
