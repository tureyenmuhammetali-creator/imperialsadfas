using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ImperialVip.Models;

namespace ImperialVip.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleImage> VehicleImages { get; set; }
        public DbSet<GalleryImage> GalleryImages { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<SiteSettings> SiteSettings { get; set; }
        public DbSet<HeroSlide> HeroSlides { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<CurrencyRate> CurrencyRates { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Vehicle Configuration
            builder.Entity<Vehicle>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PricePerKm).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinimumPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PricePerKmUsd).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinimumPriceUsd).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PricePerKmTry).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinimumPriceTry).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).HasMaxLength(5);
                
                // PERFORMANS: Index'ler (Sorguları 10x hızlandırır!)
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.SortOrder);
                entity.HasIndex(e => new { e.IsActive, e.SortOrder }); // Composite index
            });

            // Reservation Configuration
            builder.Entity<Reservation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CustomerPhone).IsRequired().HasMaxLength(30);
                entity.Property(e => e.EstimatedPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.AirlineCompany).HasMaxLength(100);
                entity.Property(e => e.AdditionalPassengerNames).HasMaxLength(500);
                
                entity.HasOne(e => e.Vehicle)
                      .WithMany()
                      .HasForeignKey(e => e.VehicleId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.Region)
                      .WithMany()
                      .HasForeignKey(e => e.RegionId)
                      .OnDelete(DeleteBehavior.SetNull);
                
                // PERFORMANS: Index'ler
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.TransferDate);
                entity.HasIndex(e => new { e.Status, e.CreatedAt });
            });

            // Contact Configuration
            builder.Entity<Contact>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
                
                // PERFORMANS: Index'ler
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.CreatedAt);
            });

            // SiteSettings Configuration
            builder.Entity<SiteSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Key).IsUnique();
            });

            // HeroSlide Configuration
            builder.Entity<HeroSlide>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(500);
                
                // PERFORMANS: Index'ler
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => new { e.IsActive, e.SortOrder }); // Composite index
            });

            // VehicleImage Configuration
            builder.Entity<VehicleImage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(500);
                entity.HasOne(e => e.Vehicle)
                      .WithMany(v => v.Images)
                      .HasForeignKey(e => e.VehicleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // GalleryImage Configuration
            builder.Entity<GalleryImage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Category).HasMaxLength(100);
                
                // PERFORMANS: Index'ler
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.SortOrder);
                entity.HasIndex(e => new { e.IsActive, e.SortOrder }); // Composite index
            });

            // Region Configuration
            builder.Entity<Region>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).HasMaxLength(5);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                
                // PERFORMANS: Index'ler
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => new { e.IsActive, e.SortOrder }); // Composite index
            });

            // CurrencyRate - Admin panelinden düzenlenen kurlar (1 EUR = Rate)
            builder.Entity<CurrencyRate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(5);
                entity.Property(e => e.Rate).HasColumnType("decimal(18,4)");
                entity.HasIndex(e => e.CurrencyCode).IsUnique();
            });
        }
    }
}
