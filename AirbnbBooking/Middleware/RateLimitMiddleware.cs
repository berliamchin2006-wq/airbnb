using System.Collections.Concurrent;
using AirbnbBooking.Models;

namespace AirbnbBooking.Middleware
{
    public class RateLimitTelemetry
    {
        public long TotalRequestsChecked { get; set; }
        public long TotalRequestsBlocked { get; set; }
        public DateTime? LastBlockedAt { get; set; }
        public string? LastBlockedIp { get; set; }
        public string? LastBlockedPath { get; set; }
    }

    /// <summary>
    /// Module 8: Sliding-window API & sensitive-endpoint rate limiting and brute-force defense middleware.
    /// </summary>
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;
        private readonly RateLimitTelemetry _telemetry;

        private static readonly ConcurrentDictionary<string, Queue<DateTime>> _buckets = new();

        private static readonly string[] SensitivePrefixes =
        [
            "/Account/Login",
            "/Account/Register",
            "/Account/ForgotPassword",
            "/Account/ResetPassword",
            "/Account/VerifyEmail",
            "/Booking/Create",
            "/Payment/Process"
        ];

        public RateLimitMiddleware(RequestDelegate next, IConfiguration config, RateLimitTelemetry telemetry)
        {
            _next = next;
            _config = config;
            _telemetry = telemetry;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string path = context.Request.Path.Value ?? "/";

            // Skip static files and SignalR negotiation
            if (path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            int windowSec = int.TryParse(_config["Security:RateLimitWindowSeconds"], out int w) ? w : 60;
            int generalMax = int.TryParse(_config["Security:RateLimitMaxRequests"], out int g) ? g : 120;
            int sensitiveMax = int.TryParse(_config["Security:SensitiveEndpointMaxRequests"], out int s) ? s : 15;

            bool isSensitivePost = HttpMethods.IsPost(context.Request.Method) &&
                                   SensitivePrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

            string ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            string bucketKey = isSensitivePost ? $"SENSITIVE:{ip}:{path.ToLowerInvariant()}" : $"GEN:{ip}";
            int limit = isSensitivePost ? sensitiveMax : generalMax;

            _telemetry.TotalRequestsChecked++;

            var queue = _buckets.GetOrAdd(bucketKey, _ => new Queue<DateTime>());
            bool blocked = false;
            int remaining;

            lock (queue)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-windowSec);
                while (queue.Count > 0 && queue.Peek() < cutoff)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= limit)
                {
                    blocked = true;
                    remaining = 0;
                }
                else
                {
                    queue.Enqueue(DateTime.UtcNow);
                    remaining = Math.Max(0, limit - queue.Count);
                }
            }

            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            context.Response.Headers["X-RateLimit-Window"] = $"{windowSec}s";

            if (blocked)
            {
                _telemetry.TotalRequestsBlocked++;
                _telemetry.LastBlockedAt = DateTime.Now;
                _telemetry.LastBlockedIp = ip;
                _telemetry.LastBlockedPath = path;

                // Log rate-limit security event to database audit log
                try
                {
                    using var scope = context.RequestServices.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DB>();
                    var helper = scope.ServiceProvider.GetRequiredService<Helper>();
                    helper.LogAudit(
                        db,
                        helper.GetCurrentUserId(),
                        helper.GetCurrentUserEmail(),
                        "Security",
                        "RateLimitExceeded",
                        $"Blocked excessive requests ({limit}/{windowSec}s) on {path} from IP {ip}.",
                        severity: "Critical");
                }
                catch { }

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync($@"
                    <!DOCTYPE html><html><head><title>429 Too Many Requests</title>
                    <style>body{{font-family:Inter,system-ui,sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;background:#f8fafc;color:#0f172a;}}
                    .card{{background:#fff;padding:2.5rem;border-radius:16px;box-shadow:0 12px 32px rgba(0,0,0,.08);max-width:460px;text-align:center;}}
                    h1{{color:#ff385c;margin-top:0;}} a{{display:inline-block;margin-top:1rem;padding:.75rem 1.5rem;background:#0f172a;color:#fff;text-decoration:none;border-radius:10px;}}</style></head>
                    <body><div class='card'><h1>429 — Slow Down</h1>
                    <p>Our brute-force &amp; rate-limit protection temporarily paused requests from your IP on <code>{path}</code>.</p>
                    <p style='color:#64748b;font-size:.9rem;'>Please wait {windowSec} seconds and try again.</p>
                    <a href='/'>Return Home</a></div></body></html>");
                return;
            }

            await _next(context);
        }
    }
}
