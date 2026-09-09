# Prompt set — Dashboard KTX

Ba ảnh được tạo bằng built-in ImageGen, taxonomy `ui-mockup`, khung 1536 × 1024. Đây là ảnh duyệt bố cục, không phải mã giao diện.

## Ràng buộc chung

- Dashboard desktop bằng tiếng Việt cho `NẾP KTX`, cơ sở Thủ Đức, học kỳ I 2026–2027.
- Dữ liệu chính: 1.142/1.280 chỗ; còn 138 chỗ; 12 đơn chờ duyệt; 7 hóa đơn quá hạn với 18.450.000 đ; 5 sự cố ưu tiên cao; đã thu phí 86%.
- UI sản phẩm có thể triển khai bằng Razor Views, CSS Grid/Flexbox và JavaScript thuần.
- Nền sáng ấm; chữ navy/graphite; một màu action; màu semantic luôn có nhãn chữ.
- Không dùng glassmorphism, neon, gradient trang trí, title quá lớn, donut chart hay dãy KPI card đồng dạng.

## Prompt A — Hàng đợi ưu tiên

Tạo Dashboard dạng trung tâm xử lý công việc: rail điều hướng 72 px, cột `Ca trực hôm nay` 270 px, hàng đợi `Cần xử lý trước 10:00` là vùng lớn nhất, cột phải là sổ công suất và tiến độ thu phí. Mỗi hàng có loại việc, trạng thái, người phụ trách/thời gian tồn và hành động. Hàng được chọn mở rộng tại chỗ để hiện `Đủ điều kiện · A-305 còn 2 chỗ`; không dùng modal.

## Prompt B — Bản đồ vận hành

Tạo Dashboard không có sidebar cố định, dùng điều hướng ngang. Ma trận `Tình trạng phòng theo tòa` chiếm 70% màn hình, gồm bốn tòa A–D, hàng tầng và các ô phòng có dấu chỗ ở. Inspector bên phải cho `Tòa A · Tầng 3`, kết nối A-301 đang bảo trì, A-302/A-305 còn chỗ với đơn Nguyễn Minh Anh. Dải dưới gom ngoại lệ theo vị trí.

## Prompt C — Bảng bàn giao theo trạng thái

Tạo Dashboard dạng bảng quy trình bốn lane: `Cần quyết định`, `Đang xử lý`, `Chờ phản hồi`, `Hoàn tất hôm nay`. Thẻ nghiệp vụ thuộc `Đơn phòng`, `Thu phí`, `Sự cố`, có thời gian tồn, người giữ việc và bước tiếp theo. Một thẻ được mở rộng inline. Cuối màn hình có thanh `Bàn giao ca` cố định tổng hợp việc chưa hoàn tất.

