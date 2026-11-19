# NẾP KTX

NẾP KTX quản lý sức chứa, lưu trú, thu phí và hỗ trợ vận hành ký túc xá; cùng một dữ liệu nghiệp vụ được trình bày theo công việc của quản lý và sinh viên.

## Language

### Không gian và sức chứa

**Cơ sở**:
Một địa điểm vận hành ký túc xá chứa một hoặc nhiều **Tòa**.
_Avoid_: Chi nhánh

**Tòa**:
Một khối nhà trong **Cơ sở**, được chia thành các **Tầng**.
_Avoid_: Khu nhà, block

**Tầng**:
Một cấp vật lý trong **Tòa** chứa nhiều **Phòng**.

**Phòng**:
Đơn vị lưu trú có loại phòng, giá tháng và **Sức chứa** xác định.

**Sức chứa**:
Tổng số giường vật lý của một **Phòng**.
_Avoid_: Chỗ trống

**Số giường**:
Vị trí đánh số từ 01 đến **Sức chứa** trong một **Phòng**. Sinh viên chọn số giường khi gửi đơn. Ban quản lý duyệt để giữ giường đó và xác nhận bàn giao để ghi số giường vào **Lượt lưu trú**.

**Ảnh phòng**:
Ảnh do Ban quản lý tải lên trong danh mục phòng để sinh viên xem khi tìm phòng.

**Tiện nghi phòng**:
Mọi **Phòng** có WC khép kín. Các tiện nghi bổ sung do Ban quản lý khai báo theo từng phòng.

**Chỗ đang ở**:
Số **Lượt lưu trú** chưa kết thúc trong một **Phòng**.

**Chỗ đang giữ**:
Số giường được giữ tạm thời bởi một **Giữ chỗ** còn hiệu lực.

**Chỗ tạm khóa**:
Số giường không thể sử dụng do **Sự cố** đang mở.

**Chỗ có thể xếp**:
Phần **Sức chứa** còn lại sau khi trừ **Chỗ đang ở**, **Chỗ đang giữ** và **Chỗ tạm khóa**.
_Avoid_: Chỗ trống, giường trống

### Sinh viên và lưu trú

**Hồ sơ sinh viên**:
Bản ghi nghiệp vụ bền vững chứa mã sinh viên, thông tin cá nhân và lịch sử KTX.
_Avoid_: Tài khoản

**Tài khoản người dùng**:
Danh tính đăng nhập và vai trò có thể được liên kết với đúng một **Hồ sơ sinh viên**.
_Avoid_: Hồ sơ sinh viên

**Lượt lưu trú**:
Khoảng thời gian một **Hồ sơ sinh viên** được xếp vào một **Phòng**.
_Avoid_: Đăng ký phòng

**Bạn cùng phòng**:
Sinh viên khác có **Lượt lưu trú** đang hoạt động trong cùng **Phòng**. Cổng sinh viên chỉ hiển thị họ tên trong mục **Phòng hiện tại**.

**Đơn lưu trú**:
Yêu cầu đăng ký ở, chuyển phòng hoặc trả phòng có trạng thái, quyết định và lịch sử xử lý.
_Avoid_: Đơn đăng ký khi nói chung cả ba loại

**Lượt xử lý**:
Bên chịu trách nhiệm cho hành động tiếp theo của một **Đơn lưu trú**: Ban quản lý hoặc sinh viên. Khi hồ sơ cần bổ sung, lượt xử lý chuyển cho sinh viên; khi sinh viên gửi bổ sung, lượt xử lý trở lại Ban quản lý.
_Avoid_: Người đang xem đơn

**Mốc chờ hành động**:
Thời điểm **Lượt xử lý** gần nhất đổi bên hoặc bắt đầu. SLA được tính từ mốc này, không phải luôn từ lúc gửi **Đơn lưu trú**.
_Avoid_: Ngày tạo đơn khi nói về SLA hiện tại

**Giữ chỗ**:
Phân bổ giường có thời hạn làm giảm **Chỗ có thể xếp** nhưng chưa tạo **Lượt lưu trú**.

**Xác nhận nhận phòng**:
Điểm bàn giao vật lý sau khi một **Đơn lưu trú** đăng ký/chuyển phòng đã được duyệt. Thao tác này giải phóng **Giữ chỗ**, kết thúc **Lượt lưu trú** nguồn khi chuyển phòng và tạo **Lượt lưu trú** mới trong cùng transaction.
_Avoid_: Duyệt đơn khi nói về thời điểm sinh viên thực sự bắt đầu ở.

### Thu phí và vận hành

**Kỳ thu**:
Khoảng thời gian và hạn thanh toán dùng để phát hành **Hóa đơn**.

**Hóa đơn**:
Khoản phải thu của một **Hồ sơ sinh viên** trong một **Kỳ thu**, gồm nhiều dòng phí. Sau khi phát hành, các dòng phí không được sửa trực tiếp.
_Avoid_: Thanh toán

**Chênh lệch hóa đơn**:
So sánh tổng phí và từng nhóm phí của hóa đơn còn hiệu lực với hóa đơn còn hiệu lực gần nhất trước kỳ hiện tại của cùng sinh viên.

**Giao dịch thu**:
Bản ghi bất biến ghi nhận một khoản tiền đã nhận cho đúng một **Hóa đơn**, gồm số tiền, phương thức, thời điểm, người ghi nhận hoặc cổng đã xác nhận và mã tham chiếu chống trùng. Sai sót của khoản thu thủ công được xử lý bằng giao dịch hoàn tác liên kết, không sửa hoặc xóa bản ghi gốc.
_Avoid_: Cập nhật số tiền đã trả

**Phiên thanh toán VNPAY**:
Yêu cầu thử nghiệm có mã và thời hạn riêng, gắn đúng một **Hóa đơn** và số **Công nợ** lúc mở cổng. Phiên đang chờ hoặc thất bại chưa phải là **Giao dịch thu**. Khi cần đối soát, phiên giữ mã giao dịch VNPAY và chưa đổi **Công nợ**.

**Công nợ**:
Số tiền còn phải thu của một **Hóa đơn**, được tính từ tổng các dòng phí trừ tổng **Giao dịch thu** còn hiệu lực.
_Avoid_: Số tiền đã thanh toán

**Sự cố**:
Yêu cầu sửa chữa gắn với một **Phòng**, có mức ưu tiên, tiến độ và số giường bị khóa.
_Avoid_: Phản ánh chung

**Theo dõi sự cố chung**:
Sinh viên cùng phòng có thể theo dõi sự cố được người báo chia sẻ. Lượt lưu trú đang hoạt động cấp quyền xem tiêu đề, nhóm sự cố và tiến độ, đồng thời nhận thông báo cập nhật.

**Ảnh hiện trường**:
Tệp ảnh minh chứng gắn với một **Sự cố**, được lưu ngoài vùng public và chỉ người báo hoặc người có vai trò quản lý được truy cập.

**Thông báo**:
Thông tin lưu theo tài khoản, hướng người dùng đến kết quả hoặc tác vụ liên quan trong hệ thống. Tab của người nhận có thể cập nhật trực tiếp sau khi thay đổi đã được lưu.

**Ảnh chụp vận hành**:
Tổng hợp tại một thời điểm của **Sức chứa**, **Công nợ** và công việc cần can thiệp. Số sức chứa là hiện tại; kết quả tài chính và quy trình phải ghi rõ **Kỳ thu**.
_Avoid_: Báo cáo kỳ khi số liệu chưa cùng một khoảng thời gian

**Tỷ lệ thu kỳ**:
Tổng **Giao dịch thu** ròng chia cho tổng giá trị **Hóa đơn** đang hiệu lực trong một **Kỳ thu**. Giao dịch hoàn tác làm giảm số đã thu; hóa đơn hủy không tham gia mẫu số.
_Avoid_: Tỷ lệ thanh toán

## Relationships

- Một **Cơ sở** chứa nhiều **Tòa**; một **Tòa** chứa nhiều **Tầng**; một **Tầng** chứa nhiều **Phòng**.
- Một **Phòng** có nhiều **Lượt lưu trú**, **Giữ chỗ** và **Sự cố** theo thời gian.
- Một **Hồ sơ sinh viên** có tối đa một **Lượt lưu trú** đang hoạt động và tối đa một **Đơn lưu trú** đang hoạt động.
- Duyệt một **Đơn lưu trú** đăng ký/chuyển phòng tạo **Giữ chỗ** và chuyển sang chờ **Xác nhận nhận phòng**; chưa thay đổi **Lượt lưu trú** tại bước duyệt.
- **Xác nhận nhận phòng** chỉ được dùng đúng **Giữ chỗ** một giường của chính đơn và đúng phòng đích; giữ chỗ đã hết hạn, đã giải phóng hoặc sai phòng không thể tạo **Lượt lưu trú**.
- **Xác nhận nhận phòng** kiểm tra lại sức chứa và lượt nguồn, rồi giải phóng **Giữ chỗ**, kết thúc lượt cũ nếu có và tạo lượt mới trong cùng transaction.
- Ban quản lý có thể từ chối **Đơn lưu trú** đang chờ nhận phòng; thao tác này giải phóng **Giữ chỗ** để sinh viên có thể nộp yêu cầu mới.
- Một **Đơn lưu trú** chuyển/trả phòng ghim đúng **Lượt lưu trú** nguồn tại lúc gửi; quyết định bị từ chối nếu lượt nguồn đã thay đổi.
- Một **Lượt lưu trú** do đơn đăng ký/chuyển phòng được duyệt tạo ra lưu lại quan hệ với đúng **Đơn lưu trú** nguồn.
- **Lượt xử lý** và **Mốc chờ hành động** đổi theo state machine của **Đơn lưu trú**; thời gian sinh viên bổ sung không được tính vào SLA của Ban quản lý.
- Một **Hồ sơ sinh viên** có thể tồn tại trước khi được liên kết với **Tài khoản người dùng**.
- Một **Kỳ thu** phát hành nhiều **Hóa đơn**; mỗi **Hóa đơn** thuộc đúng một **Hồ sơ sinh viên**.
- Một **Hóa đơn** có nhiều **Giao dịch thu**; **Công nợ** và trạng thái thanh toán được suy ra từ các bản ghi này cùng hạn của **Kỳ thu**.
- Một **Phiên thanh toán VNPAY** chỉ tạo một **Giao dịch thu** khi nhận IPN đúng chữ ký, mã phiên, số tiền và **Công nợ** còn khớp. Trang quay về chỉ đọc trạng thái; hoàn tác thủ công không hoàn tiền qua VNPAY.
- **Sự cố** và **Giữ chỗ** làm giảm **Chỗ có thể xếp** của **Phòng**.
- Mọi thao tác làm thay đổi **Chỗ đang ở**, **Chỗ đang giữ** hoặc **Chỗ tạm khóa** phải kiểm tra cùng một quy tắc sức chứa trong transaction; một **Sự cố** không được khóa phần giường đã được giữ cho đơn khác.
- Dữ liệu cũ chưa có **Số giường** tiếp tục chiếm số chỗ như trước. Khi liệt kê giường có thể xếp, hệ thống loại các số giường đã xác định và trừ tiếp số chỗ chưa có số giường.
- Một **Sự cố** có thể có nhiều **Ảnh hiện trường**; quyền đọc ảnh tuân theo quyền đọc sự cố thay vì URL tĩnh công khai.
- **Ảnh chụp vận hành** liên kết số sức chứa hiện tại với **Kỳ thu** được chọn; giao diện phải ghi rõ hai mốc này để tránh hiểu chúng là cùng một khoảng thời gian.

## Học kỳ, bàn giao và phí theo chỉ số

- Mỗi đợt gia hạn có thời gian nhận đơn và thời gian ở do Ban quản lý nhập. Khi duyệt, hệ thống cập nhật hạn dự kiến của đúng lượt đang ở.
- Ban quản lý có thể hẹn bàn giao nhận, chuyển hoặc trả phòng. Lịch hẹn dùng giờ Việt Nam. Biên bản ghi tình trạng chìa khóa, nội thất và thiết bị tại bước xác nhận. Hết hạn giữ giường thì đơn hết hiệu lực và sinh viên được chọn lại.
- Tiền phòng kỳ thu lấy giá của lượt ở gần nhất có giao với kỳ, rồi mới xét phạm vi tòa. Hai mốc đầu và cuối kỳ dùng giờ Việt Nam. Phát hành theo từng tòa và phát hành toàn KTX cho cùng kết quả trên cùng dữ liệu.
- Với kỳ thu theo chỉ số, mỗi phòng có chỉ số đầu, cuối và đơn giá riêng cho điện và nước. Khi kỳ đã kết thúc, chia tổng tiền của phòng theo thời gian ở thực tế trong kỳ. Phân bổ số đồng còn dư theo phần lẻ lớn nhất, sau đó theo mã định danh hồ sơ. Hóa đơn sinh viên lưu từng dòng phí và tỷ lệ đã dùng. Kỳ thu theo mức cố định vẫn dùng số tiền nhập theo sinh viên.

## Example dialogue

> **Dev:** “Phòng C-504 có sức chứa 8 và chưa có sinh viên, vậy đơn chuyển phòng luôn duyệt được đúng không?”
> **Domain expert:** “Chưa đủ. Phải tính lại **Chỗ có thể xếp** từ **Lượt lưu trú**, **Giữ chỗ** và **Sự cố** ngay lúc duyệt **Đơn lưu trú**.”
> **Dev:** “Cấp **Tài khoản người dùng** mới có tạo thêm sinh viên không?”
> **Domain expert:** “Không. Tài khoản chỉ liên kết với **Hồ sơ sinh viên** đã tồn tại.”

## Flagged ambiguities

- “Chỗ trống” từng được dùng cho cả giường vật lý chưa có người và giường thực sự nhận thêm sinh viên; chuẩn hóa thành **Chỗ có thể xếp** cho số liệu vận hành.
- “Tài khoản sinh viên” từng được dùng thay cho dữ liệu sinh viên; **Tài khoản người dùng** và **Hồ sơ sinh viên** là hai khái niệm riêng.
- “Đơn đăng ký” từng được dùng cho cả chuyển/trả phòng; dùng **Đơn lưu trú** khi nói về quy trình chung.
- “Đã thanh toán” từng được lưu như một số tổng có thể sửa; chuẩn hóa thành tổng các **Giao dịch thu** còn hiệu lực để giữ lịch sử và đối soát.
