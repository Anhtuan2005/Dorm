using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<DormitoryDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { RoleNames.Admin, RoleNames.DormManager, RoleNames.Student })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var managerUser = await EnsureUserAsync(userManager, "manager@nepktx.local", "Nguyễn Thị Lan", "Demo@123", RoleNames.DormManager);
        await EnsureUserAsync(userManager, "admin@nepktx.local", "Phạm Đức Minh", "Demo@123", RoleNames.Admin);
        var studentUser = await EnsureUserAsync(userManager, "student@nepktx.local", "Nguyễn Minh An", "Demo@123", RoleNames.Student);

        if (!await db.Buildings.AnyAsync())
        {

        var buildingSpecs = new[]
        {
            new { Code = "A", Name = "Tòa A", Type = "Nam", Price = 1_250_000m },
            new { Code = "B", Name = "Tòa B", Type = "Nữ", Price = 1_150_000m },
            new { Code = "C", Name = "Tòa C", Type = "Nam", Price = 1_350_000m },
            new { Code = "D", Name = "Tòa D", Type = "Nữ", Price = 1_100_000m }
        };

        var buildings = new List<Building>();
        for (var buildingIndex = 0; buildingIndex < buildingSpecs.Length; buildingIndex++)
        {
            var spec = buildingSpecs[buildingIndex];
            var building = new Building
            {
                Code = spec.Code,
                Name = spec.Name,
                SiteName = "Cơ sở Thủ Đức",
                SortOrder = buildingIndex + 1
            };

            for (var floorNumber = 1; floorNumber <= 5; floorNumber++)
            {
                var floor = new Floor { Number = floorNumber };
                for (var roomNumber = 1; roomNumber <= 8; roomNumber++)
                {
                    floor.Rooms.Add(new Room
                    {
                        Code = $"{spec.Code}-{floorNumber}{roomNumber:00}",
                        Capacity = 8,
                        MonthlyPrice = spec.Price,
                        RoomType = spec.Type,
                        Amenities = "Wi-Fi|Điều hòa|WC khép kín|Bàn học|Tủ cá nhân"
                    });
                }
                building.Floors.Add(floor);
            }
            buildings.Add(building);
        }

        db.Buildings.AddRange(buildings);
        await db.SaveChangesAsync();

        var rooms = await db.Rooms
            .Include(x => x.Floor)
            .ThenInclude(x => x.Building)
            .OrderBy(x => x.Floor.Building.SortOrder)
            .ThenBy(x => x.Floor.Number)
            .ThenBy(x => x.Code)
            .ToListAsync();

        var stays = new List<Stay>();
        var holds = new List<RoomHold>();
        var incidents = new List<MaintenanceRequest>();
        var occupiedByRoom = rooms.ToDictionary(x => x.Id, _ => 0);
        var blockedByRoom = rooms.ToDictionary(x => x.Id, _ => 0);
        var studentSequence = 1;

        void AddOccupants(
            Room room,
            int count,
            IReadOnlyList<(string Code, string Name)>? preferredStudents = null)
        {
            for (var index = 0; index < count; index++)
            {
                var preferredStudent = preferredStudents is not null && index < preferredStudents.Count
                    ? preferredStudents[index]
                    : default;
                var name = preferredStudent.Name ?? $"Sinh viên {studentSequence:0000}";
                var student = new StudentProfile
                {
                    StudentCode = preferredStudent.Code ?? $"SV{studentSequence:000000}",
                    FullName = name,
                    Gender = room.RoomType
                };
                stays.Add(new Stay
                {
                    StudentProfile = student,
                    RoomId = room.Id,
                    StartedAt = DateTime.UtcNow.AddMonths(-(studentSequence % 16 + 1))
                });
                studentSequence++;
            }
            occupiedByRoom[room.Id] += count;
        }

        Room RoomByCode(string code) => rooms.Single(x => x.Code == code);

        var aOccupancy = rooms
            .Where(x => x.Floor.Building.Code == "A")
            .ToDictionary(x => x.Code, _ => 8);
        foreach (var value in new Dictionary<string, int>
        {
            ["A-301"] = 0, ["A-302"] = 6, ["A-304"] = 7, ["A-305"] = 6,
            ["A-101"] = 7, ["A-201"] = 7, ["A-401"] = 7
        }) aOccupancy[value.Key] = value.Value;

        foreach (var item in aOccupancy)
        {
            var students = item.Key == "A-302"
                ? new[]
                {
                    ("SV001234", "Trần Minh An"),
                    ("SV001567", "Lê Thị Bích Ngọc"),
                    ("SV001890", "Nguyễn Văn Hưng"),
                    ("SV002011", "Phạm Gia Hân"),
                    ("SV002106", "Nguyễn Minh An"),
                    ("SV002174", "Đỗ Thanh Trúc")
                }
                : null;
            AddOccupants(RoomByCode(item.Key), item.Value, students);
        }

        foreach (var code in new[] { "A-101", "A-201", "A-304", "A-401" })
        {
            holds.Add(new RoomHold
            {
                RoomId = RoomByCode(code).Id,
                HeldBeds = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(2)
            });
        }

        var a301 = RoomByCode("A-301");
        blockedByRoom[a301.Id] = 8;
        incidents.Add(new MaintenanceRequest
        {
            RoomId = a301.Id,
            Code = "SC-0284",
            Title = "Thấm trần phòng A-301",
            Priority = MaintenancePriority.Urgent,
            Status = MaintenanceStatus.New,
            BlockedBeds = 8,
            CreatedAt = DateTime.UtcNow.AddHours(-18),
            UpdatedAt = DateTime.UtcNow.AddHours(-18)
        });

        void SeedBuilding(string buildingCode, int targetOccupants, int targetHolds, int blockedBeds)
        {
            var buildingRooms = rooms.Where(x => x.Floor.Building.Code == buildingCode).ToList();
            var maintenanceRoom = buildingRooms[0];
            blockedByRoom[maintenanceRoom.Id] = blockedBeds;
            incidents.Add(new MaintenanceRequest
            {
                RoomId = maintenanceRoom.Id,
                Code = $"SC-{buildingCode}-001",
                Title = $"Bảo trì một phần phòng {maintenanceRoom.Code}",
                Priority = MaintenancePriority.High,
                Status = MaintenanceStatus.InProgress,
                BlockedBeds = blockedBeds,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddHours(-6)
            });

            var remainingOccupants = targetOccupants;
            foreach (var room in buildingRooms)
            {
                var availableCapacity = room.Capacity - blockedByRoom[room.Id];
                var count = Math.Min(remainingOccupants, availableCapacity);
                AddOccupants(room, count);
                remainingOccupants -= count;
            }

            var remainingHolds = targetHolds;
            foreach (var room in buildingRooms.Where(x => x.Id != maintenanceRoom.Id))
            {
                if (remainingHolds == 0) break;
                var freeBeds = room.Capacity - blockedByRoom[room.Id] - occupiedByRoom[room.Id];
                if (freeBeds <= 0) continue;
                var held = Math.Min(remainingHolds, freeBeds);
                holds.Add(new RoomHold
                {
                    RoomId = room.Id,
                    HeldBeds = held,
                    ExpiresAt = DateTime.UtcNow.AddDays(2)
                });
                remainingHolds -= held;
            }
        }

        SeedBuilding("B", 282, 8, 4);
        SeedBuilding("C", 258, 7, 4);
        SeedBuilding("D", 298, 5, 4);

        db.Stays.AddRange(stays);
        db.RoomHolds.AddRange(holds);
        db.MaintenanceRequests.AddRange(incidents);
        await db.SaveChangesAsync();
        }

        await SeedStudentPortalAsync(db, studentUser, managerUser);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string password,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);

        return user;
    }

    private static async Task SeedStudentPortalAsync(
        DormitoryDbContext db,
        ApplicationUser studentUser,
        ApplicationUser managerUser)
    {
        var profile = await db.StudentProfiles
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .SingleOrDefaultAsync(x => x.StudentCode == "SV002106")
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ sinh viên demo SV002106.");

        profile.UserId = studentUser.Id;
        profile.FullName = studentUser.FullName;
        profile.Faculty = "Công nghệ thông tin";
        profile.PhoneNumber = "090 482 1068";

        var currentStay = profile.Stays.SingleOrDefault()
            ?? throw new InvalidOperationException("Sinh viên demo chưa có lượt lưu trú hiện tại.");

        var september = await db.BillingPeriods.SingleOrDefaultAsync(x => x.Code == "2026-09");
        if (september is null)
        {
            september = new BillingPeriod
            {
                Code = "2026-09",
                Name = "Tháng 09/2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 9, 30),
                DueDate = new DateOnly(2026, 9, 15)
            };
            db.BillingPeriods.Add(september);
        }

        var august = await db.BillingPeriods.SingleOrDefaultAsync(x => x.Code == "2026-08");
        if (august is null)
        {
            august = new BillingPeriod
            {
                Code = "2026-08",
                Name = "Tháng 08/2026",
                StartDate = new DateOnly(2026, 8, 1),
                EndDate = new DateOnly(2026, 8, 31),
                DueDate = new DateOnly(2026, 8, 15)
            };
            db.BillingPeriods.Add(august);
        }

        await db.SaveChangesAsync();

        if (!await db.Invoices.AnyAsync(x => x.Code == "HD-2026-09-02106"))
        {
            db.Invoices.Add(new Invoice
            {
                Code = "HD-2026-09-02106",
                StudentProfileId = profile.Id,
                BillingPeriodId = september.Id,
                StayId = currentStay.Id,
                TotalAmount = 1_525_000m,
                PaidAmount = 0,
                Status = InvoiceStatus.Issued,
                IssuedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
                Lines =
                [
                    new InvoiceLine { ChargeType = "Room", Description = "Phí phòng A-302", Amount = 1_250_000m },
                    new InvoiceLine { ChargeType = "Electricity", Description = "Điện tháng 08 · 42 kWh", Amount = 168_000m },
                    new InvoiceLine { ChargeType = "Water", Description = "Nước tháng 08 · 4 m³", Amount = 72_000m },
                    new InvoiceLine { ChargeType = "Service", Description = "Phí dịch vụ", Amount = 35_000m }
                ]
            });
        }

        if (!await db.Invoices.AnyAsync(x => x.Code == "HD-2026-08-02106"))
        {
            db.Invoices.Add(new Invoice
            {
                Code = "HD-2026-08-02106",
                StudentProfileId = profile.Id,
                BillingPeriodId = august.Id,
                StayId = currentStay.Id,
                TotalAmount = 1_478_000m,
                PaidAmount = 1_478_000m,
                Status = InvoiceStatus.Paid,
                IssuedAt = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
                Lines =
                [
                    new InvoiceLine { ChargeType = "Room", Description = "Phí phòng A-302", Amount = 1_250_000m },
                    new InvoiceLine { ChargeType = "Electricity", Description = "Điện tháng 07 · 36 kWh", Amount = 144_000m },
                    new InvoiceLine { ChargeType = "Water", Description = "Nước tháng 07 · 3 m³", Amount = 54_000m },
                    new InvoiceLine { ChargeType = "Service", Description = "Phí dịch vụ", Amount = 30_000m }
                ]
            });
        }

        if (!await db.HousingRequests.AnyAsync(x => x.Code == "DK-2026-0042"))
        {
            db.HousingRequests.Add(new HousingRequest
            {
                Code = "DK-2026-0042",
                StudentProfileId = profile.Id,
                TargetRoomId = currentStay.RoomId,
                Type = HousingRequestType.Registration,
                Status = HousingRequestStatus.Approved,
                StudentReason = "Đăng ký lưu trú học kỳ I.",
                DecisionReason = "Hồ sơ đầy đủ, đã xác nhận nhận phòng.",
                SubmittedAt = new DateTime(2026, 2, 18, 2, 30, 0, DateTimeKind.Utc),
                ReviewedAt = new DateTime(2026, 2, 20, 7, 15, 0, DateTimeKind.Utc),
                ReviewedByUserId = managerUser.Id,
                Events =
                [
                    new HousingRequestEvent { Status = HousingRequestStatus.Submitted, Note = "Sinh viên đã gửi yêu cầu.", ActorUserId = studentUser.Id, CreatedAt = new DateTime(2026, 2, 18, 2, 30, 0, DateTimeKind.Utc) },
                    new HousingRequestEvent { Status = HousingRequestStatus.Approved, Note = "Hồ sơ đầy đủ, đã xác nhận nhận phòng.", ActorUserId = managerUser.Id, CreatedAt = new DateTime(2026, 2, 20, 7, 15, 0, DateTimeKind.Utc) }
                ]
            });
        }

        if (!await db.MaintenanceRequests.AnyAsync(x => x.Code == "SC-SV-0142"))
        {
            db.MaintenanceRequests.Add(new MaintenanceRequest
            {
                RoomId = currentStay.RoomId,
                ReporterStudentId = profile.Id,
                Code = "SC-SV-0142",
                Title = "Vòi nước khu rửa mặt bị rò",
                Category = "Điện nước",
                Description = "Vòi nước nhỏ giọt liên tục, nền khu vực rửa mặt thường xuyên bị ướt.",
                Priority = MaintenancePriority.Medium,
                Status = MaintenanceStatus.InProgress,
                BlockedBeds = 0,
                CreatedAt = new DateTime(2026, 9, 7, 2, 20, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 9, 9, 1, 30, 0, DateTimeKind.Utc),
                Updates =
                [
                    new MaintenanceUpdate { Status = MaintenanceStatus.New, Note = "Yêu cầu đã được tiếp nhận.", CreatedAt = new DateTime(2026, 9, 7, 2, 20, 0, DateTimeKind.Utc) },
                    new MaintenanceUpdate { Status = MaintenanceStatus.Assigned, Note = "Đã phân công tổ Điện nước.", CreatedAt = new DateTime(2026, 9, 8, 3, 0, 0, DateTimeKind.Utc) },
                    new MaintenanceUpdate { Status = MaintenanceStatus.InProgress, Note = "Dự kiến xử lý trước 17:00 ngày 10/09.", CreatedAt = new DateTime(2026, 9, 9, 1, 30, 0, DateTimeKind.Utc) }
                ]
            });
        }

        if (!await db.Notifications.AnyAsync(x => x.UserId == studentUser.Id))
        {
            db.Notifications.AddRange(
                new Notification
                {
                    UserId = studentUser.Id,
                    Type = "Invoice",
                    Title = "Hóa đơn tháng 09 đã phát hành",
                    Body = "Số tiền 1.525.000 đ, hạn thanh toán ngày 15/09/2026.",
                    TargetUrl = "/Student/Invoices",
                    CreatedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)
                },
                new Notification
                {
                    UserId = studentUser.Id,
                    Type = "Maintenance",
                    Title = "Yêu cầu SC-SV-0142 đang được xử lý",
                    Body = "Tổ Điện nước dự kiến hoàn thành trước 17:00 ngày 10/09.",
                    TargetUrl = "/Student/Repairs",
                    CreatedAt = new DateTime(2026, 9, 9, 1, 35, 0, DateTimeKind.Utc)
                });
        }

        await db.SaveChangesAsync();
        await SeedApplicationQueueAsync(db, managerUser);
    }

    private static async Task SeedApplicationQueueAsync(DormitoryDbContext db, ApplicationUser managerUser)
    {
        var studentProfiles = await db.StudentProfiles
            .Where(x => new[] { "SV001234", "SV001890", "SV002011", "SV002174", "SV000007", "SV000008" }.Contains(x.StudentCode))
            .ToDictionaryAsync(x => x.StudentCode);
        var targetRooms = await db.Rooms
            .Where(x => new[] { "C-503", "C-504", "C-505", "C-506", "C-507", "C-508" }.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code);

        var studentDetails = new Dictionary<string, (string Name, string Faculty, string Phone)>
        {
            ["SV001234"] = ("Trần Minh An", "Kỹ thuật máy tính", "090 123 4107"),
            ["SV001890"] = ("Nguyễn Văn Hưng", "Công nghệ thông tin", "090 189 0108"),
            ["SV002011"] = ("Phạm Gia Hân", "Điện – Điện tử", "090 201 1109"),
            ["SV002174"] = ("Đỗ Thanh Trúc", "Khoa học dữ liệu", "090 217 4110"),
            ["SV000007"] = ("Lê Hoàng Duy", "Cơ khí", "090 700 7111"),
            ["SV000008"] = ("Phan Minh Khoa", "Logistics", "090 800 8112")
        };
        foreach (var detail in studentDetails)
        {
            if (!studentProfiles.TryGetValue(detail.Key, out var student)) continue;
            student.FullName = detail.Value.Name;
            student.Faculty = detail.Value.Faculty;
            student.PhoneNumber = detail.Value.Phone;
        }

        var specs = new[]
        {
            new { Code = "DK-2026-0107", Student = "SV001234", Room = "C-503", Status = HousingRequestStatus.Submitted, Submitted = new DateTime(2026, 9, 6, 8, 15, 0, DateTimeKind.Utc), Reason = "Muốn chuyển gần khu tự học và phòng còn nhiều chỗ hơn.", Decision = (string?)null },
            new { Code = "DK-2026-0108", Student = "SV001890", Room = "C-504", Status = HousingRequestStatus.UnderReview, Submitted = new DateTime(2026, 9, 7, 6, 30, 0, DateTimeKind.Utc), Reason = "Cần không gian yên tĩnh để thực hiện đồ án cuối kỳ.", Decision = (string?)"Quản lý đã bắt đầu kiểm tra hồ sơ và sức chứa phòng." },
            new { Code = "DK-2026-0109", Student = "SV002011", Room = "C-505", Status = HousingRequestStatus.Submitted, Submitted = new DateTime(2026, 9, 8, 1, 45, 0, DateTimeKind.Utc), Reason = "Xin chuyển cùng tòa với nhóm học tập của khoa.", Decision = (string?)null },
            new { Code = "DK-2026-0110", Student = "SV002174", Room = "C-506", Status = HousingRequestStatus.Submitted, Submitted = new DateTime(2026, 9, 8, 17, 20, 0, DateTimeKind.Utc), Reason = "Phòng mong muốn phù hợp hơn với lịch học buổi tối.", Decision = (string?)null },
            new { Code = "DK-2026-0111", Student = "SV000007", Room = "C-507", Status = HousingRequestStatus.Submitted, Submitted = new DateTime(2026, 9, 9, 20, 10, 0, DateTimeKind.Utc), Reason = "Muốn chuyển sang tòa gần khu thực hành cơ khí.", Decision = (string?)null },
            new { Code = "DK-2026-0112", Student = "SV000008", Room = "C-508", Status = HousingRequestStatus.NeedsInformation, Submitted = new DateTime(2026, 9, 9, 20, 45, 0, DateTimeKind.Utc), Reason = "Đăng ký chuyển phòng theo nhu cầu cá nhân.", Decision = (string?)"Vui lòng bổ sung lý do cụ thể và thời gian mong muốn chuyển phòng." }
        };

        foreach (var spec in specs)
        {
            if (await db.HousingRequests.AnyAsync(x => x.Code == spec.Code)) continue;
            if (!studentProfiles.TryGetValue(spec.Student, out var student) || !targetRooms.TryGetValue(spec.Room, out var room)) continue;

            var request = new HousingRequest
            {
                Code = spec.Code,
                StudentProfileId = student.Id,
                TargetRoomId = room.Id,
                Type = HousingRequestType.Transfer,
                Status = spec.Status,
                StudentReason = spec.Reason,
                DecisionReason = spec.Decision,
                SubmittedAt = spec.Submitted,
                ReviewedAt = spec.Status == HousingRequestStatus.Submitted ? null : spec.Submitted.AddHours(3),
                ReviewedByUserId = spec.Status == HousingRequestStatus.Submitted ? null : managerUser.Id,
                Events =
                [
                    new HousingRequestEvent { Status = HousingRequestStatus.Submitted, Note = "Sinh viên đã gửi yêu cầu chuyển phòng.", CreatedAt = spec.Submitted }
                ]
            };
            if (spec.Status != HousingRequestStatus.Submitted)
            {
                request.Events.Add(new HousingRequestEvent
                {
                    Status = spec.Status,
                    Note = spec.Decision!,
                    ActorUserId = managerUser.Id,
                    CreatedAt = spec.Submitted.AddHours(3)
                });
            }
            db.HousingRequests.Add(request);
        }

        await db.SaveChangesAsync();
    }
}
