using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AirbnbBooking.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking
{
    public class Helper
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _http;
        private readonly PasswordHasher<object> _hasher = new();

        // 32-byte AES-256 encryption key and 16-byte IV for sensitive data at rest (Host bank accounts)
        private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("StayNest_Secure_Key_2026_TARUMT!");
        private static readonly byte[] EncryptionIV = Encoding.UTF8.GetBytes("StayNest_IV_2026");

        private const string SessionTokenCookieName = "StayNest_RefreshToken";

        private static readonly string[] ProfanityList =
        [
            "badword", "scam", "cheat", "bastard", "idiot", "damn", "hell", "fuck", "shit", "bitch", "asshole", "stupid"
        ];

        public Helper(IWebHostEnvironment env, IHttpContextAccessor http)
        {
            _env = env;
            _http = http;
        }

        // =====================================================================
        // 1. Password Hashing (Salted PBKDF2 — No ASP.NET Core Identity Tables)
        // =====================================================================
        public string HashPassword(string password)
        {
            return _hasher.HashPassword(0, password);
        }

        public bool VerifyPassword(string hash, string password)
        {
            var result = _hasher.VerifyHashedPassword(0, hash, password);
            return result == PasswordVerificationResult.Success ||
                   result == PasswordVerificationResult.SuccessRehashNeeded;
        }

        public string ComputeSha256(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }

        // =====================================================================
        // 2. Manual Cookie Authentication + Rotating Session / Refresh Tokens
        // =====================================================================
        public async Task SignInAsync(int userId, string email, string fullName, string role, bool rememberMe)
        {
            List<Claim> claims =
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, email),
                new Claim("FullName", fullName),
                new Claim(ClaimTypes.Role, role)
            ];

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(6)
            };

            if (_http.HttpContext != null)
            {
                await _http.HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    properties);
            }
        }

        public async Task IssueSessionTokenAsync(DB db, int userId, bool rememberMe)
        {
            if (_http.HttpContext == null) return;

            string rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            string tokenHash = ComputeSha256(rawToken);
            string ua = _http.HttpContext.Request.Headers.UserAgent.ToString();
            string device = string.IsNullOrWhiteSpace(ua) ? "Web Browser" : ua[..Math.Min(ua.Length, 160)];
            string ip = GetClientIp();

            var entity = new UserSessionToken
            {
                UserId = userId,
                TokenHash = tokenHash,
                DeviceInfo = device,
                IpAddress = ip,
                ExpiresAt = rememberMe ? DateTime.Now.AddDays(14) : DateTime.Now.AddHours(8),
                IsRevoked = false,
                CreatedAt = DateTime.Now
            };

            db.UserSessionTokens.Add(entity);
            await db.SaveChangesAsync();

            _http.HttpContext.Response.Cookies.Append(SessionTokenCookieName, rawToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = _http.HttpContext.Request.IsHttps,
                Expires = entity.ExpiresAt,
                IsEssential = true
            });
        }

        public async Task<bool> RotateSessionTokenIfNeededAsync(DB db)
        {
            var ctx = _http.HttpContext;
            if (ctx == null) return false;
            if (!ctx.Request.Cookies.TryGetValue(SessionTokenCookieName, out var rawToken) || string.IsNullOrWhiteSpace(rawToken))
                return false;

            string oldHash = ComputeSha256(rawToken);
            var existing = await db.UserSessionTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == oldHash);

            if (existing == null || existing.IsRevoked || existing.ExpiresAt < DateTime.Now || !existing.User.IsActive)
            {
                ctx.Response.Cookies.Delete(SessionTokenCookieName);
                return false;
            }

            // Rotate token: revoke old token & chain new token hash
            string newRawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            string newHash = ComputeSha256(newRawToken);

            existing.IsRevoked = true;
            existing.ReplacedByTokenHash = newHash;

            var replacement = new UserSessionToken
            {
                UserId = existing.UserId,
                TokenHash = newHash,
                DeviceInfo = existing.DeviceInfo,
                IpAddress = GetClientIp(),
                ExpiresAt = DateTime.Now.AddDays(14),
                IsRevoked = false,
                CreatedAt = DateTime.Now
            };
            db.UserSessionTokens.Add(replacement);
            await db.SaveChangesAsync();

            ctx.Response.Cookies.Append(SessionTokenCookieName, newRawToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = ctx.Request.IsHttps,
                Expires = replacement.ExpiresAt,
                IsEssential = true
            });

            await SignInAsync(existing.User.Id, existing.User.Email, existing.User.FullName, existing.User.Role, true);
            return true;
        }

        public async Task SignOutAsync(DB? db = null)
        {
            var ctx = _http.HttpContext;
            if (ctx == null) return;

            if (db != null && ctx.Request.Cookies.TryGetValue(SessionTokenCookieName, out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
            {
                string hash = ComputeSha256(rawToken);
                var token = await db.UserSessionTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
                if (token != null)
                {
                    token.IsRevoked = true;
                    await db.SaveChangesAsync();
                }
            }

            ctx.Response.Cookies.Delete(SessionTokenCookieName);
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        // =====================================================================
        // 3. RBAC / Resource-Based Access Control (ABAC) Identity Helpers
        // =====================================================================
        public int? GetCurrentUserId()
        {
            var idStr = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out int id) ? id : null;
        }

        public string GetCurrentUserEmail() => _http.HttpContext?.User.Identity?.Name ?? "Anonymous";

        public string GetCurrentUserRole() => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? "";

        public string GetCurrentUserFullName() => _http.HttpContext?.User.FindFirstValue("FullName") ?? "Guest";

        public bool IsOwnerOrAdmin(int resourceOwnerId)
        {
            var currentId = GetCurrentUserId();
            if (currentId == null) return false;
            return currentId.Value == resourceOwnerId || GetCurrentUserRole().Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public string GetClientIp()
        {
            return _http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }

        // =====================================================================
        // 4. Self-Coded Anti-Bot Math CAPTCHA (Session-Backed)
        // =====================================================================
        public string GenerateCaptchaChallenge()
        {
            int a = RandomNumberGenerator.GetInt32(2, 16);
            int b = RandomNumberGenerator.GetInt32(2, 12);
            bool add = RandomNumberGenerator.GetInt32(0, 2) == 0;
            if (!add && a < b) (a, b) = (b, a);

            int answer = add ? a + b : a - b;
            string question = add ? $"{a} + {b} = ?" : $"{a} − {b} = ?";

            _http.HttpContext?.Session.SetString("CaptchaAnswer", answer.ToString());
            _http.HttpContext?.Session.SetString("CaptchaQuestion", question);
            return question;
        }

        public bool VerifyCaptcha(string? submittedAnswer)
        {
            var expected = _http.HttpContext?.Session.GetString("CaptchaAnswer");
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(submittedAnswer))
                return false;

            bool ok = string.Equals(expected.Trim(), submittedAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
            _http.HttpContext?.Session.Remove("CaptchaAnswer");
            return ok;
        }

        // =====================================================================
        // 5. File Upload & Webcam Base64 Photo Processing (from WebApplication1)
        // =====================================================================
        public string? ValidatePhoto(IFormFile f)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return "Only JPG, PNG, and WEBP images are allowed.";

            if (f.Length > 8 * 1024 * 1024)
                return "Image size cannot exceed 8 MB.";

            return null;
        }

        public string SavePhoto(IFormFile f, string subFolder)
        {
            string folderPath = Path.Combine(_env.WebRootPath, "uploads", subFolder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(f.FileName).ToLowerInvariant();
            string fullPath = Path.Combine(folderPath, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            f.CopyTo(stream);

            return fileName;
        }

        public string SaveBase64Photo(string base64Data, string subFolder)
        {
            string folderPath = Path.Combine(_env.WebRootPath, "uploads", subFolder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            int commaIndex = base64Data.IndexOf(',');
            string raw = commaIndex >= 0 ? base64Data[(commaIndex + 1)..] : base64Data;
            byte[] bytes = Convert.FromBase64String(raw);

            string fileName = Guid.NewGuid().ToString("N") + ".png";
            string fullPath = Path.Combine(folderPath, fileName);
            File.WriteAllBytes(fullPath, bytes);

            return fileName;
        }

        public static string ResolvePhotoUrl(string? fileName, string subFolder)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return subFolder == "profiles" ? "/images/default-avatar.svg" : "/images/placeholder-property.svg";

            if (fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("/"))
            {
                return fileName;
            }

            if (fileName == "default.svg" || fileName == "default.png")
                return "/images/default-avatar.svg";

            return $"/uploads/{subFolder}/{fileName}";
        }

        /// <summary>
        /// Serves right-sized WebP/AVIF Unsplash variants when available to eliminate legacy JPEG oversizing.
        /// </summary>
        public static string ResolveOptimizedPhotoUrl(string? fileName, string subFolder, int width = 640, int height = 480)
        {
            string url = ResolvePhotoUrl(fileName, subFolder);
            if (url.Contains("images.unsplash.com/", StringComparison.OrdinalIgnoreCase))
            {
                int qIdx = url.IndexOf('?');
                string baseUrl = qIdx >= 0 ? url[..qIdx] : url;
                return $"{baseUrl}?auto=format&fit=crop&w={width}&h={height}&q=78";
            }
            return url;
        }

        public string GetActiveCurrency() => _http.HttpContext?.Request.Cookies["StayNest_Currency"] ?? "MYR";

        // =====================================================================
        // 6. AES-256 Data-at-Rest Encryption & Bank Account Masking
        // =====================================================================
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.IV = EncryptionIV;
            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                using var aes = Aes.Create();
                aes.Key = EncryptionKey;
                aes.IV = EncryptionIV;
                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] buffer = Convert.FromBase64String(cipherText);
                using var ms = new MemoryStream(buffer);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                return cipherText;
            }
        }

        public string MaskBankAccount(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber)) return "Not configured";
            string trimmed = accountNumber.Trim();
            if (trimmed.Length <= 4) return "****";
            return new string('•', trimmed.Length - 4) + trimmed[^4..];
        }

        // =====================================================================
        // 7. Profanity Filter & Currency Converter (from WebApplication1)
        // =====================================================================
        public string FilterProfanity(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string result = text;
            foreach (var badWord in ProfanityList)
            {
                string pattern = @"\b" + badWord + @"\b";
                string replacement = badWord[0] + new string('*', Math.Max(1, badWord.Length - 1));
                result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
            }
            return result;
        }

        public (decimal convertedAmount, string symbol) ConvertCurrency(decimal amountInMyr, string? currency)
        {
            return currency?.ToUpperInvariant() switch
            {
                "USD" => (Math.Round(amountInMyr / 4.45m, 2), "$"),
                "SGD" => (Math.Round(amountInMyr / 3.32m, 2), "S$"),
                _ => (Math.Round(amountInMyr, 2), "RM")
            };
        }

        public string FormatMoney(decimal amountInMyr, string? currency = null)
        {
            currency ??= _http.HttpContext?.Request.Cookies["StayNest_Currency"] ?? "MYR";
            var (amt, sym) = ConvertCurrency(amountInMyr, currency);
            return $"{sym} {amt:N2}";
        }

        // =====================================================================
        // 8. Reputation Aggregation & Mutual Double-Blind Review Helper
        // =====================================================================
        public async Task SyncReviewVisibilityAndAggregateAsync(DB db, int? propertyId = null)
        {
            var now = DateTime.Now;
            var pendingReviews = await db.Reviews
                .Where(r => !r.IsPublished &&
                            ((r.GuestSubmittedAt != null && r.HostSubmittedAt != null) ||
                             (r.GuestSubmittedAt != null && r.PublishDeadline <= now)))
                .ToListAsync();

            var affectedPropertyIds = new HashSet<int>();
            if (propertyId.HasValue) affectedPropertyIds.Add(propertyId.Value);

            foreach (var r in pendingReviews)
            {
                r.IsPublished = true;
                affectedPropertyIds.Add(r.PropertyId);
            }

            if (pendingReviews.Count > 0)
            {
                await db.SaveChangesAsync();
            }

            foreach (var pid in affectedPropertyIds)
            {
                var prop = await db.Properties.FindAsync(pid);
                if (prop == null) continue;

                var published = await db.Reviews
                    .Where(r => r.PropertyId == pid && r.IsPublished && r.GuestSubmittedAt != null)
                    .Select(r => r.OverallRating)
                    .ToListAsync();

                prop.ReviewCount = published.Count;
                prop.AverageRating = published.Count > 0 ? Math.Round(published.Average(), 2) : 0.0;
            }

            if (affectedPropertyIds.Count > 0)
            {
                await db.SaveChangesAsync();
            }
        }

        // =====================================================================
        // 9. Audit Trail & In-App Notifications
        // =====================================================================
        public void LogAudit(
            DB db,
            int? userId,
            string userEmail,
            string module,
            string action,
            string details,
            string severity = "Info",
            string? entityName = null,
            int? entityId = null)
        {
            try
            {
                var log = new AuditLog
                {
                    UserId = userId,
                    UserEmail = string.IsNullOrWhiteSpace(userEmail) ? "Anonymous" : userEmail,
                    Module = module,
                    Action = action,
                    EntityName = entityName,
                    EntityId = entityId,
                    Details = details,
                    IpAddress = GetClientIp(),
                    Severity = severity,
                    Timestamp = DateTime.Now
                };
                db.AuditLogs.Add(log);
                db.SaveChanges();
            }
            catch
            {
                // Keep primary workflow resilient if audit persistence encounters a transient error
            }
        }

        public void CreateNotification(DB db, int userId, string title, string message, string type, string? linkUrl = null)
        {
            try
            {
                db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = type,
                    LinkUrl = linkUrl,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
                db.SaveChanges();
            }
            catch
            {
                // Resilient notification helper
            }
        }
    }
}
