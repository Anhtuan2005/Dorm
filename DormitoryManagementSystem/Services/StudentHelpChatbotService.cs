using System.Text.RegularExpressions;
using DormitoryManagementSystem.ViewModels;
using Microsoft.Extensions.Options;

namespace DormitoryManagementSystem.Services;

public sealed class StudentHelpChatbotService(
    StudentKnowledgeCatalog catalog,
    GeminiChatbotService gemini,
    IStudentRoomSearch roomSearch,
    IOptions<StudentSupportOptions> support,
    IOptions<VnpayOptions> vnpay) : IChatbotService
{
    private static readonly Regex BuildingMention = new(
        @"\btoa (?:nha )?([a-z][0-9]{0,3})\b", RegexOptions.CultureInvariant);

    private static readonly Workflow[] Workflows =
    [
        new(null, "contact",
            "Nếu bạn còn đăng nhập, mở tài khoản và chọn Đổi mật khẩu. Nếu quên mật khẩu hoặc tài khoản bị khóa, liên hệ Ban quản lý để kiểm tra.",
            ["quen mat khau", "mat mat khau", "khoa tai khoan", "tai khoan bi khoa", "khong dang nhap duoc", "k dang nhap duoc"]),
        new("Đổi mật khẩu thế nào?", "password",
            "Mở menu tài khoản, chọn Đổi mật khẩu và nhập mật khẩu hiện tại cùng mật khẩu mới.",
            ["doi mat khau", "thay mat khau", "mat khau moi"], AllowGemini: true),
        new(null, "applications",
            "Vào Đăng ký của tôi, mở đơn có trạng thái Cần bổ sung, điền phần Nội dung bổ sung rồi bấm Gửi bổ sung và chuyển lại BQL. Kiểm tra nhật ký đơn để biết nội dung đã gửi.",
            ["bo sung", "nop lai don", "sua ho so da gui", "them thong tin vao don"]),
        new(null, "applications",
            "Vào Đăng ký của tôi, tìm đơn và bấm Hủy đơn nếu nút này còn xuất hiện. Nếu đơn đã được xử lý và cần thay đổi, liên hệ Ban quản lý.",
            ["huy don", "rut don", "xoa don", "huy yeu cau luu tru"]),
        new(null, "applications",
            "Mở Đăng ký của tôi, tìm đơn và xem mục Việc tiếp theo. Nếu đơn đã được duyệt giữ chỗ, làm theo hướng dẫn bàn giao của Ban quản lý để xác nhận nhận phòng.",
            ["giu cho", "nhan phong", "ban giao phong", "duoc duyet chua", "da duyet chua"]),
        new(null, "rooms",
            "Vào Tìm phòng, bấm Chọn phòng để mở mô hình phòng và xem ảnh nếu có. Chọn một giường còn trống rồi tiếp tục gửi đơn.",
            ["chon giuong", "doi giuong", "xem giuong", "anh phong", "hinh phong", "hinh anh phong", "phong 3d", "mo hinh phong", "tien nghi phong"]),
        new("Chuyển phòng cần làm gì?", "rooms",
            "Vào Tìm phòng, chọn phòng và giường còn chỗ, nhập lý do chuyển rồi gửi đơn. Theo dõi tại Đăng ký của tôi.",
            ["chuyen phong", "doi phong", "chuyen sang phong khac"]),
        new("Trả phòng cần làm gì?", "applications",
            "Vào Đăng ký của tôi, chọn Trả phòng, nhập lý do và gửi yêu cầu. Lượt ở kết thúc sau khi Ban quản lý duyệt trả phòng.",
            ["tra phong", "roi ktx", "roi ky tuc xa", "chuyen ra khoi ktx"]),
        new(null, "repairs",
            "Vào Yêu cầu hỗ trợ → Gửi yêu cầu → Chọn ảnh. Bạn có thể gửi kèm tối đa 3 ảnh JPG, PNG hoặc WebP, mỗi ảnh tối đa 5 MB.",
            ["anh hien truong", "anh su co", "anh sua chua", "gui anh", "dinh kem anh", "tai anh len", "upload anh"]),
        new(null, "repairs",
            "Mở Yêu cầu hỗ trợ, tìm sự cố và xem trạng thái cùng nhật ký cập nhật ngay dưới nội dung yêu cầu.",
            ["tien do sua chua", "tien do su co", "theo doi sua chua", "theo doi su co", "sua den dau", "sua xong chua", "bao hong den dau"]),
        new("Báo sự cố ở đâu?", "repairs",
            "Vào Yêu cầu hỗ trợ → Gửi yêu cầu, chọn nhóm sự cố, mô tả vị trí và tình trạng, rồi gửi. Bạn cần có phòng đang ở để tạo yêu cầu mới.",
            ["su co", "sua chua", "bao hong", "hu hong", "ro nuoc", "ri nuoc", "mat dien", "mat nuoc",
                "may lanh hong", "dieu hoa hong", "quat hong", "den hong", "bon cau tac", "wifi hong", "wifi khong vao duoc"]),
        new("Đăng ký phòng thế nào?", "rooms",
            "Vào Tìm phòng, chọn phòng và giường còn trống, điền lý do và gửi đơn. Bạn có thể theo dõi đơn tại Đăng ký của tôi.",
            ["dang ky phong", "dang ky o ktx", "dang ky ktx", "dang ki ktx", "dang ky o ky tuc xa",
                "xin vao ktx", "nop don o ktx", "muon o ktx", "muon thue phong"]),
        new("Xem và đánh dấu thông báo đã đọc thế nào?", "notifications",
            "Bấm chuông để mở Thông báo. Mở một thông báo để xem cập nhật hoặc chọn Đánh dấu tất cả đã đọc.",
            ["thong bao", "chuong", "danh dau da doc", "doc thong bao"])
    ];

    private const string InvoiceGuide =
        "Vào Hóa đơn, chọn kỳ thu rồi mở Chi tiết các khoản phí để xem số tiền và hạn nộp. Các giao dịch đã ghi nhận nằm trong Lịch sử khoản thu.";
    private const string ApplicationGuide =
        "Mở Đăng ký của tôi, tìm đơn và xem trạng thái, mục Việc tiếp theo hoặc Thông tin và nhật ký đơn.";

    public async Task<ChatbotAnswer> AskAsync(
        string studentUserId, ChatbotQuestion question, CancellationToken cancellationToken = default)
    {
        var normalized = StudentKnowledgeCatalog.Normalize(question.Message);
        var questionText = new QuestionText(normalized);

        if (questionText.Has("lien he", "hotline", "tong dai",
                "gap ban quan ly", "gap nguoi ho tro", "noi chuyen voi ban quan ly", "dia chi van phong", "phong truc",
                "ban quan ly o dau", "van phong ktx o dau", "van phong nam o dau")
            || normalized is "ben ho tro" or "thong tin ho tro" or "thong tin ban quan ly" or "so dien thoai" or "sdt" or "email"
            || (questionText.Has("so dien thoai", "sdt", "email", "e mail", "mail")
                && (question.PreviousAction == "contact" || questionText.Has("xin", "ban quan ly", "bql", "ho tro", "ktx", "van phong")))
            || (question.PreviousAction == "contact" && normalized is
                ("o dau" or "o dau vay" or "o dau a" or "dia chi" or "dia chi o dau" or "co so khong" or "co so k")))
            return ContactAnswer();

        var match = catalog.Find(normalized);
        if (match is not null) return match;

        if (questionText.Has("khong nhan duoc thong bao", "khong thay thong bao", "k nhan duoc thong bao"))
            return new(true, "Bấm chuông để xem Thông báo. Nếu bạn đang chờ kết quả xử lý, mở hồ sơ tương ứng để xem trạng thái mới nhất.", Action: "notifications");

        if (questionText.Has("bien nhan", "lich su khoan thu", "lich su thanh toan", "lich su giao dich", "da dong tien"))
            return new(true, "Vào Hóa đơn, mở kỳ thu rồi xem Lịch sử khoản thu. Nếu khoản đã đóng chưa xuất hiện, đối chiếu chứng từ và liên hệ Ban quản lý.", Action: "invoices");

        if (questionText.Has("vnpay", "thanh toan online", "thanh toan qua mang", "nut thanh toan", "bam thanh toan",
                "nhan thanh toan", "nap tien", "dong tien o dau", "nop tien o dau", "tra tien phong")
            || question.PreviousAction == "invoices" && questionText.Has("nut bi mo", "nut bi an", "khong bam duoc nut"))
            return PaymentAnswer();

        if (questionText.Has("cong no", "con no", "no bao nhieu", "con phai dong", "con phai tra")
            || (questionText.Has("hoa don", "ky thu") && questionText.Has("cua", "bao nhieu", "han nop", "han dong", "han thanh toan", "khi nao")))
            return new(true, "Mở Hóa đơn để xem số tiền còn phải trả, hạn nộp theo kỳ và giao dịch của tài khoản đang đăng nhập.", Action: "invoices");

        // A workflow must never stand in for an answer about an unpublished policy or price.
        if (questionText.Has("noi quy", "quy dinh", "dieu kien", "giay to", "bieu phi", "phi", "gia phong", "gia o",
            "gia thue", "muc gia", "gia bao nhieu", "bao nhieu tien", "dong bao nhieu", "han nop", "han dong",
            "han thanh toan", "thoi han", "han chot", "deadline", "bao lau", "may ngay", "may gio", "luc may", "khi nao", "luc nao"))
            return MissingDocument();

        var building = BuildingMention.Match(normalized);
        var buildingFollowUp = building.Success && building.Index == 0
            && normalized[building.Length..] is "" or " thi sao" or " sao a" or " thi sao a";
        if (questionText.Has("con phong", "tim phong", "phong trong", "het phong", "con cho o", "cho nao de o", "con giuong", "co giuong trong", "giuong nao trong")
            || normalized == "giuong trong"
            || (questionText.Has("phong", "ktx", "ky tuc xa") && questionText.Has("con cho", "con trong", "cho trong", "co the xep"))
            || questionText.Has("co phong nao khong", "co phong nao k", "co phong nao ko", "phong nao trong")
            || (question.PreviousAction == "rooms" && (buildingFollowUp || questionText.Has("con cho"))))
        {
            var buildingCode = building.Success ? building.Groups[1].Value.ToUpperInvariant() : null;
            return await RoomAvailabilityAsync(studentUserId, buildingCode, cancellationToken);
        }

        var workflow = Workflows.FirstOrDefault(item => questionText.Has(item.Phrases));
        if (workflow is not null)
        {
            if (workflow.Question is { } canonical)
            {
                var guidance = catalog.Find(StudentKnowledgeCatalog.Normalize(canonical));
                if (guidance is not null) return guidance;
                if (workflow.AllowGemini)
                {
                    // Only a predefined question leaves the application.
                    var answer = await gemini.AskAsync(new() { Message = canonical }, cancellationToken);
                    if (answer.Success) return answer with { Action = workflow.Action };
                }
            }
            return new(true, workflow.Guidance, Action: workflow.Action);
        }

        if (questionText.Has("hoa don", "thanh toan", "dong tien", "nop tien", "ky thu"))
            return new(true, InvoiceGuide, Action: "invoices");

        if (questionText.Has("don", "ho so", "dang ky cua toi")
            && questionText.Has("trang thai", "tinh trang", "duyet", "tien do", "theo doi", "xem", "da gui", "toi dau"))
            return new(true, ApplicationGuide, Action: "applications");

        if (normalized is "chao" or "xin chao" or "chao ban" or "hello" or "hi" or "alo")
            return new(true, "Chào bạn! Mình có thể hỗ trợ tìm phòng, đăng ký, chuyển/trả phòng, xem hóa đơn và báo sự cố. Bạn cần giúp việc gì?");

        if (normalized is "cam on" or "cam on ban" or "cam on nhe" or "thanks" or "thank you" or "ok" or "oke")
            return new(true, "Rất vui được hỗ trợ bạn! Nếu cần tìm phòng, xem đơn hay hóa đơn, bạn cứ hỏi nhé.");

        if (questionText.Has("an gi", "gi an", "mon an", "thoi tiet"))
            return new(true, "Mình hỗ trợ các việc ở KTX như tìm phòng, xem đơn và hóa đơn, hoặc báo sự cố.");

        if (normalized is "o dau" or "o dau vay" or "o dau a" or "lam sao" or "lam the nao" or "buoc tiep theo"
            or "roi sao" or "roi lam gi" or "gui roi sao" or "xem o dau" or "xem o dau vay" or "xem the nao")
            return FollowUp(question.PreviousAction);

        return Clarify();
    }

    private static ChatbotAnswer FollowUp(string? previousAction) => previousAction switch
    {
        "invoices" => new(true, InvoiceGuide, Action: "invoices"),
        "applications" => new(true, ApplicationGuide, Action: "applications"),
        "repairs" => new(true, "Mở Yêu cầu hỗ trợ để gửi yêu cầu mới hoặc xem tiến độ ở yêu cầu đã gửi.", Action: "repairs"),
        "rooms" => new(true, "Mở Tìm phòng, chọn một phòng để xem giường và thông tin chi tiết.", Action: "rooms"),
        "notifications" => new(true, "Bấm biểu tượng chuông ở trên cùng để xem thông báo.", Action: "notifications"),
        "password" => new(true, "Mở menu tài khoản và chọn Đổi mật khẩu.", Action: "password"),
        _ => Clarify()
    };

    private static ChatbotAnswer Clarify() => new(true,
        "Mình chưa hiểu bạn cần giúp việc gì. Bạn đang hỏi về phòng hoặc giường, đơn lưu trú, hóa đơn, sự cố hay tài khoản?");

    private ChatbotAnswer PaymentAnswer()
    {
        const string alternatives = "Trong Hóa đơn, bấm Cách thanh toán để xem cách đóng tại văn phòng KTX hoặc chuyển khoản theo thông báo của Ban quản lý.";
        var gateway = vnpay.Value.ReturnUri() is not null
            ? "Vào Hóa đơn và bấm Thanh toán qua VNPAY ở hóa đơn còn nợ. Nếu nút bị mờ, xem lý do ghi ngay cạnh nút."
            : "VNPAY Sandbox hiện chưa khả dụng trên bản đang chạy nên nút thanh toán bị mờ.";
        return new(true, $"{gateway} {alternatives}", Action: "invoices");
    }

    private static ChatbotAnswer MissingDocument() => new(true,
        "Trợ lý chưa tìm được mục tài liệu đã duyệt cho câu hỏi này. Bạn có thể liên hệ Ban quản lý KTX để được xác nhận.",
        Action: "contact");

    private ChatbotAnswer ContactAnswer()
    {
        var contact = support.Value;
        var lines = new List<string> { $"Bạn có thể gặp Ban quản lý tại {contact.OfficeLocation}." };
        if (contact.PhoneNumber is { } phone) lines.Add($"Điện thoại: {phone}");
        if (contact.EmailAddress is { } email) lines.Add($"Email: {email}");
        if (lines.Count == 1) lines.Add("Hệ thống chưa có số điện thoại và email chính thức của Ban quản lý.");
        return new(true, string.Join('\n', lines), Action: "contact");
    }

    private async Task<ChatbotAnswer> RoomAvailabilityAsync(
        string studentUserId, string? building, CancellationToken cancellationToken)
    {
        var search = await roomSearch.SearchRoomsAsync(studentUserId, new() { Building = building }, cancellationToken);
        var scope = building is null ? "" : $" ở tòa {building}";
        if (search.Rooms.Count == 0)
            return new(true, $"Hiện chưa có phòng còn chỗ phù hợp với hồ sơ của bạn{scope}. Bạn có thể xem lại tại Tìm phòng.",
                Action: "rooms", RoomBuilding: building);

        var suggestions = search.Rooms.Take(3).Select(room => $"• {room.Code} — còn {room.Assignable} chỗ");
        var text = $"Một số phòng phù hợp với hồ sơ của bạn{scope} đang còn chỗ:\n{string.Join('\n', suggestions)}\nMở Tìm phòng để xem chi tiết.";
        return new(true, text, Action: "rooms", RoomBuilding: building);
    }

    private sealed record Workflow(string? Question, string Action, string Guidance, string[] Phrases, bool AllowGemini = false);

    private sealed class QuestionText(string normalized)
    {
        private readonly string words = $" {normalized} ";

        public bool Has(params string[] phrases) => phrases.Any(phrase => words.Contains($" {phrase} ", StringComparison.Ordinal));
    }
}
