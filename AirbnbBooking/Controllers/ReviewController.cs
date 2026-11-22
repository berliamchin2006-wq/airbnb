using AirbnbBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 6: Review & Reputation Management
    /// Post-Stay Mutual Reviews (4-Criteria Guest -> Property + Host -> Guest),
    /// Double-Blind 14-Day Publishing Rule, Rating Aggregation, Profanity Filter & Moderation.
    /// </summary>
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly IConfiguration _config;

        public ReviewController(DB db, Helper helper, IConfiguration config)
        {
            _db = db;
            _helper = helper;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Pending()
        {
            await _helper.SyncReviewVisibilityAndAggregateAsync(_db);

            int uid = _helper.GetCurrentUserId()!.Value;
            string role = _helper.GetCurrentUserRole();

            var query = _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p.Photos)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.Guest)
                .Include(b => b.Review)
                .Where(b => b.Status == "Completed");

            if (role == "Guest")
                query = query.Where(b => b.GuestId == uid);
            else if (role == "Host")
                query = query.Where(b => b.Property.HostId == uid);

            var completed = await query.OrderByDescending(b => b.CheckOutDate).ToListAsync();
            return View(completed);
        }

        // Guest -> Property 4-Category Review Submission
        [HttpGet]
        public async Task<IActionResult> WriteGuestReview(int bookingId)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Review)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(booking.GuestId)) return Forbid();
            if (booking.Status != "Completed")
            {
                TempData["Error"] = "Reviews can only be submitted after check-out is completed.";
                return RedirectToAction(nameof(Pending));
            }

            if (booking.Review?.GuestSubmittedAt != null)
            {
                TempData["Info"] = "You have already submitted your review for this stay.";
                return RedirectToAction(nameof(Pending));
            }

            return View(new GuestReviewViewModel
            {
                BookingId = booking.Id,
                PropertyTitle = booking.Property.Title
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WriteGuestReview(GuestReviewViewModel vm)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Review)
                .FirstOrDefaultAsync(b => b.Id == vm.BookingId);

            if (booking == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(booking.GuestId)) return Forbid();

            string? savedPhoto = null;
            if (vm.ReviewPhoto != null)
            {
                var err = _helper.ValidatePhoto(vm.ReviewPhoto);
                if (err != null) ModelState.AddModelError(nameof(vm.ReviewPhoto), err);
                else savedPhoto = _helper.SavePhoto(vm.ReviewPhoto, "reviews");
            }

            if (!ModelState.IsValid)
            {
                vm.PropertyTitle = booking.Property.Title;
                return View(vm);
            }

            int blindDays = int.TryParse(_config["Security:ReviewBlindWindowDays"], out int d) ? d : 14;
            double overall = Math.Round((vm.CleanlinessRating + vm.AccuracyRating + vm.CommunicationRating + vm.LocationRating) / 4.0, 2);
            string sanitized = _helper.FilterProfanity(vm.Comment.Trim());

            var review = booking.Review;
            if (review == null)
            {
                review = new Review
                {
                    BookingId = booking.Id,
                    PropertyId = booking.PropertyId,
                    GuestId = booking.GuestId,
                    PublishDeadline = (booking.CheckedOutAt ?? DateTime.Now).AddDays(blindDays),
                    CreatedAt = DateTime.Now
                };
                _db.Reviews.Add(review);
            }

            review.CleanlinessRating = vm.CleanlinessRating;
            review.AccuracyRating = vm.AccuracyRating;
            review.CommunicationRating = vm.CommunicationRating;
            review.LocationRating = vm.LocationRating;
            review.OverallRating = overall;
            review.Comment = sanitized;
            review.PhotoPath = savedPhoto;
            review.GuestSubmittedAt = DateTime.Now;

            // Double-Blind Publishing Rule: publish immediately ONLY if Host already submitted OR 14-day window elapsed
            review.IsPublished = review.HostSubmittedAt != null || review.PublishDeadline <= DateTime.Now;

            await _db.SaveChangesAsync();
            await _helper.SyncReviewVisibilityAndAggregateAsync(_db, booking.PropertyId);

            _helper.CreateNotification(_db, booking.Property.HostId,
                review.IsPublished ? "Mutual Reviews Published!" : "Guest Left a Review — Submit Yours to Reveal!",
                review.IsPublished
                    ? $"Both reviews for {booking.Property.Title} are now live."
                    : $"Your guest submitted a review for {booking.Property.Title}. Submit your host review to unlock both immediately!",
                "Review", "/Review/Pending");

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Review", "GuestReviewSubmitted",
                $"Guest rated Property #{booking.PropertyId} ({overall:F2}/5). Published={review.IsPublished}.",
                "Info", "Review", review.Id);

            TempData["Success"] = review.IsPublished
                ? "Review submitted and published immediately because both parties have now reviewed!"
                : $"Review submitted! Under our Double-Blind Review Policy, it stays private until the host submits theirs or the 14-day window expires ({review.PublishDeadline:dd MMM yyyy}).";

            return RedirectToAction(nameof(Pending));
        }

        // Host -> Guest Mutual Review & Public Reply
        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> WriteHostReview(int bookingId)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .Include(b => b.Review)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(booking.Property.HostId)) return Forbid();
            if (booking.Status != "Completed")
            {
                TempData["Error"] = "Host reviews are available after check-out.";
                return RedirectToAction(nameof(Pending));
            }

            return View(new HostReviewViewModel
            {
                BookingId = booking.Id,
                GuestName = booking.Guest.FullName,
                PropertyTitle = booking.Property.Title,
                HostRatingForGuest = booking.Review?.HostRatingForGuest ?? 5,
                HostFeedbackForGuest = booking.Review?.HostFeedbackForGuest ?? "",
                HostReply = booking.Review?.HostReply
            });
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WriteHostReview(HostReviewViewModel vm)
        {
            var booking = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .Include(b => b.Review)
                .FirstOrDefaultAsync(b => b.Id == vm.BookingId);

            if (booking == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(booking.Property.HostId)) return Forbid();

            if (!ModelState.IsValid)
            {
                vm.GuestName = booking.Guest.FullName;
                vm.PropertyTitle = booking.Property.Title;
                return View(vm);
            }

            int blindDays = int.TryParse(_config["Security:ReviewBlindWindowDays"], out int d) ? d : 14;
            var review = booking.Review;
            if (review == null)
            {
                review = new Review
                {
                    BookingId = booking.Id,
                    PropertyId = booking.PropertyId,
                    GuestId = booking.GuestId,
                    PublishDeadline = (booking.CheckedOutAt ?? DateTime.Now).AddDays(blindDays),
                    CreatedAt = DateTime.Now
                };
                _db.Reviews.Add(review);
            }

            review.HostRatingForGuest = vm.HostRatingForGuest;
            review.HostFeedbackForGuest = _helper.FilterProfanity(vm.HostFeedbackForGuest.Trim());
            if (!string.IsNullOrWhiteSpace(vm.HostReply))
                review.HostReply = _helper.FilterProfanity(vm.HostReply.Trim());
            review.HostSubmittedAt = DateTime.Now;

            // Double-Blind Unlock: if guest already submitted, reveal BOTH immediately!
            if (review.GuestSubmittedAt != null)
            {
                review.IsPublished = true;
            }

            await _db.SaveChangesAsync();
            await _helper.SyncReviewVisibilityAndAggregateAsync(_db, booking.PropertyId);

            _helper.CreateNotification(_db, booking.GuestId,
                review.IsPublished ? "Both Mutual Reviews Are Live!" : "Host Rated Your Stay!",
                review.IsPublished
                    ? $"Both reviews for {booking.Property.Title} are now published."
                    : $"Your host reviewed your stay at {booking.Property.Title}. Submit yours to reveal both!",
                "Review", "/Review/Pending");

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Review", "HostReviewSubmitted",
                $"Host rated Guest #{booking.GuestId} ({vm.HostRatingForGuest}/5). Published={review.IsPublished}.",
                "Info", "Review", review.Id);

            TempData["Success"] = review.IsPublished
                ? "Host review saved — both mutual reviews are now published and property rating updated!"
                : "Host review saved! It will publish automatically once the guest submits theirs or the 14-day window closes.";

            return RedirectToAction(nameof(Pending));
        }

        // AJAX Helpful Vote (Session-guarded against repeated vote spam)
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> HelpfulVote(int id)
        {
            var review = await _db.Reviews.FindAsync(id);
            if (review == null || !review.IsPublished) return NotFound();

            var votedCsv = HttpContext.Session.GetString("VotedReviews") ?? "";
            var votedSet = votedCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            if (votedSet.Contains(id.ToString()))
            {
                return Json(new { ok = false, alreadyVoted = true, votes = review.HelpfulVotes });
            }

            review.HelpfulVotes++;
            await _db.SaveChangesAsync();

            votedSet.Add(id.ToString());
            HttpContext.Session.SetString("VotedReviews", string.Join(',', votedSet));

            return Json(new { ok = true, alreadyVoted = false, votes = review.HelpfulVotes });
        }

        // Flag Review for Admin Moderation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlagReview(int id, string reason)
        {
            var review = await _db.Reviews.FindAsync(id);
            if (review == null) return NotFound();

            review.IsFlagged = true;
            review.FlagReason = string.IsNullOrWhiteSpace(reason) ? "Flagged by user for moderation" : reason.Trim();
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Review", "ReviewFlagged",
                $"Review #{id} flagged: {review.FlagReason}.", "Warning", "Review", id);

            TempData["Success"] = "Review flagged for administrator review.";
            return RedirectToAction("Details", "Property", new { id = review.PropertyId });
        }
    }
}
