using System.Security.Claims;
using DormitoryManagementSystem.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace DormitoryManagementSystem.Security;

internal static class StudentAccountSession
{
    private static readonly object LockedItem = new();
    internal const string LockedReason = "locked";
    internal const string LockedMessage = "Tài khoản đã bị quản trị viên khóa. Vui lòng liên hệ Ban quản lý.";

    internal static bool IsAdministrativelyLocked(ApplicationUser user, DateTimeOffset now) =>
        // Existing admin locks were stored 100 years ahead, while Identity's retry lock lasts 15 minutes.
        user.LockoutEnabled && user.LockoutEnd > now.AddDays(1);

    internal static bool WasLocked(HttpContext context) => context.Items.ContainsKey(LockedItem);

    internal static Task RedirectLockedAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.Equals(new PathString("/Student/NotificationSnapshot"), StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["X-Student-Session"] = LockedReason;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        else
        {
            context.Response.Redirect(QueryHelpers.AddQueryString(context.RedirectUri, "reason", LockedReason));
        }
        return Task.CompletedTask;
    }

    internal static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal?.IsInRole(RoleNames.Student) != true)
        {
            await SecurityStampValidator.ValidatePrincipalAsync(context);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var signIn = services.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await signIn.UserManager.GetUserAsync(principal);
        var locked = user is not null && IsAdministrativelyLocked(user, services.GetRequiredService<TimeProvider>().GetUtcNow());
        var stamp = principal.FindFirstValue(signIn.Options.ClaimsIdentity.SecurityStampClaimType);
        if (!locked && user is not null && await signIn.ValidateSecurityStampAsync(user, stamp))
        {
            await SecurityStampValidator.ValidatePrincipalAsync(context);
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        if (locked) context.HttpContext.Items[LockedItem] = true;
    }
}
