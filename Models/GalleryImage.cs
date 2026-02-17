namespace ImperialVip.Models
{
    public class GalleryImage
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = "";
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string Category { get; set; } = "Genel"; // Müşteri, Lokasyon, Araç vs.
        public int SortOrder { get; set; }
        public int IsActive { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
