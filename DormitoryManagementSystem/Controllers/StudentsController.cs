using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class StudentsController(IStudentManagementService studentService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] StudentDirectoryQuery query,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sinh viên";
        ViewData["ActiveNav"] = "students";
        return View(await studentService.GetDirectoryAsync(query, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProfile(CreateStudentProfileInput input, CancellationToken cancellationToken)
    {
        var result = ModelState.IsValid
            ? await studentService.CreateProfileAsync(input, cancellationToken)
            : new StudentManagementResult(false, Errors());
        return RedirectWith(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateStudentProfileInput input, CancellationToken cancellationToken)
    {
        var result = ModelState.IsValid
            ? await studentService.UpdateProfileAsync(input, cancellationToken)
            : new StudentManagementResult(false, Errors(), input.Id);
        return RedirectWith(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccount(CreateStudentAccountInput input, CancellationToken cancellationToken)
    {
        var result = ModelState.IsValid
            ? await studentService.CreateAccountAsync(input, cancellationToken)
            : new StudentManagementResult(false, Errors(), input.StudentId);
        return RedirectWith(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAccountLock(int studentId, bool locked, CancellationToken cancellationToken) =>
        RedirectWith(await studentService.SetAccountLockAsync(studentId, locked, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetStudentPasswordInput input, CancellationToken cancellationToken)
    {
        var result = ModelState.IsValid
            ? await studentService.ResetPasswordAsync(input, cancellationToken)
            : new StudentManagementResult(false, Errors(), input.StudentId);
        return RedirectWith(result);
    }

    private string Errors() => string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));

    private IActionResult RedirectWith(StudentManagementResult result)
    {
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index), new { SelectedId = result.StudentId });
    }
}
