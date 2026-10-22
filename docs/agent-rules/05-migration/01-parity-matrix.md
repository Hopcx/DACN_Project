# Ma trận chức năng Testify → DACN

## Delta As-is A01–A06 — 22/10/2026

| Feature | Trạng thái | Bằng chứng và giới hạn |
| --- | --- | --- |
| F05 Role/Teacher | IN_PROGRESS | Thêm policy `Teacher` theo level 3; `/web/teacher/classes` lọc owner từ claim. API clone đạt anonymous 401, Student 403, Teacher lớp khác 404; chưa Admin/Examiner và browser. |
| F07 Lớp/thành viên | IN_PROGRESS | Teacher tra đúng email Student đã xác minh, thêm/duyệt/xóa qua `ClassMembershipStore` có owner check trong khóa lớp; FE `TeacherClassesPage`. API/SQL và Chrome FE lookup/add đạt, fixture sạch; chưa concurrency/capacity. |
| F12 Excel câu hỏi | IN_PROGRESS | Template `.xlsx` v1, parse/validate toàn file, transaction Serializable, FE chọn file và xem lỗi. File thật qua API/SQL và Chrome FE đạt, gồm Student 403, invalid 400, valid 200, duplicate 409, aggregate 1 câu/3 đáp án, fixture sạch; chưa ma trận lỗi đầy đủ. |
| F16 Lịch/gán lớp | IN_PROGRESS | Quyết định phạm vi cùng lớp; `Exam.SubjectId` chuẩn, guard kiểm trùng môn khi gán/sửa/kích hoạt, 409 riêng. API/SQL clone đạt cùng lớp giao giờ 409, khác lớp 201, chạm mép 201, update 409; chưa race/unknown/attempt/browser. |
| F17 Lịch học viên | IN_PROGRESS | Giữ kiểm room/student và `[start,end)`; FE giữ form khi 409. Chưa browser hồi quy. |
| F23 Rời tab | IN_PROGRESS | Chỉ visibility hidden, eventId/idempotency, server time/count, owner; bảng `AttemptVisibilityEvents` áp riêng clone, SQL rollback và API count/retry/closed đạt. Chrome FE với hidden giả lập/reload tăng count đúng. Không dùng count làm chế tài. Chưa chuyển tab thật/offline/two-tab/timeout. |

Contract chi tiết: [06-as-is-additions](../04-contracts/06-as-is-additions.md). Nghiệm thu và phần chưa kiểm: [16-as-is-acceptance](16-as-is-acceptance.md). F20–F22 giữ scope legacy riêng.

## F20–F22 fixture kỹ thuật — 2026-10-19

| Feature | Trạng thái | Bằng chứng và giới hạn |
|---|---|---|
| F20 Kết quả/lịch sử | IN_PROGRESS | Projection riêng 4 `Submission` giả từ bản sao source 1 migration sang bản sao target 8 migration; API owner 2+2, phân trang, cross-owner 404, review legacy 409/404 vì không có snapshot, `AllowViewResult=false` ẩn score/pass ở list/detail. Dry-run rollback và apply/rerun/rollback đạt. FE đã nối lịch sử legacy riêng, lint/build đạt; chưa kiểm browser hoặc dữ liệu Testify thật. |
| F21 Hủy kết quả | IN_PROGRESS | Fixture giữ `Status=true/false/null` = 2/1/1, không gọi `false` là void có audit; quy tắc hủy DACN mới không đổi. Chưa đối soát hủy lịch sử Testify thật. |
| F22 Thống kê/phổ điểm | IN_PROGRESS | Admin API `legacySubmissionsOnly` tách khỏi `dacnAttemptsOnly`; API/SQL khớp 4 bài, tổng float ~10.02, Status 2/1/1, ba filter trả 2/1/1, phân trang 2; Student bị 403. FE đã nối report legacy riêng, lint/build đạt; chưa kiểm browser, dữ liệu Testify thật hoặc quyết định gộp chỉ số. |

Chi tiết [mapping fixture](15-f20-f22-fixture-mapping.md), [Task12](14-task12-testify-reconciliation.md). Fixture đã CLEAN/rollback, source còn 1 migration/0 bài giả, target còn 8 migration/0 projection, `ProjectDACN` gốc còn 1 migration/0 Submission.

## Task11 race / Task12 nguồn được xác nhận — 2026-10-19

| Feature | Trạng thái | Bằng chứng và giới hạn hiện hành |
|---|---|---|
| F20 Kết quả/lịch sử | IN_PROGRESS | Browser/API/SQL bản sao đã kiểm owner, ẩn kết quả, review từ snapshot và trạng thái hủy. DB nguồn `ProjectDACN` do người dùng xác nhận có **0 Submission/DoingExam**, nên chưa có lượt Testify thật để đối soát/hiển thị. Bốn Submission ở bản sao không tồn tại trong nguồn này, không gắn nhãn Testify. |
| F21 Hủy kết quả | IN_PROGRESS | Hai admin gửi đồng thời cùng lượt: cùng lý do 200/200, khác lý do 200/409; SQL giữ một actor/lý do/UTC và điểm gốc, Student list ẩn kết quả. Hủy vẫn tính `NumberOfRepeat`; việc hoàn lượt sau này là use case admin riêng có audit. Chưa đối soát trạng thái `Submission.Status` trên dữ liệu Testify thật (nguồn 0 hàng). |
| F22 Thống kê/phổ điểm | IN_PROGRESS | Sau race fixture 7 lượt/6 submission/3 đã hủy: tổng 7, finalized 3, đạt 1, trung bình 4, tỷ lệ 33.33%, phổ điểm 3 mức; API và SQL khớp. Report chỉ gồm attempt DACN mới, không đếm đôi. DB nguồn 0 kết quả Testify, nên chưa nghiệm thu cách gộp/báo cáo lịch sử. |

Chi tiết số hàng, mapping và rollback ở [Task12](14-task12-testify-reconciliation.md); các mục dưới là bằng chứng theo thời điểm trước.

## Task11 xem lại bài / hủy kết quả — nghiệm thu bản sao 2026-10-19

| Feature | Trạng thái | Bằng chứng và giới hạn |
|---|---|---|
| F20 Kết quả/lịch sử | IN_PROGRESS | Chrome → Vite → API → SQL copy đạt history owner/paging, kết quả ẩn ở list/detail/URL/reload; review chỉ mở sau `EndTime` với `AllowViewResult=true`, response và UI dùng snapshot dù Question/Answer nguồn đã sửa. Trước giờ kết thúc, kết quả ẩn, người khác và admin không có quyền review; sau hủy list/detail chỉ thấy `voided`, không có điểm/đáp án. Chưa đối soát và hiển thị lượt Testify cũ, chưa kiểm tình huống thu hồi membership sau chốt qua browser. |
| F21 Hủy kết quả | IN_PROGRESS | AdminManagement hủy qua Chrome → API → SQL copy với lý do; audit ghi actor/UTC, điểm/đáp án gốc giữ nguyên. Hủy lặp cùng lý do giữ nguyên fingerprint, khác lý do 409; Student 403. Lượt hủy vẫn tính quota theo code/contract hiện tại; quy tắc hoàn lượt Testify chưa xác minh. Chưa nghiệm thu race hai admin hủy khác lý do hoặc rollback migration có dữ liệu audit. |
| F22 Thống kê/phổ điểm | IN_PROGRESS | Fixture 5 lượt/4 submission: trước hủy finalized 4, đạt 2, trung bình 5, tỷ lệ 50%; sau hủy một lượt 8 điểm finalized 3, đạt 1, trung bình 4, tỷ lệ 33.33%, voided 1, total vẫn 5. Chrome admin và SQL đối chiếu, filter exam/class/subject/state và paging API đạt; không đếm đôi hai lớp. Chỉ số chỉ gồm DACN mới; chưa đối soát/union `DoingExam`/`Submission` lịch sử. |

Các kết quả Task11/13 bên dưới là ảnh chụp trước triển khai review/void; dùng mục mới trên làm trạng thái hiện hành.

## Task11/Task13 — kết quả và báo cáo lượt DACN mới, 2026-10-18

| Feature | Trạng thái | Bằng chứng và giới hạn |
|---|---|---|
| F20 Kết quả/lịch sử | IN_PROGRESS | API `GET /web/attempts/mine` và FE lịch sử riêng đã thêm. SQL copy fixture 2 học viên/5 lượt: list chỉ 4 lượt của chính học viên, phân trang 2, detail người khác 404, kết quả ẩn null cả list/detail. Dữ liệu Testify cũ chưa đối soát; chưa có review đáp án hoặc chính sách release chi tiết. Kiểm Chrome bổ sung ghi ở báo cáo nghiệm thu. |
| F22 Thống kê/phổ điểm | IN_PROGRESS | API admin chỉ lấy `ExamAttempts` mới; SQL copy fixture 5 lượt, 4 đã chốt, 1 đang làm, 1 timeout, 1 ẩn, 2 đạt; điểm 2/4/6/8 cho trung bình 5 và tỷ lệ đạt 50%. Filter exam/class/subject, phân trang, permission 403 và options đạt qua API. Chrome → Vite → API → SQL copy hiển thị đúng 5 lượt và chỉ số admin. Chưa có đối soát legacy hoặc hủy attempt. |


## Task10 nghiệm thu bản sao — 2026-10-18

| Feature | Trạng thái | Bằng chứng mới / còn thiếu |
|---|---|---|
| F17 Lịch học viên | IN_PROGRESS | Chrome → Vite → API → SQL bản sao đã đi từ lịch vào bài, reload, nộp, xem kết quả được phép và mở lịch sử/URL trực tiếp với kết quả bị ẩn. Chưa đối soát dữ liệu lịch sử timezone unknown và toàn bộ trạng thái lịch có bài cũ. |
| F18 Lượt làm bài | IN_PROGRESS | API/SQL bản sao đạt start đồng thời, cap `NumberOfRepeat` là tổng lượt, membership, lịch sớm/hết/unknown, snapshot bất biến, lựa chọn ngoài snapshot, save revision đồng thời, owner, timeout và thu hồi membership. Chrome đạt lưu/khôi phục. Worker chốt 8 lượt không cần request và chốt 1 lượt sau API restart; lượt snapshot hỏng được log riêng. Đã kiểm đồng hồ chính xác một tick trước/đúng/sau deadline ở code cô lập và request gửi trước nhưng xử lý sau hạn trên SQL; chưa kiểm đầy đủ mã đề nhiều biến thể và dữ liệu legacy có lượt cũ. |
| F19 Nộp/chấm | IN_PROGRESS | API/SQL đạt nộp lặp một kết quả, các ca single/multi đúng, thiếu, thừa, bỏ trống, tổng 4.52, ngưỡng 1.004 trước làm tròn, ẩn kết quả và timeout tự động từ draft. Submit và sweep chạy đồng thời giữ một submission; kiểm lại sau sweep và restart không đổi kết quả. Chrome đạt kết quả và thời lượng khi AllowViewResult=true. Browser/API/SQL đạt ẩn kết quả sau nộp, lịch sử, URL trực tiếp và reload. Raw 1.005/1.015 → lưu 1.00/1.02, đạt theo raw; quyết định decimal ToEven đã chốt. Race submit gửi trước hạn nhưng chờ khóa qua hạn và worker chỉ tạo một submission. Chưa chứng minh hai HTTP request cùng tick, chưa đối soát lượt Testify cũ hoặc nối báo cáo/admin Submission cũ. |

Migrations `20261018053153_Task10ExamAttempts` và `20261018063239_Task10TimeoutAndDuration` chỉ áp trên `ProjectDACN_Task04Task09_Test`; DB gốc vẫn history 1 và không có bảng ExamAttempts. Các dòng trạng thái 2026-10-11 dưới đây là lịch sử trước triển khai Task10.

## Trạng thái hiện tại — 2026-10-11

Các mục theo ngày bên dưới và bảng baseline mô tả lịch sử; ưu tiên bằng chứng hiện tại ở mục này.

| Feature | Trạng thái | Bằng chứng và phần còn thiếu |
|---|---|---|
| F16 Lịch/gán lớp | IN_PROGRESS | SQL Server bản sao đã đạt thêm duyệt membership đồng thời: lịch giao nhau 200/409, chạm biên 200/200, tranh chỗ cuối 200/409; membership bị từ chối vẫn pending, sĩ số không vượt capacity. Fixture lớp/lịch đã dọn. Khoảng trống concurrency approval đã đóng; còn đối soát dữ liệu lịch sử unknown để nghiệm thu parity toàn bộ. |
| F17 Lịch học viên | IN_PROGRESS | API bổ sung serverNow/scheduleState; FE hiển thị trước/trong/sau giờ lịch và unknown. Chrome → Vite → API → SQL đạt lọc membership đã duyệt, 4 trạng thái, giả giờ máy khách và reload sau đổi fixture. Chưa có trạng thái attempt/nộp/kết quả. |
| F18 Lượt làm bài | IN_PROGRESS | Đã đọc Task10.md hiện có; thêm factory snapshot, DTO học viên an toàn và validate draft ở mức code thuần. Chưa có persistence/API Start/Resume/Save, revision concurrency, deadline hoặc trang làm bài. Cách tính/phạm vi lượt và deadline đang chờ chốt; chưa nghiệm thu luồng attempt trên SQL. |
| F19 Nộp/chấm điểm | OPEN | Chưa có finalize idempotent/giao dịch, chấm điểm hoặc kiểm release đáp án. Quy tắc multiple-choice/rounding/ngưỡng đạt còn OPEN; không dùng CRUD Submission nhận điểm từ client. |

Không tạo/áp migration trong lần này; chỉ DB bản sao được ghi fixture. Không đánh dấu VERIFIED từ các test trạng thái lịch. Báo cáo chi tiết tại BE: docs/agent-rules/05-migration/13-task09-task10-acceptance.md.

## Task 12 rồi Task 09 tại checkout `dev/Hop` (2026-10-07)

Quyết định tiếp theo: DB DACN thử nghiệm, chưa ETL Testify; Exam 1→n Schedule với `ExamSchedules.ExamId` là FK đích. API ghi lịch/gán lớp và duyệt membership nay lấy SQL Server transaction application lock, kiểm phòng/học viên theo `[start,end)`, từ chối đổi lịch/gán lớp sau DoingExam/Submission; chỉ chấp nhận UTC `Z` cho giờ ghi mới. FE đọc offset theo Asia/Ho_Chi_Minh và giữ nguyên chuỗi cũ chưa rõ zone. Preflight trên `ProjectDACN` cho thấy Exam/Schedule/attempt/submission rỗng và chỉ migration đầu đã apply. Đã sao lưu gốc và restore `ProjectDACN_Task04Task09_Test`; trên bản sao đã áp Task04 rồi Task09. SQL Server E2E trả 201/409 cho hai request cùng phòng và hai request gán lớp làm học viên trùng giờ; khoảng chạm biên trả 201. DB gốc chưa áp migration. F16/F17 vẫn IN_PROGRESS đến khi kiểm đủ các nhánh nghiệp vụ, đặc biệt lịch có lượt thi/submission.

F16/F17 **IN_PROGRESS** (cập nhật 2026-10-08). Migration `20261008144059_Task09ScheduleUtcProvenance` đã áp trên bản sao; 16 lịch có trước migration giữ `IsTimeUtc=0`, lịch tạo mới có `IsTimeUtc=1` và GET trả UTC `Z` sau SQL round-trip. API giờ trả `hasAttempts`, có exam/room options cho permission 4; FE có form tạo/sửa và gán/hủy lớp theo API thật. SQL Server E2E đạt cho lịch không có bài làm, DoingExam, Submission, chồng phòng/học viên ghi đồng thời và dữ liệu không đổi sau 409. Chrome headless qua Vite proxy đạt thao tác trực tiếp form tạo lịch và gán/hủy lớp với kiểm hàng SQL, form sửa hiển thị giờ Việt Nam, lỗi 409 trong modal và nút khóa sau DoingExam. F16 còn thiếu đối soát timezone/dữ liệu lịch sử Testify, thử duyệt membership đồng thời và triển khai DB gốc theo bước riêng trước khi đánh dấu parity VERIFIED; F17 còn thiếu trạng thái trước/trong/sau thi và lịch học viên đầy đủ. DB gốc chưa áp migration; không ETL Testify. Xem [contract](../04-contracts/01-http.md) và [readiness](12-data-readiness.md).

## Task 08 tại checkout `dev/Hop` (2026-10-04)

F14/F15 **IN_PROGRESS**. BE chuyển NumberOfRepeat/AllowViewResult/ScoreMethodId qua Exam DTO và lưu, giữ `MaximmumMark`; cấu hình được validate. Tạo/sửa mã đề cùng tập câu thủ công và ngẫu nhiên theo môn/mức độ trong transaction serializable; chặn trùng/thiếu câu và sửa mã đề đã công khai/có lượt thi. FE có màn danh sách, cấu hình và chi tiết mã đề qua API thật. Chưa có SQL fixture để xác minh transaction/concurrency, lịch sử đề sau publish hoặc chấm theo ScoreMethod; không đánh dấu VERIFIED. Không thêm migration.

Mức bằng chứng: **CODE** có logic source đã xem; **DECLARED** mới thấy khai báo/route/model; **PARTIAL** có phần stub/thiếu rõ; không mức nào là “đã test runtime”. BE/FE đã đối chiếu source tại SHA trong README. **CODE** không có nghĩa chạy được; blocker chung B01/B02 và authorization xem audit. FE hầu hết chưa có nghiệp vụ ngoài placeholder auth/dashboard. Những hàng CODE phải có acceptance trước đánh dấu migrated.

| ID | Feature / bằng chứng cũ | Mức | Module đích và acceptance | BE tại commit đối chiếu | FE tại commit đối chiếu |
|---|---|---|---|---|---|
| F01 | Login/check token: AccessController; UserRepository | CODE | Identity: POST login, token lifecycle và deny unauthorized; không giữ GET credentials | AuthController login/refresh/me + BCrypt/JWT có code; IADO cần sửa; lifecycle còn B07 | LoginPage placeholder navigate; chưa gọi auth API |
| F02 | Register: AccessController.RegisterUser chỉ trả bool; UserController.RegisterStudent riêng | PARTIAL | Làm rõ public registration hay admin tạo tài khoản; không gộp hai flow thành hoàn chỉnh | Có POST /web/users/create-user quản trị; không có auth/register công khai; cần khóa role/policy | RegisterPage + SelectRolePage placeholder; không tạo account |
| F03 | Đổi mật khẩu/profile: Access/ChangePassword; InformationAdmin/Student | DECLARED | Identity/Profile: xác thực mật khẩu cũ, ownership, không mass assignment role | Chưa có endpoint profile/change-password trong controller mới; repo method update không phải public use case | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F04 | CRUD user, student/staff/lecturer views: UserController, Candidate/LecturerController | CODE/PARTIAL | Users: phân trang/search/role, uniqueness; dialog staff/student có phần placeholder phải kiểm từng route | UserController mới có list/create/delete; thiếu update/detail theo API quản trị đầy đủ | Chưa có quản lý user; dashboard chỉ số giả lập |
| F05 | Level/Permission/UserPermission; PermissionManagement | CODE | Access: role+permission+resource policy; matrix Teacher/Admin cần chốt | Level/Permission/UserPermission CRUD + bốn policy; UserPermissionService thiếu DI; auth chưa bao phủ | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F06 | Môn học: SubjectController/Repository, SubjectMn | CODE | AcademicCatalog: CRUD/disable/search, bảo vệ lịch sử | SubjectController/Service/Repository CRUD có code, chưa parity mọi report/filter legacy | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F07 | Lớp và thành viên: ClassController, ClassUserController, AddNewClass | CODE | Classes: mã lớp, capacity, teacher, join/remove, membership chống race | Class/ClassUser CRUD; ClassUserService thiếu DI; chưa có join-by-code/capacity concurrency use case đầy đủ | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F08 | Phòng thi: RoomController/Repository | CODE | Rooms: CRUD và lịch theo phòng; không suy diễn có kiểm trùng đầy đủ | Room CRUD + paging + ScheduleManagement; IADO blocker, cần stable sort/validator | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F09 | Loại/mức độ câu hỏi: QuestionType/QuestionLevelController | CODE | QuestionBank: lookup, CRUD theo quyền, không xóa loại đang dùng | QuestionType/QuestionLevel CRUD; chưa có validation nghiệp vụ đầy đủ | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F10 | Câu hỏi/đáp án: Question/AnswerController; CreateQuestionDialog | CODE | QuestionBank: true-false/single/multiple, filter môn/level/type, attachment theo quyền | Question/Answer CRUD riêng; DTO answer có IsCorrect; chưa có aggregate transaction + student safe projection | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F11 | FillAnswer=4 chỉ trong listQuestionTypes; seed chỉ 1..3 | DECLARED | OPEN: xác minh thực sự cần điền đáp án; chưa đưa thành parity hoàn thành | Chưa thấy engine FillAnswer; vẫn OPEN | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F12 | Import/export câu hỏi/template: QuestionController | CODE | Import: schema file, validate atomic, lỗi từng dòng; export correct answers theo quyền | Chưa có endpoint Excel import/export/template và package EPPlus trong manifest mới | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F13 | Import/export account/template: UserController | CODE | Users: giữ field nghiệp vụ cần thiết, không export hash/mật khẩu | Chưa có endpoint Excel account import/export/template trong UserController mới | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F14 | Exam: số câu, số lần, điểm tối đa/đạt, duration, AllowViewResult | CODE | ExamDefinition: cross-field invariant; ScoreMethod chưa có đủ căn cứ cơ chế riêng | Exam CRUD nhưng DTO thiếu NumberOfRepeat/AllowViewResult/ScoreMethodId; xem B09 | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F15 | ExamDetail/mã đề; CreateDeThi/UpdateExamDetail | CODE | Variants: chọn câu thủ công/ngẫu nhiên theo độ khó, không trùng, đủ số/tổng điểm | ExamDetail/ExamDetailQuestion CRUD; cả hai service thiếu DI; chưa có use case random/publish snapshot | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F16 | Lịch thi, gán lớp: ExamSchedule/ClassExamSchedule, DistributeSchedule | CODE | Scheduling: khoảng thời gian và quyền, membership, overlap theo policy đã duyệt | ExamSchedule/ClassExamSchedule CRUD; service gán lớp thiếu DI; relation Exam–Schedule cần sửa B11 | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F17 | Danh sách/lịch lớp của Student: Calendar, ListExam, CLassOfStudent | CODE | Student dashboard: chỉ thấy lịch/lớp hợp lệ, trạng thái trước/trong/sau thi đúng | Chưa expose student-specific calendar/list theo membership tương đương nguồn cũ | Chưa có Student calendar/lớp/thi |
| F18 | Làm bài: ViewExamTest.LoadExam, lựa chọn đáp án, localStorage, timer | CODE | Attempts: backend start/snapshot, resume, save revision, deadline authority | Có DoingExam entity/DbSet; chưa có start/save/resume/deadline API | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F19 | Nộp/chấm: ViewExamTest.ScoreExam; Submission/AnswerSubmission | CODE | Domain grading + transaction finalize; ghi nhận sửa lỗi chấm multiple-choice | Submission/AnswerSubmission CRUD, chưa server grading/atomic finalize; nhận điểm từ DTO B05 | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F20 | Kết quả/lịch sử: ResultOfSubmission; SubmissionReposiroty | CODE | Results: summary/review theo policy, snapshot bất biến, không xem bài người khác | Submission get-all/get-id có code; chưa có result-release/review theo ownership; repo user query chưa đủ UI/API | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F21 | Hủy/đổi status bài nộp: SetStatusSubmit; UpdateStatus | CODE | Results: explicit void use case có quyền, lý do, audit; không xóa lịch sử | Update/delete Submission CRUD; chưa có void use case với lý do/audit/quyền | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F22 | Thống kê/phổ điểm theo class/exam/subject | CODE | Reporting: thống nhất tập lượt, trạng thái hủy và denominator; đối soát fixture | Chưa có endpoint report/score-distribution; ViewModels còn tồn tại không đồng nghĩa feature hoạt động | AdminDashboardPage hardcode stats, chưa fetch |
| F23 | Nhật ký tài khoản và thi: AccountLogs, ExamActivityLog; tab event | CODE | Audit: actor/server time, quyền đọc; tab event không đủ để kết luận gian lận | Log/ExamActivityLog CRUD; cả hai service thiếu DI; chưa có đầy đủ audit use case actor server | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F24 | Ranked.razor có markup và list model, chưa thấy tải dữ liệu xếp hạng | PARTIAL | OPEN: xếp hạng thật, tie-break, phạm vi bài/lượt và quyền hiển thị | Chưa có endpoint ranking | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F25 | AdminReport UI, Organization/OrganizationUser models | DECLARED/PARTIAL | OPEN: không tự triển khai đa tổ chức hoặc báo cáo mới chỉ vì có model/UI | Chưa thấy module organization/report nghiệp vụ tương ứng được expose | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |
| F26 | Gửi email: package/config SendGrid trong Web | DECLARED | OPEN: phải tìm call site thực tế trước nhận là tính năng nghiệp vụ cần port | Chưa có SendGrid package/API email nghiệp vụ được xác minh | Chưa có màn/service nghiệp vụ tương ứng trong cây FE |

## Theo dõi khi triển khai

### Task 07 tại checkout dev/Hop (2026-10-04)

| Feature | Trạng thái | Bằng chứng hiện tại và phần còn thiếu |
|---|---|---|
| F09 | IN_PROGRESS | Lookup `question-types`/`question-levels` dùng trên FE với permission 2; chưa làm màn CRUD loại/mức độ hoặc kiểm xóa lookup đang dùng. |
| F10 | IN_PROGRESS | `QuestionService` validate loại 1–3; `QuestionRepository.SaveWithAnswersAsync` ghi aggregate trong transaction serializable, chặn sửa/xóa câu đã dùng trong đề hoặc submission. FE `QuestionBankPage.jsx` lọc, tạo, sửa, ẩn và hiển thị đáp án qua API thật. Chưa có SQL fixture để xác minh rollback/concurrency, attachment upload, hoặc dữ liệu lịch sử. |
| F11 | OPEN | Testify seed chỉ 1–3, `FillAnswer=4` chỉ xuất hiện ở enum UI; chưa có engine tương ứng. API từ chối loại 4. |

Task 07 thêm `GET /web/questions/subjects` cho permission 2, lọc `subjectId/questionTypeId/questionLevelId`, và đổi request POST/PUT câu hỏi sang aggregate `answers`. Các route ghi `/web/answers` trả 405. Admin response có `IsCorrect`; student attempt projection vẫn tách riêng, chưa có attempt API. Không thêm migration. `dotnet build` và smoke validation/quyền/DI, `npm run lint` và Vite build đã qua; chưa đánh dấu VERIFIED khi chưa có SQL E2E.


### Task 06 tại checkout dev/Hop (2026-10-04)

| Feature | Trạng thái | Bằng chứng hiện tại và phần còn thiếu |
|---|---|---|
| F07 Classes/membership | IN_PROGRESS | BE thêm `ClassMembershipStore` khóa hàng Class trong giao dịch cho join/duyệt/xóa, kiểm trùng cặp và sĩ số đã duyệt; class create/update kiểm mã, capacity, giảng viên/môn. FE `AdminClassesPage.jsx` và `StudentClassesPage.jsx` gọi API thật; student query chỉ dựa claim. Legacy status 2 chờ duyệt, 1 đã duyệt. Chưa có SQL concurrency E2E, audit dữ liệu trùng cũ hoặc unique index ClassUser; Teacher management vẫn OPEN. |
| F16 ClassExamSchedule | DEFERRED_TO_TASK09 | Source hiện chỉ có CRUD, không kiểm trùng gán lịch, overlap học viên hoặc quan hệ Exam–Schedule. Chưa nối UI gán lịch; cần Task 09 chốt model/contract trước. |

Task 06 thêm route `/web/classes/options`, `/web/class-users/by-class/{classId}` và `/web/student/classes` (GET), `/join` (POST), `/{classId}` (DELETE). Không thêm migration/schema; endpoint ClassUser POST/PUT nay kiểm invariant và có thể trả 409. Source checkout mới hơn catalogue baseline, nên quyền `[Authorize]` trên Class/ClassUser/ClassExamSchedule và auth cookie Task 04 là căn cứ áp dụng.

Kiểm tra checkout: `dotnet build DACN_Project.sln --no-restore`, `npm run lint`, `npm run build`, `git diff --check` qua. Chưa có SQL Server/account fixture để thực chạy join/approve đồng thời, nên không gắn VERIFIED.

Sai khác tài liệu: `03-database/01-model.md` nói hai DbSet ClassExamSchedule chỉ có ở Testify, nhưng `ProjectDACNDbContext` hiện cũng có `ClassExamSchedules` và `UserExamSchedules` cùng kiểu `ClassExamSchedule`. Cần xử lý trong Task 09 khi chốt mapping lịch; Task 06 không đổi schema này.

### Task 05 tại checkout dev/Hop (2026-10-04)

| Feature | Trạng thái | Bằng chứng hiện tại và phần còn thiếu |
|---|---|---|
| F04 Users | IN_PROGRESS | FE `AdminUsersPage.jsx` nối list/detail/create/update/delete với `/web/users`; search gọi query `GET /web/users/get-all-users?search=`. BE đã có GET/PUT detail từ Task 04 và Task 05 thêm email verification khi admin tạo user. Chưa có server paging, import/export và E2E SQL/mail. |
| F05 Levels/Permissions | IN_PROGRESS | FE `CatalogPage.jsx` dùng `/web/levels`, `/web/permissions`; `UserPermissionsPage.jsx` dùng `/web/user-permissions`. BE bổ sung kiểm tồn tại user/permission và trùng cặp trước create/update, bảo vệ ID hệ thống 1..4. Chưa có unique DB constraint, nên vẫn có race; thay đổi quyền chỉ thể hiện trong JWT cấp mới. Quyền Teacher ngoài Task 05 vẫn OPEN. |
| F06 Subjects | IN_PROGRESS | FE list/search/detail từ hàng list, create/update/delete qua `/web/subjects`; BE thêm validation tên. Theo quyết định người dùng, giữ `SubjectManagement` cho xóa, không cấm tên trùng. Chưa xác minh DB. |
| F08 Rooms | IN_PROGRESS | FE CRUD/paging/search qua `/web/rooms`; BE `RoomQueryDto` validate page/pageSize/capacity, controller kiểm range và lịch thi trước xóa, repository OrderBy(Id) trước Skip/Take. Theo quyết định người dùng, không cấm tên trùng. Chưa có SQL E2E. |

Task 05 giữ `/web` và `ApiResponse`; không thêm route hoặc migration. `npm run lint`, `npm run build`, `dotnet build DACN_Project.sln --no-restore` qua tại checkout; build BE còn warning cũ. Chưa đánh dấu VERIFIED vì chưa có DB/account đủ quyền để chạy CRUD end-to-end.

Quyết định người dùng ngày 2026-10-04: giữ hard delete tài khoản của BE, giữ quyền xóa môn theo `SubjectManagement` hiện tại (permission 3), không cấm trùng tên môn/phòng. BE tạo user quản trị nay phát hành email verification token và gửi liên kết như luồng đăng ký; FE có thao tác gửi lại. Chưa xác minh mail/SQL thật.

Bước tiếp theo cho mỗi feature: legacy file+symbol; BE path+commit; FE path+commit; schema migration; contract diff; test evidence; sai khác được duyệt; trạng thái TODO/IN_PROGRESS/VERIFIED/DEFERRED. Không ghi VERIFIED chỉ vì màn hình render hoặc endpoint trả 200.

Phải duyệt rõ F11/F24/F25/F26 và quyền Teacher để tránh bỏ sót hoặc mở rộng phạm vi không có căn cứ.
