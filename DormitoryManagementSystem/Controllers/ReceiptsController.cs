using System.Security.Claims;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager},{RoleNames.Student}")]
public sealed class ReceiptsController(IBillingLedgerService billingLedger) : Controller
{
    [HttpGet("Receipts/{id:long}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var isStaff = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.DormManager);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var receipt = await billingLedger.GetReceiptAsync(id, isStaff ? null : userId, cancellationToken);
        if (receipt is null) return NotFound();
        ViewData["Title"] = $"Biên nhận {receipt.Code}";
        ViewData["ReceiptBackUrl"] = isStaff
            ? Url.Action("Index", "Billing", new { selectedId = receipt.InvoiceId, search = receipt.InvoiceCode })
            : Url.Action("Invoices", "Student", new { receiptInvoice = receipt.InvoiceId }, null, null, $"invoice-{receipt.InvoiceId}");
        return View(receipt);
    }
}
