using System.Text.Json;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var variants = new List<object>();
foreach (var applicationName in new[] { @"D:\DormPrj\Dorm-master\DormitoryManagementSystem\", @"D:\DormPrj\Dorm-master\DormitoryManagementSystem" })
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASP.NET", "DataProtection-Keys")))
        .SetApplicationName(applicationName);
    services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);
    services.AddDbContext<DormitoryDbContext>(options => options.UseSqlServer(
        "Server=DESKTOP-OSSBOT1\\TUANANH;Database=DormitoryManagementSystem;Trusted_Connection=True;TrustServerCertificate=True"));
    services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<DormitoryDbContext>();
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
    var cookieOptions = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
    async Task<object[]> CookieFor(string email)
    {
        var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("Demo profile not found.");
        var principal = await factory.CreateAsync(user);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
        }, IdentityConstants.ApplicationScheme);
        return [new { name = ".NepKtx.Auth", value = cookieOptions.TicketDataFormat.Protect(ticket), domain = "localhost", path = "/", httpOnly = true, secure = false, sameSite = "Lax" }];
    }
    variants.Add(new { manager = await CookieFor("manager@nepktx.local"), student = await CookieFor("student@nepktx.local") });
}
await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "nepktx-ux-session.secrets.json"), JsonSerializer.Serialize(variants));
Console.WriteLine("Created temporary local browser sessions; no database changes.");
