using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AirbnbBooking.Validation
{
    /// <summary>
    /// Ensures a date is today or in the future.
    /// </summary>
    public class FutureDateAttribute : ValidationAttribute
    {
        public bool AllowToday { get; set; } = true;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime dt)
            {
                var today = DateTime.Today;
                if (AllowToday ? dt.Date < today : dt.Date <= today)
                {
                    return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} must be today or a future date.");
                }
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Ensures a date property is strictly greater than another date property on the same ViewModel.
    /// </summary>
    public class DateGreaterThanAttribute : ValidationAttribute
    {
        private readonly string _comparisonProperty;

        public DateGreaterThanAttribute(string comparisonProperty)
        {
            _comparisonProperty = comparisonProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
            if (property == null) return ValidationResult.Success;

            var startVal = property.GetValue(validationContext.ObjectInstance);
            if (value is DateTime endDate && startVal is DateTime startDate)
            {
                if (endDate.Date <= startDate.Date)
                {
                    return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} must be after {_comparisonProperty}.");
                }
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Enforces strong password complexity: min 8 chars, uppercase, lowercase, digit, special character.
    /// </summary>
    public class StrongPasswordAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string password || string.IsNullOrEmpty(password))
                return ValidationResult.Success; // Let [Required] handle empty

            if (password.Length < 8 ||
                !Regex.IsMatch(password, @"[A-Z]") ||
                !Regex.IsMatch(password, @"[a-z]") ||
                !Regex.IsMatch(password, @"\d") ||
                !Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            {
                return new ValidationResult(ErrorMessage ?? "Password must be at least 8 characters and include uppercase, lowercase, a number, and a special character.");
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates credit/debit card numbers using the Luhn checksum algorithm when Card payment is selected.
    /// </summary>
    public class LuhnCardNumberAttribute : ValidationAttribute
    {
        public static bool PassesLuhn(string digitsOnly)
        {
            if (digitsOnly.Length < 13 || digitsOnly.Length > 19 || !digitsOnly.All(char.IsDigit))
                return false;

            int sum = 0;
            bool doubleDigit = false;
            for (int i = digitsOnly.Length - 1; i >= 0; i--)
            {
                int digit = digitsOnly[i] - '0';
                if (doubleDigit)
                {
                    digit *= 2;
                    if (digit > 9) digit -= 9;
                }
                sum += digit;
                doubleDigit = !doubleDigit;
            }
            return sum % 10 == 0;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string raw || string.IsNullOrWhiteSpace(raw))
                return ValidationResult.Success;

            string digits = Regex.Replace(raw, @"[\s-]", "");
            if (!PassesLuhn(digits))
            {
                return new ValidationResult(ErrorMessage ?? "Please enter a valid card number (failed Luhn check).");
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates MM/YY expiry string is properly formatted and not expired.
    /// </summary>
    public class ValidCardExpiryAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string raw || string.IsNullOrWhiteSpace(raw))
                return ValidationResult.Success;

            var match = Regex.Match(raw.Trim(), @"^(0[1-9]|1[0-2])\/(\d{2})$");
            if (!match.Success)
                return new ValidationResult(ErrorMessage ?? "Expiry date must follow MM/YY format.");

            int month = int.Parse(match.Groups[1].Value);
            int year = 2000 + int.Parse(match.Groups[2].Value);
            var expiryEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            if (expiryEnd < DateTime.Today)
                return new ValidationResult("This card has already expired.");

            return ValidationResult.Success;
        }
    }
}
