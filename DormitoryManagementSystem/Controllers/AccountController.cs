using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "Tài khoản tạm khóa do đăng nhập sai nhiều lần."
                : "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return await userManager.IsInRoleAsync(user, RoleNames.Student)
            ? RedirectToAction("Dashboard", "Student")
            : RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Đổi mật khẩu";
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        ViewData["Title"] = "Đổi mật khẩu";
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Code == "PasswordMismatch" ? "Mật khẩu hiện tại không đúng." : error.Description);
            return View(model);
        }
        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Đã đổi mật khẩu. Phiên đăng nhập hiện tại vẫn được giữ an toàn.";
        return User.IsInRole(RoleNames.Student)
            ? RedirectToAction("Dashboard", "Student")
            : RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
