# Nhu cầu lưu trú và phạm vi phát triển NẾP KTX

NẾP KTX phục vụ sinh viên tìm chỗ ở, theo dõi đơn và tự xử lý các việc thường gặp trong thời gian nội trú. Ban quản lý cần duy trì đúng sức chứa, xử lý ngoại lệ và đối chiếu tiền thu. Phạm vi đề xuất bao quát từ tìm phòng đến khi trả phòng, kể cả sinh viên muốn ở tiếp.

## Căn cứ và nhu cầu

- Khảo sát 385 sinh viên nội trú tại TP.HCM ghi nhận cơ sở vật chất, an ninh và an toàn, dịch vụ hỗ trợ và nhân viên quản lý đều ảnh hưởng tích cực đến chất lượng cảm nhận. Cơ sở vật chất có ảnh hưởng mạnh nhất trong mẫu khảo sát. [Nghiên cứu của Hoàng Lệ Chi và Phạm Phương Thảo](https://nghiencuu.tapchikinhtetaichinh.vn/cac-nhan-to-anh-huong-den-su-hai-long-cua-sinh-vien-doi-voi-cong-tac-quan-ly-ky-tuc-xa-dai-hoc-tai-tp-ho-chi-minh-160168.html)
- Khảo sát 210 sinh viên tại Đại học Cần Thơ cũng xếp cơ sở vật chất và thiết bị trước các yếu tố còn lại. Giá cảm nhận, khả năng phục vụ và niềm tin đứng tiếp theo. Với NẾP KTX, đây là căn cứ ưu tiên thông tin phòng có thể đối chiếu, chi phí rõ ràng và hỗ trợ theo dõi được. [Nghiên cứu của Lê Phước Hương và Lê Thị Mộng Thúy](https://jfm.edu.vn/index.php/jfm/article/download/493/513/7398)
- Hướng dẫn của KTX ĐHQG-HCM có các quy trình riêng cho người mới, gia hạn và trả phòng. Sinh viên chọn chỗ, theo dõi xác nhận, kiểm tra hóa đơn khi gia hạn và khai ngày dự kiến khi trả phòng. Đây là quy trình tham khảo để thiết kế màn hình và dữ liệu cho NẾP KTX. Các biểu phí, thời hạn và hồ sơ cụ thể cần do cơ sở áp dụng xác nhận. [Đăng ký mới](https://huongdan.ktxhcm.edu.vn/huong-dan/tan-sinh-vien), [Gia hạn](https://huongdan.ktxhcm.edu.vn/huong-dan/huong-dan-sinh-vien-dang-noi-tru-thuc-hien-gia-han), [Trả phòng](https://huongdan.ktxhcm.edu.vn/huong-dan/huong-dan-tra-phong-menu)

| Giai đoạn | Sinh viên cần gì | Ban quản lý cần gì |
| --- | --- | --- |
| Tìm chỗ | So sánh ảnh, tiện nghi thực tế, giá phòng, chi phí có thể phát sinh và giường còn có thể xếp. Hiểu vì sao một lựa chọn không xuất hiện. | Cập nhật phòng, giá và tiện nghi ở một nơi. Phân biệt chỗ đang ở, đang giữ, tạm khóa và có thể xếp. |
| Đăng ký và nhận phòng | Biết hồ sơ cần gì, ai đang xử lý, hạn giữ giường và việc cần làm tiếp theo. | Duyệt, yêu cầu bổ sung và bàn giao theo từng mốc. Xử lý hết hạn, hủy và thay đổi sức chứa mà không xếp trùng giường. |
| Thanh toán | Thấy số tiền, hạn, cách thanh toán, trạng thái đang chờ và biên nhận. Biết liên hệ ai khi khoản tiền chưa được ghi nhận. | Đối chiếu đúng hóa đơn, khoản thu và mã giao dịch. Nhận diện khoản cần đối soát, chặn ghi thu trùng và giữ lịch sử điều chỉnh. |
| Sinh hoạt | Báo sự cố nhanh, xem tiến độ, tìm hướng dẫn có nguồn và kênh liên hệ. | Phân công theo mức độ, theo dõi thời gian phản hồi và đưa giường trở lại sử dụng sau khi xử lý. |
| Ở tiếp hoặc rời KTX | Biết ngày hết hạn, được xin gia hạn hoặc chọn ngày trả phòng, chuẩn bị bàn giao và kiểm tra công nợ còn lại. | Xem lượt sắp hết hạn, duyệt gia hạn đúng lượt ở, ghi nhận tài sản bàn giao và giải phóng giường đúng lúc. |

## Phạm vi đề xuất theo thứ tự ưu tiên

| Ưu tiên | Phạm vi | Tiêu chí nghiệm thu |
| --- | --- | --- |
| 1 | Minh bạch hóa đơn và thanh toán. Thay đổi trong đợt này giúp mỗi hóa đơn còn công nợ hiển thị nút VNPAY hoặc lý do nút bị vô hiệu hóa, kèm hướng dẫn. Tiếp theo là xem lại giao dịch đang chờ ngay từ hóa đơn và đối soát trên môi trường Sandbox có kết nối thực. | Hóa đơn còn nợ cho biết có thể thanh toán bằng cách nào. Giao dịch đang chờ có thể mở lại theo mã. IPN lặp chỉ ghi một Giao dịch thu. Khoản cần đối soát hiện rõ cho cả hai vai trò. |
| 1 | Hoàn thiện tìm phòng và giữ chỗ. Hiển thị rõ số giường có thể xếp, phân biệt ảnh chụp và mô hình 3D. Tách giá phòng khỏi các khoản chưa tính. Nối thông báo duyệt với hạn giữ, giường và bước nhận phòng. | Sinh viên biết khoản nào đã biết giá và khoản nào phụ thuộc tiêu thụ. Giường chuyển sang giữ hoặc tạm khóa được cập nhật trước khi xác nhận. Hai người chọn giường cuối chỉ có tối đa một lượt giữ hợp lệ. |
| 2 | Thêm đợt ở và gia hạn. Quản lý cấu hình mốc đăng ký, ngày bắt đầu và kết thúc, điều kiện và phạm vi áp dụng. Sinh viên đang ở xem ngày hết hạn và gửi đề nghị ở tiếp. | Một đề nghị đang xử lý cho mỗi lượt ở và đợt. Duyệt gia hạn cùng giường cập nhật thời hạn của đúng Lượt lưu trú. Danh sách sắp hết hạn cập nhật sau khi duyệt. Chuyển phòng tiếp tục dùng luồng giữ chỗ và bàn giao riêng. |
| 2 | Bàn giao có ngày hẹn và danh sách tài sản. Kết nối đơn nhận, chuyển hoặc trả phòng với việc thực tế tại tòa. | Hai bên xem được ngày dự kiến và ngày xác nhận. Ban quản lý ghi nhận chìa khóa, tình trạng tài sản và ghi chú. Sức chứa thay đổi đúng lúc xác nhận bàn giao. |
| 2 | Theo dõi sửa chữa và nguồn hướng dẫn. Làm rõ tiến độ, người hoặc đội phụ trách, thời gian dự kiến và kênh liên hệ. Công bố hướng dẫn do Ban quản lý duyệt theo chủ đề. | Sinh viên theo dõi được từng mốc và xem nguồn của hướng dẫn. Sau khi xử lý, giường liên quan được mở đúng theo dữ liệu. Ban quản lý lọc được sự cố quá hạn và biết phòng nào bị lặp lại. |
| 3 | Điện nước theo chỉ số, danh sách chờ khi hết giường và phản hồi sau xử lý. Xác nhận cách vận hành tại cơ sở trước khi chốt quy trình. | Có thể đối chiếu từ chỉ số đến dòng phí. Danh sách chờ có quy tắc ưu tiên công bố được. Phản hồi liên kết đúng sự cố đã xử lý. |

## Cách kiểm chứng ưu tiên tại cơ sở

Thử cùng sinh viên mới, sinh viên đang ở và nhân viên tòa trên các tình huống tìm phòng theo ngân sách, đăng ký một giường, thanh toán hoặc tìm biên nhận, báo sự cố và xin ở tiếp. Ghi nhận bước họ bỏ dở, câu hỏi phải hỏi thêm và thời gian hoàn thành. Đối chiếu với tỷ lệ hồ sơ cần bổ sung, thời gian từ gửi đơn đến nhận phòng, số khoản thu cần đối soát, thời gian phản hồi sự cố và số lượt ở sắp hết hạn chưa được xử lý. Các nghiên cứu công bố cung cấp căn cứ ban đầu, còn mức ưu tiên giữa từng quy trình nên được kiểm chứng tại cơ sở dùng NẾP KTX.

## Điều kiện cho thanh toán Sandbox

Ứng dụng có luồng mở phiên và ghi nhận giao dịch. Bản cục bộ cần bật Sandbox, thiết lập mã merchant và khóa ký đã xác nhận, và dùng môi trường thử nghiệm có điểm nhận IPN công khai qua HTTPS. Return URL dùng để đưa sinh viên về trang trạng thái, còn hệ thống cập nhật Công nợ sau khi nhận IPN hợp lệ. Các kiểm thử cục bộ dùng dữ liệu mô phỏng để đối chiếu cả hai luồng. [Tài liệu kết nối của VNPAY](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html), [Giải đáp về IPN và số tiền tối thiểu](https://sandbox.vnpayment.vn/apis/docs/faqs/)
