using System.Text;
using AirbnbBooking.Middleware;
using AirbnbBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 8: Security, Audit & Database Infrastructure
    /// Comprehensive Audit Log Explorer (AJAX), User & Identity Verification Moderation (Batch),
    /// Listing Moderation (Batch), Promo Codes, Disputes/Flagged Reviews & System Health Telemetry.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class SecurityController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly RateLimitTelemetry _rateLimit;

        public SecurityController(DB db, Helper helper, RateLimitTelemetry rateLimit)
        {
            _db = db;
            _helper = helper;
            _rateLimit = rateLimit;
        }

        // =====================================================================
        // Comprehensive Audit Log Explorer (AJAX Search/Filter + CSV Export)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> AuditLogs(string? search, string? module, string? severity)
        {
            var logs = await QueryAuditLogsAsync(search, module, severity);
            ViewBag.Search = search;
            ViewBag.Module = module;
            ViewBag.Severity = severity;
            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> AuditLogsAjax(string? search, string? module, string? severity)
        {
            var logs = await QueryAuditLogsAsync(search, module, severity);
            return PartialView("_AuditLogRows", logs);
        }

        private async Task<List<AuditLog>> QueryAuditLogsAsync(string? search, string? module, string? severity)
        {
            var query = _db.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(a =>
                    a.UserEmail.ToLower().Contains(s) ||
                    a.Action.ToLower().Contains(s) ||
                    a.Details.ToLower().Contains(s) ||
                    (a.IpAddress != null && a.IpAddress.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(module))
                query = query.Where(a => a.Module == module);

            if (!string.IsNullOrWhiteSpace(severity))
                query = query.Where(a => a.Severity == severity);

            return await query.OrderByDescending(a => a.Timestamp).Take(150).ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> ExportAuditCsv()
        {
            var logs = await _db.AuditLogs.OrderByDescending(a => a.Timestamp).Take(500).ToListAsync();
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Severity,Module,UserEmail,Action,IP,Details");
            foreach (var a in logs)
            {
                sb.AppendLine($"{a.Timestamp:yyyy-MM-dd HH:mm:ss},{a.Severity},{a.Module},{a.UserEmail},{a.Action},{a.IpAddress},\"{a.Details.Replace("\"", "'")}\"");
            }
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"StayNest_AuditLogs_{DateTime.Now:yyyyMMdd}.csv");
        }

        // =====================================================================
        // User & Identity Verification Management + Batch Actions
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Users(string? role, string? search)
        {
            var query = _db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Role == role);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
            }
            ViewBag.Role = role;
            ViewBag.Search = search;
            var users = await query.OrderByDescending(u => u.IdentityStatus == "PendingApproval").ThenByDescending(u => u.CreatedAt).ToListAsync();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyIdentity(int id, string decision, string? notes)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IdentityStatus = decision == "Verified" ? "Verified" : "Rejected";
            user.IdentityNotes = notes?.Trim();
            if (user.IdentityStatus == "Verified") user.IdentityApprovedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _helper.CreateNotification(_db, user.Id, $"Identity Verification {user.IdentityStatus}",
                user.IdentityStatus == "Verified"
                    ? "Congratulations! Your profile identity has been verified by StayNest."
                    : $"Your ID verification was declined. Notes: {user.IdentityNotes ?? "Please re-upload a clear document."}",
                "Security", "/Account/Profile");

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Security", "IdentityModerated",
                $"Admin set identity status of {user.Email} to {user.IdentityStatus}.", "Warning", "User", user.Id);

            TempData["Success"] = $"{user.FullName} identity marked as {user.IdentityStatus}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            user.IsActive = true;
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Security", "UserUnlocked",
                $"Admin unlocked account {user.Email}.", "Warning", "User", user.Id);

            TempData["Success"] = $"Account {user.Email} unlocked and reactivated.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchUserAction(int[] userIds, string actionType)
        {
            if (userIds == null || userIds.Length == 0)
            {
                TempData["Error"] = "Select at least one user.";
                return RedirectToAction(nameof(Users));
            }

            int adminId = _helper.GetCurrentUserId()!.Value;
            var users = await _db.Users.Where(u => userIds.Contains(u.Id) && u.Id != adminId).ToListAsync();

            foreach (var u in users)
            {
                switch (actionType)
                {
                    case "verify":
                        u.IdentityStatus = "Verified";
                        u.IdentityApprovedAt = DateTime.Now;
                        break;
                    case "unlock":
                        u.FailedLoginAttempts = 0;
                        u.LockoutEnd = null;
                        u.IsActive = true;
                        break;
                    case "suspend":
                        u.IsActive = false;
                        break;
                }
            }

            await _db.SaveChangesAsync();
            _helper.LogAudit(_db, adminId, _helper.GetCurrentUserEmail(), "Security", "BatchUserModeration",
                $"Admin applied '{actionType}' to {users.Count} user(s).", "Warning");

            TempData["Success"] = $"Applied '{actionType}' to {users.Count} user(s).";
            return RedirectToAction(nameof(Users));
        }

        // =====================================================================
        // Admin Property Listing Moderation + Batch Approve/Suspend
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Listings(string? search, string? status)
        {
            var query = _db.Properties
                .Include(p => p.Host)
                .Include(p => p.Photos)
                .Include(p => p.Bookings)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(p => p.Title.ToLower().Contains(s) || p.City.ToLower().Contains(s) || p.Host.FullName.ToLower().Contains(s));
            }
            if (status == "active") query = query.Where(p => p.IsActive);
            else if (status == "hidden") query = query.Where(p => !p.IsActive);

            ViewBag.Search = search;
            ViewBag.Status = status;
            var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchListingAction(int[] listingIds, string actionType)
        {
            if (listingIds == null || listingIds.Length == 0)
            {
                TempData["Error"] = "Select at least one listing.";
                return RedirectToAction(nameof(Listings));
            }

            var props = await _db.Properties.Where(p => listingIds.Contains(p.Id)).ToListAsync();
            foreach (var p in props)
            {
                if (actionType == "approve") p.IsActive = true;
                else if (actionType == "suspend") p.IsActive = false;
            }
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Security", "BatchListingModeration",
                $"Admin applied '{actionType}' to {props.Count} property listing(s).", "Warning");

            TempData["Success"] = $"Applied '{actionType}' to {props.Count} listing(s).";
            return RedirectToAction(nameof(Listings));
        }

        // =====================================================================
        // Promo Code Administration
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> PromoCodes()
        {
            var codes = await _db.PromoCodes.OrderByDescending(c => c.IsActive).ThenByDescending(c => c.Id).ToListAsync();
            return View(codes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePromoCode(PromoCodeFormViewModel vm)
        {
            vm.Code = (vm.Code ?? "").Trim().ToUpperInvariant();
            if (vm.DiscountType == "Percent" && vm.DiscountValue > 100m)
            {
                ModelState.AddModelError(nameof(vm.DiscountValue), "Percentage discount cannot exceed 100%.");
            }
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(PromoCodes));
            }

            if (await _db.PromoCodes.AnyAsync(c => c.Code == vm.Code && c.Id != vm.Id))
            {
                TempData["Error"] = $"Promo code '{vm.Code}' already exists.";
                return RedirectToAction(nameof(PromoCodes));
            }

            var entity = new PromoCode
            {
                Code = vm.Code,
                Description = vm.Description.Trim(),
                DiscountType = vm.DiscountType == "Fixed" ? "Fixed" : "Percent",
                DiscountValue = vm.DiscountValue,
                MinSpend = vm.MinSpend,
                MaxUses = vm.MaxUses,
                ExpiryDate = vm.ExpiryDate,
                IsActive = vm.IsActive
            };
            _db.PromoCodes.Add(entity);
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Payment", "PromoCodeCreated",
                $"Created promo code {entity.Code} ({entity.DiscountType}: {entity.DiscountValue}).", "Info", "PromoCode", entity.Id);

            TempData["Success"] = $"Promo code {entity.Code} created.";
            return RedirectToAction(nameof(PromoCodes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePromoCode(int id)
        {
            var code = await _db.PromoCodes.FindAsync(id);
            if (code == null) return NotFound();
            code.IsActive = !code.IsActive;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Promo code {code.Code} is now {(code.IsActive ? "Active" : "Disabled")}.";
            return RedirectToAction(nameof(PromoCodes));
        }

        // =====================================================================
        // Disputes & Flagged Reviews Moderation
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Disputes()
        {
            ViewBag.FlaggedReviews = await _db.Reviews
                .Include(r => r.Property)
                .Include(r => r.Guest)
                .Where(r => r.IsFlagged)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var tickets = await _db.ServiceRequests
                .Include(sr => sr.Booking).ThenInclude(b => b.Property)
                .Include(sr => sr.Guest)
                .OrderBy(sr => sr.Status == "Escalated" ? 0 : 1)
                .ThenByDescending(sr => sr.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearFlaggedReview(int id)
        {
            var r = await _db.Reviews.FindAsync(id);
            if (r == null) return NotFound();
            r.IsFlagged = false;
            r.FlagReason = null;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Review flag dismissed.";
            return RedirectToAction(nameof(Disputes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFlaggedReview(int id)
        {
            var r = await _db.Reviews.FindAsync(id);
            if (r == null) return NotFound();
            int pid = r.PropertyId;
            _db.Reviews.Remove(r);
            await _db.SaveChangesAsync();
            await _helper.SyncReviewVisibilityAndAggregateAsync(_db, pid);

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Security", "FlaggedReviewRemoved",
                $"Admin removed flagged review #{id} from Property #{pid}.", "Warning", "Review", id);

            TempData["Success"] = "Flagged review removed and property rating recalculated.";
            return RedirectToAction(nameof(Disputes));
        }

        // =====================================================================
        // System Health, EF Core Pool, AES-256 & Rate Limiter Telemetry
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> SystemHealth()
        {
            ViewBag.Telemetry = _rateLimit;
            ViewBag.DbCanConnect = await _db.Database.CanConnectAsync();
            ViewBag.ProviderName = _db.Database.ProviderName;
            ViewBag.EncryptedBankCount = await _db.Users.CountAsync(u => !string.IsNullOrEmpty(u.BankAccountNumberEncrypted));
            ViewBag.TotalAuditEntries = await _db.AuditLogs.CountAsync();
            ViewBag.LockedOutAccounts = await _db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTime.Now);

            var activeSessions = await _db.UserSessionTokens
                .Include(t => t.User)
                .Where(t => !t.IsRevoked && t.ExpiresAt > DateTime.Now)
                .OrderByDescending(t => t.CreatedAt)
                .Take(25)
                .ToListAsync();

            return View(activeSessions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAnySession(int id)
        {
            var token = await _db.UserSessionTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
            if (token == null) return NotFound();
            token.IsRevoked = true;
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Security", "AdminSessionRevoked",
                $"Admin forcefully revoked active session #{id} for {token.User.Email}.", "Warning", "UserSessionToken", id);

            TempData["Success"] = "Session revoked.";
            return RedirectToAction(nameof(SystemHealth));
        }
    }
}
