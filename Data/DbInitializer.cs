using Microsoft.AspNetCore.Identity;
using ImperialVip.Models;

namespace ImperialVip.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // Rolleri Oluştur
            string[] roles = { "Admin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Admin Kullanıcısı Oluştur
            var adminEmail = "admin@imperialvip.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Admin",
                    EmailConfirmed = true,
                    IsActive = 1
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Araçları Seed Et
            if (!context.Vehicles.Any())
            {
                var vehicles = new List<Vehicle>
                {
                    new Vehicle
                    {
                        Name = "Mercedes-Benz S-Class",
                        Type = "Sedan",
                        Brand = "Mercedes-Benz",
                        Model = "S 500 4MATIC",
                        PassengerCapacity = 3,
                        LuggageCapacity = 3,
                        Description = "Üst düzey konfor ve prestij. Deri döşeme, masaj koltuğu, panoramik tavan.",
                        Features = "Deri Koltuk,Masaj,Klima,WiFi,USB Şarj,Mini Bar,Panoramik Tavan",
                        ImageUrl = "/images/vehicles/mercedes-s-class.jpg",
                        PricePerKm = 25.00m,
                        MinimumPrice = 500.00m,
                        IsActive = 1,
                        SortOrder = 1
                    },
                    new Vehicle
                    {
                        Name = "Mercedes-Benz E-Class",
                        Type = "Sedan",
                        Brand = "Mercedes-Benz",
                        Model = "E 300 AMG",
                        PassengerCapacity = 3,
                        LuggageCapacity = 3,
                        Description = "Zarafet ve performansın mükemmel uyumu. Business class seyahat deneyimi.",
                        Features = "Deri Koltuk,Klima,WiFi,USB Şarj,Ambient Aydınlatma",
                        ImageUrl = "/images/vehicles/mercedes-e-class.jpg",
                        PricePerKm = 18.00m,
                        MinimumPrice = 350.00m,
                        IsActive = 1,
                        SortOrder = 2
                    },
                    new Vehicle
                    {
                        Name = "Mercedes-Benz V-Class",
                        Type = "VAN",
                        Brand = "Mercedes-Benz",
                        Model = "V 300 d",
                        PassengerCapacity = 7,
                        LuggageCapacity = 6,
                        Description = "Gruplar için ideal. Geniş iç hacim, konforlu koltuklar, bol bagaj alanı.",
                        Features = "Deri Koltuk,Klima,WiFi,USB Şarj,TV Ekran,Geniş Bagaj",
                        ImageUrl = "/images/vehicles/mercedes-v-class.jpg",
                        PricePerKm = 22.00m,
                        MinimumPrice = 450.00m,
                        IsActive = 1,
                        SortOrder = 3
                    },
                    new Vehicle
                    {
                        Name = "BMW 7 Series",
                        Type = "Sedan",
                        Brand = "BMW",
                        Model = "740i xDrive",
                        PassengerCapacity = 3,
                        LuggageCapacity = 3,
                        Description = "Alman mühendisliğinin zirvesi. Spor ve konforun kusursuz birleşimi.",
                        Features = "Deri Koltuk,Masaj,Klima,WiFi,USB Şarj,Harman Kardon Ses Sistemi",
                        ImageUrl = "/images/vehicles/bmw-7-series.jpg",
                        PricePerKm = 24.00m,
                        MinimumPrice = 480.00m,
                        IsActive = 1,
                        SortOrder = 4
                    },
                    new Vehicle
                    {
                        Name = "Audi A8",
                        Type = "Sedan",
                        Brand = "Audi",
                        Model = "A8 L 60 TFSI",
                        PassengerCapacity = 3,
                        LuggageCapacity = 3,
                        Description = "Teknoloji ve lüksün buluşması. Virtual cockpit, quattro güvenliği.",
                        Features = "Deri Koltuk,Masaj,Klima,WiFi,USB Şarj,Bang & Olufsen Ses Sistemi",
                        ImageUrl = "/images/vehicles/audi-a8.jpg",
                        PricePerKm = 23.00m,
                        MinimumPrice = 460.00m,
                        IsActive = 1,
                        SortOrder = 5
                    }
                };

                context.Vehicles.AddRange(vehicles);
                await context.SaveChangesAsync();
            }

            // Site Ayarlarını Seed Et - Eksik olanları ekle
            var requiredSettings = new List<SiteSettings>
            {
                new SiteSettings { Key = "CompanyName", Value = "Imperial VIP", Description = "Firma Adı" },
                new SiteSettings { Key = "Phone", Value = "+90 533 925 10 20", Description = "Telefon" },
                new SiteSettings { Key = "Email", Value = "info@imperialvip.com", Description = "E-posta" },
                new SiteSettings { Key = "Address", Value = "İstanbul, Türkiye", Description = "Adres" },
                new SiteSettings { Key = "WhatsApp", Value = "+905339251020", Description = "WhatsApp Numarası" },
                new SiteSettings { Key = "Instagram", Value = "imperialvip", Description = "Instagram Kullanıcı Adı" },
                new SiteSettings { Key = "Facebook", Value = "imperialvip", Description = "Facebook Sayfa Adı" }
            };

            // Hero Bölümü - Her dil için ayrı ayarlar
            var languages = new[] { "tr", "de", "ru", "en" };
            var langNames = new Dictionary<string, string> 
            { 
                ["tr"] = "Türkçe", 
                ["de"] = "Deutsch", 
                ["ru"] = "Русский", 
                ["en"] = "English" 
            };
            
            var heroDefaults = new Dictionary<string, Dictionary<string, string>>
            {
                ["tr"] = new Dictionary<string, string>
                {
                    ["hero_badge"] = "Premium VIP Transfer Hizmetleri",
                    ["hero_title"] = "Lüks ve Konforlu",
                    ["hero_subtitle"] = "Transfer Hizmetleri",
                    ["hero_description"] = "Havalimanı, otel ve şehirler arası VIP transfer hizmetleri. Üst segment araçlar, profesyonel şoförler ve kesintisiz iletişim."
                },
                ["de"] = new Dictionary<string, string>
                {
                    ["hero_badge"] = "Premium VIP Transferdienstleistungen",
                    ["hero_title"] = "Luxus und Komfort",
                    ["hero_subtitle"] = "Transferdienstleistungen",
                    ["hero_description"] = "VIP-Transfer vom Flughafen, Hotel und zwischen Städten. Premium-Fahrzeuge, professionelle Fahrer und ununterbrochene Kommunikation."
                },
                ["ru"] = new Dictionary<string, string>
                {
                    ["hero_badge"] = "Премиум VIP Трансферные услуги",
                    ["hero_title"] = "Роскошь и комфорт",
                    ["hero_subtitle"] = "Трансферные услуги",
                    ["hero_description"] = "VIP трансфер из аэропорта, отеля и между городами. Автомобили премиум-класса, профессиональные водители и бесперебойная связь."
                },
                ["en"] = new Dictionary<string, string>
                {
                    ["hero_badge"] = "Premium VIP Transfer Services",
                    ["hero_title"] = "Luxury and Comfort",
                    ["hero_subtitle"] = "Transfer Services",
                    ["hero_description"] = "VIP transfers from airport, hotel and between cities. Premium vehicles, professional drivers and seamless communication."
                }
            };
            
            var heroDescriptions = new Dictionary<string, string>
            {
                ["hero_badge"] = "Ana Sayfa Üst Rozet Yazısı",
                ["hero_title"] = "Ana Sayfa Başlık (1. Satır)",
                ["hero_subtitle"] = "Ana Sayfa Başlık (2. Satır)",
                ["hero_description"] = "Ana Sayfa Açıklama"
            };

            foreach (var setting in requiredSettings)
            {
                if (!context.SiteSettings.Any(s => s.Key == setting.Key))
                {
                    context.SiteSettings.Add(setting);
                }
            }
            
            // Her dil için hero ayarlarını ekle
            foreach (var lang in languages)
            {
                foreach (var heroKey in heroDefaults[lang].Keys)
                {
                    var settingKey = $"{heroKey}_{lang}";
                    if (!context.SiteSettings.Any(s => s.Key == settingKey))
                    {
                        context.SiteSettings.Add(new SiteSettings
                        {
                            Key = settingKey,
                            Value = heroDefaults[lang][heroKey],
                            Description = $"{heroDescriptions[heroKey]} ({langNames[lang]})"
                        });
                    }
                }
            }
            
            await context.SaveChangesAsync();

            // Hero Slide boş başlasın - kullanıcı kendi yükleyecek
        }
    }
}
