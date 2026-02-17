using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ImperialVip.Data;
using ImperialVip.Models;
using ImperialVip.ViewModels;
using ImperialVip.Services;
using ImperialVip.Infrastructure;

namespace ImperialVip.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HomeController> _logger;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;
        private readonly ICurrencyRateService _currencyRateService;

        public HomeController(ApplicationDbContext context, IMemoryCache cache, ILogger<HomeController> logger, IEmailService emailService, IWebHostEnvironment env, ICurrencyRateService currencyRateService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _emailService = emailService;
            _env = env;
            _currencyRateService = currencyRateService;
        }

        /// <summary>Güncel döviz kurlarını JSON döner (cache yok). Admin kur güncellemesi hemen sitede yansır.</summary>
        [HttpGet]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
        public IActionResult CurrencyRatesJson()
        {
            var rates = _currencyRateService.GetRates();
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            return Json(rates);
        }

        [OutputCache(PolicyName = "HomePage")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]
        public async Task<IActionResult> Index()
        {
            // Mevcut dili al
            var currentLang = HttpContext.GetRouteValue("lang")?.ToString() ?? "tr";
            if (!LanguageViewLocationExpander.SupportedLanguages.Contains(currentLang))
                currentLang = "tr";

            // Cache key'leri - bölgeler cache'siz (anlık güncelleme için)
            const string heroCacheKey = "homepage_hero";
            const string galleryCacheKey = "homepage_gallery";
            var settingsCacheKey = $"site_settings_{currentLang}";

            // Paralel cache lookup ve DB query (Çok daha hızlı!)
            var vehiclesTask = _cache.GetOrCreateAsync("vehicles_all_active", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                entry.Size = 1;
                var list = await _context.Vehicles
                    .AsNoTracking()
                    .Include(v => v.Images)
                    .Where(v => v.IsActive == 1)
                    .OrderBy(v => v.SortOrder)
                    .ToListAsync();
                // Görseli olmayan araçlara aracdetay'dan sırayla atayıp veritabanına yaz (kalıcı)
                var gorselsiz = list.Where(v => string.IsNullOrEmpty(v.ImageUrl) && (v.Images == null || !v.Images.Any())).ToList();
                if (gorselsiz.Any())
                {
                    var dir = Path.Combine(_env.WebRootPath, "images", "aracdetay");
                    if (Directory.Exists(dir))
                    {
                        var dosyalar = Directory.GetFiles(dir, "*.jpg")
                            .Concat(Directory.GetFiles(dir, "*.jpeg"))
                            .OrderBy(f => f)
                            .Select(f => "/images/aracdetay/" + Path.GetFileName(f))
                            .ToList();
                        if (dosyalar.Any())
                        {
                            int sira = 0;
                            foreach (var arac in gorselsiz)
                            {
                                var url = dosyalar[sira % dosyalar.Count];
                                arac.ImageUrl = url;
                                var dbArac = await _context.Vehicles.FindAsync(arac.Id);
                                if (dbArac != null)
                                {
                                    dbArac.ImageUrl = url;
                                }
                                sira++;
                            }
                            await _context.SaveChangesAsync();
                            // Cache'i kaldır ki bir sonraki istekte güncel ImageUrl'lerle liste dolsun
                            _cache.Remove("vehicles_all_active");
                            // Güncellenmiş araçları veritabanından tekrar okuyup döndür
                            list = await _context.Vehicles
                                .AsNoTracking()
                                .Include(v => v.Images)
                                .Where(v => v.IsActive == 1)
                                .OrderBy(v => v.SortOrder)
                                .ToListAsync();
                        }
                    }
                }
                return list;
            });

            var heroSlidesTask = _cache.GetOrCreateAsync(heroCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                entry.Size = 1;
                return await _context.HeroSlides
                    .AsNoTracking()
                    .Where(h => h.IsActive == 1)
                    .OrderBy(h => h.Id)
                    .ToListAsync();
            });

            var galleryImagesTask = _cache.GetOrCreateAsync(galleryCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                entry.Size = 1;
                return await _context.GalleryImages
                    .AsNoTracking()
                    .Where(g => g.IsActive == 1)
                    .OrderBy(g => g.SortOrder)
                    .Take(8)
                    .ToListAsync();
            });

            // Bölgeler her istekte DB'den (anlık güncelleme; admin bölge ekleyince hemen görünsün)
            var regionsTask = _context.Regions
                .AsNoTracking()
                .Where(r => r.IsActive == 1)
                .OrderBy(r => r.Name)
                .Take(6)
                .ToListAsync();
            var allRegionsForFormTask = _context.Regions
                .AsNoTracking()
                .Where(r => r.IsActive == 1)
                .OrderBy(r => r.Name)
                .ToListAsync();

            var settingsTask = _cache.GetOrCreateAsync(settingsCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                entry.Size = 1;
                var allSettings = await _context.SiteSettings.AsNoTracking().ToListAsync();
                
                // Dil bazlı ayarları çek, yoksa genel ayarları kullan
                var languageSpecificSettings = new Dictionary<string, string>();
                var generalSettings = new Dictionary<string, string>();
                
                foreach (var setting in allSettings)
                {
                    if (setting.Key.EndsWith($"_{currentLang}"))
                    {
                        // Dil bazlı ayar: hero_badge_tr -> hero_badge
                        var langSuffix = $"_{currentLang}";
                        var baseKey = setting.Key.Substring(0, setting.Key.Length - langSuffix.Length);
                        languageSpecificSettings[baseKey] = setting.Value;
                    }
                    else if (!setting.Key.EndsWith("_tr") && !setting.Key.EndsWith("_de") && 
                             !setting.Key.EndsWith("_ru") && !setting.Key.EndsWith("_en"))
                    {
                        // Genel ayar (dil kodu içermeyen)
                        generalSettings[setting.Key] = setting.Value;
                    }
                }
                
                // Önce dil bazlı ayarları kullan, yoksa genel ayarları kullan
                var result = new Dictionary<string, string>(generalSettings);
                foreach (var langSetting in languageSpecificSettings)
                {
                    result[langSetting.Key] = langSetting.Value;
                }
                
                return result;
            });

            // Tüm işlemleri paralel bekle (ÇOK HIZLI!)
            await Task.WhenAll(vehiclesTask, heroSlidesTask, galleryImagesTask, regionsTask, allRegionsForFormTask, settingsTask);

            var tumAraclar = await vehiclesTask;
            var vehicles = tumAraclar.Take(3).ToList();

            // Araç kartında resim yoksa veya 404 verirse kullanılacak varsayılan görsel
            var aracdetayDir = Path.Combine(_env.WebRootPath, "images", "aracdetay");
            var defaultVehicleImg = (Directory.Exists(aracdetayDir)
                ? Directory.GetFiles(aracdetayDir, "*.jpg").OrderBy(f => f).Select(f => "/images/aracdetay/" + Path.GetFileName(f)).FirstOrDefault()
                : null) ?? "";
            ViewBag.DefaultVehicleImage = defaultVehicleImg;

            var viewModel = new HomeViewModel
            {
                Vehicles = vehicles,
                HeroSlides = await heroSlidesTask,
                GalleryImages = await galleryImagesTask,
                Regions = await regionsTask,
                AllRegionsForForm = await allRegionsForFormTask,
                Settings = await settingsTask
            };
            
            return View(viewModel);
        }

        [OutputCache(PolicyName = "Static")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public IActionResult About()
        {
            return View();
        }

        [OutputCache(PolicyName = "Static")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public IActionResult Drivers()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(Contact model)
        {
            if (ModelState.IsValid)
            {
                _context.Contacts.Add(model);
                await _context.SaveChangesAsync();
                
                // Email gönder
                try
                {
                    await _emailService.SendContactFormEmailAsync(
                        model.FullName, 
                        model.Email, 
                        model.Phone ?? "", 
                        model.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Contact email failed");
                }
                
                TempData["Success"] = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapacağız.";
                var lang = HttpContext.GetRouteValue("lang") ?? "tr";
                return RedirectToAction(nameof(Contact), new { lang });
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendContactMessage(string FullName, string Phone, string Email, string Message)
        {
            if (!string.IsNullOrEmpty(FullName) && !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Message))
            {
                var contact = new Contact
                {
                    FullName = FullName,
                    Phone = Phone ?? "",
                    Email = Email,
                    Subject = "Anasayfa İletişim Formu",
                    Message = Message,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                
                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();
                
                // Email gönder
                try
                {
                    await _emailService.SendContactFormEmailAsync(FullName, Email, Phone ?? "", Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Contact email failed");
                }
                
                TempData["Success"] = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapacağız.";
            }
            else
            {
                TempData["Error"] = "Lütfen tüm alanları doldurunuz.";
            }
            var lang = HttpContext.GetRouteValue("lang") ?? "tr";
            return RedirectToAction(nameof(Index), new { lang });
        }

        [OutputCache(PolicyName = "Static")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public IActionResult Privacy()
        {
            return View();
        }

        [OutputCache(PolicyName = "Gallery")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Galeri()
        {
            const string cacheKey = "gallery_all_images";
            
            var images = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                entry.Size = 1;
                return await _context.GalleryImages
                    .AsNoTracking()
                    .Where(x => x.IsActive == 1)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();
            });
            
            return View(images);
        }

        // Cache yok: bölge ekleme/düzenleme sonrası anlık güncelleme garanti (çoklu process/instance’da da çalışır)
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> AllRegions()
        {
            var list = await _context.Regions
                .AsNoTracking()
                .Where(r => r.IsActive == 1)
                .ToListAsync();
            var cmp = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), false);
            var regions = list.OrderBy(r => string.IsNullOrWhiteSpace(r.Name) ? "" : r.Name, cmp).ToList();
            return View(regions);
        }

        // Bölge verisi cache’siz: anlık güncelleme garanti (RegionDetail sayfası)
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> RegionDetail(int id)
        {
            var region = await _context.Regions
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (region == null || region.IsActive != 1)
            {
                return NotFound();
            }

            var vehicles = await _context.Vehicles
                .AsNoTracking()
                .Where(v => v.IsActive == 1)
                .OrderBy(v => v.SortOrder)
                .ToListAsync();

            ViewBag.Vehicles = vehicles;
            return View(region);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
