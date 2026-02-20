using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using ImperialVip.Data;
using ImperialVip.Models;
using ImperialVip.Services;
using ImperialVip.Infrastructure;
using Microsoft.Extensions.Options;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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

// SQLite Veritabanı - App_Data klasörü (IIS yazma izni olan klasör)
var appDataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
if (!Directory.Exists(appDataDir))
    Directory.CreateDirectory(appDataDir);
var dbPath = Path.Combine(appDataDir, "imperialvip.db");
var connectionString = $"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate;Pooling=True";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        connectionString,
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
builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsAppSettings"));
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
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
try
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    
    context.Database.EnsureCreated();
    
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
    catch (Exception ex) { Console.WriteLine($"Regions tablosu kontrol: {ex.Message}"); }
    
    var alterCommands = new[]
    {
        "ALTER TABLE Reservations ADD COLUMN RegionId INTEGER",
        "ALTER TABLE Reservations ADD COLUMN AirlineCompany TEXT",
        "ALTER TABLE Reservations ADD COLUMN AdditionalPassengerNames TEXT",
        "ALTER TABLE Reservations ADD COLUMN HotelName TEXT",
        "ALTER TABLE Reservations ADD COLUMN IsReturnTransfer INTEGER DEFAULT 0",
        "ALTER TABLE Reservations ADD COLUMN ReturnTransferDate TEXT",
        "ALTER TABLE Reservations ADD COLUMN ReturnTransferTime TEXT",
        "ALTER TABLE Reservations ADD COLUMN ReturnFlightNumber TEXT",
        "ALTER TABLE Reservations ADD COLUMN NumberOfAdults INTEGER DEFAULT 1",
        "ALTER TABLE Reservations ADD COLUMN NumberOfChildren INTEGER DEFAULT 0",
        "ALTER TABLE Reservations ADD COLUMN ChildSeatCount INTEGER DEFAULT 0",
        "ALTER TABLE Reservations ADD COLUMN ChildNames TEXT",
        "ALTER TABLE Reservations ADD COLUMN Language TEXT DEFAULT 'en'",
        "ALTER TABLE Reservations ADD COLUMN Currency TEXT DEFAULT 'EUR'",
        "ALTER TABLE Vehicles ADD COLUMN Currency TEXT",
        "ALTER TABLE Regions ADD COLUMN Currency TEXT",
        "ALTER TABLE Vehicles ADD COLUMN PricePerKmUsd REAL DEFAULT 0",
        "ALTER TABLE Vehicles ADD COLUMN MinimumPriceUsd REAL DEFAULT 0",
        "ALTER TABLE Vehicles ADD COLUMN PricePerKmTry REAL DEFAULT 0",
        "ALTER TABLE Vehicles ADD COLUMN MinimumPriceTry REAL DEFAULT 0",
    };
    foreach (var cmd in alterCommands)
    {
        try { context.Database.ExecuteSqlRaw(cmd); } catch { }
    }
    
    var updateCommands = new[]
    {
        "UPDATE Vehicles SET Currency = 'EUR' WHERE Currency IS NULL OR Currency = ''",
        "UPDATE Regions SET Currency = 'EUR' WHERE Currency IS NULL OR Currency = ''",
    };
    foreach (var cmd in updateCommands)
    {
        try { context.Database.ExecuteSqlRaw(cmd); } catch { }
    }
    
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
    
    var nullFixCommands = new[]
    {
        "UPDATE Reservations SET VehicleId = (SELECT Id FROM Vehicles LIMIT 1) WHERE VehicleId IS NULL",
        "UPDATE Reservations SET PassengerCount = 1 WHERE PassengerCount IS NULL",
        "UPDATE Reservations SET NumberOfAdults = 1 WHERE NumberOfAdults IS NULL",
        "UPDATE Reservations SET NumberOfChildren = 0 WHERE NumberOfChildren IS NULL",
        "UPDATE Reservations SET ChildSeatCount = 0 WHERE ChildSeatCount IS NULL",
        "UPDATE Reservations SET LuggageCount = 0 WHERE LuggageCount IS NULL",
        "UPDATE Reservations SET Status = 0 WHERE Status IS NULL",
        "UPDATE Reservations SET DistanceKm = 0 WHERE DistanceKm IS NULL",
        "UPDATE Reservations SET EstimatedPrice = 0 WHERE EstimatedPrice IS NULL",
        "UPDATE Reservations SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL",
        "UPDATE Reservations SET IsReturnTransfer = 0 WHERE IsReturnTransfer IS NULL",
        "UPDATE Reservations SET PickupLocationType = 0 WHERE PickupLocationType IS NULL",
        "UPDATE Reservations SET DropoffLocationType = 0 WHERE DropoffLocationType IS NULL",
        "UPDATE Reservations SET TransferDate = date('now') WHERE TransferDate IS NULL",
        "UPDATE Reservations SET TransferTime = '' WHERE TransferTime IS NULL",
        "UPDATE Reservations SET CustomerName = '' WHERE CustomerName IS NULL",
        "UPDATE Reservations SET PickupLocation = '' WHERE PickupLocation IS NULL",
        "UPDATE Reservations SET DropoffLocation = '' WHERE DropoffLocation IS NULL",
        "UPDATE Reservations SET CustomerPhone = '' WHERE CustomerPhone IS NULL",
        "UPDATE Vehicles SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL",
        "UPDATE Vehicles SET PassengerCapacity = 4 WHERE PassengerCapacity IS NULL",
        "UPDATE Vehicles SET LuggageCapacity = 2 WHERE LuggageCapacity IS NULL",
        "UPDATE Vehicles SET IsActive = 1 WHERE IsActive IS NULL",
        "UPDATE Vehicles SET SortOrder = 0 WHERE SortOrder IS NULL",
    };
    foreach (var cmd in nullFixCommands)
    {
        try { context.Database.ExecuteSqlRaw(cmd); } catch { }
    }
    
    try
    {
        context.Database.ExecuteSqlRaw(@"UPDATE Reservations SET VehicleId = (SELECT Id FROM Vehicles WHERE Name IS NOT NULL AND TRIM(Name) <> '' LIMIT 1) WHERE VehicleId IN (SELECT Id FROM Vehicles WHERE Name IS NULL OR TRIM(COALESCE(Name,'')) = '')");
        context.Database.ExecuteSqlRaw(@"DELETE FROM VehicleImages WHERE VehicleId IN (SELECT Id FROM Vehicles WHERE Name IS NULL OR TRIM(COALESCE(Name,'')) = '')");
        context.Database.ExecuteSqlRaw(@"DELETE FROM Vehicles WHERE Name IS NULL OR TRIM(COALESCE(Name,'')) = ''");
    }
    catch (Exception ex) { Console.WriteLine($"Boş araç temizliği: {ex.Message}"); }
    
    await DbInitializer.Initialize(context, userManager, roleManager);
}
catch (Exception ex)
{
    Console.WriteLine($"KRITIK: Veritabanı başlatma hatası: {ex}");
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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Teşhis endpoint'i - sunucu durumunu kontrol et
app.MapGet("/diag", () =>
{
    var info = new
    {
        status = "OK",
        time = DateTime.UtcNow.ToString("o"),
        env = app.Environment.EnvironmentName,
        contentRoot = app.Environment.ContentRootPath,
        webRoot = app.Environment.WebRootPath,
        dbPath = Path.Combine(app.Environment.ContentRootPath, "imperialvip.db"),
        dbExists = File.Exists(Path.Combine(app.Environment.ContentRootPath, "imperialvip.db")),
        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
    };
    return Results.Json(info);
});

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
app.MapControllerRoute(name: "root", pattern: "", defaults: new { controller = "Home", action = "Index", lang = "en" });
app.MapControllerRoute(name: "account", pattern: "Account/{action=Login}/{id?}", defaults: new { controller = "Account", lang = "tr" });
app.MapControllerRoute(name: "admin", pattern: "Admin/{action=Index}/{id?}", defaults: new { controller = "Admin", lang = "tr" });
// Kur API'si - lang olmadan da erişilebilir (fetch fallback için)
app.MapControllerRoute(name: "currencyApi", pattern: "Home/CurrencyRatesJson", defaults: new { controller = "Home", action = "CurrencyRatesJson" });
app.MapControllerRoute(
    name: "default",
    pattern: "{lang}/{controller=Home}/{action=Index}/{id?}",
    constraints: new { lang = langConstraint });

app.Run();
