using Microsoft.AspNetCore.Identity;

namespace ImperialVip.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int IsActive { get; set; } = 1;
    }
}
