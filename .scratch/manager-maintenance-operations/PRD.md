# Sprint 3: Sự cố, bảo trì và sức chứa an toàn

Status: ready-for-human

## Problem Statement

Người quản lý chưa có nơi điều phối các `Sự cố` theo mức khẩn cấp, SLA, tổ phụ trách và ảnh hưởng thực tế đến `Chỗ có thể xếp`. Trước sprint này, công thức sức chứa nằm rải rác ở sơ đồ phòng, tìm phòng và xét `Đơn lưu trú`, nên một sự cố khóa giường có thể được hiển thị hoặc kiểm tra khác nhau giữa các luồng.

## Solution

Xây một bàn điều phối ba vùng: hàng đợi ưu tiên, bằng chứng phòng đang chọn và rail cập nhật xử lý. Đồng thời đưa công thức `Sức chứa - Chỗ đang ở - Chỗ đang giữ - Chỗ tạm khóa` vào `RoomCapacityPolicy` dùng chung. Khi quản lý giải quyết/đóng sự cố, chỗ tạm khóa được trả về ngay và mọi caller nhìn thấy cùng một kết quả.

## Delivered user stories

1. Quản lý thấy số sự cố đang mở, khẩn cấp, chưa phân công, vượt SLA và tổng chỗ đang tạm khóa.
2. Quản lý tìm theo mã/phòng/nội dung, lọc theo tiến độ, ưu tiên và tòa.
3. Công việc khẩn cấp và quá SLA đứng trước để không bị chìm trong hàng đợi.
4. Chi tiết hiển thị phòng, người báo, tổ phụ trách, mô tả, lịch sử và năm thành phần sức chứa.
5. Quản lý phân công tổ, đổi tiến độ/ưu tiên, cập nhật chỗ tạm khóa và bắt buộc ghi chú.
6. Trạng thái đang xử lý/chờ vật tư bắt buộc có tổ phụ trách.
7. Không thể tạm khóa nhiều hơn phần sức chứa còn lại sau người đang ở và các sự cố khác.
8. Giải quyết hoặc đóng sự cố tự trả chỗ tạm khóa về phòng.
9. Mỗi cập nhật thêm một dòng lịch sử có người thực hiện, thời gian và nội dung; sinh viên báo sự cố nhận thông báo.
10. Form cũ bị từ chối bằng row version để hai quản lý không âm thầm ghi đè nhau.
11. Sinh viên không thể truy cập màn điều phối của quản lý.
12. Desktop giữ ba vùng cùng lúc; mobile ưu tiên danh sách rồi chi tiết, với nút nhảy trực tiếp đến form cập nhật.

## Implementation decisions

- ASP.NET Core MVC, Razor, CSS và JavaScript thuần; không thêm frontend framework.
- `MaintenanceManagementService` sở hữu worklist SQL, quy tắc chuyển trạng thái, transaction, audit và thông báo.
- Cập nhật chạy trong serializable transaction qua EF Core execution strategy và optimistic concurrency.
- `MaintenanceUpdate` là nhật ký append-only; không sửa lịch sử cũ.
- `RoomCapacityPolicy` là Seam duy nhất cho các phép tính sức chứa ở room map, student portal và application approval.
- Thời gian lưu UTC và trình bày theo múi giờ nghiệp vụ Asia/Saigon; nhãn tuổi dữ liệu được chặn an toàn nếu timestamp lệch tương lai.
- Migration `AddMaintenanceWorkflow` bổ sung tổ phụ trách, actor của nhật ký và row version.

## Verification decisions

- Unit test ma trận sức chứa, bao gồm clamp dữ liệu biên và không cho assign khi sự cố khóa hết phần còn lại.
- Browser capture kiểm tra nội dung, responsive, lỗi JavaScript/request và quyền Student.
- Smoke test giải quyết `SC-0284`, xác minh A-301 đổi từ 8 chỗ tạm khóa/0 chỗ có thể xếp thành 0/8 trên cả service và sơ đồ phòng.
- Gửi lại row version cũ không được thêm update thứ hai.
- Test luôn hoàn tác dữ liệu và xác minh không để lại update/notification E2E.
- Chạy lại toàn bộ capture/smoke của cổng sinh viên, xét đơn, tài khoản và thu phí trước khi đóng sprint.

## Deferred

- Upload ảnh hiện trường và file đính kèm.
- Lịch bảo trì định kỳ, tồn kho vật tư và nhà cung cấp.
- SLA cấu hình theo cơ sở/thể loại thay vì hằng số nghiệp vụ.
- Realtime push; hiện tại thông báo được ghi bền vững trong database.
