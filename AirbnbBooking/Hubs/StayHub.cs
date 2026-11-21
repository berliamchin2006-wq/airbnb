using System.Security.Claims;
using AirbnbBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Hubs
{
    [Authorize]
    public class StayHub : Hub
    {
        private readonly DB _db;

        public StayHub(DB db)
        {
            _db = db;
        }

        public async Task JoinBookingRoom(int bookingId)
        {
            var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "";
            if (!int.TryParse(userIdStr, out int userId)) return;

            var booking = await _db.Bookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return;
            if (booking.GuestId != userId && booking.Property.HostId != userId && role != "Admin") return;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"booking-{bookingId}");
        }

        public async Task SendBookingMessage(int bookingId, string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText)) return;
            var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var fullName = Context.User?.FindFirstValue("FullName") ?? "User";
            var role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "";
            if (!int.TryParse(userIdStr, out int userId)) return;

            var booking = await _db.Bookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return;
            if (booking.GuestId != userId && booking.Property.HostId != userId && role != "Admin") return;

            string clean = messageText.Trim();
            if (clean.Length > 1500) clean = clean[..1500];

            var msg = new BookingMessage
            {
                BookingId = bookingId,
                SenderId = userId,
                MessageText = clean,
                SentAt = DateTime.Now,
                IsRead = false
            };

            _db.BookingMessages.Add(msg);

            // Create in-app notification for the other party
            int recipientId = userId == booking.GuestId ? booking.Property.HostId : booking.GuestId;
            if (recipientId != userId)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = recipientId,
                    Title = $"New Message from {fullName}",
                    Message = clean.Length > 80 ? clean[..80] + "..." : clean,
                    Type = "Booking",
                    LinkUrl = $"/Stay/Chat/{bookingId}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }

            await _db.SaveChangesAsync();

            await Clients.Group($"booking-{bookingId}").SendAsync("ReceiveBookingMessage", new
            {
                id = msg.Id,
                bookingId,
                senderId = userId,
                senderName = fullName,
                senderRole = role,
                messageText = msg.MessageText,
                sentAt = msg.SentAt.ToString("dd MMM, hh:mm tt")
            });
        }
    }
}
