using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Services;

public static class MeterAllocation
{
    public static ILookup<int, InvoiceLine> Calculate(IEnumerable<Stay> stays, IReadOnlyDictionary<int, RoomMeterReading> readings,
        DateTime startUtc, DateTime endUtc)
    {
        var lines = new List<(int Student, InvoiceLine Line)>();
        foreach (var room in stays.GroupBy(x => x.RoomId))
        {
            var reading = readings[room.Key];
            var weights = room.GroupBy(x => x.StudentProfileId).Select(group => new {
                Student = group.Key,
                Ticks = group.Sum(stay => (decimal)(Math.Min(stay.EndedAt?.Ticks ?? endUtc.Ticks, endUtc.Ticks)
                    - Math.Max(stay.StartedAt.Ticks, startUtc.Ticks)))
            }).Where(x => x.Ticks > 0).ToArray();
            if (weights.Length == 0) continue;
            var totalWeight = weights.Sum(x => x.Ticks);
            Add("Điện", "kWh", reading.PowerStart, reading.PowerEnd, reading.PowerRate);
            Add("Nước", "m³", reading.WaterStart, reading.WaterEnd, reading.WaterRate);

            void Add(string type, string units, decimal initial, decimal final, decimal rate)
            {
                var total = decimal.Round((final - initial) * rate, 0, MidpointRounding.AwayFromZero);
                if (total == 0) return;
                var parts = weights.Select(x => new { x.Student, x.Ticks, Exact = total * x.Ticks / totalWeight })
                    .OrderByDescending(x => x.Exact - decimal.Floor(x.Exact)).ThenBy(x => x.Student).ToArray();
                var remainder = (int)(total - parts.Sum(x => decimal.Floor(x.Exact)));
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var amount = decimal.Floor(part.Exact) + (i < remainder ? 1 : 0);
                    var code = room.First().Room.Code;
                    lines.Add((part.Student, new InvoiceLine {
                        ChargeType = type, Amount = amount,
                        Description = $"{type} {code}: ({final:0.##} − {initial:0.##}) {units} × {rate:0.##} đ · phần ở {part.Ticks / totalWeight:P2}"
                    }));
                }
            }
        }
        return lines.ToLookup(x => x.Student, x => x.Line);
    }
}
