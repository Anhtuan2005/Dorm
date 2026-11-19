# Rà soát PlanNhom9.xlsx

Đối chiếu 28 user story duy nhất từ hai sheet với các luồng của ứng dụng. Điểm Sprint 3 theo từng dòng là 19. Công thức tổng ở C39 dừng ở dòng 33 nên bỏ sót 8 điểm của năm story thu phí ở dòng 34–38. Các công thức tra cứu trong Product backlog có cùng giới hạn và trả `#N/A` cho năm story đó.

| Story | Kết quả đối chiếu | Nơi kiểm tra |
|---|---|---|
| US-19 | Đã có đăng nhập, đăng xuất, phân quyền Admin, Ban quản lý và sinh viên. | Đăng nhập, menu tài khoản, truy cập URL theo vai trò |
| US-04 | Đã có đổi mật khẩu và kiểm tra mật khẩu hiện tại. | Tài khoản → Đổi mật khẩu |
| US-26 | Đã sửa sơ đồ để đọc danh sách tòa và tầng từ dữ liệu thực tế, giữ bộ lọc khi chuyển tầng và hiển thị đúng chỗ có thể xếp khi chỉ khóa một phần phòng. | Sơ đồ phòng trên desktop và mobile |
| US-11 | Đã bổ sung sửa số tầng, chặn trùng trong cùng tòa và bảo toàn liên kết phòng khi đổi số tầng. | Sơ đồ phòng → Danh mục phòng → chọn tầng → Sửa tầng |
| US-02 | Đã có tạo và chỉnh sửa hồ sơ trước hoặc sau khi cấp tài khoản. | Sinh viên → Thêm sinh viên, Sửa hồ sơ |
| US-21 | Đã có cấp tài khoản và đặt lại mật khẩu theo hồ sơ. | Sinh viên → chi tiết hồ sơ |
| US-08 | Đã sửa luồng đăng xuất khi bị khóa để từng tab đều hiển thị đúng lý do. Khóa do nhập sai nhiều lần có thông báo riêng. | Hai tab sinh viên và một tab chặn SignalR, thao tác khóa từ Admin |
| US-15 | Đã thêm phân trang và tổng kết quả. Giao diện giải thích điều kiện còn chỗ và loại phòng theo hồ sơ. Ảnh và chọn giường vẫn mở từ từng phòng. | Cổng sinh viên → Tìm phòng |
| US-03 | Đã có gửi đơn, xem tiến độ, bổ sung và hủy. Đã đồng nhất so sánh loại phòng khi gửi với quy tắc ở màn quản lý. | Cổng sinh viên → Đăng ký của tôi |
| US-24 | Đã có nhận xử lý, yêu cầu bổ sung và duyệt. Nội dung bổ sung xuất hiện trong đơn và thông báo của Ban quản lý. | Đơn lưu trú → chi tiết đơn và hộp thông báo |
| US-06 | Đã có giữ đúng số giường khi duyệt và xác nhận nhận phòng riêng. | Đơn lưu trú → Duyệt và giữ chỗ → Xác nhận nhận phòng |
| US-28 | Đã có xác nhận chuyển phòng, kết thúc lượt cũ và duyệt trả phòng. | Đăng ký của tôi và Đơn lưu trú |
| US-25 | Đã có báo sự cố tại phòng đang ở, xem trạng thái và lịch sử cập nhật. | Cổng sinh viên → Yêu cầu hỗ trợ |
| US-07 | Đã có tải ảnh và xem ảnh qua kiểm tra quyền sở hữu hoặc vai trò. | Yêu cầu hỗ trợ và Sự cố & bảo trì |
| US-20 | Đã có đặt mức ưu tiên, phân công và cập nhật tiến độ. Đã đồng bộ nhãn ưu tiên và trạng thái lên chi tiết phòng. | Sự cố & bảo trì → chi tiết sự cố |
| US-12 | Đã có khóa số giường, kiểm tra xung đột với lượt ở/giữ chỗ và giải phóng khi xử lý xong. Sơ đồ hiển thị phần giường còn có thể xếp. | Sự cố & bảo trì, Sơ đồ phòng, Tìm phòng |
| US-27 | Đã có tổng quan sức chứa, công nợ và việc cần xử lý. Đã cập nhật số hóa đơn quá hạn trực tiếp khi thu hoặc hoàn tác. | Tổng quan và Thu phí |
| US-05 | Đã có lọc theo kỳ thu và tòa. Đã xử lý trường hợp chưa có kỳ thu để vẫn xem được sức chứa. | Báo cáo → Kỳ báo cáo, Phạm vi |
| US-17 | Đã có xuất CSV theo bộ lọc. Đã bổ sung số liệu đơn lưu trú và sự cố, hỗ trợ xuất sức chứa khi chưa có kỳ thu. | Báo cáo → Xuất CSV |
| US-10 | Đã có xem, đánh dấu từng mục và tất cả. Đã thêm phân trang để đọc được thông báo cũ. | Cổng sinh viên → Thông báo |
| US-23 | Đã có cập nhật SignalR theo tài khoản, tự bắt kịp sau khi kết nối lại và giữ trang đang đọc trong hộp thư. | Mở hai tab, tạo cập nhật từ tài khoản quản lý |
| US-14 | Đã có trợ lý theo luồng của ứng dụng, tìm phòng từ dữ liệu theo hồ sơ và điều hướng đến đúng màn hình. | Cổng sinh viên → Hỗ trợ |
| US-16 | Đã có tra cứu hiển thị tên tài liệu, mục và liên kết nguồn nếu được cung cấp. Bản Development có tài liệu minh họa. Chưa có bộ tài liệu chính thức được Ban quản lý xác nhận trong môi trường này. | Hỏi trợ lý về một hướng dẫn có nguồn |
| US-01 | Đã có phát hành theo kỳ và phạm vi tòa, bỏ qua hóa đơn đã có khi chạy lại. | Thu phí → Phát hành kỳ thu |
| US-18 | Đã có xem hóa đơn, khoản phí, số đã thu và số còn phải trả. | Cổng sinh viên → Hóa đơn |
| US-09 | Đã có ghi nhận thu. Đã bổ sung biên nhận xem được bởi Ban quản lý và đúng sinh viên, có thể in hoặc lưu PDF. | Thu phí → Lịch sử thu, hoặc Hóa đơn → Lịch sử khoản thu |
| US-22 | Đã có hoàn tác bằng giao dịch bù và giữ lịch sử. Biên nhận gốc hiển thị mã giao dịch hoàn tác nếu đã điều chỉnh. | Thu phí → Lịch sử thu → Hoàn tác |
| US-13 | Đã có tích hợp VNPAY Sandbox và kiểm thử tự động luồng không dùng cổng thật. Cấu hình Sandbox trên bản cục bộ chưa được bật. Muốn xác nhận giao dịch với cổng cần thông tin merchant Sandbox đang hiệu lực và địa chỉ IPN HTTPS công khai trên môi trường thử nghiệm. | Hóa đơn chưa thanh toán → VNPAY Sandbox khi được cấu hình |

## Kiểm thử

- Build Release hoàn tất. Toàn bộ 88 kiểm thử đơn vị và 12 kiểm thử tích hợp SQL Server đều đạt.
- Kiểm tra 36 lượt mở trang trên desktop và mobile, gồm phân quyền, liên kết, bố cục, form và xuất CSV.
- Chạy các kịch bản trình duyệt riêng cho sửa tầng và chuyển tầng, phân trang phòng và thông báo, biên nhận và hoàn tác, sửa hồ sơ, phát hành hóa đơn, ảnh hiện trường, đổi và đặt lại mật khẩu, khóa phiên nhiều tab, thông báo SignalR, bổ sung đơn, chọn giường, trợ lý có nguồn và VNPAY với giao dịch mô phỏng.
- Bản cập nhật được mở tại `http://127.0.0.1:5002`. Endpoint `/health` trả `Healthy`.
