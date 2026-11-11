---
status: accepted
---

# Dùng sổ Giao dịch thu bất biến cho nghiệp vụ Thu phí/Hóa đơn

`Công nợ` và trạng thái thanh toán phải được suy ra từ `Hóa đơn`, hạn của `Kỳ thu` và các `Giao dịch thu` còn hiệu lực thay vì sửa trực tiếp một tổng `PaidAmount` hoặc duy trì nhiều cách tính trạng thái. Thu phí/Hóa đơn Module sẽ sở hữu Seam nghiệp vụ duy nhất; EF Core là Adapter lưu trữ và sai sót được hoàn tác bằng bản ghi bù liên kết để giữ lịch sử đối soát.

## Consequences

- Dữ liệu tiền có lịch sử kiểm tra được và thao tác retry có thể chống ghi trùng.
- Màn quản lý, cổng sinh viên và xét `Đơn lưu trú` dùng cùng một định nghĩa `Công nợ`.
- Cần migration thêm `Giao dịch thu`; trường tổng đã trả và trạng thái thanh toán cũ chỉ được giữ tạm trong quá trình chuyển đổi rồi loại bỏ khi dữ liệu đã đối soát.
