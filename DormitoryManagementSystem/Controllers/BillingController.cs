using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class BillingController(
    IBillingLedgerService billingLedger,
    UserManager<ApplicationUser> userManager) : Controller
{
    private const string PaymentDraftKey = "BillingPaymentDraft";

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] BillingQueueQuery query,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Thu phí";
        ViewData["ActiveNav"] = "billing";
        if (TempData[PaymentDraftKey] is string serializedDraft)
            ViewData[PaymentDraftKey] = JsonSerializer.Deserialize<RecordPaymentInput>(serializedDraft);
        return View(await billingLedger.GetQueueAsync(query, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueInvoices(IssueInvoicesInput input, CancellationToken cancellationToken)
    {
        var result = ModelState.IsValid
            ? await billingLedger.IssueInvoicesAsync(input, cancellationToken)
            : new InvoiceIssueResult(false, ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage
                ?? "Dữ liệu kỳ thu chưa hợp lệ.");
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index), new { PeriodId = result.PeriodId > 0 ? result.PeriodId : (int?)null });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(
        RecordPaymentInput input,
        BillingReturnContext context,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PreserveDraft(input);
            TempData["Error"] = ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage
                ?? "Dữ liệu khoản thu chưa hợp lệ.";
            return RedirectToQueue(input.InvoiceId, context);
        }

        var result = await billingLedger.RecordPaymentAsync(input, ManagerId, cancellationToken);
        if (!result.Success) PreserveDraft(input);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToQueue(result.InvoiceId == 0 ? input.InvoiceId : result.InvoiceId, context);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReversePayment(
        ReversePaymentInput input,
        BillingReturnContext context,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage
                ?? "Dữ liệu hoàn tác chưa hợp lệ.";
            return RedirectToQueue(0, context);
        }

        var result = await billingLedger.ReversePaymentAsync(input, ManagerId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToQueue(result.InvoiceId, context);
    }

    private string ManagerId => userManager.GetUserId(User)
        ?? throw new InvalidOperationException("Không xác định được tài khoản quản lý.");

    private void PreserveDraft(RecordPaymentInput input) =>
        TempData[PaymentDraftKey] = JsonSerializer.Serialize(input);

    private IActionResult RedirectToQueue(long invoiceId, BillingReturnContext context) => RedirectToAction(nameof(Index), new
    {
        context.Search,
        context.PeriodId,
        context.Status,
        context.Building,
        context.MinBalance,
        context.MaxBalance,
        Page = Math.Max(1, context.Page),
        SelectedId = invoiceId > 0 ? (long?)invoiceId : null
    });
}
