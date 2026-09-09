# Checkpoint triển khai — Sơ đồ phòng

## Phạm vi đã làm

- ASP.NET Core 8 MVC, Razor Views, CSS thuần và JavaScript thuần.
- Identity cookie với vai trò `Admin`, `DormManager`, `Student`.
- EF Core Code First, migration đầu tiên và SQL Server `DESKTOP-OSSBOT1\TUANANH`.
- Dữ liệu demo 4 tòa, 20 tầng, 160 phòng, 1.142 lượt lưu trú hiện tại, giữ chỗ và khóa chỗ do bảo trì.
- Trang Sơ đồ phòng: chọn tòa, lọc, trạng thái rỗng, chọn phòng, panel chi tiết và các tab phụ.
- Mobile: một tầng tại một thời điểm; chi tiết phòng là màn hình toàn màn hình.

## Đối chiếu với option 2 đã duyệt

| Tiêu chí | Kết quả chạy thật |
|---|---|
| Sơ đồ một tòa là trọng tâm | Đạt — chỉ tòa đang chọn được mở theo 5 tầng |
| Tòa hiển thị số chỗ có thể xếp | Đạt — A: 4, B: 26, C: 51, D: 13 |
| Phòng có mã, sức chứa và nhãn trạng thái | Đạt — không dùng dãy chấm; trạng thái luôn có chữ |
| Phòng chọn rõ ràng | Đạt — viền teal, nền nhấn và nhãn “Đang chọn” |
| Bảo trì không tính là chỗ có thể xếp | Đạt — A-301 khóa 8 chỗ, toàn KTX khóa 20 chỗ |
| Panel phòng khớp sơ đồ | Đạt — A-302: 6/8 đang ở, còn 2 chỗ, 1.250.000 đ/tháng |
| Bộ lọc nghiệp vụ và trạng thái rỗng | Đạt — tầng, loại, giá, tình trạng, chỉ còn chỗ và xóa lọc |
| Việc quá hạn/khẩn cấp dễ thấy nhưng không lấn át sơ đồ | Đạt — dải ba mục trên desktop, tóm tắt gọn trên mobile |
| Mobile một tầng và chi tiết riêng | Đạt — tầng 3 hiển thị 8 phòng; click phòng mở màn hình chi tiết |
| Không Bootstrap/jQuery/frontend framework | Đạt — Views chỉ tham chiếu `site.css` và `site.js` tự viết |

## Bằng chứng

- `artifacts/screenshots/room-map-desktop.png`
- `artifacts/screenshots/room-map-mobile.png`
- `scripts/capture-room-map.cjs` vừa chụp ảnh vừa kiểm tra nhãn tòa, tổng số liệu, panel chọn, empty state và số tầng hiển thị trên mobile.
- `/health` trả `{"status":"Healthy","database":"Connected"}`.

## Chưa làm tại checkpoint này

Các nút sang Đơn đăng ký, Thu phí, Sự cố, Sinh viên và “Xếp sinh viên” hiện chỉ thông báo phạm vi demo. Backend giao dịch phân/chuyển phòng, hóa đơn, sửa chữa, các kiểm thử cạnh tranh và chatbot sinh viên sẽ được triển khai ở các checkpoint tiếp theo sau khi màn hình này được duyệt.
