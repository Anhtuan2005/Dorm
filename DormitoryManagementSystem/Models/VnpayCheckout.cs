using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models;

public enum VnpayCheckoutState { Pending, Settled, Failed, NeedsReview }

public sealed class VnpayCheckout
{
    internal const decimal MinimumAmount = 5_000m;

    [MaxLength(32)] public string Reference { get; set; } = string.Empty;
    [MaxLength(8)] public string MerchantCode { get; set; } = string.Empty;
    public long InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public VnpayCheckoutState State { get; set; }
    [MaxLength(15)] public string? ProviderTransactionNumber { get; set; }
    [MaxLength(2)] public string? ResponseCode { get; set; }
    [MaxLength(2)] public string? ProviderState { get; set; }

    internal static bool SupportsAmount(decimal amount) =>
        amount is >= MinimumAmount and <= 9_999_999_999m && decimal.Truncate(amount) == amount;
}
