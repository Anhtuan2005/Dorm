# Sprint 5: Báo cáo vận hành và hardening

Status: ready-for-human

## Problem Statement

Các module phòng, sinh viên, đơn lưu trú, thu phí và sự cố đã hoạt động nhưng trang vào hệ thống chưa giúp quản lý nhận ra sai lệch quan trọng trong một ca. Báo cáo và Cài đặt còn là liên kết rỗng; một số hành động trên sơ đồ phòng vẫn dùng số minh họa hoặc thông báo demo. Dự án cũng cần một lượt kiểm tra chéo hiển thị, authorization, CSRF, lỗi trình duyệt và bất biến SQL trước khi đóng phạm vi cốt lõi.

## Solution

Tạo một `Ảnh chụp vận hành` làm trang mặc định của quản lý, trong đó sức chứa, dòng tiền và việc cần can thiệp tạo thành một xương sống liên tục. Thêm báo cáo lọc theo `Kỳ thu`/tòa và xuất CSV, trang đọc cấu hình an toàn, rồi thay liên kết giả bằng worklist thật. Toàn bộ số liệu đi qua `OperationsReportingService`, còn các phép tính phần trăm và sức chứa đi qua policy dùng chung.

## Delivered user stories

1. Quản lý đăng nhập và thấy ngay sức chứa, công nợ, đơn quá SLA, hóa đơn quá hạn và sự cố khẩn cấp từ SQL Server.
2. Quản lý đối chiếu từng tòa theo chỗ đang ở, đang giữ, tạm khóa, có thể xếp, công nợ và sự cố mở.
3. Quản lý đọc nhịp đơn lưu trú và hoạt động mới nhất rồi mở đúng worklist liên quan.
4. Quản lý lọc báo cáo theo kỳ/tòa và xem tài chính, xu hướng thu, kết quả đơn, bảo trì cùng bảng đối chiếu.
5. Quản lý xuất đúng phạm vi báo cáo ra CSV UTF-8; text đầu vào được bảo vệ khỏi formula injection.
6. Sơ đồ phòng dùng số đơn/hóa đơn/sự cố thật và dẫn tới danh sách thật; hồ sơ sinh viên dẫn tới công nợ thật.
7. Tìm kiếm toàn cục phân luồng mã phòng sang sơ đồ và sinh viên/tên/email sang danh bạ.
8. Cài đặt cho biết database, migration, quy mô cơ sở, luật an toàn và ranh giới vai trò mà không hiển thị secret.
9. Student không thể mở Dashboard/Báo cáo/Cài đặt/worklist/CSV của quản lý.
10. Giao diện không tràn ngang ở desktop 1.586px hoặc mobile 430px và không phát sinh lỗi console/request.

## Implementation decisions

- Giữ ASP.NET Core MVC, Razor, CSS và JavaScript thuần.
- `OperationsReportingService` là read-model seam cho Dashboard/Báo cáo/Cài đặt; bộ lọc đặt trên entity trước projection để EF Core thực thi trong SQL.
- `OperationsMetricsPolicy` chuẩn hóa phần trăm, xử lý mẫu số 0 và clamp 0–100.
- Report phân biệt rõ số sức chứa hiện tại với kết quả theo kỳ.
- Không thêm migration vì sprint chỉ thêm read models và UI trên schema hiện tại.
- Cấu hình bí mật không xuất hiện trong HTML/CSV; production secret tiếp tục thuộc environment variable hoặc .NET User Secrets.

## Verification

- 26 unit tests pass; build Release không warning/error.
- EF Core: không có pending model changes.
- NuGet: không phát hiện package dễ tổn thương đã biết ở app hoặc test project.
- Browser audit: 21 lượt route, hai vai trò, desktop/mobile; kiểm tra HTTP, h1, console, request failure, dead link, horizontal overflow, antiforgery, role boundary và CSV.
- SQL invariant audit: 0 duplicate active stays, active requests, invoice periods, payment idempotency keys; 0 request thiếu event; 0 phòng vượt sức chứa.

## Deferred after core

- Kết nối mô hình AI và kho tài liệu thật cho chatbot.
- Upload ảnh/file chứng cứ cho sự cố.
- Realtime push qua SignalR thay cho dữ liệu thông báo đã ghi bền vững.
- Lập lịch báo cáo, PDF và phân tích nhiều cơ sở.
