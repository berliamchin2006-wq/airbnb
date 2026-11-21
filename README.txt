================================================================================
STAYNEST — AIRBNB BOOKING SYSTEM (BMIT2023 WEB AND MOBILE SYSTEMS)
================================================================================

PROJECT SUMMARY
- Framework : .NET 10, ASP.NET Core MVC, C#
- ORM       : Entity Framework Core 10 (Code-First, 100% Data Annotations, Migrations)
- Database  : SQL Server Express LocalDB (file-based: AirbnbBooking\App_Data\AirbnbBookingDb.mdf)
- Auth      : Manual Cookie-Based Authentication + Rotating Session Tokens (NO ASP.NET Core Identity)
- Email     : Gmail SMTP (AirbnbBookingSystem@gmail.com)

--------------------------------------------------------------------------------
TEST LOGIN CREDENTIALS (All seeded accounts use password: Password123!)
--------------------------------------------------------------------------------

1. SYSTEM ADMINISTRATOR (Full Platform Access)
   Email    : admin@staynest.my
   Password : Password123!
   Role     : Admin

2. PROPERTY OWNER / HOST (Verified Superhost with KL, Penang, Sunway, Melaka, Ipoh listings)
   Email    : host1@staynest.my
   Password : Password123!
   Role     : Host

3. PROPERTY OWNER / HOST (Pending ID Verification with Langkawi, Cameron, KK listings + Blind Review Demo)
   Email    : host2@staynest.my
   Password : Password123!
   Role     : Host

4. GUEST ACCOUNT 1 (Active Checked-In Stay, Upcoming Confirmed Stay, Wishlist, Completed Stay)
   Email    : guest1@staynest.my
   Password : Password123!
   Role     : Guest

5. GUEST ACCOUNT 2 (Pending Payment Booking)
   Email    : guest2@staynest.my
   Password : Password123!
   Role     : Guest

6. GUEST ACCOUNT 3 (Submitted Guest Review waiting for Host 2 — Demonstrates 14-Day Double-Blind Rule)
   Email    : guest3@staynest.my
   Password : Password123!
   Role     : Guest

--------------------------------------------------------------------------------
PROMO CODES FOR TESTING CHECKOUT
--------------------------------------------------------------------------------
- WELCOME10 : 10% off stays (Min spend RM 200)
- STAY50    : RM 50 flat discount (Min spend RM 400)

--------------------------------------------------------------------------------
TEST PAYMENT CARD (Luhn Valid)
--------------------------------------------------------------------------------
- Card Number : 4242 4242 4242 4242
- Expiry      : 12/29
- CVV         : 123

--------------------------------------------------------------------------------
8 SYSTEM MODULES OVERVIEW
--------------------------------------------------------------------------------
Module 1 : User & Identity Management (AccountController)
Module 2 : Property Listing, Search & Discovery — Combined (PropertyController)
Module 3 : Booking & Reservation Management (BookingController)
Module 4 : Payment & Transaction Management (PaymentController)
Module 5 : In-Stay & Operations Management (StayController + StayHub SignalR)
Module 6 : Review & Reputation Management (ReviewController)
Module 7 : Dashboard & Reporting (DashboardController)
Module 8 : Security, Audit & Database Infrastructure (SecurityController + RateLimitMiddleware)

--------------------------------------------------------------------------------
HOW TO RUN
--------------------------------------------------------------------------------
1. Open AirbnbBooking.sln in Visual Studio 2026 (or run `dotnet run --project AirbnbBooking`).
2. On first launch, EF Core Migrations automatically create AirbnbBooking\App_Data\AirbnbBookingDb.mdf
   and seed sample properties, bookings, payments, payouts, reviews, and audit logs.
3. Visit http://localhost:5210
================================================================================
