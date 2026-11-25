# Nợ kỹ thuật sau To-be T1–T18 (đợt tổng kiểm 25/11/2026)

Danh sách này gom lại từ các bản đánh giá độc lập (`.bangiao/luu-tru/*/danh-gia.md`, không commit) và các ghi chú "chưa kiểm". Mục Cao và Trung bình đều đã sửa trong lát của nó; dưới đây chỉ còn mục Thấp, khoảng thiếu test, và việc để sau theo quyết định.

Mức: **T** = Thấp, **Test** = thiếu test, **QĐ** = cần quyết định hoặc là tính năng. Công: S ≤ 1 giờ · M ≤ ½ ngày · L > ½ ngày.

## Đã sửa trong đợt tổng kiểm
| # | Nguồn | Nội dung | Cách sửa |
|---|---|---|---|
| D1 | T2 N-4 | `QuestionRepository` xóa tag của câu bằng I/O đồng bộ (`RemoveRange(IQueryable)`) | Đổi sang `await …ToListAsync()` |
| D2 | T2 N-5 | Bộ lọc `GET /web/questions` không giới hạn số `tagIds` | Tối đa 50 tag (400); có smoke |
| D3 | T8 N6 | `DELETE /web/review-flags` kéo và giải mã cả snapshot chỉ để kiểm chủ nguồn | Dùng `SourceExistsAsync` (`AnyAsync`, không đọc JSON) |
| D4 | T8 N4 | Bỏ phiên luyện tập đúng lúc đang `PUT /web/review-flags` có thể để lại dòng cờ mồ côi | PUT với `practice` lấy app lock theo học viên như khi bỏ phiên, nên hai thao tác xếp hàng; đúng cả khi bật RCSI |

## Còn lại — Thấp
| # | Nguồn | Nội dung | Đề xuất | Công |
|---|---|---|---|---|
| L1 | T2 N-2 | Kiểm chương/tag của câu hỏi nằm ngoài transaction lưu (TOCTOU): chương bị ẩn hoặc đổi môn đúng lúc lưu câu thì vẫn lưu được | Kiểm lại trong `SaveWithAnswersAsync` (Serializable) | M |
| L2 | T2 N-3 | `PUT /web/questions/{id}` gọi thêm một `GetByIdAsync` chỉ để phân loại audit (restore hay update) | Lấy trạng thái cũ trong cùng lần đọc của repository | S |
| L3 | T8 N5 | Đang lọc "Chỉ câu đã đánh dấu" mà bỏ đánh dấu câu đang xem thì nhảy sang câu khác | Giữ id câu đang xem thay cho chỉ số | S |
| L4 | T9 N-5 | Lịch sử luyện tập/thi thử chưa có cột "thời gian làm bài" | Thêm cột từ `CreatedAtUtc`–`FinishedAtUtc` | S |
| L5 | Dọn nợ T9 | Thi thử hết giờ nhưng chưa chốt hiện "—" ở thời gian làm bài; `Mine` chưa dùng lại helper `Checked` | Dọn khi chạm tới code đó | S |
| L6 | T2 N-6 | Chương đã ẩn không khôi phục được (tạo lại cùng tên sẽ được Id mới) | Đúng kế hoạch T2; thêm "Hiện lại" nếu cần | M |
| L7 | Thử tải | `GET /web/questions` không phân trang: 500 câu mất khoảng 0,14 s (`06-performance.md`) | Thêm `page/pageSize` ở BE, kèm bảng phân trang ở FE, khi ngân hàng lên hàng nghìn câu | M |

## Còn lại — Thiếu test
| # | Nguồn | Nội dung | Ghi chú |
|---|---|---|---|
| X1 | T11 | Worker chốt quá hạn khi DB chưa có migration T11 | Chỉ kiểm bằng đọc code; runbook §8 đã ghi thứ tự migrate → deploy |
| X2 | T13 | File `report.xlsx` chưa được mở bằng Excel hay trình đọc OOXML thật | Checklist UAT R3 ghi **Tay** |
| X3 | T14 | Khóa media/đoạn theo `AnswerSubmissions`/`QuestionAnswers` (bảng cũ, đang trống) | Dùng chung điều kiện với `ExamDetailQuestions`, đã có test |
| X4 | T15 | Race giữa tạo phiên và Admin xóa media (lỗi FK 547) | Đã chặn bằng khóa dòng; chưa có test đồng thời ổn định |
| X5 | T17 | FE chưa có test component cho `ItemAnswerInput` (chọn, chuyển vế, bỏ chọn) | Smoke trình duyệt T17 bao phủ đường chính; repo chưa có thư viện test component |
| X6 | T18 | Deadlock khi lưu đoạn và lưu mã đề cùng lúc (→ 409 qua `DeadlockAsConflict`) | Chưa tái hiện được deadlock thật trong smoke |
| X7 | T8 N7 | Smoke review-flags còn thiếu: thành viên lớp chưa duyệt / lớp đóng → 404; PUT/DELETE practice bởi người khác | Thêm vào smoke T8 |
| X8 | Thử tải | Chưa đo tải đồng thời (nhiều học viên bắt đầu thi cùng lúc) | `PerfBenchmark.ps1` chỉ đo tuần tự; cần công cụ riêng, chẳng hạn k6 |

## Để sau theo quyết định (QĐ)
| # | Nội dung | Trạng thái |
|---|---|---|
| Q-1 | Migration rỗng `20261125005237_2511check` trên ProjectDACN | QTK-1 (a): người dùng tự gỡ (`Update-Database -Migration 20261124115930_ToBeT18QuestionGroups`, rồi `Remove-Migration`) |
| Q-2 | Thời hạn lưu dữ liệu (Q15) | Giữ "chưa xóa tự động"; hướng dẫn dọn tay ở runbook §9 |
| Q-3 | Excel nhập/xuất đoạn dùng chung | QT18-4 (b), chưa làm |
| Q-4 | Gộp tag (Admin) | T2 ngoài phạm vi |
| Q-5 | Chấm từng phần cho loại nhiều đáp án / ghép cặp / nhiều mệnh đề | Q3/Q10b đã chốt tất cả-hoặc-không |
| Q-6 | URL media là bearer (ai có URL đều xem được tới khi hết hạn) | Thiết kế Q11b; đã ghi trong runbook §8 |
