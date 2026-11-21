using System.Net;
using System.Net.Mail;

namespace AirbnbBooking.Services
{
    /// <summary>
    /// Gmail SMTP utility adapted from WebApplication1 using System.Net.Mail.SmtpClient
    /// with the updated App Password token configured in appsettings.json.
    /// </summary>
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private (SmtpClient client, string senderEmail, string senderName) GetSmtpClient()
        {
            string host = _config["Smtp:Host"] ?? "smtp.gmail.com";
            int port = int.TryParse(_config["Smtp:Port"], out int p) ? p : 587;
            bool enableSsl = !bool.TryParse(_config["Smtp:EnableSsl"], out bool ssl) || ssl;
            string senderEmail = _config["Smtp:SenderEmail"] ?? "AirbnbBookingSystem@gmail.com";
            string senderName = _config["Smtp:SenderName"] ?? "StayNest Airbnb Booking System";
            string rawPassword = _config["Smtp:AppPassword"] ?? "nuiq pqlq eats wqps";
            string appPassword = rawPassword.Replace(" ", "").Trim();

            var client = new SmtpClient(host)
            {
                Port = port,
                Credentials = new NetworkCredential(senderEmail, appPassword),
                EnableSsl = enableSsl,
                Timeout = 10000
            };

            return (client, senderEmail, senderName);
        }

        // =====================================================================
        // 1. Account Activation / Email Verification (OTP + Instant Link)
        // =====================================================================
        public async Task<bool> SendVerificationCodeAsync(string recipientEmail, string otpCode, string? recipientName = null, string? verifyUrl = null)
        {
            try
            {
                var (smtpClient, senderEmail, senderName) = GetSmtpClient();
                string displayName = string.IsNullOrWhiteSpace(recipientName) ? "Valued Member" : recipientName;
                string linkSection = !string.IsNullOrEmpty(verifyUrl)
                    ? $@"<p style='margin-top:20px;text-align:center;'><a href='{verifyUrl}' style='display:inline-block;padding:12px 28px;background:#ff385c;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:700;'>Verify Account Instantly</a></p>
                         <p style='color:#64748b;font-size:12px;'>Or copy this link:<br/><span style='word-break:break-all;'>{verifyUrl}</span></p>"
                    : "";

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = $"[{otpCode}] Activate Your StayNest Airbnb Account",
                    Body = $@"
                        <div style='font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;background:#ffffff;'>
                            <div style='background:#ff385c;padding:24px;text-align:center;'>
                                <h1 style='color:#ffffff;margin:0;font-size:22px;font-weight:800;'>StayNest Airbnb Booking System</h1>
                            </div>
                            <div style='padding:30px;color:#334155;'>
                                <h2 style='color:#0f172a;margin-top:0;'>Verify Your Email Address</h2>
                                <p>Hello <b>{displayName}</b>,</p>
                                <p>Welcome to <b>StayNest</b>! Enter the 6-digit verification code below to activate your account:</p>
                                <div style='text-align:center;margin:24px 0;'>
                                    <div style='display:inline-block;background:#fff1f2;border:2px dashed #ff385c;padding:14px 30px;border-radius:10px;'>
                                        <span style='font-size:32px;font-weight:800;letter-spacing:6px;color:#ff385c;'>{otpCode}</span>
                                    </div>
                                    <p style='color:#94a3b8;font-size:12px;margin-top:8px;'>Expires in 15 minutes.</p>
                                </div>
                                {linkSection}
                            </div>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);
                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Verification email delivered to {Email}", recipientEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send verification email to {Email}.", recipientEmail);
                return false;
            }
        }

        // =====================================================================
        // 2. Token-Based Password Reset Email
        // =====================================================================
        public async Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetUrl)
        {
            try
            {
                var (smtpClient, senderEmail, senderName) = GetSmtpClient();

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = "Reset Your StayNest Airbnb Password",
                    Body = $@"
                        <div style='font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;background:#ffffff;'>
                            <div style='background:#0f172a;padding:24px;text-align:center;'>
                                <h1 style='color:#ffffff;margin:0;font-size:22px;font-weight:800;'>StayNest Security</h1>
                            </div>
                            <div style='padding:30px;color:#334155;'>
                                <h2 style='color:#0f172a;margin-top:0;'>Password Reset Request</h2>
                                <p>Hello <b>{recipientName}</b>,</p>
                                <p>We received a request to reset your password. Click below to choose a new password (valid for 1 hour):</p>
                                <div style='text-align:center;margin:25px 0;'>
                                    <a href='{resetUrl}' style='display:inline-block;padding:12px 28px;background:#ff385c;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:700;'>Reset Password Now</a>
                                </div>
                                <p style='color:#64748b;font-size:12px;'>Or copy this URL:<br/><span style='word-break:break-all;'>{resetUrl}</span></p>
                            </div>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send password reset email to {Email}.", recipientEmail);
                return false;
            }
        }

        // =====================================================================
        // 3. Booking Confirmation & E-Receipt Email
        // =====================================================================
        public async Task<bool> SendBookingConfirmationAsync(
            string recipientEmail,
            string recipientName,
            string bookingRef,
            string propertyTitle,
            decimal totalAmount,
            string checkInDate,
            string checkOutDate,
            string qrToken)
        {
            try
            {
                var (smtpClient, senderEmail, senderName) = GetSmtpClient();

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = $"Reservation Confirmed: {bookingRef} — {propertyTitle}",
                    Body = $@"
                        <div style='font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;background:#ffffff;'>
                            <div style='background:#059669;padding:24px;text-align:center;'>
                                <h1 style='color:#ffffff;margin:0;font-size:22px;font-weight:800;'>Reservation Confirmed!</h1>
                            </div>
                            <div style='padding:30px;color:#334155;'>
                                <p>Hello <b>{recipientName}</b>,</p>
                                <p>Your booking on <b>StayNest</b> is confirmed. Here is your E-Receipt summary:</p>
                                <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:18px;margin:20px 0;'>
                                    <p style='margin:6px 0;'><strong>Booking Reference:</strong> {bookingRef}</p>
                                    <p style='margin:6px 0;'><strong>Property:</strong> {propertyTitle}</p>
                                    <p style='margin:6px 0;'><strong>Check-In:</strong> {checkInDate}</p>
                                    <p style='margin:6px 0;'><strong>Check-Out:</strong> {checkOutDate}</p>
                                    <p style='margin:6px 0;'><strong>Check-In Pass Code:</strong> <code>{qrToken}</code></p>
                                    <p style='margin:10px 0 0;color:#059669;font-size:18px;font-weight:800;'>Total Paid: RM {totalAmount:N2}</p>
                                </div>
                                <p style='font-size:13px;color:#64748b;'>Open <b>My Trips</b> to view your scannable QR Check-In Pass and printable PDF E-Receipt.</p>
                            </div>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send booking confirmation email to {Email}.", recipientEmail);
                return false;
            }
        }

        // =====================================================================
        // 4. Cancellation & Automated Refund Notice Email
        // =====================================================================
        public async Task<bool> SendRefundNotificationAsync(
            string recipientEmail,
            string recipientName,
            string bookingRef,
            string propertyTitle,
            string policyName,
            decimal refundAmount,
            string reason)
        {
            try
            {
                var (smtpClient, senderEmail, senderName) = GetSmtpClient();

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = $"Cancellation & Refund Processed: {bookingRef}",
                    Body = $@"
                        <div style='font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;background:#ffffff;'>
                            <div style='background:#0f172a;padding:24px;text-align:center;'>
                                <h1 style='color:#ffffff;margin:0;font-size:22px;font-weight:800;'>Cancellation & Refund Summary</h1>
                            </div>
                            <div style='padding:30px;color:#334155;'>
                                <p>Hello <b>{recipientName}</b>,</p>
                                <p>Booking <b>{bookingRef}</b> ({propertyTitle}) has been cancelled.</p>
                                <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:18px;margin:20px 0;'>
                                    <p style='margin:6px 0;'><strong>Policy Applied:</strong> {policyName}</p>
                                    <p style='margin:6px 0;'><strong>Refund Amount:</strong> RM {refundAmount:N2}</p>
                                    <p style='margin:6px 0;'><strong>Details:</strong> {reason}</p>
                                </div>
                            </div>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send refund notice email to {Email}.", recipientEmail);
                return false;
            }
        }

        // =====================================================================
        // 5. Arrival & Departure Operational Reminder Email
        // =====================================================================
        public async Task<bool> SendStayReminderAsync(
            string recipientEmail,
            string recipientName,
            string eventType,
            string bookingRef,
            string propertyTitle,
            string targetDate)
        {
            try
            {
                var (smtpClient, senderEmail, senderName) = GetSmtpClient();

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = $"Upcoming {eventType} Reminder: {propertyTitle} ({bookingRef})",
                    Body = $@"
                        <div style='font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;background:#ffffff;'>
                            <div style='background:#ff385c;padding:22px;text-align:center;'>
                                <h1 style='color:#ffffff;margin:0;font-size:20px;font-weight:800;'>StayNest Operations Reminder</h1>
                            </div>
                            <div style='padding:28px;color:#334155;'>
                                <p>Hello <b>{recipientName}</b>,</p>
                                <p>This is a reminder for your upcoming <b>{eventType}</b> on <b>{targetDate}</b>:</p>
                                <p><strong>Booking:</strong> {bookingRef} — {propertyTitle}</p>
                                <p style='font-size:13px;color:#64748b;'>Sign in to StayNest In-Stay Operations to view check-in QR passes, chat live, or log service requests.</p>
                            </div>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send stay reminder email to {Email}.", recipientEmail);
                return false;
            }
        }
    }
}
