# To-be P0 — As-is, Gap Analysis và quyết định nghiệp vụ

Nguồn yêu cầu: `../VibeCoding/DACN_ToBe_Feature_Specification.md` (gọi tắt "đặc tả"). Khảo sát ngày 17/11/2026 trên BE `dev/Hop` `b9aa3f0`, FE `dev/Hop` `7e86ec2`, DB `ProjectDACN` (12/12 migration) và bản sao `ProjectDACN_Task04Task09_Test` — chỉ đọc. **FACT** = thấy trong code/DB (có nguồn). Chưa sửa code.

## 1. As-is Assessment (phần liên quan đặc tả)

### 1.1 Ngân hàng câu hỏi
- Entity `Question` (`Project.Domain/Entities/Question.cs`): `Content`, `Status` (byte: 1 hoạt động, 0 tạm ngừng, 255 đã ẩn), `SubjectId` (bắt buộc), `QuestionTypeId`, `QuestionLevelId` (nullable), `DocumentPath` (nullable, **không có chức năng nào ghi**), audit `CreatedBy/UpdatedBy/...`. Đáp án `Answer` (`Content`, `IsCorrect`, `Status`).
- Loại câu hỏi (`dbo.QuestionTypes`): 1 Đúng/Sai (đúng 2 đáp án "Đúng"/"Sai"), 2 Chọn một, 3 Chọn nhiều. Validation ở `QuestionService.Validate` (2–20 đáp án, nội dung khác nhau, số đáp án đúng theo loại).
- Mức độ (`dbo.QuestionLevels`): bảng tra tên tự do, **0 dòng ở cả `ProjectDACN` và bản sao** (smoke tạo rồi dọn theo tag). Không có Chương, không có Tag, không có lời giải.
- API `GET/POST/PUT/DELETE /web/questions` lọc server theo `textSearch, subjectId, questionTypeId, questionLevelId`; quyền `QuestionManagement` (permission 2) hoặc Admin. Câu đã dùng trong đề/bài nộp **không được sửa/ẩn** (409). Nhập/xuất Excel v1 (`/web/questions/import`, `/export`).
- Dữ liệu: `ProjectDACN` 0 câu hỏi, 0 bài thi; bản sao QA 0 câu hỏi sau dọn.

### 1.2 Bài thi, mã đề, lượt thi
- `Exam` (môn, số câu, số lần làm, thời gian, điểm tối đa/đạt, `AllowViewResult`, trạng thái nháp/công khai) → `ExamDetail` (mã đề) → `ExamDetailQuestion` (câu + `Point`). Tạo mã đề chọn tay **và ngẫu nhiên theo mức độ** (`ExamVariantStore`, `RandomSelections` theo `QuestionLevelId`).
- Lượt thi `ExamAttempt` **gắn bắt buộc với `ExamScheduleId`** (lịch thi + lớp). `SnapshotJson` chụp câu/đáp án/điểm lúc bắt đầu (`AttemptSnapshot` có `Version`, `Project.Domain/ValueObjects/AttemptSnapshot.cs`); nháp `DraftJson` + `Revision` chống ghi đè; deadline server; worker `ExpiredAttemptFinalizerService` chốt quá hạn; nộp idempotent; chấm `AttemptScoring.Calculate` — **mọi loại đều tất cả hoặc không** (thiếu/thừa lựa chọn = 0 điểm).
- Hủy kết quả (void) có lý do; rời tab (`AttemptVisibilityEvents`) đếm server.

### 1.3 Xem lại bài
- `GET /web/attempts/{id}/review`: chỉ chủ bài, sau khi lịch kết thúc, khi `AllowViewResult`, không bị hủy; nội dung từ snapshot (đáp án đã chọn + `isCorrect`). FE P5.5 hiển thị từng câu, "Đáp án đúng", "Câu trả lời của bạn".
- Không có lời giải. "Đánh dấu câu" chỉ lưu **sessionStorage** trình duyệt trong lúc làm bài (`DACN_FE/src/pages/attempts/StudentAttemptPage.jsx` `attempt-flags:{id}`), bị xóa khi nộp — không có ở server, không có ở review.

### 1.4 Báo cáo
- Học viên: lịch sử lượt thi `/web/attempts/mine` (điểm theo lượt), không thống kê theo chương/câu.
- Giảng viên: `/web/teacher/overview` (tổng hợp theo lớp mình phụ trách). Admin: `/web/admin/attempt-reports` (+ phổ điểm theo bài/lớp/môn), `/web/admin/dashboard`. Export Excel chỉ có tài khoản (admin) và câu hỏi (QuestionManagement); không có export báo cáo.
- Xếp hạng (F24) chưa có — parity matrix ghi OPEN.

### 1.5 Phân quyền, audit, media
- Role theo `Levels` (1 Admin, 2 Examiner, 3 Teacher, 4 Student) + permission 1 bài thi, 2 câu hỏi, 3 môn, 4 lịch; Admin vượt mọi policy; FallbackPolicy yêu cầu đăng nhập.
- Audit nghiệp vụ `dbo.Logs` qua `AuditWriter`: hiện chỉ `account.export`, `account.import`, `question.export`; void lưu ngay trên `ExamAttemptSubmissions`. Log kỹ thuật Serilog.
- Upload: chỉ nhận `.xlsx` (nhập câu hỏi, nhập tài khoản). Không có lưu trữ file/media.

## 2. Gap Analysis

`Implemented` = có và đã nghiệm thu; `Partial` = có một phần; `Missing` = chưa có; `Unknown` = chưa đủ căn cứ.

| Mục đặc tả | Yêu cầu | Trạng thái | Căn cứ / thiếu gì |
|---|---|---|---|
| 3.1 | Phân loại theo Môn | Implemented | `Question.SubjectId`, lọc server |
| 3.1 | Chương | Missing | Không có entity/cột |
| 3.1 | Độ khó Dễ/TB/Khó | Partial | Có `QuestionLevel` (bảng tra tên tự do, nullable, đang dùng cho chọn ngẫu nhiên mã đề) nhưng 0 dòng và không cố định 3 mức |
| 3.1 | Tag | Missing | — |
| 3.1 | Lọc kết hợp môn/chương/độ khó/tag | Partial | Lọc môn/loại/mức độ/nội dung có; chương/tag chưa |
| 3.1 | Quyền thao tác qua API | Implemented | Policy `QuestionManagement`, test `TaskAdminPermissionApiSmoke` |
| 2.2 | Trạng thái nháp/xuất bản/ngừng | Partial | 1/0/255 + khóa khi đã dùng; chưa có "nháp" tách khỏi "ngừng" |
| 2.2/4.2 | Snapshot khi làm bài | Implemented (Exam) | `SnapshotJson` mỗi lượt; Practice/Mock chưa có |
| 3.2 | Practice Mode | Missing | Engine lượt thi gắn lịch thi; không có phiên tự tạo |
| 3.2 | Hiện đáp án sau từng câu/cuối phiên (BE quyết) | Missing | — |
| 3.3 | Mock Exam ngẫu nhiên từ ngân hàng | Missing (hạ tầng Partial) | Có chọn ngẫu nhiên theo mức độ khi tạo mã đề, có timer/finalize cho Exam |
| 3.3 | So sánh lớp / xếp hạng | Missing | F24 OPEN, thiếu quy tắc |
| 3.4 | Review: câu trả lời, đúng/sai, đáp án đúng | Implemented (Exam) | `/review` theo snapshot |
| 3.4 | Lời giải chi tiết | Missing | Không có trường |
| 3.4 | Đánh dấu cần xem lại (lưu server) | Missing | Chỉ sessionStorage lúc làm bài |
| 3.5 | Trắc nghiệm có hình / âm thanh | Missing | `DocumentPath` không dùng |
| 3.5 | Đúng/Sai | Partial | 1 mệnh đề (loại 1); nhiều mệnh đề chưa |
| 3.5 | Ghép cặp, điền ngắn, đoạn dùng chung | Missing | Snapshot/chấm chỉ theo lựa chọn |
| 3.5 | Chấm từng phần | Missing | Tất cả hoặc không |
| 3.6 | Upload/preview media, kiểm nội dung file, quyền truy cập | Missing | — |
| 3.7 | Phân tích Student (tiến bộ, theo chương, câu hay sai) | Missing (Partial lịch sử) | Có lịch sử điểm; không theo chương/câu |
| 3.8 | Phân tích Teacher + export | Partial | Tổng quan theo lớp; không theo chương, không export |
| 4.1 | Phân quyền dùng hệ thống sẵn có | Implemented | Mở rộng policy hiện có |
| 4.3 | Lưu tiến độ, chống ghi đè, idempotent | Implemented (Exam) | `Revision`, submit idempotent; Practice cần dùng lại |
| 4.4 | Audit sự kiện câu hỏi/làm bài | Partial | Chỉ export/import + void; chưa audit tạo/sửa câu, bắt đầu/nộp |
| 4.5 | Chuẩn bị AI | Không làm | Đặc tả cấm thêm bảng/cột vì giả định AI |

## 3. Business Decisions

### 3.1 Quy tắc đã có trong code/tài liệu (giữ nguyên)
- BE quyết quyền, thời gian, đáp án đúng, điểm; không lộ đáp án trong API làm bài (`docs/agent-rules/AGENTS.md` mục Bất biến).
- Câu hỏi đã dùng trong đề/bài nộp không sửa được; sửa câu không đổi kết quả cũ nhờ snapshot.
- Chấm hiện tại: tất cả hoặc không, làm tròn 2 chữ số, so điểm đạt trước khi làm tròn.
- Xem kết quả Exam: sau giờ kết thúc lịch, `AllowViewResult`, không bị hủy.
- Báo cáo DACN tách nguồn (`dacnAttemptsOnly`) khỏi dữ liệu cũ.

### 3.2 Câu hỏi cần người dùng xác nhận (không tự quyết)

**QUYẾT ĐỊNH (17/11/2026):** người dùng chấp thuận toàn bộ cột "Đề xuất" của Q1–Q19 bên dưới. Các mục ghi "Chốt khi vào Phase 2/3" (Q10–Q14) và "Để Phase sau" (Q8) vẫn còn mở cho tới Phase tương ứng.
Đánh số theo đặc tả mục 6 (Q1–Q15), thêm Q16–Q19 phát sinh khi khảo sát. **Đề xuất** chỉ để tham khảo.

| # | Câu hỏi | Ảnh hưởng | Đề xuất |
|---|---|---|---|
| Q1 | Chương thuộc một Môn hay nhiều-nhiều? | Schema Chapter | Thuộc một Môn (FK `SubjectId`) |
| Q2 | Tag do Admin quản lý hay Teacher tự tạo? | Quyền, trùng lặp | Người có quyền câu hỏi tạo; Admin gộp/xóa |
| Q3 | Nhiều đáp án đúng: giữ tất cả-hoặc-không hay chấm từng phần? | Scoring | Giữ tất cả-hoặc-không ở Phase 1 |
| Q4 | Practice lưu lịch sử bao lâu? Làm lại cùng bộ câu? | Lưu trữ, API | Lưu vô thời hạn; có "Làm lại bộ này" |
| Q5 | Hiện đáp án sau từng câu hay cuối phiên — ai cấu hình? | Practice API | Học viên chọn khi tạo phiên |
| Q6 | Mock Exam là loại Exam riêng hay chế độ phiên làm bài? | Kiến trúc | Chế độ của phiên tự luyện (có giới hạn giờ), không phải Exam |
| Q7 | Mock Exam có tính vào điểm lớp/xếp hạng? | Báo cáo | Không |
| Q8 | So sánh điểm theo lớp, toàn hệ thống hay nhóm? | Thống kê | Để Phase sau (cùng F24) |
| Q9 | Ai xem lời giải, khi nào? | Review | Như đáp án đúng của từng chế độ |
| Q10 | Chấm câu điền ngắn theo quy tắc nào? Teacher duyệt? | Phase 2 | Chốt khi vào Phase 2 |
| Q11 | Media lưu local, object storage hay dịch vụ có sẵn? | Phase 2 | Chốt khi vào Phase 2 |
| Q12 | Giới hạn định dạng/dung lượng/thời lượng audio? | Phase 2 | Chốt khi vào Phase 2 |
| Q13 | Định nghĩa "yếu/top/bottom"? | Phase 3 | Chốt khi vào Phase 3 |
| Q14 | Export báo cáo định dạng và cột nào? | Phase 3 | Chốt khi vào Phase 3 |
| Q15 | Thời hạn lưu bài làm, audit, media? | Lưu trữ | Chưa xóa tự động cho tới khi có chính sách |
| Q16 | "Độ khó" dùng lại bảng `QuestionLevels` (seed 3 mức Dễ/TB/Khó) hay thêm cột mới giữ `QuestionLevels` cho mục khác? | Schema, chọn ngẫu nhiên mã đề | Dùng lại `QuestionLevels`, seed 3 mức (DB đang 0 dòng nên không xung đột) |
| Q17 | Trạng thái câu hỏi: thêm "Nháp" riêng, hay giữ 1/0/255 (0 = nháp/tạm ngừng)? | Lọc câu cho Practice/Mock | Giữ 1/0/255; chỉ lấy câu status 1 |
| Q18 | Học viên được luyện tập môn nào: mọi môn đang hoạt động, hay chỉ môn của lớp đã tham gia? | Quyền Practice | Chỉ môn của lớp đã được duyệt (`Class.SubjectId`) |
| Q19 | Phạm vi lát đầu tiên sau khi chốt: chỉ Phase 1 mục 3.1 (metadata), hay 3.1 + 3.2 + 3.4? | Kế hoạch | Làm tuần tự: T1–T3 (3.1) → T4–T7 (3.2) → T8–T9 (3.4) → Mock sau |
