# As-is A01–A06 — kiểm kê 22/10/2026

## Phạm vi và trạng thái

BE `dev/Hop` 04c5d28; FE `dev/Hop` 348eb4d. Các thay đổi chưa commit sẵn có (BE `.gitmodules`, `AuthController.cs`, DOCX, `impeccable`; export Testify hai repo) được giữ. Ảnh `VibeCoding/images/01–07` là mockup; không thêm quên mật khẩu, xuất báo cáo, admin xem đáp án, mời user, ranking hay biểu đồ vì ảnh.

| As-is | Route BE/policy và tầng ghi | Trang FE | Kiểm thực chạy | Trạng thái |
| --- | --- | --- | --- | --- |
| Bốn role | JWT `level_id`, AdminManagement/Teacher/Student và permission policy; Teacher owner qua khóa Class | Điều hướng Admin/Examiner/Teacher/Student, URL khác role hiện 403 ở FE | Build BE/FE, API Student→Teacher 403, Teacher owner; chưa ma trận đủ Admin/Examiner/browser URL khác role | PARTIAL |
| Student join bằng code | `/web/student/classes/join`, Student, `ClassMembershipStore` | `StudentClassesPage` | Hồi quy source; chưa chạy lại browser | PARTIAL |
| Teacher thêm/duyệt/xóa | `/web/teacher/classes/*`, Teacher, `ClassMembershipStore` | `TeacherClassesPage` | API/SQL clone: 401, Student 403, owner 404, lookup, add/duplicate/approve/remove đạt; Chrome FE lookup/add đạt; chưa capacity/race | PARTIAL |
| Lớp/môn/bài thi/mã đề/câu hỏi | `/web/classes`, `/web/subjects`, `/web/exams`, `/web/exam-details`, `/web/questions` | AdminClasses/Catalog/ExamManagement/QuestionBank | Source và build/lint; chưa hồi quy browser | PARTIAL |
| Import Excel | `/web/questions/import`, QuestionManagement, transaction Serializable | QuestionBankPage | API/SQL clone với `.xlsx` thật: Student 403, invalid 400, valid 200, duplicate 409, 1 question/3 answers; Chrome FE import thật đạt; chưa rollback nhiều dòng | PARTIAL |
| Thi/chấm/timeout/ẩn điểm | `/web/attempts`, Student, snapshot/finalizer/worker | StudentAttempt/History | Source và build/lint; chưa chạy lại API/browser | PARTIAL |
| Rời tab | `/web/attempts/{id}/visibility-*`, Student, bảng AttemptVisibilityEvents | StudentAttemptPage | API/SQL clone: 401/404, count 0→1→2, retry không đếm đôi, đóng bài 409; Chrome giả lập visibility hidden tăng 2→3 và reload giữ count; chưa chuyển tab thật/offline/two-tab/timeout | PARTIAL |
| Tránh trùng môn/lịch | `/web/exam-schedules`, `/web/class-exam-schedules`, ScheduleManagement, ScheduleWriteGuard | AdminSchedulesPage | API/SQL clone: Student 403, cùng lớp cùng môn 409, khác lớp 201, chạm mép 201, update giao giờ 409; chưa concurrent/unknown/attempt/browser | PARTIAL |

Không mục nào được đánh dấu VERIFIED chỉ bằng build, route hoặc mockup. F20–F22 legacy giữ scope riêng, không gộp vào `dacnAttemptsOnly` và không suy fixture là dữ liệu Testify thật.

## Mapping control mockup → API/policy → FE

| Hình | Control có hợp đồng | FE hiện tại và khác biệt |
| --- | --- | --- |
| 01 Auth | Login, đăng ký Student và verify email theo auth lifecycle | Có trang Login/Register/VerifyEmail; bỏ link quên mật khẩu và nút Google không có API; login Teacher về “Lớp của tôi” |
| 02 Dashboard/báo cáo | Admin report `dacnAttemptsOnly`, legacy report tách `legacySubmissionsOnly` | AdminDashboardPage/AdminAttemptReport dùng scope API; hình minh họa không quyết số liệu |
| 03 Lớp/câu hỏi/bài thi | Admin classes, Teacher classes, QuestionManagement, ExamManagement, import v1 | AdminClasses, TeacherClasses, QuestionBank, ExamManagement; không thêm export đáp án |
| 04 Lịch | ScheduleManagement và 409 từ BE | AdminSchedulesPage giữ modal/form khi lỗi; chọn giờ Việt Nam gửi UTC Z |
| 05 Student | Join, lịch, start/save/resume/submit/history, visibility count | StudentClasses/Schedules/Attempt/History; count chỉ ghi nhận |
| 06 Danh mục/user | Subject/Room/AdminManagement có API | CatalogPage/AdminUsersPage; không tạo control không có route |
| 07 Review/void | Review Student sau lịch và AllowViewResult; void Admin | StudentAttemptPage/History và Admin report; không mở đáp án cho Admin |

## Kiểm môi trường

`dotnet build DACN_Project.sln --no-restore -m:1 -v quiet` đạt, 0 errors, các warning hiện hữu. `dotnet run --project tests/DependencyInjectionSmoke/DependencyInjectionSmoke.csproj --no-build` đạt, gồm template Excel v1, snapshot, scoring, validation và DI. `npm run lint` và `npm run build` đạt sau khi script build dùng Vite native config loader; bundler config mặc định bị `spawn EPERM` trên máy này. FE `schedulePolicy.test.js` và `scheduleTime.test.js` chạy trực tiếp qua `node file` đạt 4 ca; `node --test` bị `spawn EPERM` trên sandbox.

SQL clone preflight qua `sqlcmd` ngoài sandbox: `DB_NAME()=ProjectDACN_Task04Task09_Test`, 8 migration đến Task12, `ExamAttempts=0`, `ExamAttemptSubmissions=0`, chưa có bảng visibility. Đã review và áp **chỉ trên clone** `tests/TaskAsIsVisibilityMigration.sql`: lịch sử 9 migration đến `20261022000000_AttemptVisibilityEvents`; bảng có `AttemptId`, `EventId`, `RecordedAtUtc`, `IsCounted`, 1 FK và 1 index phụ; 0 event. `tests/TaskAsIsVisibilitySqlSmoke.sql` chèn fixture trong transaction rồi rollback đạt; `TaskAsIsVisibilityDownDryRun.sql` thử DROP/history DELETE trong transaction rollback rồi xác nhận schema/history còn nguyên. `TaskAsIsTeacherApiSmoke.ps1`, `TaskAsIsQuestionImportApiSmoke.ps1`, `TaskAsIsVisibilityApiSmoke.ps1`, `TaskAsIsSubjectOverlapApiSmoke.ps1` đều đạt qua HTTPS API cục bộ trỏ clone; mỗi script dọn fixture trong `finally`. Chrome qua Vite HTTPS proxy đã đạt Teacher lookup/add, import Excel FE–API–SQL, visibility hidden giả lập/reload. Hậu kiểm QA user/class/question/attempt/event bằng SQL đều 0. DB gốc `ProjectDACN` truy vấn chỉ đọc vẫn có 1 migration initial và `AttemptVisibilityEvents=NULL`.

## Các ca cần chạy trên clone trước nghiệm thu

1. Chạy `Down` thật bằng EF trên clone ở cửa sổ bảo trì nếu cần nghiệm thu migration trọn vòng; hiện đã có dry-run SQL rollback và `Up` thật.
2. A01: Admin 403 route Teacher; email chưa xác minh, lớp đầy, duyệt đồng thời và lịch học viên giao nhau 409. Kiểm Admin/Student cũ hồi quy.
3. A02: file v1 thật thêm loại 1/3; công thức, thiếu cột, mức độ/môn sai, quá 500 dòng/2 MiB; SQL rollback không còn Question/Answer lẻ.
4. A03: Chrome chuyển tab thật, offline, hai tab, submit/timeout đồng thời; kiểm giới hạn mất event khi đóng browser. Browser smoke hiện dùng sự kiện hidden giả lập để kiểm listener/FE–API–SQL, chưa chứng minh browser luôn gửi event khi tab bị đóng.
5. A04: khác môn cùng phòng, cùng học viên khác lớp, hai request đồng thời, lịch unknown và lịch có attempt; kiểm FE 409.
6. A05: auth, CRUD, join, lịch UTC, start/save/resume/submit/timeout, AllowViewResult=false, review sau giờ kết thúc, void chỉ “Đã hủy”, dashboard/report scope riêng. Chỉ cập nhật VERIFIED theo bằng chứng thực chạy.

## Kết quả P1 — 11/11/2026

BE `dev/Hop` sau P3 (chưa commit), FE `dev/Hop` sau P3. Toàn bộ chạy qua HTTPS API cục bộ (Development) trỏ `ProjectDACN_Task04Task09_Test`; mỗi script tạo fixture riêng mang tag và dọn trong `finally`. Hậu kiểm SQL sau cả loạt: 0 user/lớp/bài thi/câu hỏi/lượt thi QA còn lại. DB gốc `ProjectDACN` không bị ghi.

| As-is | Script | Ca đã chạy (kết quả thực) | Trạng thái |
| --- | --- | --- | --- |
| A01 Teacher | `tests/TaskP1AcceptanceApiSmoke.ps1` | Email chưa xác minh: lookup/add 404; lớp sức chứa 1: thêm người thứ hai 409; hai request duyệt chỗ cuối đồng thời 200/409, SQL sĩ số 1; học viên có lịch giao nhau ở lớp khác 409, lịch chạm mép 201; Admin vào route Teacher 200 (Admin toàn quyền theo Task_AdminPermission), Student 403. Chrome: Teacher mở lớp, tab Kết quả thấy lượt đã nộp | VERIFIED |
| A02 Import Excel | `tests/TaskP1ImportMatrixApiSmoke.ps1` | 400: ô có công thức, thiếu cột tiêu đề, sai tên sheet, 501 dòng, tệp > 2 MiB (dưới RequestSizeLimit), đuôi không phải .xlsx. 400 theo dòng với đúng mã: `2:TYPE_ID, 3:CORRECT_INDEXES, 5:DUPLICATE_FILE, 6:ANSWERS, 7:INVALID_ANSWERS, 8:CONTENT`, dòng hợp lệ cùng tệp không bị ghi. 409: môn/mức độ không tồn tại; một dòng trùng ngân hàng làm cả tệp 409, hai dòng hợp lệ khác không bị ghi lẻ. 200: loại 1/2/3 trong một tệp, SQL 4 câu/12 đáp án/5 đúng. Student 403 | VERIFIED |
| A03 Rời tab | `tests/TaskP1AcceptanceApiSmoke.ps1` | Hai tab gửi hidden cùng lúc: 200/200, chỉ tính 1; sự kiện sau >1 giây tính tiếp; gửi lại cùng `eventId` (mất mạng rồi thử lại) không đếm đôi; nộp bài và hidden đồng thời: submit 200, SQL 1 bài nộp, sự kiện 200 hoặc 409; sau nộp 409; sau hạn 409 và lượt được chốt `timedOut` | PARTIAL — phía máy chủ đạt; chưa chứng minh trình duyệt luôn gửi được sự kiện khi tab bị đóng/mất mạng lâu (giới hạn của client) |
| A04 Trùng môn/lịch | `tests/TaskP1AcceptanceApiSmoke.ps1` | Khác môn cùng phòng giao giờ 409, khác phòng 201; cùng học viên ở hai lớp khác nhau, hai bài khác môn giao giờ 409; hai request gán đồng thời hai lịch cùng môn giao giờ vào một lớp 201/409, SQL còn 1 bản ghi; lịch unknown: sửa/xóa/gán 409; lịch đã có lượt thi: dời giờ/xóa/gán thêm lớp 409 | VERIFIED (API/SQL; hiển thị 409 ở FE đã kiểm trong Task09) |
| A05 Hồi quy 4 role | `tests/TaskP1RoleRegressionSmoke.ps1` → `DACN_FE/tests/p1RoleRegressionBrowserSmoke.mjs` | Chrome headless, mỗi role một browser context, đăng nhập qua form thật, 25 bước. Student: vào lớp, lịch dạng thẻ "Còn 2 lượt", làm bài, lưu bản nháp, tải lại khôi phục, nộp qua hộp xác nhận, lịch sử "Lần 1/2"; bài `AllowViewResult=false` nộp xong không lộ điểm, lịch sử "Chưa công bố kết quả"; lượt thứ hai lưu nháp rồi hết hạn → tải lại thấy "Đã hết thời gian làm bài" và điểm chấm từ nháp; lịch kết thúc → "Xem lại bài" mở đáp án đúng từ snapshot; vào lớp bằng mã → "Chờ duyệt" → "Đã tham gia". Teacher: tab Kết quả, duyệt học viên xin vào lớp. Examiner (chỉ quyền câu hỏi): tổng quan dự phòng, menu đúng quyền, ngân hàng câu hỏi. Admin: KPI, hủy kết quả có lý do, vào mọi khu vực, CRUD qua giao diện (môn: thêm/sửa/ẩn; lớp có mã và giảng viên; bài thi nháp). URL khác role bị chặn ở FE. Hậu kiểm SQL `2/1/1/1,1,1,1,1` (2 lượt: 1 nộp, 1 hết giờ, 1 hủy đúng người/lý do; bài ẩn 1 bài nộp; môn đổi tên và ẩn 255; lớp UI đúng giảng viên, học viên đã duyệt; bài thi trạng thái 2). Bản cuối chạy 2 lần liên tiếp đều đạt | VERIFIED |
| A06 Parity | Tài liệu này | Cập nhật bảng trên; không đánh VERIFIED chỉ bằng build/route | Đã cập nhật |

Hồi quy cùng ngày (đều PASS): `TaskAsIsTeacherApiSmoke`, `TaskAsIsQuestionImportApiSmoke`, `TaskAsIsVisibilityApiSmoke`, `TaskAsIsSubjectOverlapApiSmoke`, `TaskAdminPermissionApiSmoke` (với `-BaseUrl https://localhost:7242`), `TaskP2DashboardApiSmoke`, `TaskP3StudentFlowSmoke`; `TaskP1AcceptanceApiSmoke` chạy 3 lần liên tiếp đều đạt.

Lưu ý môi trường: DB gốc `ProjectDACN` hiện đã có đủ 11 migration (gồm `0911`, `0911_new`), khác ghi nhận cũ "1 migration initial". Browser smoke cũ `attemptBrowserSmoke.mjs`, `studentScheduleBrowserSmoke.mjs`, `visibilityBrowserSmoke.mjs` tìm dòng bảng lịch thi đã bị thay bằng thẻ nên không còn chạy được; luồng tương ứng đã được `p1RoleRegressionBrowserSmoke.mjs` thay thế. Tệp > RequestSizeLimit bị Kestrel đóng kết nối (không trả JSON) — chưa đưa vào ca kiểm.

### Bổ sung A05 — phát hiện trong quá trình kiểm (11/11/2026)

- **Đã xử lý (11/11/2026): giới hạn tần suất `/auth/refresh` theo IP.** Trước đây policy `auth-session` chỉ phân vùng theo IP với 20 lần/phút, nên cả phòng thi dùng chung IP NAT chỉ có 20 lượt refresh mỗi phút; tải lại trang hàng loạt làm học viên bị 429 rồi bị đăng xuất. Nay (`Project.Api/Services/SessionRateLimits.cs`, cấu hình `RateLimits` trong appsettings): 10 lần/phút cho mỗi refresh cookie; 20 lần/phút/IP khi không có cookie (giữ như cũ); thêm trần 300 lần/phút/IP cho refresh + logout. Token vẫn xoay vòng và dùng lại token cũ vẫn thu hồi cả họ. `tests/TaskSessionRateLimitApiSmoke.ps1` đạt: 25 phiên chung IP đều 200; một phiên lặp: 200, 9×401, lần 11 bị 429; phiên khác không bị ảnh hưởng; không cookie: 20×401 rồi 429; cookie ngẫu nhiên: request thứ 301 bị 429. Hồi quy trình duyệt 4 role với `DACN_QA_FULL_RELOAD=1` (mỗi lần chuyển trang đều tải lại toàn trang, kịch bản trước đây làm mất phiên) đạt 25/25.
- Bảng lớp/bài thi phân trang 10 dòng; bản ghi mới nằm ở trang sau (lớp có ô tìm kiếm, bài thi chưa có). Không phải lỗi, nhưng trang bài thi nên có tìm kiếm khi làm P5.