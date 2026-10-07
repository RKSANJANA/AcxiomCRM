using Microsoft.AspNetCore.Identity;

namespace Acxiom.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int? TeamId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
