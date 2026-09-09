# Option 2 — Bản chỉnh sửa để duyệt

> Trạng thái: chỉ là mockup. Chưa triển khai Razor, backend, database hoặc chức năng thật.

## Ý định

- **Người dùng:** nhân viên quản lý KTX làm việc hằng ngày trên desktop và kiểm tra nhanh bằng mobile.
- **Tác vụ chính:** tìm phòng có thể nhận thêm sinh viên mà không bỏ sót chỗ đang giữ hoặc sức chứa bị khóa do bảo trì.
- **Focal point:** sơ đồ phòng của một tòa đang chọn; cảnh báo, điều hướng và panel chi tiết đều phục vụ sơ đồ.
- **Vibe:** campus sáng, thân thiện, thực dụng; teal chỉ dành cho selection và hành động chính.

## Hợp đồng dữ liệu mockup

| Phạm vi | Sức chứa | Đang ở | Đang giữ | Có thể xếp | Tạm khóa |
|---|---:|---:|---:|---:|---:|
| Toàn KTX | 1.280 | 1.142 | 24 | 94 | 20 |
| Tòa A | 320 | 304 | 4 | 4 | 8 |
| Tòa B | 320 | 282 | 8 | 26 | 4 |
| Tòa C | 320 | 258 | 7 | 51 | 4 |
| Tòa D | 320 | 298 | 5 | 13 | 4 |
| Tòa A · Tầng 3 | 64 | 51 | 1 | 4 | 8 |
| Phòng A-302 | 8 | 6 | 0 | 2 | 0 |

Các dòng đều thỏa `Sức chứa = Đang ở + Đang giữ + Có thể xếp + Tạm khóa`.

### Tòa A

- 5 tầng, mỗi tầng 8 phòng, mỗi phòng 8 chỗ: `5 × 8 × 8 = 320`.
- Tầng 1, 2 và 4: mỗi tầng có một phòng `7/8` với `1 chỗ đang giữ`; các phòng còn lại đầy.
- Tầng 5: toàn bộ phòng đầy.
- Tầng 3: A-301 bảo trì và khóa 8 chỗ; A-302 và A-305 mỗi phòng còn 2 chỗ; A-304 giữ 1 chỗ; các phòng còn lại đầy.
- Không có phòng mã B xuất hiện trong tòa A.

## Thay đổi chính

1. Sidebar gọn, có điều khiển thu lại; header chỉ giữ tìm kiếm, cơ sở, thông báo và tài khoản.
2. Dải tòa chỉ hiển thị số chỗ có thể xếp; chỉ tòa A mở sơ đồ chi tiết.
3. Mỗi phòng dùng ba dòng chữ thay dãy chấm: mã, tỷ lệ đang ở và trạng thái.
4. Bộ lọc gồm tầng, loại phòng, giá, tình trạng, lựa chọn nhanh còn chỗ và trợ giúp khi không có kết quả.
5. Panel A-302 có số liệu tách bạch, tab thông tin và một hành động chính `Xếp sinh viên`.
6. Mobile chỉ hiển thị Tầng 3; chạm A-302 mở màn hình chi tiết riêng.
7. Cảnh báo được thu gọn thành một dải để không tranh tiêu điểm với sơ đồ.

## Token dự kiến

- Primary: `#0F766E`
- Page: `#F5F7F6`
- Surface: `#FFFFFF`
- Text: `#1F2937`
- Border: `#E2E8E5`
- Spacing base: 4px; nhóm chính theo nhịp 8/12/16/24px.
- Radius: 8px control, 10–12px panel.
- Body desktop: 14–16px; vùng chạm tối thiểu 44px.

## Prompt set đã dùng

- **Desktop:** sửa Option 2 thành layout sidebar + header + cảnh báo + dải tòa + bộ lọc + sơ đồ 5 tầng của riêng tòa A + panel A-302; bắt buộc đủ 40 phòng và khớp phép cộng công suất.
- **Mobile:** dùng desktop làm tham chiếu hệ thống; tạo một ảnh gồm trạng thái xem riêng Tầng 3 và màn hình chi tiết A-302, không thu nhỏ nguyên desktop.
- **Công cụ:** built-in ImageGen, taxonomy `ui-mockup`; một lượt chỉnh có mục tiêu được dùng để bổ sung đủ 8 phòng ở mỗi tầng desktop.

## Database cho giai đoạn sau

SQL Server instance do người dùng cung cấp: `DESKTOP-OSSBOT1\TUANANH`.

Chưa kiểm tra kết nối, chưa tạo database và chưa ghi dữ liệu vì thiết kế chưa được duyệt triển khai. Khi được duyệt, cấu hình nên nằm trong User Secrets hoặc biến môi trường thay vì commit thông tin kết nối nhạy cảm.

