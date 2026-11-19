using DormitoryManagementSystem.Models;
namespace DormitoryManagementSystem.ViewModels;
public sealed record MeterRow(int RoomId, string Code, decimal PowerStart, decimal PowerEnd, decimal PowerRate, decimal WaterStart, decimal WaterEnd, decimal WaterRate, byte[] Version);
public sealed record MeterPage(IReadOnlyList<BillingPeriod> Periods, BillingPeriod? Selected, bool Issued, IReadOnlyList<string> Rooms, IReadOnlyList<MeterRow> Readings, MeterRow? Edit);
