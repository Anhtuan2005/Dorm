# Đánh giá cơ hội đào sâu kiến trúc

Status: ready-for-human

Đánh giá này dùng từ vựng trong `CONTEXT.md` và áp dụng deletion test. Không có ADR hiện hữu xung đột.

## 1. Đào sâu Module Thu phí/Hóa đơn — đã chọn

- **Files:** mô hình `Hóa đơn`/`Kỳ thu`, cấu hình EF Core, dữ liệu demo, các Module cổng sinh viên, quản lý sinh viên và xét `Đơn lưu trú`, cùng màn hình hóa đơn sinh viên.
- **Problem:** tổng tiền đã trả và trạng thái đang được lưu trực tiếp, trong khi trạng thái quá hạn lại được tính riêng ở lúc đọc. Chưa có lịch sử khoản thu, người ghi nhận, hoàn tác hoặc chống ghi trùng. Interface hiện tại quá nông so với Implementation mà nghiệp vụ thu phí cần.
- **Solution:** tạo một Thu phí/Hóa đơn Module sâu. Seam duy nhất sở hữu phát hành `Hóa đơn`, ghi nhận `Giao dịch thu`, hoàn tác, tính `Công nợ` và trạng thái. EF Core là Adapter lưu trữ; các màn quản lý, sinh viên và quy trình xét đơn chỉ nhận kết quả nghiệp vụ.
- **Deletion test:** nếu xóa Module này, quy tắc số dư, quá hạn, thu một phần, thu đủ và hoàn tác sẽ tái xuất hiện ở ít nhất bốn caller.
- **Benefits:** tăng Locality cho quy tắc tiền và lịch sử đối soát; tạo Leverage cho quản lý, sinh viên, báo cáo và thông báo. Interface trở thành bề mặt kiểm thử chung cho mọi luồng.
- **Tests:** phát hành duy nhất theo `Hồ sơ sinh viên` + `Kỳ thu`; thu một phần/đủ; từ chối thu vượt; retry không ghi trùng; hoàn tác; quá hạn theo ngày nghiệp vụ; tổng đối soát.

## 2. Đào sâu Module Sức chứa/Chỗ có thể xếp

**Implementation checkpoint:** hoàn thành và được gia cố ở P0. `RoomCapacityPolicy` là Seam dùng chung cho đặt giữ chỗ, xác nhận nhận phòng và khóa giường bảo trì; quy tắc có tính cả các giữ chỗ khác và cho phép đơn tiêu thụ đúng giữ chỗ của chính nó. Các check constraint bảo vệ invariant cục bộ ở SQL Server, còn integration test thật khóa các race condition xuyên bảng.

- **Files:** Module sơ đồ `Phòng`, cổng sinh viên, xét `Đơn lưu trú` và mô hình lưu trú.
- **Problem:** công thức `Sức chứa - Chỗ đang ở - Chỗ đang giữ - Chỗ tạm khóa` có ba Implementation hơi khác nhau, làm trạng thái hiển thị và điều kiện duyệt có thể lệch nhau.
- **Solution:** một Module sâu trả cùng bộ dữ kiện sức chứa và lý do không thể xếp; các truy vấn EF nằm sau Seam.
- **Deletion test:** xóa Module sẽ làm công thức tái xuất hiện ở ba caller hiện hữu.
- **Benefits:** Locality cho invariant của `Phòng`; Leverage cho sơ đồ, tìm phòng và duyệt `Đơn lưu trú`.
- **Tests:** ma trận `Chỗ đang ở`, `Chỗ đang giữ`, `Chỗ tạm khóa`, giữ chỗ hết hạn và cập nhật đồng thời.

## 3. Đào sâu Module vòng đời Đơn lưu trú

**Implementation checkpoint:** hoàn thành và được gia cố ở P0. `HousingRequestWorkflow` là Seam duy nhất cho transition, trách nhiệm hành động, SLA và presentation trạng thái. Duyệt đăng ký/chuyển phòng đi tới `AwaitingMoveIn`, còn duyệt trả phòng kết thúc trực tiếp ở `Approved`. Giữ chỗ hết hạn/sai phòng/đã giải phóng không thể tạo lượt lưu trú; quản lý vẫn có đường từ chối để đóng ca và trả sức chứa.

- **Files:** Module xét đơn, cổng sinh viên, badge hàng đợi, cấu hình EF Core và migrations.
- **Problem:** trạng thái đang hoạt động, SLA, nhãn và chuyển trạng thái nằm ở nhiều nơi; Interface cập nhật trạng thái chưa diễn đạt đầy đủ luật chuyển.
- **Solution:** đưa state machine, thời điểm quá hạn, lịch sử xử lý và quyết định vào một Module sâu; lưu trữ là Adapter phía sau Seam.
- **Deletion test:** xóa Module sẽ phát tán lại cùng quy tắc sang cả giao diện quản lý và sinh viên.
- **Benefits:** Locality cho vòng đời `Đơn lưu trú`; Leverage cho danh sách, chi tiết, badge và constraint dữ liệu.
- **Tests:** mọi chuyển trạng thái hợp lệ/không hợp lệ, stale row version, hủy đơn và hai quản lý quyết định đồng thời.

## 4. Đào sâu Worklist Module bằng truy vấn phía database

- **Files:** danh sách `Đơn lưu trú`, danh bạ `Hồ sơ sinh viên` và màn Thu phí sắp tới.
- **Problem:** các navigation được tải rộng rồi mới lọc và giới hạn trong bộ nhớ, không bền vững khi dữ liệu tăng.
- **Solution:** từng domain có Worklist Module chiếu danh sách đã lọc, ưu tiên và phân trang trong SQL; chi tiết mục đang chọn được tải riêng. Không thêm generic repository hoặc Adapter giả.
- **Deletion test:** helper phân trang thuần là nông; Module chỉ có Depth khi Interface che cả thứ tự ưu tiên, tổng đếm và projection nghiệp vụ.
- **Benefits:** Locality cho tiêu chí hàng đợi; Leverage cho UI desktop/mobile và hiệu năng ổn định.
- **Tests:** thứ tự, tìm kiếm, tổng đếm và phân trang trên bộ dữ liệu lớn qua Interface.

## 5. Các enabler hẹp

- **Thời gian vận hành:** dùng `TimeProvider` để thống nhất UTC và ngày nghiệp vụ Asia/Saigon; system clock và fake clock là hai Adapter thật tại Seam.
- **Mã chứng từ:** cấp số nguyên tử trong transaction thay cho `Count + 1`; áp dụng cho mã `Đơn lưu trú`, `Sự cố`, `Hóa đơn` và `Giao dịch thu`.
- **Thông báo:** chỉ kéo phần tối thiểu cần cho Thu phí; chưa xây hệ thống realtime tổng quát.

## Quyết định ưu tiên

Ứng viên 1–3 đã được triển khai theo vertical slice: Thu phí/Hóa đơn ở Sprint 2, Sức chứa dùng chung cùng bàn điều phối Sự cố ở Sprint 3, và vòng đời `Đơn lưu trú` ở Sprint 4. Gói P0 tiếp theo đã khóa các giao điểm giữ chỗ–nhận phòng–bảo trì bằng 37 unit test và 5 integration test trên SQL Server thật. Cơ hội P1 kế tiếp là mở rộng idempotency cho các command, tiếp tục làm sâu Worklist Module ở danh sách còn lại và tách seam phát hành hóa đơn trước khi tích hợp AI thật.
