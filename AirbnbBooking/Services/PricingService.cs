using AirbnbBooking.Models;

namespace AirbnbBooking.Services
{
    public record NightlyPriceItem(DateTime Date, decimal Rate, string RateLabel);

    public record PriceBreakdownResult(
        int Nights,
        decimal AverageNightlyRate,
        decimal SubtotalAmount,
        decimal CleaningFee,
        decimal ServiceTax,
        decimal DiscountAmount,
        decimal TotalAmount,
        decimal HostGross,
        decimal HostPlatformFee,
        decimal HostNetPayout,
        List<NightlyPriceItem> NightlyBreakdown,
        string? AppliedPromoCode,
        string? PromoMessage);

    public record RefundCalculationResult(
        string PolicyName,
        decimal RefundPercent,
        decimal RefundAmount,
        string Explanation);

    public class PricingService
    {
        public const decimal GuestServiceTaxRate = 0.10m; // 10% platform service tax
        public const decimal HostCommissionRate = 0.03m;  // 3% host commission

        /// <summary>
        /// Computes dynamic per-night breakdown considering SeasonalRate overrides, Weekend rates, Base rates,
        /// cleaning fee, 10% service tax, and optional promo code.
        /// </summary>
        public PriceBreakdownResult CalculateBookingPrice(
            Property property,
            IEnumerable<SeasonalRate> seasonalRates,
            DateTime checkIn,
            DateTime checkOut,
            PromoCode? promo = null)
        {
            var start = checkIn.Date;
            var end = checkOut.Date;
            int nights = Math.Max(1, (end - start).Days);
            var seasons = seasonalRates.ToList();
            var breakdown = new List<NightlyPriceItem>();
            decimal subtotal = 0m;

            for (int i = 0; i < nights; i++)
            {
                var date = start.AddDays(i);
                var matchedSeason = seasons.FirstOrDefault(s => date >= s.StartDate.Date && date <= s.EndDate.Date);

                decimal rate;
                string label;
                if (matchedSeason != null)
                {
                    rate = matchedSeason.NightlyRate;
                    label = $"Seasonal ({matchedSeason.SeasonName})";
                }
                else if (date.DayOfWeek == DayOfWeek.Friday || date.DayOfWeek == DayOfWeek.Saturday)
                {
                    rate = property.CustomWeekendRate > 0 ? property.CustomWeekendRate : property.BaseNightlyRate;
                    label = "Weekend Rate";
                }
                else
                {
                    rate = property.BaseNightlyRate;
                    label = "Standard Rate";
                }

                breakdown.Add(new NightlyPriceItem(date, rate, label));
                subtotal += rate;
            }

            decimal avgNightly = Math.Round(subtotal / nights, 2);
            decimal cleaningFee = property.CleaningFee;
            decimal serviceTax = Math.Round(subtotal * GuestServiceTaxRate, 2);
            decimal preDiscountTotal = subtotal + cleaningFee + serviceTax;

            decimal discount = 0m;
            string? appliedCode = null;
            string? promoMsg = null;

            if (promo != null)
            {
                if (!promo.IsActive || promo.ExpiryDate.Date < DateTime.Today || promo.CurrentUses >= promo.MaxUses)
                {
                    promoMsg = "Promo code is expired or has reached its usage limit.";
                }
                else if (subtotal < promo.MinSpend)
                {
                    promoMsg = $"Minimum stay subtotal of RM {promo.MinSpend:N2} required for {promo.Code}.";
                }
                else
                {
                    discount = promo.DiscountType.Equals("Percent", StringComparison.OrdinalIgnoreCase)
                        ? Math.Round(preDiscountTotal * (promo.DiscountValue / 100m), 2)
                        : Math.Min(preDiscountTotal, promo.DiscountValue);
                    appliedCode = promo.Code;
                    promoMsg = $"Promo {promo.Code} applied (-RM {discount:N2}).";
                }
            }

            decimal total = Math.Max(0m, Math.Round(preDiscountTotal - discount, 2));
            decimal hostGross = subtotal + cleaningFee;
            decimal hostFee = Math.Round(hostGross * HostCommissionRate, 2);
            decimal hostNet = Math.Max(0m, Math.Round(hostGross - hostFee, 2));

            return new PriceBreakdownResult(
                nights,
                avgNightly,
                subtotal,
                cleaningFee,
                serviceTax,
                discount,
                total,
                hostGross,
                hostFee,
                hostNet,
                breakdown,
                appliedCode,
                promoMsg);
        }

        /// <summary>
        /// Enforces Flexible, Moderate, and Strict cancellation policies automatically.
        /// </summary>
        public RefundCalculationResult CalculateRefund(Booking booking, decimal paidAmount, DateTime cancellationTime)
        {
            string policy = booking.Property?.CancellationPolicy ?? "Flexible";
            double hoursBeforeCheckIn = (booking.CheckInDate.Date.AddHours(15) - cancellationTime).TotalHours;
            double daysBeforeCheckIn = hoursBeforeCheckIn / 24.0;

            decimal pct;
            string explanation;

            if (booking.Status == "Pending" || paidAmount <= 0)
            {
                return new RefundCalculationResult(policy, 0m, 0m, "Booking had not been paid yet; cancelled with no charge.");
            }

            switch (policy)
            {
                case "Strict":
                    if (daysBeforeCheckIn >= 7)
                    {
                        pct = 50m;
                        explanation = "Strict policy: cancelled 7+ days before check-in (50% refund).";
                    }
                    else
                    {
                        pct = 0m;
                        explanation = "Strict policy: cancelled within 7 days of check-in (non-refundable).";
                    }
                    break;

                case "Moderate":
                    if (daysBeforeCheckIn >= 5)
                    {
                        pct = 100m;
                        explanation = "Moderate policy: cancelled 5+ days before check-in (100% full refund).";
                    }
                    else if (hoursBeforeCheckIn >= 24)
                    {
                        pct = 50m;
                        explanation = "Moderate policy: cancelled 1–4 days before check-in (50% partial refund).";
                    }
                    else
                    {
                        pct = 0m;
                        explanation = "Moderate policy: cancelled within 24 hours of check-in (non-refundable).";
                    }
                    break;

                default: // Flexible
                    if (hoursBeforeCheckIn >= 24)
                    {
                        pct = 100m;
                        explanation = "Flexible policy: cancelled 24+ hours before check-in (100% full refund).";
                    }
                    else
                    {
                        pct = 50m;
                        explanation = "Flexible policy: cancelled within 24 hours of check-in (50% refund).";
                    }
                    break;
            }

            decimal refund = Math.Round(paidAmount * (pct / 100m), 2);
            return new RefundCalculationResult(policy, pct, refund, explanation);
        }
    }
}
