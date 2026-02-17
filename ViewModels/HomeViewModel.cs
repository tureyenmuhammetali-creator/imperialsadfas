using ImperialVip.Models;

namespace ImperialVip.ViewModels
{
    public class HomeViewModel
    {
        public IEnumerable<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public IEnumerable<HeroSlide> HeroSlides { get; set; } = new List<HeroSlide>();
        public IEnumerable<GalleryImage> GalleryImages { get; set; } = new List<GalleryImage>();
        public IEnumerable<Region> Regions { get; set; } = new List<Region>();
        /// <summary>Form dropdown'larında gösterilmek üzere tüm bölgeler (sadece havalimanı↔bölge transferi için).</summary>
        public IEnumerable<Region> AllRegionsForForm { get; set; } = new List<Region>();
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}
