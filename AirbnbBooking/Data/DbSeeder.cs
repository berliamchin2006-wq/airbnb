using AirbnbBooking.Models;

namespace AirbnbBooking.Data
{
    public static class DbSeeder
    {
        public static void Seed(DB db, Helper helper)
        {
            if (db.Users.Any()) return;

            // =================================================================
            // 1. Seed Users (Admin, Hosts, Guests)
            // =================================================================
            var admin = new User
            {
                Email = "admin@staynest.my",
                PasswordHash = helper.HashPassword("Password123!"),
                FullName = "System Administrator",
                PhoneNumber = "012-3456789",
                Role = "Admin",
                ProfilePhoto = "default.svg",
                Bio = "Platform Trust, Safety & Operations Lead.",
                IsEmailVerified = true,
                IdentityStatus = "Verified",
                IdentityApprovedAt = DateTime.Now.AddDays(-90),
                ThemePreference = "light",
                PreferredCurrency = "MYR",
                CreatedAt = DateTime.Now.AddDays(-90)
            };

            var host1 = new User
            {
                Email = "host1@staynest.my",
                PasswordHash = helper.HashPassword("Password123!"),
                FullName = "Tan Wei Ming",
                PhoneNumber = "016-8889911",
                Role = "Host",
                ProfilePhoto = "default.svg",
                Bio = "Superhost curating architectural city stays across Kuala Lumpur, George Town, and Melaka.",
                IsEmailVerified = true,
                IdentityStatus = "Verified",
                IdentityApprovedAt = DateTime.Now.AddDays(-45),
                BankName = "Maybank",
                BankAccountNumberEncrypted = helper.Encrypt("514012349876"),
                BankHolderName = "Tan Wei Ming",
                ThemePreference = "light",
                PreferredCurrency = "MYR",
                CreatedAt = DateTime.Now.AddDays(-75)
            };

            var host2 = new User
            {
                Email = "host2@staynest.my",
                PasswordHash = helper.HashPassword("Password123!"),
                FullName = "Lim Mei Ling",
                PhoneNumber = "017-5551234",
                Role = "Host",
                ProfilePhoto = "default.svg",
                Bio = "Boutique nature retreats and beachfront villas in Langkawi, Cameron Highlands, and Sabah.",
                IsEmailVerified = true,
                IdentityStatus = "PendingApproval",
                IdentityNotes = "Uploaded Malaysian MyKad for Superhost verification.",
                BankName = "CIMB Bank",
                BankAccountNumberEncrypted = helper.Encrypt("701234567890"),
                BankHolderName = "Lim Mei Ling",
                ThemePreference = "light",
                PreferredCurrency = "MYR",
                CreatedAt = DateTime.Now.AddDays(-40)
            };

            var guest1 = new User
            {
                Email = "guest1@staynest.my",
                PasswordHash = helper.HashPassword("Password123!"),
                FullName = "Nurul Aina",
                PhoneNumber = "011-22334455",
                Role = "Guest",
                ProfilePhoto = "default.svg",
                Bio = "Weekend architecture enthusiast & specialty coffee traveler.",
                IsEmailVerified = true,
                IdentityStatus = "Verified",
                ThemePreference = "light",
                PreferredCurrency = "MYR",
                CreatedAt = DateTime.Now.AddDays(-50)
            };

            var guest2 = new User
            {
                Email = "guest2@staynest.my",
                PasswordHash = helper.HashPassword("Password123!"),
                FullName = "Jason Wong",
                PhoneNumber = "019-3344556",
                Role = "Guest",
                ProfilePhoto = "default.svg",
                Bio = "Remote software engineer who loves highland cabins and beach villas.",
                IsEmailVerified = true,
                IdentityStatus = "Unverified",
                ThemePreference = "light",
                PreferredCurrency = "MYR",
                CreatedAt = DateTime.Now.AddDays(-25)
            };

            var guest3 = new User
            {
                Email = "guest3@staynest.my",
                PasswordHash = helper.HashPassword("Password123!"),
                FullName = "Priya Nair",
                PhoneNumber = "014-7788990",
                Role = "Guest",
                ProfilePhoto = "default.svg",
                Bio = "Family traveler exploring heritage towns across Malaysia.",
                IsEmailVerified = true,
                IdentityStatus = "Verified",
                ThemePreference = "light",
                PreferredCurrency = "MYR",
                CreatedAt = DateTime.Now.AddDays(-12)
            };

            db.Users.AddRange(admin, host1, host2, guest1, guest2, guest3);
            db.SaveChanges();

            // =================================================================
            // 2. Seed Normalized Amenities
            // =================================================================
            var amenities = new List<Amenity>
            {
                new() { Name = "High-Speed Wi-Fi", IconClass = "bi-wifi", Category = "Essentials" },
                new() { Name = "Air Conditioning", IconClass = "bi-snow", Category = "Essentials" },
                new() { Name = "Full Kitchen", IconClass = "bi-cup-hot", Category = "Dining" },
                new() { Name = "Free Parking", IconClass = "bi-p-circle", Category = "Facilities" },
                new() { Name = "Swimming Pool", IconClass = "bi-water", Category = "Leisure" },
                new() { Name = "Fitness Gym", IconClass = "bi-heart-pulse", Category = "Leisure" },
                new() { Name = "Smart 4K TV", IconClass = "bi-tv", Category = "Entertainment" },
                new() { Name = "Self Check-In", IconClass = "bi-key", Category = "Access" },
                new() { Name = "Dedicated Workspace", IconClass = "bi-laptop", Category = "Essentials" },
                new() { Name = "Sea / Skyline View", IconClass = "bi-binoculars", Category = "Scenic" },
                new() { Name = "Washer & Dryer", IconClass = "bi-droplet", Category = "Essentials" },
                new() { Name = "Pet Friendly", IconClass = "bi-emoji-smile", Category = "Policies" }
            };
            db.Amenities.AddRange(amenities);
            db.SaveChanges();

            int AmenId(string name) => amenities.First(a => a.Name == name).Id;

            // =================================================================
            // 3. Seed 8 Curated Properties with Real Spatial Coordinates
            // =================================================================
            var p1 = new Property
            {
                HostId = host1.Id,
                Title = "KLCC Sky Residence with Twin Towers View",
                PropertyType = "Condominium",
                Address = "199 Jalan Tun Razak, City Centre",
                City = "Kuala Lumpur",
                State = "Kuala Lumpur",
                Postcode = "50400",
                Latitude = 3.1579,
                Longitude = 101.7116,
                Description = "Sun-drenched 2-bedroom residence on the 42nd floor overlooking the Petronas Twin Towers. Enjoy rooftop infinity pool access, high-speed fiber Wi-Fi, designer kitchenware, and a 5-minute stroll to Suria KLCC and KLCC Park.",
                MaxGuests = 4,
                Bedrooms = 2,
                Beds = 2,
                Bathrooms = 2,
                BaseNightlyRate = 280m,
                CleaningFee = 60m,
                CustomWeekendRate = 340m,
                CancellationPolicy = "Flexible",
                HouseRules = "Check-in 3:00 PM · Check-out 11:00 AM · No indoor smoking · Quiet hours after 10:00 PM.",
                AverageRating = 5.0,
                ReviewCount = 1,
                CreatedAt = DateTime.Now.AddDays(-65)
            };

            var p2 = new Property
            {
                HostId = host1.Id,
                Title = "George Town Peranakan Heritage Shophouse Loft",
                PropertyType = "Townhouse",
                Address = "88 Armenian Street, UNESCO Heritage Zone",
                City = "George Town",
                State = "Penang",
                Postcode = "10200",
                Latitude = 5.4150,
                Longitude = 100.3364,
                Description = "Restored 1920s Peranakan shophouse blending timber courtyards, sunlit skylights, and contemporary minimalist interiors. Step outside into Penang's street-art alleys, heritage kopitiams, and artisan bakeries.",
                MaxGuests = 5,
                Bedrooms = 2,
                Beds = 3,
                Bathrooms = 2,
                BaseNightlyRate = 225m,
                CleaningFee = 50m,
                CustomWeekendRate = 270m,
                CancellationPolicy = "Moderate",
                HouseRules = "Check-in 3:00 PM · Please remove footwear on heritage timber floors · No smoking indoors.",
                AverageRating = 4.75,
                ReviewCount = 1,
                CreatedAt = DateTime.Now.AddDays(-58)
            };

            var p3 = new Property
            {
                HostId = host2.Id,
                Title = "Cameron Highlands Misty Tea Valley Cottage",
                PropertyType = "Chalet",
                Address = "Batu 33, Jalan Besar, Ringlet",
                City = "Cameron Highlands",
                State = "Pahang",
                Postcode = "39000",
                Latitude = 4.4721,
                Longitude = 101.3756,
                Description = "Tudor-inspired highland timber cottage surrounded by cool mist and emerald tea plantations. Features a stone hearth lounge, outdoor morning tea deck, and space for families seeking crisp mountain air.",
                MaxGuests = 6,
                Bedrooms = 3,
                Beds = 4,
                Bathrooms = 2,
                BaseNightlyRate = 340m,
                CleaningFee = 75m,
                CustomWeekendRate = 410m,
                CancellationPolicy = "Strict",
                HouseRules = "Check-in 2:00 PM · Check-out 12:00 PM · Family and pet friendly in outdoor garden.",
                AverageRating = 0.0,
                ReviewCount = 0,
                CreatedAt = DateTime.Now.AddDays(-48)
            };

            var p4 = new Property
            {
                HostId = host1.Id,
                Title = "Sunway Lagoon Canopy Penthouse Suite",
                PropertyType = "Condominium",
                Address = "Jalan Lagoon Selatan, Bandar Sunway",
                City = "Subang Jaya",
                State = "Selangor",
                Postcode = "47500",
                Latitude = 3.0673,
                Longitude = 101.6038,
                Description = "Expansive corner penthouse linked directly to Sunway Pyramid and Sunway Lagoon via covered walkway. Includes panoramic floor-to-ceiling glazing, 65-inch cinema screen, work desk, and resort pool.",
                MaxGuests = 8,
                Bedrooms = 3,
                Beds = 5,
                Bathrooms = 3,
                BaseNightlyRate = 420m,
                CleaningFee = 80m,
                CustomWeekendRate = 490m,
                CancellationPolicy = "Flexible",
                HouseRules = "Check-in 3:00 PM · Access card provided via smart lockbox · No loud parties.",
                AverageRating = 4.8,
                ReviewCount = 0,
                CreatedAt = DateTime.Now.AddDays(-38)
            };

            var p5 = new Property
            {
                HostId = host2.Id,
                Title = "Langkawi Cenang Sunset Beach Villa & Plunge Pool",
                PropertyType = "Villa",
                Address = "Pantai Cenang Coastal Walk",
                City = "Langkawi",
                State = "Kedah",
                Postcode = "07000",
                Latitude = 6.2929,
                Longitude = 99.7289,
                Description = "Private tropical sanctuary just 50 meters from Pantai Cenang shoreline. Dip into your private saltwater plunge pool at golden hour, cook outdoors, and fall asleep to ocean breezes.",
                MaxGuests = 4,
                Bedrooms = 2,
                Beds = 2,
                Bathrooms = 2,
                BaseNightlyRate = 580m,
                CleaningFee = 100m,
                CustomWeekendRate = 660m,
                CancellationPolicy = "Moderate",
                HouseRules = "Check-in 3:00 PM · Complimentary beach towels · Quiet hours after 11:00 PM.",
                AverageRating = 4.95,
                ReviewCount = 0,
                CreatedAt = DateTime.Now.AddDays(-32)
            };

            var p6 = new Property
            {
                HostId = host1.Id,
                Title = "Melaka Jonker Riverfront Balcony Studio",
                PropertyType = "Studio",
                Address = "15 Lorong Hang Jebat, Waterfront",
                City = "Melaka",
                State = "Melaka",
                Postcode = "75200",
                Latitude = 2.1950,
                Longitude = 102.2486,
                Description = "Boutique riverfront studio perched directly above the Melaka River, two minutes from Jonker Street Night Market. Sip morning coffee on a teak balcony as river cruises glide below.",
                MaxGuests = 2,
                Bedrooms = 1,
                Beds = 1,
                Bathrooms = 1,
                BaseNightlyRate = 165m,
                CleaningFee = 40m,
                CustomWeekendRate = 205m,
                CancellationPolicy = "Flexible",
                HouseRules = "Check-in 3:00 PM · Self check-in keypad · Keep balcony doors closed when AC is running.",
                AverageRating = 4.7,
                ReviewCount = 0,
                CreatedAt = DateTime.Now.AddDays(-24)
            };

            var p7 = new Property
            {
                HostId = host2.Id,
                Title = "Kota Kinabalu Jesselton Oceanfront Loft",
                PropertyType = "Apartment",
                Address = "Jesselton Quay Waterfront, Jalan Tun Fuad Stephens",
                City = "Kota Kinabalu",
                State = "Sabah",
                Postcode = "88400",
                Latitude = 5.9912,
                Longitude = 116.0774,
                Description = "Bright coastal apartment facing the South China Sea and Tunku Abdul Rahman islands. Minutes from Jesselton Point ferry jetty for island hopping, with sunset views right from the living room.",
                MaxGuests = 4,
                Bedrooms = 2,
                Beds = 2,
                Bathrooms = 2,
                BaseNightlyRate = 260m,
                CleaningFee = 55m,
                CustomWeekendRate = 310m,
                CancellationPolicy = "Moderate",
                HouseRules = "Check-in 3:00 PM · Rinse beach sand at lobby shower · No smoking.",
                AverageRating = 4.85,
                ReviewCount = 0,
                CreatedAt = DateTime.Now.AddDays(-18)
            };

            var p8 = new Property
            {
                HostId = host1.Id,
                Title = "Ipoh Limestone Valley Minimalist Bungalow",
                PropertyType = "Bungalow",
                Address = "22 Persiaran Tambun Indah",
                City = "Ipoh",
                State = "Perak",
                Postcode = "31400",
                Latitude = 4.6225,
                Longitude = 101.1489,
                Description = "Architectural single-storey concrete-and-timber bungalow framed by dramatic Ipoh limestone hills. Quiet cul-de-sac 8 minutes from Lost World of Tambun and famous Ipoh dim sum streets.",
                MaxGuests = 6,
                Bedrooms = 3,
                Beds = 4,
                Bathrooms = 2,
                BaseNightlyRate = 310m,
                CleaningFee = 65m,
                CustomWeekendRate = 370m,
                CancellationPolicy = "Flexible",
                HouseRules = "Check-in 3:00 PM · Free covered driveway for 2 cars · Pets welcome outdoors.",
                AverageRating = 0.0,
                ReviewCount = 0,
                CreatedAt = DateTime.Now.AddDays(-10)
            };

            db.Properties.AddRange(p1, p2, p3, p4, p5, p6, p7, p8);
            db.SaveChanges();

            // =================================================================
            // 4. Seed Multi-Image Galleries
            // =================================================================
            db.PropertyPhotos.AddRange(
                new PropertyPhoto { PropertyId = p1.Id, FileName = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=1000&q=80", Caption = "Living room overlooking KL skyline", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p1.Id, FileName = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?w=1000&q=80", Caption = "Master suite", IsPrimary = false, DisplayOrder = 2 },
                new PropertyPhoto { PropertyId = p1.Id, FileName = "https://images.unsplash.com/photo-1584622650111-993a426fbf0a?w=1000&q=80", Caption = "Spa bathroom", IsPrimary = false, DisplayOrder = 3 },

                new PropertyPhoto { PropertyId = p2.Id, FileName = "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?w=1000&q=80", Caption = "Courtyard facade", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p2.Id, FileName = "https://images.unsplash.com/photo-1513694203232-719a280e022f?w=1000&q=80", Caption = "Heritage dining loft", IsPrimary = false, DisplayOrder = 2 },

                new PropertyPhoto { PropertyId = p3.Id, FileName = "https://images.unsplash.com/photo-1518780664697-55e3ad937233?w=1000&q=80", Caption = "Highland cottage exterior", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p3.Id, FileName = "https://images.unsplash.com/photo-1542314831-068cd1dbfeeb?w=1000&q=80", Caption = "Cozy fireplace lounge", IsPrimary = false, DisplayOrder = 2 },

                new PropertyPhoto { PropertyId = p4.Id, FileName = "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?w=1000&q=80", Caption = "Penthouse living lounge", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p4.Id, FileName = "https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?w=1000&q=80", Caption = "Open kitchen & dining", IsPrimary = false, DisplayOrder = 2 },

                new PropertyPhoto { PropertyId = p5.Id, FileName = "https://images.unsplash.com/photo-1580587771525-78b9dba3b914?w=1000&q=80", Caption = "Private sunset plunge pool", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p5.Id, FileName = "https://images.unsplash.com/photo-1512915922686-57c11dde9b6b?w=1000&q=80", Caption = "Tropical garden deck", IsPrimary = false, DisplayOrder = 2 },

                new PropertyPhoto { PropertyId = p6.Id, FileName = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=1000&q=80", Caption = "Riverfront studio suite", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p6.Id, FileName = "https://images.unsplash.com/photo-1507089947368-19c1da9775ae?w=1000&q=80", Caption = "Bright kitchen nook", IsPrimary = false, DisplayOrder = 2 },

                new PropertyPhoto { PropertyId = p7.Id, FileName = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?w=1000&q=80", Caption = "Oceanfront bedroom", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p7.Id, FileName = "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=1000&q=80", Caption = "Sea view living room", IsPrimary = false, DisplayOrder = 2 },

                new PropertyPhoto { PropertyId = p8.Id, FileName = "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?w=1000&q=80", Caption = "Limestone valley bungalow", IsPrimary = true, DisplayOrder = 1 },
                new PropertyPhoto { PropertyId = p8.Id, FileName = "https://images.unsplash.com/photo-1600566753376-12c8ab7fb75b?w=1000&q=80", Caption = "Minimalist courtyard", IsPrimary = false, DisplayOrder = 2 }
            );

            // =================================================================
            // 5. Seed PropertyAmenity Links
            // =================================================================
            void TagAmenities(int pid, params string[] names)
            {
                foreach (var n in names)
                    db.PropertyAmenities.Add(new PropertyAmenity { PropertyId = pid, AmenityId = AmenId(n) });
            }

            TagAmenities(p1.Id, "High-Speed Wi-Fi", "Air Conditioning", "Full Kitchen", "Free Parking", "Swimming Pool", "Fitness Gym", "Smart 4K TV", "Self Check-In", "Sea / Skyline View");
            TagAmenities(p2.Id, "High-Speed Wi-Fi", "Air Conditioning", "Full Kitchen", "Smart 4K TV", "Self Check-In", "Dedicated Workspace");
            TagAmenities(p3.Id, "High-Speed Wi-Fi", "Full Kitchen", "Free Parking", "Smart 4K TV", "Self Check-In", "Pet Friendly");
            TagAmenities(p4.Id, "High-Speed Wi-Fi", "Air Conditioning", "Full Kitchen", "Free Parking", "Swimming Pool", "Fitness Gym", "Smart 4K TV", "Self Check-In", "Washer & Dryer");
            TagAmenities(p5.Id, "High-Speed Wi-Fi", "Air Conditioning", "Full Kitchen", "Free Parking", "Swimming Pool", "Sea / Skyline View", "Self Check-In");
            TagAmenities(p6.Id, "High-Speed Wi-Fi", "Air Conditioning", "Smart 4K TV", "Self Check-In", "Dedicated Workspace");
            TagAmenities(p7.Id, "High-Speed Wi-Fi", "Air Conditioning", "Full Kitchen", "Swimming Pool", "Sea / Skyline View", "Self Check-In", "Washer & Dryer");
            TagAmenities(p8.Id, "High-Speed Wi-Fi", "Air Conditioning", "Full Kitchen", "Free Parking", "Dedicated Workspace", "Pet Friendly", "Washer & Dryer");

            // =================================================================
            // 6. Seed Custom Seasonal Rates
            // =================================================================
            db.SeasonalRates.AddRange(
                new SeasonalRate { PropertyId = p1.Id, SeasonName = "Year-End Festive Peak", StartDate = DateTime.Today.AddDays(25), EndDate = DateTime.Today.AddDays(45), NightlyRate = 420m },
                new SeasonalRate { PropertyId = p5.Id, SeasonName = "Island Dry Season Peak", StartDate = DateTime.Today.AddDays(10), EndDate = DateTime.Today.AddDays(35), NightlyRate = 740m },
                new SeasonalRate { PropertyId = p3.Id, SeasonName = "School Holiday Retreat", StartDate = DateTime.Today.AddDays(15), EndDate = DateTime.Today.AddDays(30), NightlyRate = 460m }
            );

            // =================================================================
            // 7. Seed Promo Codes
            // =================================================================
            var promoWelcome = new PromoCode
            {
                Code = "WELCOME10",
                Description = "10% off your StayNest booking",
                DiscountType = "Percent",
                DiscountValue = 10m,
                MinSpend = 200m,
                MaxUses = 200,
                CurrentUses = 1,
                ExpiryDate = DateTime.Today.AddYears(1),
                IsActive = true
            };
            var promoHoliday = new PromoCode
            {
                Code = "STAY50",
                Description = "RM 50 instant discount on stays RM 400+",
                DiscountType = "Fixed",
                DiscountValue = 50m,
                MinSpend = 400m,
                MaxUses = 100,
                CurrentUses = 0,
                ExpiryDate = DateTime.Today.AddMonths(8),
                IsActive = true
            };
            db.PromoCodes.AddRange(promoWelcome, promoHoliday);
            db.SaveChanges();

            // =================================================================
            // 8. Seed Bookings across full State Machine
            // =================================================================
            var b1 = new Booking
            {
                BookingReference = "SN-2026-1001",
                PropertyId = p1.Id,
                GuestId = guest1.Id,
                CheckInDate = DateTime.Today.AddDays(-18),
                CheckOutDate = DateTime.Today.AddDays(-15),
                TotalNights = 3,
                GuestCount = 2,
                AverageNightlyRate = 280m,
                SubtotalAmount = 840m,
                CleaningFee = 60m,
                ServiceTax = 84m,
                TotalAmount = 984m,
                Status = "Completed",
                QrCheckInToken = "QR-SN-KLCC-1001",
                CheckedInAt = DateTime.Today.AddDays(-18).AddHours(15),
                CheckedOutAt = DateTime.Today.AddDays(-15).AddHours(11),
                BookingDate = DateTime.Today.AddDays(-35)
            };

            var b2 = new Booking
            {
                BookingReference = "SN-2026-1002",
                PropertyId = p2.Id,
                GuestId = guest2.Id,
                CheckInDate = DateTime.Today.AddDays(-12),
                CheckOutDate = DateTime.Today.AddDays(-9),
                TotalNights = 3,
                GuestCount = 3,
                AverageNightlyRate = 225m,
                SubtotalAmount = 675m,
                CleaningFee = 50m,
                ServiceTax = 67.50m,
                TotalAmount = 792.50m,
                Status = "Completed",
                QrCheckInToken = "QR-SN-PNG-1002",
                CheckedInAt = DateTime.Today.AddDays(-12).AddHours(15),
                CheckedOutAt = DateTime.Today.AddDays(-9).AddHours(11),
                BookingDate = DateTime.Today.AddDays(-24)
            };

            var b3 = new Booking
            {
                BookingReference = "SN-2026-1003",
                PropertyId = p3.Id,
                GuestId = guest3.Id,
                CheckInDate = DateTime.Today.AddDays(-6),
                CheckOutDate = DateTime.Today.AddDays(-4),
                TotalNights = 2,
                GuestCount = 4,
                AverageNightlyRate = 340m,
                SubtotalAmount = 680m,
                CleaningFee = 75m,
                ServiceTax = 68m,
                TotalAmount = 823m,
                Status = "Completed",
                QrCheckInToken = "QR-SN-CAM-1003",
                CheckedInAt = DateTime.Today.AddDays(-6).AddHours(14),
                CheckedOutAt = DateTime.Today.AddDays(-4).AddHours(12),
                BookingDate = DateTime.Today.AddDays(-18)
            };

            // Active Checked-In Stay (for demoing In-Stay Operations, Chat & Service Requests!)
            var b4 = new Booking
            {
                BookingReference = "SN-2026-1004",
                PropertyId = p4.Id,
                GuestId = guest1.Id,
                CheckInDate = DateTime.Today.AddDays(-1),
                CheckOutDate = DateTime.Today.AddDays(2),
                TotalNights = 3,
                GuestCount = 4,
                AverageNightlyRate = 420m,
                SubtotalAmount = 1260m,
                CleaningFee = 80m,
                ServiceTax = 126m,
                TotalAmount = 1466m,
                Status = "CheckedIn",
                SpecialRequests = "Late arrival around 6 PM, extra baby cot if available.",
                QrCheckInToken = "QR-SN-SUNWAY-1004",
                CheckedInAt = DateTime.Today.AddDays(-1).AddHours(18),
                BookingDate = DateTime.Today.AddDays(-10)
            };

            // Upcoming Confirmed Stay (for demoing QR Check-in & Cancellation Policy!)
            var b5 = new Booking
            {
                BookingReference = "SN-2026-1005",
                PropertyId = p5.Id,
                GuestId = guest1.Id,
                PromoCodeId = promoWelcome.Id,
                CheckInDate = DateTime.Today.AddDays(4),
                CheckOutDate = DateTime.Today.AddDays(7),
                TotalNights = 3,
                GuestCount = 2,
                AverageNightlyRate = 580m,
                SubtotalAmount = 1740m,
                CleaningFee = 100m,
                ServiceTax = 174m,
                DiscountAmount = 201.40m,
                TotalAmount = 1812.60m,
                Status = "Confirmed",
                SpecialRequests = "Airport pickup requested for 2 PM flight.",
                QrCheckInToken = "QR-SN-LGK-1005",
                BookingDate = DateTime.Today.AddDays(-3)
            };

            // Pending Payment Stay
            var b6 = new Booking
            {
                BookingReference = "SN-2026-1006",
                PropertyId = p6.Id,
                GuestId = guest2.Id,
                CheckInDate = DateTime.Today.AddDays(8),
                CheckOutDate = DateTime.Today.AddDays(10),
                TotalNights = 2,
                GuestCount = 2,
                AverageNightlyRate = 165m,
                SubtotalAmount = 330m,
                CleaningFee = 40m,
                ServiceTax = 33m,
                TotalAmount = 403m,
                Status = "Pending",
                QrCheckInToken = "QR-SN-MLK-1006",
                BookingDate = DateTime.Now.AddHours(-3)
            };

            db.Bookings.AddRange(b1, b2, b3, b4, b5, b6);
            db.SaveChanges();

            // =================================================================
            // 9. Seed Payments & Host Payout Ledger
            // =================================================================
            db.Payments.AddRange(
                new Payment { BookingId = b1.Id, Amount = 984m, PaymentDate = b1.BookingDate, PaymentMethod = "Credit / Debit Card", CardLast4 = "4242", TransactionRef = "TXN-KLCC-99881", Status = "Paid" },
                new Payment { BookingId = b2.Id, Amount = 792.50m, PaymentDate = b2.BookingDate, PaymentMethod = "FPX Online Banking", TransactionRef = "TXN-PNG-77412", Status = "Paid" },
                new Payment { BookingId = b3.Id, Amount = 823m, PaymentDate = b3.BookingDate, PaymentMethod = "Credit / Debit Card", CardLast4 = "1111", TransactionRef = "TXN-CAM-66201", Status = "Paid" },
                new Payment { BookingId = b4.Id, Amount = 1466m, PaymentDate = b4.BookingDate, PaymentMethod = "Touch 'n Go eWallet", TransactionRef = "TXN-SUN-33215", Status = "Paid" },
                new Payment { BookingId = b5.Id, Amount = 1812.60m, PaymentDate = b5.BookingDate, PaymentMethod = "Credit / Debit Card", CardLast4 = "4242", TransactionRef = "TXN-LGK-51209", Status = "Paid" }
            );

            db.HostPayouts.AddRange(
                new HostPayout { HostId = host1.Id, BookingId = b1.Id, GrossAmount = 900m, PlatformFee = 27m, NetPayoutAmount = 873m, Status = "Disbursed", PayoutReference = "PO-2026-001", ScheduledDate = b1.CheckOutDate.AddDays(1), DisbursedAt = b1.CheckOutDate.AddDays(1) },
                new HostPayout { HostId = host1.Id, BookingId = b2.Id, GrossAmount = 725m, PlatformFee = 21.75m, NetPayoutAmount = 703.25m, Status = "Disbursed", PayoutReference = "PO-2026-002", ScheduledDate = b2.CheckOutDate.AddDays(1), DisbursedAt = b2.CheckOutDate.AddDays(1) },
                new HostPayout { HostId = host2.Id, BookingId = b3.Id, GrossAmount = 755m, PlatformFee = 22.65m, NetPayoutAmount = 732.35m, Status = "Disbursed", PayoutReference = "PO-2026-003", ScheduledDate = b3.CheckOutDate.AddDays(1), DisbursedAt = b3.CheckOutDate.AddDays(1) },
                new HostPayout { HostId = host1.Id, BookingId = b4.Id, GrossAmount = 1340m, PlatformFee = 40.20m, NetPayoutAmount = 1299.80m, Status = "Pending", PayoutReference = "PO-2026-004", ScheduledDate = b4.CheckOutDate.AddDays(1) },
                new HostPayout { HostId = host2.Id, BookingId = b5.Id, GrossAmount = 1840m, PlatformFee = 55.20m, NetPayoutAmount = 1784.80m, Status = "Pending", PayoutReference = "PO-2026-005", ScheduledDate = b5.CheckOutDate.AddDays(1) }
            );

            // =================================================================
            // 10. Seed In-Stay Service Requests, Chat Messages & Notifications
            // =================================================================
            db.ServiceRequests.AddRange(
                new ServiceRequest
                {
                    BookingId = b4.Id,
                    GuestId = guest1.Id,
                    Category = "Extra Amenities",
                    IssueTitle = "Request for 2 extra bath towels and iron",
                    Description = "Could housekeeping drop off two extra bath towels and a steam iron this afternoon? Thank you!",
                    Priority = "Medium",
                    Status = "InProgress",
                    HostResponse = "Housekeeping team has been notified and will deliver by 3:30 PM.",
                    CreatedAt = DateTime.Now.AddHours(-5)
                },
                new ServiceRequest
                {
                    BookingId = b1.Id,
                    GuestId = guest1.Id,
                    Category = "Wi-Fi & Tech",
                    IssueTitle = "Smart TV HDMI cable replacement",
                    Description = "HDMI port 1 cable felt loose when connecting laptop.",
                    Priority = "Low",
                    Status = "Resolved",
                    HostResponse = "Replaced with a new braided 4K HDMI cable.",
                    RepairCost = 35m,
                    CreatedAt = DateTime.Today.AddDays(-17),
                    ResolvedAt = DateTime.Today.AddDays(-17).AddHours(3)
                }
            );

            db.BookingMessages.AddRange(
                new BookingMessage { BookingId = b4.Id, SenderId = guest1.Id, MessageText = "Hi Tan! We just checked in using the QR pass. The view is gorgeous!", SentAt = DateTime.Now.AddHours(-18), IsRead = true },
                new BookingMessage { BookingId = b4.Id, SenderId = host1.Id, MessageText = "Welcome Nurul! So glad you made it smoothly. Feel free to message anytime if you need anything during your stay.", SentAt = DateTime.Now.AddHours(-17), IsRead = true },
                new BookingMessage { BookingId = b4.Id, SenderId = guest1.Id, MessageText = "Thank you! I also logged a quick service request for two extra towels.", SentAt = DateTime.Now.AddHours(-4), IsRead = false }
            );

            db.Notifications.AddRange(
                new Notification { UserId = host1.Id, Title = "Active Guest Stay — Sunway Penthouse", Message = "Nurul Aina checked in to Sunway Lagoon Canopy Penthouse Suite.", Type = "Arrival", LinkUrl = $"/Stay/Index", CreatedAt = DateTime.Now.AddHours(-18) },
                new Notification { UserId = host2.Id, Title = "Upcoming Arrival in 4 Days", Message = "Nurul Aina arrives at Langkawi Cenang Sunset Beach Villa soon.", Type = "Arrival", LinkUrl = $"/Stay/Index", CreatedAt = DateTime.Now.AddHours(-6) },
                new Notification { UserId = guest1.Id, Title = "Booking Confirmed — Langkawi Villa", Message = "Your stay SN-2026-1005 is confirmed. QR Check-In Pass is ready.", Type = "Booking", LinkUrl = $"/Booking/CheckInPass/{b5.Id}", CreatedAt = DateTime.Now.AddDays(-3) },
                new Notification { UserId = admin.Id, Title = "Host Identity Pending Verification", Message = "Lim Mei Ling submitted MyKad documents for host verification.", Type = "Security", LinkUrl = "/Security/Users", CreatedAt = DateTime.Now.AddDays(-2) }
            );

            // =================================================================
            // 11. Seed Reviews (Published Mutual + 1 Double-Blind Pending Demo!)
            // =================================================================
            db.Reviews.AddRange(
                new Review
                {
                    BookingId = b1.Id,
                    PropertyId = p1.Id,
                    GuestId = guest1.Id,
                    CleanlinessRating = 5,
                    AccuracyRating = 5,
                    CommunicationRating = 5,
                    LocationRating = 5,
                    OverallRating = 5.0,
                    Comment = "Breathtaking Twin Towers view from the living room! Everything was spotless, check-in via QR was seamless, and Tan responded within minutes.",
                    GuestSubmittedAt = DateTime.Today.AddDays(-14),
                    HostRatingForGuest = 5,
                    HostFeedbackForGuest = "Nurul was a respectful and tidy guest. Welcome back anytime!",
                    HostReply = "Thank you so much Nurul! We loved hosting you at KLCC Sky Residence.",
                    HostSubmittedAt = DateTime.Today.AddDays(-13),
                    IsPublished = true,
                    PublishDeadline = DateTime.Today.AddDays(-1),
                    HelpfulVotes = 9,
                    CreatedAt = DateTime.Today.AddDays(-14)
                },
                new Review
                {
                    BookingId = b2.Id,
                    PropertyId = p2.Id,
                    GuestId = guest2.Id,
                    CleanlinessRating = 5,
                    AccuracyRating = 5,
                    CommunicationRating = 4,
                    LocationRating = 5,
                    OverallRating = 4.75,
                    Comment = "Authentic heritage loft right on Armenian Street. Walking distance to street murals and great coffee spots.",
                    GuestSubmittedAt = DateTime.Today.AddDays(-8),
                    HostRatingForGuest = 5,
                    HostFeedbackForGuest = "Jason took great care of the shophouse!",
                    HostSubmittedAt = DateTime.Today.AddDays(-7),
                    IsPublished = true,
                    PublishDeadline = DateTime.Today.AddDays(5),
                    HelpfulVotes = 5,
                    CreatedAt = DateTime.Today.AddDays(-8)
                },
                // Blind review waiting for Host 2 to submit -> demonstrates the 14-day double-blind publishing rule!
                new Review
                {
                    BookingId = b3.Id,
                    PropertyId = p3.Id,
                    GuestId = guest3.Id,
                    CleanlinessRating = 5,
                    AccuracyRating = 5,
                    CommunicationRating = 5,
                    LocationRating = 5,
                    OverallRating = 5.0,
                    Comment = "Magical misty mornings sipping tea on the cottage veranda. Kids loved the strawberry farms nearby!",
                    GuestSubmittedAt = DateTime.Today.AddDays(-3),
                    IsPublished = false,
                    PublishDeadline = DateTime.Today.AddDays(11),
                    CreatedAt = DateTime.Today.AddDays(-3)
                }
            );

            // =================================================================
            // 12. Seed Wishlists & Audit Logs
            // =================================================================
            db.Wishlists.AddRange(
                new Wishlist { GuestId = guest1.Id, PropertyId = p2.Id, FolderName = "Penang Weekend", AddedAt = DateTime.Now.AddDays(-8) },
                new Wishlist { GuestId = guest1.Id, PropertyId = p5.Id, FolderName = "Island Escapes", AddedAt = DateTime.Now.AddDays(-5) },
                new Wishlist { GuestId = guest1.Id, PropertyId = p7.Id, FolderName = "Island Escapes", AddedAt = DateTime.Now.AddDays(-2) }
            );

            db.SaveChanges();

            helper.LogAudit(db, admin.Id, admin.Email, "Security", "SystemInitialized", "StayNest 8-module Airbnb database migrated and seeded with Malaysian properties & bookings.", "Info", "System");
            helper.LogAudit(db, host1.Id, host1.Email, "UserIdentity", "HostVerified", "Host Tan Wei Ming passed identity verification and encrypted banking onboarding.", "Info", "User", host1.Id);
            helper.LogAudit(db, guest1.Id, guest1.Email, "Payment", "PaymentCaptured", "Captured RM 1,812.60 via Credit Card (****4242) for booking SN-2026-1005.", "Info", "Booking", b5.Id);
        }
    }
}
