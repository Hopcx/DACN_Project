# Task12 — kiểm kê nguồn ProjectDACN và đối soát Testify, 2026-10-19

## Bổ sung: nghiệm thu fixture **giả** F20–F22 trên bản sao, 2026-10-19

Fixture do người dùng cung cấp ở `VibeCoding/DACN_F20_F22_LegacyFixture.sql` là dữ liệu giả để kiểm kỹ thuật; **không** thay thế nguồn Testify thật. Đã backup `ProjectDACN` với `COPY_ONLY`/CHECKSUM, `RESTORE VERIFYONLY`, restore riêng thành `ProjectDACN_LegacyFixture_Test` mà không ghi DB gốc. Trước seed: `DB_NAME()` đúng, lịch sử chỉ `20260628110050_2806_updaetmơi11`, marker 0 hàng. Chạy nguyên script `@Mode='VERIFY'` rồi bản sao script đổi đúng dòng mode sang `SEED` với `sqlcmd -f 65001`; source có 2 user giả, 1 exam/schedule/detail/DoingExam, 4 Submission, 3 AnswerSubmission; FK/orphan sai = 0. Hai điểm biên SQL float đọc `1.0149999999999999` và `1.0049999999999999`; điểm tổng khoảng `10.02`, `IsPassed=true` 3, `Status=true/false/null` = 2/1/1. Không suy timezone của `datetime2`.

Mapping và rollback được viết **trước khi ghi đích** trong [bảng fixture](15-f20-f22-fixture-mapping.md). User nguồn map sang hai Student QA riêng ở bản sao đích; không copy password hash fixture. Parent business ID chỉ giữ provenance, không chèn Exam/Schedule/Attempt. `Submissions` → `LegacySubmissionRecords` 4/4, `AnswerSubmissions` → `LegacyAnswerSelections` 3/3; SHA-256 theo các trường nguồn quyết định, unique `(batch,table,legacyId)`. Nhóm không ánh xạ được (user, parent FK, exam–schedule, answer FK, count/checksum/score/status lệch) bị từ chối và transaction rollback. Script ở `tests/Task12LegacyFixtureImport.sql` có `DRYRUN`, `APPLY`, `ROLLBACK` theo batch. Điểm/đạt/Status/time nguyên bản, không snapshot, không chấm lại.

Đích `ProjectDACN_Task04Task09_Test` trước ghi có đúng 7 migration đến Task11. Đã review SQL Up/Down và áp **chỉ** migration `20261019090000_Task12LegacyProjection` trên đích thử; sau áp có 8 migration. EF `HasPendingModelChanges=False`. Dry-run trong transaction trả 2 map/4 submission/3 answer/tổng score 10.02 rồi rollback, batch 0. Apply cùng batch được chạy hai lần: trước/sau vẫn 4/3/2, checksum và score/status không đổi; batch `E81013F9-D2CB-4288-AA10-05EA55518DD9`. SQL trực tiếp và admin API đều có tổng 4, Status 2/1/1, tổng điểm lưu khoảng 10.02.

API–SQL qua HTTPS trên bản sao: 2 Student QA đăng nhập, mỗi người list 2 bài; trang 1 size 1 trả 1/2, detail của người khác 404, review chủ sở hữu 409 `LEGACY_SNAPSHOT_UNAVAILABLE`, review người khác 404, detail `timeZoneStatus=unknown` và không có `IsCorrect`; Student gọi admin report 403. Sau khi kiểm checksum/rerun, đổi tạm `AllowViewResult=false` cho một projection: API list/detail trả `result=null`, response không có `totalMark`/`isPassed`, sau đó rollback cả batch. Admin report size 2 trả tổng 4, ba nhóm Status 2/1/1, ba filter cũng trả 2/1/1, score sum ~10.02; báo cáo DACN mới vẫn `scope=dacnAttemptsOnly`. Build BE, FE lint/build đạt. FE đã nối hai phần legacy riêng trong lịch sử và report nhưng **chưa chạy browser** với fixture; đây là giới hạn F20/F22, không gọi là parity lịch sử `VERIFIED`. Sau khi mở rộng so sánh checksum sang toàn bộ field projection, dry-run SQL lại đạt 4/3/2 rồi rollback.

Sau thử đã `ROLLBACK` đúng batch (projection 0), dọn 3 tài khoản QA ở đích, chạy source script mode `CLEAN` và xóa script mode tạm. Postflight: source clone 1 migration, 0 fixture Submission/User; target clone 8 migration, 0 ImportBatch/LegacySubmission/LegacyAnswerSelection; `ProjectDACN` gốc 1 migration, 0 Submission. Số hàng chuyển còn lại = 0. Migration Task12 chỉ tồn tại/đã áp trên bản sao, **chưa áp DB gốc**. Chưa kiểm dữ liệu Testify thật, browser legacy và chính sách gộp báo cáo; F20–F22 vẫn `IN_PROGRESS`.

## Nguồn được xác nhận và phạm vi

Người dùng đã xác nhận dùng DB **`ProjectDACN` làm nguồn**. Mọi truy vấn lên DB này chỉ đọc; đích thử là `ProjectDACN_Task04Task09_Test`. Kết nối dùng Windows Authentication, xác nhận `DB_NAME()` trước truy vấn. `Testify` cục bộ từng được kiểm tra trước khi người dùng xác nhận nguồn: DB đó có 4 Users nhưng **0 Exam, 0 ExamSchedule, 0 DoingExam, 0 Submission**; không dùng làm nguồn chuyển dữ liệu. Export code Testify chỉ dùng để xác định ý nghĩa field, không dùng credential. Không có truy vấn ghi nào tới `ProjectDACN` hoặc `Testify`.

**Kết luận dữ liệu:** `ProjectDACN` là DACN schema ban đầu, có 1 User/1 Subject và **không có lượt thi lịch sử**. Nó không cung cấp bản ghi Testify cần chuyển. Bản sao có nhiều hàng QA phát sinh sau khi restore; các hàng đó không chứng minh đã chuyển dữ liệu Testify. Vì vậy số hàng lịch sử nguồn = **0**, số hàng ETL = **0**, không có điểm hoặc trạng thái đạt/trượt thực tế để so sánh.

## Preflight chỉ đọc và chênh lệch schema

| Kiểm tra | `ProjectDACN` nguồn | Bản sao DACN sau dọn fixture |
|---|---:|---:|
| Migration | 1: `20260628110050_2806_updaetmơi11` | 7: đến `20261019090000_Task11AttemptVoidAudit` |
| Bảng dbo / FK | 26 / 30, tất cả FK enabled/trusted | Có thêm bảng attempt mới và cột audit |
| Users / Subjects | 1 / 1 | 76 / 1 |
| Levels / Permissions / QuestionTypes | 4 / 4 / 3 | 4 / 4 / 3 |
| Classes / ClassUsers / ClassExamSchedule | 0 / 0 / 0 | 42 / 32 / 26 |
| Questions / Answers / ExamDetailQuestions | 0 / 0 / 0 | 0 / 0 / 0 |
| Exams / ExamDetails / ExamSchedules / Rooms | 0 / 0 / 0 / 0 | 14 / 4 / 74 / 28 |
| DoingExams / Submissions / AnswerSubmissions | 0 / 0 / 0 | 12 / 4 / 0 |
| ExamAttempts / ExamAttemptSubmissions | không tồn tại | 0 / 0 |

User nguồn khớp **1/1 theo GUID và email** với bản sao; cùng User này có `EmailVerifiedAt=NULL` sau migration Task04 trên bản sao, nên không backfill hoặc mở login. Level, Permission, QuestionType và Subject khớp ID và Name toàn bộ (4/4, 4/4, 3/3, 1/1). User→Level orphan = 0; 30 FK nguồn đều enabled/trusted. Vì các bảng thi nguồn đều 0, orphan/duplicate ID ở chuỗi user–exam–schedule–attempt–submission, reverse Exam↔Schedule mismatch, trạng thái hủy và điểm sát `1.015` đều **0 hàng có thể kiểm**. Không coi số 0 là bằng chứng quy tắc ETL đã được nghiệm thu. `Users.LastLogin` có 0 giá trị; source ExamSchedule/DoingExam/Submission có 0 timestamp để xác minh timezone.

Schema nguồn có `Exams.ExamScheduleId` nullable và `ExamSchedules.ExamId` nhưng chưa có FK đích Task09; bản sao đã chuyển sang FK duy nhất `ExamSchedules.ExamId` và thêm `IsTimeUtc`. Nguồn `Submissions.TimeTaken` là SQL `time`, `TotalMark` là `float`, `Status` là `bit?`; bản sao giữ bảng legacy này riêng với `ExamAttempts`/`ExamAttemptSubmissions` mới dùng snapshot/điểm `decimal(18,2)` và audit hủy. Không thể đưa hàng legacy vào bảng attempt mới mà không có snapshot gốc/provenance.

## Mapping có provenance trước khi có dữ liệu Testify thật

Mọi hàng nhập sau này cần khóa `(sourceSystem, sourceDatabase, sourceMigration, table, legacyId, batchId, targetId)`, checksum các field quyết định và unique constraint theo nguồn/ID. Không dùng ID trùng ngẫu nhiên làm bằng chứng cùng bản ghi.

| Nhóm Testify → DACN | Mapping/kiểm tra bắt buộc | Phân loại với nguồn được chọn |
|---|---|---|
| User, Level, Permission, UserPermission → bảng tương ứng | GUID hoặc map GUID, email/username/phone, role, hash, verified status; token phiên không chuyển | 1 User và lookup DACN nguồn đã hiện diện trong bản sao; **0 hàng cần ETL**. Dữ liệu Testify user thật chưa có. |
| Subject, Class, ClassUser → bảng tương ứng | SubjectId, ClassId, UserId, membership/status, capacity và cặp trùng | Subject đã khớp 1/1; Class/ClassUser nguồn 0. |
| Question, Answer, QuestionType/Level → bảng tương ứng | ID câu/đáp án, loại, đáp án đúng, trạng thái, mã đề tham chiếu | Question/Answer nguồn 0; không dựng snapshot giả từ ngân hàng hiện tại. |
| Exam, ExamDetail, ExamDetailQuestion → bảng tương ứng | ExamId, variant, tập câu/điểm, NumberOfRepeat, PassMark và ScoreMethod | 0 hàng nguồn; không áp quy tắc chấm DACN mới cho điểm Testify cũ. |
| ExamSchedule, Room, ClassExamSchedule → bảng tương ứng | `ExamSchedule.ExamId` đích, liên kết ngược cũ, Room/Class FK và timezone provenance | 0 hàng nguồn; timestamp không offset tương lai giữ `unknown`, không gắn UTC. |
| DoingExam, Submission, AnswerSubmission → **nhóm lịch sử riêng**, không thành `ExamAttempts` mới | User/schedule/examDetail/answer qua mapping, ID nguồn/batch, score/status/time nguyên bản | 0 hàng nguồn; chưa có gì để hiển thị hoặc cộng báo cáo. |

Theo code Testify, `SubmissionReposiroty.CheckNumberOfSubmit` đếm mọi Submission theo user/lịch, không lọc Status. `SetStatusSubmit.razor` dùng `true` = chấp nhận, `false` = không công nhận, `null` = cần kiểm tra. Trạng thái `false` **không tương đương** audit hủy mới có actor/lý do. Nếu sau này có dữ liệu Testify, giữ `legacyStatus` riêng để rà thủ công. `NumberOfRepeat` DACN mới tiếp tục đếm lượt đã start thành công kể cả khi kết quả bị hủy.

Điểm Testify phải giữ `TotalMark` và `IsPassed` đã lưu. Ví dụ kiểm thuật toán `Math.Round(1.015d,2)=1.01` so với DACN decimal ToEven `1.02` là sai khác số học dự kiến, **không phải một hàng thực trong nguồn được chọn**; nguồn hiện có 0 Submission và 0 hàng điểm sát biên. Khi có nguồn lịch sử, xuất danh sách legacy ID/điểm gốc/điểm tham chiếu và trạng thái gốc; không chấm lại hay sửa điểm. `SubmitTime`, `StartTime`, `EndTime` cũ không có offset phải giữ timezone `unknown` cho đến khi xác minh.

## Hiển thị và chống đếm đôi

Chỉ khi có dữ liệu lịch sử với mapping được chứng minh, tạo projection `source=legacySubmission` và khóa `(source, legacySubmissionId)` tách khỏi `source=dacnAttempt`. Student API phải kiểm owner từ UserId đã map ở từng truy vấn; legacy không có snapshot bất biến thì không mở route review đáp án đúng. Timestamp `unknown` không tự chuyển giờ Việt Nam. Admin báo cáo scope legacy riêng cho đến khi xác nhận status/mẫu số; không union ngầm với summary DACN mới. Bốn `Submissions` đang có trong bản sao không tồn tại trong `ProjectDACN` nguồn và không có provenance Testify, vì vậy **không cộng vào lịch sử hay báo cáo Testify**.

## Dry-run, rollback và blocker

Trước xác nhận nguồn, đã dry-run **một lookup QuestionLevel từ DB `Testify` cục bộ** trên bản sao trong transaction: kiểm DB_NAME/migration, insert 1, so count và toàn bộ cột bằng `EXCEPT`, rồi `ROLLBACK`; count sau rollback = 0. Đây chỉ là kiểm kỹ thuật phụ, **không phải ETL từ nguồn `ProjectDACN` đã chọn**. Sau khi người dùng xác nhận nguồn, không có hàng mới an toàn để insert từ `ProjectDACN`: 1 User/1 Subject và lookup đều đã hiện diện; tất cả nhóm thi nguồn 0. Số hàng chuyển còn tồn tại sau dry-run = **0**. Không tạo migration hoặc ETL dữ liệu.

Khi có DB/backup chứa lượt Testify thật, cần backup bản sao đích, preflight source/target và export mapping/count/FK/timezone/score/status theo batch trước ghi. Dry-run trong transaction rồi rollback nếu count/checksum sai. Sau commit chỉ rollback theo batch nếu chưa có phụ thuộc; nếu có, restore backup đã kiểm. Hiện thiếu **dữ liệu lượt Testify thực tế** để nghiệm thu F20–F22 và chưa có timezone gốc; giữ các feature `IN_PROGRESS`.
