using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using ImperialVip.Data;
using ImperialVip.Models;
using ImperialVip.Services;
using ImperialVip.Infrastructure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ========== PERFORMANS OPTİMİZASYONLARI ==========

// 1. Response Compression - Gzip & Brotli (Hız: %70 daha hızlı)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "image/svg+xml",
        "application/json",
        "application/javascript",
        "text/css",
        "text/html",
        "text/plain"
    });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// 2. Memory Cache (Redis hazırlığıyla)
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // 1GB limit
    options.CompactionPercentage = 0.2;
});

// 3. Response Caching
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 64 * 1024 * 1024; // 64MB
    options.UseCaseSensitivePaths = false;
});

// 4. Output Caching (.NET 7+)
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Cache());
    
    // Ana sayfa cache (5 dakika)
    options.AddPolicy("HomePage", builder => builder
        .Expire(TimeSpan.FromMinutes(5))
        .Tag("homepage"));
    
    // Araçlar cache (15 dakika)
    options.AddPolicy("Vehicles", builder => builder
        .Expire(TimeSpan.FromMinutes(15))
        .Tag("vehicles"));
    
    // Bölgeler cache (30 dakika)
    options.AddPolicy("Regions", builder => builder
        .Expire(TimeSpan.FromMinutes(30))
        .Tag("regions"));
    
    // Galeri cache (1 saat)
    options.AddPolicy("Gallery", builder => builder
        .Expire(TimeSpan.FromHours(1))
        .Tag("gallery"));
    
    // Static content cache (24 saat)
    options.AddPolicy("Static", builder => builder
        .Expire(TimeSpan.FromHours(24))
        .Tag("static"));
});

// SQLite Veritabanı Bağlantısı (Optimizasyonlar eklendi)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqliteOptions =>
        {
            sqliteOptions.CommandTimeout(30);
        })
        .EnableSensitiveDataLogging(false)
        .EnableDetailedErrors(false)
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)); // Default NoTracking

// Identity Yapılandırması (Admin Girişi İçin)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Cookie Ayarları
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
});

// Antiforgery - Cache sorununu çöz
builder.Services.AddAntiforgery(options =>
{
    options.SuppressXFrameOptionsHeader = false;
});

// MVC Servislerini Ekle - 4 Dil Desteği (TR, DE, RU, EN - çeviri değil, ayrı sayfalar)
builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        options.ViewLocationExpanders.Add(new LanguageViewLocationExpander());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Veri Aktarım Servisi
builder.Services.AddScoped<DataMigrationService>();

// Email Servisi
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IReservationPdfService, ReservationPdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICurrencyRateService, CurrencyRateService>();

// Admin Exception Filter (hata yakalama ve kullanıcıya popup gösterme)
builder.Services.AddScoped<ImperialVip.Infrastructure.AdminExceptionFilter>();

// HTTP Client Factory (Performans için)
builder.Services.AddHttpClient();

// ========== PERFORMANS OPTİMİZASYONLARI SONU ==========

var app = builder.Build();

// E-posta log dosyası klasörü (Logs/email_log.txt)
ImperialVip.Infrastructure.EmailLogHelper.SetLogFolder(app.Environment.ContentRootPath ?? Directory.GetCurrentDirectory());

// Veritabanını Oluştur ve Seed Data Ekle
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    
    context.Database.EnsureCreated();
    
    // Regions tablosu yoksa oluştur (EnsureCreated mevcut DB'ye yeni tablo eklemez)
    try
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS Regions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                NameEn TEXT,
                Description TEXT,
                DescriptionEn TEXT,
                ImageUrl TEXT,
                Price REAL NOT NULL DEFAULT 0,
                StartPoint TEXT,
                StartPointEn TEXT,
                DistanceKm REAL DEFAULT 0,
                EstimatedDurationMinutes INTEGER DEFAULT 0,
                SortOrder INTEGER DEFAULT 0,
                IsActive INTEGER DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            )";
        context.Database.ExecuteSqlRaw(sql);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Regions tablosu kontrol edilirken hata: {ex.Message}");
    }
    
    // Reservations tablosuna yeni kolonlar (RegionId, AirlineCompany, AdditionalPassengerNames)
    try
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN RegionId INTEGER");
    }
    catch { /* Kolon zaten varsa yoksay */ }
    try
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN AirlineCompany TEXT");
    }
    catch { /* Kolon zaten varsa yoksay */ }
    try
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN AdditionalPassengerNames TEXT");
    }
    catch { /* Kolon zaten varsa yoksay */ }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN HotelName TEXT"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN IsReturnTransfer INTEGER DEFAULT 0"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN ReturnTransferDate TEXT"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN ReturnTransferTime TEXT"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN ReturnFlightNumber TEXT"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN NumberOfAdults INTEGER DEFAULT 1"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN NumberOfChildren INTEGER DEFAULT 0"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Reservations ADD COLUMN ChildSeatCount INTEGER DEFAULT 0"); } catch { }
    
    // Vehicles ve Regions: Para birimi kolonu (admin panelinde TL/EUR/USD seçimi)
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN Currency TEXT"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Vehicles SET Currency = 'EUR' WHERE Currency IS NULL OR Currency = ''"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Regions ADD COLUMN Currency TEXT"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Regions SET Currency = 'EUR' WHERE Currency IS NULL OR Currency = ''"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN PricePerKmUsd REAL DEFAULT 0"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN MinimumPriceUsd REAL DEFAULT 0"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN PricePerKmTry REAL DEFAULT 0"); } catch { }
    try { context.Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN MinimumPriceTry REAL DEFAULT 0"); } catch { }
    
    // CurrencyRates tablosu (admin panelinden kur düzenleme)
    try
    {
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS CurrencyRates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CurrencyCode TEXT NOT NULL,
                Rate REAL NOT NULL,
                UpdatedAt TEXT
            )");
        context.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_CurrencyRates_CurrencyCode ON CurrencyRates(CurrencyCode)");
    }
    catch (Exception ex) { Console.WriteLine($"CurrencyRates tablosu: {ex.Message}"); }
    try
    {
        if (!context.CurrencyRates.Any())
        {
            var config = services.GetRequiredService<IConfiguration>();
            var tryRate = config.GetValue<decimal>("CurrencyRates:TRY", 38.27m);
            var usdRate = config.GetValue<decimal>("CurrencyRates:USD", 1.05m);
            var gbpRate = config.GetValue<decimal>("CurrencyRates:GBP", 0.83m);
            context.CurrencyRates.Add(new CurrencyRate { CurrencyCode = "TRY", Rate = tryRate, UpdatedAt = DateTime.UtcNow });
            context.CurrencyRates.Add(new CurrencyRate { CurrencyCode = "USD", Rate = usdRate, UpdatedAt = DateTime.UtcNow });
            context.CurrencyRates.Add(new CurrencyRate { CurrencyCode = "GBP", Rate = gbpRate, UpdatedAt = DateTime.UtcNow });
            context.SaveChanges();
        }
    }
    catch (Exception ex) { Console.WriteLine($"CurrencyRates seed: {ex.Message}"); }
    
    // Reservations: NULL olan alanları varsayılanla doldur (ordinal 26 / admin hatası önlenir)
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET VehicleId = (SELECT Id FROM Vehicles LIMIT 1) WHERE VehicleId IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET PassengerCount = 1 WHERE PassengerCount IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET NumberOfAdults = 1 WHERE NumberOfAdults IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET NumberOfChildren = 0 WHERE NumberOfChildren IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET ChildSeatCount = 0 WHERE ChildSeatCount IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET LuggageCount = 0 WHERE LuggageCount IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET Status = 0 WHERE Status IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET DistanceKm = 0 WHERE DistanceKm IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET EstimatedPrice = 0 WHERE EstimatedPrice IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET IsReturnTransfer = 0 WHERE IsReturnTransfer IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET PickupLocationType = 0 WHERE PickupLocationType IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET DropoffLocationType = 0 WHERE DropoffLocationType IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET TransferDate = date('now') WHERE TransferDate IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET TransferTime = '' WHERE TransferTime IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET CustomerName = '' WHERE CustomerName IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET PickupLocation = '' WHERE PickupLocation IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET DropoffLocation = '' WHERE DropoffLocation IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Reservations SET CustomerPhone = '' WHERE CustomerPhone IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Vehicles SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL"); } catch { }
    // Vehicle: NULL sayısal alanlar (admin rezervasyon detay join hatası önlenir)
    try { context.Database.ExecuteSqlRaw("UPDATE Vehicles SET PassengerCapacity = 4 WHERE PassengerCapacity IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Vehicles SET LuggageCapacity = 2 WHERE LuggageCapacity IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Vehicles SET IsActive = 1 WHERE IsActive IS NULL"); } catch { }
    try { context.Database.ExecuteSqlRaw("UPDATE Vehicles SET SortOrder = 0 WHERE SortOrder IS NULL"); } catch { }
    
    // Boş/yarım kalmış araç kayıtlarını temizle (güncelleme hatasıyla boşalan kayıt)
    try
    {
        context.Database.ExecuteSqlRaw(@"UPDATE Reservations SET VehicleId = (SELECT Id FROM Vehicles WHERE Name IS NOT NULL AND TRIM(Name) <> '' LIMIT 1) WHERE VehicleId IN (SELECT Id FROM Vehicles WHERE Name IS NULL OR TRIM(COALESCE(Name,'')) = '')");
        context.Database.ExecuteSqlRaw(@"DELETE FROM VehicleImages WHERE VehicleId IN (SELECT Id FROM Vehicles WHERE Name IS NULL OR TRIM(COALESCE(Name,'')) = '')");
        context.Database.ExecuteSqlRaw(@"DELETE FROM Vehicles WHERE Name IS NULL OR TRIM(COALESCE(Name,'')) = ''");
    }
    catch (Exception ex) { Console.WriteLine($"Boş araç temizliği: {ex.Message}"); }
    
    await DbInitializer.Initialize(context, userManager, roleManager);
}

// Bölge görselleri için images/regions klasörünü oluştur (yoksa)
try
{
    var regionsDir = Path.Combine(app.Environment.WebRootPath ?? "", "images", "regions");
    if (!Directory.Exists(regionsDir))
    {
        Directory.CreateDirectory(regionsDir);
        Console.WriteLine($"Oluşturuldu: {regionsDir}");
    }
}
catch (Exception ex) { Console.WriteLine($"images/regions klasörü: {ex.Message}"); }

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ========== PERFORMANS MIDDLEWARE'LERİ ==========

// Admin ve Account sayfalarında sıkıştırma bazen ERR_CONTENT_DECODING_FAILED hatasına yol açıyor; bu yolları atla
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase)
              && !context.Request.Path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase),
    appBuilder => appBuilder.UseResponseCompression());

// 2. Static Files with Caching (Statik dosyalar için aggressive caching)
var cacheMaxAge = app.Environment.IsDevelopment() ? 0 : 31536000; // Dev: cache yok, Prod: 1 yıl
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Aggressive caching for static files
        ctx.Context.Response.Headers.Append("Cache-Control", $"public,max-age={cacheMaxAge}");
        ctx.Context.Response.Headers.Append("Expires", DateTime.UtcNow.AddSeconds(cacheMaxAge).ToString("R"));
        
        // Gzip compression hint
        if (ctx.File.Name.EndsWith(".css") || ctx.File.Name.EndsWith(".js"))
        {
            ctx.Context.Response.Headers.Append("Vary", "Accept-Encoding");
        }
    }
});

// 3. Response Caching
app.UseResponseCaching();

// 4. Output Caching
app.UseOutputCache();

// ========== PERFORMANS MIDDLEWARE'LERİ SONU ==========

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Dil bazlı routing: /tr/, /de/, /ru/, /en/ - Her dil ayrı site (çeviri değil)
var langConstraint = new LanguageRouteConstraint();
app.MapControllerRoute(name: "root", pattern: "", defaults: new { controller = "Home", action = "Index", lang = "tr" });
app.MapControllerRoute(name: "account", pattern: "Account/{action=Login}/{id?}", defaults: new { controller = "Account", lang = "tr" });
app.MapControllerRoute(name: "admin", pattern: "Admin/{action=Index}/{id?}", defaults: new { controller = "Admin", lang = "tr" });
// Kur API'si - lang olmadan da erişilebilir (fetch fallback için)
app.MapControllerRoute(name: "currencyApi", pattern: "Home/CurrencyRatesJson", defaults: new { controller = "Home", action = "CurrencyRatesJson" });
app.MapControllerRoute(
    name: "default",
    pattern: "{lang}/{controller=Home}/{action=Index}/{id?}",
    constraints: new { lang = langConstraint });

app.Run();
