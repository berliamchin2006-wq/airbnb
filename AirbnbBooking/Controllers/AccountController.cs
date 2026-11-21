using System.Security.Cryptography;
using AirbnbBooking.Models;
using AirbnbBooking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 1: User & Identity Management
    /// Manual Cookie Auth, Rotating Session Tokens, RBAC/ABAC, Email OTP Verification,
    /// 3-Strike Lockout, Password Reset, Anti-Bot CAPTCHA, Webcam Profile Photo & AES-256 Bank Details.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;
        private readonly EmailService _email;
        private readonly IConfiguration _config;

        public AccountController(DB db, Helper helper, EmailService email, IConfiguration config)
        {
            _db = db;
            _helper = helper;
            _email = email;
            _config = config;
        }

        // [Remote] AJAX Validation for unique email during registration
        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> CheckEmailAvailable(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return Json(true);
            bool exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            return Json(exists ? "This email address is already registered." : true);
        }

        // Refreshable Self-Coded CAPTCHA endpoint
        [HttpGet]
        public IActionResult RefreshCaptcha()
        {
            string question = _helper.GenerateCaptchaChallenge();
            return Json(new { question });
        }

        // =====================================================================
        // Registration + Email Verification (OTP + Link via Gmail SMTP)
        // =====================================================================
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Catalog", "Property");

            ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            if (!_helper.VerifyCaptcha(vm.CaptchaAnswer))
            {
                ModelState.AddModelError(nameof(vm.CaptchaAnswer), "Incorrect CAPTCHA answer. Please try again.");
            }

            string normalizedEmail = vm.Email.Trim().ToLowerInvariant();
            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
                return View(vm);
            }

            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            var user = new User
            {
                FullName = vm.FullName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = vm.PhoneNumber.Trim(),
                Role = vm.Role == "Host" ? "Host" : "Guest",
                PasswordHash = _helper.HashPassword(vm.Password),
                ProfilePhoto = "default.svg",
                IsEmailVerified = false,
                EmailVerificationToken = otp,
                EmailVerificationExpiry = DateTime.Now.AddMinutes(15),
                IdentityStatus = "Unverified",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            string verifyUrl = Url.Action("VerifyEmailLink", "Account", new { email = user.Email, code = otp }, Request.Scheme) ?? "";
            bool sent = await _email.SendVerificationCodeAsync(user.Email, otp, user.FullName, verifyUrl);

            _helper.LogAudit(_db, user.Id, user.Email, "UserIdentity", "UserRegistered",
                $"New {user.Role} account registered. Verification email sent={sent}.", "Info", "User", user.Id);

            TempData["Success"] = sent
                ? $"Account created! We emailed a 6-digit verification code to {user.Email}."
                : $"Account created! SMTP unavailable in this environment — Demo OTP: {otp}";
            TempData["DemoOtp"] = sent ? null : otp;

            return RedirectToAction(nameof(VerifyEmail), new { email = user.Email });
        }

        [HttpGet]
        public IActionResult VerifyEmail(string? email)
        {
            return View(new VerifyEmailViewModel { Email = email ?? "" });
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmailLink(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                TempData["Error"] = "Invalid verification link.";
                return RedirectToAction(nameof(Login));
            }
            return await VerifyEmail(new VerifyEmailViewModel { Email = email, Code = code });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == vm.Email.Trim().ToLower());
            if (user == null)
            {
                ModelState.AddModelError("", "Account not found.");
                return View(vm);
            }

            if (user.IsEmailVerified)
            {
                TempData["Success"] = "Your email is already verified. Please sign in.";
                return RedirectToAction(nameof(Login));
            }

            if (user.EmailVerificationToken != vm.Code.Trim() ||
                (user.EmailVerificationExpiry.HasValue && user.EmailVerificationExpiry.Value < DateTime.Now))
            {
                ModelState.AddModelError(nameof(vm.Code), "Invalid or expired verification code.");
                return View(vm);
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationExpiry = null;
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, user.Id, user.Email, "UserIdentity", "EmailVerified",
                "User successfully activated account via OTP/link.", "Info", "User", user.Id);

            TempData["Success"] = "Email verified! You can now sign in to StayNest.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string email)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == (email ?? "").Trim().ToLower());
            if (user == null || user.IsEmailVerified)
            {
                TempData["Info"] = "If an unverified account exists for that email, a new code has been sent.";
                return RedirectToAction(nameof(VerifyEmail), new { email });
            }

            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            user.EmailVerificationToken = otp;
            user.EmailVerificationExpiry = DateTime.Now.AddMinutes(15);
            await _db.SaveChangesAsync();

            string verifyUrl = Url.Action("VerifyEmailLink", "Account", new { email = user.Email, code = otp }, Request.Scheme) ?? "";
            bool sent = await _email.SendVerificationCodeAsync(user.Email, otp, user.FullName, verifyUrl);

            TempData["Success"] = sent
                ? $"A fresh verification code was sent to {user.Email}."
                : $"Fresh code generated (SMTP fallback) — Demo OTP: {otp}";
            TempData["DemoOtp"] = sent ? null : otp;

            return RedirectToAction(nameof(VerifyEmail), new { email = user.Email });
        }

        // =====================================================================
        // Login (Manual Cookie Auth + 3-Strike Lockout + CAPTCHA + Rotating Token)
        // =====================================================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Catalog", "Property");

            ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!_helper.VerifyCaptcha(vm.CaptchaAnswer))
            {
                ModelState.AddModelError(nameof(vm.CaptchaAnswer), "Incorrect CAPTCHA answer. Please solve the new challenge.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
                return View(vm);
            }

            string email = vm.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user == null)
            {
                _helper.LogAudit(_db, null, email, "Security", "LoginFailed", "Attempted login for non-existent email.", "Warning");
                ModelState.AddModelError("", "Invalid email or password.");
                ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
                return View(vm);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "This account has been suspended by an administrator.");
                ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
                return View(vm);
            }

            // Check 3-Strike Temporary Login Lockout
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
            {
                var minsLeft = Math.Ceiling((user.LockoutEnd.Value - DateTime.Now).TotalMinutes);
                ViewBag.LockoutSeconds = (int)(user.LockoutEnd.Value - DateTime.Now).TotalSeconds;
                ModelState.AddModelError("", $"Account temporarily locked due to failed login attempts. Try again in {minsLeft} minute(s).");
                ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
                return View(vm);
            }

            if (!_helper.VerifyPassword(user.PasswordHash, vm.Password))
            {
                int maxFails = int.TryParse(_config["Security:MaxFailedLoginAttempts"], out int mf) ? mf : 3;
                int lockMins = int.TryParse(_config["Security:LockoutMinutes"], out int lm) ? lm : 15;

                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= maxFails)
                {
                    user.LockoutEnd = DateTime.Now.AddMinutes(lockMins);
                    user.FailedLoginAttempts = 0;
                    await _db.SaveChangesAsync();

                    _helper.LogAudit(_db, user.Id, user.Email, "Security", "AccountLockedOut",
                        $"Account temporarily locked for {lockMins} minutes after {maxFails} consecutive failed logins.", "Critical", "User", user.Id);

                    ViewBag.LockoutSeconds = lockMins * 60;
                    ModelState.AddModelError("", $"Too many failed attempts ({maxFails}). Account locked for {lockMins} minutes.");
                }
                else
                {
                    int remaining = maxFails - user.FailedLoginAttempts;
                    await _db.SaveChangesAsync();

                    _helper.LogAudit(_db, user.Id, user.Email, "Security", "LoginFailed",
                        $"Invalid password. Attempt {user.FailedLoginAttempts}/{maxFails}.", "Warning", "User", user.Id);

                    ModelState.AddModelError("", $"Invalid email or password. {remaining} attempt(s) remaining before temporary lockout.");
                }

                ViewBag.CaptchaQuestion = _helper.GenerateCaptchaChallenge();
                return View(vm);
            }

            if (!user.IsEmailVerified)
            {
                TempData["Error"] = "Please verify your email address before signing in.";
                return RedirectToAction(nameof(VerifyEmail), new { email = user.Email });
            }

            // Successful login -> reset lockout counters, sign in manual cookie & issue rotating session token
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _db.SaveChangesAsync();

            await _helper.SignInAsync(user.Id, user.Email, user.FullName, user.Role, vm.RememberMe);
            await _helper.IssueSessionTokenAsync(_db, user.Id, vm.RememberMe);

            // Sync saved theme & currency cookies
            Response.Cookies.Append("StayNest_Theme", user.ThemePreference, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30) });
            Response.Cookies.Append("StayNest_Currency", user.PreferredCurrency, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30) });

            _helper.LogAudit(_db, user.Id, user.Email, "UserIdentity", "LoginSuccess",
                $"User signed in ({user.Role}). Rotating session token issued.", "Info", "User", user.Id);

            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return user.Role switch
            {
                "Admin" => RedirectToAction("Admin", "Dashboard"),
                "Host" => RedirectToAction("Host", "Dashboard"),
                _ => RedirectToAction("Catalog", "Property")
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var uid = _helper.GetCurrentUserId();
            var email = _helper.GetCurrentUserEmail();
            await _helper.SignOutAsync(_db);
            _helper.LogAudit(_db, uid, email, "UserIdentity", "Logout", "User signed out and revoked active refresh token.", "Info", "User", uid);
            TempData["Success"] = "You have been signed out securely.";
            return RedirectToAction("Catalog", "Property");
        }

        // =====================================================================
        // Forgot & Reset Password (Email Token Link via Gmail SMTP)
        // =====================================================================
        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == vm.Email.Trim().ToLower());
            if (user != null)
            {
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                user.ResetToken = token;
                user.ResetTokenExpiry = DateTime.Now.AddHours(1);
                await _db.SaveChangesAsync();

                string resetUrl = Url.Action("ResetPassword", "Account", new { email = user.Email, token }, Request.Scheme) ?? "";
                bool sent = await _email.SendPasswordResetEmailAsync(user.Email, user.FullName, resetUrl);

                _helper.LogAudit(_db, user.Id, user.Email, "Security", "PasswordResetRequested",
                    $"Password reset token issued. Email sent={sent}.", "Warning", "User", user.Id);

                if (!sent)
                {
                    TempData["DemoResetUrl"] = resetUrl;
                }
            }

            TempData["Success"] = "If an account exists for that email, a password reset link has been sent.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            return View(new ResetPasswordViewModel { Email = email ?? "", Token = token ?? "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == vm.Email.Trim().ToLower());
            if (user == null || user.ResetToken != vm.Token || !user.ResetTokenExpiry.HasValue || user.ResetTokenExpiry.Value < DateTime.Now)
            {
                ModelState.AddModelError("", "This password reset link is invalid or has expired.");
                return View(vm);
            }

            user.PasswordHash = _helper.HashPassword(vm.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            // Revoke all old session tokens on password reset
            var tokens = await _db.UserSessionTokens.Where(t => t.UserId == user.Id && !t.IsRevoked).ToListAsync();
            foreach (var t in tokens) t.IsRevoked = true;

            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, user.Id, user.Email, "Security", "PasswordResetCompleted",
                "Password changed via reset token; all previous session tokens revoked.", "Warning", "User", user.Id);

            TempData["Success"] = "Password reset successfully! Please sign in with your new password.";
            return RedirectToAction(nameof(Login));
        }

        // =====================================================================
        // Profile, Webcam/Crop Photo, Host Identity Doc & AES-256 Bank Details
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var user = await _db.Users.FindAsync(uid);
            if (user == null) return RedirectToAction(nameof(Login));

            string decryptedAcct = _helper.Decrypt(user.BankAccountNumberEncrypted ?? "");

            var vm = new ProfileViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
                CurrentProfilePhoto = user.ProfilePhoto,
                IsEmailVerified = user.IsEmailVerified,
                IdentityStatus = user.IdentityStatus,
                IdentityNotes = user.IdentityNotes,
                CurrentIdDocumentPhoto = user.IdDocumentPhoto,
                MaskedBankAccount = _helper.MaskBankAccount(decryptedAcct),
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                BankName = user.BankName,
                BankHolderName = user.BankHolderName,
                ThemePreference = user.ThemePreference,
                PreferredCurrency = user.PreferredCurrency
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel vm)
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var user = await _db.Users.FindAsync(uid);
            if (user == null) return RedirectToAction(nameof(Login));

            if (vm.ProfilePhotoFile != null)
            {
                var photoErr = _helper.ValidatePhoto(vm.ProfilePhotoFile);
                if (photoErr != null) ModelState.AddModelError(nameof(vm.ProfilePhotoFile), photoErr);
            }

            if (vm.IdDocumentFile != null)
            {
                var docErr = _helper.ValidatePhoto(vm.IdDocumentFile);
                if (docErr != null) ModelState.AddModelError(nameof(vm.IdDocumentFile), docErr);
            }

            if (!ModelState.IsValid)
            {
                vm.Email = user.Email;
                vm.Role = user.Role;
                vm.CurrentProfilePhoto = user.ProfilePhoto;
                vm.IdentityStatus = user.IdentityStatus;
                vm.MaskedBankAccount = _helper.MaskBankAccount(_helper.Decrypt(user.BankAccountNumberEncrypted ?? ""));
                return View(vm);
            }

            user.FullName = vm.FullName.Trim();
            user.PhoneNumber = vm.PhoneNumber.Trim();
            user.Bio = vm.Bio?.Trim();
            user.ThemePreference = vm.ThemePreference == "dark" ? "dark" : "light";
            user.PreferredCurrency = vm.PreferredCurrency is "USD" or "SGD" ? vm.PreferredCurrency : "MYR";

            // Profile photo: prefer cropped webcam/canvas base64 if supplied, otherwise uploaded file
            if (!string.IsNullOrWhiteSpace(vm.WebcamPhotoBase64))
            {
                user.ProfilePhoto = _helper.SaveBase64Photo(vm.WebcamPhotoBase64, "profiles");
            }
            else if (vm.ProfilePhotoFile != null)
            {
                user.ProfilePhoto = _helper.SavePhoto(vm.ProfilePhotoFile, "profiles");
            }

            // Profile / Host Identity Verification Document Upload
            if (vm.IdDocumentFile != null)
            {
                user.IdDocumentPhoto = _helper.SavePhoto(vm.IdDocumentFile, "documents");
                user.IdentityStatus = "PendingApproval";
                user.IdentityNotes = $"Submitted on {DateTime.Now:dd MMM yyyy HH:mm} for admin verification.";

                // Notify admins
                var admins = await _db.Users.Where(u => u.Role == "Admin").Select(u => u.Id).ToListAsync();
                foreach (var aid in admins)
                {
                    _helper.CreateNotification(_db, aid, "Identity Document Uploaded",
                        $"{user.FullName} ({user.Role}) submitted ID verification.", "Security", "/Security/Users");
                }
            }

            // Host Banking Details (AES-256 Encrypted at Rest)
            if (!string.IsNullOrWhiteSpace(vm.BankName)) user.BankName = vm.BankName.Trim();
            if (!string.IsNullOrWhiteSpace(vm.BankHolderName)) user.BankHolderName = vm.BankHolderName.Trim();
            if (!string.IsNullOrWhiteSpace(vm.BankAccountNumber))
            {
                user.BankAccountNumberEncrypted = _helper.Encrypt(vm.BankAccountNumber.Trim());
                _helper.LogAudit(_db, user.Id, user.Email, "Security", "BankAccountEncryptedUpdated",
                    "Host updated payout bank account (stored encrypted with AES-256).", "Warning", "User", user.Id);
            }

            await _db.SaveChangesAsync();

            // Refresh auth cookie full name & preference cookies
            await _helper.SignInAsync(user.Id, user.Email, user.FullName, user.Role, true);
            Response.Cookies.Append("StayNest_Theme", user.ThemePreference, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30) });
            Response.Cookies.Append("StayNest_Currency", user.PreferredCurrency, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30) });

            _helper.LogAudit(_db, user.Id, user.Email, "UserIdentity", "ProfileUpdated", "Profile details updated.", "Info", "User", user.Id);
            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Profile));
            }

            int uid = _helper.GetCurrentUserId()!.Value;
            var user = await _db.Users.FindAsync(uid);
            if (user == null) return RedirectToAction(nameof(Login));

            if (!_helper.VerifyPassword(user.PasswordHash, vm.CurrentPassword))
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction(nameof(Profile));
            }

            user.PasswordHash = _helper.HashPassword(vm.NewPassword);
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, user.Id, user.Email, "Security", "PasswordChanged", "User changed password from profile.", "Warning", "User", user.Id);
            TempData["Success"] = "Your password has been updated securely.";
            return RedirectToAction(nameof(Profile));
        }

        // =====================================================================
        // Active Rotating Sessions Viewer & Revocation
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Sessions()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var sessions = await _db.UserSessionTokens
                .Where(t => t.UserId == uid)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync();
            return View(sessions);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSession(int id)
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var token = await _db.UserSessionTokens.FindAsync(id);
            if (token == null || !_helper.IsOwnerOrAdmin(token.UserId))
                return Forbid();

            token.IsRevoked = true;
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "Security", "SessionRevoked",
                $"Revoked rotating session token #{id}.", "Info", "UserSessionToken", id);
            TempData["Success"] = "Session revoked.";
            return RedirectToAction(nameof(Sessions));
        }

        // AJAX Preference Switcher (Theme / Currency)
        [HttpPost]
        public async Task<IActionResult> SetPreferences(string? theme, string? currency)
        {
            if (theme is "light" or "dark")
                Response.Cookies.Append("StayNest_Theme", theme, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30) });

            if (currency is "MYR" or "USD" or "SGD")
                Response.Cookies.Append("StayNest_Currency", currency, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30) });

            var uid = _helper.GetCurrentUserId();
            if (uid.HasValue)
            {
                var user = await _db.Users.FindAsync(uid.Value);
                if (user != null)
                {
                    if (theme is "light" or "dark") user.ThemePreference = theme;
                    if (currency is "MYR" or "USD" or "SGD") user.PreferredCurrency = currency;
                    await _db.SaveChangesAsync();
                }
            }

            return Json(new { ok = true, theme, currency });
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}
