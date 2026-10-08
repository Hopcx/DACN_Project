# Task 12 — kiểm kê dữ liệu trước đổi schema (2026-10-07)

## Bằng chứng tại checkout

BE `dev/Hop` tại `0496f8b813dd872b806ceef43efc6319b6d9385a`; FE `dev/Hop` tại `968d40cd2e460026d5585b97a8a9d482dee5cc62`. Đã kiểm source và DB local `ProjectDACN` bằng Windows Authentication; không dùng credential trong export Testify.

Migration khởi tạo `20260628110050_2806_updaetmơi11` tạo `ExamSchedules.ExamId` (int, không FK), `Exams.ExamScheduleId` (nullable FK sang ExamSchedules), `ClassExamSchedule` gắn Class–ExamSchedule, `Submissions.TimeTaken` SQL `time` và điểm SQL `float`. Migration auth chưa áp trên DB local. `ProjectDACNDbContext` có hai DbSet cùng kiểu `ClassExamSchedule` (`ClassExamSchedules`, `UserExamSchedules`); chỉ bảng `ClassExamSchedule` được map.

## Mapping cần đối soát trước ETL

| Dữ liệu Testify | Đích DACN | Khóa/FK và kiểm tra bắt buộc |
|---|---|---|
| User, Level, Permission, UserPermission | Users, Levels, Permissions, UserPermissions | Giữ bảng `legacyUserId → newUserId` (Guid), `legacyLevelId → newLevelId`; kiểm hash tương thích, email/phone trùng, role/permission đặc quyền; không tự kích hoạt login cho hash chưa xác minh. |
| Subject, Class, ClassUser | Subjects, Classes, ClassUsers | `legacySubjectId → newSubjectId`, `legacyClassId → newClassId`; kiểm Class.SubjectId, ClassUser.UserId, trạng thái lớp/thành viên và cặp trùng. |
| Question, Answer, QuestionType/Level | Questions, Answers và lookup | Ánh xạ ID câu/đáp án; kiểm loại 1–3, đáp án đúng và các tham chiếu mã đề. Không suy diễn loại FillAnswer=4 đã có engine. |
| Exam, ExamDetail, ExamDetailQuestion | Exams, ExamDetails, ExamDetailQuestions | Ánh xạ ID đề/mã đề/câu; kiểm số câu, điểm, duration (đơn vị phút trong DACN), trạng thái và lịch sử đã công khai. |
| ExamSchedule, Room, ClassExamSchedule | ExamSchedules, Rooms, ClassExamSchedule | Ánh xạ ID lịch/phòng/lớp; đối soát `ExamSchedule.ExamId` với `Exams.ExamScheduleId` và bản ghi mâu thuẫn trước khi chốt FK. Giữ nguyên giá trị DateTime cho đến khi biết timezone legacy. |
| DoingExam, Submission, AnswerSubmission | DoingExams, Submissions, AnswerSubmissions | Ánh xạ user/lịch/đề/câu/đáp án; so số lượt, trạng thái, điểm và thời lượng. Legacy TimeSpan có thể vượt 24 giờ, SQL `time`/TimeOnly không biểu diễn được; không cast âm thầm. Điểm DACN hiện `double`/`float`; chưa chốt đổi decimal hoặc rounding. |

Mỗi bảng cần đối soát số bản ghi nguồn/đích, số FK mồ côi, ID trùng và mẫu kết quả thi theo user/lịch. Nếu chuyển dữ liệu cũ, lưu mapping ID trong staging có kiểm soát, backup trước dry-run và kiểm checksum/count sau chạy. Không tạo snapshot lịch sử từ nội dung câu hỏi hiện tại.

## Gate cho Task 09 và rollback

Đã chạy [script preflight chỉ đọc](12-readonly-preflight.sql) trên DB `ProjectDACN` và scaffold migration Task09. Chưa áp migration hoặc ETL. Trước khi áp cần xử lý auth migration còn pending, backup DB có user hiện tại, chạy thử trên bản sao có fixture và đối soát hàng trước/sau. Rollback khi FK/backfill làm mất dữ liệu là restore backup hoặc migration bù đã thử; `Down` đơn thuần không khôi phục được liên kết bị ghi đè.

## Quyết định người dùng cho Task 09/12 (2026-10-07)

Nghiệm thu trên DB DACN thử nghiệm; Task 09 không chuyển Testify. Quan hệ đích Exam 1→n ExamSchedule, FK duy nhất `ExamSchedules.ExamId`. Chỉ tạo migration sau khi chạy preflight trên DB đích và phân loại orphan, liên kết mâu thuẫn, lịch có DoingExam/Submission. Không tự sửa hoặc xóa bản ghi mâu thuẫn. Lịch mới lưu UTC; DateTime cũ chưa rõ timezone phải giữ nguyên. Chính sách overlap: cùng phòng và học viên đã duyệt qua các lớp gán, khoảng nửa mở `[start,end)`. Lịch có DoingExam/Submission không đổi thời gian, ExamId, RoomId hoặc gán lớp.

Dấu phân biệt timestamp UTC mới với DateTime cũ đã được bổ sung trong migration `20261008144059_Task09ScheduleUtcProvenance` ở phần nghiệm thu bên dưới; SQL `datetime2` không lưu timezone/DateTimeKind. FE không được tự coi chuỗi không có offset là UTC.

### Preflight thực tế trên `ProjectDACN` local (2026-10-07)

Đã dùng Windows Authentication chạy `12-readonly-preflight.sql` chỉ đọc trên DB local `ProjectDACN` (SQL Server 2025 Developer). `__EFMigrationsHistory` có **một** migration: `20260628110050_2806_updaetmơi11` (EF 8.0.14). Migration auth `20261003130000_Task04AuthLifecycle` chưa apply. Schema live có FK `FK_Exams_ExamSchedules_ExamScheduleId`; không có FK từ `ExamSchedules.ExamId` sang `Exams`. `IX_Exams_ExamScheduleId` tồn tại.

| Bảng/kiểm tra | Số dòng/kết quả |
|---|---:|
| Exams, ExamSchedules, ClassExamSchedule, DoingExams, Submissions, ExamDetails | 0 mỗi bảng |
| Exams có ExamScheduleId, orphan Schedule.ExamId, liên kết hai chiều không khớp, reverse mismatch | 0 mỗi loại |
| Users, Subjects | 1 mỗi bảng |
| Classes, ClassUsers, Rooms, RefreshTokens | 0 mỗi bảng |

Do không có hàng Exam/Schedule, hiện **không có dữ liệu thực tế để suy ra cardinality**; chỉ schema hiện tại cho biết chiều FK là Schedule→n Exam. Có thể dùng DB này làm DB thử Task9 mà không phải backfill liên kết lịch, nhưng phải xử lý migration auth còn pending và ảnh hưởng đến user duy nhất trước khi `database update`.

Sau preflight đã scaffold `20261007145624_Task09ExamScheduleDirection` bằng `dotnet-ef 8.0.14`. Up bỏ FK/index/cột cũ ở `Exams`, thêm index và FK không cascade `ExamSchedules.ExamId → Exams.Id`. Up tự từ chối khi có liên kết cũ, orphan hoặc DoingExam/Submission phát sinh sau preflight. Down từ chối khi đã có lịch vì không thể phục hồi quan hệ 1→n vào cột cũ. Đã sinh/review SQL Up/Down; **chưa áp migration** và chưa chạy trên bản sao DB có fixture.

Đã diễn tập SQL Up trong giao dịch bao ngoài trên chính `ProjectDACN`: bên trong giao dịch FK mới có 1, FK cũ có 0; sau `ROLLBACK`, FK cũ có 1, FK mới có 0 và Task09 không có trong history. Đã diễn tập Up→Down trong giao dịch rồi rollback; sau Down FK cũ có 1, FK mới có 0. Kiểm lại `__EFMigrationsHistory` còn đúng 1 dòng, Task09 chưa áp.

Đã diễn tập thêm fixture sau SQL Up trong giao dịch bao ngoài: tạo một Exam và hai ExamSchedule cùng ExamId, truy vấn trả đúng 2 lịch; rollback xong Exams=0, ExamSchedules=0, history vẫn 1 dòng. Fixture này kiểm cardinality trên SQL Server, nhưng không kiểm quyền/API hoặc hai request ghi đồng thời.

### Xử lý từng nhóm preflight trước migration quan hệ

| Kết quả | Cách xử lý cần duyệt trên DB thử |
|---|---|
| `ExamSchedules.ExamId` không có Exam | Không tạo FK; xác định Exam đúng hoặc giữ lịch ngoài tập migrate. Không thay bằng một Exam bất kỳ. |
| `Exams.ExamScheduleId` trỏ lịch khác với `ExamSchedules.ExamId` | Đối soát từng cặp với đề/lượt thi/submission. Bản ghi không mâu thuẫn có thể dùng `ExamSchedules.ExamId` làm nguồn FK; bản ghi mâu thuẫn phải có mapping được xác nhận, không backfill tự động. |
| Lịch có DoingExam hoặc Submission | Giữ nguyên ID lịch và liên kết lịch sử; kiểm payload/lượt thi mẫu trước và sau migration. Không sửa lịch hoặc gán lớp trong lúc xử lý. |
| Timestamp không có nguồn timezone | Giữ nguyên giá trị và gắn trạng thái `Unknown` cho hàng cũ. Chỉ hàng ghi mới nhận `UTC`; FE chuyển giờ Việt Nam khi API có bằng chứng offset. |

Migration Task09 đã thêm FK `ExamSchedules.ExamId → Exams.Id`, index theo truy vấn và bỏ FK cũ `Exams.ExamScheduleId`; không chỉnh migration gốc. Trước khi áp cần backup DB và thử SQL generated, FK/index, fresh DB, DB copy có dữ liệu đại diện và hai request ghi lịch/gán lớp/duyệt thành viên đồng thời.

## Nghiệm thu trên bản sao `ProjectDACN_Task04Task09_Test` (2026-10-07)

Đã tạo backup `COPY_ONLY, COMPRESSION, CHECKSUM` của `ProjectDACN` tại đường dẫn SQL Server `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\ProjectDACN_Task04Task09_20261007_221313.bak`; `RESTORE VERIFYONLY WITH CHECKSUM` thành công (753 pages). Restore sang DB mới `ProjectDACN_Task04Task09_Test` với MDF/LDF riêng, không dùng `REPLACE`. Trước mỗi lệnh EF ghi, kiểm connection string đích và `SELECT DB_NAME()` trên bản sao. Các kết quả preflight và diễn tập trên DB gốc ở các mục trên là trạng thái **trước** khi tạo bản sao; DB gốc vẫn chưa áp Task04/Task09.

Trên bản sao, `__EFMigrationsHistory` ban đầu chỉ có `20260628110050_2806_updaetmơi11`. Đã áp `20261003130000_Task04AuthLifecycle` trước, sau đó `20261007145624_Task09ExamScheduleDirection`; mỗi lệnh `dotnet-ef database update` hoàn thành và history theo đúng thứ tự 3 dòng. Task04 tạo `EmailVerificationTokens` và `BlackListTokens`, thêm `Users.EmailVerifiedAt`; user gốc vẫn `NULL`. Task09 bỏ `Exams.ExamScheduleId`/FK cũ, tạo `IX_ExamSchedules_ExamId` không unique và FK `FK_ExamSchedules_Exams_ExamId`. Preflight chỉ đọc sau migration: trước fixture, Exams/Schedules/DoingExams/Submissions đều 0, orphan 0, user/subject mỗi bảng 1.

API chạy với connection string trỏ đích bản sao. Trên SQL Server, hai request `POST /web/exam-schedules` cùng phòng và khoảng giờ trả 201/409; một lịch bắt đầu đúng lúc lịch trước kết thúc trả 201. Fixture một Exam có hai ExamSchedule được kiểm bằng SQL. Hai request `POST /web/class-exam-schedules` cho hai lớp có cùng một học viên đã duyệt và hai lịch giao giờ trả 201/409. `GET /web/student/schedules` trả lịch được gán. Sau khi thêm một DoingExam, `PUT /web/exam-schedules/{id}` đổi giờ và `DELETE /web/class-exam-schedules/{id}` đều trả 409. Các fixture chỉ ghi trong bản sao.

Không có quyền truy cập hộp thư của user gốc; không backfill `EmailVerifiedAt`. Luồng Task04 được thử với Student QA riêng qua register → nhận mail trong development maildrop → verify → login → `/me` → refresh → logout; JWT sau logout trả 401. User QA không xuất hiện trong `ProjectDACN` gốc. Login của user gốc chưa được nghiệm thu. Script E2E là [`tests/Task04Task09CopySmoke.ps1`](../../../tests/Task04Task09CopySmoke.ps1). Nhánh Submission và timezone của dữ liệu lịch sử vẫn cần fixture/dữ liệu nguồn thích hợp; chưa ETL Testify hoặc áp migration trên DB gốc.

Đối soát cuối: DB thử có 8 Users (1 user gốc `EmailVerifiedAt=NULL`, 1 QA chưa verify do một lần test gặp rate limit 429), 1 Subject, 4 Exams, 16 ExamSchedules, 4 ClassExamSchedule, 1 DoingExam thử, 0 Submission, 2 JWT blacklist; orphan schedule = 0, cả 4 Exam đều có ít nhất 2 lịch. FK mới enabled và trusted. DB gốc vẫn có đúng 1 migration đầu, 1 User, 1 Subject, 0 Exam/Schedule và FK cũ. Các hàng QA là dữ liệu nghiệm thu trong bản sao, không phải dữ liệu Testify.

## Tiếp tục Task 09 trên bản sao (2026-10-08)

Sau khi kiểm history 3 migration và `SELECT DB_NAME()`, đã áp **chỉ trên** `ProjectDACN_Task04Task09_Test` migration `20261008144059_Task09ScheduleUtcProvenance`. SQL generated thêm `ExamSchedules.IsTimeUtc bit NOT NULL DEFAULT 0`; 16 hàng lịch đã có giữ nguyên timestamp và nhận `0`. Lịch mới đặt `1`; API đọc từ SQL `datetime2` rồi gắn `DateTimeKind.Utc` để JSON có `Z`, kèm `timeZoneStatus="utc"`. Hàng cũ trả `"unknown"` không có offset, FE không chuyển đổi; PUT/gán lớp vào lịch này trả 409. Lịch unknown cùng phòng hoặc có học viên chung được xem là xung đột tiềm tàng, vì không thể so sánh chính xác giờ. `Down` của migration từ chối nếu còn hàng `IsTimeUtc=1`, tránh mất provenance; khôi phục bản sao/backup là đường rollback khi đã có lịch UTC mới.

Đã chạy `tests/Task04Task09CopySmoke.ps1` trên API kết nối bản sao: option endpoints permission 4; student không có permission trả 403; cùng phòng và học viên trùng giờ khi ghi đồng thời trả 201/409; khoảng chạm biên trả 201; một Exam có hai Schedule. Tạo/sửa/gán/hủy/xóa mềm lịch **không** có bài làm đạt và dữ liệu sau reload khớp SQL. Fixture DoingExam và fixture Submission riêng đều làm PUT đổi giờ/bài thi/phòng, POST/DELETE gán lớp và DELETE lịch trả 409; so sánh hàng lịch/gán trước và sau không đổi. Hàng mới GET có `timeZoneStatus="utc"` và `Z`; hàng cũ có `"unknown"` và không có `Z`. Trên lịch cũ chưa rõ múi giờ, PUT/xóa mềm lịch và POST/PUT/DELETE gán lớp đều trả 409; kiểm hàng SQL không đổi sau từ chối.

Chrome headless đã chạy `DACN_FE/tests/scheduleBrowserSmoke.mjs` qua Vite HTTPS proxy nối API/SQL bản sao: Axios service login/đọc option/tạo/sửa/gán/hủy, lỗi quyền 403, trùng phòng 409, giờ Việt Nam sau reload, lỗi 409 hiện trong modal sửa và nút sửa/xóa/form gán lớp khóa sau DoingExam đều đạt. Thao tác trực tiếp form tạo và modal gán/hủy được đối chiếu với SQL. Bản sao vẫn giữ fixture QA và có thể tạo lại từ backup gốc rồi áp Task04 → Task09 → migration marker; không có dữ liệu Testify được chuyển, DB `ProjectDACN` gốc không thay đổi.
