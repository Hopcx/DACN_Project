# As-is additions v1 — 22/10/2026

BE `dev/Hop` 04c5d28, FE `dev/Hop` 348eb4d trước thay đổi local. `ApiResponse` vẫn dùng `success`, `data`, `message`; JWT Bearer và `/web` giữ nguyên.

## A01 — Teacher classes

Policy `Teacher` yêu cầu claim `level_id=3`. Mọi route dưới `/web/teacher/classes` dùng user ID từ `NameIdentifier` và chỉ truy cập `Classes.TeacherId` trùng claim. Lớp khác trả 404; anonymous 401; Admin/Student 403. Admin CRUD `/web/classes` và `/web/class-users` giữ nguyên; Student join bằng code giữ nguyên.

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
