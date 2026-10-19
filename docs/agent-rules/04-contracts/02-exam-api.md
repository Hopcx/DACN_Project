# API thi — contract hiện hành và phụ lục lịch sử

## Task11: xem lại bài và hủy kết quả — 2026-10-19

`GET /web/attempts/{id}/review` yêu cầu policy `Student`, JWT của chủ lượt và membership đã duyệt hiện tại. Chỉ lượt DACN mới đã chốt (`submitted` hoặc `timedOut`), chưa hủy, có snapshot `AllowViewResult=true` và lịch có `IsTimeUtc=true`, `serverNowUtc >= ExamSchedule.EndTime` được xem. Người khác nhận 404 `ATTEMPT_NOT_FOUND`; lượt đang làm nhận 409 `ATTEMPT_NOT_FINALIZED`; kết quả ẩn, lịch chưa kết thúc hoặc đã hủy nhận 403 lần lượt `RESULT_NOT_RELEASED`, `REVIEW_NOT_RELEASED`, `ATTEMPT_VOIDED`. Response chỉ khi đủ điều kiện: `attemptId`, `state`, `reviewedFrom=attemptSnapshot`, `questions` gồm `id`, `questionTypeId`, `content`, `selectedAnswerIds`, `options[{id,content,isCorrect}]`. Câu hỏi, lựa chọn và đáp án đúng lấy từ snapshot/đáp án đã nộp của chính lượt; không đọc ngân hàng câu hỏi hiện tại. AdminManagement không có route xem bài cá nhân theo quyền admin; policy `Student` chặn route này. `GET /web/attempts/{id}` vẫn chỉ trả điểm/tình trạng đạt theo `AllowViewResult` ngay sau chốt, nhưng `questions=[]`, `savedAnswers=[]` và không trả `isCorrect` trước khi lịch kết thúc. Học viên phải gọi route review riêng.

`POST /web/admin/attempts/{id}/void` yêu cầu `AdminManagement` (level 1), JSON `{ "reason": "..." }` dài 1..1000 ký tự sau trim. Chỉ lượt đã chốt và có submission được hủy; inProgress nhận 409 `ATTEMPT_NOT_FINALIZED`. API ghi `VoidedAtUtc` và `VoidedByUserId` từ server/JWT, `VoidReason`, giữ nguyên snapshot, bản nháp, điểm, `IsPassed` và đáp án đã chấm để audit. Lặp cùng lý do trả 200 với audit cũ, không ghi sự kiện thứ hai; lặp với lý do khác trả 409 `ATTEMPT_ALREADY_VOIDED`. Học viên thấy `state=voided`, `result=null` trong detail/list, không xem review; API không gửi điểm/đạt/đáp án đúng cho lượt hủy. Admin report vẫn thấy điểm gốc và audit theo quyền.

`GET /web/attempts/mine` nhận thêm `state=inProgress|submitted|timedOut|voided`. `GET /web/admin/attempt-reports` nhận cùng bộ lọc state; row đã hủy có `state=voided`, `originalState`, `voidedAt`, `voidedByUserId`, `voidReason`. `totalAttempts` vẫn gồm lượt hủy; `voidedCount` đếm riêng. `finalizedCount`, `passedCount`, `failedCount`, `passPercent`, `averageScore`, `distribution`, `timeoutCount`, `hiddenResultCount` chỉ tính submission chưa hủy; inProgress không vào mẫu số. Timeout và AllowViewResult=false chưa hủy vẫn được tính. Báo cáo chỉ lấy lượt DACN mới, không gộp bảng lịch sử.

**Quyết định nghiệp vụ:** lượt đã hủy **vẫn tính** trong giới hạn `NumberOfRepeat` (tổng lượt start thành công). Hủy không hoàn lượt; nếu sau này cần hoàn lượt, đó là use case quản trị riêng có quyền, lý do, actor, giờ server và audit. Code Testify `CheckNumberOfSubmit` cũng đếm mọi Submission, không lọc `Status`. Migration `20261019090000_Task11AttemptVoidAudit` thêm ba cột nullable cùng FK actor `Users`, giữ các hàng trước đó. Đã áp trên DB bản sao thử nghiệm; DB gốc chưa áp.

**Task12:** DB nguồn được người dùng xác nhận là `ProjectDACN`, nhưng preflight chỉ đọc có 0 Exam/DoingExam/Submission; không có lượt Testify thực tế để ETL hoặc đối soát điểm. API attempt history/report vẫn chỉ gồm `source=dacnAttempt` mới; route projection legacy riêng ở dưới chỉ nhận hàng có provenance đã nhập và không union ngầm. Không dựng snapshot giả, không gắn UTC cho DateTime không offset, không tính lại `TotalMark`/`IsPassed` đã lưu. Xem [báo cáo Task12](../05-migration/14-task12-testify-reconciliation.md).

## Task11/Task13: lịch sử học viên và báo cáo lượt DACN mới — 2026-10-18

`GET /web/attempts/mine?page=1&pageSize=10` yêu cầu policy `Student`; identity lấy từ JWT. `page` thuộc 1..100000, `pageSize` thuộc 1..100; lỗi 400 `INVALID_PAGING`. Trả `scope=dacnAttemptsOnly`, `page`, `pageSize`, `totalCount`, `items` xếp theo `startedAt` giảm dần. Mỗi item có `attemptId`, `scheduleId`, `examId`, tên bài/lịch, `state`, `pendingFinalization`, `startedAt`, `deadline`, `finishedAt`, `submittedAt`, `result`. Thời gian UTC có `Z`. `result={totalMark,isPassed,timeTakenSeconds}` chỉ khi snapshot `AllowViewResult=true` và đã có submission; trường hợp ẩn là `null`, không gửi điểm, đạt/trượt hay đáp án. GET detail vẫn kiểm owner và membership hiện tại; bản ghi của người khác trả 404. List chỉ lấy lượt có `UserId` từ JWT; hàng `DoingExam`/`Submission` lịch sử chưa đối soát không nằm trong list và không có snapshot giả. Lượt quá hạn chưa được worker chốt hiển thị `pendingFinalization=true` và không có điểm; FE cho tải lại.

`GET /web/admin/attempt-reports` và `/options` yêu cầu policy `AdminManagement` (level 1). Báo cáo chỉ truy vấn `ExamAttempts` DACN mới, không union CRUD `Submissions` lịch sử; `scope=dacnAttemptsOnly`. Query: `page` 1..100000, `pageSize` 1..100, `examId`, `classId`, `subjectId` dương hoặc bỏ trống; lỗi 400 `INVALID_FILTER`. `/options` trả các bài thi, lớp và môn có lượt DACN mới. Bộ lọc lớp nghĩa là lịch của lượt được gán vào lớp (`classFilterMeaning=scheduleAssignedToClass`); một lượt được gán nhiều lớp vẫn chỉ đếm **một lần trong mỗi truy vấn**. Đây không phải lớp mà học viên đã dùng để dự thi, vì attempt hiện không chụp class ID.

`totalAttempts` gồm mọi lượt mới trong bộ lọc. `inProgressCount` đếm state `inProgress`. Tập `finalized` gồm state `submitted` hoặc `timedOut` **có submission**; không tính lượt đang làm hoặc trạng thái khác/hủy. `passedCount` dựa trên `IsPassed` chụp khi chấm; `failedCount=finalizedCount-passedCount`; `passPercent=100*passedCount/finalizedCount` làm tròn 2 chữ số `ToEven`, bằng 0 khi mẫu số 0. `averageScore` tính trên `TotalMark` đã lưu của tập finalized, làm tròn 2 chữ số `ToEven`. `distribution` nhóm theo điểm lưu chính xác `decimal(18,2)` và đếm lượt. Theo quyết định nghiệp vụ, **timeout và kết quả bị ẩn vẫn vào mẫu số, trung bình và phổ điểm của admin**; `timeoutCount` và `hiddenResultCount` là các tập con, không trừ ra. Response còn có `totalCount`, `summary`, `distribution`, `items` phân trang. Admin thấy điểm của lượt mới theo policy hiện tại; API học viên vẫn che theo `AllowViewResult`. Mỗi row có `source=dacnAttempt` để không lẫn bảng lịch sử.

Chưa xác minh mapping `DoingExam`/`Submission` cũ; không cộng các hàng đó vào chỉ số. Báo cáo theo lớp phản ánh gán lịch hiện tại; lịch có lượt đã thi không cho sửa gán lớp theo Task9. Số liệu báo cáo là ảnh chụp tại lúc request, các câu SQL có thể thấy worker chốt lượt giữa hai câu truy vấn; nếu cần snapshot hoàn toàn đồng thời cho tải cao, cần transaction read snapshot riêng.

## Task10 đã triển khai trên DB bản sao — 2026-10-18

**Quyết định điểm cho lượt DACN mới:** tính từ snapshot bằng `decimal`, cộng trước rồi làm tròn tổng một lần tới hai chữ số `MidpointRounding.ToEven`; so đạt với tổng **trước làm tròn**. Do đó raw `1.005`, PassMark `1.004` → lưu/hiển thị `1.00`, `isPassed=true`; raw `1.015`, PassMark `1.016` → lưu/hiển thị `1.02`, `isPassed=false`. Trạng thái đạt có thể trông khác điểm hai chữ số ở sát ngưỡng; FE hiển thị cả hai giá trị server trả và không tự tính lại. Testify dùng `double`: `Math.Round(1.015d,2)=1.01`, còn DACN dùng `decimal` cho `1.02`. Giữ điểm Testify lịch sử, không chấm lại; Task12 liệt kê sai khác số học khi đối soát, không xem là mất dữ liệu.

**Ranh giới deadline:** điều kiện server là `serverNowUtc >= DeadlineUtc`. Một tick trước deadline còn nhận submit; đúng deadline và sau deadline chốt `timedOut` từ nháp đã lưu. Mốc `serverNowUtc` được lấy sau khi request có SQL application lock, nên request gửi trước hạn nhưng phải chờ khóa qua deadline cũng bị timeout. Worker và request dùng chung finalizer, chỉ có một submission. Test đồng hồ cô lập đã kiểm ba mốc chính xác; SQL barrier kiểm request gửi trước hạn nhưng xử lý sau hạn, không khẳng định hai request đến cùng tick.

**Chính sách xem kết quả:** khi snapshot `AllowViewResult=false`, API trả `result=null`, `questions=[]`, `savedAnswers=[]` sau finalize; không trả điểm, đạt/trượt, thời lượng, đáp án đúng hoặc khóa chấm trong object lồng. FE hiển thị trạng thái và thông báo chưa được xem kết quả trên trang sau nộp, liên kết lịch, URL trực tiếp và sau reload. Phiên đăng nhập được khôi phục qua refresh cookie HttpOnly; các lần khôi phục đồng thời trên FE dùng chung một promise.

Các route **đã chạy** (policy `Student`, JWT identity; envelope `ApiResponse`): `POST /web/exam-schedules/{id}/attempts`, `GET /web/attempts/{guid}`, `PUT /web/attempts/{guid}/answers`, `POST /web/attempts/{guid}/submit`. Route review hiện hành ở mục Task11 trên. GET/PUT/POST theo attempt ID của người khác trả 404. Response của lượt đang mở gồm `attemptId`, `scheduleId`, `state`, `serverNow`, `deadline` UTC Z, `revision`, `questions` an toàn (`id`, `questionTypeId`, `content`, `options` chỉ có `id`/`content`), `savedAnswers`; không có source ID, điểm từng câu hay cờ đúng. Kết thúc trả `questions=[]`, `savedAnswers=[]`; `result={totalMark,isPassed,submittedAt,timeTakenSeconds}` chỉ khi `AllowViewResult` được chụp là true tại lúc start, ngược lại null.

`NumberOfRepeat` trong Testify được gán vào `limitofsubmit` và chặn khi số lượt đã làm `>= NumberOfRepeat`; UI gọi là “Số lần làm bài”. Vì vậy đây là **tổng lượt tối đa**, không cộng thêm một. Một lượt tính khi transaction start đã commit. Request start lặp trả đúng lượt đang mở; SQL application lock `DACN:schedule-write` và unique filtered index `(UserId,ExamScheduleId) WHERE State='inProgress'` chặn race. Chỉ start khi Student có membership đã duyệt trong lớp hoạt động, exam/schedule public, `IsTimeUtc=1`, server UTC thuộc `[start,end)`, mã đề hợp lệ. Hàng legacy `DoingExam`/`Submission` cùng user/lịch chặn start với `LEGACY_ATTEMPT_RECONCILIATION_REQUIRED` vì chưa có liên kết đáng tin cậy để đếm lượt.

Timeout tự động: `ExpiredAttemptFinalizerService` quét ngay khi API khởi động rồi mỗi 10 giây; chỉ lấy lượt `inProgress` với `DeadlineUtc <= server UTC`. Mỗi lượt được đọc lại và chấm trong transaction riêng sau SQL application lock `DACN:schedule-write`; lỗi của một lượt được log theo attempt ID và các lượt còn lại vẫn được thử. Sau khởi động lại, lượt chưa chốt được quét tiếp; lượt đã chốt không bị chấm lại. Không giữ transaction hoặc khóa trong lúc chờ kỳ quét. `POST submit` và worker dùng cùng `AttemptFinalizer`, cùng snapshot/draft và PK duy nhất của submission. Trước deadline, nộp thủ công đặt `state=submitted`, `submittedAt=serverNow`; từ deadline trở đi, kể cả request nộp thủ công, đặt `state=timedOut`, `submittedAt=DeadlineUtc`. `FinishedAtUtc` là giờ UTC server thực sự chốt; `TimeTakenSeconds=floor(max(0,submittedAt-startedAt))` và nằm trong kết quả được phép xem. Chỉ chấm bản nháp cuối cùng đã lưu; lựa chọn còn ở FE chưa lưu không được tính. Client không cung cấp giờ, trạng thái, điểm hoặc thời lượng.

Chấm điểm từ snapshot bất biến: câu chọn một/chọn nhiều chỉ được trọn điểm khi tập lựa chọn đúng chính xác; câu bỏ trống, chọn thiếu hoặc chọn thừa được 0, không có điểm từng phần. Cộng các điểm nguồn trong kiểu decimal, so `PassMark` chụp lúc start với tổng **chưa làm tròn**, rồi làm tròn tổng một lần đến 2 chữ số `MidpointRounding.ToEven` để lưu SQL `decimal(18,2)` và trả JSON. `PassMark` snapshot giữ độ chính xác của cột nguồn SQL `float` để không đổi ngưỡng 1.004 thành 1.00; cấu hình điểm/PassMark không lưu được an toàn bị từ chối ở start bằng `EXAM_CONFIGURATION_INVALID`. `ScoreMethodId` khác null tiếp tục bị từ chối. Sai khác đã đo trên .NET: Testify `Math.Round(1.015d,2)=1.01` còn DACN `decimal.Round(1.015m,2,ToEven)=1.02`; cần quyết định đối soát trước khi coi điểm lịch sử là tương đương.

Deadline cố định là `min(serverStart + Exam.Duration phút, ExamSchedule.EndTime)`; `datetime2` đọc lại gắn UTC trước JSON. PUT answers thay toàn bộ draft, cần `revision` hiện hành. ID câu/đáp án không thuộc snapshot, ID trùng hoặc nhiều lựa chọn cho loại 1/2 trả 400 `INVALID_SELECTION`; revision cũ trả 409 `CONCURRENCY_CONFLICT`. Các ghi được tuần tự hóa bằng cùng SQL application lock; request sau không ghi đè revision trước. Nộp chỉ chấm draft đã lưu; FE lưu trước rồi nộp. Nộp lặp trả kết quả đã lưu, một hàng `ExamAttemptSubmissions` có PK/FK duy nhất trên attempt. Nếu request tới khi `serverNow >= deadline`, server chấm draft đã lưu, đặt `timedOut`, từ chối đáp án mới. GET attempt cũng hoàn tất quá hạn. Điểm lấy snapshot: mỗi câu đạt trọn điểm khi tập lựa chọn bằng chính xác tập đáp án đúng; tổng làm tròn 2 chữ số kiểu `MidpointRounding.ToEven` theo contract DACN (Testify dùng `double`, nên một số biên nhị phân như 1.015 có thể khác); đạt khi tổng **trước làm tròn** >= PassMark chụp lúc start. Không nhận UserId, điểm, deadline, trạng thái hoặc thời gian nộp từ client.

Các lỗi hiện có: 403 không có membership/quyền; 404 không tồn tại hoặc không sở hữu attempt; 409 `SCHEDULE_TIME_UNKNOWN`, `EXAM_NOT_OPEN`, `NO_PUBLISHED_VARIANT`, `EXAM_CONFIGURATION_INVALID`, `ATTEMPT_LIMIT_REACHED`, `LEGACY_ATTEMPT_RECONCILIATION_REQUIRED`, `CONCURRENCY_CONFLICT`, `ATTEMPT_CLOSED`; 400 `INVALID_SELECTION`. Các phần sketch bên dưới như `GET /web/submissions/{id}/review`, `finalAnswers`, activity events và Idempotency-Key **chưa triển khai**. Tính idempotent của start/submit dựa trên trạng thái attempt và khóa SQL, không dùng header key.

GET/save/submit còn kiểm membership đã duyệt hiện tại; thu hồi membership khiến các route attempt đó trả 404. Start từ exam có `ScoreMethodId` khác null bị chặn `EXAM_CONFIGURATION_INVALID` cho đến khi có thuật toán chấm tương ứng, tránh chấm ngầm bằng quy tắc mặc định.

Nếu request start gặp lượt cũ đang mở nhưng đã qua deadline, server finalize timeout và trả chính lượt đó sau khi commit. Request start kế tiếp mới xét tạo lượt mới nếu lịch còn mở và còn quota.

## Projection lịch sử tách biệt — fixture kỹ thuật Task12, 2026-10-19

Migration `20261019090000_Task12LegacyProjection` tạo `LegacyImportBatches`, `LegacyUserMaps`, `LegacySubmissionRecords`, `LegacyAnswerSelections`. Khóa provenance gồm hệ thống nguồn, database nguồn, migration nguồn, marker/batch, bảng và legacy ID; cùng marker chỉ được nhập một batch, unique source ID trong batch. Import giữ `Submissions.TotalMark` kiểu SQL `float`, `IsPassed`, `Status bit?`, `SubmitTime datetime2` và `TimeTaken time` nguyên bản; `Status=false` không được diễn giải thành lượt hủy DACN mới. Timestamp không offset trả `submittedAtLocalUnknown` dạng chuỗi không có `Z` và `timeZoneStatus="unknown"`. Không chèn vào `ExamAttempts`, không tạo snapshot hoặc chấm lại. Hai user nguồn được map rõ sang hai user đích có quyền Student; hash/password nguồn không nhập.

| Route hiện hành | Quyền và dữ liệu |
|---|---|
| `GET /web/legacy-submissions/mine?page=&pageSize=` | Student từ JWT; chỉ projection có `TargetUserId` khớp, phân trang. `scope=legacySubmissionsOnly`, `source=legacySubmission`, `legacyStatus` giữ true/false/null. `result` chỉ có điểm/đạt khi `AllowViewResult=true`. Không trả đáp án đúng. |
| `GET /web/legacy-submissions/{id}` | Student chủ sở hữu; ID người khác 404. Cùng allowlist như list, `hasSnapshot=false`, `canReviewAnswers=false`. |
| `GET /web/legacy-submissions/{id}/review` | Chủ sở hữu trả 409 `LEGACY_SNAPSHOT_UNAVAILABLE`; người khác 404. Không dựng lại đáp án từ câu hỏi hiện tại. |
| `GET /web/admin/legacy-submission-reports?page=&pageSize=&status=` | `AdminManagement`; `scope=legacySubmissionsOnly`, summary tổng số, số `Status=true/false/null`, tổng `TotalMark` lưu gốc; items phân trang. `status=accepted/notRecognized/pending` lọc ba trạng thái riêng, giá trị khác trả 400 `INVALID_LEGACY_STATUS`. Không union với `/web/admin/attempt-reports` (`dacnAttemptsOnly`). |

Các route trên hỗ trợ projection có provenance đã kiểm; fixture 4 bài nộp chỉ là dữ liệu giả, không chứng minh parity Testify. API cho lượt DACN mới và quota `NumberOfRepeat` không thay đổi. FE đã nối danh sách legacy riêng trong lịch sử học viên và báo cáo admin với lọc `Status`, nhưng chưa kiểm trên trình duyệt; đối soát nguồn Testify thật chưa nghiệm thu.

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
