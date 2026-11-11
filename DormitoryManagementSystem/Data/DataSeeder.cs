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
        var configuration = services.GetRequiredService<IConfiguration>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var demoPassword = configuration["DemoData:Password"];
        if (string.IsNullOrWhiteSpace(demoPassword))
            throw new InvalidOperationException("DemoData:Password phải được cấu hình bằng User Secrets khi bật demo seed.");

        foreach (var role in new[] { RoleNames.Admin, RoleNames.DormManager, RoleNames.Student })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var managerUser = await EnsureUserAsync(userManager, "manager@nepktx.local", "Nguyễn Thị Lan", demoPassword, RoleNames.DormManager);
        await EnsureUserAsync(userManager, "admin@nepktx.local", "Phạm Đức Minh", demoPassword, RoleNames.Admin);
        var studentUser = await EnsureUserAsync(userManager, "student@nepktx.local", "Nguyễn Minh An", demoPassword, RoleNames.Student);

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
        await SeedMaintenanceWorkflowDemoAsync(db, managerUser);
        await SeedBillingDemoAsync(db, managerUser);
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
                Status = InvoiceStatus.Issued,
                IssuedAt = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
                Lines =
                [
                    new InvoiceLine { ChargeType = "Room", Description = "Phí phòng A-302", Amount = 1_250_000m },
                    new InvoiceLine { ChargeType = "Electricity", Description = "Điện tháng 07 · 36 kWh", Amount = 144_000m },
                    new InvoiceLine { ChargeType = "Water", Description = "Nước tháng 07 · 3 m³", Amount = 54_000m },
                    new InvoiceLine { ChargeType = "Service", Description = "Phí dịch vụ", Amount = 30_000m }
                ],
                Transactions =
                [
                    new PaymentTransaction
                    {
                        Code = "THU-DEMO-202608-02106",
                        Amount = 1_478_000m,
                        Method = PaymentMethod.BankTransfer,
                        ReceivedAt = new DateTime(2026, 8, 10, 3, 15, 0, DateTimeKind.Utc),
                        RecordedAt = new DateTime(2026, 8, 10, 3, 17, 0, DateTimeKind.Utc),
                        RecordedByUserId = managerUser.Id,
                        ExternalReference = "VCB-20260810-02106",
                        IdempotencyKey = "seed-payment-2026-08-02106",
                        Note = "Khoản thu demo đã đối soát."
                    }
                ]
            });
        }

        if (!await db.HousingRequests.AnyAsync(x => x.Code == "DK-2026-0042"))
        {
            var initialRequest = new HousingRequest
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
            };
            db.HousingRequests.Add(initialRequest);
            currentStay.HousingRequest = initialRequest;
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
        var currentStays = await db.Stays
            .Where(x => x.EndedAt == null && studentProfiles.Values.Select(student => student.Id).Contains(x.StudentProfileId))
            .ToDictionaryAsync(x => x.StudentProfileId);

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
                SourceStayId = currentStays.GetValueOrDefault(student.Id)?.Id,
                Type = HousingRequestType.Transfer,
                Status = spec.Status,
                StudentReason = spec.Reason,
                DecisionReason = spec.Decision,
                SubmittedAt = spec.Submitted,
                ActionRequiredAt = spec.Status == HousingRequestStatus.NeedsInformation
                    ? spec.Submitted.AddHours(3)
                    : spec.Submitted,
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

    private static async Task SeedMaintenanceWorkflowDemoAsync(DormitoryDbContext db, ApplicationUser managerUser)
    {
        var now = DateTime.UtcNow;
        var existing = await db.MaintenanceRequests
            .Where(x => x.Code == "SC-0284" || x.Code == "SC-B-001" || x.Code == "SC-C-001" || x.Code == "SC-D-001")
            .ToListAsync();
        foreach (var request in existing)
        {
            request.Category = string.IsNullOrWhiteSpace(request.Category) ? "Cơ sở vật chất" : request.Category;
            request.Description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Kiểm tra hiện trường {request.RoomId}; khu vực có dấu hiệu hư hỏng và cần cô lập phần giường bị ảnh hưởng."
                : request.Description;
            if (request.Status != MaintenanceStatus.New)
                request.AssignedTeam ??= "Tổ Hạ tầng";
            if (request.UpdatedAt == default)
                request.UpdatedAt = request.CreatedAt;
        }

        var roomCodes = new[] { "A-408", "C-205", "B-502", "C-108" };
        var rooms = await db.Rooms.Where(x => roomCodes.Contains(x.Code)).ToDictionaryAsync(x => x.Code);
        var demo = new[]
        {
            new MaintenanceRequest
            {
                RoomId = rooms["A-408"].Id, Code = "SC-0292", Title = "Mất điện toàn phòng", Category = "Điện nước",
                Description = "Toàn bộ ổ cắm và đèn trong phòng mất điện, aptomat hành lang vẫn đang bật.",
                Priority = MaintenancePriority.Urgent, Status = MaintenanceStatus.New, BlockedBeds = 0,
                CreatedAt = now.AddMinutes(-20), UpdatedAt = now.AddMinutes(-20)
            },
            new MaintenanceRequest
            {
                RoomId = rooms["C-205"].Id, Code = "SC-0289", Title = "Rò rỉ nước dưới lavabo", Category = "Điện nước",
                Description = "Khớp nối ống cấp dưới lavabo rò liên tục, nước lan ra lối đi cạnh giường số 1.",
                Priority = MaintenancePriority.High, Status = MaintenanceStatus.InProgress, AssignedTeam = "Tổ Điện nước", BlockedBeds = 0,
                CreatedAt = now.AddHours(-9), UpdatedAt = now.AddMinutes(-55),
                Updates = [new MaintenanceUpdate { Status = MaintenanceStatus.InProgress, Note = "Đã khóa van nhánh và bắt đầu thay khớp nối.", ActorUserId = managerUser.Id, CreatedAt = now.AddMinutes(-55) }]
            },
            new MaintenanceRequest
            {
                RoomId = rooms["B-502"].Id, Code = "SC-0286", Title = "Điều hòa không làm mát", Category = "Thiết bị phòng",
                Description = "Máy vẫn chạy quạt nhưng không giảm nhiệt độ sau khoảng 30 phút hoạt động.",
                Priority = MaintenancePriority.Medium, Status = MaintenanceStatus.WaitingParts, AssignedTeam = "Tổ Thiết bị", BlockedBeds = 0,
                CreatedAt = now.AddDays(-1.5), UpdatedAt = now.AddHours(-2),
                Updates = [new MaintenanceUpdate { Status = MaintenanceStatus.WaitingParts, Note = "Đã xác định hỏng tụ đề, đang chờ vật tư thay thế.", ActorUserId = managerUser.Id, CreatedAt = now.AddHours(-2) }]
            },
            new MaintenanceRequest
            {
                RoomId = rooms["C-108"].Id, Code = "SC-0279", Title = "Đèn hành lang chập chờn", Category = "Khu vực chung",
                Description = "Đèn trước cụm phòng C-107 đến C-109 chập chờn vào buổi tối.",
                Priority = MaintenancePriority.Low, Status = MaintenanceStatus.Assigned, AssignedTeam = "Tổ Điện nước", BlockedBeds = 0,
                CreatedAt = now.AddDays(-3), UpdatedAt = now.AddHours(-10),
                Updates = [new MaintenanceUpdate { Status = MaintenanceStatus.Assigned, Note = "Đã lên lịch kiểm tra vào 08:30 ngày mai.", ActorUserId = managerUser.Id, CreatedAt = now.AddHours(-10) }]
            }
        };

        foreach (var request in demo)
            if (!await db.MaintenanceRequests.AnyAsync(x => x.Code == request.Code))
                db.MaintenanceRequests.Add(request);

        var futureDemoRows = await db.MaintenanceRequests
            .Where(x => (x.Code == "SC-0292" || x.Code == "SC-0289" || x.Code == "SC-0286" || x.Code == "SC-0279") &&
                        (x.CreatedAt > now || x.UpdatedAt > now))
            .ToListAsync();
        foreach (var request in futureDemoRows)
        {
            if (request.CreatedAt > now) request.CreatedAt = now.AddMinutes(-20);
            if (request.UpdatedAt > now) request.UpdatedAt = now.AddMinutes(-10);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedBillingDemoAsync(DormitoryDbContext db, ApplicationUser managerUser)
    {
        if (await db.Invoices.AnyAsync(x => x.Code.StartsWith("HD-MGR-"))) return;

        var periods = await db.BillingPeriods.ToDictionaryAsync(x => x.Code);
        var stays = await db.Stays.AsNoTracking()
            .Include(x => x.StudentProfile)
            .Include(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
            .Where(x => x.EndedAt == null && x.StudentProfile.StudentCode != "SV002106")
            .OrderBy(x => x.Room.Floor.Building.SortOrder)
            .ThenBy(x => x.StudentProfile.StudentCode)
            .ToListAsync();
        var selected = stays
            .GroupBy(x => x.Room.Floor.Building.Code)
            .SelectMany(group => group.Take(2))
            .Take(8)
            .ToList();

        for (var index = 0; index < selected.Count; index++)
        {
            var stay = selected[index];
            var period = index < 4 ? periods["2026-08"] : periods["2026-09"];
            var electricity = 132_000m + index * 9_000m;
            var water = 54_000m + index * 3_000m;
            var service = 35_000m;
            var total = stay.Room.MonthlyPrice + electricity + water + service;
            var collected = index switch
            {
                1 => 650_000m,
                2 => total,
                5 => 500_000m,
                _ => 0
            };
            var invoice = new Invoice
            {
                Code = $"HD-MGR-{index + 1:0000}",
                StudentProfileId = stay.StudentProfileId,
                BillingPeriodId = period.Id,
                StayId = stay.Id,
                TotalAmount = total,
                Status = InvoiceStatus.Issued,
                IssuedAt = period.StartDate.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc),
                Lines =
                [
                    new InvoiceLine { ChargeType = "Room", Description = $"Phí phòng {stay.Room.Code}", Amount = stay.Room.MonthlyPrice },
                    new InvoiceLine { ChargeType = "Electricity", Description = $"Điện kỳ {period.Name}", Amount = electricity },
                    new InvoiceLine { ChargeType = "Water", Description = $"Nước kỳ {period.Name}", Amount = water },
                    new InvoiceLine { ChargeType = "Service", Description = "Phí dịch vụ", Amount = service }
                ]
            };
            if (collected > 0)
            {
                invoice.Transactions.Add(new PaymentTransaction
                {
                    Code = $"THU-DEMO-MGR-{index + 1:0000}",
                    Amount = collected,
                    Method = index == 1 ? PaymentMethod.Cash : PaymentMethod.BankTransfer,
                    ReceivedAt = period.StartDate.AddDays(8).ToDateTime(new TimeOnly(9, 15), DateTimeKind.Utc),
                    RecordedAt = period.StartDate.AddDays(8).ToDateTime(new TimeOnly(9, 17), DateTimeKind.Utc),
                    RecordedByUserId = managerUser.Id,
                    ExternalReference = index == 1 ? null : $"DEMO-{period.Code}-{index + 1:0000}",
                    IdempotencyKey = $"seed-manager-payment-{index + 1:0000}",
                    Note = "Dữ liệu minh họa nghiệp vụ thu phí."
                });
            }
            db.Invoices.Add(invoice);
        }

        await db.SaveChangesAsync();
    }
}
