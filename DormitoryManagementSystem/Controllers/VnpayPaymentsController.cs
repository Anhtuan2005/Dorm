using System.Data.Common;
using System.Security.Claims;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = RoleNames.Student)]
[Route("Student/Payments/Vnpay")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VnpayPaymentsController(
    VnpayCheckoutService checkout, ILogger<VnpayPaymentsController> logger) : Controller
{
    [HttpPost("Start"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Start([FromForm] long invoiceId, CancellationToken cancellationToken)
    {
        var result = await checkout.StartAsync(UserId, invoiceId, HttpContext.Connection.RemoteIpAddress, cancellationToken);
        if (result.CheckoutUrl is { } url) return Redirect(url);

        TempData["Error"] = result.Error ?? "Không thể mở phiên thanh toán VNPAY.";
        return RedirectToAction(nameof(StudentController.Invoices), "Student");
    }

    [HttpGet("/payments/vnpay/return")]
    public async Task<IActionResult> Return(CancellationToken cancellationToken)
    {
        var payment = await checkout.ReadReturnAsync(UserId, Request.Query, cancellationToken);
        if (payment is not null)
            return RedirectToAction(nameof(Status), new { reference = payment.Reference });

        TempData["Error"] = "Không xác minh được giao dịch VNPAY. Vui lòng kiểm tra lịch sử khoản thu.";
        return RedirectToAction(nameof(StudentController.Invoices), "Student");
    }

    [HttpGet("{reference}")]
    public async Task<IActionResult> Status(string reference, CancellationToken cancellationToken)
    {
        var payment = await checkout.GetStatusAsync(UserId, reference, cancellationToken);
        if (payment is null) return NotFound();

        ViewData["Title"] = "Trạng thái thanh toán";
        ViewData["ActiveStudentNav"] = "invoices";
        return View(payment);
    }

    [AllowAnonymous, HttpGet("/payments/vnpay/ipn")]
    public async Task<IActionResult> Ipn(CancellationToken cancellationToken)
    {
        try
        {
            return Json(await checkout.ReceiveIpnAsync(Request.Query, cancellationToken));
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or RetryLimitExceededException)
        {
            logger.LogError(exception, "Không thể ghi nhận IPN VNPAY Sandbox.");
            return Json(VnpayIpnReply.Retry);
        }
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Không xác định được tài khoản sinh viên.");
}
