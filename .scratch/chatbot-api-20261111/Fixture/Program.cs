using System.Text.Json;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var credentialsPath = Path.Combine(Path.GetTempPath(), "nepktx-chat-test.secrets.json");
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<DormitoryDbContext>(options => options.UseSqlServer(
    "Server=DESKTOP-OSSBOT1\\TUANANH;Database=DormitoryManagementSystem;Trusted_Connection=True;TrustServerCertificate=True"));
services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<DormitoryDbContext>();
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<DormitoryDbContext>();
var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

if (args.FirstOrDefault() == "cleanup")
{
    if (!File.Exists(credentialsPath)) return;
    var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(credentialsPath))!;
    var user = await users.FindByIdAsync(credentials["id"]);
    if (user is not null)
    {
        if (!user.Email!.StartsWith("qa-chat-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe fixture owner.");
        await db.StudentProfiles.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException("Fixture cleanup failed.");
    }
    File.Delete(credentialsPath);
    Console.WriteLine("Fixture removed.");
    return;
}

if (File.Exists(credentialsPath)) throw new InvalidOperationException("Clean up the existing fixture first.");
var marker = Guid.NewGuid().ToString("N")[..10];
var email = $"qa-chat-{marker}@nepktx.local";
var password = Guid.NewGuid().ToString("N") + "Aa1!";
var account = new ApplicationUser { UserName = email, Email = email, FullName = "Kiểm thử chatbot", EmailConfirmed = true };
var created = await users.CreateAsync(account, password);
if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Code)));
await File.WriteAllTextAsync(credentialsPath, JsonSerializer.Serialize(new { id = account.Id, email, password }));
var role = await users.AddToRoleAsync(account, RoleNames.Student);
if (!role.Succeeded) throw new InvalidOperationException("Fixture role assignment failed.");
db.StudentProfiles.Add(new StudentProfile
{
    UserId = account.Id, StudentCode = $"QA-CHAT-{marker}", FullName = "Kiểm thử chatbot", Gender = "Nam", Faculty = "QA"
});
await db.SaveChangesAsync();
Console.WriteLine("Fixture created; credentials saved outside the repository.");
