using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ImperialVip.Data;

namespace ImperialVip.Controllers
{
    public class VehicleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public VehicleController(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [ResponseCache(NoStore = true, Duration = 0)]
        public async Task<IActionResult> Index()
        {
            const string cacheKey = "vehicles_all_active";
            
            var vehicles = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                entry.Size = 1;
                return await _context.Vehicles
                    .AsNoTracking()
                    .Include(v => v.Images)
                    .Where(v => v.IsActive == 1)
                    .OrderBy(v => v.SortOrder)
                    .ToListAsync();
            });
            
            return View(vehicles);
        }

        [OutputCache(PolicyName = "Vehicles")]
        [ResponseCache(Duration = 900, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "id" })]
        public async Task<IActionResult> Details(int id)
        {
            var cacheKey = $"vehicle_detail_{id}";
            
            var vehicle = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                entry.Size = 1;
                return await _context.Vehicles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == id);
            });
            
            if (vehicle == null || vehicle.IsActive != 1)
            {
                return NotFound();
            }

            return View(vehicle);
        }
    }
}
