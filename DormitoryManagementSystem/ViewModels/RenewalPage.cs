using DormitoryManagementSystem.Models;
namespace DormitoryManagementSystem.ViewModels;

public sealed record RenewalRow(int Id, string Student, string Room, string Term, DateOnly Ends, bool? Approved, string? Note);
public sealed record ExpiringStay(string Student, string Room, DateOnly? Ends);
public sealed class RenewalPage
{
    public int? StayId { get; init; }
    public DateOnly? CurrentEnd { get; init; }
    public IReadOnlyList<ResidenceTerm> Terms { get; init; } = [];
    public IReadOnlyList<RenewalRow> Requests { get; init; } = [];
    public IReadOnlyList<ExpiringStay> Expiring { get; init; } = [];
    public int Page { get; init; } = 1;
    public bool More { get; init; }
}
