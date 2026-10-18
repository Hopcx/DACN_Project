# API thi — contract hiện hành và phụ lục lịch sử

## Task10 đã triển khai trên DB bản sao — 2026-10-18

**Quyết định điểm cho lượt DACN mới:** tính từ snapshot bằng `decimal`, cộng trước rồi làm tròn tổng một lần tới hai chữ số `MidpointRounding.ToEven`; so đạt với tổng **trước làm tròn**. Do đó raw `1.005`, PassMark `1.004` → lưu/hiển thị `1.00`, `isPassed=true`; raw `1.015`, PassMark `1.016` → lưu/hiển thị `1.02`, `isPassed=false`. Trạng thái đạt có thể trông khác điểm hai chữ số ở sát ngưỡng; FE hiển thị cả hai giá trị server trả và không tự tính lại. Testify dùng `double`: `Math.Round(1.015d,2)=1.01`, còn DACN dùng `decimal` cho `1.02`. Giữ điểm Testify lịch sử, không chấm lại; Task12 liệt kê sai khác số học khi đối soát, không xem là mất dữ liệu.

**Ranh giới deadline:** điều kiện server là `serverNowUtc >= DeadlineUtc`. Một tick trước deadline còn nhận submit; đúng deadline và sau deadline chốt `timedOut` từ nháp đã lưu. Mốc `serverNowUtc` được lấy sau khi request có SQL application lock, nên request gửi trước hạn nhưng phải chờ khóa qua deadline cũng bị timeout. Worker và request dùng chung finalizer, chỉ có một submission. Test đồng hồ cô lập đã kiểm ba mốc chính xác; SQL barrier kiểm request gửi trước hạn nhưng xử lý sau hạn, không khẳng định hai request đến cùng tick.

**Chính sách xem kết quả:** khi snapshot `AllowViewResult=false`, API trả `result=null`, `questions=[]`, `savedAnswers=[]` sau finalize; không trả điểm, đạt/trượt, thời lượng, đáp án đúng hoặc khóa chấm trong object lồng. FE hiển thị trạng thái và thông báo chưa được xem kết quả trên trang sau nộp, liên kết lịch, URL trực tiếp và sau reload. Phiên đăng nhập được khôi phục qua refresh cookie HttpOnly; các lần khôi phục đồng thời trên FE dùng chung một promise.

Các route **đã chạy** (policy `Student`, JWT identity; envelope `ApiResponse`): `POST /web/exam-schedules/{id}/attempts`, `GET /web/attempts/{guid}`, `PUT /web/attempts/{guid}/answers`, `POST /web/attempts/{guid}/submit`. Không có route review đáp án. GET/PUT/POST theo attempt ID của người khác trả 404. Response của lượt đang mở gồm `attemptId`, `scheduleId`, `state`, `serverNow`, `deadline` UTC Z, `revision`, `questions` an toàn (`id`, `questionTypeId`, `content`, `options` chỉ có `id`/`content`), `savedAnswers`; không có source ID, điểm từng câu hay cờ đúng. Kết thúc trả `questions=[]`, `savedAnswers=[]`; `result={totalMark,isPassed,submittedAt,timeTakenSeconds}` chỉ khi `AllowViewResult` được chụp là true tại lúc start, ngược lại null.

`NumberOfRepeat` trong Testify được gán vào `limitofsubmit` và chặn khi số lượt đã làm `>= NumberOfRepeat`; UI gọi là “Số lần làm bài”. Vì vậy đây là **tổng lượt tối đa**, không cộng thêm một. Một lượt tính khi transaction start đã commit. Request start lặp trả đúng lượt đang mở; SQL application lock `DACN:schedule-write` và unique filtered index `(UserId,ExamScheduleId) WHERE State='inProgress'` chặn race. Chỉ start khi Student có membership đã duyệt trong lớp hoạt động, exam/schedule public, `IsTimeUtc=1`, server UTC thuộc `[start,end)`, mã đề hợp lệ. Hàng legacy `DoingExam`/`Submission` cùng user/lịch chặn start với `LEGACY_ATTEMPT_RECONCILIATION_REQUIRED` vì chưa có liên kết đáng tin cậy để đếm lượt.

Timeout tự động: `ExpiredAttemptFinalizerService` quét ngay khi API khởi động rồi mỗi 10 giây; chỉ lấy lượt `inProgress` với `DeadlineUtc <= server UTC`. Mỗi lượt được đọc lại và chấm trong transaction riêng sau SQL application lock `DACN:schedule-write`; lỗi của một lượt được log theo attempt ID và các lượt còn lại vẫn được thử. Sau khởi động lại, lượt chưa chốt được quét tiếp; lượt đã chốt không bị chấm lại. Không giữ transaction hoặc khóa trong lúc chờ kỳ quét. `POST submit` và worker dùng cùng `AttemptFinalizer`, cùng snapshot/draft và PK duy nhất của submission. Trước deadline, nộp thủ công đặt `state=submitted`, `submittedAt=serverNow`; từ deadline trở đi, kể cả request nộp thủ công, đặt `state=timedOut`, `submittedAt=DeadlineUtc`. `FinishedAtUtc` là giờ UTC server thực sự chốt; `TimeTakenSeconds=floor(max(0,submittedAt-startedAt))` và nằm trong kết quả được phép xem. Chỉ chấm bản nháp cuối cùng đã lưu; lựa chọn còn ở FE chưa lưu không được tính. Client không cung cấp giờ, trạng thái, điểm hoặc thời lượng.

Chấm điểm từ snapshot bất biến: câu chọn một/chọn nhiều chỉ được trọn điểm khi tập lựa chọn đúng chính xác; câu bỏ trống, chọn thiếu hoặc chọn thừa được 0, không có điểm từng phần. Cộng các điểm nguồn trong kiểu decimal, so `PassMark` chụp lúc start với tổng **chưa làm tròn**, rồi làm tròn tổng một lần đến 2 chữ số `MidpointRounding.ToEven` để lưu SQL `decimal(18,2)` và trả JSON. `PassMark` snapshot giữ độ chính xác của cột nguồn SQL `float` để không đổi ngưỡng 1.004 thành 1.00; cấu hình điểm/PassMark không lưu được an toàn bị từ chối ở start bằng `EXAM_CONFIGURATION_INVALID`. `ScoreMethodId` khác null tiếp tục bị từ chối. Sai khác đã đo trên .NET: Testify `Math.Round(1.015d,2)=1.01` còn DACN `decimal.Round(1.015m,2,ToEven)=1.02`; cần quyết định đối soát trước khi coi điểm lịch sử là tương đương.

Deadline cố định là `min(serverStart + Exam.Duration phút, ExamSchedule.EndTime)`; `datetime2` đọc lại gắn UTC trước JSON. PUT answers thay toàn bộ draft, cần `revision` hiện hành. ID câu/đáp án không thuộc snapshot, ID trùng hoặc nhiều lựa chọn cho loại 1/2 trả 400 `INVALID_SELECTION`; revision cũ trả 409 `CONCURRENCY_CONFLICT`. Các ghi được tuần tự hóa bằng cùng SQL application lock; request sau không ghi đè revision trước. Nộp chỉ chấm draft đã lưu; FE lưu trước rồi nộp. Nộp lặp trả kết quả đã lưu, một hàng `ExamAttemptSubmissions` có PK/FK duy nhất trên attempt. Nếu request tới khi `serverNow >= deadline`, server chấm draft đã lưu, đặt `timedOut`, từ chối đáp án mới. GET attempt cũng hoàn tất quá hạn. Điểm lấy snapshot: mỗi câu đạt trọn điểm khi tập lựa chọn bằng chính xác tập đáp án đúng; tổng làm tròn 2 chữ số kiểu `MidpointRounding.ToEven` theo contract DACN (Testify dùng `double`, nên một số biên nhị phân như 1.015 có thể khác); đạt khi tổng **trước làm tròn** >= PassMark chụp lúc start. Không nhận UserId, điểm, deadline, trạng thái hoặc thời gian nộp từ client.

Các lỗi hiện có: 403 không có membership/quyền; 404 không tồn tại hoặc không sở hữu attempt; 409 `SCHEDULE_TIME_UNKNOWN`, `EXAM_NOT_OPEN`, `NO_PUBLISHED_VARIANT`, `EXAM_CONFIGURATION_INVALID`, `ATTEMPT_LIMIT_REACHED`, `LEGACY_ATTEMPT_RECONCILIATION_REQUIRED`, `CONCURRENCY_CONFLICT`, `ATTEMPT_CLOSED`; 400 `INVALID_SELECTION`. Các phần sketch bên dưới như `GET /web/submissions/{id}/review`, `finalAnswers`, activity events và Idempotency-Key **chưa triển khai**. Tính idempotent của start/submit dựa trên trạng thái attempt và khóa SQL, không dùng header key.

GET/save/submit còn kiểm membership đã duyệt hiện tại; thu hồi membership khiến các route attempt đó trả 404. Start từ exam có `ScoreMethodId` khác null bị chặn `EXAM_CONFIGURATION_INVALID` cho đến khi có thuật toán chấm tương ứng, tránh chấm ngầm bằng quy tắc mặc định.

Nếu request start gặp lượt cũ đang mở nhưng đã qua deadline, server finalize timeout và trả chính lượt đó sau khi commit. Request start kế tiếp mới xét tạo lượt mới nếu lịch còn mở và còn quota.

## Ghi chú trước triển khai (lịch sử, không phải contract hiện hành)

Trạng thái 2026-10-11: đã đọc `VibeCoding/Task10.md`; file hiện có và yêu cầu start/resume/save, snapshot, revision, deadline server và trang làm bài. Các route attempt dưới đây vẫn chưa được triển khai. Phần đã chạy thực tế là `GET /web/student/schedules` bổ sung `serverNow`/`scheduleState`, xem `01-http.md`. Cách tính/phạm vi số lượt và deadline O05/O06 đang chờ chốt; rounding/chấm điểm thuộc phần finalize, không chặn việc chuẩn bị snapshot và draft. Không dùng trạng thái `open` của lịch làm xác nhận đủ điều kiện start. Route Submission hiện chỉ dành AdminManagement và các thao tác ghi là NonAction; kết quả cho Student cần route owner riêng khi triển khai engine.

Đã thêm code thuần `AttemptSnapshotFactory` chuẩn bị snapshot từ mã đề công khai, chiếu DTO học viên theo allowlist và validate draft thay toàn bộ. ID câu/options trong snapshot là Guid; DTO không có SourceQuestionId/SourceAnswerId/Point/IsCorrect. Snapshot phía server giữ nguồn và cờ đúng để phục vụ chấm sau này; điểm hiện giữ double của source, chưa chốt rounding hay chuyển dữ liệu cũ. Chưa nối persistence/controller, chưa có route để FE gọi; kiểm cô lập không thay cho nghiệm thu SQL/concurrency.

Các route/field dưới đây là PROPOSED, chưa tồn tại trong BE đã đọc. Dùng prefix /web để đồng nhất với API hiện tại; không nối student submit vào /web/submissions CRUD vì endpoint đó nhận điểm từ request. Envelope ngoài cùng tiếp tục ApiResponse theo 01-http.md; JSON ví dụ bên dưới là phần data.

| Method + route | Input chính | Output / policy |
|---|---|---|
| POST /web/exam-schedules/{id}/attempts | Idempotency-Key | Attempt đang có hoặc mới; identity từ auth |
| GET /web/attempts/{id} | ID | Safe questions, savedAnswers, revision, deadline |
| PUT /web/attempts/{id}/answers | revision + answers | revision mới, savedAt; chỉ khi còn hạn |
| POST /web/attempts/{id}/submit | Idempotency-Key + revision + finalAnswers tùy chọn | Kết quả đã persisted; retry trả cùng kết quả |
| GET /web/submissions/{id} | ID | Summary theo ownership và release policy |
| GET /web/submissions/{id}/review | ID | Chi tiết theo AllowViewResult và policy riêng |
| POST /web/attempts/{id}/activity-events | eventType, clientObservedAt tùy chọn | Audit với serverReceivedAt; không tin client là chứng cứ tuyệt đối |

## Safe attempt DTO (ví dụ minh họa, không phải dữ liệu thật)

```json
{
  "attemptId": "00000000-0000-4000-8000-000000000001",
  "state": "inProgress",
  "serverNow": "2026-09-23T02:00:00Z",
  "expiresAt": "2026-09-23T02:45:00Z",
  "revision": 1,
  "questions": [{
    "id": "snapshot-q-1",
    "type": "singleChoice",
    "content": "Nội dung câu hỏi minh họa",
    "options": [{ "id": "snapshot-a-1", "content": "Đáp án minh họa" }]
  }],
  "savedAnswers": []
}
```

ID snapshot dạng chuỗi trong ví dụ là opaque identifier; format cuối cùng phải pin trong OpenAPI. ID không cho phép suy ra đáp án đúng. Không có IsCorrect, correctAnswerIds, PasswordHash hoặc navigation entity trong DTO này.

## Save/final answers

```json
{
  "revision": 1,
  "answers": [{ "questionId": "snapshot-q-1", "selectedAnswerIds": ["snapshot-a-1"] }]
}
```

PUT answers thay toàn bộ tập trả lời của attempt trong bản baseline; FE gửi snapshot draft đầy đủ để xóa lựa chọn có semantics rõ. Nếu đổi sang patch từng câu, phải đổi contract/revision policy và tests. Submit `finalAnswers` có cùng kiểu array; chỉ tiếp nhận khi server còn hạn và revision hợp lệ. Không nhận totalMark/isPassed/userId/deadline.

Sau deadline: không merge finalAnswers mới; finalize draft server đã lưu và trả trạng thái timeout rõ. Nếu revision conflict trước deadline, FE fetch state mới và cho xử lý; không tự overwrite draft của tab khác.

## Các lỗi cụ thể

403 user không có quyền; 409 ATTEMPT_LIMIT_REACHED/ATTEMPT_CLOSED/CONCURRENCY_CONFLICT; 400 ANSWER_NOT_IN_ATTEMPT/INVALID_SELECTION. EXAM_NOT_OPEN trả 409 với thông tin lịch được phép công khai. Retry do network phải giữ key và payload ban đầu, hoặc fetch attempt khi trạng thái commit chưa rõ.
