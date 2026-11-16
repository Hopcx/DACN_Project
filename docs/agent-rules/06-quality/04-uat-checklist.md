# Checklist UAT theo vai trò (G7)

Dùng cho buổi nghiệm thu với người dùng thật trên môi trường UAT (Development trỏ bản sao QA, hoặc môi trường chạy thật trước khi mở). Cột "Tự động" là smoke trong `tests/RunUatSuite.ps1` đã phủ ở mức API/trình duyệt; dòng **Tay** phải kiểm bằng người. Ghi Đạt / Không đạt / Ghi chú, người kiểm và ngày.

Tài khoản thử: tạo bằng giao diện Admin hoặc đăng ký + xác minh email; **không** dùng tài khoản thật của người dùng cuối.

## Chung

| # | Chức năng | Bước kiểm | Kết quả mong đợi | Tự động | Đạt? |
|---|---|---|---|---|---|
| C1 | Đăng nhập 4 vai trò | Đăng nhập Admin/Người ra đề/Giảng viên/Học viên | Vào đúng trang chủ vai trò; menu đúng quyền | P1 Role Regression | |
| C2 | Chặn URL khác vai trò | Học viên mở `/admin/users` | "Bạn không có quyền xem trang này." | P1 Role Regression | |
| C3 | Đăng ký + xác minh email | Đăng ký mới, mở link email | Chưa xác minh không đăng nhập được; sau xác minh vào được | P2/P4 (maildrop) — **Tay** với SMTP thật | |
| C4 | Hồ sơ | Sửa họ tên/địa chỉ; đổi mật khẩu sai/đúng | Lỗi dưới ô; đổi đúng thì đăng xuất về trang đăng nhập | **Tay** | |
| C5 | Giao diện 360 px | Mở các trang trên điện thoại | Không tràn ngang; bảng cuộn trong thẻ | **Tay** (đã đo P5.6) | |

## Admin

| # | Chức năng | Bước kiểm | Kết quả mong đợi | Tự động | Đạt? |
|---|---|---|---|---|---|
| A1 | Tổng quan | Mở Tổng quan | KPI lượt thi, biểu đồ | P1 Role, P2 Dashboard | |
| A2 | Môn/phòng/cấp/quyền | Thêm, sửa, ẩn/xóa | Danh sách cập nhật; mã 1–4 khóa | P1 Role (môn) — **Tay** phần còn lại | |
| A3 | Tài khoản | Thêm, sửa chi tiết, xóa; Excel mẫu/nhập/xuất | Lỗi trong Modal; nhập file lỗi không tạo user | P4 Account Excel, P4 Frontend | |
| A4 | Phân quyền | Gán/sửa/thu hồi quyền | Bảng cập nhật | Admin Permission (API) — **Tay** UI | |
| A5 | Lớp học | Thêm lớp có mã/giảng viên; thành viên, duyệt | Đã duyệt/Đang chờ đúng | P1 Role, P1 Acceptance | |
| A6 | Ngân hàng câu hỏi | Thêm/sửa/ẩn/hiện; nhập Excel; xuất Excel v1 | Lỗi theo dòng khi nhập; câu đã dùng khóa sửa | P1 Import Matrix, P4 Question Export, P4 Frontend | |
| A7 | Bài thi, mã đề | Tạo bài nháp, mã đề, công khai | Không sửa mã đề khi đã công khai | P1 Role (bài nháp) — **Tay** mã đề | |
| A8 | Lịch thi | Tạo lịch giờ VN, gán/hủy gán lớp, lọc | 409 trùng phòng/học viên hiện trong khung gán lớp | P1 Acceptance (API 409) — **Tay** UI gán lớp | |
| A9 | Báo cáo lượt thi | Lọc; Chi tiết; Hủy kết quả có lý do | Dòng "Đã hủy"; lỗi tiếng Việt khi hủy lại | P1 Role, P2 Dashboard | |
| A10 | Nhật ký | Lọc hành động/khoảng giờ | Thấy xuất câu hỏi/tài khoản | P4 Audit, P4 Frontend | |

## Người ra đề (quyền câu hỏi)

| # | Chức năng | Bước kiểm | Kết quả mong đợi | Tự động | Đạt? |
|---|---|---|---|---|---|
| E1 | Menu theo quyền | Đăng nhập | Chỉ thấy mục được cấp | P1 Role | |
| E2 | Ngân hàng câu hỏi | Thêm/sửa câu | Như A6 | P1 Role (mở trang) — **Tay** thao tác | |

## Giảng viên

| # | Chức năng | Bước kiểm | Kết quả mong đợi | Tự động | Đạt? |
|---|---|---|---|---|---|
| T1 | Tổng quan giảng viên | Mở trang | Bảng "Theo lớp" chỉ lớp mình | P4 Teacher Overview, P4 Frontend | |
| T2 | Lớp của tôi | Tìm học viên theo email, thêm, duyệt, xóa | Thêm/duyệt/xóa đúng | P1 Role, P1 Acceptance | |
| T2b | Lớp ngừng hoạt động | Mở lớp có trạng thái ngừng hoạt động | Nút "Thêm vào lớp" bị khóa | **Tay** | |
| T3 | Lịch thi / Kết quả lớp | Mở tab | Đúng lịch và lượt thi của lớp | P1 Role (tab Kết quả) | |

## Học viên

| # | Chức năng | Bước kiểm | Kết quả mong đợi | Tự động | Đạt? |
|---|---|---|---|---|---|
| S1 | Vào lớp bằng mã | Nhập mã lớp | "Chờ duyệt" → "Đã tham gia" sau khi giảng viên duyệt | P1 Role | |
| S2 | Lịch thi | Mở lịch | Thẻ lịch, "Còn n lượt làm bài" | P1 Role, P3 Student Flow | |
| S3 | Làm bài | Chọn đáp án, lưu nháp, tải lại, nộp | Khôi phục đáp án; nộp xong thấy "Điểm:" | P1 Role | |
| S4 | Hết giờ | Để quá hạn | "Đã hết thời gian làm bài", chấm từ nháp | P1 Role | |
| S5 | Kết quả ẩn | Bài không cho xem kết quả | Không lộ điểm; "Chưa công bố kết quả" | P1 Role | |
| S6 | Xem lại bài | Sau giờ kết thúc | Từng câu, "Đáp án đúng"/"Câu trả lời của bạn" | P1 Role | |
| S7 | Rời tab | Chuyển tab khi làm bài | Số lần rời tab tăng | P1 Acceptance (API) — **Tay** trình duyệt thật | |
| S8 | Lịch sử | Mở lịch sử | "Lần x/y", "Đã hủy" khi bị hủy | P1 Role | |

## Ngoài phạm vi UAT lần này
Xếp hạng (F24), đa tổ chức (F25), quên mật khẩu, xuất báo cáo — chưa có API/quyết định (`05-migration/01-parity-matrix.md`).
