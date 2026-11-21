using System.Text;
using AirbnbBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 7: Dashboard & Reporting
    /// Host Analytics (Occupancy %, Monthly Earnings, Chart.js, CSV Export) &
    /// Admin Platform Analytics (GMV, Commission Revenue, User Growth, Disputes, CSV Export).
    /// </summary>
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;

        public DashboardController(DB db, Helper helper)
        {
            _db = db;
            _helper = helper;
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> Host(int rangeDays = 90)
        {
            rangeDays = rangeDays is 30 or 90 or 180 or 365 ? rangeDays : 90;
            var vm = await BuildHostDashboardAsync(rangeDays);
            return View(vm);
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> HostChartData(int rangeDays = 90)
        {
            var vm = await BuildHostDashboardAsync(rangeDays);
            return Json(new
            {
                labels = vm.ChartLabels,
                earnings = vm.MonthlyEarnings,
                bookings = vm.MonthlyBookingCounts,
                statusDist = vm.StatusDistribution,
                occupancyRate = vm.OccupancyRatePercent,
                totalNet = vm.TotalNetPayouts
            });
        }

        private async Task<HostDashboardViewModel> BuildHostDashboardAsync(int rangeDays)
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";
            var cutoff = DateTime.Today.AddDays(-rangeDays);

            var props = await _db.Properties
                .Where(p => isAdmin || p.HostId == uid)
                .ToListAsync();
            var propIds = props.Select(p => p.Id).ToList();

            var bookings = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .Include(b => b.HostPayout)
                .Where(b => propIds.Contains(b.PropertyId))
                .ToListAsync();

            var rangeBookings = bookings.Where(b => b.BookingDate >= cutoff).ToList();
            var validRangeBookings = rangeBookings.Where(b => b.Status != "Cancelled").ToList();

            // Occupancy rate over selected range
            int bookedNights = validRangeBookings.Sum(b => b.TotalNights);
            int availableRoomNights = Math.Max(1, props.Count(p => p.IsActive) * Math.Min(rangeDays, 90));
            double occupancy = Math.Min(100.0, Math.Round((double)bookedNights / availableRoomNights * 100.0, 1));

            var payouts = await _db.HostPayouts
                .Where(p => (isAdmin || p.HostId == uid) && p.Status != "Reversed")
                .ToListAsync();

            int openRequests = await _db.ServiceRequests
                .CountAsync(sr => propIds.Contains(sr.Booking.PropertyId) && sr.Status != "Resolved");

            // Monthly breakdown (last 6 months)
            var labels = new List<string>();
            var earnings = new List<decimal>();
            var counts = new List<int>();

            for (int i = 5; i >= 0; i--)
            {
                var mStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
                var mEnd = mStart.AddMonths(1);
                labels.Add(mStart.ToString("MMM yyyy"));

                var mBookings = bookings.Where(b => b.Status != "Cancelled" && b.BookingDate >= mStart && b.BookingDate < mEnd).ToList();
                counts.Add(mBookings.Count);
                earnings.Add(mBookings.Sum(b => b.HostPayout?.NetPayoutAmount ?? (b.SubtotalAmount + b.CleaningFee)));
            }

            var ratedProps = props.Where(p => p.ReviewCount > 0).ToList();

            return new HostDashboardViewModel
            {
                TotalListings = props.Count,
                ActiveListings = props.Count(p => p.IsActive),
                TotalBookings = bookings.Count,
                ActiveStaysCount = bookings.Count(b => b.Status == "CheckedIn"),
                OccupancyRatePercent = occupancy,
                TotalGrossEarnings = payouts.Sum(p => p.GrossAmount),
                TotalNetPayouts = payouts.Sum(p => p.NetPayoutAmount),
                PendingPayoutAmount = payouts.Where(p => p.Status == "Pending").Sum(p => p.NetPayoutAmount),
                AverageHostRating = ratedProps.Count > 0 ? Math.Round(ratedProps.Average(p => p.AverageRating), 2) : 0.0,
                OpenServiceRequests = openRequests,
                RangeDays = rangeDays,
                UpcomingArrivals = bookings.Where(b => b.Status == "Confirmed" && b.CheckInDate >= DateTime.Today).OrderBy(b => b.CheckInDate).Take(5).ToList(),
                UpcomingDepartures = bookings.Where(b => b.Status == "CheckedIn").OrderBy(b => b.CheckOutDate).Take(5).ToList(),
                RecentBookings = bookings.OrderByDescending(b => b.BookingDate).Take(8).ToList(),
                ChartLabels = labels,
                MonthlyEarnings = earnings,
                MonthlyBookingCounts = counts,
                StatusDistribution = bookings.GroupBy(b => b.Status).ToDictionary(g => g.Key, g => g.Count())
            };
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> ExportHostReportCsv()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";

            var bookings = await _db.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .Include(b => b.HostPayout)
                .Where(b => isAdmin || b.Property.HostId == uid)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("BookingRef,Property,Guest,CheckIn,CheckOut,Nights,Status,TotalGuestMYR,HostNetPayoutMYR");
            foreach (var b in bookings)
            {
                sb.AppendLine($"{b.BookingReference},\"{b.Property.Title.Replace("\"", "'")}\",\"{b.Guest.FullName}\",{b.CheckInDate:yyyy-MM-dd},{b.CheckOutDate:yyyy-MM-dd},{b.TotalNights},{b.Status},{b.TotalAmount:F2},{(b.HostPayout?.NetPayoutAmount ?? 0m):F2}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"StayNest_HostReport_{DateTime.Now:yyyyMMdd}.csv");
        }

        // =====================================================================
        // Admin Platform-Wide Analytics Dashboard
        // =====================================================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Admin(int rangeDays = 180)
        {
            var vm = await BuildAdminDashboardAsync(rangeDays);
            return View(vm);
        }

        private async Task<AdminDashboardViewModel> BuildAdminDashboardAsync(int rangeDays)
        {
            var users = await _db.Users.ToListAsync();
            var props = await _db.Properties.ToListAsync();
            var bookings = await _db.Bookings.Include(b => b.Payments).ToListAsync();
            var payments = await _db.Payments.ToListAsync();
            var payouts = await _db.HostPayouts.ToListAsync();

            var monthLabels = new List<string>();
            var monthlyGmv = new List<decimal>();
            var monthlyUsers = new List<int>();

            for (int i = 5; i >= 0; i--)
            {
                var mStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
                var mEnd = mStart.AddMonths(1);
                monthLabels.Add(mStart.ToString("MMM yyyy"));

                monthlyGmv.Add(payments
                    .Where(p => p.Status is "Paid" or "PartiallyRefunded" && p.PaymentDate >= mStart && p.PaymentDate < mEnd)
                    .Sum(p => p.Amount - p.RefundAmount));

                monthlyUsers.Add(users.Count(u => u.CreatedAt >= mStart && u.CreatedAt < mEnd));
            }

            decimal totalGuestServiceTax = bookings.Where(b => b.Status != "Cancelled").Sum(b => b.ServiceTax);
            decimal totalHostCommission = payouts.Where(p => p.Status != "Reversed").Sum(p => p.PlatformFee);

            return new AdminDashboardViewModel
            {
                TotalUsers = users.Count,
                TotalHosts = users.Count(u => u.Role == "Host"),
                TotalGuests = users.Count(u => u.Role == "Guest"),
                PendingHostVerifications = users.Count(u => u.IdentityStatus == "PendingApproval"),
                TotalProperties = props.Count,
                ActiveProperties = props.Count(p => p.IsActive),
                TotalBookings = bookings.Count,
                PlatformGmv = payments.Where(p => p.Status is "Paid" or "PartiallyRefunded").Sum(p => p.Amount - p.RefundAmount),
                PlatformCommissionRevenue = totalGuestServiceTax + totalHostCommission,
                TotalRefundsProcessed = payments.Sum(p => p.RefundAmount),
                OpenDisputesCount = await _db.ServiceRequests.CountAsync(sr => sr.Status != "Resolved"),
                FlaggedReviewsCount = await _db.Reviews.CountAsync(r => r.IsFlagged),
                RangeDays = rangeDays,
                MonthLabels = monthLabels,
                MonthlyGmv = monthlyGmv,
                MonthlyNewUsers = monthlyUsers,
                PropertyTypeCounts = props.GroupBy(p => p.PropertyType).ToDictionary(g => g.Key, g => g.Count()),
                RecentSecurityEvents = await _db.AuditLogs.OrderByDescending(a => a.Timestamp).Take(10).ToListAsync(),
                OpenDisputes = await _db.ServiceRequests
                    .Include(sr => sr.Booking).ThenInclude(b => b.Property)
                    .Include(sr => sr.Guest)
                    .Where(sr => sr.Status != "Resolved")
                    .OrderByDescending(sr => sr.CreatedAt)
                    .Take(6)
                    .ToListAsync()
            };
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ExportAdminReportCsv()
        {
            var payments = await _db.Payments
                .Include(p => p.Booking).ThenInclude(b => b.Property)
                .Include(p => p.Booking).ThenInclude(b => b.Guest)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("TxnRef,Date,BookingRef,Property,Guest,Method,AmountMYR,RefundMYR,Status");
            foreach (var p in payments)
            {
                sb.AppendLine($"{p.TransactionRef},{p.PaymentDate:yyyy-MM-dd HH:mm},{p.Booking.BookingReference},\"{p.Booking.Property.Title.Replace("\"", "'")}\",\"{p.Booking.Guest.FullName}\",\"{p.PaymentMethod}\",{p.Amount:F2},{p.RefundAmount:F2},{p.Status}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"StayNest_PlatformReport_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}
