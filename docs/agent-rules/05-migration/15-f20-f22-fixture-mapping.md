# F20–F22: mapping fixture kỹ thuật trước khi ghi đích — 2026-10-19

Nguồn giả: `ProjectDACN_LegacyFixture_Test`, migration duy nhất `20260628110050_2806_updaetmơi11`, marker `QA_F20F22_20261019`. Đích thử: `ProjectDACN_Task04Task09_Test`, 7 migration đến Task11 trước thay đổi. Không dùng DB `ProjectDACN` gốc hoặc `Testify` làm đích. Fixture không chứng minh parity với dữ liệu Testify thật.

## Bảng mapping và số lượng kỳ vọng

| Nguồn / khóa | Đích kỹ thuật | Số nguồn | Số đích dự kiến | Kiểm trước insert |
|---|---|---:|---:|---|
| `Users.Id` (2 GUID) | `LegacyUserMaps.SourceUserId → Users.Id` của 2 tài khoản Student QA riêng | 2 | 2 map, 0 User copy từ hash fixture | Hai tài khoản QA tồn tại, Level Student và khác nhau; không import password hash giả. |
| `Subjects.Id`/`Rooms.Id`/`Classes.Id`/`Exams.Id`/`ExamSchedules.Id`/`ExamDetails.Id` (mỗi loại 1) | Chỉ giữ source ID và nhãn Exam/Schedule trong projection; không đụng bảng nghiệp vụ DACN | 1 mỗi loại | 0 row nghiệp vụ | Source FK/quan hệ hai chiều nhất quán; timezone lịch `unknown`. |
| `Questions.Id` (1), `Answers.Id` (2) | Chỉ validate source; không tạo snapshot từ ngân hàng hiện tại | 1 / 2 | 0 | Answer thuộc Question; không xuất `IsCorrect` ở API học viên. |
| `DoingExams.Id` (1) | Giữ nguồn riêng, không tạo `ExamAttempts` | 1 | 0 | FK User/Exam/Detail/Schedule hợp lệ. |
| `Submissions.Id` (4) | `LegacySubmissionRecords` | 4 | 4 | Source User/ExamDetail/Exam/Schedule khớp; `TotalMark float`, `IsPassed`, `Status bit?`, `SubmitTime datetime2`, `TimeTaken time` giữ nguyên. Source IDs 1–4 trên bản sao mới. |
| `AnswerSubmissions.Id` (3) | `LegacyAnswerSelections` | 3 | 3 | FK Submission/Question/Answer hợp lệ; lưu ID lựa chọn, không lưu cờ đúng hay snapshot. |

Mọi row projection thuộc `LegacyImportBatch` có `SourceSystem=syntheticLegacyFixture`, `SourceDatabase=ProjectDACN_LegacyFixture_Test`, source migration, marker và batch GUID. Mỗi submission/answer có `SourceTable` + `SourceId`, unique theo batch để chạy lại không nhân đôi; checksum SHA-256 gồm các trường gốc quyết định. Source user map có unique `(BatchId, SourceUserId)` và FK TargetUserId. `ExamAttempts`/`ExamAttemptSubmissions` **không được insert**.

Nguồn preflight: 4 submission, 3 answer rows, 2 user, 1 exam/schedule/detail, 0 broken FK. `Status=true/false/null` lần lượt 2/1/1; `IsPassed=true` 3; tổng điểm SQL float = khoảng `10.02` (không dùng tổng đó để tính lại từng điểm). Hai score biên đọc qua SQL là `1.0149999999999999` và `1.0049999999999999`; không ép sang decimal hai chữ số. Trường hợp không ánh xạ được phải rollback: user chưa map/không phải Student, parent/answer FK mồ côi, cặp Exam–Schedule sai, duplicate source ID, checksum khác khi chạy lại, count 4/3 sai, hoặc DB/migration không đúng.

## Transaction, chạy lại và rollback

Dry-run: `BEGIN TRAN`, nhập batch/map/4 submission/3 answer, đối soát count, FK, checksum, điểm và trạng thái với source; `ROLLBACK` và kiểm các bảng projection trở về 0. Apply thử chỉ trên bản sao đích sau dry-run đạt. Rerun cùng batch phải xác nhận checksum/map không đổi và giữ nguyên count 4/3. Rollback batch thử: trong một transaction, kiểm đúng marker/batch/provenance và không có phụ thuộc ngoài batch, xóa AnswerSelection → SubmissionRecord → UserMap → Batch; postflight 0 row marker. Nếu kiểm không đạt sau commit, restore backup bản sao đã chụp trước apply; không xóa theo điểm/tên học viên.

API học viên chỉ đọc projection theo TargetUserId từ JWT; detail của user khác 404; review đáp án đúng không khả dụng khi không có snapshot gốc. API admin có scope `legacySubmissionsOnly` riêng, thống kê Status true/false/null và tổng điểm gốc; không union vào `dacnAttemptsOnly`.
