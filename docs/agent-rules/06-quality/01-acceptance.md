# Definition of Done và kiểm thử

## Cổng nghiệm thu bổ sung từ audit v0.2

- DI activation: IADO đọc cấu hình đúng và bảy service thiếu được resolve.
- Auth integration: FE gọi đúng /web/auth/login, unwrap ApiResponse, gắn JWT và phân biệt 401/403.
- Security contracts: mọi public User DTO không có PasswordHash; student payload không có IsCorrect; create-user không cho tự cấp role đặc quyền.
- Room paging: page bounds, deterministic sort; chưa được gọi là mẫu CRUD đã chạy trước khi test.
- Giữ hiện trạng JS: script npm lint/build có thật; chưa có typecheck/test script. Thêm test infrastructure là nhiệm vụ khi triển khai, không ghi “test pass” ở phiên tài liệu.
- Kiểm relation Exam–Schedule, TimeOnly duration và các Exam fields thiếu DTO trước parity engine thi.

Đây là yêu cầu kiểm thử cho triển khai tương lai. Chưa chạy các bài test sản phẩm trong phiên tạo tài liệu này.

## Mỗi feature

- Có feature ID, nguồn cũ và contract nhất quán FE–BE.
- Authorization được kiểm ở API/use case, bao gồm tài nguyên của người khác.
- DB migration/config/index liên quan được review; không mất dữ liệu ngoài yêu cầu.
- FE đủ loading, empty, error, validation, disabled và retry theo ngữ nghĩa.
- Có kiểm thử trực tiếp các invariant thay đổi; không coi mock repository là bằng chứng SQL đúng.
- Đã cập nhật docs/parity và ghi sai khác so với legacy.

## Bộ kiểm thử theo rủi ro

| Nhóm | Test cần có |
|---|---|
| Domain scoring | 3 loại câu; chọn thiếu/thừa/trùng; không chọn; ngưỡng đạt; rounding đã chốt |
| Auth | Login sai/đúng, refresh reuse, logout, thay role/ownership, payload giả userId |
| API | ApiResponse/validation error adapter hiện tại, contract lỗi mục tiêu khi được bổ sung, schema DTO, student payload không có đáp án đúng, paging/sort allowlist |
| SQL | Unique/FK, transaction rollback, rowversion, concurrency start/submit, migrations từ baseline |
| FE validators | Number locale/null/range; date-only/instant; cross-field |
| End-to-end | Tạo câu → mã đề → lịch/lớp → thi → reload → nộp → kết quả → báo cáo |
| Resilience | Commit xong mất response; submit retry; save out-of-order; worker timeout restart |
| UI | Keyboard/focus, narrow viewport, long content, offline, zoom |

Integration test dùng SQL Server đại diện cho provider production; EF InMemory/SQLite không chứng minh constraint/concurrency/provider SQL Server. Fixture synthetic, không credentials hoặc dữ liệu học sinh thật.

## Test đặc biệt của conversion

Câu multiple-choice có hai đáp án đúng: chọn một đáp án đúng phải theo quy tắc mới đã duyệt (baseline 0 điểm), không thừa hưởng nhánh one-selected cũ. Lịch đang diễn ra phải thỏa start <= now < end theo baseline; kiểm đúng các biên start/end.

AllowViewResult=false phải bị chặn cả review endpoint trực tiếp. Sửa question sau khi đã nộp không đổi kết quả/history. Báo cáo không nhân bản bài nộp khi student có nhiều membership.

Không đặt phần trăm coverage hoặc SLA tùy ý. Tải test và mục tiêu p95 dựa số thí sinh đồng thời/môi trường được chủ dự án cung cấp.

## Cổng nghiệm thu To-be (T1–T18, bổ sung 25/11/2026)
- **Không lộ đáp án:** mọi DTO làm bài (thi, luyện tập, thi thử) được kiểm bằng smoke để không chứa `isCorrect`, `matchId`, `matchContent`, `acceptedAnswers`, `correctItems`, `sourceAnswerId`. Đáp án chỉ có ở trang xem lại đã được mở, hoặc trong kết quả kiểm tra của câu đã chốt.
- **Snapshot:** bài làm chấm và xem lại hoàn toàn theo snapshot lúc bắt đầu. Đổi phiên bản snapshot phải giữ đọc được mọi phiên bản cũ, và có unit test cho phiên bản cũ ngay trước nó.
- **Một hàm chấm:** điểm, kiểm tra từng câu, outcome, báo cáo và xem lại dùng chung `AnswerGrading`. Thêm loại câu mới phải sửa ở một chỗ và có test cho cả 3 hàm chấm.
- **Migration:** chỉ thêm (bảng, cột, seed có chặn). Down bị chặn khi đã có dữ liệu của tính năng. Mỗi migration phải qua `CutoverRehearsal.ps1` từ bản sao ProjectDACN.
- **Tài liệu:** route mới của controller To-be phải có dòng trong `04-contracts/07-tobe-endpoints.md`. Smoke `TOBE_ENDPOINT_DOC` kiểm cả hai chiều. Smoke này **không** nằm trong `RunUatSuite.ps1`; chạy riêng trước khi nghiệm thu: `dotnet run --project tests/DependencyInjectionSmoke` (không cần SQL/API).
- **Hồi quy:** `RunUatSuite.ps1` đầy đủ PASS trước khi merge vào `developer`. Mỗi lát được review độc lập, các mục Cao/Trung bình phải sửa trước khi commit.
- **Hiệu năng:** endpoint nặng (xếp hạng, báo cáo lớp, phân tích, tạo phiên/mã đề) có p95 < 2 giây với bộ dữ liệu tải thử (`06-performance.md`).
