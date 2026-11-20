# HTTP contract: hiện tại và quy tắc phát triển

## G7 health — 2026-11-16

`GET /web/health/live` (tiến trình chạy; 200 khi API sống) và `GET /web/health/ready` (200 khi kết nối được SQL Server, 503 khi không) — không cần đăng nhập, body chỉ `Healthy`/`Unhealthy`, không trả chi tiết lỗi hay chuỗi kết nối.

## Task10 lịch học viên — 2026-10-18

`GET /web/student/schedules` thêm `latestAttemptId` (Guid hoặc null) và `latestAttemptState` (`inProgress`, `submitted`, `timedOut` hoặc null) cho lượt mới gần nhất của chính học viên trong lịch. Nếu deadline lượt đang mở đã qua, state hiển thị là `timedOut`; GET attempt/submit sẽ ghi kết quả timeout vào SQL. FE dùng ID này để tiếp tục hoặc xem trạng thái sau thi; thao tác start vẫn do API kiểm quyền, giờ và giới hạn lượt.

## Bổ sung lịch học viên — 2026-10-11

`GET /web/student/schedules` giữ mảng data và policy Student, thêm `serverNow` UTC có Z và `scheduleState` vào mỗi phần tử. State = unknown nếu IsTimeUtc=false; với lịch UTC: upcoming khi serverNow < startTime, open khi startTime <= serverNow < endTime, ended khi serverNow >= endTime. Một response dùng chung một mốc giờ server. Đây là trạng thái thời gian của lịch, chưa phải quyền bắt đầu hoặc trạng thái attempt. Lọc theo membership đã duyệt/lớp hoạt động giữ nguyên. FE lấy nhãn từ API, refresh mỗi 30 giây/khi focus hoặc Tải lại.

SQL Server/API đã kiểm duyệt membership đồng thời: hai lớp/lịch giao nhau trả 200/409, chạm biên trả 200/200; tranh chỗ cuối cùng lớp trả 200/409. Membership bị từ chối không đổi, sĩ số không vượt capacity. Chưa triển khai route attempt hay migration Task10.

## Task 09 — lịch thi tại checkout `dev/Hop` (2026-10-07)

Cập nhật nghiệm thu 2026-10-08 trên bản sao `ProjectDACN_Task04Task09_Test`: migration `20261008144059_Task09ScheduleUtcProvenance` thêm `ExamSchedules.IsTimeUtc bit NOT NULL DEFAULT 0`. Hàng có trước migration giữ nguyên `datetime2` và trả `timeZoneStatus="unknown"`, thời gian không có offset; PUT/xóa mềm lịch và POST/PUT/DELETE gán lớp vào lịch này trả 409 cho đến khi xác minh dữ liệu gốc. Lịch tạo mới đặt `IsTimeUtc=1`; GET danh sách/chi tiết và GET lịch học viên trả `timeZoneStatus="utc"` với `startTime`/`endTime` có `Z` sau khi đọc lại từ SQL. Lịch hoạt động chưa rõ múi giờ được xem là xung đột tiềm tàng nếu cùng phòng hoặc có học viên đã duyệt chung qua các lớp; không suy đoán giờ của hàng cũ.

`GET /web/exam-schedules` và `/{id}` nay có `hasAttempts` từ DoingExam hoặc Submission để FE khóa thao tác. `GET /web/exam-schedules/exam-options` trả `{id,name,subjectId,status}` của bài thi chưa xóa; `/room-options` trả `{id,name,capacity}` của phòng có status khác false; `/class-options` giữ `{id,name,classCode}`. Cả ba yêu cầu permission 4. FE dùng `POST/PUT/DELETE /web/exam-schedules` và `POST/DELETE /web/class-exam-schedules`; request giờ UTC `Z`, response lỗi 400/403/409 theo envelope `ApiResponse`. SQL/API E2E đã kiểm tạo/sửa/xóa/gán/hủy lịch không có bài làm, từ chối đổi giờ/bài thi/phòng/gán/hủy/xóa mềm cho DoingExam và Submission, dữ liệu không đổi sau 409. FE browser test qua Vite proxy đã kiểm giờ Việt Nam sau tải lại, lỗi 409 trong modal và khóa thao tác khi `hasAttempts=true`.

Quyết định bổ sung: `POST/PUT /web/exam-schedules` chỉ nhận `startTime` và `endTime` ISO UTC có `Z`; reject 400 nếu DateTimeKind không phải UTC. Lịch status=1 bị từ chối 409 nếu giao khoảng `[start,end)` trong cùng RoomId hoặc có học viên membership=1 ở các lớp gán của lịch khác giao giờ. Gán lớp qua `/web/class-exam-schedules` và duyệt học viên cũng kiểm overlap; các luồng ghi này lấy cùng `sp_getapplock` transaction-scope trên SQL Server để serialize request đồng thời. Lịch có DoingExam hoặc Submission từ chối đổi thời gian, exam, phòng, gán lớp; xóa mềm lịch này cũng bị từ chối. SQL Server E2E trên bản sao `ProjectDACN_Task04Task09_Test` xác nhận hai request tạo lịch cùng phòng trả 201/409, hai request gán lớp có học viên trùng giờ trả 201/409, lịch sau bắt đầu đúng giờ kết thúc lịch trước trả 201. Dữ liệu datetime2 cũ chưa rõ timezone; response không có offset được FE hiển thị như chưa rõ múi giờ. Migration `20261007145624_Task09ExamScheduleDirection` đã áp trên bản sao sau Task04; DB gốc `ProjectDACN` vẫn ở migration đầu.

`GET /web/student/schedules` yêu cầu policy `Student` (level_id=4), lấy userId từ claim; trả `ApiResponse<List<{id,title,startTime,endTime,timeZoneStatus,status,roomId,roomName,subjectName}>>`. Chỉ trả lịch hoạt động (`status=1`, theo `Testify.ExamScheduleRepository.GetSchedulesActive`) có ít nhất một lớp hoạt động với membership đã duyệt (`ClassUser.Status=1`) của chính user. Quyền xem lịch dựa vào gán lớp; `ExamSchedule.ExamId` nay có FK sang bài thi.

`GET /web/exam-schedules/class-options` yêu cầu `ScheduleManagement` (permission 4), trả lớp hoạt động `{id,name,classCode}` cho màn quản lý. `POST/PUT /web/exam-schedules` giữ DTO hiện tại, thêm lỗi 400 nếu `startTime >= endTime`, thiếu thời điểm, `examId` không tồn tại/đã ngừng hoạt động, hoặc `roomId`/`subjectId` được gửi mà không tồn tại. Quyền CRUD `ClassExamSchedule` vẫn là permission 4. Hai màn FE `/admin/schedules` và `/student/schedules` hiện chỉ đọc API thật. Task09 có migration đổi chiều FK như trên.

## Task 08 — cấu hình bài thi và mã đề tại `dev/Hop`

Các route dưới đây dùng `ExamManagement` (permission 1), envelope `ApiResponse`. `GET/POST /web/exams`, `GET/PUT/DELETE /web/exams/{id}` giữ tên JSON `maximmumMark`. Request/response nay gồm `numberOfRepeat`, `allowViewResult`, `scoreMethodId`; request cũ bỏ hai field đầu nhận mặc định lần lượt 1 và true. Tạo bài thi là trạng thái nháp 2; công khai trạng thái 1 chỉ khi có ít nhất một mã đề công khai đủ số câu và tổng điểm. Số câu, số lượt, duration, điểm tối đa/đạt phải hợp lệ; scoreMethodId có thể null, giá trị khác null phải tham chiếu bản ghi sẵn có. Không có thuật toán chấm mới.

`GET /web/exams/subjects` trả môn đang hoạt động cho permission 1. `GET /web/exams/{id}/question-options` trả `{id,content,questionLevelId,questionLevelName}` của câu hoạt động cùng môn, không có đáp án. `GET /web/exams/{id}/variants` và `GET /web/exams/{id}/variants/{variantId}` trả mã đề cùng `questions: [{id,questionId,point}]`.

`POST /web/exams/{id}/variants` và `PUT /web/exams/{id}/variants/{variantId}` nhận `{code,status,questionIds,randomSelections:[{questionLevelId,count}]}`; `questionLevelId` null nghĩa là mọi mức độ. Trạng thái 2 là nháp, 1 là công khai. Server chọn ngẫu nhiên trong câu hoạt động cùng môn, trừ các câu đã chọn trong request; số câu cuối phải đúng cấu hình, không trùng và điểm mỗi câu là `maximmumMark / numberOfQuestions`. Thiếu câu phù hợp trả 409; dữ liệu sai trả 400. Ghi mã đề và câu hỏi trong transaction. Mã đề đã công khai hoặc có lượt thi không được sửa. `POST/PUT/DELETE /web/exam-details` và `/web/exam-detail-questions` trả 405 để tránh ghi rời vượt validation; GET cũ còn đọc được. Không thêm schema/migration.

## Task 07 — ngân hàng câu hỏi tại `dev/Hop`

`GET /web/questions` (policy `QuestionManagement`, permission 2) nhận `textSearch`, `subjectId`, `questionTypeId`, `questionLevelId`; ba ID lọc phải dương. Response `data` là mảng admin `QuestionResponseDto`, có `answers: [{id,content,isCorrect}]` và `isUsedInExam`. `GET /web/questions/{id}` cũng là admin projection. Không dùng hai response này cho student attempt.

`GET /web/questions/subjects` trả `[{id,name}]` cho người có permission 2, vì `/web/subjects` yêu cầu permission 3. Lookup loại/mức độ vẫn là `/web/question-types`, `/web/question-levels`.

`POST /web/questions` và `PUT /web/questions/{id}` nhận `{content,status,subjectId,questionTypeId,questionLevelId,documentPath,answers:[{content,isCorrect}]}`. `questionLevelId` và `documentPath` có thể null. Loại 1 cần hai đáp án Đúng/Sai và một đáp án đúng; loại 2 cần đúng một đáp án đúng; loại 3 cần ít nhất một đáp án đúng. Mọi loại cần 2–20 đáp án khác nội dung. Chỉ loại 1–3 được nhận. Ghi câu hỏi và toàn bộ đáp án trong transaction serializable; update thay tập đáp án khi câu hỏi chưa được dùng trong đề/bài nộp. `PUT`/`DELETE` trả 409 nếu câu hỏi đã dùng. `DELETE` ẩn bằng status 255. Các route ghi độc lập `POST/PUT/DELETE /web/answers` trả 405; `GET /web/answers` còn dành cho admin.

Không có migration/schema mới. Student attempt vẫn phải dùng projection riêng không có `isCorrect`; Task 07 chưa tạo attempt API. Chưa kiểm SQL rollback thực tế vì chưa có SQL Server fixture.

PUT thay toàn bộ tập đáp án, vì vậy ID đáp án mới sẽ khác ID cũ; client quản trị phải tải lại response sau lưu.

## FACT — contract đang có

Các controller nghiệp vụ dùng `/web/*`; WeatherForecast dùng route theo controller. SwaggerDoc version v1 không làm path thành /api/v1. Xem [catalogue](04-current-endpoints.md) để lấy action path thật.

`ApiResponse<T>` có Success, Data, Message. Với JSON web camelCase: `{ "success": true, "data": ..., "message": null }`. Axios interceptor hiện trả nguyên response, nên envelope là response.data, payload là response.data.data. Không destructure payload ở sai tầng.

Room list trả ApiResponse<PagedResult<RoomResponseDto>>; PagedResult có items, totalCount, page, pageSize (không có totalPages). Query mặc định Page=1/PageSize=10. Các list khác thường trả array trong data, không được giả định đều đã phân trang.

HTTP create dùng 201 với Created("", ...); delete thường 200 envelope; lỗi controller 400/404 envelope. Middleware map ValidationException/BadRequestException=400, NotFound=404, unexpected=500, trả ApiResponse.Fail. MVC model-validation tự động có thể trả ValidationProblemDetails; 401/403 từ middleware có thể không có envelope. Chưa chạy để pin toàn bộ response runtime.

## REQUIRED — compatibility trước khi chuẩn hóa

Giữ prefix/envelope hiện có khi port FE. Viết error adapter nhận được envelope, validation problem hoặc body rỗng. Không nhận định mọi lỗi là ProblemDetails hoặc có code/traceId khi current DTO chưa chứa chúng.

Bổ sung code/fieldErrors/traceId và chuẩn hóa HTTP theo một change có OpenAPI/contract tests + FE adapter đi cùng. Không đổi tất cả endpoint sang /api/v1 chỉ để khớp v0.1. Nếu chọn versioning sau này, ghi transition rõ.

## HTTP semantics mục tiêu

## Task 05 — thay đổi validation tại `dev/Hop`

`GET /web/users/get-all-users` nhận thêm query tùy chọn `search` (tên, username hoặc email), trim tối đa 100 ký tự; response vẫn là mảng `UserResponseDto` trong envelope, chưa phân trang.

## Task 06 — lớp và thành viên tại `dev/Hop`

`GET /web/classes/options` (AdminManagement) trả `teachers[{id,fullName}]`, `subjects[{id,name}]` đang hoạt động. `GET /web/class-users/by-class/{classId}` (AdminManagement) trả thành viên lớp. `POST /web/class-users` chỉ nhận `status=1` để admin thêm học viên; `PUT /web/class-users/{id}` chỉ duyệt bản ghi đang chờ với cùng classId/userId và `status=1`; `DELETE` xóa thành viên. Các ghi thành viên khóa hàng Class trong giao dịch; trùng cặp hoặc đầy sĩ số trả 409.

`GET /web/student/classes` (level_id=4) chỉ trả lớp của claim user hiện tại, gồm trạng thái thành viên 1=đã duyệt, 2=chờ duyệt. `POST /web/student/classes/join` nhận `{classCode}` và tạo yêu cầu status=2; `DELETE /web/student/classes/{classId}` rời lớp của chính mình. Không nhận userId từ FE cho student flow. `POST/PUT /web/classes` kiểm tên/mã/sức chứa, giáo viên level 3 đang hoạt động, môn học đang hoạt động; mã trùng và sĩ số giảm dưới số đã duyệt trả 409. Không thêm migration. Gán lịch qua ClassExamSchedule giữ nguyên endpoint cũ và chờ Task 09 để xác định Exam–Schedule.

Không có route hoặc schema mới. `GET /web/rooms` trả 400 nếu `page < 1`, `pageSize` ngoài 1..100, capacity âm, khoảng capacity đảo chiều hoặc offset vượt `int.MaxValue`; danh sách sắp theo `Id` tăng dần trước phân trang. `POST/PUT /web/rooms` yêu cầu tên, địa chỉ và sức chứa dương; `DELETE /web/rooms/{id}` trả 409 nếu phòng có lịch thi hoặc bị FK chặn. `POST/PUT /web/subjects` yêu cầu tên. `POST /web/users/create-user` trả 409 nếu tên đăng nhập, email hoặc số điện thoại đã tồn tại và cấp token xác minh email sau khi tạo. `POST/PUT /web/user-permissions` trả 400 nếu user/permission không tồn tại, 409 nếu cặp đã được gán (kiểm trước ở API, chưa có unique DB constraint). `DELETE /web/levels/{id}` chặn ID 1..4; `DELETE /web/permissions/{id}` chặn ID 1..4; PUT cùng ID chặn status khác 1. `DELETE /web/users/delete-user-{id}` chặn tự xóa và xóa admin hoạt động cuối cùng. Các trường hợp chặn trả `ApiResponse.Fail` với HTTP 409, ID user sai trả 400.

200 đọc/update, 201 tạo có Location phù hợp, 204 cho thao tác không body nếu đã đổi contract; 400 invalid, 401 unauthenticated, 403 forbidden, 404 missing, 409 state/concurrency/idempotency conflict, 429 rate limit, 500 unexpected. Không bọc mọi lỗi bằng status 200.

List mới phải validate page/pageSize và sort allowlist; hiện chưa áp dụng đồng nhất. Payload không chứa entity navigation hoặc secret.

## PROPOSED — engine thi

Start/submit cần Idempotency-Key, identity+operation+key scope, request hash. Cùng key/cùng request trả cùng kết quả; payload khác trả conflict. Unique Submission.AttemptId vẫn bảo vệ sau TTL. Save draft dùng revision hoặc rowversion mapping rõ. Chưa có các cơ chế này trong snapshot hiện tại.

OpenAPI là contract build artifact khi thiết lập pipeline. Hiện chỉ thấy Swagger startup, chưa thấy artifact client generation. Không gọi lệnh generate chưa được khai báo trong repo.

## Task 04 — contract thêm tại checkout `dev/Hop`

Các route mới giữ `/web` và `ApiResponse`. `GET/PUT /web/profile` yêu cầu Bearer; PUT chỉ nhận `fullName`, `address`, lấy UserId từ claim và trả `UserResponseDto` không chứa hash. `POST /web/profile/change-password` nhận `oldPassword`, `newPassword`; kiểm BCrypt mật khẩu cũ, mật khẩu mới tối thiểu 8 ký tự gồm hoa/thường/số, thu hồi toàn bộ refresh token của user sau khi đổi. Sai mật khẩu cũ trả 400.

`POST /web/auth/logout` yêu cầu Bearer, thu hồi toàn bộ refresh token của user hiện tại và trả 200. JWT đã phát hành vẫn hợp lệ đến khi hết hạn vì hiện không có token version/denylist; FE xóa phiên trong memory. Refresh JSON hiện tại được giữ; việc dùng lại token đã rotate/revoke trả 401, conditional update ngăn hai request cùng rotate một token.

`GET/PUT /web/users/{id:guid}` yêu cầu `AdminManagement` (claim `level_id=1`). GET trả `UserResponseDto`; PUT chỉ nhận `fullName`, `userName`, `email`, `phoneNumber`, `address`, `avatarUrl`. `levelId`, `status`, `passwordHash`, `lastLogin` không được cập nhật qua route này. Duplicate được kiểm trước và trả 409; unique index DB vẫn là chốt cuối khi có race. Public registration và cookie refresh chưa được thêm.

**Cập nhật Task 04 tiếp theo:** đoạn mô tả JSON refresh ở trên chỉ là trạng thái trước thay đổi. Contract hiện hành của branch được ghi trong [05-auth-lifecycle.md](05-auth-lifecycle.md): refresh bằng cookie HttpOnly, access JSON, CSRF và email verification. Migration `20261003130000_Task04AuthLifecycle` chưa apply trên DB thực.

## To-be T2 — chương, tag, metadata câu hỏi tại `dev/Hop`

Không thêm migration (schema từ T1 `20261117021231_ToBeT1QuestionMetadata`). Mọi route giữ `/web` và `ApiResponse`.

**Chương** `web/chapters`: `GET ?subjectId=&includeHidden=` cần quyền câu hỏi (2) hoặc môn học (3); `POST`, `PUT /{id}`, `DELETE /{id}` cần quyền môn học (3). Body `{subjectId, name, sortOrder, status?}`; tên trim, gộp khoảng trắng, 1–200 ký tự; `sortOrder >= 0`; `status` 0/1; môn phải đang hoạt động. Trùng tên trong cùng môn → 409. Đổi môn của chương đã có câu hỏi → 409. `DELETE` là ẩn (Status 255): câu đang gắn giữ liên kết, không gắn mới được. Response `{id, subjectId, name, sortOrder, status, questionCount}`.

**Tag** `web/tags`: `GET ?textSearch=&includeHidden=` và `POST` cần quyền câu hỏi (2); `PUT /{id}` (đổi tên, status 0/1; áp dụng cả tag đã ẩn, gửi status 1 để hiện lại) và `DELETE /{id}` (ẩn) chỉ Admin. Tên 1–50 ký tự; trùng khi tên chuẩn hóa (trim, gộp khoảng trắng, chữ thường, bỏ dấu, đ→d; giữ gạch nối) trùng tag có sẵn kể cả tag đã ẩn → 409 kèm "id X". Không có gộp tag. Response `{id, name, status, questionCount}`.

**Câu hỏi**: `POST/PUT /web/questions` nhận thêm `chapterId?`, `explanation?` (≤ 4000, trim, rỗng → null), `tagIds?` (ID dương, tối đa 10 sau khi bỏ trùng). Chương phải tồn tại và cùng môn; tag gắn mới phải đang hoạt động (Status 1), tag đã gắn sẵn với câu đang sửa được giữ dù đã ẩn/tắt; chương phải chưa bị ẩn (Status ≠ 255); sai → 400. **PUT là thay toàn bộ**: không gửi `chapterId`/`explanation`/`tagIds` nghĩa là xóa; FE (T3) phải luôn gửi đủ. Response thêm `chapterId`, `explanation`, `tagIds` (tăng dần). `GET /web/questions` nhận thêm `chapterId` và `tagIds` lặp (`tagIds=1&tagIds=2`, khớp bất kỳ tag nào), kết hợp AND với các bộ lọc cũ; ID ≤ 0 → 400.

**Mức độ hệ thống**: `PUT/DELETE /web/question-levels/{1|2|3}` → 409.

**Audit** (best-effort, ghi sau khi thao tác thành công bằng SaveChanges riêng; lỗi chỉ log): `question.create/update/hide/restore`, `chapter.create/update/hide`, `tag.create/update/hide`. Mô tả chỉ có ID, môn, chương, số tag, trạng thái; không chứa nội dung câu hỏi hay lời giải.

## To-be T4 — snapshot v2 có lời giải tại `dev/Hop`

Không migration. Lượt thi bắt đầu sau khi triển khai lưu `SnapshotJson` `Version = 2`: mỗi câu có thêm `Explanation` chụp từ `Questions.Explanation` lúc bắt đầu (trim, rỗng → null). Lượt cũ `Version = 1` giữ nguyên, không bổ sung lời giải từ ngân hàng câu hỏi.

`GET /web/attempts/{id}` (làm bài) **không** đổi: câu không có `explanation`, đáp án không có `isCorrect`.

`GET /web/attempts/{id}/review` giữ nguyên điều kiện mở (chủ bài, đã chốt, `AllowViewResult`, sau giờ kết thúc lịch, không bị hủy); response thêm `snapshotVersion` (1 hoặc 2) và `questions[].explanation` (string hoặc null).

## To-be T5 — Luyện tập tại `dev/Hop`

Migration `ToBeT5PracticeSessions` thêm bảng `PracticeSessions` (không đổi bảng cũ). Tách hoàn toàn khỏi `ExamAttempts`, lịch thi, worker và báo cáo Exam.

Route `/web/student/practice`, policy `Student`. Chỉ môn đang hoạt động thuộc lớp hoạt động mà học viên là thành viên đã duyệt (Q18). Phiên của người khác → 404 `PRACTICE_NOT_FOUND`. Mọi thao tác ghi của một học viên tuần tự hóa bằng app lock.

- `GET options` → `{minQuestions: 1, maxQuestions: 50, maxOpenSessions: 3, subjects[{id, name, questionCount, chapters[{id, name, questionCount}], levels[...], tags[...]}]}`. Đếm chỉ câu dùng được (Status 1, loại 1–3, tập đáp án hợp lệ); chương chỉ Status 1; tag chỉ Status 1 và đang gắn câu.
- `POST` `{subjectId, chapterIds?, questionLevelIds?, tagIds?, questionCount, revealMode}` → 201 trạng thái phiên. `questionCount` 1–50; mỗi danh sách lọc ≤ 20 ID dương (chương phải thuộc môn và Status 1, mức độ đang dùng, tag Status 1 — sai → 400), khớp bất kỳ trong danh sách, các nhóm AND; `revealMode` `end` (mặc định) hoặc `perQuestion`. Lỗi: 400 tiêu chí; 403 `PRACTICE_SUBJECT_FORBIDDEN`; 409 `PRACTICE_TOO_MANY_OPEN` (đã có 3 phiên `inProgress`); 409 `PRACTICE_NOT_ENOUGH_QUESTIONS` với `data {available, requested}`. Câu chọn ngẫu nhiên không lặp; snapshot là `AttemptSnapshot` v2 (điểm mỗi câu 1, có lời giải, chỉ server đọc).
- Trạng thái phiên (POST, `GET {id}`, retry): `{id, subjectId, subjectName, kind, revealMode, state, revision, questionCount, correctCount, createdAtUtc, finishedAtUtc, criteria, questions[{id, questionTypeId, content, options[{id, content}]}], answers[{questionId, selectedAnswerIds}]}` — `questions` không có `isCorrect`, điểm hay `explanation`; riêng phiên perQuestion có thêm `checked[]` (đáp án + lời giải của các câu đã chốt, xem To-be T6).
- `GET mine?page=1&pageSize=10` (pageSize 1–50) → `{page, pageSize, totalCount, items[{id, subjectId, subjectName, kind, revealMode, state, questionCount, correctCount, createdAtUtc, finishedAtUtc}]}`, mới nhất trước.
- `PUT {id}/answers` `{revision, answers}` → `{id, revision, answers}`; thay toàn bộ nháp; 409 `PRACTICE_CLOSED` / `CONCURRENCY_CONFLICT`; 400 `INVALID_SELECTION`.
- `POST {id}/finish` `{revision}` → `{id, state, revision, questionCount, correctCount, finishedAtUtc}`; chấm tất cả-hoặc-không (Q3), `correctCount` = số câu đúng. Gọi lại khi đã finish trả cùng kết quả (không kiểm revision).
- `GET {id}/review` (chỉ sau finish, nếu chưa → 409 `PRACTICE_NOT_FINISHED`) → `questions[{id, questionTypeId, content, selectedAnswerIds, isCorrect, options[{id, content, isCorrect}], explanation}]` + tổng.
- `POST {id}/retry` (T6: chỉ phiên đã finish, phiên đang làm → 409 `PRACTICE_NOT_FINISHED`) → 201 phiên mới cùng câu gốc (lấy lại từ ngân hàng hiện tại, bỏ câu không còn dùng được, xáo thứ tự), cùng `revealMode`; `criteria.retryOf` = id gốc. Không còn câu → 409 `PRACTICE_NOT_ENOUGH_QUESTIONS`.
- `DELETE {id}` → bỏ phiên `inProgress` (xóa hẳn); phiên đã finish → 409 `PRACTICE_FINISHED`; T6: phiên đã chốt ≥ 1 câu → 409 `PRACTICE_HAS_CHECKED`.
- `revealMode = perQuestion`: xem mục To-be T6.

## To-be T6 — Luyện tập "kiểm tra từng câu" tại `dev/Hop`

Migration `ToBeT6PracticeChecked` thêm cột `PracticeSessions.CheckedJson` nvarchar(max) NOT NULL default `'[]'` (Guid câu đã chốt).

- `POST /web/student/practice/{id}/questions/{questionId}/check` `{revision, selectedAnswerIds}` — `questionId` là Guid câu trong phiên. Chỉ phiên `revealMode = perQuestion` (khác → 409 `PRACTICE_CHECK_NOT_ALLOWED`). Lưu lựa chọn câu đó vào nháp và chốt trong cùng giao dịch, tăng `revision`. Trả `{result: {questionId, selectedAnswerIds, isCorrect, correctAnswerIds, explanation}, revision}`. Câu đã chốt → trả lại kết quả cũ (không kiểm revision, cả khi phiên đã finish). Lỗi: 404 `PRACTICE_NOT_FOUND` / `PRACTICE_QUESTION_NOT_FOUND`; 409 `PRACTICE_CLOSED` (đã finish, câu chưa chốt), `CONCURRENCY_CONFLICT`; 400 `INVALID_SELECTION` (trống, đáp án không thuộc câu, loại 1–2 chọn > 1).
- Trạng thái phiên (POST, `GET {id}`, retry) thêm `checked[]` cùng cấu trúc `result` — chỉ các câu đã chốt; câu chưa chốt vẫn không có đáp án/lời giải.
- `PUT {id}/answers`: câu đã chốt bị bỏ qua trong payload → giữ lựa chọn cũ; gửi lựa chọn khác (kể cả `[]`) → 409 `PRACTICE_QUESTION_LOCKED`. Payload sai cấu trúc (đáp án không thuộc câu, trùng câu, quá số lượng) bị kiểm trước → 400 `INVALID_SELECTION`.
- `finish`, `review` không đổi (chấm theo nháp đã gồm lựa chọn của câu đã chốt).

## To-be T8 — đánh dấu câu (ReviewFlags) tại `dev/Hop`

Migration `ToBeT8ReviewFlags` thêm bảng `ReviewFlags` (`UserId`, `SourceKind` 'exam'|'practice', `SourceId`, `SnapshotQuestionId`, `CreatedAtUtc`; PK 4 cột; FK Users). Không đổi API làm bài/review hiện có.

Route `/web/review-flags`, policy `Student`. Nguồn: `exam` = `ExamAttempts.Id` (chủ lượt + thành viên đã duyệt của lớp được gán lịch, như `GET /web/attempts/{id}`), `practice` = `PracticeSessions.Id` của chính học viên. Không phải chủ / không tồn tại → 404 `REVIEW_FLAG_SOURCE_NOT_FOUND`. `sourceKind` khác 'exam'/'practice' (không phân biệt hoa/thường) hoặc `sourceId` rỗng → 400. Được đánh dấu mọi lúc chủ mở được nguồn (đang làm, đã nộp/hết giờ/bị hủy, xem lại); không ảnh hưởng điểm.

- `GET ?sourceKind=&sourceId=` → `{sourceKind, sourceId, questionIds[]}` (Guid câu trong snapshot, theo thứ tự câu).
- `PUT` `{sourceKind, sourceId, questionId}` → `{..., flagged: true}`; câu không thuộc snapshot → 400 `REVIEW_FLAG_QUESTION_INVALID`; đã đánh dấu → vẫn 200.
- `DELETE ?sourceKind=&sourceId=&questionId=` → `{..., flagged: false}`; chưa đánh dấu → vẫn 200.
- `DELETE /web/student/practice/{id}` (bỏ phiên) xóa luôn đánh dấu của phiên trong cùng giao dịch.

## To-be T9 — Thi thử (Mock Exam) tại `dev/Hop`

Không migration (dùng `PracticeSessions.Kind = 'mock'` và `DeadlineUtc` có sẵn). Không tính vào điểm lớp/xếp hạng (Q7); chưa có so sánh/xếp hạng (Q8).

- `POST /web/student/practice` nhận thêm `kind` ('practice' mặc định | 'mock') và `durationMinutes` (mock: bắt buộc, 5–180; practice: bỏ qua). Mock chỉ nhận `revealMode` 'end' (gửi 'perQuestion' → 400). `DeadlineUtc` = lúc tạo + thời lượng. Giới hạn đang làm: 3 phiên practice (409 `PRACTICE_TOO_MANY_OPEN`), 1 phiên mock (409 `MOCK_ALREADY_OPEN`), tính riêng.
- `GET options` thêm `maxOpenMocks`, `minDurationMinutes`, `maxDurationMinutes`.
- Trạng thái phiên thêm `deadlineUtc` (null với practice), `serverNow`, `durationMinutes`; mọi thời điểm trả về có hậu tố `Z`.
- Hết giờ: request tới trước `DeadlineUtc + 5 giây` vẫn được nhận (ân hạn). Từ đó phiên được chốt bằng **nháp đã lưu**, `FinishedAtUtc = DeadlineUtc` — lười ở mọi request của phiên (GET, lưu, nộp, review, retry, bỏ phiên, tạo phiên mới) và bởi worker `ExpiredPracticeFinalizerService` (30 giây/lần). `PUT answers` sau hạn → 409 `PRACTICE_EXPIRED`; `finish` sau hạn → trả kết quả đã chốt; `DELETE` sau hạn → 409 `PRACTICE_FINISHED` (phiên được giữ làm lịch sử). Nộp trong ân hạn ghi `FinishedAtUtc` = hạn nộp.
- `POST {id}/questions/{qid}/check` trên mock → 409 `PRACTICE_CHECK_NOT_ALLOWED`. `POST {id}/retry` từ mock đã nộp → mock mới cùng thời lượng.
- `GET mine`: `items[].kind`, `deadlineUtc`; mock quá hạn chưa kịp chốt hiện `state = "expired"`.

## Dọn nợ sau To-be T9 tại `dev/Hop`

- Migration `ToBeDebtPracticeSweepIndex`: index lọc `IX_PracticeSessions_OpenDeadline` trên `PracticeSessions(DeadlineUtc)` `WHERE [State] = 'inProgress' AND [DeadlineUtc] IS NOT NULL` (worker quét Thi thử quá hạn). Lệnh SQL ghi `PracticeSessions` ngoài EF cần `SET QUOTED_IDENTIFIER ON` (sqlcmd `-I`).
- `GET /web/student/practice/mine` `items[]` thêm `durationSeconds` (phiên đã nộp: `FinishedAtUtc − CreatedAtUtc`, giây; đang làm: null) và `checkedCount` (số câu đã kiểm tra ở chế độ Từng câu).
