using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Services;

public enum HousingRequestAction
{
    BeginReview,
    RequestInformation,
    SubmitInformation,
    Approve,
    ConfirmMoveIn,
    Reject,
    Cancel
}

public enum HousingRequestResponsibility
{
    Management,
    Student,
    None
}

public sealed record HousingRequestTransition(
    bool Allowed,
    HousingRequestStatus NextStatus,
    string ErrorMessage);

public sealed record HousingRequestWorkflowSnapshot(
    HousingRequestStatus Status,
    string StatusLabel,
    string StatusClass,
    HousingRequestResponsibility Responsibility,
    string ResponsibilityLabel,
    string NextActionLabel,
    bool IsActive,
    bool IsManagerOverdue,
    int WaitingHours);

public static class HousingRequestWorkflow
{
    public const int ManagerSlaHours = 24;

    public static readonly HousingRequestStatus[] ActiveStatuses =
        [HousingRequestStatus.Submitted, HousingRequestStatus.UnderReview, HousingRequestStatus.NeedsInformation, HousingRequestStatus.AwaitingMoveIn];

    public static bool IsActive(HousingRequestStatus status) => ActiveStatuses.Contains(status);

    public static bool Can(HousingRequestStatus status, HousingRequestAction action) =>
        Transition(status, action).Allowed;

    public static HousingRequestTransition Transition(
        HousingRequestStatus current,
        HousingRequestAction action,
        HousingRequestType? requestType = null)
    {
        var next = action switch
        {
            HousingRequestAction.BeginReview when current == HousingRequestStatus.Submitted => HousingRequestStatus.UnderReview,
            HousingRequestAction.RequestInformation when current is HousingRequestStatus.Submitted or HousingRequestStatus.UnderReview => HousingRequestStatus.NeedsInformation,
            HousingRequestAction.SubmitInformation when current == HousingRequestStatus.NeedsInformation => HousingRequestStatus.Submitted,
            HousingRequestAction.Approve when current == HousingRequestStatus.UnderReview && requestType == HousingRequestType.Checkout => HousingRequestStatus.Approved,
            HousingRequestAction.Approve when current == HousingRequestStatus.UnderReview => HousingRequestStatus.AwaitingMoveIn,
            HousingRequestAction.ConfirmMoveIn when current == HousingRequestStatus.AwaitingMoveIn => HousingRequestStatus.Approved,
            HousingRequestAction.Reject when current is HousingRequestStatus.Submitted or HousingRequestStatus.UnderReview or HousingRequestStatus.NeedsInformation or HousingRequestStatus.AwaitingMoveIn => HousingRequestStatus.Rejected,
            HousingRequestAction.Cancel when IsActive(current) => HousingRequestStatus.Cancelled,
            _ => current
        };

        return next == current
            ? new(false, current, ErrorFor(current, action))
            : new(true, next, string.Empty);
    }

    public static HousingRequestWorkflowSnapshot Describe(
        HousingRequestStatus status,
        DateTime? actionRequiredAt,
        DateTime nowUtc)
    {
        var (label, css) = Presentation(status);
        var responsibility = status switch
        {
            HousingRequestStatus.Submitted or HousingRequestStatus.UnderReview => HousingRequestResponsibility.Management,
            HousingRequestStatus.NeedsInformation => HousingRequestResponsibility.Student,
            HousingRequestStatus.AwaitingMoveIn => HousingRequestResponsibility.Management,
            _ => HousingRequestResponsibility.None
        };
        var waitingHours = actionRequiredAt.HasValue
            ? Math.Max(0, (int)(nowUtc - actionRequiredAt.Value).TotalHours)
            : 0;
        var overdue = responsibility == HousingRequestResponsibility.Management &&
                      waitingHours >= ManagerSlaHours;

        return new(
            status,
            label,
            css,
            responsibility,
            responsibility switch
            {
                HousingRequestResponsibility.Management => "Ban quản lý",
                HousingRequestResponsibility.Student => "Sinh viên",
                _ => "Không còn"
            },
            status switch
            {
                HousingRequestStatus.Submitted => "Chờ Ban quản lý tiếp nhận",
                HousingRequestStatus.UnderReview => "Chờ Ban quản lý quyết định",
                HousingRequestStatus.NeedsInformation => "Chờ sinh viên bổ sung",
                HousingRequestStatus.AwaitingMoveIn => "Chờ xác nhận nhận phòng",
                HousingRequestStatus.Approved => "Đơn đã được duyệt",
                HousingRequestStatus.Rejected => "Đơn đã bị từ chối",
                _ => "Đơn đã được hủy"
            },
            IsActive(status),
            overdue,
            waitingHours);
    }

    public static (string Label, string Css) Presentation(HousingRequestStatus status) => status switch
    {
        HousingRequestStatus.Submitted => ("Đã gửi", "submitted"),
        HousingRequestStatus.UnderReview => ("Đang xét duyệt", "reviewing"),
        HousingRequestStatus.NeedsInformation => ("Cần bổ sung", "needs-info"),
        HousingRequestStatus.AwaitingMoveIn => ("Chờ nhận phòng", "awaiting-movein"),
        HousingRequestStatus.Approved => ("Đã duyệt", "approved"),
        HousingRequestStatus.Rejected => ("Từ chối", "rejected"),
        _ => ("Đã hủy", "cancelled")
    };

    public static string TypeLabel(HousingRequestType type) => type switch
    {
        HousingRequestType.Registration => "Đăng ký ở",
        HousingRequestType.Transfer => "Chuyển phòng",
        _ => "Trả phòng"
    };

    private static string ErrorFor(HousingRequestStatus current, HousingRequestAction action) => action switch
    {
        HousingRequestAction.BeginReview => "Chỉ đơn đã gửi mới có thể được nhận xử lý.",
        HousingRequestAction.RequestInformation => "Chỉ có thể yêu cầu bổ sung khi đơn đang chờ hoặc đang xét duyệt.",
        HousingRequestAction.SubmitInformation => "Đơn này hiện không chờ sinh viên bổ sung thông tin.",
        HousingRequestAction.Approve => current == HousingRequestStatus.Submitted
            ? "Hãy nhận xử lý và kiểm tra hồ sơ trước khi duyệt."
            : "Chỉ đơn đang xét duyệt mới có thể được duyệt.",
        HousingRequestAction.ConfirmMoveIn => "Chỉ đơn đã giữ chỗ mới có thể xác nhận nhận phòng.",
        HousingRequestAction.Reject => "Đơn đã hoàn tất nên không thể từ chối lại.",
        HousingRequestAction.Cancel => "Chỉ có thể hủy đơn chưa có quyết định cuối.",
        _ => "Chuyển trạng thái không hợp lệ."
    };
}
