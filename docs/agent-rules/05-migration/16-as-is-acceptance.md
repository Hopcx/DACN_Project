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
