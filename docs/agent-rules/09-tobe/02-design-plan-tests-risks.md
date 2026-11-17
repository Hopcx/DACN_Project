# To-be P0 — Thiết kế kỹ thuật, kế hoạch triển khai, test và rủi ro

Đọc kèm [01-asis-gap-decisions.md](01-asis-gap-decisions.md). Toàn bộ là **PROPOSED**, viết theo các phương án "Đề xuất" ở mục 3.2 của file đó. Nếu người dùng chọn khác, sửa tài liệu này trước khi viết code. Phase 2 và 3 chỉ phác thảo, vì phụ thuộc Q10–Q14.

## 4. Technical Design (Phase 1)

### 4.1 Metadata ngân hàng câu hỏi (3.1)
- **DB** (migration mới, chỉ thêm, nullable):
  - `Chapters` (`Id`, `SubjectId` FK `Subjects`, `Name` nvarchar(200), `SortOrder` int, `Status` byte 1/0/255, audit). Unique `(SubjectId, Name)` khi `Status <> 255`.
  - `Questions.ChapterId` int NULL FK `Chapters`; kiểm ở service: chương phải cùng môn với câu.
  - `Tags` (`Id`, `Name` nvarchar(50) unique theo chữ thường/không dấu, `Status`), `QuestionTags` (`QuestionId`, `TagId`) PK kép.
  - `Questions.Explanation` nvarchar(4000) NULL (lời giải, dùng cho 3.2/3.4).
  - Độ khó (Q16): seed 3 dòng `QuestionLevels` "Dễ", "Trung bình", "Khó" bằng migration `InsertData` có kiểm tồn tại; không đổi cột.
- **Application/API**: mở rộng `QuestionCreateDto`/`QuestionResponseDto` thêm `chapterId`, `tagIds`, `explanation` (tùy chọn → FE cũ vẫn chạy); `GET /web/questions` thêm `chapterId`, `tagIds` (AND giữa các tiêu chí, OR trong danh sách tag); CRUD `GET/POST/PUT/DELETE /web/chapters` (policy `SubjectManagement` hoặc `QuestionManagement` — chốt khi code), `GET/POST /web/tags` (policy `QuestionManagement`). Excel v1 giữ nguyên; thêm cột tùy chọn ở v2 sau.
- **Snapshot**: `AttemptQuestionSnapshot` thêm `Explanation` (nullable) và tăng `AttemptSnapshot.Version` lên 2; đọc snapshot v1 → `Explanation = null`.
- **FE**: `QuestionBankPage` thêm Select chương (phụ thuộc môn), Select tag (multiple, tạo mới nếu có quyền), ô lời giải; bộ lọc chương/tag. Trang quản lý chương (trong Quản lý môn học: Modal "Chương").

### 4.2 Practice Mode (3.2) — tách khỏi Exam
- **DB**: `PracticeSessions` (`Id` Guid, `UserId`, `SubjectId`, `CriteriaJson` (chương/độ khó/tag/số câu), `RevealMode` ('perQuestion'|'end'), `Kind` ('practice' — chừa 'mock' cho 4.3), `State` ('inProgress'|'finished'), `SnapshotJson`, `DraftJson`, `Revision`, `CreatedAtUtc`, `FinishedAtUtc` NULL, `DeadlineUtc` NULL (Practice luôn NULL), `CorrectCount`, `QuestionCount`). Index `(UserId, CreatedAtUtc)`.
- **Chọn câu**: chỉ câu `Status = 1` của môn (và chương/độ khó/tag theo bộ lọc); ngẫu nhiên không lặp; thiếu câu → 409 `PRACTICE_NOT_ENOUGH_QUESTIONS` kèm số câu có. Quyền: Student, môn thuộc lớp đã duyệt (Q18).
- **Snapshot**: tái dùng builder kiểu `AttemptSnapshotFactory` với điểm mỗi câu = 1; lưu cả `Explanation`.
- **API** `/web/student/practice`: `POST` (tạo), `GET mine` (lịch sử, phân trang), `GET {id}` (câu không đáp án + nháp), `PUT {id}/answers` (revision), `POST {id}/questions/{qid}/check` (chỉ khi `RevealMode=perQuestion`: chốt câu đó, trả đúng/sai + đáp án + lời giải; câu đã chốt không đổi được), `POST {id}/finish` (idempotent, chấm bằng `AttemptScoring`), `GET {id}/review` (sau finish).
- **Không** dùng deadline/worker của Exam; không ghi vào `ExamAttempts`; báo cáo Exam không đọc `PracticeSessions`.
- **FE**: trang Học viên "Luyện tập" (form tiêu chí → làm bài dùng lại khung `attempt-layout`/`attempt-palette`, có nút "Kiểm tra câu này" khi perQuestion) + lịch sử luyện tập.

### 4.3 Mock Exam (3.3) — sau Practice
- Theo Q6 đề xuất: `PracticeSessions.Kind = 'mock'` + `DeadlineUtc` = tạo + thời lượng; `RevealMode` cố định 'end'; worker riêng (hoặc mở rộng finalizer, cùng nguyên tắc idempotent) chốt quá hạn. Không lộ đáp án trước finish. So sánh/xếp hạng (Q8) **không** làm cho tới khi chốt quy tắc.

### 4.4 Review và đánh dấu (3.4)
- `ReviewFlags` (`UserId`, `SourceKind` 'exam'|'practice', `SourceId` Guid, `SnapshotQuestionId` Guid, `CreatedAtUtc`), PK `(UserId, SourceKind, SourceId, SnapshotQuestionId)`. API `PUT/DELETE /web/review-flags` (chỉ chủ lượt/phiên), trả kèm trong review. Cho phép đánh dấu cả lúc làm bài (thay sessionStorage) và lúc review.
- Exam review trả thêm `explanation` (snapshot v2) theo đúng điều kiện hiện tại; Practice review sau finish (hoặc từng câu đã "check").

### 4.5 Phase 2–3 (phác thảo, chờ Q10–Q14)
- Loại câu mới: thêm `QuestionTypeId` 4+ và bảng/thuộc tính đặc thù theo loại (vd. `MatchingPairs`, `AcceptedAnswers` cho điền ngắn) thay vì JSON tự do; validator + scorer theo loại; đoạn dùng chung = `QuestionGroups` (nội dung/media) + `Questions.GroupId`.
- Media: `MediaFiles` (metadata + đường dẫn lưu trữ), kiểm chữ ký file (magic bytes) + giới hạn cấu hình, endpoint tải có kiểm quyền; đếm tham chiếu trước khi xóa.
- Phân tích: truy vấn tổng hợp trên snapshot + đáp án đã nộp theo chương/câu; định nghĩa chỉ số trước khi code.

### Trade-off chính
- Practice tách bảng thay vì tái dùng `ExamAttempts`: tránh đụng ràng buộc lịch/lớp/deadline và báo cáo `dacnAttemptsOnly`; đổi lại có một ít code trùng (đã giảm bằng cách dùng chung snapshot builder và `AttemptScoring`).
- Độ khó dùng lại `QuestionLevels`: không đổi cột, giữ chọn ngẫu nhiên mã đề đang chạy; đổi lại tên mức độ là dữ liệu (cần seed và khóa sửa 3 mức hệ thống nếu muốn cố định).
- Tag/Chương là bảng có FK thay vì JSON: lọc/ràng buộc được bằng SQL, đúng đặc tả 2.3.

## 5. Implementation Plan (Phase 1, mỗi task = một lát theo dây chuyền planner → coder → tester → reviewer)

| Task | Nội dung | Phụ thuộc | Repo/file dự kiến | Nghiệm thu |
|---|---|---|---|---|
| T1 | Migration Chapters, Tags, QuestionTags, `Questions.ChapterId/Explanation`, seed 3 mức độ | Q1, Q2, Q16 | BE `Project.Domain/Entities`, `Project.Infrastructure/Persistence`, `Migrations` | Áp lên bản sao + diễn tập cutover PASS; câu cũ đọc/làm bài được |
| T2 | API chương/tag + mở rộng DTO/lọc câu hỏi + audit tạo/sửa câu. Mang theo từ review T1: (a) quyết định có khóa sửa/xóa 3 mức độ hệ thống Id 1–3 qua `QuestionLevelController` không; (b) service luôn gán `Status` cho Chapter/Tag (DB không có default); (c) sinh `Tags.NormalizedName` ở service (trim, chữ thường, bỏ dấu) trước khi lưu để unique có tác dụng | T1 | BE `Controllers`, `QuestionService`, `QuestionRepository`, contract `01-http.md` | Smoke API: CRUD, lọc kết hợp, 403 khi thiếu quyền, tag trùng sau chuẩn hóa → 409 |
| T3 | FE ngân hàng câu hỏi: chương/tag/lời giải + quản lý chương | T2 | FE `QuestionBankPage`, `CatalogPage` | Lint/build, smoke P1/P4 không vỡ, kiểm bằng mắt |
| T4 | Snapshot v2 (lời giải) + review Exam trả `explanation` | T1 | BE `AttemptSnapshot`, `AttemptSnapshotFactory`, `StudentAttemptController` | Snapshot v1 vẫn review được; unit test builder |
| T5 | `PracticeSessions` + chọn câu + API tạo/làm/lưu/finish/review | T1, Q4, Q5, Q18 | BE mới `PracticeController`, service, migration | Smoke API: thiếu câu 409, revision, finish idempotent, không lộ đáp án, chỉ chủ phiên |
| T6 | Chế độ "kiểm tra từng câu" (perQuestion) | T5 | BE | Câu chưa check không trả đáp án |
| T7 | FE Luyện tập (tạo, làm, lịch sử, review) | T5, T6 | FE trang Học viên mới | Smoke trình duyệt mới + kiểm bằng mắt |
| T8 | `ReviewFlags` API + FE thay sessionStorage | T4, T5 | BE + FE `StudentAttemptPage` | Đánh dấu giữ qua reload/thiết bị, chỉ chủ bài |
| T9 | Mock Exam (`Kind=mock`, deadline, worker) | T5, Q6, Q7 | BE + FE | Hết giờ tự chốt, không lộ đáp án |
| Sau | Phase 2 (loại câu, media), Phase 3 (phân tích) | Q10–Q14 | — | Lập kế hoạch riêng |

## 6. Test Plan
- **Unit** (cần tạo test project BE — hiện chưa có, `06-quality/02-delivery-operations.md`): chọn câu ngẫu nhiên không lặp/thiếu câu; snapshot v1/v2; `AttemptScoring` cho Practice; chuẩn hóa tên tag; validator chương cùng môn.
- **API smoke** (PowerShell, mẫu `tests/TaskP*`): mỗi task một script tự tạo/dọn fixture theo tag, chạy trên bản sao QA; thêm vào `tests/RunUatSuite.ps1`.
- **FE**: `node --test` cho hàm thuần (bộ lọc, trạng thái câu Practice); smoke trình duyệt mới cho Practice; lint/build.
- **Hồi quy**: toàn bộ `RunUatSuite.ps1` (12 smoke hiện có) sau mỗi task; `CutoverRehearsal.ps1` sau mỗi migration.

## 7. Risk & Migration Plan
| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Đổi schema câu hỏi làm hỏng đề/lượt cũ | Thấp (DB 0 câu hỏi) | Chỉ thêm bảng/cột nullable; snapshot giữ nội dung cũ; diễn tập cutover |
| Lộ đáp án/lời giải qua API Practice/Mock | Cao | DTO làm bài không có trường đáp án/lời giải; test API so sánh JSON không chứa `isCorrect`/`explanation` trước thời điểm cho phép |
| Practice lẫn vào báo cáo Exam | Trung bình | Bảng riêng; báo cáo hiện đọc `ExamAttempts` |
| Chọn ngẫu nhiên lệch/thiếu câu | Trung bình | 409 rõ số câu có; unit test |
| Ghi đè nháp đồng thời | Trung bình | Dùng lại `Revision` như Exam |
| Seed 3 mức độ đụng mức độ người dùng đã tạo | Thấp | Migration chỉ chèn khi bảng trống hoặc tên chưa có; báo cáo nếu đã có dữ liệu khác |
| Thêm test project/thư viện | Thấp | Hỏi trước khi thêm gói (xUnit) |
| Rollback | — | Restore backup trước migration (runbook `06-quality/03-cutover-runbook.md`); `Down` chỉ dùng trên DB chưa có dữ liệu mới |
