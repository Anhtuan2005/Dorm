# Sprint 4: Vòng đời Đơn lưu trú và bàn giao xử lý

Status: ready-for-human

## Problem Statement

Trước Sprint 4, trạng thái, SLA và nhãn của `Đơn lưu trú` bị lặp giữa cổng quản lý và cổng sinh viên. Quản lý có thể đi thẳng từ đơn mới sang duyệt, sinh viên không có cách trả lời yêu cầu bổ sung, và luồng trả phòng chưa hoàn chỉnh. Đơn chuyển/trả phòng cũng không ghim `Lượt lưu trú` nguồn nên một quyết định muộn có thể tác động sai dữ liệu đã thay đổi.

## Solution

Đưa state machine vào Module `HousingRequestWorkflow`, coi `Lượt xử lý` và `Mốc chờ hành động` là dữ kiện nghiệp vụ chung. Mọi mutation quản lý đi qua một Interface thực thi action; cổng sinh viên sở hữu action bổ sung/hủy/gửi trả phòng. Worklist chiếu, lọc, ưu tiên và phân trang ở SQL. Mỗi quyết định dùng row version và transaction; đơn ghim stay nguồn và stay mới lưu provenance về đơn đã duyệt.

## Delivered user stories

1. Quản lý thấy bên đang giữ `Lượt xử lý`, việc tiếp theo và SLA thật của từng đơn.
2. Đơn mới phải được “Nhận xử lý” trước khi có thể duyệt.
3. Quản lý có thể yêu cầu thông tin cụ thể; hành động duyệt bị thay bằng lý do đang chờ sinh viên.
4. Sinh viên thấy yêu cầu bổ sung ngay trong đơn, trả lời từ 10–600 ký tự và chuyển lượt lại Ban quản lý.
5. Cả hai phía xem cùng lịch sử append-only gồm trạng thái, ghi chú, người thực hiện và thời gian.
6. Sinh viên chưa có phòng gửi đơn đăng ký ở; sinh viên đang ở gửi đơn chuyển phòng.
7. Sinh viên đang ở có thể gửi đơn trả phòng; quản lý xác nhận để chỉ kết thúc đúng stay nguồn.
8. Nếu stay nguồn đổi sau khi gửi đơn, hệ thống từ chối duyệt thay vì tác động stay mới.
9. Stay do duyệt đăng ký/chuyển phòng lưu lại request nguồn.
10. Queue quản lý lọc, sắp xếp ưu tiên và phân trang trong database.
11. Mã đơn được cấp bằng SQL sequence, không còn rủi ro trùng từ `Count + 1`.
12. Form manager/student dùng row version để từ chối cập nhật từ giao diện cũ.

## Implementation decisions

- Giữ ASP.NET Core MVC, Razor, CSS và JavaScript thuần.
- `HousingRequestWorkflow` là Seam sâu cho state machine/presentation/SLA; Controller chỉ ánh xạ HTTP intent.
- `ApplicationManagementService.ExecuteAsync` là Interface quản lý duy nhất cho action workflow.
- `SourceStayId` là snapshot định danh, không suy luận lại từ stay hiện tại khi quyết định.
- `Stay.HousingRequestId` là provenance duy nhất cho đầu ra duyệt đăng ký/chuyển phòng.
- Thời gian lưu UTC qua `TimeProvider`; SLA quản lý không chạy khi `Lượt xử lý` thuộc sinh viên.
- Không thêm generic repository; EF Core là Adapter persistence trực tiếp của Module.

## Verification decisions

- Policy tests bao phủ transition hợp lệ/không hợp lệ và ranh giới SLA.
- Browser smoke test hai vai trò bao phủ `Submitted → UnderReview → NeedsInformation → Submitted` và audit bổ sung.
- Browser smoke test trả phòng bao phủ submit, receive, approve, kết thúc stay nguồn và portal không còn phòng hiện tại.
- Test transaction chuyển phòng tiếp tục xác minh sức chứa sống, tạo stay, thông báo và rollback dữ liệu.
- Capture desktop/mobile kiểm tra handoff strip, form bổ sung, không có lỗi console/page/request.

## Deferred

- File đính kèm cho nội dung bổ sung.
- Gán hồ sơ cho một quản lý cụ thể thay vì trách nhiệm theo vai trò.
- SLA cấu hình theo cơ sở/loại đơn và lịch làm việc.
- Realtime push và tích hợp chatbot với dữ liệu cá nhân.
