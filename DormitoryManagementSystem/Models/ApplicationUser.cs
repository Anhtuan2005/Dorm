using Microsoft.AspNetCore.Identity;

namespace DormitoryManagementSystem.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public StudentProfile? StudentProfile { get; set; }
}
