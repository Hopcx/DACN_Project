# HTTP contract: hiện tại và quy tắc phát triển

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
