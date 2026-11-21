using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Models
{
    public class DB : DbContext
    {
        public DB(DbContextOptions<DB> options) : base(options) { }

        // Module 1: User & Identity Management
        public DbSet<User> Users { get; set; }
        public DbSet<UserSessionToken> UserSessionTokens { get; set; }

        // Module 2: Property Listing, Search & Discovery (Combined)
        public DbSet<Property> Properties { get; set; }
        public DbSet<PropertyPhoto> PropertyPhotos { get; set; }
        public DbSet<Amenity> Amenities { get; set; }
        public DbSet<PropertyAmenity> PropertyAmenities { get; set; }
        public DbSet<SeasonalRate> SeasonalRates { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }

        // Module 3: Booking & Reservation Management
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }

        // Module 4: Payment & Transaction Management
        public DbSet<Payment> Payments { get; set; }
        public DbSet<HostPayout> HostPayouts { get; set; }

        // Module 5: In-Stay & Operations Management
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<BookingMessage> BookingMessages { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // Module 6: Review & Reputation Management
        public DbSet<Review> Reviews { get; set; }

        // Module 8: Security, Audit & Database Infrastructure
        public DbSet<AuditLog> AuditLogs { get; set; }
    }

    // =========================================================================
    // MODULE 1: USER & IDENTITY MANAGEMENT
    // =========================================================================
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(120), EmailAddress]
        public string Email { get; set; } = "";

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = "";

        [Required, MaxLength(100)]
        public string FullName { get; set; } = "";

        [Required, MaxLength(25)]
        public string PhoneNumber { get; set; } = "";

        // Roles: "Guest", "Host", "Admin"
        [Required, MaxLength(20)]
        public string Role { get; set; } = "Guest";

        [MaxLength(255)]
        public string ProfilePhoto { get; set; } = "default.svg";

        [MaxLength(500)]
        public string? Bio { get; set; }

        // Email Verification (OTP + Token Link)
        public bool IsEmailVerified { get; set; } = false;

        [MaxLength(100)]
        public string? EmailVerificationToken { get; set; }

        public DateTime? EmailVerificationExpiry { get; set; }

        // Temporary 3-Strike Login Lockout
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
        public bool IsActive { get; set; } = true;

        // Token-Based Password Reset
        [MaxLength(120)]
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // Profile / Host Identity Verification (MyKad / Passport)
        [MaxLength(255)]
        public string? IdDocumentPhoto { get; set; }

        // "Unverified", "PendingApproval", "Verified", "Rejected"
        [Required, MaxLength(30)]
        public string IdentityStatus { get; set; } = "Unverified";

        [MaxLength(500)]
        public string? IdentityNotes { get; set; }
        public DateTime? IdentityApprovedAt { get; set; }

        // Host Payout Banking Details (AES-256 Encrypted at Rest)
        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(255)]
        public string? BankAccountNumberEncrypted { get; set; }

        [MaxLength(100)]
        public string? BankHolderName { get; set; }

        // User UI Preferences
        [Required, MaxLength(10)]
        public string ThemePreference { get; set; } = "light"; // "light", "dark"

        [Required, MaxLength(10)]
        public string PreferredCurrency { get; set; } = "MYR"; // "MYR", "USD", "SGD"

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [InverseProperty(nameof(UserSessionToken.User))]
        public List<UserSessionToken> SessionTokens { get; set; } = [];

        [InverseProperty(nameof(Property.Host))]
        public List<Property> HostedProperties { get; set; } = [];

        [InverseProperty(nameof(Booking.Guest))]
        public List<Booking> Bookings { get; set; } = [];

        [InverseProperty(nameof(Wishlist.Guest))]
        public List<Wishlist> Wishlists { get; set; } = [];

        [InverseProperty(nameof(HostPayout.Host))]
        public List<HostPayout> Payouts { get; set; } = [];

        [InverseProperty(nameof(Notification.User))]
        public List<Notification> Notifications { get; set; } = [];
    }

    [Index(nameof(TokenHash), IsUnique = true)]
    public class UserSessionToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required, MaxLength(128)]
        public string TokenHash { get; set; } = "";

        [MaxLength(200)]
        public string DeviceInfo { get; set; } = "Web Browser";

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;

        [MaxLength(128)]
        public string? ReplacedByTokenHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // =========================================================================
    // MODULE 2: PROPERTY LISTING, SEARCH & DISCOVERY (COMBINED)
    // =========================================================================
    public class Property
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int HostId { get; set; }

        [ForeignKey(nameof(HostId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public User Host { get; set; } = null!;

        [Required, MaxLength(150)]
        public string Title { get; set; } = "";

        // "Condominium", "Villa", "Bungalow", "Apartment", "Townhouse", "Studio", "Chalet"
        [Required, MaxLength(50)]
        public string PropertyType { get; set; } = "Condominium";

        [Required, MaxLength(255)]
        public string Address { get; set; } = "";

        [Required, MaxLength(60)]
        public string City { get; set; } = "";

        [Required, MaxLength(60)]
        public string State { get; set; } = "";

        [Required, MaxLength(15)]
        public string Postcode { get; set; } = "";

        // Map-Ready Spatial Coordinates
        public double Latitude { get; set; } = 3.1390;
        public double Longitude { get; set; } = 101.6869;

        [Required, MaxLength(3000)]
        public string Description { get; set; } = "";

        // Capacity & Space
        [Range(1, 30)]
        public int MaxGuests { get; set; } = 2;

        [Range(0, 20)]
        public int Bedrooms { get; set; } = 1;

        [Range(1, 30)]
        public int Beds { get; set; } = 1;

        [Range(1, 20)]
        public int Bathrooms { get; set; } = 1;

        // Base & Weekend Pricing
        [Precision(10, 2)]
        public decimal BaseNightlyRate { get; set; } = 150m;

        [Precision(10, 2)]
        public decimal CleaningFee { get; set; } = 50m;

        [Precision(10, 2)]
        public decimal CustomWeekendRate { get; set; } = 180m;

        // Cancellation Policy: "Flexible", "Moderate", "Strict"
        [Required, MaxLength(30)]
        public string CancellationPolicy { get; set; } = "Flexible";

        [MaxLength(1200)]
        public string HouseRules { get; set; } = "Check-in after 3:00 PM. Check-out by 11:00 AM. No smoking inside. Quiet hours 10:00 PM – 8:00 AM.";

        public bool IsActive { get; set; } = true;

        // Aggregated Reputation Cache (Updated by Module 6)
        public double AverageRating { get; set; } = 0.0;
        public int ReviewCount { get; set; } = 0;

        // Optimistic Concurrency Token
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        public List<PropertyPhoto> Photos { get; set; } = [];
        public List<PropertyAmenity> PropertyAmenities { get; set; } = [];
        public List<SeasonalRate> SeasonalRates { get; set; } = [];
        public List<Booking> Bookings { get; set; } = [];
        public List<Review> Reviews { get; set; } = [];
        public List<Wishlist> Wishlists { get; set; } = [];
    }

    public class PropertyPhoto
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        [Required, MaxLength(350)]
        public string FileName { get; set; } = "";

        [MaxLength(120)]
        public string? Caption { get; set; }

        public bool IsPrimary { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;
    }

    [Index(nameof(Name), IsUnique = true)]
    public class Amenity
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(60)]
        public string Name { get; set; } = "";

        [Required, MaxLength(50)]
        public string IconClass { get; set; } = "bi-check-circle";

        [Required, MaxLength(40)]
        public string Category { get; set; } = "Essentials";

        public List<PropertyAmenity> PropertyAmenities { get; set; } = [];
    }

    [PrimaryKey(nameof(PropertyId), nameof(AmenityId))]
    public class PropertyAmenity
    {
        public int PropertyId { get; set; }

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        public int AmenityId { get; set; }

        [ForeignKey(nameof(AmenityId))]
        public Amenity Amenity { get; set; } = null!;
    }

    public class SeasonalRate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        [Required, MaxLength(80)]
        public string SeasonName { get; set; } = "";

        [Required, DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Precision(10, 2)]
        public decimal NightlyRate { get; set; }
    }

    public class Wishlist
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GuestId { get; set; }

        [ForeignKey(nameof(GuestId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public User Guest { get; set; } = null!;

        [Required]
        public int PropertyId { get; set; }

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        [Required, MaxLength(60)]
        public string FolderName { get; set; } = "Saved Stays";

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }

    // =========================================================================
    // MODULE 3: BOOKING & RESERVATION MANAGEMENT
    // =========================================================================
    [Index(nameof(Code), IsUnique = true)]
    public class PromoCode
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string Code { get; set; } = "";

        [Required, MaxLength(150)]
        public string Description { get; set; } = "";

        // "Percent" or "Fixed"
        [Required, MaxLength(15)]
        public string DiscountType { get; set; } = "Percent";

        [Precision(10, 2)]
        public decimal DiscountValue { get; set; } = 10m;

        [Precision(10, 2)]
        public decimal MinSpend { get; set; } = 0m;

        public int MaxUses { get; set; } = 100;
        public int CurrentUses { get; set; } = 0;

        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; } = DateTime.Today.AddMonths(6);

        public bool IsActive { get; set; } = true;
    }

    [Index(nameof(BookingReference), IsUnique = true)]
    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string BookingReference { get; set; } = "";

        [Required]
        public int PropertyId { get; set; }

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        [Required]
        public int GuestId { get; set; }

        [ForeignKey(nameof(GuestId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public User Guest { get; set; } = null!;

        public int? PromoCodeId { get; set; }

        [ForeignKey(nameof(PromoCodeId))]
        [DeleteBehavior(DeleteBehavior.SetNull)]
        public PromoCode? PromoCode { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime CheckInDate { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime CheckOutDate { get; set; }

        public int TotalNights { get; set; }
        public int GuestCount { get; set; } = 1;

        // Automated Price Breakdown
        [Precision(10, 2)]
        public decimal AverageNightlyRate { get; set; }

        [Precision(10, 2)]
        public decimal SubtotalAmount { get; set; }

        [Precision(10, 2)]
        public decimal CleaningFee { get; set; }

        [Precision(10, 2)]
        public decimal ServiceTax { get; set; } // 10% platform service tax

        [Precision(10, 2)]
        public decimal DiscountAmount { get; set; } = 0m;

        [Precision(10, 2)]
        public decimal TotalAmount { get; set; }

        // State Machine: "Pending" -> "Confirmed" -> "CheckedIn" -> "Completed" | "Cancelled"
        [Required, MaxLength(30)]
        public string Status { get; set; } = "Pending";

        [MaxLength(500)]
        public string? SpecialRequests { get; set; }

        // Digital QR Check-In Pass Token
        [Required, MaxLength(80)]
        public string QrCheckInToken { get; set; } = "";

        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }

        [MaxLength(400)]
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public int? CancelledByUserId { get; set; }

        // Optimistic Concurrency Token
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public List<Payment> Payments { get; set; } = [];
        public HostPayout? HostPayout { get; set; }
        public List<ServiceRequest> ServiceRequests { get; set; } = [];
        public List<BookingMessage> Messages { get; set; } = [];
        public Review? Review { get; set; }
    }

    // =========================================================================
    // MODULE 4: PAYMENT & TRANSACTION MANAGEMENT
    // =========================================================================
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;

        // "FullPayment", "Refund"
        [Required, MaxLength(30)]
        public string PaymentType { get; set; } = "FullPayment";

        [Precision(10, 2)]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // "Credit / Debit Card", "FPX Online Banking", "Touch 'n Go eWallet"
        [Required, MaxLength(60)]
        public string PaymentMethod { get; set; } = "Credit / Debit Card";

        [MaxLength(10)]
        public string? CardLast4 { get; set; }

        [Required, MaxLength(100)]
        public string TransactionRef { get; set; } = "";

        // "Paid", "PartiallyRefunded", "Refunded", "Failed"
        [Required, MaxLength(30)]
        public string Status { get; set; } = "Paid";

        [Precision(10, 2)]
        public decimal RefundAmount { get; set; } = 0m;

        [MaxLength(300)]
        public string? RefundReason { get; set; }

        public DateTime? RefundedAt { get; set; }
    }

    public class HostPayout
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int HostId { get; set; }

        [ForeignKey(nameof(HostId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public User Host { get; set; } = null!;

        [Required]
        public int BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;

        [Precision(10, 2)]
        public decimal GrossAmount { get; set; }

        [Precision(10, 2)]
        public decimal PlatformFee { get; set; } // 3% host commission

        [Precision(10, 2)]
        public decimal NetPayoutAmount { get; set; }

        // "Pending", "Processing", "Disbursed", "Reversed"
        [Required, MaxLength(30)]
        public string Status { get; set; } = "Pending";

        [MaxLength(80)]
        public string PayoutReference { get; set; } = "";

        public DateTime ScheduledDate { get; set; } = DateTime.Today.AddDays(1);
        public DateTime? DisbursedAt { get; set; }
    }

    // =========================================================================
    // MODULE 5: IN-STAY & OPERATIONS MANAGEMENT
    // =========================================================================
    public class ServiceRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;

        [Required]
        public int GuestId { get; set; }

        [ForeignKey(nameof(GuestId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public User Guest { get; set; } = null!;

        // "Housekeeping", "Air-Conditioning", "Wi-Fi & Tech", "Plumbing", "Extra Amenities", "Safety / Dispute"
        [Required, MaxLength(50)]
        public string Category { get; set; } = "Housekeeping";

        [Required, MaxLength(150)]
        public string IssueTitle { get; set; } = "";

        [Required, MaxLength(1500)]
        public string Description { get; set; } = "";

        // "Low", "Medium", "High", "Urgent"
        [Required, MaxLength(20)]
        public string Priority { get; set; } = "Medium";

        [MaxLength(255)]
        public string? PhotoProof { get; set; }

        // "Open", "InProgress", "Resolved", "Escalated"
        [Required, MaxLength(30)]
        public string Status { get; set; } = "Open";

        [MaxLength(1000)]
        public string? HostResponse { get; set; }

        [Precision(10, 2)]
        public decimal RepairCost { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ResolvedAt { get; set; }
    }

    public class BookingMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;

        [Required]
        public int SenderId { get; set; }

        [ForeignKey(nameof(SenderId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public User Sender { get; set; } = null!;

        [Required, MaxLength(1500)]
        public string MessageText { get; set; } = "";

        public DateTime SentAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
    }

    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required, MaxLength(120)]
        public string Title { get; set; } = "";

        [Required, MaxLength(500)]
        public string Message { get; set; } = "";

        // "Booking", "Payment", "Arrival", "Departure", "ServiceRequest", "Review", "Security"
        [Required, MaxLength(30)]
        public string Type { get; set; } = "Booking";

        [MaxLength(250)]
        public string? LinkUrl { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // =========================================================================
    // MODULE 6: REVIEW & REPUTATION MANAGEMENT
    // =========================================================================
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;

        [Required]
        public int PropertyId { get; set; }

        [ForeignKey(nameof(PropertyId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public Property Property { get; set; } = null!;

        [Required]
        public int GuestId { get; set; }

        [ForeignKey(nameof(GuestId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public User Guest { get; set; } = null!;

        // Guest -> Property 4-Criteria Ratings (1–5)
        [Range(1, 5)]
        public int? CleanlinessRating { get; set; }

        [Range(1, 5)]
        public int? AccuracyRating { get; set; }

        [Range(1, 5)]
        public int? CommunicationRating { get; set; }

        [Range(1, 5)]
        public int? LocationRating { get; set; }

        [Range(0.0, 5.0)]
        public double OverallRating { get; set; } = 0.0;

        [MaxLength(1500)]
        public string? Comment { get; set; }

        [MaxLength(255)]
        public string? PhotoPath { get; set; }

        public DateTime? GuestSubmittedAt { get; set; }

        // Host -> Guest Mutual Review
        [Range(1, 5)]
        public int? HostRatingForGuest { get; set; }

        [MaxLength(1000)]
        public string? HostFeedbackForGuest { get; set; }

        [MaxLength(1000)]
        public string? HostReply { get; set; }

        public DateTime? HostSubmittedAt { get; set; }

        // Double-Blind Publishing Rule: Hidden until BOTH parties submit OR PublishDeadline expires
        public bool IsPublished { get; set; } = false;
        public DateTime PublishDeadline { get; set; } = DateTime.Now.AddDays(14);

        // Moderation & Engagement
        public bool IsFlagged { get; set; } = false;

        [MaxLength(300)]
        public string? FlagReason { get; set; }

        public int HelpfulVotes { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // =========================================================================
    // MODULE 8: SECURITY, AUDIT & DATABASE INFRASTRUCTURE
    // =========================================================================
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public int? UserId { get; set; }

        [Required, MaxLength(120)]
        public string UserEmail { get; set; } = "System";

        [Required, MaxLength(50)]
        public string Module { get; set; } = "Security";

        [Required, MaxLength(100)]
        public string Action { get; set; } = "";

        [MaxLength(60)]
        public string? EntityName { get; set; }

        public int? EntityId { get; set; }

        [Required, MaxLength(2000)]
        public string Details { get; set; } = "";

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        // "Info", "Warning", "Critical"
        [Required, MaxLength(20)]
        public string Severity { get; set; } = "Info";

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
