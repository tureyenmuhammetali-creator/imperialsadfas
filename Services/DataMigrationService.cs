using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ImperialVip.Data;
using ImperialVip.Models;

namespace ImperialVip.Services
{
    public class DataMigrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DataMigrationService> _logger;
        
        // Eski SQL Server connection string - SADECE OKUMA YAPILACAK!
        private const string OldDbConnectionString = "Data Source=94.73.170.33;Initial Catalog=u1950568_dbVip;User Id=u1950568_userVip;Password=k7L57:yfo2SC_S==;TrustServerCertificate=true;Encrypt=false;";

        public DataMigrationService(ApplicationDbContext context, ILogger<DataMigrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MigrationResult> MigrateAllDataAsync()
        {
            var result = new MigrationResult();
            
            try
            {
                _logger.LogInformation("Veri aktarımı başlıyor...");
                
                // 1. Araçları aktar
                result.VehiclesImported = await MigrateVehiclesAsync();
                
                // 2. Araç görsellerini aktar
                result.ImagesImported = await MigrateVehicleImagesAsync();
                
                // 3. İletişim mesajlarını aktar
                result.ContactsImported = await MigrateContactsAsync();
                
                // 4. Rezervasyonları aktar
                result.ReservationsImported = await MigrateReservationsAsync();
                
                // 5. Galeri görsellerini aktar
                result.GalleryImported = await MigrateGalleryImagesAsync();
                
                // 6. Slider görsellerini aktar
                result.SlidersImported = await MigrateSliderImagesAsync();
                
                // 7. Bölgeleri aktar
                result.RegionsImported = await MigrateRegionsAsync();
                
                result.Success = true;
                result.Message = "Veri aktarımı başarıyla tamamlandı!";
                _logger.LogInformation(result.Message);
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "";
                result.Success = false;
                result.Message = $"Hata: {ex.Message}" + (string.IsNullOrEmpty(inner) ? "" : " | " + inner);
                _logger.LogError(ex, "Veri aktarımı sırasında hata oluştu");
            }
            
            return result;
        }

        /// <summary>
        /// Sadece eksik araçları eski veritabanından geri yükler (silinen araçları geri getirir).
        /// Önce eksik araçları ekler, sonra tüm araç görsellerini eski DB'den yeniden aktarır.
        /// </summary>
        public async Task<MigrationResult> RestoreMissingVehiclesAsync()
        {
            var result = new MigrationResult();
            try
            {
                _logger.LogInformation("Eksik araçlar geri yükleniyor...");
                result.VehiclesImported = await MigrateVehiclesAsync();
                result.ImagesImported = await MigrateVehicleImagesAsync();
                result.Success = true;
                result.Message = $"Eksik araçlar geri yüklendi: {result.VehiclesImported} araç, {result.ImagesImported} araç görseli.";
                _logger.LogInformation(result.Message);
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "";
                result.Success = false;
                result.Message = $"Hata: {ex.Message}" + (string.IsNullOrEmpty(inner) ? "" : " | " + inner);
                _logger.LogError(ex, "Eksik araçlar geri yüklenirken hata");
            }
            return result;
        }

        private async Task<int> MigrateVehiclesAsync()
        {
            int count = 0;
            
            using var connection = new SqlConnection(OldDbConnectionString);
            await connection.OpenAsync();
            
            // Eski araçları çek (SADECE OKUMA - SELECT)
            var query = @"
                SELECT a.Id, a.AracAdi, a.SiraNo, a.EkUcret,
                       ad.MinYolcuSayisi, ad.MaxYolcuSayisi, ad.Bagaj, ad.Aciklama,
                       ad.FiyataDahilOlanlar
                FROM Araclar a
                LEFT JOIN AracDetaylar ad ON a.Id = ad.AracId AND ad.DilKodu = 'tr'
                ORDER BY a.SiraNo";
            
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var vehicles = new List<Vehicle>();
            
            while (await reader.ReadAsync())
            {
                var ad = reader["AracAdi"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(ad)) continue;
                var vehicle = new Vehicle
                {
                    Name = ad,
                    Type = "Sedan",
                    Brand = ExtractBrand(ad),
                    Model = ad,
                    PassengerCapacity = reader["MaxYolcuSayisi"] != DBNull.Value ? Convert.ToInt32(reader["MaxYolcuSayisi"]) : 3,
                    LuggageCapacity = ParseLuggageCapacity(reader["Bagaj"]?.ToString()),
                    Description = reader["Aciklama"]?.ToString() ?? "",
                    Features = reader["FiyataDahilOlanlar"]?.ToString() ?? "",
                    ImageUrl = $"/images/vehicles/{Slugify(ad)}.jpg",
                    PricePerKm = reader["EkUcret"] != DBNull.Value ? Convert.ToDecimal(reader["EkUcret"]) : 20m,
                    MinimumPrice = 350m,
                    MinimumPriceUsd = 0m,
                    MinimumPriceTry = 0m,
                    Currency = "EUR",
                    IsActive = 1,
                    SortOrder = reader["SiraNo"] != DBNull.Value ? Convert.ToInt32(reader["SiraNo"]) : 0,
                    CreatedAt = DateTime.UtcNow
                };
                vehicles.Add(vehicle);
            }
            
            if (vehicles.Any())
            {
                var mevcutAdlar = await _context.Vehicles.Where(v => !string.IsNullOrWhiteSpace(v.Name)).Select(v => v.Name.Trim().ToLower()).ToListAsync();
                var eklenecekler = vehicles.Where(v => !string.IsNullOrEmpty(v.Name) && !mevcutAdlar.Contains(v.Name.Trim().ToLower())).ToList();
                if (eklenecekler.Any())
                {
                    await _context.Vehicles.AddRangeAsync(eklenecekler);
                    await _context.SaveChangesAsync();
                }
                count = eklenecekler.Count;
            }
            
            _logger.LogInformation($"{count} araç aktarıldı");
            return count;
        }

        private async Task<int> MigrateVehicleImagesAsync()
        {
            int count = 0;
            
            // Önce mevcut VehicleImages'ları temizle
            var existingImages = await _context.VehicleImages.ToListAsync();
            _context.VehicleImages.RemoveRange(existingImages);
            await _context.SaveChangesAsync();
            
            using var connection = new SqlConnection(OldDbConnectionString);
            await connection.OpenAsync();
            
            // TÜM araç görsellerini çek (SADECE OKUMA - SELECT)
            var query = @"
                SELECT a.AracAdi, adg.GorselUrl, adg.Id as GorselId
                FROM AracDetayGorseller adg
                INNER JOIN AracDetaylar ad ON adg.AracDetayId = ad.Id
                INNER JOIN Araclar a ON ad.AracId = a.Id
                WHERE ad.DilKodu = 'tr'
                ORDER BY a.Id, adg.Id";
            
            using var command = new SqlCommand(query, connection);
            
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                
                var imageData = new List<(string AracAdi, string GorselUrl)>();
                
                while (await reader.ReadAsync())
                {
                    var aracAdi = reader["AracAdi"]?.ToString() ?? "";
                    var gorselUrl = reader["GorselUrl"]?.ToString() ?? "";
                    
                    if (!string.IsNullOrEmpty(gorselUrl) && !string.IsNullOrEmpty(aracAdi))
                    {
                        imageData.Add((aracAdi, gorselUrl));
                    }
                }
                
                // Araç bazında görsel sayacı
                var vehicleImageCounts = new Dictionary<string, int>();
                
                foreach (var (aracAdi, gorselUrl) in imageData)
                {
                    var fileName = Path.GetFileName(gorselUrl);
                    var newImagePath = "/images/aracdetay/" + fileName;
                    var localFilePath = Path.Combine("wwwroot", "images", "aracdetay", fileName);
                    
                    if (!File.Exists(localFilePath))
                    {
                        _logger.LogWarning($"Görsel bulunamadı: {localFilePath}");
                        continue;
                    }
                    
                    var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Name == aracAdi);
                    if (vehicle == null) continue;
                    
                    // Sayacı artır
                    if (!vehicleImageCounts.ContainsKey(aracAdi))
                        vehicleImageCounts[aracAdi] = 0;
                    vehicleImageCounts[aracAdi]++;
                    
                    // İlk görsel ana görsel olsun
                    if (vehicleImageCounts[aracAdi] == 1)
                    {
                        vehicle.ImageUrl = newImagePath;
                    }
                    
                    // VehicleImages tablosuna ekle
                    var vehicleImage = new VehicleImage
                    {
                        VehicleId = vehicle.Id,
                        ImageUrl = newImagePath,
                        SortOrder = vehicleImageCounts[aracAdi]
                    };
                    _context.VehicleImages.Add(vehicleImage);
                    count++;
                }
                
                await _context.SaveChangesAsync();
                _logger.LogInformation($"{count} araç görseli aktarıldı");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Araç görselleri aktarılamadı: {ex.Message}");
            }
            
            return count;
        }
        
        private async Task<int> MigrateGalleryImagesAsync()
        {
            int count = 0;
            
            // Önce mevcut galeri görsellerini temizle
            var existingImages = await _context.GalleryImages.ToListAsync();
            _context.GalleryImages.RemoveRange(existingImages);
            await _context.SaveChangesAsync();
            
            // wwwroot/images/imgs/galeri klasöründeki görselleri aktar
            var galeriPath = Path.Combine("wwwroot", "images", "imgs", "galeri");
            
            if (!Directory.Exists(galeriPath))
            {
                _logger.LogWarning($"Galeri klasörü bulunamadı: {galeriPath}");
                return 0;
            }
            
            var imageFiles = Directory.GetFiles(galeriPath, "*.*")
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                           f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();
            
            int sortOrder = 1;
            foreach (var file in imageFiles)
            {
                var fileName = Path.GetFileName(file);
                var imageUrl = "/images/imgs/galeri/" + fileName;
                
                var galleryImage = new GalleryImage
                {
                    ImageUrl = imageUrl,
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    Category = "Genel",
                    SortOrder = sortOrder++,
                    IsActive = 1
                };
                
                _context.GalleryImages.Add(galleryImage);
                count++;
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation($"{count} galeri görseli aktarıldı");
            
            return count;
        }

        private async Task<int> MigrateSliderImagesAsync()
        {
            int count = 0;
            
            // Önce mevcut slider görsellerini temizle
            var existingSlides = await _context.HeroSlides.ToListAsync();
            _context.HeroSlides.RemoveRange(existingSlides);
            await _context.SaveChangesAsync();
            
            // wwwroot/images/imgs/slider klasöründeki görselleri aktar
            var sliderPath = Path.Combine("wwwroot", "images", "imgs", "slider");
            
            if (!Directory.Exists(sliderPath))
            {
                _logger.LogWarning($"Slider klasörü bulunamadı: {sliderPath}");
                return 0;
            }
            
            var imageFiles = Directory.GetFiles(sliderPath, "*.*")
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                           f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();
            
            int sortOrder = 1;
            foreach (var file in imageFiles)
            {
                var fileName = Path.GetFileName(file);
                var imageUrl = "/images/imgs/slider/" + fileName;
                
                var heroSlide = new HeroSlide
                {
                    ImageUrl = imageUrl,
                    SortOrder = sortOrder++,
                    IsActive = 1
                };
                
                _context.HeroSlides.Add(heroSlide);
                count++;
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation($"{count} slider görseli aktarıldı");
            
            return count;
        }

        private async Task<int> MigrateContactsAsync()
        {
            int count = 0;
            
            using var connection = new SqlConnection(OldDbConnectionString);
            await connection.OpenAsync();
            
            // Eski iletişim mesajlarını çek (SADECE OKUMA - SELECT)
            var query = @"
                SELECT Id, AdSoyad, Email, Telefon, Konu, Mesaj, OkunduMu, KayitTarihi
                FROM BizUlasinMailler
                ORDER BY KayitTarihi DESC";
            
            using var command = new SqlCommand(query, connection);
            
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                
                var contacts = new List<Contact>();
                
                while (await reader.ReadAsync())
                {
                    var contact = new Contact
                    {
                        FullName = reader["AdSoyad"]?.ToString() ?? "",
                        Email = reader["Email"]?.ToString() ?? "",
                        Phone = reader["Telefon"]?.ToString() ?? "",
                        Subject = reader["Konu"]?.ToString() ?? "İletişim",
                        Message = reader["Mesaj"]?.ToString() ?? "",
                        IsRead = reader["OkunduMu"] != DBNull.Value && Convert.ToBoolean(reader["OkunduMu"]),
                        CreatedAt = reader["KayitTarihi"] != DBNull.Value ? Convert.ToDateTime(reader["KayitTarihi"]) : DateTime.UtcNow
                    };
                    
                    contacts.Add(contact);
                }
                
                if (contacts.Any())
                {
                    await _context.Contacts.AddRangeAsync(contacts);
                    await _context.SaveChangesAsync();
                    count = contacts.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"İletişim mesajları aktarılamadı: {ex.Message}");
            }
            
            _logger.LogInformation($"{count} iletişim mesajı aktarıldı");
            return count;
        }

        private async Task<int> MigrateReservationsAsync()
        {
            int count = 0;
            
            using var connection = new SqlConnection(OldDbConnectionString);
            await connection.OpenAsync();
            
            // Eski rezervasyonları çek (SADECE OKUMA - SELECT)
            var query = @"
                SELECT r.Id, r.AdSoyad, r.Telefon, r.Email, r.OzelNot,
                       r.GelisTarihi, r.GelisUcusNumarasi, r.YetiskinSayisi, r.CocukSayisi,
                       r.Fiyat, r.RezervasyonOnay, r.IsCompleted, r.KayitTarihi,
                       r.AracAlisSaati, r.OtelAdi,
                       b1.BolgeAdi as AlisNoktasi,
                       b2.BolgeAdi as VarisNoktasi,
                       a.AracAdi
                FROM Rezervasyonlar r
                LEFT JOIN Bolgeler b1 ON r.AlisNoktasiId = b1.Id
                LEFT JOIN Bolgeler b2 ON r.VarisNoktasiId = b2.Id
                LEFT JOIN Araclar a ON r.AracId = a.Id
                ORDER BY r.KayitTarihi DESC";
            
            using var command = new SqlCommand(query, connection);
            
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                
                var reservations = new List<Reservation>();
                
                while (await reader.ReadAsync())
                {
                    var reservation = new Reservation
                    {
                        CustomerName = reader["AdSoyad"]?.ToString() ?? "",
                        CustomerPhone = reader["Telefon"]?.ToString() ?? "",
                        CustomerEmail = reader["Email"]?.ToString() ?? "",
                        PickupLocationType = LocationType.Havalimani,
                        PickupLocation = reader["AlisNoktasi"]?.ToString() ?? "",
                        PickupLocationDetail = reader["OtelAdi"]?.ToString() ?? "",
                        DropoffLocationType = LocationType.Otel,
                        DropoffLocation = reader["VarisNoktasi"]?.ToString() ?? "",
                        DropoffLocationDetail = "",
                        TransferDate = reader["GelisTarihi"] != DBNull.Value ? Convert.ToDateTime(reader["GelisTarihi"]) : DateTime.UtcNow,
                        TransferTime = reader["AracAlisSaati"] != DBNull.Value ? ((TimeSpan)reader["AracAlisSaati"]).ToString(@"hh\:mm") : "12:00",
                        FlightNumber = reader["GelisUcusNumarasi"]?.ToString() ?? "",
                        PassengerCount = reader["YetiskinSayisi"] != DBNull.Value ? Convert.ToInt32(reader["YetiskinSayisi"]) : 1,
                        LuggageCount = reader["CocukSayisi"] != DBNull.Value ? Convert.ToInt32(reader["CocukSayisi"]) : 0,
                        Notes = reader["OzelNot"]?.ToString() ?? "",
                        EstimatedPrice = ParsePrice(reader["Fiyat"]?.ToString()),
                        Status = GetReservationStatus(reader),
                        CreatedAt = reader["KayitTarihi"] != DBNull.Value ? Convert.ToDateTime(reader["KayitTarihi"]) : DateTime.UtcNow
                    };
                    
                    reservations.Add(reservation);
                }
                
                if (reservations.Any())
                {
                    await _context.Reservations.AddRangeAsync(reservations);
                    await _context.SaveChangesAsync();
                    count = reservations.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Rezervasyonlar aktarılamadı: {ex.Message}");
            }
            
            _logger.LogInformation($"{count} rezervasyon aktarıldı");
            return count;
        }

        private async Task<int> MigrateRegionsAsync()
        {
            int count = 0;
            
            using var connection = new SqlConnection(OldDbConnectionString);
            await connection.OpenAsync();
            
            // Eski bölgeleri çek - Basit sorgu (SADECE OKUMA - SELECT)
            var query = @"SELECT Id, BolgeAdi, SiraNo, Fiyat FROM Bolgeler ORDER BY SiraNo";
            
            using var command = new SqlCommand(query, connection);
            
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                
                var regions = new List<Region>();
                
                while (await reader.ReadAsync())
                {
                    var bolgeAdi = reader["BolgeAdi"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(bolgeAdi)) continue;
                    
                    var region = new Region
                    {
                        Name = bolgeAdi,
                        NameEn = bolgeAdi,
                        Description = "",
                        DescriptionEn = "",
                        StartPoint = "Antalya Havalimanı",
                        StartPointEn = "Antalya Airport",
                        Price = reader["Fiyat"] != DBNull.Value ? Convert.ToDecimal(reader["Fiyat"]) : 50m,
                        DistanceKm = 0,
                        EstimatedDurationMinutes = 0,
                        SortOrder = reader["SiraNo"] != DBNull.Value ? Convert.ToInt32(reader["SiraNo"]) : 0,
                        ImageUrl = GetRegionImageUrl(bolgeAdi),
                        IsActive = 1,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    regions.Add(region);
                }
                
                if (regions.Any())
                {
                    // Mevcut bölgeleri temizle
                    _context.Regions.RemoveRange(_context.Regions);
                    await _context.SaveChangesAsync();
                    
                    await _context.Regions.AddRangeAsync(regions);
                    await _context.SaveChangesAsync();
                    count = regions.Count;
                }
                
                _logger.LogInformation($"{count} bölge aktarıldı");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Bölgeler aktarılamadı (ilk sorgu): {ex.Message}");
                
                // Alternatif: Rezervasyonlardan benzersiz bölgeleri çek
                try
                {
                    await connection.CloseAsync();
                    await connection.OpenAsync();
                    
                    var altQuery = @"
                        SELECT DISTINCT BolgeAdi, MIN(Id) as Id 
                        FROM Bolgeler 
                        WHERE BolgeAdi IS NOT NULL AND BolgeAdi != ''
                        GROUP BY BolgeAdi
                        ORDER BY MIN(Id)";
                    
                    using var altCommand = new SqlCommand(altQuery, connection);
                    using var altReader = await altCommand.ExecuteReaderAsync();
                    
                    var regions = new List<Region>();
                    int sortOrder = 1;
                    
                    while (await altReader.ReadAsync())
                    {
                        var bolgeAdi = altReader["BolgeAdi"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(bolgeAdi)) continue;
                        
                        var region = new Region
                        {
                            Name = bolgeAdi,
                            NameEn = bolgeAdi,
                            Description = "",
                            DescriptionEn = "",
                            StartPoint = "Antalya Havalimanı",
                            StartPointEn = "Antalya Airport",
                            Price = 50m,
                            DistanceKm = 0,
                            EstimatedDurationMinutes = 0,
                            SortOrder = sortOrder++,
                            ImageUrl = GetRegionImageUrl(bolgeAdi),
                            IsActive = 1,
                            CreatedAt = DateTime.UtcNow
                        };
                        
                        regions.Add(region);
                    }
                    
                    if (regions.Any())
                    {
                        _context.Regions.RemoveRange(_context.Regions);
                        await _context.SaveChangesAsync();
                        
                        await _context.Regions.AddRangeAsync(regions);
                        await _context.SaveChangesAsync();
                        count = regions.Count;
                    }
                    
                    _logger.LogInformation($"{count} bölge aktarıldı (alternatif sorgu)");
                }
                catch (Exception altEx)
                {
                    _logger.LogWarning($"Bölgeler aktarılamadı (alternatif): {altEx.Message}");
                }
            }
            
            return count;
        }

        private string GetRegionImageUrl(string bolgeAdi)
        {
            // Bölge adını küçük harfe çevir ve Türkçe karakterleri düzelt
            var slug = bolgeAdi.ToLowerInvariant()
                .Replace("ş", "s")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c")
                .Replace("ı", "i")
                .Replace(" ", "-")
                .Trim();
            
            // wwwroot/images/imgs/location klasöründe eşleşen resim ara
            var locationFolder = Path.Combine("wwwroot", "images", "imgs", "location");
            if (Directory.Exists(locationFolder))
            {
                var allFiles = Directory.GetFiles(locationFolder, "*.*")
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                               f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                // 1. Tam eşleşme (slug ile başlayan)
                var exactMatch = allFiles.FirstOrDefault(f => 
                    Path.GetFileNameWithoutExtension(f).ToLowerInvariant().StartsWith(slug));
                if (exactMatch != null)
                {
                    return "/images/imgs/location/" + Path.GetFileName(exactMatch);
                }
                
                // 2. İlk 4 karakter eşleşmesi (çenger -> ceng, cender -> cend gibi yakın eşleşmeler)
                if (slug.Length >= 4)
                {
                    var partialMatch = allFiles.FirstOrDefault(f =>
                    {
                        var fileName = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        // İlk 4 karakterin en az 3'ü eşleşiyor mu?
                        var slugStart = slug.Substring(0, Math.Min(4, slug.Length));
                        var fileStart = fileName.Substring(0, Math.Min(4, fileName.Length));
                        int matchCount = 0;
                        for (int i = 0; i < Math.Min(slugStart.Length, fileStart.Length); i++)
                        {
                            if (slugStart[i] == fileStart[i]) matchCount++;
                        }
                        return matchCount >= 3;
                    });
                    
                    if (partialMatch != null)
                    {
                        return "/images/imgs/location/" + Path.GetFileName(partialMatch);
                    }
                }
                
                // 3. Dosya adı slug'ı içeriyor mu veya slug dosya adını içeriyor mu
                var containsMatch = allFiles.FirstOrDefault(f =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Split('-')[0];
                    return fileName.Contains(slug) || slug.Contains(fileName);
                });
                
                if (containsMatch != null)
                {
                    return "/images/imgs/location/" + Path.GetFileName(containsMatch);
                }
            }
            
            return $"/images/imgs/location/{slug}.jpg";
        }

        #region Helper Methods
        
        private string ExtractBrand(string aracAdi)
        {
            if (aracAdi.Contains("Mercedes", StringComparison.OrdinalIgnoreCase)) return "Mercedes-Benz";
            if (aracAdi.Contains("BMW", StringComparison.OrdinalIgnoreCase)) return "BMW";
            if (aracAdi.Contains("Audi", StringComparison.OrdinalIgnoreCase)) return "Audi";
            if (aracAdi.Contains("Volkswagen", StringComparison.OrdinalIgnoreCase)) return "Volkswagen";
            if (aracAdi.Contains("VW", StringComparison.OrdinalIgnoreCase)) return "Volkswagen";
            return "Diğer";
        }
        
        private int ParseLuggageCapacity(string? bagaj)
        {
            if (string.IsNullOrEmpty(bagaj)) return 3;
            
            var numbers = new string(bagaj.Where(char.IsDigit).ToArray());
            return int.TryParse(numbers, out int result) ? result : 3;
        }
        
        private string Slugify(string text)
        {
            return text.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("ş", "s")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c")
                .Replace("ı", "i");
        }
        
        private decimal ParsePrice(string? priceStr)
        {
            if (string.IsNullOrEmpty(priceStr)) return 0;
            
            var cleanPrice = new string(priceStr.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
            cleanPrice = cleanPrice.Replace(",", ".");
            
            return decimal.TryParse(cleanPrice, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out decimal result) ? result : 0;
        }
        
        private ReservationStatus GetReservationStatus(SqlDataReader reader)
        {
            bool isCompleted = reader["IsCompleted"] != DBNull.Value && Convert.ToBoolean(reader["IsCompleted"]);
            bool isApproved = reader["RezervasyonOnay"] != DBNull.Value && Convert.ToBoolean(reader["RezervasyonOnay"]);
            
            if (isCompleted) return ReservationStatus.Tamamlandi;
            if (isApproved) return ReservationStatus.Onaylandi;
            return ReservationStatus.Beklemede;
        }
        
        #endregion
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int VehiclesImported { get; set; }
        public int ImagesImported { get; set; }
        public int ContactsImported { get; set; }
        public int ReservationsImported { get; set; }
        public int GalleryImported { get; set; }
        public int SlidersImported { get; set; }
        public int RegionsImported { get; set; }
    }
}
