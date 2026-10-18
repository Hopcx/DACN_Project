# Task09 membership và phần độc lập Task10 — 2026-10-11

## Checkout và phạm vi

BE `dev/Hop` tại `1d95f71da6ff719c75de182b13a0f0368c53d157`, FE `dev/Hop` tại `c008165d57a74ec836184b2b97f3fa8d94fa24e1`, trước các thay đổi của lần này. Giữ nguyên thay đổi người dùng ở `.gitmodules`, `AuthController.cs`, DOCX bị xóa, gitlink `impeccable` và hai file export `Gittodoc_testify.txt`.

Chỉ ghi fixture trên `ProjectDACN_Task04Task09_Test`. Preflight đọc history có đúng initial → Task04AuthLifecycle → Task09ExamScheduleDirection → Task09ScheduleUtcProvenance. Có 74 lịch, 16 lịch `IsTimeUtc=0`. API chạy với `ConnectionStrings__Default` chỉ rõ tên DB bản sao. Không tạo hoặc áp migration trong lần này.

## Task09 — SQL Server/API thật

Lệnh: `pwsh -NoProfile -File tests/Task09MembershipConcurrencySmoke.ps1 -BaseUrl https://localhost:7255`.

Script tạo tài khoản QA qua register/verify/login, cấp role Admin chỉ cho actor mới trong bản sao; tạo hai Student synthetic riêng. Hai HTTP PUT duyệt membership được gửi trước khi chờ response. `ClassMembershipStore` hiện có khóa `DACN:schedule-write` trong transaction và khóa hàng lớp; không cần sửa logic nghiệp vụ sau nghiệm thu.

| Ca | Fixture | Trước | HTTP | Sau |
|---|---|---|---|---|
| Một học viên, hai lớp, lịch giao nhau | Exam 16; classes 43/44; schedules 1076/1077; memberships 33/34 | Cả hai status 2; sĩ số 0/1 mỗi lớp | 200,409 | 33 vẫn status 2; 34 status 1; sĩ số 0/1 và 1/1 |
| Hai lịch `[08:00,09:00)` và `[09:00,10:00)` UTC | Exam 17; classes 45/46; schedules 1078/1079; memberships 35/36 | Cả hai status 2; sĩ số 0/1 mỗi lớp | 200,200 | Cả hai status 1; sĩ số 1/1 mỗi lớp |
| Hai học viên tranh chỗ cuối cùng một lớp | Exam 18; class 47 capacity 1; memberships 37/38 | Cả hai status 2; sĩ số 0/1 | 200,409 | 37 status 1; 38 vẫn status 2; sĩ số 1/1 |

Script kiểm từng membership theo response, so sĩ số với capacity và dọn Exam/Class/Schedule/ClassUser/ClassExamSchedule theo ID fixture. Các tài khoản QA được giữ trong bản sao có thể tạo lại từ backup đã ghi ở `12-data-readiness.md`; tag lần đạt: `4ccbb96026`. Các lần bootstrap ban đầu gặp thiếu `QUOTED_IDENTIFIER` và auth 429; đã sửa sqlcmd dùng `-I` và retry 429 có giới hạn, sau đó toàn bộ ba ca đạt. Không tính các lần lỗi fixture là bằng chứng đạt.

Task09 còn thiếu ca concurrent approval đã được đóng. F16 vẫn IN_PROGRESS ở mức parity toàn bộ dữ liệu: lịch sử chưa rõ timezone chưa được đối soát. Phạm vi lịch UTC DACN đã có bằng chứng các ca trên và các ca của ngày 2026-10-08. Triển khai DB gốc và ETL là các bước riêng, không thực hiện trong lần này.

## Task10 — phần đã triển khai và kiểm thật

`GET /web/student/schedules` bổ sung `serverNow` UTC `Z` và `scheduleState`: `upcoming`, `open`, `ended`, `unknown`. BE lấy một mốc `DateTime.UtcNow` sau truy vấn, tính `[start,end)` cho lịch có provenance UTC; lịch cũ luôn `unknown`. Đây là trạng thái khoảng giờ lịch, không phải trạng thái lượt làm bài hoặc xác nhận đủ điều kiện bắt đầu.

FE `/student/schedules` hiển thị nhãn từ API, tải lại mỗi 30 giây/khi focus và bằng nút Tải lại. Không dùng giờ máy khách để quyết định trạng thái. Không thêm nút bắt đầu giả hoặc gọi CRUD Submission để nộp bài.

Lệnh browser: `node tests/studentScheduleBrowserSmoke.mjs`, Chrome headless → Vite HTTPS → API → SQL Server bản sao. Fixture gồm 4 lịch thuộc lớp đã duyệt, một lịch lớp đang chờ duyệt, một lịch lớp không có membership. Kết quả:

- API trả đúng 4 lịch với 4 trạng thái, `serverNow` có `Z`, lịch unknown không bị thêm offset; không có `IsCorrect` trong payload lịch.
- Lịch chưa duyệt và lịch không thuộc lớp bị loại khỏi danh sách.
- Sau khi giả `Date.now()` của browser thành 0, UI vẫn hiển thị đúng trạng thái từ server.
- Đổi giờ fixture open trong SQL thành đã hết hạn, click Tải lại: UI đổi sang “Đã hết giờ”.
- Dọn cả 6 lịch, các lớp/link/membership và Exam fixture sau kiểm; QA account giữ ở bản sao.

Build BE đạt 0 lỗi; FE lint/build đạt (build vẫn có cảnh báo chunk >500 kB). Postflight: history 4, schedules 74, unknown 16, orphan schedule 0. DB gốc chỉ đọc để xác nhận history 1, Users 1, Exams/Schedules 0.

## Task10 — schema và quyết định đang chờ

Ở lần kiểm đầu chưa có `VibeCoding/Task10.md`; file hiện đã có và đã được đọc lại. Phạm vi file là start/resume/save, snapshot, concurrency, deadline server và trang làm bài. `00-overview/04-decisions.md` O05/O06 vẫn để OPEN về cách tính/phạm vi lượt và deadline. Chấm nhiều đáp án/rounding/ngưỡng đạt thuộc phần finalize của yêu cầu mở rộng, không chặn việc chuẩn bị snapshot/draft.

Source và schema SQL đã đối chiếu:

- DoingExams chỉ có UserId/ExamId/ExamDetailId/InProgress/StartTime/ExamScheduleId/Content; chưa có deadline, revision, snapshot typed, unique active attempt.
- Submissions chưa có AttemptId unique; TotalMark là float, TimeTaken là SQL time; không phù hợp để tự đổi hàng lịch sử sang decimal/duration khi chưa có policy.
- AnswerSubmissions tham chiếu Question/Answer sống, chưa lưu snapshot nội dung/đáp án đúng. Các route ghi CRUD Submission/AnswerSubmission hiện `[NonAction]`, không phải API nộp bài.
- Contract attempt trong `04-contracts/02-exam-api.md` vẫn PROPOSED. Route đọc Submission hiện yêu cầu AdminManagement; API kết quả của học viên cần route và ownership riêng, không mở rộng quyền đọc toàn bộ CRUD cũ.

Thiết kế cần thực hiện sau khi chốt policy: thêm provenance/version cho lượt mới; deadline UTC, revision và trạng thái finalize; bảng snapshot câu hỏi/options/điểm và draft riêng; liên kết Submission–Attempt unique; thời lượng giây và điểm decimal cho lượt mới, giữ nguyên lịch sử cũ. Start/Save/Submit phải dùng khóa/giao dịch, kiểm owner và snapshot, không nhận UserId/điểm từ client. Không sinh migration trước khi chốt semantics ảnh hưởng dữ liệu này.

Chưa có bằng chứng API/SQL cho start/resume/save/submit/grading, vượt số lượt, nộp lặp, save/submit đồng thời, owner của attempt, release kết quả hoặc không lộ đáp án trong **attempt response thực tế**. Không dùng test payload lịch thay cho test an toàn đề thi. F17/F18 IN_PROGRESS; F19 OPEN. Chưa có luồng Task10 xuyên suốt.

## Tiếp tục chuẩn bị snapshot/draft

Thêm `Project.Domain/ValueObjects/AttemptSnapshot.cs`, DTO học viên trong `Project.Application/DTOs/AttemptDTO` và `AttemptSnapshotFactory`. Factory kiểm mã đề công khai và câu/đáp án hợp lệ theo loại 1–3 hiện có, sao chép nội dung/cờ đúng/điểm nguồn, tạo Guid riêng cho câu/options. DTO allowlist loại bỏ cờ đúng, ID nguồn và điểm. ValidateDraft kiểm quyền sở hữu lựa chọn trong snapshot, không cho trùng câu/đáp án hoặc nhiều lựa chọn cho loại 1/2; danh sách rỗng xóa toàn bộ lựa chọn, câu bỏ qua thành chưa trả lời. Điểm vẫn là double nguồn, chưa áp thuật toán chấm hay rounding.

Lệnh đã đạt: `dotnet run --project tests/DependencyInjectionSmoke -- --attempt-snapshot`. Kiểm snapshot độc lập với thay đổi entity nguồn, JSON round-trip, allowlist chính xác DTO học viên, draft tách khỏi list client, câu bỏ qua/rỗng, nhiều lựa chọn, và các payload sai. Đây là **test cô lập**, không SQL/API; factory chưa nối vào controller/persistence. Không thêm package, schema hoặc migration. Không đưa baseline deadline/tính lượt vào code khi chưa có lựa chọn của người dùng.
