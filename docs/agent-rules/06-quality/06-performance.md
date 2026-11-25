# Hiệu năng — thử tải sau To-be T1–T18 (đợt tổng kiểm 25/11/2026)

**Kết quả: PASS.** Cả 14 endpoint đều có p95 < 2 000 ms; p95 cao nhất là 196 ms. Không cần thêm index hay migration.

## Cách chạy
```powershell
pwsh -NoProfile -File tests/PerfBenchmark.ps1 -ResultPath "$env:TEMP\dacn-perf.json"
```
- **Điều kiện:**
  - Chạy từ thư mục gốc DACN_Project.
  - API chạy ở môi trường Development, trỏ vào `ProjectDACN_Task04Task09_Test`.
  - Lệnh SQL của script từ chối mọi DB khác. Sau khi đăng ký, script kiểm tài khoản vừa tạo qua API có trong DB bản sao; không có (API trỏ DB khác) thì dừng trước mọi bước seed.
  - File media được dọn theo thư mục mặc định Development (`dacn-media-dev` cạnh repo), tức `Media:RootPath` để trống.
- **Tham số:**
  - `-Runs`: số lần đo mỗi endpoint, mặc định 20.
  - `-ThresholdMs`: ngưỡng p95, mặc định 2000.
- **Tách khỏi `RunUatSuite`:**
  - Script không nằm trong `RunUatSuite`.
  - Script đăng ký 1 tài khoản qua API, nhân bản 3 tài khoản (giảng viên, người ra đề, Admin) và 199 học viên bằng SQL, rồi đăng nhập 4 lần. Không chạy song song với UAT, vì giới hạn đăng nhập dùng chung.
- **Dọn dữ liệu:**
  - Dữ liệu seed được dọn trong `finally`, gồm cả file media trên đĩa.
  - Cuối lần chạy, script in số bản ghi còn sót; kết quả phải là `0|0|0|0|0|0|0` (users, subjects, media, exams, classes, questions, logs).

## Dữ liệu seed
Phần lớn dữ liệu được ghi thẳng bằng SQL. Riêng media và đoạn đi qua API.

| Thành phần | Số lượng |
|---|---|
| Câu hỏi | 500, trong 5 chương, 3 mức độ: loại 1 ×80, loại 2 ×200, loại 3 ×80, loại 4 ×60, loại 5 ×40, loại 6 ×40 |
| Đoạn dùng chung | 20 đoạn × 3 câu con, mỗi đoạn 1 ảnh; tạo bằng `POST /web/question-groups` và `/media` |
| Media | 20 ảnh thật; 80 câu gắn media; 300 dòng media mồ côi (chỉ có dòng `MediaFiles`) |
| Lớp | 5 lớp × 40 học viên, cùng một giảng viên |
| Lịch thi đã kết thúc | 10 lịch, mỗi lịch giao cho cả 5 lớp → 2 000 lượt đã nộp (snapshot v3, 20 câu) |
| Outcome | 44 000: 40 000 từ bài thi, 4 000 từ 200 phiên luyện tập × 20 câu |

**Giới hạn của dữ liệu seed:**
- Phiên luyện tập chỉ được seed ở dạng outcome; không có dòng `PracticeSessions`. Phân tích học viên chỉ đọc outcome nên không bị ảnh hưởng.
- Học viên dùng để đo có 1 200 outcome: 50 phiên luyện tập và 10 lượt thi.

## Kết quả (máy dev, SQL Server cục bộ, API Debug, 20 lần sau 1 lần khởi động)
| Endpoint | p50 (ms) | p95 (ms) | max (ms) |
|---|---:|---:|---:|
| `GET /web/teacher/classes/{id}/ranking` | 62 | 79 | 80 |
| `GET /web/teacher/classes/{id}/results` | 43 | 63 | 69 |
| `GET /web/teacher/classes/{id}/analysis` (báo cáo lớp) | 170 | 196 | 227 |
| `GET /web/teacher/classes/{id}/report.xlsx` | 171 | 191 | 222 |
| `GET /web/student/classes/{id}/ranking` | 60 | 67 | 69 |
| `GET /web/student/analysis?days=365` | 46 | 68 | 70 |
| `POST /web/student/practice` (50 câu) | 51 | 79 | 93 |
| `GET /web/student/practice/mine` | 17 | 23 | 23 |
| `POST /web/exams/{id}/variants` (ngẫu nhiên 20 câu, có đoạn) | 26 | 54 | 54 |
| `POST /web/exam-schedules/{id}/attempts` (bắt đầu, snapshot v7 + media) | 87 | 101 | 109 |
| `GET /web/attempts/mine` | 22 | 38 | 38 |
| `GET /web/admin/media/orphans` | 34 | 38 | 40 |
| `GET /web/questions?subjectId=` (500 câu) | 111 | 137 | 138 |
| `GET /web/exams/{id}/question-options` | 45 | 71 | 73 |

Lần đo thứ hai (sau khi sửa theo review, cùng máy): mọi endpoint vẫn PASS, p95 cao nhất 264 ms (`report.xlsx`); số liệu dao động khoảng 20–40 % giữa hai lần chạy.

12 trong 21 mã đề ngẫu nhiên có ít nhất một đoạn, nên đường chọn theo đơn vị (đoạn) đã được đo thực tế.

## Nhận xét
- **Endpoint nặng nhất:** báo cáo lớp và `report.xlsx`, khoảng 0,2 s. Một lớp có 40 học viên, 10 lịch, 400 lượt và 8 000 outcome. Thời gian chủ yếu nằm ở việc đọc outcome và snapshot của các lượt được tính.
  - Giới hạn cứng hiện có: 500 học viên, 200 lịch (`CLASS_TOO_LARGE`).
  - Ở quy mô lớn hơn khoảng 10 lần, nên đo lại trước khi kết luận.
- **Danh sách câu hỏi không phân trang:** `GET /web/questions` trả đủ 500 câu trong khoảng 0,14 s. Khi ngân hàng lên hàng nghìn câu, nên thêm phân trang. Việc này đã được ghi vào nợ kỹ thuật (`05-tech-debt.md`, L7).
- **Phạm vi đo:**
  - Chỉ đo tuần tự, một người dùng mỗi lần; chưa đo đồng thời nhiều học viên bắt đầu thi cùng lúc.
  - Muốn kiểm tải đồng thời cần công cụ riêng, chẳng hạn k6. Phần này ngoài phạm vi đợt này.
