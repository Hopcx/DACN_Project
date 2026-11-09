# Decision register v0.2

## Quyết định hủy lượt và nguồn Task12 — 2026-10-19

Lượt DACN đã hủy **vẫn tính vào tổng lượt tối đa `NumberOfRepeat`**. Hủy kết quả chỉ đổi quyền hiển thị và tập thống kê; không xóa attempt, không hoàn lượt và không thay điểm/đáp án audit. Code Testify `SubmissionReposiroty.CheckNumberOfSubmit` đếm mọi Submission theo user/lịch mà không lọc `Status`, phù hợp với quy tắc này. Nếu sau này cần hoàn lượt, phải có use case quản trị **riêng** với quyền, lý do, actor, giờ server và audit; không gắn ngầm vào thao tác hủy.

Người dùng xác nhận dùng DB `ProjectDACN` làm nguồn Task12. Preflight chỉ đọc cho thấy DB này có 0 Exam/DoingExam/Submission; bản sao có dữ liệu QA phát sinh sau restore. Vì vậy chưa có lượt Testify thực tế để chuyển hoặc đối soát điểm. Không lấy DB `Testify` cục bộ hay fixture trong bản sao làm nguồn thay thế nếu chưa được chỉ định lại.

## Quyết định Task10 cho lượt DACN mới — 2026-10-18

Giữ snapshot, deadline UTC cố định và số lượt tính khi transaction start thành công. Chấm điểm từ snapshot bằng `decimal`: mỗi câu chỉ được trọn điểm khi tập chọn bằng đúng tập đáp án đúng; bỏ trống, thiếu hoặc thừa được 0. Cộng điểm trước, so `PassMark` với **tổng chưa làm tròn**, rồi làm tròn tổng một lần đến hai chữ số bằng `MidpointRounding.ToEven` để lưu/hiển thị. Vì vậy có thể hiển thị 1.00 nhưng vẫn đạt ngưỡng 1.004, hoặc hiển thị 1.02 nhưng chưa đạt ngưỡng 1.016. Không đổi trạng thái đạt để khớp điểm hiển thị.

Testify dùng `double` và `Math.Round`: 1.015 cho 1.01; DACN dùng `decimal`: 1.015 cho 1.02. Không sửa hoặc chấm lại điểm lịch sử Testify. Task12 phải liệt kê các ca sai khác số học trong kết quả đối soát, không xem là dữ liệu bị mất. Deadline = `min(serverStart + Exam.Duration, ExamSchedule.EndTime)`; từ `serverNow >= DeadlineUtc` chỉ chấm bản nháp đã lưu. `NumberOfRepeat` là tổng lượt tối đa theo source Testify.

FACT phản ánh commit đã đọc. REQUIRED là quy tắc triển khai trên codebase hiện hữu. PROPOSED không được ngầm coi là đã có code.

| ID | Quyết định / trạng thái | Căn cứ và tác động |
|---|---|---|
| A01 | Giữ bốn project Project.* — REQUIRED | Khung đã có; sửa boundary I/O Domain và auth controller theo từng việc |
| A02 | Baseline .NET 8 / EF 8.0.14 — FACT | Nâng .NET 10 là nhiệm vụ riêng, không tự nâng trong port feature; đánh giá vòng đời trước production |
| A03 | JavaScript JSX + Vite — FACT/REQUIRED | TypeScript strict là lựa chọn migration riêng, chưa có tsconfig/typecheck script |
| A04 | Ant Design 6 + Tailwind 4 — FACT/REQUIRED | Cả hai đã được bootstrap; form hiện tại vẫn là HTML/useState |
| A05 | Giữ Redux Toolkit + Context và Axios — REQUIRED | Không thêm TanStack Query/Zustand mặc định; có thể mở rộng bằng RTK Query khi cần, đây chưa là code đang có |
| A06 | Form mới ưu tiên Ant Design Form — PROPOSED | Chuyển từng form; không gọi HTML form hiện tại là AntD Form |
| A07 | EF Code First + SQL Server — FACT/REQUIRED | DbContext/configurations/migration đã có; phiên bản SQL engine/data deployed vẫn OPEN |
| A08 | Chưa triển khai Redis — REQUIRED baseline | Không thấy dependency/tích hợp Redis; chưa có tải để chứng minh cần |
| A09 | Index theo query; chưa thêm procedure mặc định — REQUIRED | Repo có IADO/helper raw SQL, không phải bằng chứng có stored procedure nghiệp vụ đã deploy |
| A10 | Giữ AutoMapper 12 + FluentValidation 11 — REQUIRED | Đã dùng cho User; mở rộng có kiểm chứng, không thay bằng framework mới |
| A11 | Attempt/snapshot/server grading — ĐÃ TRIỂN KHAI CHO LƯỢT MỚI | Attempt API dùng snapshot và finalizer riêng; Submission CRUD lịch sử không dùng để nộp bài mới. Xem contract thi và báo cáo Task10. |
| A12 | Giữ contract auth body để tích hợp, rồi harden có kế hoạch — REQUIRED | Hiện trả accessToken/refreshToken JSON; HttpOnly cookie và token hash là đích chuyển đổi, chưa tồn tại |
| A13 | `/web/*` và ApiResponse — FACT/REQUIRED | Không gọi `/api/v1` hay assume response ProblemDetails cho mọi lỗi |
| A14 | Lịch sử không snapshot dùng projection riêng — quyết định kỹ thuật Task12 | Fixture giả trên bản sao khẳng định khả năng lưu provenance/score float/Status ba giá trị mà không tạo `ExamAttempts`; review đáp án đúng trả 409. Chưa suy ra trạng thái Testify thật hoặc nhập dữ liệu nguồn gốc. |

## OPEN và mục đã giải quyết

| ID | Trạng thái | Nội dung |
|---|---|---|
| O01 | RESOLVED | URL đã xác nhận; dùng default branch master BE, main FE, pin SHA trong README |
| O02 | RESOLVED 2026-10-22 | Không chuyển dữ liệu Testify. DACN chạy trên DB mới; `ProjectDACN` nguồn có 0 Exam/Submission nên không có gì để ETL. Projection legacy và fixture F20–F22 giữ nguyên làm công cụ kỹ thuật, không xem là dữ liệu thật. Muốn ETL sau này phải mở task riêng với backup và dry-run. |
| O03 | RESOLVED 2026-10-22 | Không làm FillAnswer (loại 4) và xếp hạng. Testify chỉ seed loại 1–3, `Ranked.razor` không tải dữ liệu. API tiếp tục từ chối loại 4. Báo cáo placeholder chỉ giữ báo cáo lượt DACN thật. |
| O04 | RESOLVED baseline | FE đã có Ant Design/Tailwind; tiếp tục dùng, không cần hỏi chọn lại UI library |
| O05 | RESOLVED cho lượt DACN mới | Chấm chính xác tập đáp án, không điểm từng phần; decimal ToEven hai chữ số, đạt theo điểm trước làm tròn; tính lượt khi start transaction commit. Lượt Testify cũ giữ nguyên. |
| O06 | RESOLVED cho lượt DACN mới | Deadline UTC cố định = min(start+duration, scheduleEnd); đúng deadline đã quá hạn. |
| O07 | RESOLVED 2026-10-22 | Teacher (level 3) chỉ thao tác trên lớp có `Classes.TeacherId` trùng claim: thêm/duyệt/xóa thành viên theo email, xem lịch thi và kết quả điểm (chỉ đọc, không xem đáp án, lượt hủy không hiện điểm) của học viên đã duyệt. Teacher không tạo câu hỏi, bài thi, lịch; việc đó theo permission 1/2/4 cấp cho Admin/Examiner. Đăng ký công khai chỉ tạo Student, email phải xác minh. |
| O08 | RESOLVED 2026-10-22 | Khóa cứng, không sửa tại chỗ. Mã đề công khai hoặc đã có lượt thi không được sửa; lịch đã có attempt không được đổi giờ/phòng/gán lớp. Muốn thay đổi thì tạo bài thi/lịch mới. Lượt đang làm luôn dùng snapshot, nên sửa ngân hàng câu hỏi không ảnh hưởng bài đã bắt đầu. |
| O09 | RESOLVED 2026-10-22 | Dữ liệu `DateTime` cũ không có thông tin múi giờ được coi là giờ Việt Nam (UTC+7) khi và chỉ khi có ETL được duyệt; cho đến lúc đó giữ `IsTimeUtc=0` và FE hiển thị "chưa rõ múi giờ". Mọi bản ghi mới lưu UTC, FE hiển thị giờ Việt Nam. `Exam.Duration` tính bằng phút. |
| O10 | RESOLVED 2026-10-22 | Mục tiêu triển khai: một backend + SQL Server, cùng origin qua HTTPS reverse proxy, tối đa khoảng 300 thí sinh đồng thời, SMTP cấu hình bằng biến môi trường. Giới hạn tốc độ auth theo tiến trình nên nếu chạy nhiều instance cần chuyển lên ingress. Giữ log kiểm toán 12 tháng và attempt/kết quả không xóa. Chưa có SLA, chưa dùng Redis hay microservice. |
| O11 | OPEN | Agent chính là Codex/Cursor/Copilot để đóng gói native rule adapters |

Tiếp tục phần độc lập đã đủ bằng chứng; chỉ hỏi khi quyết định thay đổi semantics. Xem [audit](06-codebase-audit.md) cho thứ tự blocker cần xử lý. Nguồn vòng đời framework ở [external sources](05-external-sources.md), tách khỏi version hiện tại trong csproj.
