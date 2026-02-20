using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.OutputCaching;
using ImperialVip.Data;
using ImperialVip.Models;
using ImperialVip.Services;
using ImperialVip.Infrastructure;

namespace ImperialVip.Controllers
{
    [Authorize(Roles = "Admin")]
    [ServiceFilter(typeof(AdminExceptionFilter))]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly DataMigrationService _migrationService;
        private readonly ICurrencyRateService _currencyRateService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminController> _logger;
        private readonly IOutputCacheStore _outputCache;
        private readonly IEmailService _emailService;
        private readonly IReservationPdfService _pdfService;
        private readonly IWhatsAppService _whatsAppService;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment environment, DataMigrationService migrationService, ICurrencyRateService currencyRateService, IMemoryCache cache, ILogger<AdminController> logger, IOutputCacheStore outputCache, IEmailService emailService, IReservationPdfService pdfService, IWhatsAppService whatsAppService)
        {
            _context = context;
            _environment = environment;
            _migrationService = migrationService;
            _currencyRateService = currencyRateService;
            _cache = cache;
            _logger = logger;
            _outputCache = outputCache;
            _emailService = emailService;
            _pdfService = pdfService;
            _whatsAppService = whatsAppService;
        }

        /// <summary>Bölge listesi/detay önbelleğini temizler. Admin bölge ekleyince/silince/düzenleyince ana sayfa ve rezervasyon güncel veri gösterir.</summary>
        private async Task InvalidateRegionCaches(int? regionId = null)
        {
            _cache.Remove("homepage_regions");
            _cache.Remove("homepage_all_regions_form");
            _cache.Remove("regions_all");
            _cache.Remove("regions_all_alphabetic");
            _cache.Remove("regions_all_active");
            if (regionId.HasValue)
            {
                _cache.Remove($"region_detail_{regionId.Value}");
                _cache.Remove($"region_{regionId.Value}");
            }

            try
            {
                // OutputCache tarafındaki HTML cache'lerini de temizle (AllRegions, RegionDetail, varsa ana sayfa)
                await _outputCache.EvictByTagAsync("regions", default);
                await _outputCache.EvictByTagAsync("homepage", default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Region output cache temizlenirken hata oluştu.");
            }
        }

        /// <summary>Araç listesi önbelleğini temizler. Admin araç ekleyince/silince/düzenleyince ana sayfa güncel araç görsellerini gösterir.</summary>
        private void InvalidateVehicleCaches()
        {
            _cache.Remove("homepage_vehicles");
            _cache.Remove("vehicles_all_active");
        }

        /// <summary>Slider görselleri önbelleğini temizler. Admin slider ekleyince/silince/düzenleyince ana sayfa hemen güncellenir.</summary>
        private async Task InvalidateHeroCaches()
        {
            _cache.Remove("homepage_hero");
            try
            {
                await _outputCache.EvictByTagAsync("homepage", default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hero output cache temizlenirken uyarı.");
            }
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            var stats = new
            {
                TotalReservations = await _context.Reservations.CountAsync(),
                PendingReservations = await _context.Reservations.CountAsync(r => (r.Status ?? ReservationStatus.Beklemede) == ReservationStatus.Beklemede),
                TodayReservations = await _context.Reservations.CountAsync(r => r.TransferDate.HasValue && r.TransferDate.Value.Date == DateTime.Today),
                UnreadMessages = await _context.Contacts.CountAsync(c => !c.IsRead)
            };

            ViewBag.Stats = stats;

            // Tam entity yüklemeden sadece listelenecek alanları projekte et (ordinal 26 NULL hatası önlenir)
            var recentReservations = await _context.Reservations
                .OrderByDescending(r => r.CreatedAt ?? DateTime.MinValue)
                .Take(10)
                .Select(r => new AdminReservationSummary
                {
                    Id = r.Id,
                    CustomerName = r.CustomerName,
                    CustomerPhone = r.CustomerPhone ?? "",
                    TransferDate = r.TransferDate,
                    TransferTime = r.TransferTime ?? "",
                    PickupLocation = r.PickupLocation,
                    DropoffLocation = r.DropoffLocation,
                    Status = r.Status ?? ReservationStatus.Beklemede
                })
                .ToListAsync();

            return View(recentReservations);
        }

        #region Rezervasyonlar

        public async Task<IActionResult> Reservations(ReservationStatus? status = null)
        {
            // Eski kayıtlardaki NULL sütunlar nedeniyle tam Reservation entity'si
            // materialize edilirken oluşan "The data is NULL at ordinal" hatasını
            // engellemek için sadece listede ihtiyaç duyulan alanları projekte ediyoruz.
            var query = _context.Reservations
                .Include(r => r.Vehicle)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => (r.Status ?? ReservationStatus.Beklemede) == status.Value);
            }

            var reservations = await query
                .OrderByDescending(r => r.CreatedAt ?? DateTime.MinValue)
                .Select(r => new AdminReservationListItem
                {
                    Id = r.Id,
                    CustomerName = r.CustomerName,
                    CustomerPhone = r.CustomerPhone ?? string.Empty,
                    TransferDate = r.TransferDate,
                    TransferTime = r.TransferTime ?? string.Empty,
                    PickupLocation = r.PickupLocation,
                    DropoffLocation = r.DropoffLocation,
                    VehicleName = r.Vehicle != null ? r.Vehicle.Name : null,
                    Status = r.Status ?? ReservationStatus.Beklemede,
                    CreatedAt = r.CreatedAt,
                    EstimatedPrice = r.EstimatedPrice
                })
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            return View(reservations);
        }

        public async Task<IActionResult> CreateReservation()
        {
            ViewBag.Vehicles = await _context.Vehicles.Where(v => v.IsActive == 1).OrderBy(v => v.SortOrder).ToListAsync();
            ViewBag.Regions = await _context.Regions.Where(r => r.IsActive == 1).OrderBy(r => r.Name).ToListAsync();
            return View(new Reservation());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(Reservation model)
        {
            ModelState.Remove("Vehicle");
            ModelState.Remove("Region");
            ModelState.Remove("PickupLocationDetail");
            ModelState.Remove("DropoffLocationDetail");
            ModelState.Remove("ChildNames");
            ModelState.Remove("Language");

            if (string.IsNullOrWhiteSpace(model.CustomerName))
                ModelState.AddModelError("CustomerName", "Ad Soyad zorunludur.");
            if (string.IsNullOrWhiteSpace(model.CustomerPhone))
                ModelState.AddModelError("CustomerPhone", "Telefon numarası zorunludur.");
            if (string.IsNullOrWhiteSpace(model.PickupLocation))
                ModelState.AddModelError("PickupLocation", "Alınacak nokta zorunludur.");
            if (string.IsNullOrWhiteSpace(model.DropoffLocation))
                ModelState.AddModelError("DropoffLocation", "Bırakılacak nokta zorunludur.");
            if (!model.VehicleId.HasValue || model.VehicleId.Value < 1)
                ModelState.AddModelError("VehicleId", "Lütfen bir araç seçiniz.");
            if (!model.RegionId.HasValue || model.RegionId.Value < 1)
                ModelState.AddModelError("RegionId", "Lütfen bir bölge seçiniz.");

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                model.Status ??= ReservationStatus.Beklemede;
                model.PickupLocationDetail ??= "";
                model.DropoffLocationDetail ??= "";
                model.FlightNumber ??= "";
                model.Notes ??= "";
                model.AdminNotes ??= "";
                model.CustomerEmail ??= "";
                model.PassengerCount = (model.NumberOfAdults ?? 1) + (model.NumberOfChildren ?? 0);
                if ((model.PassengerCount ?? 0) < 1) model.PassengerCount = 1;
                model.DistanceKm ??= 0;
                model.EstimatedPrice ??= 0;

                _context.Reservations.Add(model);
                await _context.SaveChangesAsync();

                if (model.VehicleId.HasValue)
                    model.Vehicle = await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == model.VehicleId.Value);
                if (model.RegionId.HasValue)
                    model.Region = await _context.Regions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == model.RegionId.Value);

                try
                {
                    if (!string.IsNullOrEmpty(model.CustomerEmail))
                    {
                        await _emailService.SendReservationConfirmationAsync(model);
                        await _emailService.SendReservationNotificationToAdminAsync(model);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Admin rezervasyon mail gönderilirken hata: {Message}", ex.Message);
                }

                try
                {
                    await _whatsAppService.SendReservationPdfAsync(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Admin rezervasyon WhatsApp gönderiminde hata: {Message}", ex.Message);
                }

                TempData["Success"] = "Rezervasyon başarıyla kaydedildi. (Site dışı manuel kayıt)";
                return RedirectToAction(nameof(ReservationDetails), new { id = model.Id });
            }

            ViewBag.Vehicles = await _context.Vehicles.Where(v => v.IsActive == 1).OrderBy(v => v.SortOrder).ToListAsync();
            ViewBag.Regions = await _context.Regions.Where(r => r.IsActive == 1).OrderBy(r => r.Name).ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> ReservationDetails(int id)
        {
            // Önce rezervasyonu Include olmadan yükle (join'deki NULL ordinal hatası riskini azaltır)
            var reservation = await _context.Reservations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                return NotFound();
            }

            // Aracı ayrı sorguyla yükle (veritabanında NULL alan olsa bile güvenli)
            if (reservation.VehicleId.HasValue)
            {
                reservation.Vehicle = await _context.Vehicles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == reservation.VehicleId.Value);
            }

            return View(reservation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReservationStatus(int id, ReservationStatus status, string? adminNotes)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            reservation.Status = status;
            reservation.UpdatedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(adminNotes))
            {
                reservation.AdminNotes = adminNotes;
            }

            if (status == ReservationStatus.Onaylandi)
            {
                reservation.ConfirmedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            if (status == ReservationStatus.Onaylandi && !string.IsNullOrEmpty(reservation.CustomerEmail))
            {
                try
                {
                    if (reservation.VehicleId.HasValue)
                        reservation.Vehicle = await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == reservation.VehicleId.Value);
                    await _emailService.SendReservationConfirmationAsync(reservation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Onay maili gönderilirken hata: {Message}", ex.Message);
                }
            }

            TempData["Success"] = "Rezervasyon durumu güncellendi.";

            return RedirectToAction(nameof(ReservationDetails), new { id });
        }

        public async Task<IActionResult> DownloadPdf(int id, string lang = "tr")
        {
            var reservation = await _context.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (reservation == null) return NotFound();
            if (reservation.VehicleId.HasValue)
                reservation.Vehicle = await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == reservation.VehicleId.Value);

            var pdfBytes = _pdfService.GenerateReservationPdf(reservation, lang);
            var fileName = $"Rezervasyon_{reservation.Id}_{lang}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReservation(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Rezervasyon silindi.";

            return RedirectToAction(nameof(Reservations));
        }

        #endregion

        #region Araçlar

        public async Task<IActionResult> Vehicles()
        {
            var vehicles = await _context.Vehicles
                .OrderBy(v => v.SortOrder)
                .ToListAsync();

            return View(vehicles);
        }

        public IActionResult CreateVehicle()
        {
            return View(new Vehicle());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
        public async Task<IActionResult> CreateVehicle(IFormCollection form, IFormFile? mainImageFile, List<IFormFile>? albumFiles)
        {
            var name = form["Name"].ToString().Trim();
            if (string.IsNullOrEmpty(name))
            {
                var modelForView = BuildVehicleFromForm(form);
                ModelState.AddModelError("Name", "Araç Adı zorunludur.");
                return View(modelForView);
            }

            var vehicle = new Vehicle
            {
                Name = name,
                Type = form["Type"].ToString().Trim(),
                Brand = form["Brand"].ToString().Trim(),
                Model = form["Model"].ToString().Trim(),
                Description = form["Description"].ToString() ?? "",
                Features = form["Features"].ToString() ?? "",
                ImageUrl = "",
                Currency = string.IsNullOrEmpty(form["Currency"].ToString().Trim()) ? "EUR" : form["Currency"].ToString().Trim(),
                IsActive = FormIsActiveChecked(form) ? 1 : 0,
                CreatedAt = DateTime.UtcNow,
                Images = new List<VehicleImage>()
            };

            if (int.TryParse(form["PassengerCapacity"].ToString(), out var pcap) && pcap > 0) vehicle.PassengerCapacity = pcap;
            if (int.TryParse(form["LuggageCapacity"].ToString(), out var lcap) && lcap >= 0) vehicle.LuggageCapacity = lcap;
            else vehicle.LuggageCapacity = 0;
            if (int.TryParse(form["SortOrder"].ToString(), out var so)) vehicle.SortOrder = so;

            ParseAndSetDecimal(form["MinimumPrice"].ToString(), v => vehicle.MinimumPrice = v, () => vehicle.MinimumPrice);
            ParseAndSetDecimal(form["MinimumPriceUsd"].ToString(), v => vehicle.MinimumPriceUsd = v, () => vehicle.MinimumPriceUsd);
            ParseAndSetDecimal(form["MinimumPriceTry"].ToString(), v => vehicle.MinimumPriceTry = v, () => vehicle.MinimumPriceTry);

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "aracdetay");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (mainImageFile != null && mainImageFile.Length > 0)
            {
                var ext = Path.GetExtension(mainImageFile.FileName).ToLowerInvariant();
                var fileName = $"vehicle_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await mainImageFile.CopyToAsync(stream);
                vehicle.ImageUrl = $"/images/aracdetay/{fileName}";
            }

            if (albumFiles != null)
            {
                int sortIdx = 0;
                foreach (var file in albumFiles.Where(f => f.Length > 0))
                {
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var fileName = $"vehicle_{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await file.CopyToAsync(stream);
                    vehicle.Images.Add(new VehicleImage { ImageUrl = $"/images/aracdetay/{fileName}", SortOrder = sortIdx++ });
                }
            }

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();
            InvalidateVehicleCaches();
            TempData["Success"] = "Araç başarıyla eklendi.";
            return RedirectToAction(nameof(Vehicles));
        }

        private static Vehicle BuildVehicleFromForm(IFormCollection form)
        {
            var cur = form["Currency"].ToString().Trim();
            var v = new Vehicle
            {
                Name = form["Name"].ToString().Trim(),
                Type = form["Type"].ToString().Trim(),
                Brand = form["Brand"].ToString().Trim(),
                Model = form["Model"].ToString().Trim(),
                Description = form["Description"].ToString() ?? "",
                Features = form["Features"].ToString() ?? "",
                ImageUrl = form["ImageUrl"].ToString()?.Trim() ?? "",
                Currency = string.IsNullOrEmpty(cur) ? "EUR" : cur,
                IsActive = FormIsActiveChecked(form) ? 1 : 0
            };
            if (int.TryParse(form["PassengerCapacity"].ToString(), out var pcap)) v.PassengerCapacity = pcap;
            if (int.TryParse(form["LuggageCapacity"].ToString(), out var lcap)) v.LuggageCapacity = lcap;
            else v.LuggageCapacity = 0;
            if (int.TryParse(form["SortOrder"].ToString(), out var so)) v.SortOrder = so;
            ParseAndSetDecimal(form["MinimumPrice"].ToString(), x => v.MinimumPrice = x, () => v.MinimumPrice);
            ParseAndSetDecimal(form["MinimumPriceUsd"].ToString(), x => v.MinimumPriceUsd = x, () => v.MinimumPriceUsd);
            ParseAndSetDecimal(form["MinimumPriceTry"].ToString(), x => v.MinimumPriceTry = x, () => v.MinimumPriceTry);
            return v;
        }

        public async Task<IActionResult> EditVehicle(int id)
        {
            var vehicle = await _context.Vehicles.Include(v => v.Images).FirstOrDefaultAsync(v => v.Id == id);
            if (vehicle == null)
            {
                return NotFound();
            }
            return View(vehicle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
        public async Task<IActionResult> EditVehicle(int id, IFormCollection form, IFormFile? mainImageFile, List<IFormFile>? albumFiles)
        {
            var formId = form["Id"].ToString();
            if (id <= 0 && !string.IsNullOrEmpty(formId) && int.TryParse(formId, out var parsedId))
                id = parsedId;
            if (id <= 0)
            {
                TempData["Error"] = "Araç bulunamadı.";
                return RedirectToAction(nameof(Vehicles));
            }
            try
            {
                var existing = await _context.Vehicles.Include(v => v.Images).AsTracking().FirstOrDefaultAsync(v => v.Id == id);
                if (existing == null)
                {
                    TempData["Error"] = "Araç bulunamadı.";
                    return RedirectToAction(nameof(Vehicles));
                }
                // Tüm alanları doğrudan formdan oku (model binding'e güvenmiyoruz)
                var name = form["Name"].ToString().Trim();
                if (!string.IsNullOrEmpty(name)) existing.Name = name;
                var type = form["Type"].ToString().Trim();
                if (!string.IsNullOrEmpty(type)) existing.Type = type;
                var brand = form["Brand"].ToString().Trim();
                if (!string.IsNullOrEmpty(brand)) existing.Brand = brand;
                var modelVal = form["Model"].ToString().Trim();
                if (!string.IsNullOrEmpty(modelVal)) existing.Model = modelVal;
                if (int.TryParse(form["PassengerCapacity"].ToString(), out var pcap) && pcap > 0) existing.PassengerCapacity = pcap;
                if (int.TryParse(form["LuggageCapacity"].ToString(), out var lcap) && lcap >= 0) existing.LuggageCapacity = lcap;
                existing.Description = form["Description"].ToString() ?? existing.Description;
                existing.Features = form["Features"].ToString() ?? existing.Features;
                existing.ImageUrl = form["ImageUrl"].ToString()?.Trim() ?? existing.ImageUrl;
                ParseAndSetDecimal(form["MinimumPrice"].ToString(), v => existing.MinimumPrice = v, () => existing.MinimumPrice);
                ParseAndSetDecimal(form["MinimumPriceUsd"].ToString(), v => existing.MinimumPriceUsd = v, () => existing.MinimumPriceUsd);
                ParseAndSetDecimal(form["MinimumPriceTry"].ToString(), v => existing.MinimumPriceTry = v, () => existing.MinimumPriceTry);
                if (form.TryGetValue("PricePerKm", out var ppkVal) && decimal.TryParse(ppkVal.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var ppk))
                    existing.PricePerKm = ppk;
                var currency = form["Currency"].ToString().Trim();
                if (!string.IsNullOrEmpty(currency)) existing.Currency = currency;
                existing.IsActive = FormIsActiveChecked(form) ? 1 : 0;
                if (int.TryParse(form["SortOrder"].ToString(), out var so)) existing.SortOrder = so;
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "aracdetay");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                if (mainImageFile != null && mainImageFile.Length > 0)
                {
                    var ext = Path.GetExtension(mainImageFile.FileName).ToLowerInvariant();
                    var fileName = $"vehicle_{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await mainImageFile.CopyToAsync(stream);
                    existing.ImageUrl = $"/images/aracdetay/{fileName}";
                }

                if (albumFiles != null && albumFiles.Any(f => f.Length > 0))
                {
                    existing.Images.Clear();
                    int sortIdx = 0;
                    foreach (var file in albumFiles.Where(f => f.Length > 0))
                    {
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var fileName = $"vehicle_{Guid.NewGuid():N}{ext}";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                            await file.CopyToAsync(stream);
                        existing.Images.Add(new VehicleImage { VehicleId = id, ImageUrl = $"/images/aracdetay/{fileName}", SortOrder = sortIdx++ });
                    }
                }
                else
                {
                    var albumUrls = form["AlbumUrls"].ToString();
                    if (!string.IsNullOrWhiteSpace(albumUrls))
                    {
                        existing.Images.Clear();
                        var urls = albumUrls.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(u => u.Trim()).Where(u => u.Length > 0).ToList();
                        for (int i = 0; i < urls.Count; i++)
                            existing.Images.Add(new VehicleImage { VehicleId = id, ImageUrl = urls[i], SortOrder = i });
                    }
                }
                await _context.SaveChangesAsync();
                InvalidateVehicleCaches();
                TempData["Success"] = "Araç başarıyla güncellendi.";
                return RedirectToAction(nameof(Vehicles));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EditVehicle POST hatası, Id: {Id}", id);
                TempData["Error"] = "Kayıt sırasında hata: " + ex.Message;
                return RedirectToAction(nameof(EditVehicle), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            InvalidateVehicleCaches();
            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Araç silindi.";

            return RedirectToAction(nameof(Vehicles));
        }

        /// <summary>
        /// Formda IsActive (hidden 0 + checkbox 1) gönderildiğinde checkbox işaretliyse değer "1" gelir; form["IsActive"] bazen sadece ilk değeri ("0") döndürdüğü için tüm değerlere bakıyoruz.
        /// </summary>
        private static bool FormIsActiveChecked(IFormCollection form)
        {
            var values = form["IsActive"];
            foreach (var v in values)
            {
                if (v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Formdan gelen fiyat metnini parse eder; başarılıysa setter ile yazar, değilse mevcut değer korunur (0'a düşme hatası önlenir).
        /// </summary>
        private static void ParseAndSetDecimal(string formValue, Action<decimal> setter, Func<decimal> getCurrent)
        {
            if (string.IsNullOrWhiteSpace(formValue)) return;
            var normalized = formValue.Trim().Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                setter(value);
        }

        #endregion

        #region Mesajlar

        public async Task<IActionResult> Messages()
        {
            var messages = await _context.Contacts
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(messages);
        }

        public async Task<IActionResult> MessageDetails(int id)
        {
            var message = await _context.Contacts.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            if (!message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var message = await _context.Contacts.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            _context.Contacts.Remove(message);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Mesaj silindi.";

            return RedirectToAction(nameof(Messages));
        }

        #endregion

        #region Site Ayarları

        public async Task<IActionResult> Settings()
        {
            var settings = await _context.SiteSettings.ToListAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(Dictionary<string, string> settings)
        {
            // Form alanlarını Request.Form'dan oku: name="setting_Key" formatı
            var settingsDict = new Dictionary<string, string>();
            const string prefix = "setting_";
            if (Request.Form != null)
            {
                foreach (var formKey in Request.Form.Keys)
                {
                    if (formKey != null && formKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var settingKey = formKey.Substring(prefix.Length);
                        settingsDict[settingKey] = Request.Form[formKey].ToString();
                    }
                }
            }
            if (settingsDict.Count == 0 && settings != null)
                settingsDict = new Dictionary<string, string>(settings);

            if (settingsDict.Count == 0)
            {
                TempData["Error"] = "Form verisi alınamadı. Lütfen tekrar deneyin.";
                return RedirectToAction(nameof(Settings));
            }

            var heroDescriptions = new Dictionary<string, string>
            {
                ["hero_badge"] = "Ana Sayfa Üst Rozet Yazısı",
                ["hero_title"] = "Ana Sayfa Başlık (1. Satır)",
                ["hero_subtitle"] = "Ana Sayfa Başlık (2. Satır)",
                ["hero_description"] = "Ana Sayfa Açıklama"
            };
            
            var contactDescriptions = new Dictionary<string, string>
            {
                ["CompanyName"] = "Firma Adı",
                ["Phone"] = "Telefon",
                ["Email"] = "E-posta",
                ["Address"] = "Adres",
                ["WhatsApp"] = "WhatsApp Numarası",
                ["Instagram"] = "Instagram Kullanıcı Adı",
                ["Facebook"] = "Facebook Sayfa Adı"
            };

            foreach (var item in settingsDict)
            {
                var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == item.Key);
                if (setting != null)
                {
                    setting.Value = item.Value;
                    setting.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Yeni ayar oluştur
                    string description = "";
                    if (item.Key.StartsWith("hero_"))
                    {
                        var baseKey = item.Key.Split('_').Take(2).Aggregate((a, b) => $"{a}_{b}");
                        if (heroDescriptions.ContainsKey(baseKey))
                        {
                            var lang = item.Key.Split('_').Last();
                            var langNames = new Dictionary<string, string> 
                            { 
                                ["tr"] = "Türkçe", 
                                ["de"] = "Deutsch", 
                                ["ru"] = "Русский", 
                                ["en"] = "English" 
                            };
                            description = $"{heroDescriptions[baseKey]} ({langNames.GetValueOrDefault(lang, lang)})";
                        }
                    }
                    else if (contactDescriptions.ContainsKey(item.Key))
                    {
                        description = contactDescriptions[item.Key];
                    }
                    else
                    {
                        description = item.Key;
                    }
                    
                    setting = new SiteSettings
                    {
                        Key = item.Key,
                        Value = item.Value,
                        Description = description,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SiteSettings.Add(setting);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Ayarlar başarıyla güncellendi.";
            return RedirectToAction(nameof(Settings));
        }

        #endregion

        #region Kur Ayarları

        public IActionResult CurrencyRates()
        {
            var rates = _currencyRateService.GetRates();
            ViewBag.TRY = rates.TryGetValue("TRY", out var tr) ? tr : 38.27m;
            ViewBag.USD = rates.TryGetValue("USD", out var us) ? us : 1.05m;
            ViewBag.GBP = rates.TryGetValue("GBP", out var gb) ? gb : 0.83m;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CurrencyRates(string TRY, string USD, string GBP)
        {
            if (!TryParseRate(TRY, out var tryRate) || !TryParseRate(USD, out var usdRate) || !TryParseRate(GBP, out var gbpRate))
            {
                TempData["Error"] = "Lütfen tüm kurları geçerli sayı olarak girin (örn: 38,27 veya 38.27).";
                return RedirectToAction(nameof(CurrencyRates));
            }
            if (tryRate <= 0 || usdRate <= 0 || gbpRate <= 0)
            {
                TempData["Error"] = "Tüm kurlar 0'dan büyük olmalıdır.";
                return RedirectToAction(nameof(CurrencyRates));
            }
            await _currencyRateService.SaveRatesAsync(tryRate, usdRate, gbpRate);
            try
            {
                await _outputCache.EvictByTagAsync("homepage", default);
                await _outputCache.EvictByTagAsync("regions", default);
                await _outputCache.EvictByTagAsync("vehicles", default);
                await _outputCache.EvictByTagAsync("gallery", default);
                await _outputCache.EvictByTagAsync("static", default);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Kur kaydedildi; output cache temizlenirken uyarı."); }
            TempData["Success"] = "Kurlar güncellendi. Sitede fiyatlar yeni kura göre gösterilecek.";
            return RedirectToAction(nameof(CurrencyRates));
        }

        /// <summary>
        /// Virgül veya nokta ile yazılmış kur değerini parse eder (TR/EN uyumlu).
        /// </summary>
        private static bool TryParseRate(string? value, out decimal rate)
        {
            rate = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim().Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out rate);
        }

        #endregion

        #region Hero Slider

        public async Task<IActionResult> HeroSlides()
        {
            var slides = await _context.HeroSlides
                .OrderBy(h => h.Id)
                .ToListAsync();

            return View(slides);
        }

        public IActionResult CreateHeroSlide()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateHeroSlide(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                TempData["Error"] = "Lütfen bir görsel seçin.";
                return View();
            }

            // Dosya uzantısı kontrolü
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Sadece JPG, PNG veya WebP formatında görseller yükleyebilirsiniz.";
                return View();
            }

            // Klasör oluştur
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "hero");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Benzersiz dosya adı oluştur
            var uniqueFileName = $"slide_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Dosyayı kaydet
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Veritabanına kaydet (sıralama otomatik: Id ile)
            var slide = new HeroSlide
            {
                ImageUrl = $"/images/hero/{uniqueFileName}",
                SortOrder = 0,
                IsActive = 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.HeroSlides.Add(slide);
            await _context.SaveChangesAsync();

            await InvalidateHeroCaches();
            TempData["Success"] = "Slider görseli başarıyla eklendi.";
            return RedirectToAction(nameof(HeroSlides));
        }

        public async Task<IActionResult> EditHeroSlide(int id)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide == null)
            {
                return NotFound();
            }
            return View(slide);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHeroSlide(int id, IFormFile? imageFile, bool isActive)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide == null)
            {
                return NotFound();
            }

            // Yeni görsel yüklendiyse
            if (imageFile != null && imageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Error"] = "Sadece JPG, PNG veya WebP formatında görseller yükleyebilirsiniz.";
                    return View(slide);
                }

                // Eski görseli sil (varsa)
                if (!string.IsNullOrEmpty(slide.ImageUrl))
                {
                    var oldPath = Path.Combine(_environment.WebRootPath, slide.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                // Yeni görseli kaydet
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "hero");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"slide_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                slide.ImageUrl = $"/images/hero/{uniqueFileName}";
            }

            slide.IsActive = isActive ? 1 : 0;

            await _context.SaveChangesAsync();
            await InvalidateHeroCaches();
            TempData["Success"] = "Slider görseli başarıyla güncellendi.";
            return RedirectToAction(nameof(HeroSlides));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHeroSlide(int id)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide == null)
            {
                return NotFound();
            }

            // Görseli sil (varsa)
            if (!string.IsNullOrEmpty(slide.ImageUrl))
            {
                var filePath = Path.Combine(_environment.WebRootPath, slide.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.HeroSlides.Remove(slide);
            await _context.SaveChangesAsync();
            await InvalidateHeroCaches();
            TempData["Success"] = "Slider görseli silindi.";

            return RedirectToAction(nameof(HeroSlides));
        }

        #endregion

        #region Bölgeler

        public async Task<IActionResult> Regions()
        {
            var regions = await _context.Regions
                .OrderBy(r => r.Name)
                .ToListAsync();

            return View(regions);
        }

        public IActionResult CreateRegion()
        {
            return View(new Region { Currency = "EUR", SortOrder = 0, IsActive = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)] // 20MB - bölge görseli için
        public async Task<IActionResult> CreateRegion(Region model, IFormFile? imageFile)
        {
            ModelState.Remove("Id");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(extension))
                        {
                            ModelState.AddModelError("", "Sadece JPG, PNG veya WebP formatında görsel yükleyebilirsiniz.");
                            return View(model);
                        }

                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "regions");
                        if (!Directory.Exists(uploadsFolder))
                            Directory.CreateDirectory(uploadsFolder);

                        var uniqueFileName = $"region_{Guid.NewGuid():N}{extension}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                            await imageFile.CopyToAsync(stream);

                        model.ImageUrl = $"/images/regions/{uniqueFileName}";
                    }

                    model.CreatedAt = DateTime.UtcNow;
                    model.NameEn = model.Name ?? "";
                    model.DescriptionEn = model.Description ?? "";
                    model.StartPointEn = model.StartPoint ?? "";
                    _context.Regions.Add(model);
                    await _context.SaveChangesAsync();
                    await InvalidateRegionCaches();
                    TempData["Success"] = "Bölge başarıyla eklendi.";
                    return RedirectToAction(nameof(Regions));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bölge eklenirken hata oluştu.");
                    TempData["Error"] = "Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.";
                    return View(model);
                }
            }

            TempData["Error"] = "Lütfen zorunlu alanları doldurun ve hataları kontrol edin.";
            return View(model);
        }

        public async Task<IActionResult> EditRegion(int id)
        {
            var region = await _context.Regions.FindAsync(id);
            if (region == null)
            {
                return NotFound();
            }
            return View(region);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)] // 20MB - bölge görseli için
        public async Task<IActionResult> EditRegion(Region model, IFormFile? imageFile)
        {
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                var existingRegion = await _context.Regions
                    .AsTracking()
                    .FirstOrDefaultAsync(r => r.Id == model.Id);
                if (existingRegion == null)
                {
                    return NotFound();
                }

                if (imageFile != null && imageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("", "Sadece JPG, PNG veya WebP formatında görsel yükleyebilirsiniz.");
                        return View(model);
                    }

                    if (!string.IsNullOrEmpty(existingRegion.ImageUrl) && existingRegion.ImageUrl.Contains("/images/regions/"))
                    {
                        var oldPath = Path.Combine(_environment.WebRootPath, existingRegion.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "regions");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    var uniqueFileName = $"region_{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await imageFile.CopyToAsync(stream);

                    existingRegion.ImageUrl = $"/images/regions/{uniqueFileName}";
                }

                // Diğer alanları güncelle (tek form: aynı değer TR ve EN için kullanılır)
                existingRegion.Name = model.Name;
                existingRegion.NameEn = model.Name;
                existingRegion.Description = model.Description;
                existingRegion.DescriptionEn = model.Description;
                existingRegion.Price = model.Price;
                existingRegion.Currency = model.Currency ?? "EUR";
                existingRegion.StartPoint = model.StartPoint ?? "";
                existingRegion.StartPointEn = model.StartPoint ?? "";
                // Mesafe, tahmini süre, sıralama formda yok; mevcut değerler korunur
                // IsActiveBool üzerinden gelen değer int IsActive'e zaten set ediliyor, 
                // yine de açıkça atayalım ki net olsun.
                existingRegion.IsActive = model.IsActive;
                existingRegion.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await _context.SaveChangesAsync();
                    await InvalidateRegionCaches(model.Id);
                    TempData["Success"] = "Bölge başarıyla güncellendi.";
                    return RedirectToAction(nameof(Regions));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bölge güncellenirken hata oluştu.");
                    TempData["Error"] = "Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.";
                    return View(model);
                }
            }

            TempData["Error"] = "Lütfen zorunlu alanları doldurun ve hataları kontrol edin.";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRegion(int id)
        {
            var region = await _context.Regions.FindAsync(id);
            if (region == null)
            {
                return NotFound();
            }

            // Görseli sil (varsa ve /images/regions/ içindeyse)
            if (!string.IsNullOrEmpty(region.ImageUrl) && region.ImageUrl.Contains("/images/regions/"))
            {
                var filePath = Path.Combine(_environment.WebRootPath, region.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            await InvalidateRegionCaches(id);
            _context.Regions.Remove(region);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Bölge silindi.";

            return RedirectToAction(nameof(Regions));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRegionStatus(int id)
        {
            var region = await _context.Regions.FindAsync(id);
            if (region == null)
            {
                return NotFound();
            }

            region.IsActive = region.IsActive == 1 ? 0 : 1;
            region.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await InvalidateRegionCaches(id);

            TempData["Success"] = region.IsActive == 1 ? "Bölge aktif edildi." : "Bölge pasif edildi.";
            return RedirectToAction(nameof(Regions));
        }

        #endregion

        #region Veri Aktarımı

        public IActionResult DataMigration()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunMigration()
        {
            var result = await _migrationService.MigrateAllDataAsync();
            
            if (result.Success)
            {
                TempData["Success"] = $"Veri aktarımı başarılı! {result.VehiclesImported} araç, {result.ImagesImported} araç görseli, {result.RegionsImported} bölge, {result.GalleryImported} galeri görseli, {result.SlidersImported} slider, {result.ContactsImported} mesaj, {result.ReservationsImported} rezervasyon aktarıldı.";
            }
            else
            {
                TempData["Error"] = result.Message;
            }
            
            return RedirectToAction(nameof(DataMigration));
        }

        /// <summary>
        /// Sadece eksik (silinmiş) araçları eski veritabanından geri yükler. Diğer verilere dokunmaz.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreMissingVehicles()
        {
            var result = await _migrationService.RestoreMissingVehiclesAsync();
            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;
            return RedirectToAction(nameof(DataMigration));
        }

        #endregion

        #region Galeri

        public async Task<IActionResult> Galeri()
        {
            var images = await _context.GalleryImages.OrderBy(x => x.SortOrder).ToListAsync();
            return View(images);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GaleriEkle(IFormFile imageFile, string? title, string category = "Genel")
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                TempData["Error"] = "Lütfen bir görsel seçin.";
                return RedirectToAction(nameof(Galeri));
            }

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "galeri");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                var maxSortOrder = await _context.GalleryImages.MaxAsync(x => (int?)x.SortOrder) ?? 0;

                var galleryImage = new GalleryImage
                {
                    ImageUrl = "/images/galeri/" + fileName,
                    Title = title,
                    Category = category,
                    SortOrder = maxSortOrder + 1,
                    IsActive = 1
                };

                _context.GalleryImages.Add(galleryImage);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Görsel başarıyla eklendi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Görsel eklenirken hata oluştu: " + ex.Message;
            }

            return RedirectToAction(nameof(Galeri));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GaleriSil(int id)
        {
            var image = await _context.GalleryImages.FindAsync(id);
            if (image != null)
            {
                // Dosyayı sil
                var filePath = Path.Combine(_environment.WebRootPath, image.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                _context.GalleryImages.Remove(image);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Görsel silindi.";
            }

            return RedirectToAction(nameof(Galeri));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GaleriTopluSil(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                TempData["Error"] = "Silinecek görsel seçilmedi.";
                return RedirectToAction(nameof(Galeri));
            }

            var idList = ids.Split(',').Select(int.Parse).ToList();
            var images = await _context.GalleryImages.Where(x => idList.Contains(x.Id)).ToListAsync();
            
            int deletedCount = 0;
            foreach (var image in images)
            {
                // Dosyayı sil
                var filePath = Path.Combine(_environment.WebRootPath, image.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
                
                _context.GalleryImages.Remove(image);
                deletedCount++;
            }
            
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{deletedCount} görsel başarıyla silindi.";

            return RedirectToAction(nameof(Galeri));
        }

        #endregion
    }
}
