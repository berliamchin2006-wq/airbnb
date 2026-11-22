using System.ComponentModel.DataAnnotations;
using AirbnbBooking.Validation;
using Microsoft.AspNetCore.Mvc;

namespace AirbnbBooking.Models
{
    // =========================================================================
    // MODULE 1: USER & IDENTITY VIEWMODELS
    // =========================================================================
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(120)]
        [Remote(action: "CheckEmailAvailable", controller: "Account", ErrorMessage = "This email is already registered.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [StringLength(25, MinimumLength = 8)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "Please select an account role.")]
        [RegularExpression("^(Guest|Host)$", ErrorMessage = "Role must be Guest or Host.")]
        public string Role { get; set; } = "Guest";

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [StrongPassword]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = "";

        [Required(ErrorMessage = "Please solve the anti-bot CAPTCHA.")]
        [Display(Name = "Security CAPTCHA")]
        public string CaptchaAnswer { get; set; } = "";
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Keep me signed in (Rotating Token)")]
        public bool RememberMe { get; set; } = true;

        [Required(ErrorMessage = "Please answer the anti-bot CAPTCHA.")]
        [Display(Name = "Security CAPTCHA")]
        public string CaptchaAnswer { get; set; } = "";

        public string? ReturnUrl { get; set; }
    }

    public class VerifyEmailViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Enter the 6-digit verification code.")]
        [StringLength(10, MinimumLength = 6)]
        [Display(Name = "Verification Code (OTP)")]
        public string Code { get; set; } = "";
    }

    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";
    }

    public class ResetPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Token { get; set; } = "";

        [Required, DataType(DataType.Password), StrongPassword]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = "";

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } = "";
    }

    public class ProfileViewModel
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string CurrentProfilePhoto { get; set; } = "default.svg";
        public bool IsEmailVerified { get; set; }
        public string IdentityStatus { get; set; } = "Unverified";
        public string? IdentityNotes { get; set; }
        public string? CurrentIdDocumentPhoto { get; set; }
        public string MaskedBankAccount { get; set; } = "Not configured";

        [Required, StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = "";

        [Required, Phone, StringLength(25, MinimumLength = 8)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = "";

        [MaxLength(500)]
        public string? Bio { get; set; }

        [Display(Name = "Upload Profile Photo")]
        public IFormFile? ProfilePhotoFile { get; set; }

        public string? WebcamPhotoBase64 { get; set; }

        [Display(Name = "Identity Document (MyKad / Passport)")]
        public IFormFile? IdDocumentFile { get; set; }

        // Host Payout Banking (Encrypted at Rest via AES-256)
        [MaxLength(100), Display(Name = "Bank Name")]
        public string? BankName { get; set; }

        [MaxLength(40), Display(Name = "Bank Account Number")]
        public string? BankAccountNumber { get; set; }

        [MaxLength(100), Display(Name = "Account Holder Name")]
        public string? BankHolderName { get; set; }

        [Display(Name = "Theme")]
        public string ThemePreference { get; set; } = "light";

        [Display(Name = "Currency")]
        public string PreferredCurrency { get; set; } = "MYR";
    }

    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = "";

        [Required, DataType(DataType.Password), StrongPassword, Display(Name = "New Password")]
        public string NewPassword { get; set; } = "";

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm New Password")]
        public string ConfirmNewPassword { get; set; } = "";
    }

    // =========================================================================
    // MODULE 2: PROPERTY LISTING, SEARCH & DISCOVERY VIEWMODELS
    // =========================================================================
    public class PropertySearchViewModel
    {
        public string? Query { get; set; }
        public string? City { get; set; }
        public string? PropertyType { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CheckIn { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CheckOut { get; set; }

        [Range(1, 30)]
        public int? Guests { get; set; }

        [Range(0, 50000)]
        public decimal? MinPrice { get; set; }

        [Range(0, 50000)]
        public decimal? MaxPrice { get; set; }

        public List<int> SelectedAmenityIds { get; set; } = [];
        public string SortBy { get; set; } = "featured"; // featured, price_asc, price_desc, rating, newest
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 9;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        public List<Property> Results { get; set; } = [];
        public List<Amenity> AvailableAmenities { get; set; } = [];
        public List<string> AvailableCities { get; set; } = [];
        public HashSet<int> WishlistedPropertyIds { get; set; } = [];
    }

    public class PropertyFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Property title is required.")]
        [StringLength(150, MinimumLength = 6)]
        public string Title { get; set; } = "";

        [Required]
        [Display(Name = "Property Type")]
        public string PropertyType { get; set; } = "Condominium";

        [Required, MaxLength(255)]
        public string Address { get; set; } = "";

        [Required, MaxLength(60)]
        public string City { get; set; } = "";

        [Required, MaxLength(60)]
        public string State { get; set; } = "";

        [Required, MaxLength(15)]
        public string Postcode { get; set; } = "";

        [Range(-90, 90)]
        public double Latitude { get; set; } = 3.1390;

        [Range(-180, 180)]
        public double Longitude { get; set; } = 101.6869;

        [Required, StringLength(3000, MinimumLength = 20)]
        public string Description { get; set; } = "";

        [Required, Range(1, 30), Display(Name = "Max Guests")]
        public int MaxGuests { get; set; } = 4;

        [Required, Range(0, 20)]
        public int Bedrooms { get; set; } = 1;

        [Required, Range(1, 30)]
        public int Beds { get; set; } = 2;

        [Required, Range(1, 20)]
        public int Bathrooms { get; set; } = 1;

        [Required, Range(20, 50000), Display(Name = "Base Nightly Rate (RM)")]
        public decimal BaseNightlyRate { get; set; } = 200m;

        [Required, Range(0, 5000), Display(Name = "Cleaning Fee (RM)")]
        public decimal CleaningFee { get; set; } = 50m;

        [Required, Range(20, 50000), Display(Name = "Weekend Nightly Rate (RM)")]
        public decimal CustomWeekendRate { get; set; } = 240m;

        [Required, Display(Name = "Cancellation Policy")]
        public string CancellationPolicy { get; set; } = "Flexible";

        [MaxLength(1200), Display(Name = "House Rules")]
        public string HouseRules { get; set; } = "Check-in after 3:00 PM. Check-out by 11:00 AM. No smoking inside. Quiet hours 10:00 PM – 8:00 AM.";

        [Display(Name = "Active / Published")]
        public bool IsActive { get; set; } = true;

        public List<int> SelectedAmenityIds { get; set; } = [];
        public List<IFormFile>? Photos { get; set; }
        public int PrimaryPhotoIndex { get; set; } = 0;

        // Display helpers for Edit
        public List<Amenity> AllAmenities { get; set; } = [];
        public List<PropertyPhoto> ExistingPhotos { get; set; } = [];
        public List<SeasonalRate> ExistingSeasonalRates { get; set; } = [];
    }

    public class SeasonalRateInputViewModel : IValidatableObject
    {
        [Required]
        public int PropertyId { get; set; }

        [Required, StringLength(80, MinimumLength = 2), Display(Name = "Season / Holiday Name")]
        public string SeasonName { get; set; } = "";

        [Required, DataType(DataType.Date), Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(14);

        [Required, DataType(DataType.Date), Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(20);

        [Required, Range(20, 50000), Display(Name = "Seasonal Nightly Rate (RM)")]
        public decimal NightlyRate { get; set; } = 350m;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate.Date < StartDate.Date)
            {
                yield return new ValidationResult("End date cannot be earlier than start date.", [nameof(EndDate)]);
            }
        }
    }

    public class PropertyDetailViewModel
    {
        public Property Property { get; set; } = null!;
        public List<Review> PublishedReviews { get; set; } = [];
        public bool IsWishlisted { get; set; }
        public double AvgCleanliness { get; set; }
        public double AvgAccuracy { get; set; }
        public double AvgCommunication { get; set; }
        public double AvgLocation { get; set; }
        public BookingCreateViewModel BookingForm { get; set; } = new();
    }

    // =========================================================================
    // MODULE 3: BOOKING & RESERVATION VIEWMODELS
    // =========================================================================
    public class BookingCreateViewModel : IValidatableObject
    {
        [Required]
        public int PropertyId { get; set; }

        [Required, DataType(DataType.Date), FutureDate(AllowToday = true), Display(Name = "Check-In")]
        public DateTime CheckInDate { get; set; } = DateTime.Today.AddDays(3);

        [Required, DataType(DataType.Date), DateGreaterThan(nameof(CheckInDate), ErrorMessage = "Check-out date must be after check-in date."), Display(Name = "Check-Out")]
        public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(6);

        [Required, Range(1, 30), Display(Name = "Guests")]
        public int GuestCount { get; set; } = 2;

        [MaxLength(30), Display(Name = "Promo Code")]
        public string? PromoCode { get; set; }

        [MaxLength(500), Display(Name = "Special Requests")]
        public string? SpecialRequests { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if ((CheckOutDate.Date - CheckInDate.Date).Days > 60)
            {
                yield return new ValidationResult("Maximum short-stay booking duration is 60 nights.", [nameof(CheckOutDate)]);
            }
        }
    }

    // =========================================================================
    // MODULE 4: PAYMENT & TRANSACTION VIEWMODELS
    // =========================================================================
    public class CheckoutViewModel : IValidatableObject
    {
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Credit / Debit Card";

        [Display(Name = "Cardholder Name")]
        [MaxLength(100)]
        public string? CardHolderName { get; set; }

        [Display(Name = "Card Number")]
        [LuhnCardNumber]
        public string? CardNumber { get; set; }

        [Display(Name = "Expiry (MM/YY)")]
        [ValidCardExpiry]
        public string? CardExpiry { get; set; }

        [Display(Name = "CVV")]
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits.")]
        public string? CardCvv { get; set; }

        [Display(Name = "FPX Bank")]
        public string? FpxBank { get; set; }

        [Display(Name = "eWallet Phone")]
        public string? WalletPhone { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PaymentMethod == "Credit / Debit Card")
            {
                if (string.IsNullOrWhiteSpace(CardHolderName))
                    yield return new ValidationResult("Cardholder name is required for card payments.", [nameof(CardHolderName)]);
                if (string.IsNullOrWhiteSpace(CardNumber))
                    yield return new ValidationResult("Card number is required for card payments.", [nameof(CardNumber)]);
                if (string.IsNullOrWhiteSpace(CardExpiry))
                    yield return new ValidationResult("Card expiry (MM/YY) is required.", [nameof(CardExpiry)]);
                if (string.IsNullOrWhiteSpace(CardCvv))
                    yield return new ValidationResult("Card CVV is required.", [nameof(CardCvv)]);
            }
            else if (PaymentMethod == "FPX Online Banking" && string.IsNullOrWhiteSpace(FpxBank))
            {
                yield return new ValidationResult("Please choose your FPX bank.", [nameof(FpxBank)]);
            }
            else if (PaymentMethod == "Touch 'n Go eWallet" && string.IsNullOrWhiteSpace(WalletPhone))
            {
                yield return new ValidationResult("Please enter your eWallet mobile number.", [nameof(WalletPhone)]);
            }
            else if (PaymentMethod != "Credit / Debit Card" && PaymentMethod != "FPX Online Banking" && PaymentMethod != "Touch 'n Go eWallet")
            {
                yield return new ValidationResult("Invalid payment method selected.", [nameof(PaymentMethod)]);
            }
        }
    }

    // =========================================================================
    // MODULE 5: IN-STAY & OPERATIONS VIEWMODELS
    // =========================================================================
    public class ServiceRequestCreateViewModel
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public string Category { get; set; } = "Housekeeping";

        [Required, StringLength(150, MinimumLength = 4), Display(Name = "Issue Summary")]
        public string IssueTitle { get; set; } = "";

        [Required, StringLength(1500, MinimumLength = 10)]
        public string Description { get; set; } = "";

        [Required]
        public string Priority { get; set; } = "Medium";

        [Display(Name = "Attach Photo Evidence (Optional)")]
        public IFormFile? PhotoProofFile { get; set; }
    }

    // =========================================================================
    // MODULE 6: REVIEW & REPUTATION VIEWMODELS
    // =========================================================================
    public class GuestReviewViewModel
    {
        [Required]
        public int BookingId { get; set; }
        public string? PropertyTitle { get; set; }

        [Required, Range(1, 5), Display(Name = "Cleanliness")]
        public int CleanlinessRating { get; set; } = 5;

        [Required, Range(1, 5), Display(Name = "Accuracy")]
        public int AccuracyRating { get; set; } = 5;

        [Required, Range(1, 5), Display(Name = "Communication")]
        public int CommunicationRating { get; set; } = 5;

        [Required, Range(1, 5), Display(Name = "Location")]
        public int LocationRating { get; set; } = 5;

        [Required, StringLength(1500, MinimumLength = 10), Display(Name = "Your Review")]
        public string Comment { get; set; } = "";

        [Display(Name = "Attach Stay Photo (Optional)")]
        public IFormFile? ReviewPhoto { get; set; }
    }

    public class HostReviewViewModel
    {
        [Required]
        public int BookingId { get; set; }
        public string? GuestName { get; set; }
        public string? PropertyTitle { get; set; }

        [Required, Range(1, 5), Display(Name = "Guest Rating (1–5)")]
        public int HostRatingForGuest { get; set; } = 5;

        [Required, StringLength(1000, MinimumLength = 5), Display(Name = "Private/Mutual Feedback for Guest")]
        public string HostFeedbackForGuest { get; set; } = "";

        [MaxLength(1000), Display(Name = "Public Reply to Guest Review (Optional)")]
        public string? HostReply { get; set; }
    }

    // =========================================================================
    // MODULE 7: DASHBOARD & REPORTING VIEWMODELS
    // =========================================================================
    public class HostDashboardViewModel
    {
        public int TotalListings { get; set; }
        public int ActiveListings { get; set; }
        public int TotalBookings { get; set; }
        public int ActiveStaysCount { get; set; }
        public double OccupancyRatePercent { get; set; }
        public decimal TotalGrossEarnings { get; set; }
        public decimal TotalNetPayouts { get; set; }
        public decimal PendingPayoutAmount { get; set; }
        public double AverageHostRating { get; set; }
        public int OpenServiceRequests { get; set; }
        public int RangeDays { get; set; } = 90;

        public List<Booking> UpcomingArrivals { get; set; } = [];
        public List<Booking> UpcomingDepartures { get; set; } = [];
        public List<Booking> RecentBookings { get; set; } = [];
        public List<string> ChartLabels { get; set; } = [];
        public List<decimal> MonthlyEarnings { get; set; } = [];
        public List<int> MonthlyBookingCounts { get; set; } = [];
        public Dictionary<string, int> StatusDistribution { get; set; } = [];
    }

    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalHosts { get; set; }
        public int TotalGuests { get; set; }
        public int PendingHostVerifications { get; set; }
        public int TotalProperties { get; set; }
        public int ActiveProperties { get; set; }
        public int TotalBookings { get; set; }
        public decimal PlatformGmv { get; set; }
        public decimal PlatformCommissionRevenue { get; set; }
        public decimal TotalRefundsProcessed { get; set; }
        public int OpenDisputesCount { get; set; }
        public int FlaggedReviewsCount { get; set; }
        public int RangeDays { get; set; } = 180;

        public List<string> MonthLabels { get; set; } = [];
        public List<decimal> MonthlyGmv { get; set; } = [];
        public List<int> MonthlyNewUsers { get; set; } = [];
        public Dictionary<string, int> PropertyTypeCounts { get; set; } = [];
        public List<AuditLog> RecentSecurityEvents { get; set; } = [];
        public List<ServiceRequest> OpenDisputes { get; set; } = [];
    }

    // =========================================================================
    // MODULE 8: SECURITY & ADMIN VIEWMODELS
    // =========================================================================
    public class PromoCodeFormViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(30, MinimumLength = 3)]
        [RegularExpression("^[A-Z0-9_-]+$", ErrorMessage = "Use uppercase letters, numbers, hyphens, or underscores.")]
        public string Code { get; set; } = "";

        [Required, StringLength(150, MinimumLength = 4)]
        public string Description { get; set; } = "";

        [Required]
        public string DiscountType { get; set; } = "Percent";

        [Required, Range(1, 5000)]
        public decimal DiscountValue { get; set; } = 10m;

        [Range(0, 50000)]
        public decimal MinSpend { get; set; } = 100m;

        [Range(1, 100000)]
        public int MaxUses { get; set; } = 100;

        [Required, DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; } = DateTime.Today.AddMonths(6);

        public bool IsActive { get; set; } = true;
    }
}
