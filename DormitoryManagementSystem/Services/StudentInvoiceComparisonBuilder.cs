using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

internal static class StudentInvoiceComparisonBuilder
{
    public static StudentInvoiceComparison Compare(BillingInvoiceSnapshot current, BillingInvoiceSnapshot previous)
    {
        var currentCharges = SumCharges(current.Lines);
        var previousCharges = SumCharges(previous.Lines);
        var changes = currentCharges.Keys.Union(previousCharges.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(label => new StudentChargeChange(label, previousCharges.GetValueOrDefault(label), currentCharges.GetValueOrDefault(label)))
            .Where(change => change.Difference != 0)
            .OrderByDescending(change => Math.Abs(change.Difference))
            .ThenBy(change => change.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new(previous.Id, previous.PeriodName, current.Summary.Total - previous.Summary.Total, changes);
    }

    private static Dictionary<string, decimal> SumCharges(IEnumerable<BillingLedgerLine> lines) =>
        lines.GroupBy(ChargeLabel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Amount), StringComparer.OrdinalIgnoreCase);

    private static string ChargeLabel(BillingLedgerLine line)
    {
        var type = line.ChargeType.Trim();
        return type.ToUpperInvariant() switch
        {
            "ROOM" or "TIỀN PHÒNG" or "PHÍ PHÒNG" => "Tiền phòng",
            "ELECTRICITY" or "ĐIỆN" or "TIỀN ĐIỆN" => "Tiền điện",
            "WATER" or "NƯỚC" or "TIỀN NƯỚC" => "Tiền nước",
            "SERVICE" or "DỊCH VỤ" or "PHÍ DỊCH VỤ" => "Phí dịch vụ",
            "" => line.Description.Trim(),
            _ => type
        };
    }
}
