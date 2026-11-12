# As-is additions v1 — 22/10/2026

BE `dev/Hop` 04c5d28, FE `dev/Hop` 348eb4d trước thay đổi local. `ApiResponse` vẫn dùng `success`, `data`, `message`; JWT Bearer và `/web` giữ nguyên.

## A01 — Teacher classes

Policy `Teacher` yêu cầu `level_id=3` hoặc Admin. Mọi route dưới `/web/teacher/classes` dùng user ID từ `NameIdentifier` và chỉ truy cập `Classes.TeacherId` trùng claim. Lớp khác trả 404; anonymous 401; Student 403; Admin được qua policy `Teacher` (Admin 200) và thao tác được trên mọi lớp (theo `AuthorizationExtensions` và `TeacherClassController`). Admin CRUD `/web/classes` và `/web/class-users` giữ nguyên; Student join bằng code giữ nguyên.

| Method | Route | Body/response |
| --- | --- | --- |
| GET | `/web/teacher/classes` | Các lớp của chính Teacher |
| GET | `/web/teacher/classes/{classId}/members` | Thành viên, tên và email trong lớp mình |
| GET | `/web/teacher/classes/{classId}/students/lookup?email=...` | Tra đúng một email Student đã xác minh, không liệt kê toàn hệ thống |
| POST | `/web/teacher/classes/{classId}/members` | `{email}`; chỉ thêm Student đã có tài khoản và xác minh email, trạng thái 1 |
| POST | `/web/teacher/classes/{classId}/members/{membershipId}/approve` | Duyệt trạng thái 2 sang 1 |
| DELETE | `/web/teacher/classes/{classId}/members/{membershipId}` | Xóa membership |

Mọi ghi dùng `ClassMembershipStore` với khóa hàng lớp và kiểm owner **trong giao dịch**. 409 cho trùng, lớp đầy, lịch học viên giao nhau hoặc trạng thái không hợp lệ. Không tạo lời mời cho email chưa có tài khoản. Lookup không tìm thấy trả 404; email sai 400.

## A02 — Question Excel v1

`GET /web/questions/import/template` trả `dacn-questions-v1.xlsx`. `POST /web/questions/import` nhận multipart field `file`; policy `QuestionManagement`. File `.xlsx` tối đa 2 MiB, một sheet tên `Questions`, tối đa 500 dòng dữ liệu. Sáu cột theo đúng thứ tự:

| Cột | Tên | Nội dung |
| --- | --- | --- |
| A | SubjectId | ID môn đang hoạt động |
| B | QuestionLevelId | ID mức độ đang hoạt động |
| C | QuestionTypeId | 1 Đúng/Sai, 2 chọn một, 3 chọn nhiều |
| D | Content | 1–4000 ký tự |
| E | Answers | 2–20 đáp án, phân cách `;`, mỗi đáp án 1–2000 ký tự; không dùng `;` bên trong đáp án |
| F | CorrectIndexes | Vị trí đáp án đúng bắt đầu từ 1, phân cách `,` |

Ví dụ: `1 | 1 | 2 | 2+2=? | 3;4;5 | 2`. Loại 1 cần hai đáp án `Đúng;Sai` và đúng một vị trí; loại 2 đúng một; loại 3 ít nhất một. Không hỗ trợ FillAnswer=4. Không nhận công thức Excel. Kiểm toàn file và tham chiếu trước khi ghi; ghi câu hỏi cùng đáp án trong một transaction Serializable. Lỗi HTTP 400/409 trả `data: { imported: 0, errors: [{row,column,code,message}] }`; thành công `{imported,errors:[]}`. Dòng tiêu đề là 1; số dòng lỗi là số dòng Excel. Trùng nội dung trong cùng môn, trong file hoặc ngân hàng, bị từ chối.

## A03 — Visibility hidden

Theo quyết định người dùng 22/10/2026, chỉ `document.visibilitychange` sang `hidden` tạo sự kiện; `blur` không tính. Count chỉ ghi nhận, không tự phạt, trừ điểm, khóa hoặc nộp bài. Trình duyệt không chứng minh chắc chắn hành vi gian lận.

`GET /web/attempts/{id}/visibility-count` trả `{count}`. `POST /web/attempts/{id}/visibility-hidden` nhận `{eventId: UUID}`; trả `{count,duplicate}`. Cả hai chỉ cho Student sở hữu attempt và còn quyền qua membership; attempt khác 404. Ghi chỉ khi `inProgress` và trước deadline; muộn 409 `ATTEMPT_CLOSED`. Server cấp `RecordedAtUtc`; `eventId` chống retry, sự kiện `hidden` trong cùng một giây gộp count dù hai tab. Bảng mới `AttemptVisibilityEvents` có khóa `(AttemptId,EventId)`, `IsCounted` và FK attempt. FE giữ hàng đợi event chưa gửi trong sessionStorage để thử lại khi quay về hoặc reload; sự kiện có thể mất khi trình duyệt đóng trước khi gửi.

## A04 — Same-subject class overlap

Theo quyết định người dùng 22/10/2026, một **lớp** không có hai lịch **hoạt động** cùng môn giao giờ. Môn chuẩn là `Exam.SubjectId`; `ExamSchedule.SubjectId` nếu có phải trùng. Khoảng giao nhau là `[start,end)`, nên chạm mép hợp lệ. Khi gán lớp hoặc sửa/kích hoạt lịch đã gán, kiểm trong transaction dưới `ScheduleWriteGuard`; xung đột trả 409 `Lớp đã có lịch thi cùng môn giao nhau`. Lịch timezone unknown được coi là xung đột bảo thủ. Tạo lịch chưa gán không thể xung đột theo lớp; gán lớp về sau sẽ kiểm. Phòng và học viên giữ kiểm riêng.

## O07 — Teacher read-only schedules/results (22/10/2026)

Cùng policy `Teacher` và quy tắc owner (lớp khác trả 404, anonymous 401, Admin/Student 403).

| Method | Route | Response |
| --- | --- | --- |
| GET | `/web/teacher/classes/{classId}/schedules` | Lịch được gán cho lớp: `id,title,examName,subjectName,roomName,startTime,endTime,timeZoneStatus,status` |
| GET | `/web/teacher/classes/{classId}/results` | Tối đa 500 lượt của học viên đã duyệt (`ClassUsers.Status=1`) trong lịch của lớp: tên, email, bài thi, thời gian, `state`, `totalMark`, `isPassed` |

Lượt đã hủy trả `state=voided` và `totalMark/isPassed=null`. Không trả đáp án hay snapshot. Học viên chưa được duyệt hoặc đã rời lớp không xuất hiện.

## A05 — Question Excel export v1 (P4, 11/11/2026)

`GET /web/questions/export?subjectId=&questionTypeId=&questionLevelId=` (policy `QuestionManagement`). `subjectId` bắt buộc; hai ID còn lại tùy chọn; ID không dương hoặc thiếu `subjectId` trả 400 `INVALID_FILTER`; môn không tồn tại 404 `SUBJECT_NOT_FOUND`; không có câu hỏi 404 `NO_QUESTIONS`. Chỉ xuất câu có `Status != 255`; import lại luôn ghi `Status = 1`.

File là workbook v1 giống template của A02 (sheet `Questions`, 6 cột `SubjectId, QuestionLevelId, QuestionTypeId, Content, Answers, CorrectIndexes`). Mọi ô là `inlineStr`, không có công thức; đáp án nối bằng `;`, `CorrectIndexes` là vị trí 1-based tăng dần. Tên file `dacn-questions-v1-subject-{subjectId}.xlsx`.

Kiểm toàn bộ trước khi xuất; có câu không import lại được thì 409 với `data = { exported: 0, errors: [{questionId, code, message}] }` và không có file. Mã: `LEVEL_MISSING`, `TYPE_UNSUPPORTED`, `CONTENT_LENGTH`, `ANSWER_COUNT`, `ANSWER_SEPARATOR`, `ANSWER_LENGTH`, `ANSWER_WHITESPACE`, `INVALID_ANSWERS`, `INVALID_XML_CHAR`, `DUPLICATE_CONTENT`. Quá 500 câu hoặc file > 2 MiB: 409 `EXPORT_TOO_LARGE`. Xuất thành công ghi một dòng audit `question.export` (A07) trước khi trả file; ghi audit lỗi thì 500 và không trả file.

## A06 — Account Excel v1 (P4, 11/11/2026)

Policy `AdminManagement`, route `/web/users/excel`.

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/web/users/excel/template` | `dacn-accounts-v1.xlsx`, chỉ dòng tiêu đề |
| POST | `/web/users/excel/import` | multipart: `file` (.xlsx, 1 byte–2 MiB, ≤ 500 dòng dữ liệu) và `levelId` ∈ {3,4} áp cho cả file |
| GET | `/web/users/excel/export?levelId=` | `dacn-accounts.xlsx`; `levelId` tùy chọn 1..4, sai trả 400 `INVALID_FILTER` |

Sheet `Accounts`, 8 cột: `A FullName` (1–100) · `B DateOfBirth` (`yyyy-MM-dd` hoặc số ngày Excel nguyên; 1900-01-01..hôm nay) · `C Email` (hợp lệ, ≤ 450, lưu chữ thường) · `D PhoneNumber` (tùy chọn, 10–11 chữ số) · `E Address` (1–200) · `F Sex` (`1` nam, `0` nữ) · `G UserName` (6–100, `[a-zA-Z0-9_]`) · `H Password` (8–128, có hoa/thường/số, không Trim). Mã lỗi theo dòng: `FULL_NAME, DATE_OF_BIRTH, EMAIL, PHONE, ADDRESS, SEX, USER_NAME, PASSWORD, DUPLICATE_FILE, EMPTY_ROW` (400) và `DUPLICATE_DB, LEVEL_NOT_FOUND` (409); mã cấp tệp `LEVEL_ID, INVALID_XLSX, EMPTY`. Thông báo lỗi không bao giờ chứa mật khẩu.

Có bất kỳ lỗi hoặc dòng trùng (trong file hoặc với DB) thì từ chối cả file, không ghi dở dang, không upsert. Thành công: mật khẩu băm BCrypt, `EmailVerifiedAt = null`, mỗi user một `EmailVerificationToken` 24 giờ, mail xác minh gửi tuần tự sau commit (lỗi mail không rollback, đếm vào `emailFailed`; người dùng dùng `resend-verification`). Response `{ imported, emailFailed, errors: [] }` không chứa Id, hash hay mật khẩu. File import chứa mật khẩu dạng rõ do admin tự quản; hệ thống không sinh mật khẩu chung.

Export 10 cột: `FullName, DateOfBirth, Email, PhoneNumber, Address, Sex, UserName, LevelId, Status, EmailVerified` (`Sex` và `EmailVerified` xuất `1/0`), sắp theo `UserName, Id`, projection không đọc `PasswordHash`; không có token hay `LastLogin`. Không giới hạn số dòng export. Import và export ghi audit `account.import` / `account.export` (A07).

## A07 — Audit read API (P4, 11/11/2026)

Bảng `Logs` thêm 6 cột nullable (migration `P4AuditLogColumns`): `ActorUserId`, `CreatedAtUtc`, `Action`, `EntityType`, `EntityId`, `CorrelationId` (+ index `(CreatedAtUtc, Id)`, `ActorUserId`; không FK tới `Users` để audit còn sau khi xóa user). Hàng cũ giữ nguyên, không backfill. `AuditWriter` (scoped) lấy actor từ claim `NameIdentifier`, `CorrelationId` từ `HttpContext.TraceIdentifier`, thời gian UTC của server. Phạm vi ghi trong P4: `account.import`, `account.export`, `question.export`; chưa ghi cho sửa quyền, hủy bài, phân lịch. Description không chứa mật khẩu, token hay nội dung đáp án.

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/web/admin/audit/logs?page&pageSize&search&action&actorUserId&fromUtc&toUtc` | Phân trang `Logs`, sắp `CreatedAtUtc desc, Id desc`; item gồm `actorUserName` (null nếu user đã xóa) và `createdAt` UTC |
| GET | `/web/admin/audit/exam-activity?page&pageSize&userId&examId&examScheduleId&actionType&from&to` | Phân trang `ExamActivityLogs` kèm user, bài thi, lịch |

Policy `AdminManagement`. `page` 1..100000, `pageSize` 1..100, ID dương, `search/action/actionType` ≤ 100 ký tự, `fromUtc/toUtc` phải có `Z` hoặc offset (vd `+07:00`, được đổi sang UTC); chuỗi không có offset thì 400, `from > to` thì 400 `INVALID_FILTER`. Khoảng giờ là `[from, to)`. `ActionTime` của `ExamActivityLogs` không rõ múi giờ: trả và lọc nguyên giá trị, không gắn `Z` (O09). DACN chưa có writer phía server cho `ExamActivityLogs`.

Các route ghi `POST/PUT/DELETE /web/logs` và `/web/exam-activity-logs` trả 405 (`Nhật ký chỉ được ghi bởi hệ thống.`); GET cũ giữ nguyên. P4 không có job xóa log; giữ log 12 tháng (O10) chưa được thực thi.

## A08 — Teacher overview (P4, 11/11/2026)

`GET /web/teacher/overview?upcomingLimit=8` (policy `Teacher`; `upcomingLimit` 1..50, sai trả 400 `INVALID_FILTER`). Giảng viên thấy các lớp mình phụ trách; Admin 200 và thấy mọi lớp (khớp code, xem A01). Không có lớp vẫn 200, mọi số 0, mảng rỗng. Chỉ số: `classes {total, active}`; `members {approvedStudents (distinct UserId, Status=1), pendingRequests (Status=2)}`; `schedules {upcomingCount, openCount, unknownTimeZoneCount}` (lịch `Status=1` gán cho lớp, `IsTimeUtc` quyết định upcoming/open/unknown); `attempts` tính theo `ExamAttempts.Id` khác nhau thỏa điều kiện của `/results` (lịch gán cho lớp và học viên là thành viên đã duyệt của chính lớp đó) nên không nhân đôi khi học viên ở nhiều lớp: `totalAttempts, inProgressCount, finalizedCount, passedCount, failedCount, voidedCount, averageScore, passPercent` (lượt đã chốt = `submitted/timedOut` có `Submission` chưa hủy; làm tròn 2 chữ số, ToEven); `perClass` mỗi lớp (`perClassMeaning = attemptMayCountInEveryOwnedClassOfStudent`, tổng có thể lớn hơn `attempts`); `upcoming` tối đa `upcomingLimit` lịch UTC chưa kết thúc. Không trả đáp án, snapshot hay email học viên. Phạm vi `dacnAttemptsOnly`.
