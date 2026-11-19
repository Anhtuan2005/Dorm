using System.Data;
using System.Data.Common;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DormitoryManagementSystem.Services;

public sealed partial class BillingLedgerService(
    DormitoryDbContext context,
    TimeProvider clock,
    RealtimeUpdates? realtime = null) : IBillingLedgerService
{
    private readonly DormitoryDbContext db = context;
    private readonly TimeProvider timeProvider = clock;
    private readonly RealtimeUpdates? live = realtime;
    private const string VnpayIdempotencyPrefix = "vnpay-sandbox:";
    private const int PageSize = 20;
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    public async Task<BillingQueueViewModel> GetQueueAsync(
        BillingQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Page = Math.Max(1, query.Page);
        var businessDate = GetBusinessDate();
        var tomorrow = businessDate.AddDays(1);
        var dueSoonEnd = businessDate.AddDays(7);
        var projections = InvoiceProjections();

        var monthStart = new DateTime(businessDate.Year, businessDate.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var nextMonth = monthStart.AddMonths(1);
        var monthStartUtc = TimeZoneInfo.ConvertTimeToUtc(monthStart, BusinessTimeZone);
        var nextMonthUtc = TimeZoneInfo.ConvertTimeToUtc(nextMonth, BusinessTimeZone);

        var summaryRows = await projections
            .Select(x => new { x.TotalAmount, x.CollectedAmount, x.DueDate, x.IsCancelled })
            .ToListAsync(cancellationToken);
        var outstandingRows = summaryRows.Where(x => !x.IsCancelled && x.TotalAmount > x.CollectedAmount).ToList();
        var overdueRows = outstandingRows.Where(x => x.DueDate < businessDate).ToList();
        var outstandingCount = outstandingRows.Count;
        var outstandingAmount = outstandingRows.Sum(x => x.TotalAmount - x.CollectedAmount);
        var overdueCount = overdueRows.Count;
        var overdueAmount = overdueRows.Sum(x => x.TotalAmount - x.CollectedAmount);
        var dueSoonCount = outstandingRows.Count(x => x.DueDate >= tomorrow && x.DueDate <= dueSoonEnd);
        var collectedThisMonth = await db.PaymentTransactions.AsNoTracking()
            .Where(x => x.RecordedAt >= monthStartUtc && x.RecordedAt < nextMonthUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        var filtered = ApplyFilters(projections, query, businessDate);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / PageSize));
        query.Page = Math.Min(query.Page, totalPages);

        var rows = await filtered
            .OrderBy(x => x.IsCancelled ? 4 : x.TotalAmount <= x.CollectedAmount ? 3 : x.DueDate < businessDate ? 0 : x.CollectedAmount > 0 ? 1 : 2)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.IssuedAt)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var selectedId = query.SelectedId ?? rows.FirstOrDefault()?.Id;
        query.SelectedId = selectedId;
        var items = rows.Select(x => ToQueueItem(x, selectedId, businessDate)).ToList();
        var selected = selectedId.HasValue
            ? await LoadDetailAsync(selectedId.Value, businessDate, cancellationToken)
            : null;

        var periods = await db.BillingPeriods.AsNoTracking()
            .OrderByDescending(x => x.StartDate)
            .Select(x => new BillingFilterOption(x.Id.ToString(), x.Name))
            .ToListAsync(cancellationToken);
        var buildings = await db.Buildings.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new BillingFilterOption(x.Code, x.Name))
            .ToListAsync(cancellationToken);

        return new BillingQueueViewModel
        {
            Query = query,
            Summary = new BillingQueueSummaryViewModel(
                outstandingCount,
                outstandingAmount,
                overdueCount,
                overdueAmount,
                dueSoonCount,
                collectedThisMonth),
            Items = items,
            Periods = periods,
            Buildings = buildings,
            DefaultReceivedAtLocal = ToBusinessLocal(timeProvider.GetUtcNow().UtcDateTime),
            SelectedInvoice = selected,
            TotalItems = totalItems,
            PageSize = PageSize,
            SuggestedPeriodStart = new DateOnly(businessDate.Year, businessDate.Month, 1).AddMonths(1),
            SuggestedPeriodEnd = new DateOnly(businessDate.Year, businessDate.Month, 1).AddMonths(2).AddDays(-1),
            SuggestedDueDate = new DateOnly(businessDate.Year, businessDate.Month, 10).AddMonths(1)
        };
    }

    public async Task<InvoiceIssueResult> IssueInvoicesAsync(
        IssueInvoicesInput input,
        CancellationToken cancellationToken = default)
    {
        var code = input.PeriodCode.Trim().ToUpperInvariant();
        var name = input.PeriodName.Trim();
        if (input.StartDate is null || input.EndDate is null || input.DueDate is null)
            return new(false, "Vui lòng nhập đủ thời gian kỳ thu.");
        if (input.EndDate == DateOnly.MaxValue) return new(false, "Ngày cuối kỳ vượt phạm vi hỗ trợ.");
        if (input.StartDate > input.EndDate)
            return new(false, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (input.DueDate < input.StartDate)
            return new(false, "Hạn thanh toán không được trước ngày bắt đầu kỳ thu.");
        if (input.ElectricityAmount < 0 || input.WaterAmount < 0 || input.ServiceAmount < 0)
            return new(false, "Các khoản phí bổ sung không được âm.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<InvoiceIssueResult>(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var period = await db.BillingPeriods.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
                if (period is null)
                {
                    period = new BillingPeriod
                    {
                        Code = code,
                        Name = name,
                        StartDate = input.StartDate.Value,
                        EndDate = input.EndDate.Value,
                        DueDate = input.DueDate.Value
                    };
                    db.BillingPeriods.Add(period);
                    await db.SaveChangesAsync(cancellationToken);
                }
                else if (period.StartDate != input.StartDate || period.EndDate != input.EndDate || period.DueDate != input.DueDate)
                {
                    return new(false, $"Mã kỳ {code} đã tồn tại với mốc thời gian khác.", period.Id);
                }

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(input.StartDate.Value.ToDateTime(TimeOnly.MinValue), BusinessTimeZone);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(input.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), BusinessTimeZone);
                var staysQuery = db.Stays
                    .Include(x => x.StudentProfile)
                    .Include(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
                    .Where(x => x.StartedAt < endUtc && (x.EndedAt == null || x.EndedAt > startUtc)
                        && (x.EndedAt == null || x.EndedAt > x.StartedAt));

                var stays = await staysQuery.OrderBy(x => x.StudentProfile.StudentCode).ToListAsync(cancellationToken);
                var existingStudentIds = (await db.Invoices
                    .Where(x => x.BillingPeriodId == period.Id)
                    .Select(x => x.StudentProfileId)
                    .ToListAsync(cancellationToken)).ToHashSet();
                var now = timeProvider.GetUtcNow().UtcDateTime;
                var issued = 0;
                var skipped = 0;
                var notifiedUsers = new HashSet<string>(StringComparer.Ordinal);

                ILookup<int, InvoiceLine>? meterFees = null;
                if (period.Metered)
                {
                    if (now < endUtc) return new(false, "Chờ kết thúc kỳ để chốt thời gian ở và chỉ số thực tế.", period.Id);
                    var readings = await db.RoomMeterReadings.AsNoTracking().Where(x => x.BillingPeriodId == period.Id)
                        .ToDictionaryAsync(x => x.RoomId, cancellationToken);
                    var missing = stays.Where(x => !readings.ContainsKey(x.RoomId)).Select(x => x.Room.Code).Distinct().Order().ToList();
                    if (missing.Count != 0)
                        return new(false, $"Cần nhập điện nước cho {missing.Count} phòng trước khi phát hành: {string.Join(", ", missing.Take(8))}.", period.Id);
                    meterFees = MeterAllocation.Calculate(stays, readings, startUtc, endUtc);
                }

                foreach (var stay in stays.GroupBy(x => x.StudentProfileId)
                    .Select(group => group.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).First()))
                {
                    if (!string.IsNullOrWhiteSpace(input.BuildingCode) &&
                        !string.Equals(stay.Room.Floor.Building.Code, input.BuildingCode.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!existingStudentIds.Add(stay.StudentProfileId))
                    {
                        skipped++;
                        continue;
                    }

                    var lines = new List<InvoiceLine>
                    {
                        new() { ChargeType = "Tiền phòng", Description = $"Phí lưu trú {stay.Room.Code} — {period.Name}", Amount = stay.Room.MonthlyPrice }
                    };
                    if (meterFees != null) lines.AddRange(meterFees[stay.StudentProfileId]);
                    else
                    {
                        AddCharge(lines, "Điện", "Điện sinh hoạt", input.ElectricityAmount);
                        AddCharge(lines, "Nước", "Nước sinh hoạt", input.WaterAmount);
                    }
                    AddCharge(lines, "Dịch vụ", "Phí dịch vụ KTX", input.ServiceAmount);
                    var invoice = new Invoice
                    {
                        Code = BuildInvoiceCode(period.Id, stay.StudentProfile.StudentCode),
                        StudentProfileId = stay.StudentProfileId,
                        BillingPeriodId = period.Id,
                        StayId = stay.Id,
                        TotalAmount = lines.Sum(x => x.Amount),
                        Status = InvoiceStatus.Issued,
                        IssuedAt = now,
                        LedgerUpdatedAt = now,
                        Lines = lines
                    };
                    db.Invoices.Add(invoice);
                    if (!string.IsNullOrWhiteSpace(stay.StudentProfile.UserId))
                    {
                        notifiedUsers.Add(stay.StudentProfile.UserId);
                        db.Notifications.Add(new Notification
                        {
                            UserId = stay.StudentProfile.UserId,
                            Type = "InvoiceIssued",
                            Title = $"Hóa đơn {period.Name}",
                            Body = $"Đã phát hành {invoice.TotalAmount:N0} đ, hạn thanh toán {period.DueDate:dd/MM/yyyy}.",
                            TargetUrl = "/Student/Invoices",
                            CreatedAt = now
                        });
                    }
                    issued++;
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                if (issued != 0 && live is not null) await live.NotifyOperationsAsync(notifiedUsers.ToArray());
                var scope = string.IsNullOrWhiteSpace(input.BuildingCode) ? "toàn KTX" : $"tòa {input.BuildingCode.Trim().ToUpperInvariant()}";
                return new(true, $"Đã phát hành {issued} hóa đơn cho {scope}; bỏ qua {skipped} hóa đơn đã tồn tại.", period.Id, issued, skipped);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, "Dữ liệu kỳ thu vừa được thay đổi. Hãy tải lại và thử lại.");
            }
        });
    }

    public async Task<IReadOnlyList<BillingInvoiceSnapshot>> GetStudentInvoicesAsync(
        int studentProfileId,
        CancellationToken cancellationToken = default)
    {
        var invoices = await db.Invoices.AsNoTracking()
            .Include(x => x.BillingPeriod)
            .Include(x => x.Lines)
            .Include(x => x.Transactions).ThenInclude(x => x.RecordedByUser)
            .Include(x => x.Transactions).ThenInclude(x => x.ReversalTransaction)
            .Where(x => x.StudentProfileId == studentProfileId)
            .OrderByDescending(x => x.BillingPeriod.StartDate)
            .ThenByDescending(x => x.BillingPeriod.EndDate)
            .ThenByDescending(x => x.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var businessDate = GetBusinessDate();
        return invoices.Select(x => ToSnapshot(x, businessDate)).ToList();
    }

    public async Task<decimal> GetOutstandingBalanceAsync(
        int studentProfileId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Invoices.AsNoTracking()
            .Where(x => x.StudentProfileId == studentProfileId && x.Status != InvoiceStatus.Cancelled)
            .Select(x => new
            {
                x.TotalAmount,
                CollectedAmount = x.Transactions.Sum(transaction => (decimal?)transaction.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        return rows.Sum(x => Math.Max(0, x.TotalAmount - x.CollectedAmount));
    }

    public async Task<BillingOperationResult> RecordPaymentAsync(
        RecordPaymentInput input,
        string managerUserId,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = input.IdempotencyKey.Trim();
        if (idempotencyKey.StartsWith(VnpayIdempotencyPrefix, StringComparison.OrdinalIgnoreCase))
            return new(false, "Mã ghi nhận thu không hợp lệ.", input.InvoiceId);
        var duplicate = await FindByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (duplicate is not null)
            return new(true, $"Khoản thu {duplicate.Code} đã được ghi nhận trước đó.", duplicate.InvoiceId, duplicate.Code);

        if (input.Amount <= 0)
            return new(false, "Số tiền thu phải lớn hơn 0.", input.InvoiceId);
        if (!TryParseMethod(input.Method, out var method))
            return new(false, "Phương thức thu không hợp lệ.", input.InvoiceId);
        if (method == PaymentMethod.BankTransfer && string.IsNullOrWhiteSpace(input.ExternalReference))
            return new(false, "Vui lòng nhập mã tham chiếu chuyển khoản.", input.InvoiceId);
        if (input.ReceivedAt is null)
            return new(false, "Vui lòng nhập thời điểm nhận tiền.", input.InvoiceId);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => RecordPaymentWithinTransactionAsync(
            input, managerUserId, idempotencyKey, method, cancellationToken));
    }

    private async Task<BillingOperationResult> RecordPaymentWithinTransactionAsync(
        RecordPaymentInput input,
        string managerUserId,
        string idempotencyKey,
        PaymentMethod method,
        CancellationToken cancellationToken)
    {
        var duplicate = await FindByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (duplicate is not null)
            return new(true, $"Khoản thu {duplicate.Code} đã được ghi nhận trước đó.", duplicate.InvoiceId, duplicate.Code);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var invoice = await db.Invoices
                .Include(x => x.StudentProfile)
                .Include(x => x.BillingPeriod)
                .Include(x => x.Transactions)
                .SingleOrDefaultAsync(x => x.Id == input.InvoiceId, cancellationToken);
            if (invoice is null)
                return new(false, "Không tìm thấy hóa đơn.", input.InvoiceId);
            if (!HasCurrentVersion(invoice, input.RowVersion))
                return new(false, "Hóa đơn đã thay đổi. Hãy tải lại trước khi ghi nhận thu.", invoice.Id);

            var summary = Summarize(invoice);
            if (!summary.CanCollect)
                return new(false, invoice.Status == InvoiceStatus.Cancelled ? "Hóa đơn đã hủy." : "Hóa đơn đã thu đủ.", invoice.Id);
            if (input.Amount > summary.Balance)
                return new(false, $"Số tiền vượt công nợ hiện tại {summary.Balance:N0} đ.", invoice.Id);

            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (await PendingVnpayCheckouts(invoice.Id, now).AnyAsync(cancellationToken))
                return new(false, "Hóa đơn đang chờ VNPAY xác nhận. Hãy kiểm tra lại trước khi ghi nhận thu.", invoice.Id);

            var receivedAt = ToUtc(input.ReceivedAt!.Value);
            if (receivedAt > now.AddMinutes(5))
                return new(false, "Thời điểm nhận tiền không thể ở tương lai.", invoice.Id);

            db.Entry(invoice).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(input.RowVersion);
            var code = await AppendCollectionAsync(invoice, new PaymentTransaction
            {
                Amount = input.Amount,
                Method = method,
                ReceivedAt = receivedAt,
                RecordedByUserId = managerUserId,
                ExternalReference = NullIfWhiteSpace(input.ExternalReference),
                IdempotencyKey = idempotencyKey,
                Note = NullIfWhiteSpace(input.Note)
            }, now, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync(invoice.StudentProfile.UserId);
            return new(true, $"Đã ghi nhận {code} · {input.Amount:N0} đ.", invoice.Id, code);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Hóa đơn vừa được người khác cập nhật. Hãy tải lại và kiểm tra công nợ.", input.InvoiceId);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            duplicate = await FindByIdempotencyAsync(idempotencyKey, cancellationToken);
            if (duplicate is not null)
                return new(true, $"Khoản thu {duplicate.Code} đã được ghi nhận trước đó.", duplicate.InvoiceId, duplicate.Code);
            throw;
        }
    }

    public async Task<BillingOperationResult> ReversePaymentAsync(
        ReversePaymentInput input,
        string managerUserId,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = input.IdempotencyKey.Trim();
        if (idempotencyKey.StartsWith(VnpayIdempotencyPrefix, StringComparison.OrdinalIgnoreCase))
            return new(false, "Mã hoàn tác không hợp lệ.", 0);
        var duplicate = await FindByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (duplicate is not null)
            return new(true, $"Hoàn tác {duplicate.Code} đã được ghi nhận trước đó.", duplicate.InvoiceId, duplicate.Code);

        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 8)
            return new(false, "Lý do hoàn tác cần ít nhất 8 ký tự.", 0);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => ReversePaymentWithinTransactionAsync(
            input, managerUserId, idempotencyKey, cancellationToken));
    }

    private async Task<BillingOperationResult> ReversePaymentWithinTransactionAsync(
        ReversePaymentInput input,
        string managerUserId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var duplicate = await FindByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (duplicate is not null)
            return new(true, $"Hoàn tác {duplicate.Code} đã được ghi nhận trước đó.", duplicate.InvoiceId, duplicate.Code);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var original = await db.PaymentTransactions
                .Include(x => x.Invoice).ThenInclude(x => x.StudentProfile)
                .Include(x => x.Invoice).ThenInclude(x => x.BillingPeriod)
                .Include(x => x.Invoice).ThenInclude(x => x.Transactions)
                .Include(x => x.ReversalTransaction)
                .SingleOrDefaultAsync(x => x.Id == input.TransactionId, cancellationToken);
            if (original is null)
                return new(false, "Không tìm thấy giao dịch thu.", 0);
            if (original.Method == PaymentMethod.VnpaySandbox)
                return new(false, "Khoản thu VNPAY cần đối soát trên cổng thanh toán; hoàn tác thủ công không hoàn tiền VNPAY.", original.InvoiceId);
            if (original.Amount <= 0 || original.ReversesTransactionId is not null)
                return new(false, "Chỉ có thể hoàn tác một khoản thu gốc.", original.InvoiceId);
            if (original.ReversalTransaction is not null)
                return new(false, $"Khoản thu đã được hoàn tác bởi {original.ReversalTransaction.Code}.", original.InvoiceId);
            if (!HasCurrentVersion(original.Invoice, input.RowVersion))
                return new(false, "Hóa đơn đã thay đổi. Hãy tải lại trước khi hoàn tác.", original.InvoiceId);

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var code = await NextReceiptCodeAsync(now, cancellationToken);
            original.Invoice.LedgerUpdatedAt = now;
            db.Entry(original.Invoice).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(input.RowVersion);
            db.PaymentTransactions.Add(new PaymentTransaction
            {
                InvoiceId = original.InvoiceId,
                Code = code,
                Amount = -original.Amount,
                Method = PaymentMethod.Adjustment,
                ReceivedAt = now,
                RecordedAt = now,
                RecordedByUserId = managerUserId,
                ExternalReference = original.Code,
                IdempotencyKey = idempotencyKey,
                ReversesTransactionId = original.Id,
                Note = input.Reason.Trim()
            });
            AddNotification(original.Invoice, now, "Khoản thu đã được điều chỉnh", $"Khoản thu {original.Code} của {original.Invoice.Code} đã được hoàn tác. Vui lòng kiểm tra công nợ mới.");

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync(original.Invoice.StudentProfile.UserId);
            return new(true, $"Đã hoàn tác {original.Code} bằng {code}.", original.InvoiceId, code);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Hóa đơn vừa được người khác cập nhật. Hãy tải lại trước khi hoàn tác.", 0);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            duplicate = await FindByIdempotencyAsync(idempotencyKey, cancellationToken);
            if (duplicate is not null)
                return new(true, $"Hoàn tác {duplicate.Code} đã được ghi nhận trước đó.", duplicate.InvoiceId, duplicate.Code);
            throw;
        }
    }

    private IQueryable<BillingProjection> InvoiceProjections() => db.Invoices.AsNoTracking()
        .Select(x => new BillingProjection
        {
            Id = x.Id,
            Code = x.Code,
            StudentCode = x.StudentProfile.StudentCode,
            StudentName = x.StudentProfile.FullName,
            RoomCode = x.Stay != null ? x.Stay.Room.Code : "Chưa xếp",
            BuildingCode = x.Stay != null ? x.Stay.Room.Floor.Building.Code : string.Empty,
            PeriodId = x.BillingPeriodId,
            PeriodName = x.BillingPeriod.Name,
            DueDate = x.BillingPeriod.DueDate,
            IssuedAt = x.IssuedAt,
            TotalAmount = x.TotalAmount,
            CollectedAmount = x.Transactions.Sum(transaction => (decimal?)transaction.Amount) ?? 0,
            IsCancelled = x.Status == InvoiceStatus.Cancelled
        });

    private static IQueryable<BillingProjection> ApplyFilters(
        IQueryable<BillingProjection> source,
        BillingQueueQuery query,
        DateOnly businessDate)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.Code.Contains(search)
                || x.StudentCode.Contains(search)
                || x.StudentName.Contains(search)
                || x.RoomCode.Contains(search));
        }
        if (query.PeriodId.HasValue)
            source = source.Where(x => x.PeriodId == query.PeriodId.Value);
        if (!string.IsNullOrWhiteSpace(query.Building))
            source = source.Where(x => x.BuildingCode == query.Building);
        if (query.MinBalance.HasValue)
            source = source.Where(x => x.TotalAmount - x.CollectedAmount >= query.MinBalance.Value);
        if (query.MaxBalance.HasValue)
            source = source.Where(x => x.TotalAmount - x.CollectedAmount <= query.MaxBalance.Value);

        var tomorrow = businessDate.AddDays(1);
        var dueSoonEnd = businessDate.AddDays(7);
        source = query.Status?.ToLowerInvariant() switch
        {
            "outstanding" => source.Where(x => !x.IsCancelled && x.TotalAmount > x.CollectedAmount),
            "due-soon" => source.Where(x => !x.IsCancelled && x.TotalAmount > x.CollectedAmount
                && x.DueDate >= tomorrow && x.DueDate <= dueSoonEnd),
            "overdue" => source.Where(x => !x.IsCancelled && x.TotalAmount > x.CollectedAmount && x.DueDate < businessDate),
            "partial" => source.Where(x => !x.IsCancelled && x.CollectedAmount > 0 && x.CollectedAmount < x.TotalAmount && x.DueDate >= businessDate),
            "paid" => source.Where(x => !x.IsCancelled && x.CollectedAmount >= x.TotalAmount),
            "awaiting" => source.Where(x => !x.IsCancelled && x.CollectedAmount <= 0 && x.DueDate >= businessDate),
            "cancelled" => source.Where(x => x.IsCancelled),
            _ => source
        };
        return source;
    }

    public async Task<BillingReceiptViewModel?> GetReceiptAsync(long transactionId, string? studentUserId, CancellationToken cancellationToken = default)
    {
        var records = db.PaymentTransactions.AsNoTracking().Where(x => x.Id == transactionId && x.Amount > 0 && x.ReversesTransactionId == null);
        if (studentUserId is not null) records = records.Where(x => x.Invoice.StudentProfile.UserId == studentUserId);
        var receipt = await records.Select(x => new
        {
            x.InvoiceId, InvoiceCode = x.Invoice.Code, PeriodName = x.Invoice.BillingPeriod.Name,
            x.Invoice.StudentProfile.StudentCode, StudentName = x.Invoice.StudentProfile.FullName,
            x.Code, x.Amount, x.Method, x.ReceivedAt,
            ReversalCode = x.ReversalTransaction == null ? null : x.ReversalTransaction.Code
        }).SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? null : new(receipt.InvoiceId, receipt.InvoiceCode, receipt.PeriodName,
            receipt.StudentCode, receipt.StudentName, receipt.Code, receipt.Amount, MethodLabel(receipt.Method),
            ToBusinessLocal(receipt.ReceivedAt), receipt.ReversalCode);
    }

    private async Task<BillingInvoiceDetailViewModel?> LoadDetailAsync(
        long invoiceId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(x => x.StudentProfile)
            .Include("Stay.Room.Floor.Building")
            .Include(x => x.BillingPeriod)
            .Include(x => x.Lines)
            .Include(x => x.Transactions).ThenInclude(x => x.RecordedByUser)
            .Include(x => x.Transactions).ThenInclude(x => x.ReversalTransaction)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
        if (invoice is null) return null;

        var summary = Summarize(invoice, businessDate);
        var vnpayPending = summary.CanCollect &&
            await PendingVnpayCheckouts(invoice.Id, timeProvider.GetUtcNow().UtcDateTime).AnyAsync(cancellationToken);
        var vnpayReviews = await db.VnpayCheckouts.AsNoTracking()
            .Where(x => x.InvoiceId == invoice.Id && x.State == VnpayCheckoutState.NeedsReview)
            .OrderByDescending(x => x.ConfirmedAtUtc)
            .Select(x => new VnpayReviewViewModel(x.Reference, x.Amount, x.ProviderTransactionNumber, x.ResponseCode, x.ProviderState))
            .ToListAsync(cancellationToken);
        var resolutions = await db.VnpayCheckouts.AsNoTracking()
            .Where(x => x.InvoiceId == invoice.Id && x.ReviewedAtUtc != null)
            .OrderByDescending(x => x.ReviewedAtUtc)
            .Select(x => new VnpayResolutionViewModel(x.Reference, x.State == VnpayCheckoutState.ReviewedRefunded,
                x.ReviewEvidence!, x.ReviewedAtUtc!.Value, db.Users.Where(u => u.Id == x.ReviewedById).Select(u => u.FullName).FirstOrDefault() ?? "Ban quản lý"))
            .ToListAsync(cancellationToken);
        return new BillingInvoiceDetailViewModel
        {
            Id = invoice.Id,
            StudentId = invoice.StudentProfileId,
            Code = invoice.Code,
            StudentCode = invoice.StudentProfile.StudentCode,
            StudentName = invoice.StudentProfile.FullName,
            Faculty = invoice.StudentProfile.Faculty,
            PhoneNumber = invoice.StudentProfile.PhoneNumber,
            RoomCode = invoice.Stay?.Room.Code ?? "Chưa xếp",
            BuildingName = invoice.Stay?.Room.Floor.Building.Name ?? "Chưa xác định",
            PeriodName = invoice.BillingPeriod.Name,
            DueDate = invoice.BillingPeriod.DueDate,
            IssuedAt = ToBusinessLocal(invoice.IssuedAt),
            TotalAmount = summary.Total,
            CollectedAmount = summary.Collected,
            Balance = summary.Balance,
            StatusLabel = summary.StatusLabel,
            StatusClass = summary.StatusClass,
            CanCollect = summary.CanCollect && !vnpayPending,
            VnpayPending = vnpayPending,
            VnpayReviews = vnpayReviews,
            VnpayResolutions = resolutions,
            RowVersion = Convert.ToBase64String(invoice.RowVersion),
            Lines = invoice.Lines.OrderBy(x => x.Id)
                .Select(x => new BillingInvoiceLineViewModel(x.Description, x.ChargeType, x.Amount))
                .ToList(),
            Transactions = MapTransactions(invoice.Transactions)
        };
    }

    private BillingInvoiceSnapshot ToSnapshot(Invoice invoice, DateOnly businessDate)
    {
        var summary = Summarize(invoice, businessDate);
        return new BillingInvoiceSnapshot(
            invoice.Id,
            invoice.Code,
            invoice.BillingPeriod.Name,
            invoice.BillingPeriod.StartDate,
            invoice.BillingPeriod.EndDate,
            invoice.BillingPeriod.DueDate,
            invoice.IssuedAt,
            summary,
            invoice.Lines.OrderBy(x => x.Id).Select(x => new BillingLedgerLine(x.Description, x.ChargeType, x.Amount)).ToList(),
            invoice.Transactions.OrderByDescending(x => x.RecordedAt).Select(ToLedgerTransaction).ToList());
    }

    private static IReadOnlyList<BillingTransactionViewModel> MapTransactions(IEnumerable<PaymentTransaction> transactions) =>
        transactions.OrderByDescending(x => x.RecordedAt)
            .Select(x =>
            {
                var ledger = ToLedgerTransaction(x);
                return new BillingTransactionViewModel(
                    ledger.Id,
                    ledger.Code,
                    ledger.Amount,
                    ledger.MethodLabel,
                    ToBusinessLocal(ledger.ReceivedAt),
                    ToBusinessLocal(ledger.RecordedAt),
                    ledger.RecordedBy,
                    ledger.ExternalReference,
                    ledger.Note,
                    ledger.IsReversal,
                    ledger.HasBeenReversed,
                    ledger.ReversedCode,
                    x.Method != PaymentMethod.VnpaySandbox && ledger.Amount > 0 && !ledger.IsReversal && !ledger.HasBeenReversed);
            }).ToList();

    private static BillingLedgerTransaction ToLedgerTransaction(PaymentTransaction transaction) => new(
        transaction.Id,
        transaction.Code,
        transaction.Amount,
        MethodLabel(transaction.Method),
        transaction.ReceivedAt,
        transaction.RecordedAt,
        transaction.RecordedByUser?.FullName ?? (transaction.Method == PaymentMethod.VnpaySandbox ? "VNPAY Sandbox" : "Dữ liệu chuyển đổi"),
        transaction.ExternalReference,
        transaction.Note,
        transaction.ReversesTransactionId.HasValue,
        transaction.ReversalTransaction is not null,
        transaction.ReversalTransaction?.Code);

    private static DateTime ToBusinessLocal(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(utc, BusinessTimeZone),
            DateTimeKind.Unspecified);
    }

    private BillingLedgerSummary Summarize(Invoice invoice) => Summarize(invoice, GetBusinessDate());

    private static BillingLedgerSummary Summarize(Invoice invoice, DateOnly businessDate) =>
        BillingLedgerPolicy.Summarize(
            invoice.TotalAmount,
            invoice.Transactions.Select(x => x.Amount),
            invoice.BillingPeriod.DueDate,
            businessDate,
            invoice.Status);

    private static BillingQueueItemViewModel ToQueueItem(
        BillingProjection row,
        long? selectedId,
        DateOnly businessDate)
    {
        var lifecycle = row.IsCancelled ? InvoiceStatus.Cancelled : InvoiceStatus.Issued;
        var summary = BillingLedgerPolicy.Summarize(row.TotalAmount, [row.CollectedAmount], row.DueDate, businessDate, lifecycle);
        return new BillingQueueItemViewModel(
            row.Id,
            row.Code,
            row.StudentCode,
            row.StudentName,
            Initials(row.StudentName),
            row.RoomCode,
            row.PeriodName,
            row.DueDate,
            row.TotalAmount,
            summary.Collected,
            summary.Balance,
            summary.StatusLabel,
            summary.StatusClass,
            row.Id == selectedId);
    }

    private async Task<PaymentTransaction?> FindByIdempotencyAsync(string key, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : await db.PaymentTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);

    private async Task<string> NextReceiptCodeAsync(DateTime now, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT NEXT VALUE FOR [dbo].[ReceiptNumbers]";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return $"THU-{ToBusinessLocal(now):yyyyMMdd}-{value:000000}";
    }

    private async Task<string> AppendCollectionAsync(
        Invoice invoice, PaymentTransaction collection, DateTime now, CancellationToken cancellationToken)
    {
        collection.InvoiceId = invoice.Id;
        collection.Code = await NextReceiptCodeAsync(now, cancellationToken);
        collection.RecordedAt = now;
        invoice.LedgerUpdatedAt = now;
        db.Entry(invoice).Property(x => x.LedgerUpdatedAt).IsModified = true;
        db.PaymentTransactions.Add(collection);
        AddNotification(invoice, now, "Đã ghi nhận khoản thu",
            $"{collection.Code} · {collection.Amount:N0} đ đã được ghi nhận cho {invoice.Code}.");
        return collection.Code;
    }

    private static bool HasCurrentVersion(Invoice invoice, string rowVersion)
    {
        try
        {
            return invoice.RowVersion.SequenceEqual(Convert.FromBase64String(rowVersion));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void AddNotification(Invoice invoice, DateTime now, string title, string body)
    {
        if (invoice.StudentProfile.UserId is null) return;
        db.Notifications.Add(new Notification
        {
            UserId = invoice.StudentProfile.UserId,
            Type = "Invoice",
            Title = title,
            Body = body,
            TargetUrl = "/Student/Invoices",
            CreatedAt = now
        });
    }

    private DateOnly GetBusinessDate()
    {
        var local = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static DateTime ToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc) return value;
        var unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, BusinessTimeZone);
    }

    private static bool TryParseMethod(string value, out PaymentMethod method) =>
        Enum.TryParse(value, true, out method) && method is PaymentMethod.Cash or PaymentMethod.BankTransfer;

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.VnpaySandbox => "VNPAY Sandbox",
        _ => "Điều chỉnh"
    };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddCharge(ICollection<InvoiceLine> lines, string type, string description, decimal amount)
    {
        if (amount > 0)
            lines.Add(new InvoiceLine { ChargeType = type, Description = description, Amount = amount });
    }

    private static string BuildInvoiceCode(int periodId, string studentCode)
    {
        var normalized = new string(studentCode.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var suffix = normalized.Length <= 15 ? normalized : normalized[^15..];
        return $"HD{periodId:000000}-{suffix}"[..Math.Min(24, 9 + suffix.Length)];
    }

    private static string Initials(string fullName) => string.Concat(
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(2).Select(x => x[0]));

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Utc;
    }

    private sealed class BillingProjection
    {
        public long Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string StudentCode { get; init; } = string.Empty;
        public string StudentName { get; init; } = string.Empty;
        public string RoomCode { get; init; } = string.Empty;
        public string BuildingCode { get; init; } = string.Empty;
        public int PeriodId { get; init; }
        public string PeriodName { get; init; } = string.Empty;
        public DateOnly DueDate { get; init; }
        public DateTime IssuedAt { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal CollectedAmount { get; init; }
        public bool IsCancelled { get; init; }
    }
}
