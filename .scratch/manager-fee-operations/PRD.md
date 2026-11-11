# Sprint: Thu phí và công nợ

Status: ready-for-human

## Problem Statement

Người quản lý chưa có màn Thu phí hoạt động. Họ không thể tìm nhanh `Hóa đơn` đến hạn hoặc quá hạn, kiểm tra cấu phần phí, ghi nhận khoản tiền nhận tại văn phòng/chuyển khoản, sửa một lần ghi nhận sai theo cách có kiểm soát hay xem lịch sử đối soát. Dữ liệu hiện chỉ lưu một tổng đã trả và một trạng thái, trong khi cổng sinh viên tự tính lại quá hạn, nên cùng một `Hóa đơn` có thể được diễn giải khác nhau giữa các màn hình.

## Solution

Xây dựng vertical slice Thu phí và công nợ cho quản lý, lấy danh sách cần xử lý làm trọng tâm. Người quản lý tìm và lọc `Hóa đơn`, mở chi tiết, ghi nhận `Giao dịch thu`, hoàn tác giao dịch sai và thấy `Công nợ`/trạng thái được cập nhật nhất quán. Cổng sinh viên và các màn hình đang tham chiếu công nợ cùng đọc từ Thu phí/Hóa đơn Module sâu, sử dụng Razor, CSS và JavaScript thuần trên ASP.NET Core MVC và SQL Server hiện hữu.

## User Stories

1. Là quản lý KTX, tôi muốn thấy tổng số và tổng tiền của hóa đơn quá hạn, để ưu tiên công việc thu hồi công nợ.
2. Là quản lý KTX, tôi muốn thấy hóa đơn sắp đến hạn, để chủ động nhắc sinh viên.
3. Là quản lý KTX, tôi muốn phân biệt hóa đơn chờ thu, thu một phần, đã thu đủ, quá hạn và đã hủy, để hiểu trạng thái toàn bộ kỳ thu.
4. Là quản lý KTX, tôi muốn tìm theo mã hóa đơn, mã sinh viên, họ tên hoặc mã phòng, để định vị nhanh trường hợp đang xử lý.
5. Là quản lý KTX, tôi muốn lọc theo kỳ thu, trạng thái, tòa và khoảng công nợ, để thu hẹp danh sách công việc.
6. Là quản lý KTX, tôi muốn hóa đơn quá hạn đứng trước mặc định, để việc khẩn không bị chìm dưới hóa đơn mới.
7. Là quản lý KTX, tôi muốn danh sách có phân trang và tổng đếm ổn định, để màn hình vẫn nhanh khi dữ liệu tăng.
8. Là quản lý KTX, tôi muốn mở một hóa đơn mà không mất bộ lọc hiện tại, để xử lý liên tiếp nhiều trường hợp.
9. Là quản lý KTX, tôi muốn xem sinh viên, lượt lưu trú, phòng, kỳ thu, hạn và các dòng phí cùng chỗ, để xác minh công nợ trước khi nhận tiền.
10. Là quản lý KTX, tôi muốn thấy rõ tổng hóa đơn, đã thu và còn phải thu, để không nhầm khoản phải thu với khoản đã nhận.
11. Là quản lý KTX, tôi muốn ghi nhận khoản thu tiền mặt hoặc chuyển khoản, để thanh toán ngoại tuyến được phản ánh vào hệ thống.
12. Là quản lý KTX, tôi muốn hệ thống bắt buộc số tiền, phương thức và thời điểm nhận, để mỗi khoản thu đủ dữ liệu đối soát.
13. Là quản lý KTX, tôi muốn ghi mã tham chiếu ngân hàng cho chuyển khoản, để khớp khoản thu với sao kê.
14. Là quản lý KTX, tôi muốn việc gửi lại cùng một thao tác chỉ tạo tối đa một khoản thu, để retry hoặc nhấn đúp không làm tăng tiền.
15. Là quản lý KTX, tôi muốn chấp nhận số tiền nhỏ hơn hoặc bằng công nợ, để hỗ trợ thu một phần mà không vô tình thu vượt.
16. Là quản lý KTX, tôi muốn công nợ và trạng thái cập nhật ngay sau khi ghi nhận, để xác nhận kết quả trước khi xử lý người tiếp theo.
17. Là quản lý KTX, tôi muốn mỗi khoản thu hiển thị người ghi nhận và thời điểm, để hành động của nhân viên có thể kiểm tra.
18. Là quản lý KTX, tôi muốn hoàn tác khoản thu sai với lý do bắt buộc, để giữ nguyên dấu vết đối soát.
19. Là quản lý KTX, tôi muốn giao dịch gốc và giao dịch hoàn tác liên kết với nhau, để giải thích được vì sao tổng tiền thay đổi.
20. Là quản lý KTX, tôi muốn thao tác trên dữ liệu cũ thất bại an toàn và yêu cầu tải lại, để hai người không âm thầm ghi đè trạng thái tiền.
21. Là quản lý KTX, tôi muốn trạng thái rỗng hướng dẫn xóa hoặc đổi bộ lọc, để khôi phục danh sách nhanh.
22. Là quản lý KTX, tôi muốn lỗi nhập liệu giữ lại giá trị đã điền, để sửa nhanh mà không nhập lại.
23. Là quản lý KTX, tôi muốn thông báo thành công ngắn gọn kèm mã biên nhận, để xác nhận kết quả với sinh viên.
24. Là sinh viên, tôi muốn trạng thái hóa đơn và công nợ khớp với màn quản lý, để tin cậy cổng sinh viên.
25. Là sinh viên, tôi muốn thấy lịch sử khoản thu của hóa đơn mình, để xác nhận tiền đã được KTX ghi nhận.
26. Là sinh viên, tôi muốn không thể xem hóa đơn người khác bằng cách đổi URL, để dữ liệu tài chính cá nhân được bảo vệ.
27. Là quản lý KTX, tôi muốn sinh viên nhận thông báo trong ứng dụng khi khoản thu được ghi nhận hoặc hoàn tác, để biết công nợ vừa thay đổi.
28. Là quản lý KTX, tôi muốn màn xét đơn dùng cùng cách tính công nợ, để kiểm tra tài chính không mâu thuẫn với màn Thu phí.
29. Là quản trị viên, tôi muốn chỉ vai trò quản lý được phép ghi nhận hoặc hoàn tác khoản thu, để quyền xem không đồng nghĩa với quyền sửa.
30. Là người vận hành, tôi muốn tiền, ngày và nhãn trạng thái theo định dạng tiếng Việt thống nhất, để quét thông tin nhanh và ít nhầm.
31. Là người vận hành trên điện thoại, tôi muốn danh sách và chi tiết hóa đơn dùng được trên màn hình hẹp, để xác minh khoản thu khi rời bàn làm việc.
32. Là kỹ sư hỗ trợ, tôi muốn thao tác tài chính được ghi log nhưng không lộ bí mật hoặc dữ liệu thanh toán nhạy cảm, để chẩn đoán lỗi an toàn.

## Implementation Decisions

- Xây một Thu phí/Hóa đơn Module sâu. Interface biểu đạt thao tác và kết quả nghiệp vụ; Implementation sở hữu tính bất biến của hóa đơn, ghi `Giao dịch thu`, hoàn tác, tính `Công nợ`, trạng thái và transaction database.
- Tuân theo ADR 0001. `Giao dịch thu` không được sửa hoặc xóa; sửa sai bằng bản ghi hoàn tác liên kết và lý do bắt buộc.
- Tách vòng đời hóa đơn khỏi trạng thái thanh toán. Phát hành/hủy là trạng thái vòng đời; chờ thu, thu một phần, đã thu đủ và quá hạn được suy ra từ `Công nợ` cùng hạn của `Kỳ thu`.
- Thêm dữ liệu bền vững cho `Giao dịch thu`: hóa đơn, mã biên nhận, số tiền có dấu, phương thức, thời điểm nhận, thời điểm ghi nhận, tài khoản ghi nhận, mã tham chiếu ngoài tùy chọn, khóa idempotency, giao dịch gốc tùy chọn và ghi chú/lý do.
- Interface chỉ nhận khoản thu dương. Implementation tạo hoàn tác như một khoản bù có dấu và chỉ cho phép hoàn tác một giao dịch gốc còn hiệu lực.
- Không cho phép thu vượt trong sprint này. Số tiền được chấp nhận không lớn hơn `Công nợ` tại thời điểm commit.
- Mã biên nhận là duy nhất và được cấp số nguyên tử tại database; không dùng số lượng dòng để tạo mã.
- Khóa idempotency là duy nhất để browser retry hoặc submit trùng trả về kết quả trước đó thay vì ghi thêm tiền.
- Xác minh công nợ, thêm giao dịch, tạo thông báo và cập nhật concurrency trong một EF Core transaction.
- Dùng `TimeProvider` sau một Seam thật để thống nhất UTC và ngày nghiệp vụ Asia/Saigon; kiểm thử dùng fake Adapter.
- Thêm Worklist Module cho quản lý, chiếu dữ liệu lọc, sắp xếp và phân trang trực tiếp trong SQL. Module sở hữu thứ tự ưu tiên và tổng đếm.
- Thứ tự mặc định: quá hạn trước, rồi hạn gần nhất, rồi hóa đơn mới nhất. Bộ lọc và hóa đơn đang chọn nằm trong URL để refresh/back không mất ngữ cảnh.
- Hành động chính là “Ghi nhận thu” khi còn công nợ. Hóa đơn đã đủ hoặc đã hủy không có hành động này. “Hoàn tác” là hành động phụ chỉ xuất hiện trên khoản thu còn hiệu lực và đúng quyền.
- Dùng panel chi tiết bên phải trên desktop và bề mặt chi tiết riêng trên mobile, đồng bộ hệ thống giao diện quản lý hiện hữu và giữ danh sách là trọng tâm.
- Tái sử dụng vai trò ASP.NET Core Identity hiện có. `Admin` và `DormManager` được xem, ghi nhận và hoàn tác; nếu sau này cần tách vai trò thu ngân sẽ là quyết định riêng.
- Dữ liệu sinh viên được giới hạn bởi `Tài khoản người dùng` đăng nhập liên kết với `Hồ sơ sinh viên`; chỉ có ID trên URL không tạo quyền truy cập.
- Thay mọi phép tính trực tiếp `TotalAmount - PaidAmount` ở caller bằng kết quả của Thu phí/Hóa đơn Module trong quá trình migration.
- Tạo một `Giao dịch thu` mở đầu cho mỗi hóa đơn cũ có tổng đã trả dương, kiểm tra đối soát rồi ngừng ghi trường tổng cũ. Nếu dữ liệu triển khai chỉ là demo có thể xóa trường cũ trong cùng migration; dữ liệu thật dùng migration hai bước.
- Chỉ tạo `Thông báo` sau khi khoản thu/hoàn tác commit. Delivery realtime là best-effort và không quyết định thành công của transaction.
- Giữ toàn bộ khóa và cấu hình server ngoài Razor/JavaScript; sprint này không thêm khóa nhà cung cấp thanh toán.

## Testing Decisions

- Kiểm thử tốt chỉ xác nhận hành vi qua Interface của Module hoặc bề mặt HTTP, không bám vào private method, hình dạng truy vấn EF hay selector CSS không liên quan hành vi người dùng.
- Unit-test kết quả nghiệp vụ của Thu phí/Hóa đơn Module với đồng hồ giả: chưa thu, thu một phần, thu đủ, quá hạn trước/sau ranh giới ngày, số tiền sai, thu vượt, hủy và hoàn tác.
- Integration-test Adapter EF Core trên database quan hệ: duy nhất hóa đơn theo kỳ, duy nhất khóa idempotency, duy nhất mã biên nhận, rollback transaction và hai người vận hành đồng thời.
- Xác minh gửi lại cùng khóa idempotency chỉ tạo một giao dịch có hiệu lực và một lần thay đổi công nợ.
- Xác minh request cũ bị từ chối và không thể gây thu vượt sau khi người khác đã ghi trước.
- Xác minh không thể hoàn tác lặp hoặc hoàn tác quá giao dịch gốc, đồng thời bản ghi kiểm tra gốc vẫn còn.
- Xác minh bộ lọc, thứ tự ưu tiên, tổng đếm và phân trang trên bộ dữ liệu lớn hơn một trang.
- Xác minh riêng quyền xem, ghi nhận và hoàn tác của quản lý.
- Xác minh sinh viên không thể truy cập trực tiếp hóa đơn của sinh viên khác.
- Xác minh chi tiết quản lý, trang hóa đơn sinh viên và màn xét đơn trả cùng công nợ/trạng thái cho cùng một hóa đơn.
- Mở rộng mẫu transaction smoke test của luồng xét đơn để kiểm tra ghi nhận, retry và hoàn tác.
- Mở rộng mẫu browser capture hiện có cho desktop quản lý, mobile quản lý và trang hóa đơn sinh viên cập nhật.
- Giữ kiểm thử đối soát migration chứng minh giao dịch mở đầu bằng tổng đã trả cũ trước khi loại trường legacy.

## Out of Scope

- Cổng thanh toán online, webhook, hoàn tiền qua thẻ/ngân hàng và lưu khóa nhà cung cấp thanh toán.
- Nhập hoặc tự động đối soát sao kê ngân hàng.
- Phát hành hóa đơn hàng loạt, luật phí định kỳ, nhập chỉ số điện nước và chỉnh danh mục phí.
- In hóa đơn thuế hoặc hóa đơn điện tử có giá trị pháp lý.
- Phí phạt chậm, xóa nợ, học bổng, giảm trừ và đa tiền tệ.
- Tích hợp sổ cái kế toán và chốt ca thu ngân chính thức.
- Xuất bảng tính và báo cáo tài chính nâng cao.
- Nền tảng thông báo realtime tổng quát ngoài delivery best-effort cho sự kiện khoản thu của sprint.
- Refactor toàn bộ Module hiện hữu; chỉ thay caller cần thiết để có một định nghĩa `Công nợ`.

## Further Notes

- Dữ liệu demo hiện có `Kỳ thu` tháng 08 và 09/2026 cùng hóa đơn của sinh viên `SV002106`; hóa đơn tháng 09 là 1.525.000 đồng và tháng 08 đã thu đủ. Đây là dữ liệu nghiệm thu xác định sau backfill legacy.
- Công việc quan trọng nhất của màn hình là xử lý công nợ quá hạn chính xác; summary, thứ tự, chi tiết và hành động phải xoay quanh mục tiêu này.
- Sau sprint này, hạng mục hardening giá trị nhất là Module dùng chung cho `Chỗ có thể xếp`, rồi đến state machine của `Đơn lưu trú`.

## Implementation Checkpoint — 10/09/2026

- Đã triển khai Worklist quản lý, chi tiết hóa đơn, ghi nhận tiền mặt/chuyển khoản, hoàn tác có lý do, lịch sử đối soát và trang hóa đơn sinh viên dùng chung `BillingLedgerService`.
- Đã áp dụng migration `20260909184140_AddBillingLedger`, backfill dữ liệu cũ và xác minh database demo có 10 hóa đơn, 4 giao dịch nền, tổng thu ròng 4.023.000 đồng; không còn dữ liệu E2E sau cleanup.
- `dotnet test DormitoryManagementSystem.sln -c Release`: 6/6 policy tests qua, build 0 warning/0 error.
- `scripts/smoke-billing-ledger.cjs`: ghi khoản thu, replay idempotent, chặn thu vượt và hoàn tác đều qua.
- Các capture/smoke của room map, cổng sinh viên, xét đơn và vòng đời tài khoản đều qua; ảnh desktop/mobile Thu phí không có page error, console error hay request lỗi.
- Phạm vi còn lại trong Testing Decisions như stress nhiều writer và bộ dữ liệu phân trang quy mô lớn là hardening tiền-production, không chặn nghiệm thu vertical slice đồ án hiện tại.
