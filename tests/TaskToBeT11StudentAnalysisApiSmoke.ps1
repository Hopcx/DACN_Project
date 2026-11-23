# To-be T11 - Phan tich hoc vien theo chuong/tag: snapshot v3, QuestionOutcomes ghi luc chot, worker bu du lieu cu,
# GET /web/student/analysis (chi du lieu cua minh, bai thi chi tinh khi da duoc xem ket qua).
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao đã áp migration T11. Chỉ ghi vào bản sao;
# fixture gắn tag ngẫu nhiên và được dọn trong finally.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -f 65001 -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51241, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t11qa$tag@example.invalid"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
function Auth([string]$route, $body) {
    for ($i = 0; $i -lt 6; $i++) {
        $r = Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck `
            -Headers @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)
        if ([int]$r.StatusCode -ne 429) { return @{ status = [int]$r.StatusCode; body = ($r.Content | ConvertFrom-Json) } }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
function Call([string]$method, [string]$url, [string]$token, $body = $null) {
    $p = @{ Uri = "$BaseUrl$url"; Method = $method; Headers = @{ Authorization = "Bearer $token" }; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $p.ContentType = 'application/json'; $p.Body = ($body | ConvertTo-Json -Compress -Depth 8) }
    $r = Invoke-WebRequest @p
    return @{ status = [int]$r.StatusCode; raw = [string]$r.Content; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected '$expected', got '$actual'" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
# Chọn đáp án theo chữ ('OK' đúng / 'NO' sai); $plan: mảng 'ok' | 'no' | 'blank' theo thứ tự nội dung câu (n1..n6)
function Answers($questions, [hashtable]$plan) {
    return @($questions | ForEach-Object {
        $n = [regex]::Match($_.content, 'n(\d)').Groups[1].Value
        $choice = $plan[$n]
        # Gán trong nhánh (không lấy đầu ra của if): @() làm đầu ra sẽ thành $null
        if ($choice -eq 'blank') { $ids = [object[]]@() } else { $ids = [object[]]@(@($_.options | Where-Object { $_.content -eq $choice.ToUpper() })[0].id) }
        @{ questionId = $_.id; selectedAnswerIds = $ids }
    })
}
function Analysis([string]$token, [string]$query = '') { Call 'GET' "/web/student/analysis$query" $token }
function Stat($list, [string]$name) { return @($list | Where-Object { $_.name -eq $name })[0] }

$student = [Guid]::Empty; $other = [Guid]::NewGuid(); $exam = 0; $mail = $null; $subject = 0
try {
    Expect (Auth 'register' @{ fullName = 'T11 Học viên'; userName = "t11qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $token = [string]$login.body.data.accessToken
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$other',N'T11 Khác',N't11other$tag',N't11other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';" | Out-Null
    $otherToken = [string](Auth 'login' @{ keyword = "t11other$tag@example.invalid"; password = $password }).body.data.accessToken

    # Môn riêng; chương C1 (n1-n5), C2 ẩn (n6); tag A (n1-n3), tag B ẩn (n4-n6). Đề 6 câu, cho xem kết quả; lớp đã duyệt.
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$student';
INSERT dbo.Subjects(Name,Status) VALUES (N'T11 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,N'T11 C1 $tag',1,1,SYSUTCDATETIME()); DECLARE @c1 int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,N'T11 C2 $tag',2,0,SYSUTCDATETIME()); DECLARE @c2 int=SCOPE_IDENTITY();
INSERT dbo.Tags(Name,NormalizedName,Status,CreatedAt) VALUES (N'T11 tagA $tag',N't11 taga $tag',1,SYSUTCDATETIME()); DECLARE @ta int=SCOPE_IDENTITY();
INSERT dbo.Tags(Name,NormalizedName,Status,CreatedAt) VALUES (N'T11 tagB $tag',N't11 tagb $tag',0,SYSUTCDATETIME()); DECLARE @tb int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T11 đề $tag',@s,6,1,1,6,3,30,1); DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@exam,N'T11',1,SYSUTCDATETIME(),@user,SYSUTCDATETIME(),@user);
DECLARE @v int=SCOPE_IDENTITY(), @i int=1, @q int;
WHILE @i <= 6 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,ChapterId)
  VALUES (CONCAT(N'T11 $tag n',@i),SYSUTCDATETIME(),1,@s,2,CASE WHEN @i<=5 THEN @c1 ELSE @c2 END);
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  INSERT dbo.QuestionTags(QuestionId,TagId) VALUES (@q,CASE WHEN @i<=3 THEN @ta ELSE @tb END);
  INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES(@v,@q,1);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'T11 $tag',N'T11-$tag',5,@user,@s,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(SCOPE_IDENTITY(),@user,1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES(@exam,N'T11 lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,30,SYSUTCDATETIME()),1,1);
DECLARE @schedule int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT Id,@schedule FROM dbo.Classes WHERE ClassCode=N'T11-$tag';
COMMIT;
SELECT CONCAT(@s,',',@exam,',',@schedule,',',@c1,',',@c2);
"@
    $p = $ids.Split(','); $subject = [int]$p[0]; $exam = [int]$p[1]; $schedule = [int]$p[2]; $c1 = [int]$p[3]; $c2 = [int]$p[4]

    Write-Output '== Bài thi: kết quả từng câu ghi lúc nộp, snapshot v3'
    $start = Call 'POST' "/web/exam-schedules/$schedule/attempts" $token
    Expect $start.status 200 'start exam'
    $attempt = $start.body.data.attemptId
    Expect (Sql "SELECT JSON_VALUE(SnapshotJson,'$.version') FROM dbo.ExamAttempts WHERE Id='$attempt';") '4' 'snapshot v4 (T15; chương/tag từ v3)'
    Expect (Sql "SELECT COUNT(*) FROM dbo.ExamAttempts a CROSS APPLY OPENJSON(a.SnapshotJson,'$.questions') WITH (chapterId int '$.chapterId', tagIds nvarchar(max) '$.tagIds' AS JSON) q WHERE a.Id='$attempt' AND q.chapterId IS NOT NULL AND q.tagIds LIKE '[[]%]' AND q.tagIds <> '[]';") '6' 'snapshot carries chapter + tags'
    $plan = @{ '1' = 'ok'; '2' = 'ok'; '3' = 'ok'; '4' = 'ok'; '5' = 'no'; '6' = 'blank' }
    $save = Call 'PUT' "/web/attempts/$attempt/answers" $token @{ revision = $start.body.data.revision; answers = (Answers $start.body.data.questions $plan) }
    Expect $save.status 200 'save exam answers'
    Expect (Call 'POST' "/web/attempts/$attempt/submit" $token @{ revision = $save.body.data.revision }).status 200 'submit exam'
    Expect (Sql "SELECT CONCAT(COUNT(*),':',SUM(CAST(IsCorrect AS int)),':',SUM(CAST(IsAnswered AS int)),':',MIN(ClassificationSource),':',MIN(SourceKind),':',COUNT(DISTINCT ExamScheduleId))
      FROM dbo.QuestionOutcomes WHERE SourceId='$attempt';") '6:4:5:snapshot:exam:1' 'exam outcomes (6 câu, 4 đúng, 1 bỏ trống)'
    Expect (Sql "SELECT CAST(TotalMark AS int) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$attempt';") '4' 'outcomes agree with score'
    Expect (Sql "SELECT SelectedAnswerIdsJson FROM dbo.QuestionOutcomes o JOIN dbo.Questions q ON q.Id=o.SourceQuestionId WHERE o.SourceId='$attempt' AND q.Content=N'T11 $tag n6';") '[]' 'blank stores no selection'
    Pass '6 dòng, 4 đúng khớp điểm, bỏ trống = sai (IsAnswered=0), chương/tag lấy từ snapshot'

    Write-Output '== Chưa công bố kết quả: bài thi không được tính'
    $a0 = Analysis $token
    Expect $a0.status 200 'analysis before release'
    Expect @($a0.body.data.subjects).Count 0 'no eligible subject while schedule open'
    Expect (Analysis $token '?days=0').status 400 'days=0'
    Expect (Analysis $token '?days=366').status 400 'days=366'
    Sql "UPDATE dbo.ExamSchedules SET StartTime=DATEADD(hour,-2,SYSUTCDATETIME()), EndTime=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=$schedule;" | Out-Null
    Sql "UPDATE dbo.ExamAttempts SET AllowViewResult=0 WHERE Id='$attempt';" | Out-Null
    Expect @((Analysis $token).body.data.subjects).Count 0 'AllowViewResult=0 still hidden'
    Sql "UPDATE dbo.ExamAttempts SET AllowViewResult=1 WHERE Id='$attempt';" | Out-Null
    $a1 = (Analysis $token).body.data
    Expect $a1.subjectId $subject 'subject auto-selected'
    Expect "$($a1.totals.total)/$($a1.totals.correct)" '6/4' 'exam counted after release'
    Pass 'lịch chưa kết thúc / không cho xem kết quả → không tính; công bố xong → tính'

    Write-Output '== Luyện tập: ghi lúc nộp, cộng dồn'
    $pr = Call 'POST' '/web/student/practice' $token @{ subjectId = $subject; questionCount = 6 }
    Expect $pr.status 201 'create practice'
    $prId = $pr.body.data.id
    $all = @{ '1' = 'ok'; '2' = 'ok'; '3' = 'ok'; '4' = 'ok'; '5' = 'ok'; '6' = 'ok' }
    Expect (Call 'PUT' "/web/student/practice/$prId/answers" $token @{ revision = 0; answers = (Answers $pr.body.data.questions $all) }).status 200 'save practice'
    Expect (Call 'POST' "/web/student/practice/$prId/finish" $token @{ revision = 1 }).status 200 'finish practice'
    Expect (Sql "SELECT CONCAT(COUNT(*),':',SUM(CAST(IsCorrect AS int)),':',MIN(SourceKind)) FROM dbo.QuestionOutcomes WHERE SourceId='$prId';") '6:6:practice' 'practice outcomes'
    $a2 = (Analysis $token).body.data
    Expect "$($a2.totals.total)/$($a2.totals.correct)" '12/10' 'totals exam+practice'
    Expect "$($a2.bySource.exam)/$($a2.bySource.practice)/$($a2.bySource.mock)" '6/6/0' 'bySource'
    $ch1 = Stat $a2.chapters "T11 C1 $tag"; $ch2 = Stat $a2.chapters "T11 C2 $tag"
    Expect "$($ch1.stats.total)/$($ch1.stats.correct)/$($ch1.stats.level)/$($ch1.hidden)" '10/9/good/False' 'chapter C1 90% good'
    Expect "$($ch2.stats.total)/$($ch2.stats.correct)/$($ch2.stats.level)/$($ch2.hidden)" '2/1/insufficient/True' 'chapter C2 hidden, < 5 câu'
    $tA = Stat $a2.tags "T11 tagA $tag"; $tB = Stat $a2.tags "T11 tagB $tag"
    Expect "$($tA.stats.total)/$($tA.stats.correct)/$($tA.stats.level)" '6/6/good' 'tagA'
    Expect "$($tB.stats.total)/$($tB.stats.correct)/$($tB.stats.level)/$([decimal]$tB.stats.rate)/$($tB.hidden)" '6/4/needsImprovement/66.7/True' 'tagB hidden 66.7%'
    Expect $a2.currentClassificationCount 0 'all from snapshot'
    Expect $a2.thresholds.weakBelow 50 'thresholds exposed'
    Pass 'chương/tag ẩn vẫn tính (hidden=true), xếp loại đúng biên, < 5 câu = insufficient'

    Write-Output '== Chỉ dữ liệu của mình; lượt hủy; ngoài 90 ngày'
    $o = Analysis $otherToken
    Expect $o.status 200 'other student'
    Expect @($o.body.data.subjects).Count 0 'other student sees nothing'
    Expect @((Analysis $otherToken "?subjectId=$subject").body.data.chapters).Count 0 'other student by subjectId sees nothing'
    # Review T11: rời lớp (không còn thành viên đã duyệt) → bài thi không tính, như review
    Sql "UPDATE dbo.ClassUsers SET Status=2 WHERE UserId='$student' AND ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T11-$tag');" | Out-Null
    Expect "$((Analysis $token).body.data.totals.total)" '6' 'exam excluded after leaving class'
    Sql "UPDATE dbo.ClassUsers SET Status=1 WHERE UserId='$student' AND ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T11-$tag');" | Out-Null
    Expect "$((Analysis $token).body.data.totals.total)" '12' 'exam back after rejoin'
    Sql "UPDATE dbo.ExamAttemptSubmissions SET VoidedAtUtc=SYSUTCDATETIME(),VoidReason=N'T11 QA' WHERE ExamAttemptId='$attempt';" | Out-Null
    Expect "$((Analysis $token).body.data.totals.total)" '6' 'voided exam excluded'
    Sql "UPDATE dbo.QuestionOutcomes SET FinishedAtUtc=DATEADD(day,-100,SYSUTCDATETIME()) WHERE SourceId='$prId';" | Out-Null
    Expect @((Analysis $token).body.data.subjects).Count 0 'older than 90 days excluded'
    Expect "$((Analysis $token '?days=120').body.data.totals.total)" '6' 'days=120 includes it'
    Pass 'học viên khác không thấy gì; rời lớp / lượt hủy không tính; cửa sổ ngày đúng'

    Write-Output '== Thi thử hết giờ tự chốt cũng ghi kết quả từng câu'
    $mk = Call 'POST' '/web/student/practice' $token @{ subjectId = $subject; questionCount = 6; kind = 'mock'; durationMinutes = 5 }
    Expect $mk.status 201 'create mock'
    $mkId = $mk.body.data.id
    Sql "UPDATE dbo.PracticeSessions SET DeadlineUtc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id='$mkId';" | Out-Null
    Expect (Call 'GET' "/web/student/practice/$mkId" $token).body.data.state 'finished' 'mock lazily finalized'
    Expect (Sql "SELECT CONCAT(COUNT(*),':',SUM(CAST(IsCorrect AS int)),':',SUM(CAST(IsAnswered AS int)),':',MIN(SourceKind)) FROM dbo.QuestionOutcomes WHERE SourceId='$mkId';") '6:0:0:mock' 'mock outcomes (bỏ trống hết)'
    Expect "$((Analysis $token '?days=120').body.data.bySource.mock)" '6' 'mock counted in analysis'
    Pass 'thi thử quá hạn chốt lười → 6 dòng mock, bỏ trống = sai'

    Write-Output '== Worker bù dữ liệu cũ (snapshot v2 → phân loại hiện tại)'
    # Giả lập phiên trước T11: snapshot v2 không có chapterId/tagIds, chưa có QuestionOutcomes. Đổi chương n1 trong ngân hàng.
    Sql "DELETE dbo.QuestionOutcomes WHERE SourceId='$prId';
      UPDATE dbo.PracticeSessions SET SnapshotJson=JSON_MODIFY(SnapshotJson,'$.version',2) WHERE Id='$prId';
      DECLARE @k int=0; WHILE @k < 6 BEGIN
        UPDATE dbo.PracticeSessions SET SnapshotJson=JSON_MODIFY(JSON_MODIFY(SnapshotJson,CONCAT('$.questions[',@k,'].chapterId'),NULL),CONCAT('$.questions[',@k,'].tagIds'),NULL) WHERE Id='$prId';
        SET @k+=1; END
      UPDATE dbo.Questions SET ChapterId=$c2 WHERE Content=N'T11 $tag n1';
      -- Review T11: lượt thi v2 cũng được bù (kể cả lượt đã hủy — lọc khi đọc)
      DELETE dbo.QuestionOutcomes WHERE SourceId='$attempt';
      UPDATE dbo.ExamAttempts SET SnapshotJson=JSON_MODIFY(SnapshotJson,'$.version',2) WHERE Id='$attempt';
      SET @k=0; WHILE @k < 6 BEGIN
        UPDATE dbo.ExamAttempts SET SnapshotJson=JSON_MODIFY(JSON_MODIFY(SnapshotJson,CONCAT('$.questions[',@k,'].chapterId'),NULL),CONCAT('$.questions[',@k,'].tagIds'),NULL) WHERE Id='$attempt';
        SET @k+=1; END" | Out-Null
    Expect (Sql "SELECT CONCAT(JSON_VALUE(SnapshotJson,'$.version'),':',CASE WHEN SnapshotJson LIKE '%chapterId%' THEN 1 ELSE 0 END) FROM dbo.PracticeSessions WHERE Id='$prId';") '2:0' 'v2 snapshot prepared'
    $filled = ''
    for ($i = 0; $i -lt 45 -and $filled -ne '6'; $i++) { Start-Sleep -Seconds 3; $filled = Sql "SELECT COUNT(*) FROM dbo.QuestionOutcomes WHERE SourceId='$prId';" }
    Expect $filled '6' 'worker backfilled within ~135s'
    Expect (Sql "SELECT CONCAT(MIN(ClassificationSource),':',SUM(CAST(IsCorrect AS int)),':',SUM(CASE WHEN ChapterId=$c2 THEN 1 ELSE 0 END)) FROM dbo.QuestionOutcomes WHERE SourceId='$prId';") 'current:6:2' 'backfill uses current classification (n1 moved to C2)'
    $exFilled = ''
    for ($i = 0; $i -lt 30 -and $exFilled -ne '6'; $i++) { $exFilled = Sql "SELECT COUNT(*) FROM dbo.QuestionOutcomes WHERE SourceId='$attempt';"; if ($exFilled -ne '6') { Start-Sleep -Seconds 3 } }
    Expect (Sql "SELECT CONCAT(COUNT(*),':',MIN(ClassificationSource),':',SUM(CAST(IsCorrect AS int)),':',MIN(SourceKind)) FROM dbo.QuestionOutcomes WHERE SourceId='$attempt';") '6:current:4:exam' 'exam v2 backfilled (4 đúng khớp điểm)'
    Expect "$((Analysis $token).body.data.currentClassificationCount)" '6' 'API reports current classification count (lượt thi hủy vẫn bị loại)'
    Pass 'worker bù phiên luyện tập và lượt thi snapshot v2, ClassificationSource=current, chương theo ngân hàng hiện tại'
    Write-Output "TOBE_T11_STUDENT_ANALYSIS_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "'$student','$other'"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users);"
    Clean "DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    if ($exam -gt 0) { Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam);
      DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam);
      DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" }
    if ($subject -gt 0) { Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T11-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T11-$tag';
      DELETE qt FROM dbo.QuestionTags qt JOIN dbo.Questions q ON q.Id=qt.QuestionId WHERE q.SubjectId=$subject;
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject;
      DELETE dbo.Tags WHERE Name LIKE N'T11 tag_ $tag'; DELETE dbo.Chapters WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" }
    Clean "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N'$email';"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
