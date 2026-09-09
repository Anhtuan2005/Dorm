---
name: Nếp KTX
description: Hệ thống vận hành ký túc xá rõ ràng, điềm tĩnh và đáng tin.
colors:
  canvas-warm: "oklch(0.973 0.008 78)"
  surface-paper: "oklch(0.993 0.004 78)"
  sidebar-lilac: "oklch(0.958 0.012 278)"
  ledger-ink: "oklch(0.245 0.026 272)"
  muted-slate: "oklch(0.57 0.024 272)"
  divider: "oklch(0.89 0.014 275)"
  action-indigo: "oklch(0.46 0.135 276)"
  action-indigo-hover: "oklch(0.405 0.14 276)"
  action-indigo-soft: "oklch(0.935 0.035 276)"
  success-leaf: "oklch(0.49 0.115 151)"
  warning-amber: "oklch(0.57 0.12 70)"
  danger-brick: "oklch(0.52 0.17 28)"
typography:
  headline:
    fontFamily: "Segoe UI, Be Vietnam Pro, system-ui, sans-serif"
    fontSize: "28px"
    fontWeight: 760
    lineHeight: 1.14
    letterSpacing: "-0.035em"
  title:
    fontFamily: "Segoe UI, Be Vietnam Pro, system-ui, sans-serif"
    fontSize: "16px"
    fontWeight: 720
    lineHeight: 1.25
    letterSpacing: "-0.02em"
  body:
    fontFamily: "Segoe UI, Be Vietnam Pro, system-ui, sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "Segoe UI, Be Vietnam Pro, system-ui, sans-serif"
    fontSize: "10px"
    fontWeight: 760
    lineHeight: 1.2
    letterSpacing: "0.08em"
rounded:
  sm: "8px"
  md: "12px"
  lg: "16px"
  pill: "999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  xxl: "32px"
components:
  button-primary:
    backgroundColor: "{colors.action-indigo}"
    textColor: "{colors.surface-paper}"
    rounded: "{rounded.sm}"
    padding: "0 14px"
    height: "38px"
  button-primary-hover:
    backgroundColor: "{colors.action-indigo-hover}"
    textColor: "{colors.surface-paper}"
    rounded: "{rounded.sm}"
  button-secondary:
    backgroundColor: "{colors.surface-paper}"
    textColor: "{colors.ledger-ink}"
    rounded: "{rounded.sm}"
    padding: "0 14px"
    height: "38px"
  status-success:
    backgroundColor: "oklch(0.948 0.036 151)"
    textColor: "{colors.success-leaf}"
    rounded: "{rounded.pill}"
    padding: "0 9px"
    height: "23px"
---

# Design System: Nếp KTX

## 1. Overview

**Creative North Star: "Sổ trực vận hành"**

Nếp KTX mang cảm giác của một sổ trực được tổ chức tốt: việc cần xử lý ở phía trước, số liệu có nguồn gốc rõ và mọi trạng thái đều chỉ ra bước tiếp theo. Bề mặt sáng ấm phù hợp văn phòng ban ngày, còn indigo xuất hiện có chủ đích tại hành động chính, lựa chọn hiện tại và liên kết quan trọng.

Hệ thống ưu tiên bố cục split-view cho bảng và chi tiết để nhân viên không mất ngữ cảnh. Mobile chuyển cấu trúc sang tác vụ một tay, giữ nội dung sinh viên cần nhất và không cố thu nhỏ nguyên dashboard desktop.

**Key Characteristics:**

- Nền giấy ấm, chữ slate, điểm nhấn indigo tiết chế.
- Mật độ thông tin cao nhưng có nhịp trắng thay đổi theo mức ưu tiên.
- Hàng đợi và trạng thái quan trọng hơn trang trí.
- Bảng, panel chi tiết và filter có thể chuyển thẳng sang Razor và CSS thuần.

## 2. Colors

Bảng màu nghiêng ấm ở nền và nghiêng lạnh ở chữ, tạo sự điềm tĩnh mà không rơi vào giao diện hành chính xám nhạt.

### Primary

- **Action Indigo:** dành cho primary action, item đang chọn, focus và liên kết có bước tiếp theo.
- **Action Indigo Soft:** nền selection, tiến độ và trạng thái thông tin liên quan hành động.

### Secondary

- **Success Leaf:** hoàn tất, còn chỗ, đã thu và kết nối trực tiếp.
- **Warning Amber:** gần hạn, đang chờ và rủi ro cần chú ý.
- **Danger Brick:** quá hạn, từ chối, khẩn cấp và thao tác phá hủy.

### Neutral

- **Canvas Warm:** nền trang, tạo khác biệt nhẹ với surface mà không cần shadow nặng.
- **Surface Paper:** bảng, panel và control.
- **Sidebar Lilac:** lớp điều hướng riêng biệt nhưng cùng họ với accent.
- **Ledger Ink:** nội dung chính và số liệu cần đối chiếu.
- **Muted Slate:** metadata và nội dung phụ.
- **Divider:** phân tách hàng và nhóm thông tin.

### Named Rules

**The Ten Percent Rule.** Action Indigo chiếm không quá khoảng 10% một màn hình; độ hiếm giúp hành động chính nổi bật.

**The Meaning Rule.** Success, warning và danger chỉ xuất hiện khi mang nghĩa trạng thái, không dùng để trang trí.

## 3. Typography

**Display Font:** Segoe UI với Be Vietnam Pro và system-ui làm fallback

**Body Font:** Segoe UI với Be Vietnam Pro và system-ui làm fallback

**Character:** Một sans quen thuộc giúp màn hình nghiệp vụ đọc nhanh; cá tính đến từ nhịp, độ đậm và chữ số thay vì display font.

### Hierarchy

- **Headline** (760, 28px, 1.14): tiêu đề trang, tối đa một lần trên màn hình.
- **Title** (720, 16px, 1.25): section, panel và thực thể đang chọn.
- **Body** (400, 14px, 1.5): nội dung và mô tả; đoạn văn tối đa 70 ký tự mỗi dòng.
- **Label** (760, 10px, 0.08em): eyebrow, đầu cột và nhãn nhóm; có thể viết hoa.

### Named Rules

**The Numbers First Rule.** Tiền, công suất và thời hạn dùng tabular alignment, trọng lượng cao và không bị ngắt dòng giữa số với đơn vị.

## 4. Elevation

Hệ thống phẳng theo mặc định và dùng tonal layering cùng viền 1 px để tạo cấu trúc. Shadow chỉ xuất hiện ở app panel lớn, toast hoặc bề mặt cần tách khỏi canvas; không xếp card bên trong card.

### Shadow Vocabulary

- **Ambient Low** (0 1px 2px với opacity 4%, 0 8px 24px với opacity 4.5%): panel chính trên canvas.
- **Action Lift** (0 5px 14px với màu primary opacity 13%): primary button và brand mark.
- **Toast Lift** (0 16px 38px với opacity 16%): phản hồi nổi tạm thời.

### Named Rules

**The Flat-by-Default Rule.** Nếu mọi container đều có shadow, cấu trúc đang sai; dùng khoảng cách, tone và divider trước.

## 5. Components

Các thành phần quen thuộc, gọn và có trạng thái hoàn chỉnh.

### Buttons

- **Shape:** cạnh cong vừa phải (8px), cao 38px desktop và 44px mobile.
- **Primary:** Action Indigo, chữ Surface Paper, padding ngang 14px.
- **Hover / Focus:** tối hơn một bậc, focus ring 3px bằng Action Indigo Soft, chuyển trạng thái 160 ms ease-out.
- **Secondary:** Surface Paper, viền Divider Strong; danger outline chỉ dành cho từ chối, hủy hoặc vô hiệu hóa.

### Chips

- **Style:** pill 23px, nền tint nhẹ và chữ semantic đậm.
- **State:** luôn có chữ; icon chỉ là tín hiệu bổ sung, không thay nhãn.

### Cards / Containers

- **Corner Style:** 16px cho panel, 12px cho nhóm con.
- **Background:** Surface Paper trên Canvas Warm.
- **Shadow Strategy:** Ambient Low chỉ ở panel cấp cao.
- **Border:** Divider 1px bao quanh đầy đủ.
- **Internal Padding:** 16 đến 24px tùy mật độ.

### Inputs / Fields

- **Style:** cao 38px, nền Surface Paper, viền Divider, radius 8px.
- **Focus:** đổi viền sang Action Indigo và thêm ring 3px mềm.
- **Error / Disabled:** error có message cạnh trường; disabled giảm tương phản nhưng vẫn đọc được.

### Navigation

Sidebar desktop rộng 232px; item cao 43px, active dùng nền Action Indigo Soft và glyph có viền. Mobile dùng header 62px và bottom navigation 64px cho bốn tác vụ sinh viên quan trọng.

### Split View

Bảng hoặc danh sách ở trái, panel chi tiết và hành động ở phải. Selection dùng nền tint; panel không mở modal trừ trường hợp bắt buộc xác nhận phá hủy.

## 6. Do's and Don'ts

### Do:

- **Do** đưa hàng đợi, bước tiếp theo và trạng thái giữ việc lên trước số liệu tham khảo.
- **Do** dùng skeleton cùng hình dạng với nội dung và giữ layout ổn định khi tải.
- **Do** giữ vùng chạm tối thiểu 44px trên mobile và focus rõ cho bàn phím.
- **Do** giữ dữ liệu VND theo định dạng vi-VN và dùng nhãn chữ cạnh màu trạng thái.
- **Do** dùng split-view để người xử lý đối chiếu danh sách với chi tiết.

### Don't:

- **Don't** làm giống dashboard mẫu SaaS với các thẻ số liệu giống hệt nhau và màu xanh trang trí khắp nơi.
- **Don't** dùng giao diện tối, neon, gradient chữ hoặc glassmorphism để tạo cảm giác công nghệ giả tạo.
- **Don't** biến nghiệp vụ nhiều trạng thái thành bảng dày đặc thiếu ưu tiên, hoặc giấu tác vụ chính trong modal.
- **Don't** dùng màu làm tín hiệu duy nhất cho trạng thái.
- **Don't** dùng border trái hoặc phải dày hơn 1px làm sọc màu trên card, row, callout hoặc alert.

