# Task09 membership và phần độc lập Task10 — 2026-10-11

## Task11 xem lại bài, hủy kết quả và nghiệm thu lại Task13 — 2026-10-19

Trước sửa: BE `dev/Hop` `8d90db7118cd9541744ab777f5b2b6e97f9a6d36`, FE `dev/Hop` `b250bd06a682614658a40ef4401a76ab06a441fa`. History/report Task11/13 đã nằm trong hai commit này; Task10 ở BE `d37f4311e0e3f26369f45349177d343dbe75dff5`, FE `56adc93b03d914a030105e97b4eb5bdc8ab2d6d3`. Giữ nguyên file người dùng có sẵn: BE `.gitmodules`, `AuthController.cs`, DOCX, `impeccable`, hai export `Gittodoc_testify.txt`; không stage/commit các file đó.

Preflight SQL chỉ đọc: `DB_NAME()=ProjectDACN_Task04Task09_Test`, 6 migration đến `20261018063239_Task10TimeoutAndDuration`, `ExamAttempts=0`, `ExamAttemptSubmissions=0`. Tạo migration `20261019090000_Task11AttemptVoidAudit`: ba cột nullable `VoidedAtUtc datetime2`, `VoidedByUserId uniqueidentifier` FK Users không cascade, `VoidReason nvarchar(1000)` trên `ExamAttemptSubmissions`. SQL Up đã review gồm ADD cột/index/FK và history insert; Down từ chối nếu còn hàng đã hủy. Đã áp **chỉ lên bản sao** sau khi kiểm DB_NAME/history. Postflight: history 7 migration đến Task11, `ExamAttempts=0`, `ExamAttemptSubmissions=0`, QA exam/report user còn lại 0. Không ghi ProjectDACN gốc hoặc Testify.

`pwsh -NoProfile -File tests/Task11Task13SqlSmoke.ps1 -Browser` PASS qua HTTPS API, Chrome/Vite và SQL Server copy. Fixture 3 user, 2 lớp, 4 lịch, 5 lượt/4 submission. Trước hủy: `total=5`, `finalized=4`, `inProgress=1`, `timeout=1`, `hidden=1`, `passed=2`, `averageScore=5`, `passPercent=50`; mỗi mức điểm 2/4/6/8 có một lượt. API Student list/paging/owner 404, hidden list/detail null; review trước cuối lịch 403, hidden 403, người khác 404, admin 403. Chrome Admin hủy lượt 8 điểm bằng lý do bắt buộc, reload báo cáo. SQL xác nhận actor/UTC/lý do và điểm gốc `8.00` còn nguyên. Hủy lặp cùng lý do 200 không đổi fingerprint audit, lý do khác 409; Student không thể hủy. Student detail/list sau hủy chỉ có `state=voided`, `result=null`, review 403. Sau hủy `total=5`, `voided=1`, `finalized=3`, `passed=1`, `averageScore=4`, `passPercent=33.33`; SQL vẫn có 4 submission, trong đó 1 đã hủy. Bộ lọc exam/class/subject/state, trang 2 dòng và trường hợp lịch gán hai lớp đều đạt.

`node tests/attemptBrowserSmoke.mjs` và `--hidden-result` PASS qua Chrome → Vite → API → SQL copy. Sau start/save/reload/submit, route review trả 403 trước giờ kết thúc lịch. Fixture sửa nội dung Question/Answer nguồn rồi đưa `EndTime` của lịch thử nghiệm về quá khứ: `AllowViewResult=true` nhận câu hỏi, lựa chọn, đáp án đúng và lựa chọn đã nộp từ snapshot gốc; FE nút “Xem lại bài” hiển thị nội dung gốc. `AllowViewResult=false` vẫn trả 403, API không gửi `isCorrect` hoặc `selectedAnswerIds`; trang/URL/reload/history không hiện điểm hay đáp án. Mỗi fixture browser có đúng một submission trước khi dọn. Script hiện dọn cả user/email QA của chính lượt chạy và kiểm `COUNT(*)=0` sau dọn; các user QA tồn từ lần chạy cũ không bị tự xóa.

`dotnet build DACN_Project.sln --no-restore` PASS (warning NU1903 AutoMapper tồn tại), FE `npm run lint`, `npm run build` PASS (cảnh báo chunk >500 kB), `git diff --check` không có lỗi whitespace. F20–F22 giữ `IN_PROGRESS`: chưa đối soát `DoingExam`/`Submission` lịch sử, chưa chứng minh race hai admin cùng hủy khác lý do, chưa nghiệm thu rollback migration có audit, và chưa xác minh chính sách hoàn lượt Testify. `NumberOfRepeat` hiện vẫn đếm lượt đã start kể cả khi kết quả bị hủy; không tự hoàn lượt. DB gốc chưa triển khai migration.

## Task10 nghiệm thu bổ sung — 2026-10-18

Checkout trước sửa: BE `dev/Hop` tại `e69b7007fdf7000ce866f81a47e0f306450a6335`, FE `dev/Hop` tại `6b7c999ef6dbfc859d307723664a0d75bbc475ad`. Giữ nguyên thay đổi sẵn có của Task10 và các file người dùng không liên quan (`.gitmodules`, `AuthController.cs`, DOCX, `impeccable`, hai export `Gittodoc_testify.txt`). Không tạo/áp migration trong lượt nghiệm thu này. Preflight SQL copy trả `DB_NAME()=ProjectDACN_Task04Task09_Test`, history 6 với migration cuối `20261018063239_Task10TimeoutAndDuration`, `ExamAttempts=0`, `ExamAttemptSubmissions=0`. API chạy với connection string chỉ rõ DB copy.

**Kết quả ẩn qua browser:** `node tests/attemptBrowserSmoke.mjs --hidden-result` đạt trên Chrome → HTTPS Vite → API → SQL copy. Fixture Exam `AllowViewResult=0` có câu một lựa chọn và đáp án đúng; học viên đăng ký/xác minh/đăng nhập qua API, start, lưu, reload bản nháp, nộp. Sau nộp, đi qua liên kết lịch sử “Xem lượt gần nhất”, mở URL attempt trực tiếp và reload đều thấy “Kết quả chưa được phép xem.”; không có điểm, đạt/trượt, thời lượng, lựa chọn/đáp án trong trang. Browser đọc cả GET và POST submit lặp từ API thật: `result=null`, `questions=[]`, `savedAnswers=[]`, không có các key `totalMark`, `isPassed`, `timeTakenSeconds`, `isCorrect`, `point`, source ID hay JSON chấm. SQL có một submission cho fixture rồi script dọn. Lần chạy đầu phát hiện mở URL trực tiếp về login do FE khôi phục refresh cookie đồng thời; đã gom `resumeSession` thành một promise và chạy lại đạt. Lần lỗi fixture cũng được dọn, không tính là bằng chứng đạt. Chạy lại `node tests/attemptBrowserSmoke.mjs` với kết quả được phép xem đạt, gồm URL trực tiếp/reload và điểm/thời lượng.

**Ranh giới deadline:** test cô lập truyền mốc UTC vào `AttemptTiming.IsExpired`: một tick trước deadline trả false, đúng deadline và một tick sau trả true. Cùng predicate được controller, finalizer và worker dùng sau khi có SQL lock. `pwsh -NoProfile -File tests/Task10DeadlineRaceSqlSmoke.ps1` trên copy tạo ba lượt riêng: trước deadline → `submitted`, `SubmittedAtUtc < DeadlineUtc`; quá deadline → `timedOut`, `SubmittedAtUtc=DeadlineUtc`; race → một process giữ `DACN:schedule-write`, HTTP submit được gửi trước deadline, worker sweep chạy sau deadline, cả hai chỉ xử lý sau khi khóa được nhả: response `timedOut`, SQL có đúng một submission, `SubmittedAtUtc=DeadlineUtc`, `FinishedAtUtc>=DeadlineUtc`; submit lặp không đổi kết quả. Output: `TASK10_DEADLINE_SQL=PASS tag=1d2fc88b60 before=submitted after=timedOut race=timedOut singleSubmission=1 sentBeforeDeadline=1`. Đây là kiểm có kiểm soát thứ tự gửi/xử lý qua deadline, không chứng minh hai request đến cùng một tick.

**Điểm và snapshot:** `pwsh -NoProfile -File tests/Task10TimeoutScoringSqlSmoke.ps1` chạy lại sau khi sửa/ẩn Question và Answer nguồn. Snapshot lưu raw `1.005` và `1.015`; DACN lưu `1.00`/`1.02`, `isPassed=true` với PassMark `1.004` và `false` với PassMark `1.016`; tổng đúng ba câu `4.52`, multi thiếu/thừa `2.02`, bỏ trống `0.00`. .NET/Testify `Math.Round(double,2)` cho `1.00`/`1.01` ở hai giá trị riêng. Output: `TASK10_TIMEOUT_SCORING_SQL_API=PASS tag=a1cddb6675 due=8 raw=1.005/1.015 stored=1.00/1.02 passed=1/0 testifyDouble=1.00/1.01 malformedOpen=1 fingerprint=8:19.62:504`. Giữ thuật toán decimal/ToEven và so ngưỡng trước làm tròn theo quyết định người dùng; không sửa điểm Testify cũ. Task12 cần liệt kê sai khác 1.015 trong đối soát, không coi là mất dữ liệu.

`dotnet run --project tests/DependencyInjectionSmoke -- --attempt-snapshot`, build BE, FE lint/build và hai chế độ browser đạt; postflight SQL copy sau dọn có hai bảng attempt/submission 0 hàng; tài khoản QA phục vụ register/verify vẫn nằm trên bản sao có thể tạo lại. `ProjectDACN` gốc không bị ghi. F17–F19 giữ `IN_PROGRESS` vì dữ liệu Testify cũ/chính sách lịch sử chưa đối soát và kết quả Task10 chưa nối báo cáo admin; không lấy build hoặc fixture riêng làm parity toàn bộ. Phần exact-tick chỉ đạt test predicate cô lập, không có kiểm hai HTTP request cùng tick trên SQL Server.

## Task10 timeout không cần request và biên chấm — 2026-10-18

Trước ghi: `ProjectDACN_Task04Task09_Test` có migration Task10ExamAttempts, `ExamAttempts=0`, `ExamAttemptSubmissions=0`; DB gốc `ProjectDACN` vẫn chỉ có initial migration và không có bảng attempt. Tạo rồi chỉ áp trên bản sao migration `20261018063239_Task10TimeoutAndDuration`: thêm `ExamAttemptSubmissions.TimeTakenSeconds bigint NULL`, đổi `ExamAttempts.PassMark` từ `decimal(18,2)` sang `float` để giữ ngưỡng điểm nguồn. Sau áp, history bản sao có migration này, `PassMark float NOT NULL`, `TimeTakenSeconds bigint NULL`; không áp DB gốc.

BE có `AttemptFinalizer` dùng chung cho manual submit và worker. Worker quét ngay khi khởi động và mỗi 10 giây, từng lượt trong giao dịch riêng với SQL application lock; `DeadlineUtc` là mốc quyết định. Timeout chấm bản nháp cuối cùng đã lưu, `SubmittedAtUtc=DeadlineUtc`, `FinishedAtUtc=giờ server chốt`, `TimeTakenSeconds=floor(max(0, submittedAt-startedAt))`. PK `ExamAttemptSubmissions.ExamAttemptId` và khóa chung giữ một kết quả khi submit/timeout tranh nhau. Lỗi từng lượt ghi attempt ID rồi tiếp tục lượt khác. Worker kiểm migration mới trước khi chạy để không đọc schema chưa cập nhật.

`pwsh -NoProfile -File tests/Task10TimeoutScoringSqlSmoke.ps1` qua API HTTPS và SQL Server bản sao: tạo 9 attempt riêng, lưu nháp 7 lượt, sửa/ẩn Question và Answer nguồn, rồi ép deadline/startedAt của fixture thành quá khứ. Không có request HTTP tới 7 lượt chấm cho đến khi worker xử lý. Tám lượt hợp lệ `timedOut` và có đúng 8 submissions; lượt thứ 9 có snapshot JSON hỏng vẫn `inProgress`, không có submission, worker ghi lỗi theo ID và tiếp tục. Kết quả lưu: đúng cả 3 câu 4.52 đạt; multi thiếu hoặc thừa 2.02 không đạt; bỏ trống 0; chỉ câu 1 có 1.005 điểm nguồn lưu 1.00 nhưng vẫn đạt ngưỡng 1.004; chỉ câu 2 có 1.015 điểm nguồn lưu 1.02 nhưng không đạt ngưỡng 1.016 (so trước làm tròn); `AllowViewResult=false` trả `result=null` dù SQL lưu 4.52. Cả 8 hàng có `SubmittedAtUtc=DeadlineUtc`, `FinishedAtUtc>=DeadlineUtc`, thời lượng 63 giây từ fixture; bản nháp không đổi sau khi nguồn bị sửa. Một HTTP submit và một sweep độc lập chạy đồng thời cho lượt `race`, cuối cùng vẫn một submission; submit lặp trả kết quả cũ, sweep sau 11 giây giữ fingerprint `8:19.62:504`. Script dọn toàn bộ 9 attempt, submission và exam/class/schedule/question fixture. Ca này kiểm tranh chấp thực tế nhưng không bảo đảm hai request đến cùng một tick deadline.

Kiểm khởi động lại riêng: dừng API, insert một attempt snapshot hợp lệ đã quá hạn vào bản sao (trước: submission 0), khởi động API với `ConnectionStrings__Default` trỏ rõ bản sao. Log startup `Finalized 1 overdue exam attempts`; SQL thấy `timedOut,0.00,0,60,submittedAtEqualsDeadline=1`, submission count 1. Chạy `WorkerSweepSmoke` ở process khác trả `WORKER_SWEEP_COMPLETED=0`; dọn fixture, hai bảng attempt/submission trở về 0. Đây là fixture SQL cho worker recovery, không phải API start.

`dotnet run --project tests/DependencyInjectionSmoke -- --attempt-snapshot` đạt các ca chấm cô lập về đáp án bỏ trống/sai, multi thiếu/thừa/đúng, tổng và rounding ToEven. `dotnet build DACN_Project.sln --no-restore -v:q /clp:ErrorsOnly` đạt 0 lỗi; FE `npm run lint`, `npm run build` đạt. `node tests/attemptBrowserSmoke.mjs` chạy Chrome → HTTPS Vite → API → SQL bản sao đạt start/save/reload/submit và hiện thời lượng sau nộp/tải lại. Chưa có kiểm browser riêng cho `AllowViewResult=false`; API/SQL đã kiểm. Chưa chứng minh hai contender được giữ cùng một khóa đúng tick deadline bằng SQL barrier; cần nghiệm thu nếu muốn chứng cứ race chính xác thời điểm đó. Chưa đối soát lượt Testify cũ, chưa nối báo cáo/admin Submission cũ, chưa triển khai DB gốc. F17–F19 vẫn IN_PROGRESS.

Sai khác số học đã xác minh: Testify cộng `double` rồi dùng `Math.Round(double,2)`; DACN theo contract hiện tại chuyển điểm snapshot sang `decimal`, cộng rồi làm tròn ToEven. Trên .NET hiện tại, `Math.Round(1.015d,2)=1.01` còn DACN lưu 1.02. Ngưỡng đạt vẫn được so trước làm tròn. Không tuyên bố tương đương điểm tuyệt đối với Testify ở các biên nhị phân; cần quyết định nghiệp vụ trước đối soát/nhập lượt cũ hoặc đổi quy tắc chấm DACN.

## Task10 API, FE, SQL bản sao — 2026-10-18

Source Testify `ViewExamTest.razor` gán `limitofsubmit = exam.NumberOfRepeat`, chặn khi `lst.Count >= exam.NumberOfRepeat`; UI gọi “Số lần làm bài”. Chốt `NumberOfRepeat` là **tổng lượt**, không phải số lần thi lại cộng một. Deadline cố định UTC `min(start + Duration phút, scheduleEnd)`. `ScoreMethodId` khác null bị chặn ở start vì chưa có thuật toán tương ứng. Lượt cũ DoingExam/Submission cùng user/lịch chặn start để chờ đối soát; không thay đổi hàng lịch sử.

Preflight `SELECT DB_NAME()` trên `ProjectDACN_Task04Task09_Test`: history 4 migration Task04/Task09, DoingExams 12, Submissions 4. Sinh migration `20261018053153_Task10ExamAttempts` từ EF8 model, review Up tạo hai bảng `ExamAttempts`/`ExamAttemptSubmissions`, FK restrict, unique filtered index lượt mở `(UserId,ExamScheduleId)`; Down từ chối khi còn dữ liệu lượt mới. Đã áp đúng migration này lên **bản sao**; không áp lên ProjectDACN. Bảng mới trước fixture đều 0. DB gốc hậu kiểm: history 1, `OBJECT_ID('dbo.ExamAttempts')=NULL`.

`pwsh -NoProfile -File tests/Task10AttemptSqlSmoke.ps1` chạy API HTTPS → SQL Server bản sao: start đồng thời trả cùng attempt và DB count 1; membership ngoài lớp 403; lịch unknown/sớm/hết 409; snapshot không đổi sau sửa Question/Answer nguồn; response không có field chấm điểm/cờ đúng; người khác GET/PUT/submit đều 404; ID ngoài snapshot 400; hai PUT cùng revision trả 200/409 và revision cuối 1, submit với revision cũ 409; nộp lặp trả cùng kết quả, submission count 1; `NumberOfRepeat=1` chặn lần thứ hai; điểm hai câu (single + multi) 10; deadline được đối chiếu với scheduleEnd khi ngắn hơn Duration; ép deadline fixture sang quá khứ thì start lặp trả cùng attempt timedOut **đã commit**, nộp lặp vẫn trả kết quả đó, PUT sau hạn 409; `AllowViewResult=false` ẩn result; thu hồi membership khiến GET/submit 404. Các thay đổi giờ deadline/Question/Answer chỉ trên fixture riêng rồi xóa.

`node tests/attemptBrowserSmoke.mjs` chạy Chrome headless → Vite HTTPS → API → SQL bản sao: lịch học viên → vào bài → chọn đáp án → lưu → reload trang khôi phục revision/lựa chọn → nộp → điểm 5 theo AllowViewResult=true → quay về lịch thấy liên kết lượt gần nhất → mở lại kết quả; DB có đúng một hàng kết quả. `dotnet build DACN_Project.sln --no-restore -v:q /clp:ErrorsOnly`, `dotnet run --project tests/DependencyInjectionSmoke -- --attempt-snapshot`, FE `npm run lint` và `npm run build` đạt. Test cô lập kiểm chính xác biên `[start,end)` và hai nhánh deadline min; không coi đây là SQL boundary test chính xác cùng một tick.

Postflight bản sao: history Task10 là migration mới nhất, ExamAttempts 0, ExamAttemptSubmissions 0, Exam/Schedule QA đã xóa, ExamSchedules 74. Các tài khoản QA tạo để xác thực vẫn nằm trong bản sao có thể tạo lại. Bảng kết quả mới chưa được nối vào báo cáo/admin CRUD `Submissions` cũ; không dùng dữ liệu cũ để giả lập snapshot. Chưa chạy exhaustive nhiều biến thể, mọi mức điểm/rounding, kết quả ẩn qua browser, lịch có dữ liệu legacy cần đối soát, và background timeout không có request. Chưa đối soát Testify và chưa triển khai DB gốc; F17–F19 giữ IN_PROGRESS.

## Checkout và phạm vi

BE `dev/Hop` tại `1d95f71da6ff719c75de182b13a0f0368c53d157`, FE `dev/Hop` tại `c008165d57a74ec836184b2b97f3fa8d94fa24e1`, trước các thay đổi của lần này. Giữ nguyên thay đổi người dùng ở `.gitmodules`, `AuthController.cs`, DOCX bị xóa, gitlink `impeccable` và hai file export `Gittodoc_testify.txt`.

Chỉ ghi fixture trên `ProjectDACN_Task04Task09_Test`. Preflight đọc history có đúng initial → Task04AuthLifecycle → Task09ExamScheduleDirection → Task09ScheduleUtcProvenance. Có 74 lịch, 16 lịch `IsTimeUtc=0`. API chạy với `ConnectionStrings__Default` chỉ rõ tên DB bản sao. Không tạo hoặc áp migration trong lần này.

## Task09 — SQL Server/API thật

Lệnh: `pwsh -NoProfile -File tests/Task09MembershipConcurrencySmoke.ps1 -BaseUrl https://localhost:7255`.

Script tạo tài khoản QA qua register/verify/login, cấp role Admin chỉ cho actor mới trong bản sao; tạo hai Student synthetic riêng. Hai HTTP PUT duyệt membership được gửi trước khi chờ response. `ClassMembershipStore` hiện có khóa `DACN:schedule-write` trong transaction và khóa hàng lớp; không cần sửa logic nghiệp vụ sau nghiệm thu.

| Ca | Fixture | Trước | HTTP | Sau |
|---|---|---|---|---|
| Một học viên, hai lớp, lịch giao nhau | Exam 16; classes 43/44; schedules 1076/1077; memberships 33/34 | Cả hai status 2; sĩ số 0/1 mỗi lớp | 200,409 | 33 vẫn status 2; 34 status 1; sĩ số 0/1 và 1/1 |
| Hai lịch `[08:00,09:00)` và `[09:00,10:00)` UTC | Exam 17; classes 45/46; schedules 1078/1079; memberships 35/36 | Cả hai status 2; sĩ số 0/1 mỗi lớp | 200,200 | Cả hai status 1; sĩ số 1/1 mỗi lớp |
| Hai học viên tranh chỗ cuối cùng một lớp | Exam 18; class 47 capacity 1; memberships 37/38 | Cả hai status 2; sĩ số 0/1 | 200,409 | 37 status 1; 38 vẫn status 2; sĩ số 1/1 |

Script kiểm từng membership theo response, so sĩ số với capacity và dọn Exam/Class/Schedule/ClassUser/ClassExamSchedule theo ID fixture. Các tài khoản QA được giữ trong bản sao có thể tạo lại từ backup đã ghi ở `12-data-readiness.md`; tag lần đạt: `4ccbb96026`. Các lần bootstrap ban đầu gặp thiếu `QUOTED_IDENTIFIER` và auth 429; đã sửa sqlcmd dùng `-I` và retry 429 có giới hạn, sau đó toàn bộ ba ca đạt. Không tính các lần lỗi fixture là bằng chứng đạt.

Task09 còn thiếu ca concurrent approval đã được đóng. F16 vẫn IN_PROGRESS ở mức parity toàn bộ dữ liệu: lịch sử chưa rõ timezone chưa được đối soát. Phạm vi lịch UTC DACN đã có bằng chứng các ca trên và các ca của ngày 2026-10-08. Triển khai DB gốc và ETL là các bước riêng, không thực hiện trong lần này.

## Task10 — phần đã triển khai và kiểm thật

`GET /web/student/schedules` bổ sung `serverNow` UTC `Z` và `scheduleState`: `upcoming`, `open`, `ended`, `unknown`. BE lấy một mốc `DateTime.UtcNow` sau truy vấn, tính `[start,end)` cho lịch có provenance UTC; lịch cũ luôn `unknown`. Đây là trạng thái khoảng giờ lịch, không phải trạng thái lượt làm bài hoặc xác nhận đủ điều kiện bắt đầu.

FE `/student/schedules` hiển thị nhãn từ API, tải lại mỗi 30 giây/khi focus và bằng nút Tải lại. Không dùng giờ máy khách để quyết định trạng thái. Không thêm nút bắt đầu giả hoặc gọi CRUD Submission để nộp bài.

Lệnh browser: `node tests/studentScheduleBrowserSmoke.mjs`, Chrome headless → Vite HTTPS → API → SQL Server bản sao. Fixture gồm 4 lịch thuộc lớp đã duyệt, một lịch lớp đang chờ duyệt, một lịch lớp không có membership. Kết quả:

- API trả đúng 4 lịch với 4 trạng thái, `serverNow` có `Z`, lịch unknown không bị thêm offset; không có `IsCorrect` trong payload lịch.
- Lịch chưa duyệt và lịch không thuộc lớp bị loại khỏi danh sách.
- Sau khi giả `Date.now()` của browser thành 0, UI vẫn hiển thị đúng trạng thái từ server.
- Đổi giờ fixture open trong SQL thành đã hết hạn, click Tải lại: UI đổi sang “Đã hết giờ”.
- Dọn cả 6 lịch, các lớp/link/membership và Exam fixture sau kiểm; QA account giữ ở bản sao.

Build BE đạt 0 lỗi; FE lint/build đạt (build vẫn có cảnh báo chunk >500 kB). Postflight: history 4, schedules 74, unknown 16, orphan schedule 0. DB gốc chỉ đọc để xác nhận history 1, Users 1, Exams/Schedules 0.

## Task10 — schema và quyết định đang chờ

Ở lần kiểm đầu chưa có `VibeCoding/Task10.md`; file hiện đã có và đã được đọc lại. Phạm vi file là start/resume/save, snapshot, concurrency, deadline server và trang làm bài. `00-overview/04-decisions.md` O05/O06 vẫn để OPEN về cách tính/phạm vi lượt và deadline. Chấm nhiều đáp án/rounding/ngưỡng đạt thuộc phần finalize của yêu cầu mở rộng, không chặn việc chuẩn bị snapshot/draft.

Source và schema SQL đã đối chiếu:

- DoingExams chỉ có UserId/ExamId/ExamDetailId/InProgress/StartTime/ExamScheduleId/Content; chưa có deadline, revision, snapshot typed, unique active attempt.
- Submissions chưa có AttemptId unique; TotalMark là float, TimeTaken là SQL time; không phù hợp để tự đổi hàng lịch sử sang decimal/duration khi chưa có policy.
- AnswerSubmissions tham chiếu Question/Answer sống, chưa lưu snapshot nội dung/đáp án đúng. Các route ghi CRUD Submission/AnswerSubmission hiện `[NonAction]`, không phải API nộp bài.
- Contract attempt trong `04-contracts/02-exam-api.md` vẫn PROPOSED. Route đọc Submission hiện yêu cầu AdminManagement; API kết quả của học viên cần route và ownership riêng, không mở rộng quyền đọc toàn bộ CRUD cũ.

Thiết kế cần thực hiện sau khi chốt policy: thêm provenance/version cho lượt mới; deadline UTC, revision và trạng thái finalize; bảng snapshot câu hỏi/options/điểm và draft riêng; liên kết Submission–Attempt unique; thời lượng giây và điểm decimal cho lượt mới, giữ nguyên lịch sử cũ. Start/Save/Submit phải dùng khóa/giao dịch, kiểm owner và snapshot, không nhận UserId/điểm từ client. Không sinh migration trước khi chốt semantics ảnh hưởng dữ liệu này.

Chưa có bằng chứng API/SQL cho start/resume/save/submit/grading, vượt số lượt, nộp lặp, save/submit đồng thời, owner của attempt, release kết quả hoặc không lộ đáp án trong **attempt response thực tế**. Không dùng test payload lịch thay cho test an toàn đề thi. F17/F18 IN_PROGRESS; F19 OPEN. Chưa có luồng Task10 xuyên suốt.

## Tiếp tục chuẩn bị snapshot/draft

Thêm `Project.Domain/ValueObjects/AttemptSnapshot.cs`, DTO học viên trong `Project.Application/DTOs/AttemptDTO` và `AttemptSnapshotFactory`. Factory kiểm mã đề công khai và câu/đáp án hợp lệ theo loại 1–3 hiện có, sao chép nội dung/cờ đúng/điểm nguồn, tạo Guid riêng cho câu/options. DTO allowlist loại bỏ cờ đúng, ID nguồn và điểm. ValidateDraft kiểm quyền sở hữu lựa chọn trong snapshot, không cho trùng câu/đáp án hoặc nhiều lựa chọn cho loại 1/2; danh sách rỗng xóa toàn bộ lựa chọn, câu bỏ qua thành chưa trả lời. Điểm vẫn là double nguồn, chưa áp thuật toán chấm hay rounding.

Lệnh đã đạt: `dotnet run --project tests/DependencyInjectionSmoke -- --attempt-snapshot`. Kiểm snapshot độc lập với thay đổi entity nguồn, JSON round-trip, allowlist chính xác DTO học viên, draft tách khỏi list client, câu bỏ qua/rỗng, nhiều lựa chọn, và các payload sai. Đây là **test cô lập**, không SQL/API; factory chưa nối vào controller/persistence. Không thêm package, schema hoặc migration. Không đưa baseline deadline/tính lượt vào code khi chưa có lựa chọn của người dùng.

## Task11/Task13 — lịch sử học viên và báo cáo DACN mới, 2026-10-18

Phần Task10 đã được tách và commit trên `dev/Hop`: BE `d37f4311e0e3f26369f45349177d343dbe75dff5`, FE `56adc93b03d914a030105e97b4eb5bdc8ab2d6d3`. Các thay đổi có sẵn ngoài Task10 (`.gitmodules`, `AuthController.cs`, tài liệu DOCX, `impeccable`, bản export `Gittodoc_testify.txt`) vẫn giữ nguyên; chưa push trong lượt này. Sau commit mới phát triển Task11/Task13, hiện là thay đổi chưa commit riêng.

Danh sách file chính xác của commit Task10 BE (`git show --name-only d37f431`):

```text
Project.Api/Controllers/ExamScheduleController.cs
Project.Api/Controllers/StudentAttemptController.cs
Project.Api/Controllers/StudentScheduleController.cs
Project.Api/Program.cs
Project.Api/Services/AttemptFinalizer.cs
Project.Api/Services/ExpiredAttemptFinalizerService.cs
Project.Application/Services/AttemptScoring.cs
Project.Application/Services/AttemptTiming.cs
Project.Domain/Entities/ExamAttempt.cs
Project.Infrastructure/Migrations/20261018053153_Task10ExamAttempts.Designer.cs
Project.Infrastructure/Migrations/20261018053153_Task10ExamAttempts.cs
Project.Infrastructure/Migrations/20261018063239_Task10TimeoutAndDuration.Designer.cs
Project.Infrastructure/Migrations/20261018063239_Task10TimeoutAndDuration.cs
Project.Infrastructure/Migrations/ProjectDACNDbContextModelSnapshot.cs
Project.Infrastructure/Persistence/Configurations/ExamAttemptConfiguration.cs
Project.Infrastructure/Persistence/ProjectDACNDbContext.cs
Project.Infrastructure/Persistence/ScheduleWriteGuard.cs
docs/agent-rules/00-overview/04-decisions.md
docs/agent-rules/01-backend/04-exam-engine.md
docs/agent-rules/04-contracts/01-http.md
docs/agent-rules/04-contracts/02-exam-api.md
docs/agent-rules/05-migration/01-parity-matrix.md
docs/agent-rules/05-migration/12-data-readiness.md
docs/agent-rules/05-migration/13-task09-task10-acceptance.md
tests/DependencyInjectionSmoke/AttemptScoringSmoke.cs
tests/DependencyInjectionSmoke/AttemptSnapshotSmoke.cs
tests/DependencyInjectionSmoke/Program.cs
tests/Task10AttemptSqlSmoke.ps1
tests/Task10DeadlineRaceSqlSmoke.ps1
tests/Task10TimeoutScoringSqlSmoke.ps1
tests/WorkerSweepSmoke/Program.cs
tests/WorkerSweepSmoke/WorkerSweepSmoke.csproj
```

Commit Task10 FE (`git show --name-only 56adc93`): `docs/agent-rules/00-overview/04-decisions.md`, `docs/agent-rules/04-contracts/01-http.md`, `docs/agent-rules/04-contracts/02-exam-api.md`, `docs/agent-rules/05-migration/01-parity-matrix.md`, `src/App.jsx`, `src/pages/attempts/StudentAttemptPage.jsx`, `src/pages/schedules/StudentSchedulesPage.jsx`, `src/services/attemptService.js`, `src/services/authService.js`, `tests/attemptBrowserSmoke.mjs`. File có thêm thay đổi Task11/13 sau commit được giữ ở working tree và không đưa vào commit Task10.

Preflight chỉ đọc trên SQL Server: `DB_NAME()=ProjectDACN_Task04Task09_Test`, `__EFMigrationsHistory` đủ 6 migration đến `20261018063239_Task10TimeoutAndDuration`, trước fixture `ExamAttempts=0`, `ExamAttemptSubmissions=0`. **Không tạo hoặc áp migration**, không ghi `ProjectDACN` gốc, không đụng dữ liệu Testify.

`tests/Task11Task13SqlSmoke.ps1` tạo ba user QA, một exam, một mã đề, hai lớp, bốn lịch và 5 lượt trên bản sao; hai lớp cùng gán vào lịch để kiểm không đếm đôi. Lượt gồm học viên A: 1 submitted/8 điểm/đạt, 1 submitted/6 điểm/đạt nhưng `AllowViewResult=false`, 1 timedOut/2 điểm/trượt, 1 inProgress; học viên B: 1 submitted/4 điểm/trượt. Qua API thật: học viên A list được đúng 4 lượt, trang 2 dòng, không thấy lượt B; kết quả ẩn là `result=null` ở list và detail; truy cập detail của B trả 404; Student gọi report trả 403. Admin report lọc exam/lớp/môn trả `total=5`, `finalized=4`, `inProgress=1`, `timeout=1`, `hidden=1`, `passed=2`, `averageScore=5`, `passPercent=50`, phổ điểm 2/4/6/8 mỗi mức một lượt. Trang 1 và 2 mỗi trang 2 dòng; lọc lớp thứ hai vẫn đếm 5 thay vì nhân đôi. SQL độc lập xác nhận 5 attempt, 1 inProgress và 4 submission. Chạy test trả PASS, finally dọn fixture; postflight `ExamAttempts=0`, `ExamAttemptSubmissions=0`.

Chrome headless → Vite HTTPS → API → SQL bản sao: `attemptBrowserSmoke.mjs --hidden-result` PASS sau khi mở trang lịch sử mới, row và response list không có điểm/đạt/đáp án; mở lại chi tiết từ row vẫn ẩn. Bản chạy kết quả được phép xem cũng PASS: lịch → start → save → reload → submit → lịch sử → chi tiết, API và SQL có đúng một submission. `Task11Task13SqlSmoke.ps1 -Browser` PASS: màn admin hiển thị đúng 5 lượt cùng 8 chỉ số từ API, dù auth rate limit buộc smoke chờ retry. FE `npm run lint` PASS, `npm run build` PASS khi Vite được phép nạp native binding; BE `dotnet build Project.Api/Project.Api.csproj --no-restore` PASS (2 warning tồn tại từ package và UserController). Postflight cuối: DB copy có 0 attempt, 0 submission, 0 user `reportqa` của fixture báo cáo còn lại. Script browser Task10 giữ các user QA đã đăng ký như thiết kế cũ.

F20/F22 giữ `IN_PROGRESS`: chưa đối soát/hiển thị `DoingExam`/`Submission` Testify cũ, chưa có review đáp án theo chính sách riêng hoặc void/audit lượt mới. Báo cáo chỉ có phạm vi DACN mới, không union dữ liệu legacy. Công thức gồm timeout và kết quả ẩn theo quyết định nghiệp vụ; lượt đang làm không nằm trong mẫu số. Báo cáo không chấm lại điểm lịch sử.
