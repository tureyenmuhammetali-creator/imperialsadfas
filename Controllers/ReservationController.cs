using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ImperialVip.Data;
using ImperialVip.Models;
using ImperialVip.Services;
using ImperialVip.Infrastructure;

namespace ImperialVip.Controllers
{
    public class ReservationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReservationController> _logger;

        public ReservationController(ApplicationDbContext context, IMemoryCache cache, IEmailService emailService, ILogger<ReservationController> logger)
        {
            _context = context;
            _cache = cache;
            _emailService = emailService;
            _logger = logger;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(int? vehicleId, string? pickupLocation, string? dropoffLocation, int? regionId, int? passengers)
        {
            const string vehiclesCacheKey = "vehicles_all_active";
            const string regionsCacheKey = "regions_all_active";
            
            var vehicles = await _cache.GetOrCreateAsync(vehiclesCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                entry.Size = 1;
                return await _context.Vehicles
                    .AsNoTracking()
                    .Where(v => v.IsActive == 1)
                    .OrderBy(v => v.SortOrder)
                    .ToListAsync();
            });
            
            var regions = await _cache.GetOrCreateAsync(regionsCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                entry.Size = 1;
                return await _context.Regions
                    .AsNoTracking()
                    .Where(r => r.IsActive == 1)
                    .OrderBy(r => r.Name)
                    .ToListAsync();
            });
            
            ViewBag.Vehicles = vehicles;
            ViewBag.Regions = regions;
            ViewBag.SelectedVehicleId = vehicleId;
            ViewBag.PickupLocation = pickupLocation;
            ViewBag.DropoffLocation = dropoffLocation;
            ViewBag.RegionId = regionId;
            // Ana sayfadaki "Yolcu Sayısı" alanından gelen değer (1-20 arası, yoksa 1)
            var passengerCount = passengers.HasValue && passengers.Value >= 1 && passengers.Value <= 20 ? passengers.Value : 1;
            ViewBag.PassengerCount = passengerCount;
            
            if (vehicleId.HasValue)
            {
                var selectedVehicle = vehicles.FirstOrDefault(v => v.Id == vehicleId.Value);
                ViewBag.SelectedVehicle = selectedVehicle;
            }
            
            if (regionId.HasValue)
            {
                var cacheKey = $"region_{regionId.Value}";
                var region = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                    entry.Size = 1;
                    return await _context.Regions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == regionId.Value);
                });
                ViewBag.SelectedRegion = region;
            }
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation model)
        {
            _logger.LogInformation("Rezervasyon Create POST çağrıldı. ModelState.IsValid={IsValid}", ModelState.IsValid);
            EmailLogHelper.Write($"[CREATE] Rezervasyon POST çağrıldı. ModelState.IsValid={ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogWarning("ModelState hatası: {Key} = {Error}", state.Key, error.ErrorMessage);
                        EmailLogHelper.Write($"[VALIDATION HATA] {state.Key}: {error.ErrorMessage}");
                    }
                }
            }

            // Zorunlu olmayan alanları kontrol etmeden devam et
            ModelState.Remove("Vehicle");
            ModelState.Remove("Region");
            ModelState.Remove("PickupLocationDetail");
            ModelState.Remove("DropoffLocationDetail");
            ModelState.Remove("FlightNumber");
            ModelState.Remove("AirlineCompany");
            ModelState.Remove("Notes");
            ModelState.Remove("AdminNotes");
            ModelState.Remove("CustomerEmail");
            ModelState.Remove("AdditionalPassengerNames");
            ModelState.Remove("HotelName");
            ModelState.Remove("IsReturnTransfer");
            ModelState.Remove("ReturnTransferDate");
            ModelState.Remove("ReturnTransferTime");
            ModelState.Remove("ReturnFlightNumber");
            ModelState.Remove("NumberOfAdults");
            ModelState.Remove("NumberOfChildren");
            ModelState.Remove("ChildSeatCount");
            
            if (!model.VehicleId.HasValue || model.VehicleId.Value < 1)
                ModelState.AddModelError("VehicleId", "Araç seçimi zorunludur.");
            if (ModelState.IsValid)
            {
                _logger.LogInformation("Rezervasyon kaydı oluşturuluyor: {Name}, {Phone}, {Email}", model.CustomerName, model.CustomerPhone, model.CustomerEmail ?? "(boş)");
                EmailLogHelper.Write($"[CREATE] Kayıt oluşturuluyor: {model.CustomerName}, {model.CustomerPhone}, Email={model.CustomerEmail ?? "(boş)"}");

                // Tarih ve saat kontrolü - geçmiş bir zamana rezervasyon yapılamaz
                if (!string.IsNullOrEmpty(model.TransferTime))
                {
                    var timeParts = model.TransferTime.Split(':');
                    if (timeParts.Length >= 2 && int.TryParse(timeParts[0], out int hour) && int.TryParse(timeParts[1], out int minute))
                    {
                        var transferDateTime = (model.TransferDate ?? DateTime.Today).Date.AddHours(hour).AddMinutes(minute);
                        var minAllowedTime = DateTime.Now.AddHours(1); // En az 1 saat sonrası
                        
                        if (transferDateTime < minAllowedTime)
                        {
                            ModelState.AddModelError("TransferTime", "Rezervasyon en az 1 saat sonrası için yapılabilir.");
                            var vehicleList = await _context.Vehicles.Where(v => v.IsActive == 1).OrderBy(v => v.SortOrder).ToListAsync();
                            var regionList = await _context.Regions.Where(r => r.IsActive == 1).OrderBy(r => r.Name).ToListAsync();
                            ViewBag.Vehicles = vehicleList;
                            ViewBag.Regions = regionList;
                            return View("Index", model);
                        }
                    }
                }
                
                model.CreatedAt = DateTime.UtcNow;
                model.Status = ReservationStatus.Beklemede;
                
                // Null alanları boş string olarak ayarla (veritabanı NOT NULL constraint için)
                model.PickupLocationDetail ??= "";
                model.DropoffLocationDetail ??= "";
                model.FlightNumber ??= "";
                model.Notes ??= "";
                model.AdminNotes ??= "";
                model.CustomerEmail ??= "";
                model.PassengerCount = (model.NumberOfAdults ?? 1) + (model.NumberOfChildren ?? 0);
                if ((model.PassengerCount ?? 0) < 1) model.PassengerCount = 1;
                // Veritabanında NOT NULL olan alanlar - NULL gönderilirse varsayılan değer
                model.DistanceKm = model.DistanceKm ?? 0;
                model.EstimatedPrice = model.EstimatedPrice ?? 0;

                _context.Reservations.Add(model);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Rezervasyon veritabanına kaydedildi. Id={Id}", model.Id);
                EmailLogHelper.Write($"[CREATE] Rezervasyon kaydedildi. Id={model.Id}");

                // Araç ve bölge bilgisini yükle (mail için)
                if (model.VehicleId.HasValue)
                    model.Vehicle = await _context.Vehicles.FindAsync(model.VehicleId.Value);
                if (model.RegionId.HasValue)
                    model.Region = await _context.Regions.FindAsync(model.RegionId.Value);

                // Email gönder - Müşteriye onay
                try
                {
                    var customerEmailSent = await _emailService.SendReservationConfirmationAsync(model);
                    if (customerEmailSent)
                    {
                        _logger.LogInformation("Rezervasyon #{Id}: Müşteriye onay maili gönderildi: {Email}", model.Id, model.CustomerEmail);
                        EmailLogHelper.Write($"[MAIL] Rezervasyon #{model.Id}: Müşteriye onay maili GÖNDERİLDİ -> {model.CustomerEmail}");
                    }
                    else
                    {
                        _logger.LogWarning("Rezervasyon #{Id}: Müşteriye onay maili GÖNDERİLMEDİ (e-posta boş veya SMTP hatası).", model.Id);
                        EmailLogHelper.Write($"[MAIL] Rezervasyon #{model.Id}: Müşteriye mail GÖNDERİLMEDİ (e-posta boş veya SMTP hatası)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Rezervasyon #{Id}: Müşteriye mail gönderilirken HATA: {Message}", model.Id, ex.Message);
                    EmailLogHelper.Write($"[MAIL HATA] Rezervasyon #{model.Id}: Müşteriye mail - {ex.Message}");
                }

                // Email gönder - Admin'e bildirim (info@ + imperialtransfervip@gmail.com)
                try
                {
                    var adminEmailSent = await _emailService.SendReservationNotificationToAdminAsync(model);
                    if (adminEmailSent)
                    {
                        _logger.LogInformation("Rezervasyon #{Id}: Admin adreslerine bildirim maili gönderildi.", model.Id);
                        EmailLogHelper.Write($"[MAIL] Rezervasyon #{model.Id}: Admin adreslerine bildirim maili GÖNDERİLDİ.");
                    }
                    else
                    {
                        _logger.LogWarning("Rezervasyon #{Id}: Admin adreslerine bildirim maili GÖNDERİLMEDİ.", model.Id);
                        EmailLogHelper.Write($"[MAIL] Rezervasyon #{model.Id}: Admin adreslerine mail GÖNDERİLMEDİ.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Rezervasyon #{Id}: Admin mail gönderilirken HATA: {Message}", model.Id, ex.Message);
                    EmailLogHelper.Write($"[MAIL HATA] Rezervasyon #{model.Id}: Admin mail - {ex.Message}");
                }

                TempData["Success"] = "Rezervasyonunuz başarıyla alındı. En kısa sürede size dönüş yapacağız.";
                var lang = HttpContext.GetRouteValue("lang") ?? "tr";
                return RedirectToAction(nameof(Success), new { id = model.Id, lang });
            }

            var vehicles = await _context.Vehicles
                .Where(v => v.IsActive == 1)
                .OrderBy(v => v.SortOrder)
                .ToListAsync();
            var regions = await _context.Regions
                .Where(r => r.IsActive == 1)
                .OrderBy(r => r.Name)
                .ToListAsync();
            ViewBag.Vehicles = vehicles;
            ViewBag.Regions = regions;
            return View("Index", model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Success(int id)
        {
            var reservation = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // API: Mesafe ve Fiyat Hesaplama (Cache ile optimize edilmiş)
        [HttpPost]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> CalculatePrice([FromBody] PriceCalculationRequest request)
        {
            if (request.VehicleId == null || request.DistanceKm <= 0)
            {
                return Json(new { success = false, message = "Geçersiz parametreler" });
            }

            var cacheKey = $"vehicle_{request.VehicleId}";
            var vehicle = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                entry.Size = 1;
                return await _context.Vehicles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == request.VehicleId);
            });
            
            if (vehicle == null)
            {
                return Json(new { success = false, message = "Araç bulunamadı" });
            }

            // Aracın kullanım ücreti = MinimumPrice (km başına fiyat kaldırıldı)
            var finalPrice = (double)vehicle.MinimumPrice;

            return Json(new 
            { 
                success = true, 
                price = finalPrice,
                formattedPrice = finalPrice.ToString("N2") + " ₺",
                distanceKm = request.DistanceKm,
                vehicleName = vehicle.Name
            });
        }
    }

    public class PriceCalculationRequest
    {
        public int? VehicleId { get; set; }
        public double DistanceKm { get; set; }
    }
}
