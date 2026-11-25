# Nợ kỹ thuật sau To-be T1–T18 (đợt tổng kiểm 25/11/2026, cập nhật sau T19)

Danh sách này gom lại từ các bản đánh giá độc lập (`.bangiao/luu-tru/*/danh-gia.md`, không commit) và các ghi chú "chưa kiểm". Mục Cao và Trung bình đều đã sửa trong lát của nó; dưới đây chỉ còn mục Thấp, khoảng thiếu test, và việc để sau theo quyết định.

Mức: **T** = Thấp, **Test** = thiếu test, **QĐ** = cần quyết định hoặc là tính năng. Công: S ≤ 1 giờ · M ≤ ½ ngày · L > ½ ngày.

## Đã sửa trong đợt tổng kiểm
| # | Nguồn | Nội dung | Cách sửa |
|---|---|---|---|
| D1 | T2 N-4 | `QuestionRepository` xóa tag của câu bằng I/O đồng bộ (`RemoveRange(IQueryable)`) | Đổi sang `await …ToListAsync()` |
| D2 | T2 N-5 | Bộ lọc `GET /web/questions` không giới hạn số `tagIds` | Tối đa 50 tag (400); có smoke |
| D3 | T8 N6 | `DELETE /web/review-flags` kéo và giải mã cả snapshot chỉ để kiểm chủ nguồn | Dùng `SourceExistsAsync` (`AnyAsync`, không đọc JSON) |
| D4 | T8 N4 | Bỏ phiên luyện tập đúng lúc đang `PUT /web/review-flags` có thể để lại dòng cờ mồ côi | PUT với `practice` lấy app lock theo học viên như khi bỏ phiên, nên hai thao tác xếp hàng; đúng cả khi bật RCSI |

## Đã sửa trong T19 (dọn nợ)
| # | Nguồn | Nội dung | Cách sửa |
|---|---|---|---|
| L1 | T2 N-2 | Kiểm chương/tag của câu hỏi nằm ngoài transaction lưu (TOCTOU) | `SaveWithAnswersAsync` kiểm lại trong transaction Serializable (`QuestionMetadataException` → 400 cùng message); `QuestionController` có `[DeadlockAsConflict]` (409) |
| L2 | T2 N-3 | `PUT /web/questions/{id}` đọc thêm một lần chỉ để phân loại audit | Repository trả `PreviousStatus` (không lưu DB, không trả JSON) |
| L3 | T8 N5 | Đang lọc "Chỉ câu đã đánh dấu" mà bỏ đánh dấu câu đang xem thì nhảy sang câu khác | Câu vừa bỏ đánh dấu được giữ trong danh sách lọc tới khi bật/tắt lại bộ lọc (`keptAfterToggle`), cả trang xem lại bài thi và luyện tập |
| L4 | T9 N-5 | Lịch sử luyện tập/thi thử chưa có cột "thời gian làm bài" | **Đã làm từ đợt dọn nợ sau T9** (`durationSeconds`); bảng nợ ghi nhầm |
| L5 | Dọn nợ T9 | Thi thử hết giờ chưa chốt hiện "—"; `Mine` không dùng lại helper `Checked` | `durationSeconds = hạn − lúc tạo` khi `expired`; dùng `Checked(string)` |
| L7 | Thử tải | `GET /web/questions` không phân trang | Phân trang khi gửi `page`/`pageSize` (`{ page, pageSize, totalCount, items }`); không gửi thì vẫn mảng. Ngân hàng câu hỏi phân trang phía server; dashboard chỉ đọc `totalCount` |
| X7 | T8 N7 | Smoke review-flags còn thiếu ca | Thêm: thành viên chưa duyệt / lớp đóng → 404; người khác PUT/DELETE phiên → 404; PUT trên phiên đã bỏ → 404; PUT song song với bỏ phiên (3 vòng) không để cờ mồ côi |

## Còn lại — Thấp
| # | Nguồn | Nội dung | Đề xuất | Công |
|---|---|---|---|---|
| L6 | T2 N-6 | Chương đã ẩn không khôi phục được (tạo lại cùng tên sẽ được Id mới) | Đúng kế hoạch T2; thêm "Hiện lại" nếu cần (QT19-1 để sau) | M |

## Còn lại — Thiếu test
| # | Nguồn | Nội dung | Ghi chú |
|---|---|---|---|
| X1 | T11 | Worker chốt quá hạn khi DB chưa có migration T11 | Chỉ kiểm bằng đọc code; runbook §8 đã ghi thứ tự migrate → deploy |
| X2 | T13 | File `report.xlsx` chưa được mở bằng Excel hay trình đọc OOXML thật | Checklist UAT R3 ghi **Tay** |
| X3 | T14 | Khóa media/đoạn theo `AnswerSubmissions`/`QuestionAnswers` (bảng cũ, đang trống) | Dùng chung điều kiện với `ExamDetailQuestions`, đã có test |
| X4 | T15 | Race giữa tạo phiên và Admin xóa media (lỗi FK 547) | Đã chặn bằng khóa dòng; chưa có test đồng thời ổn định |
| X5 | T17 | FE chưa có test component cho `ItemAnswerInput` (chọn, chuyển vế, bỏ chọn) | Smoke trình duyệt T17 bao phủ đường chính; repo chưa có thư viện test component |
| X6 | T18 | Deadlock khi lưu đoạn và lưu mã đề cùng lúc (→ 409 qua `DeadlockAsConflict`) | Chưa tái hiện được deadlock thật trong smoke |
| X8 | Thử tải | Chưa đo tải đồng thời (nhiều học viên bắt đầu thi cùng lúc) | `PerfBenchmark.ps1` chỉ đo tuần tự; cần công cụ riêng, chẳng hạn k6 |
| X9 | T19 | Kiểm lại chương/tag trong transaction lưu câu (L1) chỉ kiểm bằng unit test với repository giả | Khe thời gian giữa lần kiểm sớm và transaction quá hẹp để tái hiện ổn định bằng smoke API |

## Để sau theo quyết định (QĐ)
| # | Nội dung | Trạng thái |
|---|---|---|
| Q-1 | Migration rỗng `20261125005237_2511check` trên ProjectDACN | **Xong:** người dùng đã gỡ; ProjectDACN còn 23 migration |
| Q-2 | Thời hạn lưu dữ liệu (Q15) | Giữ "chưa xóa tự động"; hướng dẫn dọn tay ở runbook §9 |
| Q-3 | Excel nhập/xuất đoạn dùng chung | QT18-4 (b), chưa làm |
| Q-4 | Gộp tag (Admin) | T2 ngoài phạm vi |
| Q-5 | Chấm từng phần cho loại nhiều đáp án / ghép cặp / nhiều mệnh đề | Q3/Q10b đã chốt tất cả-hoặc-không |
| Q-6 | URL media là bearer (ai có URL đều xem được tới khi hết hạn) | Thiết kế Q11b; đã ghi trong runbook §8 |
