using System.Text;
using AirbnbBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirbnbBooking.Controllers
{
    /// <summary>
    /// Module 2 (Combined): Property Listing, Search & Discovery
    /// Full Host CRUD, Multi-Photo Gallery + Primary Designation, Amenities, Seasonal Rates,
    /// Dynamic AJAX Search/Filter/Sort/Paging, Real-Time Availability Exclusion, Leaflet Map & Wishlists.
    /// </summary>
    public class PropertyController : Controller
    {
        private readonly DB _db;
        private readonly Helper _helper;

        public PropertyController(DB db, Helper helper)
        {
            _db = db;
            _helper = helper;
        }

        // =====================================================================
        // Public Discovery & Dynamic Search (Full Page + AJAX Partial)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Catalog(PropertySearchViewModel filter)
        {
            await _helper.SyncReviewVisibilityAndAggregateAsync(_db);
            var vm = await ExecuteSearchAsync(filter);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SearchAjax(PropertySearchViewModel filter)
        {
            var vm = await ExecuteSearchAsync(filter);
            return PartialView("_PropertyCatalogGrid", vm);
        }

        private async Task<PropertySearchViewModel> ExecuteSearchAsync(PropertySearchViewModel filter)
        {
            var query = _db.Properties
                .Include(p => p.Photos)
                .Include(p => p.Host)
                .Include(p => p.PropertyAmenities).ThenInclude(pa => pa.Amenity)
                .Include(p => p.SeasonalRates)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Query))
            {
                string q = filter.Query.Trim().ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(q) ||
                    p.City.ToLower().Contains(q) ||
                    p.State.ToLower().Contains(q) ||
                    p.Description.ToLower().Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                query = query.Where(p => p.City == filter.City);
            }

            if (!string.IsNullOrWhiteSpace(filter.PropertyType))
            {
                query = query.Where(p => p.PropertyType == filter.PropertyType);
            }

            if (filter.Guests.HasValue && filter.Guests.Value > 0)
            {
                query = query.Where(p => p.MaxGuests >= filter.Guests.Value);
            }

            if (filter.MinPrice.HasValue)
            {
                query = query.Where(p => p.BaseNightlyRate >= filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(p => p.BaseNightlyRate <= filter.MaxPrice.Value);
            }

            // Amenity filtering (must contain all selected amenities)
            if (filter.SelectedAmenityIds != null && filter.SelectedAmenityIds.Count > 0)
            {
                foreach (var amenityId in filter.SelectedAmenityIds)
                {
                    int aid = amenityId;
                    query = query.Where(p => p.PropertyAmenities.Any(pa => pa.AmenityId == aid));
                }
            }

            // Real-time availability check: exclude properties with overlapping active bookings
            if (filter.CheckIn.HasValue && filter.CheckOut.HasValue && filter.CheckOut.Value.Date > filter.CheckIn.Value.Date)
            {
                var reqStart = filter.CheckIn.Value.Date;
                var reqEnd = filter.CheckOut.Value.Date;

                query = query.Where(p => !p.Bookings.Any(b =>
                    b.Status != "Cancelled" &&
                    b.CheckInDate < reqEnd &&
                    b.CheckOutDate > reqStart));
            }

            // Dynamic sorting
            query = filter.SortBy switch
            {
                "price_asc" => query.OrderBy(p => p.BaseNightlyRate),
                "price_desc" => query.OrderByDescending(p => p.BaseNightlyRate),
                "rating" => query.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.ReviewCount),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.AverageRating).ThenBy(p => p.Id)
            };

            filter.Page = Math.Max(1, filter.Page);
            filter.PageSize = 9;
            filter.TotalCount = await query.CountAsync();
            filter.Results = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            filter.AvailableAmenities = await _db.Amenities.OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync();
            filter.AvailableCities = await _db.Properties
                .Where(p => p.IsActive)
                .Select(p => p.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var uid = _helper.GetCurrentUserId();
            if (uid.HasValue)
            {
                filter.WishlistedPropertyIds = (await _db.Wishlists
                    .Where(w => w.GuestId == uid.Value)
                    .Select(w => w.PropertyId)
                    .ToListAsync()).ToHashSet();
            }

            return filter;
        }

        // =====================================================================
        // Property Detail View (Gallery, Map, Seasonal Rates, Reviews, Booking Widget)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id, DateTime? checkIn = null, DateTime? checkOut = null, int? guests = null)
        {
            await _helper.SyncReviewVisibilityAndAggregateAsync(_db, id);

            var prop = await _db.Properties
                .Include(p => p.Host)
                .Include(p => p.Photos.OrderByDescending(ph => ph.IsPrimary).ThenBy(ph => ph.DisplayOrder))
                .Include(p => p.PropertyAmenities).ThenInclude(pa => pa.Amenity)
                .Include(p => p.SeasonalRates.OrderBy(s => s.StartDate))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prop == null) return NotFound();
            if (!prop.IsActive && !_helper.IsOwnerOrAdmin(prop.HostId)) return NotFound();

            var reviews = await _db.Reviews
                .Include(r => r.Guest)
                .Where(r => r.PropertyId == id && r.IsPublished && r.GuestSubmittedAt != null)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var uid = _helper.GetCurrentUserId();
            bool isWishlisted = uid.HasValue && await _db.Wishlists.AnyAsync(w => w.GuestId == uid.Value && w.PropertyId == id);

            var defaultIn = checkIn.HasValue && checkIn.Value.Date >= DateTime.Today ? checkIn.Value.Date : DateTime.Today.AddDays(3);
            var defaultOut = checkOut.HasValue && checkOut.Value.Date > defaultIn ? checkOut.Value.Date : defaultIn.AddDays(3);
            int defaultGuests = guests.HasValue && guests.Value >= 1 ? Math.Min(guests.Value, prop.MaxGuests) : Math.Min(2, prop.MaxGuests);

            var vm = new PropertyDetailViewModel
            {
                Property = prop,
                PublishedReviews = reviews,
                IsWishlisted = isWishlisted,
                AvgCleanliness = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.CleanlinessRating ?? 5), 2) : 0,
                AvgAccuracy = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.AccuracyRating ?? 5), 2) : 0,
                AvgCommunication = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.CommunicationRating ?? 5), 2) : 0,
                AvgLocation = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.LocationRating ?? 5), 2) : 0,
                BookingForm = new BookingCreateViewModel
                {
                    PropertyId = prop.Id,
                    CheckInDate = defaultIn,
                    CheckOutDate = defaultOut,
                    GuestCount = defaultGuests
                }
            };

            return View(vm);
        }

        // =====================================================================
        // Host Property Management (CRUD, Multi-Photo, Seasonal Rates, Batch)
        // =====================================================================
        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> MyProperties()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";

            var props = await _db.Properties
                .Include(p => p.Photos)
                .Include(p => p.SeasonalRates)
                .Include(p => p.Bookings)
                .Where(p => isAdmin || p.HostId == uid)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(props);
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new PropertyFormViewModel
            {
                AllAmenities = await _db.Amenities.OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync()
            };
            return View(vm);
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PropertyFormViewModel vm)
        {
            if (vm.Photos != null)
            {
                foreach (var file in vm.Photos)
                {
                    var err = _helper.ValidatePhoto(file);
                    if (err != null) ModelState.AddModelError(nameof(vm.Photos), $"{file.FileName}: {err}");
                }
            }

            if (!ModelState.IsValid)
            {
                vm.AllAmenities = await _db.Amenities.OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync();
                return View(vm);
            }

            int uid = _helper.GetCurrentUserId()!.Value;
            var prop = new Property
            {
                HostId = uid,
                Title = vm.Title.Trim(),
                PropertyType = vm.PropertyType,
                Address = vm.Address.Trim(),
                City = vm.City.Trim(),
                State = vm.State.Trim(),
                Postcode = vm.Postcode.Trim(),
                Latitude = vm.Latitude,
                Longitude = vm.Longitude,
                Description = vm.Description.Trim(),
                MaxGuests = vm.MaxGuests,
                Bedrooms = vm.Bedrooms,
                Beds = vm.Beds,
                Bathrooms = vm.Bathrooms,
                BaseNightlyRate = vm.BaseNightlyRate,
                CleaningFee = vm.CleaningFee,
                CustomWeekendRate = vm.CustomWeekendRate,
                CancellationPolicy = vm.CancellationPolicy,
                HouseRules = vm.HouseRules.Trim(),
                IsActive = vm.IsActive,
                CreatedAt = DateTime.Now
            };

            _db.Properties.Add(prop);
            await _db.SaveChangesAsync();

            // Amenity tagging
            if (vm.SelectedAmenityIds != null)
            {
                foreach (var aid in vm.SelectedAmenityIds.Distinct())
                {
                    _db.PropertyAmenities.Add(new PropertyAmenity { PropertyId = prop.Id, AmenityId = aid });
                }
            }

            // Multi-photo gallery save with primary designation
            if (vm.Photos != null && vm.Photos.Count > 0)
            {
                int idx = 0;
                foreach (var file in vm.Photos)
                {
                    string saved = _helper.SavePhoto(file, "properties");
                    _db.PropertyPhotos.Add(new PropertyPhoto
                    {
                        PropertyId = prop.Id,
                        FileName = saved,
                        IsPrimary = idx == vm.PrimaryPhotoIndex,
                        DisplayOrder = idx + 1
                    });
                    idx++;
                }
            }
            else
            {
                _db.PropertyPhotos.Add(new PropertyPhoto
                {
                    PropertyId = prop.Id,
                    FileName = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=1000&q=80",
                    IsPrimary = true,
                    DisplayOrder = 1
                });
            }

            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "Property", "PropertyCreated",
                $"Created listing '{prop.Title}' in {prop.City} (RM {prop.BaseNightlyRate:N2}/night).", "Info", "Property", prop.Id);

            TempData["Success"] = $"Listing \"{prop.Title}\" created! You can now also configure custom seasonal rates.";
            return RedirectToAction(nameof(Edit), new { id = prop.Id });
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var prop = await _db.Properties
                .Include(p => p.Photos.OrderByDescending(ph => ph.IsPrimary).ThenBy(ph => ph.DisplayOrder))
                .Include(p => p.PropertyAmenities)
                .Include(p => p.SeasonalRates.OrderBy(s => s.StartDate))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prop == null) return NotFound();

            // ABAC: Ensure host owns this property (or is Admin)
            if (!_helper.IsOwnerOrAdmin(prop.HostId))
            {
                _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Security", "UnauthorizedEditAttempt",
                    $"Blocked unauthorized edit attempt on Property #{id} owned by Host #{prop.HostId}.", "Critical", "Property", id);
                return Forbid();
            }

            var vm = new PropertyFormViewModel
            {
                Id = prop.Id,
                Title = prop.Title,
                PropertyType = prop.PropertyType,
                Address = prop.Address,
                City = prop.City,
                State = prop.State,
                Postcode = prop.Postcode,
                Latitude = prop.Latitude,
                Longitude = prop.Longitude,
                Description = prop.Description,
                MaxGuests = prop.MaxGuests,
                Bedrooms = prop.Bedrooms,
                Beds = prop.Beds,
                Bathrooms = prop.Bathrooms,
                BaseNightlyRate = prop.BaseNightlyRate,
                CleaningFee = prop.CleaningFee,
                CustomWeekendRate = prop.CustomWeekendRate,
                CancellationPolicy = prop.CancellationPolicy,
                HouseRules = prop.HouseRules,
                IsActive = prop.IsActive,
                SelectedAmenityIds = prop.PropertyAmenities.Select(pa => pa.AmenityId).ToList(),
                AllAmenities = await _db.Amenities.OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync(),
                ExistingPhotos = prop.Photos,
                ExistingSeasonalRates = prop.SeasonalRates
            };

            return View(vm);
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PropertyFormViewModel vm)
        {
            var prop = await _db.Properties
                .Include(p => p.Photos)
                .Include(p => p.PropertyAmenities)
                .Include(p => p.SeasonalRates)
                .FirstOrDefaultAsync(p => p.Id == vm.Id);

            if (prop == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(prop.HostId)) return Forbid();

            if (vm.Photos != null)
            {
                foreach (var file in vm.Photos)
                {
                    var err = _helper.ValidatePhoto(file);
                    if (err != null) ModelState.AddModelError(nameof(vm.Photos), $"{file.FileName}: {err}");
                }
            }

            if (!ModelState.IsValid)
            {
                vm.AllAmenities = await _db.Amenities.OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync();
                vm.ExistingPhotos = prop.Photos;
                vm.ExistingSeasonalRates = prop.SeasonalRates;
                return View(vm);
            }

            prop.Title = vm.Title.Trim();
            prop.PropertyType = vm.PropertyType;
            prop.Address = vm.Address.Trim();
            prop.City = vm.City.Trim();
            prop.State = vm.State.Trim();
            prop.Postcode = vm.Postcode.Trim();
            prop.Latitude = vm.Latitude;
            prop.Longitude = vm.Longitude;
            prop.Description = vm.Description.Trim();
            prop.MaxGuests = vm.MaxGuests;
            prop.Bedrooms = vm.Bedrooms;
            prop.Beds = vm.Beds;
            prop.Bathrooms = vm.Bathrooms;
            prop.BaseNightlyRate = vm.BaseNightlyRate;
            prop.CleaningFee = vm.CleaningFee;
            prop.CustomWeekendRate = vm.CustomWeekendRate;
            prop.CancellationPolicy = vm.CancellationPolicy;
            prop.HouseRules = vm.HouseRules.Trim();
            prop.IsActive = vm.IsActive;

            // Replace amenity tags
            _db.PropertyAmenities.RemoveRange(prop.PropertyAmenities);
            if (vm.SelectedAmenityIds != null)
            {
                foreach (var aid in vm.SelectedAmenityIds.Distinct())
                {
                    _db.PropertyAmenities.Add(new PropertyAmenity { PropertyId = prop.Id, AmenityId = aid });
                }
            }

            // Append newly uploaded photos
            if (vm.Photos != null && vm.Photos.Count > 0)
            {
                int nextOrder = prop.Photos.Count > 0 ? prop.Photos.Max(p => p.DisplayOrder) + 1 : 1;
                bool hasPrimary = prop.Photos.Any(p => p.IsPrimary);
                foreach (var file in vm.Photos)
                {
                    string saved = _helper.SavePhoto(file, "properties");
                    _db.PropertyPhotos.Add(new PropertyPhoto
                    {
                        PropertyId = prop.Id,
                        FileName = saved,
                        IsPrimary = !hasPrimary,
                        DisplayOrder = nextOrder++
                    });
                    hasPrimary = true;
                }
            }

            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Property", "PropertyUpdated",
                $"Updated listing '{prop.Title}' (#{prop.Id}).", "Info", "Property", prop.Id);

            TempData["Success"] = "Property updated successfully.";
            return RedirectToAction(nameof(Edit), new { id = prop.Id });
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryPhoto(int propertyId, int photoId)
        {
            var prop = await _db.Properties.Include(p => p.Photos).FirstOrDefaultAsync(p => p.Id == propertyId);
            if (prop == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(prop.HostId)) return Forbid();

            foreach (var ph in prop.Photos)
            {
                ph.IsPrimary = ph.Id == photoId;
            }
            await _db.SaveChangesAsync();
            TempData["Success"] = "Primary cover photo updated.";
            return RedirectToAction(nameof(Edit), new { id = propertyId });
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(int propertyId, int photoId)
        {
            var prop = await _db.Properties.Include(p => p.Photos).FirstOrDefaultAsync(p => p.Id == propertyId);
            if (prop == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(prop.HostId)) return Forbid();

            var photo = prop.Photos.FirstOrDefault(ph => ph.Id == photoId);
            if (photo != null && prop.Photos.Count > 1)
            {
                bool wasPrimary = photo.IsPrimary;
                _db.PropertyPhotos.Remove(photo);
                await _db.SaveChangesAsync();

                if (wasPrimary)
                {
                    var firstRemaining = await _db.PropertyPhotos.FirstOrDefaultAsync(p => p.PropertyId == propertyId);
                    if (firstRemaining != null)
                    {
                        firstRemaining.IsPrimary = true;
                        await _db.SaveChangesAsync();
                    }
                }
                TempData["Success"] = "Photo removed from gallery.";
            }
            else
            {
                TempData["Error"] = "A listing must keep at least one photo.";
            }

            return RedirectToAction(nameof(Edit), new { id = propertyId });
        }

        // Custom Seasonal Rate Management
        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSeasonalRate(SeasonalRateInputViewModel vm)
        {
            var prop = await _db.Properties.FindAsync(vm.PropertyId);
            if (prop == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(prop.HostId)) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Edit), new { id = vm.PropertyId });
            }

            bool overlaps = await _db.SeasonalRates.AnyAsync(s =>
                s.PropertyId == vm.PropertyId &&
                s.StartDate <= vm.EndDate.Date &&
                s.EndDate >= vm.StartDate.Date);

            if (overlaps)
            {
                TempData["Error"] = "This seasonal rate overlaps with an existing seasonal date range.";
                return RedirectToAction(nameof(Edit), new { id = vm.PropertyId });
            }

            _db.SeasonalRates.Add(new SeasonalRate
            {
                PropertyId = vm.PropertyId,
                SeasonName = vm.SeasonName.Trim(),
                StartDate = vm.StartDate.Date,
                EndDate = vm.EndDate.Date,
                NightlyRate = vm.NightlyRate
            });
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Property", "SeasonalRateAdded",
                $"Added seasonal rate '{vm.SeasonName}' (RM {vm.NightlyRate:N2}) to Property #{vm.PropertyId}.", "Info", "Property", vm.PropertyId);

            TempData["Success"] = $"Seasonal rate \"{vm.SeasonName}\" added.";
            return RedirectToAction(nameof(Edit), new { id = vm.PropertyId });
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSeasonalRate(int id)
        {
            var rate = await _db.SeasonalRates.Include(s => s.Property).FirstOrDefaultAsync(s => s.Id == id);
            if (rate == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(rate.Property.HostId)) return Forbid();

            int pid = rate.PropertyId;
            _db.SeasonalRates.Remove(rate);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Seasonal rate removed.";
            return RedirectToAction(nameof(Edit), new { id = pid });
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var prop = await _db.Properties.Include(p => p.Bookings).FirstOrDefaultAsync(p => p.Id == id);
            if (prop == null) return NotFound();
            if (!_helper.IsOwnerOrAdmin(prop.HostId)) return Forbid();

            bool hasActiveBookings = prop.Bookings.Any(b => b.Status is "Confirmed" or "CheckedIn" or "Pending");
            if (hasActiveBookings)
            {
                prop.IsActive = false;
                await _db.SaveChangesAsync();
                TempData["Error"] = "Listing has active reservations so it was unpublished instead of permanently deleted.";
                return RedirectToAction(nameof(MyProperties));
            }

            _db.Properties.Remove(prop);
            await _db.SaveChangesAsync();

            _helper.LogAudit(_db, _helper.GetCurrentUserId(), _helper.GetCurrentUserEmail(), "Property", "PropertyDeleted",
                $"Deleted listing '{prop.Title}' (#{id}).", "Warning", "Property", id);

            TempData["Success"] = "Property listing deleted.";
            return RedirectToAction(nameof(MyProperties));
        }

        // Batch Operations for Hosts (Publish / Unpublish / Adjust Price %)
        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchAction(int[] selectedIds, string batchOperation, decimal? pricePercent)
        {
            if (selectedIds == null || selectedIds.Length == 0)
            {
                TempData["Error"] = "Select at least one property to perform a batch action.";
                return RedirectToAction(nameof(MyProperties));
            }

            int uid = _helper.GetCurrentUserId()!.Value;
            bool isAdmin = _helper.GetCurrentUserRole() == "Admin";

            var props = await _db.Properties
                .Where(p => selectedIds.Contains(p.Id) && (isAdmin || p.HostId == uid))
                .ToListAsync();

            foreach (var p in props)
            {
                switch (batchOperation)
                {
                    case "publish":
                        p.IsActive = true;
                        break;
                    case "unpublish":
                        p.IsActive = false;
                        break;
                    case "adjust_price" when pricePercent.HasValue && pricePercent.Value is >= -50 and <= 100:
                        decimal factor = 1m + (pricePercent.Value / 100m);
                        p.BaseNightlyRate = Math.Max(20m, Math.Round(p.BaseNightlyRate * factor, 2));
                        p.CustomWeekendRate = Math.Max(20m, Math.Round(p.CustomWeekendRate * factor, 2));
                        break;
                }
            }

            await _db.SaveChangesAsync();
            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "Property", "BatchPropertyUpdate",
                $"Applied batch operation '{batchOperation}' to {props.Count} listings.", "Info");

            TempData["Success"] = $"Batch action applied to {props.Count} listing(s).";
            return RedirectToAction(nameof(MyProperties));
        }

        // Batch CSV Import for Hosts
        [Authorize(Roles = "Host,Admin")]
        [HttpGet]
        public IActionResult BatchImport() => View();

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchImport(IFormFile? csvFile)
        {
            if (csvFile == null || csvFile.Length == 0 || !csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Please upload a valid .csv file.";
                return View();
            }

            int uid = _helper.GetCurrentUserId()!.Value;
            int imported = 0;

            using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
            string? header = await reader.ReadLineAsync(); // skip header
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = line.Split(',');
                if (cols.Length < 8) continue;

                if (!decimal.TryParse(cols[6].Trim(), out decimal rate)) rate = 200m;
                if (!int.TryParse(cols[7].Trim(), out int maxGuests)) maxGuests = 4;

                var prop = new Property
                {
                    HostId = uid,
                    Title = cols[0].Trim(),
                    PropertyType = cols[1].Trim(),
                    Address = cols[2].Trim(),
                    City = cols[3].Trim(),
                    State = cols[4].Trim(),
                    Postcode = cols[5].Trim(),
                    BaseNightlyRate = Math.Max(20m, rate),
                    CustomWeekendRate = Math.Max(20m, Math.Round(rate * 1.15m, 2)),
                    CleaningFee = 50m,
                    MaxGuests = Math.Clamp(maxGuests, 1, 30),
                    Description = cols.Length > 8 ? cols[8].Trim() : "Imported StayNest listing.",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _db.Properties.Add(prop);
                await _db.SaveChangesAsync();

                _db.PropertyPhotos.Add(new PropertyPhoto
                {
                    PropertyId = prop.Id,
                    FileName = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=1000&q=80",
                    IsPrimary = true,
                    DisplayOrder = 1
                });
                imported++;
            }

            await _db.SaveChangesAsync();
            _helper.LogAudit(_db, uid, _helper.GetCurrentUserEmail(), "Property", "BatchCsvImport",
                $"Batch-imported {imported} listings via CSV.", "Info");

            TempData["Success"] = $"Successfully imported {imported} listing(s) from CSV.";
            return RedirectToAction(nameof(MyProperties));
        }

        // =====================================================================
        // Guest Wishlist (AJAX Toggle & Saved Folders)
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Wishlist()
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var items = await _db.Wishlists
                .Include(w => w.Property).ThenInclude(p => p.Photos)
                .Where(w => w.GuestId == uid)
                .OrderByDescending(w => w.AddedAt)
                .ToListAsync();

            return View(items);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ToggleWishlist(int propertyId, string? folderName)
        {
            int uid = _helper.GetCurrentUserId()!.Value;
            var existing = await _db.Wishlists.FirstOrDefaultAsync(w => w.GuestId == uid && w.PropertyId == propertyId);
            bool saved;

            if (existing != null)
            {
                _db.Wishlists.Remove(existing);
                saved = false;
            }
            else
            {
                _db.Wishlists.Add(new Wishlist
                {
                    GuestId = uid,
                    PropertyId = propertyId,
                    FolderName = string.IsNullOrWhiteSpace(folderName) ? "Saved Stays" : folderName.Trim(),
                    AddedAt = DateTime.Now
                });
                saved = true;
            }

            await _db.SaveChangesAsync();
            return Json(new { saved });
        }
    }
}
