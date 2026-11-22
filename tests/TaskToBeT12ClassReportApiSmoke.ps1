# To-be T12 - Bao cao lop cho giang vien/Admin: GET /web/teacher/classes/{id}/analysis (theo chuong, cau hay sai).
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao. Chỉ ghi vào DB bản sao;
# fixture gắn tag ngẫu nhiên và được dọn trong finally.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -f 65001 -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51241, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 8)
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
function Call([string]$method, [string]$url, [string]$token = '') {
    $args2 = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($token) { $args2.Headers = @{ Authorization = "Bearer $token" } }
    $r = Invoke-WebRequest @args2
    $text = if ($r.Headers['Content-Type'] -match 'json') { $r.Content } else { $null }
    return @{ status = [int]$r.StatusCode; body = if ($text) { $text | ConvertFrom-Json } else { $null }; text = [string]$text }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected '$expected', got '$actual'" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'T12 $name',N't12$name$tag',N't12$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t12$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }
function FindMail([string]$email) {
    return Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
}
function MailToken($mail) { return [Uri]::UnescapeDataString([regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)').Groups[1].Value) }

$seed = [Guid]::Empty; $teacherA = [Guid]::NewGuid(); $teacherB = [Guid]::NewGuid(); $admin = [Guid]::NewGuid(); $outsider = [Guid]::NewGuid()
$clones = 1..9 | ForEach-Object { [Guid]::NewGuid() }
$exam = 0; $class = 0; $subject = 0; $mail = $null
try {
    $email = "t12seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T12 s0'; userName = "t12seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = FindMail $email
    Expect (Auth 'verify-email' @{ token = (MailToken $mail) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $tokenA = LoginToken (CloneUser $teacherA 3 'teachera' $seed)
    $tokenB = LoginToken (CloneUser $teacherB 3 'teacherb' $seed)
    $adminToken = LoginToken (CloneUser $admin 1 'admin' $seed)
    for ($i = 0; $i -lt 9; $i++) { CloneUser $clones[$i] 4 "s$($i + 1)" $seed | Out-Null }
    CloneUser $outsider 4 'outsider' $seed | Out-Null
    $m = @($seed) + $clones   # S0..S9

    # Đề: q1-q4, q6 chương C1; q5 chương C2. Mỗi câu: đáp án đúng "OK-<tag>", nhiễu "D1-<tag>", "D2-<tag>".
    # sA (đã công bố): mọi học viên làm q1..q5. Đúng: q1 S0-S2 (30%), q2 S0-S3 (40% biên), q3 S0-S5, q4 S0-S6, q5 tất cả.
    #   q1 sai: S3-S7 chọn D1, S8-S9 chọn D2 → đáp án nhiễu D1 (5 lần).
    #   S0 có thêm lượt thấp hơn (sai hết, chọn D2) — không tính; S8 có lượt hủy điểm cao hơn (đúng hết) — không tính;
    #   người ngoài lớp làm sA (đúng hết) — không tính.
    # sB (đã công bố): q6, đúng S0-S4. sC (chưa công bố): S9 làm q1 đúng — không tính.
    # C1 theo học viên (5 câu): S0-S2 5, S3 4, S4 3, S5 2, S6 1, S7-S9 0 → "Yếu" (< 50%) = S5..S9.
    $ok = @{ 1 = 3; 2 = 4; 3 = 6; 4 = 7; 5 = 10; 6 = 5 }
    $att = @(); $out = @()
    for ($i = 0; $i -lt 10; $i++) {
        $totA = @(1..5 | Where-Object { $i -lt $ok[$_] }).Count
        $att += "('A$i','$($m[$i])','sA',$totA,0)"
        foreach ($q in 1..5) {
            $right = $i -lt $ok[$q]
            $pick = if ($right) { 'ok' } elseif ($q -eq 1 -and $i -ge 8) { 'd2' } else { 'd1' }
            $out += "('A$i',$q,$(if ($right) { 1 } else { 0 }),'$pick')"
        }
        $rightB = $i -lt 5
        $att += "('B$i','$($m[$i])','sB',$(if ($rightB) { 1 } else { 0 }),0)"
        $out += "('B$i',6,$(if ($rightB) { 1 } else { 0 }),'$(if ($rightB) { 'ok' } else { 'd1' })')"
    }
    $att += "('A0low','$($m[0])','sA',0,0)", "('A8void','$($m[8])','sA',5,1)", "('Aout','$outsider','sA',5,0)", "('C9','$($m[9])','sC',1,0)"
    foreach ($q in 1..5) { $out += "('A0low',$q,0,'d2')"; $out += "('A8void',$q,1,'ok')"; $out += "('Aout',$q,1,'ok')" }
    $out += "('C9',1,1,'ok')"
    # Review T12: S1 có lượt thứ hai CÙNG điểm (5) nộp sau → được tính (hòa → nộp sau); lượt này sai q1 (chọn D2)
    $att += "('A1tie','$($m[1])','sA',5,0)"
    foreach ($q in 1..5) { $out += "('A1tie',$q,$(if ($q -eq 1) { 0 } else { 1 }),'$(if ($q -eq 1) { 'd2' } else { 'ok' })')" }
    $members = (($m | ForEach-Object { "(@class,'$_',1)" }) -join ',')
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$seed';
INSERT dbo.Subjects(Name,Status) VALUES (N'T12 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,N'T12 C1 $tag',1,1,SYSUTCDATETIME()); DECLARE @c1 int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,N'T12 C2 $tag',2,1,SYSUTCDATETIME()); DECLARE @c2 int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T12 đề $tag',@s,5,3,1,10,3,30,1); DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@e,N'T12',1,SYSUTCDATETIME(),@user,SYSUTCDATETIME(),@user);
DECLARE @v int=SCOPE_IDENTITY();
DECLARE @QT TABLE(N int, QId int, OkId int, D1Id int, D2Id int);
DECLARE @n int=1, @q int;
WHILE @n <= 6 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,ChapterId,QuestionLevelId)
  VALUES (CONCAT(N'T12 $tag câu q',@n),SYSUTCDATETIME(),1,@s,2,CASE WHEN @n=5 THEN @c2 ELSE @c1 END,2);
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK-$tag',1,1);
  INSERT @QT(N,QId,OkId) VALUES (@n,@q,SCOPE_IDENTITY());
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'D1-$tag',0,1); UPDATE @QT SET D1Id=SCOPE_IDENTITY() WHERE N=@n;
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'D2-$tag',0,1); UPDATE @QT SET D2Id=SCOPE_IDENTITY() WHERE N=@n;
  SET @n+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'T12 $tag',N'T12-$tag',20,'$teacherA',@s,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES $members;
DECLARE @past datetime2=DATEADD(hour,-3,SYSUTCDATETIME()), @ended datetime2=DATEADD(hour,-1,SYSUTCDATETIME());
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'T12 sA',@past,@ended,1,1),(@e,N'T12 sB',@past,DATEADD(minute,5,@ended),1,1),(@e,N'T12 sC',@past,@ended,1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@e;
DECLARE @ST TABLE(K nvarchar(5), Id int);
INSERT @ST SELECT REPLACE(Title,N'T12 ',N''),Id FROM dbo.ExamSchedules WHERE ExamId=@e;
-- Snapshot v3 có đáp án thật (sourceAnswerId, isCorrect) để báo cáo lấy đáp án đúng theo đề lúc thi
DECLARE @snapA nvarchar(max)=(SELECT 3 AS version, (SELECT 1 AS point, q.QId AS sourceQuestionId,
  (SELECT x.Id AS sourceAnswerId, CAST(x.Ok AS bit) AS isCorrect FROM (VALUES (q.OkId,1),(q.D1Id,0),(q.D2Id,0)) x(Id,Ok) FOR JSON PATH) AS options
  FROM @QT q WHERE q.N<=5 ORDER BY q.N FOR JSON PATH) AS questions FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
DECLARE @snapB nvarchar(max)=(SELECT 3 AS version, (SELECT 1 AS point, q.QId AS sourceQuestionId,
  (SELECT x.Id AS sourceAnswerId, CAST(x.Ok AS bit) AS isCorrect FROM (VALUES (q.OkId,1),(q.D1Id,0),(q.D2Id,0)) x(Id,Ok) FOR JSON PATH) AS options
  FROM @QT q WHERE q.N=6 FOR JSON PATH) AS questions FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
DECLARE @A TABLE(K nvarchar(10), U uniqueidentifier, S nvarchar(5), Total int, Voided bit, Id uniqueidentifier DEFAULT NEWID());
INSERT @A(K,U,S,Total,Voided) VALUES $($att -join ',');
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
SELECT a.Id,a.U,s.Id,@e,@v,@past,DATEADD(minute,30,@past),DATEADD(minute,20,@past),N'submitted',0,CASE WHEN a.S=N'sB' THEN @snapB ELSE @snapA END,N'[]',3,
  CASE WHEN a.S=N'sC' THEN 0 ELSE 1 END
FROM @A a JOIN @ST s ON s.K=a.S;
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson,VoidedAtUtc,VoidReason)
SELECT Id,DATEADD(minute,CASE WHEN K LIKE N'%tie' THEN 21 ELSE 20 END,@past),60,Total,CASE WHEN Total>=3 THEN 1 ELSE 0 END,N'[]',CASE WHEN Voided=1 THEN SYSUTCDATETIME() END,CASE WHEN Voided=1 THEN N'T12 QA' END FROM @A;
DECLARE @O TABLE(K nvarchar(10), N int, Ok bit, Pick nvarchar(3));
INSERT @O VALUES $($out -join ',');
INSERT dbo.QuestionOutcomes(SourceKind,SourceId,SnapshotQuestionId,UserId,SubjectId,ExamScheduleId,SourceQuestionId,ChapterId,TagIdsJson,IsCorrect,IsAnswered,SelectedAnswerIdsJson,FinishedAtUtc,ClassificationSource)
SELECT N'exam',a.Id,NEWID(),a.U,@s,s.Id,q.QId,CASE WHEN o.N=5 THEN @c2 ELSE @c1 END,N'[]',o.Ok,1,
  CONCAT('[',CASE o.Pick WHEN 'ok' THEN q.OkId WHEN 'd1' THEN q.D1Id ELSE q.D2Id END,']'),DATEADD(minute,20,@past),N'snapshot'
FROM @O o JOIN @A a ON a.K=o.K JOIN @ST s ON s.K=a.S JOIN @QT q ON q.N=o.N;
COMMIT;
SELECT CONCAT(@s,'|',@e,'|',@class,'|',(SELECT QId FROM @QT WHERE N=1),'|',@c2,'|',(SELECT D1Id FROM @QT WHERE N=1));
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $exam = [int]$p[1]; $class = [int]$p[2]; $q1 = [int]$p[3]; $c2 = [int]$p[4]; $d1 = [int]$p[5]
    # Review T12: sau kỳ thi ngân hàng bị đổi — D1 của q1 thành "đúng", q1 chuyển sang C2 và bị ẩn. Báo cáo phải vẫn theo đề lúc thi.
    # Thêm kết quả LUYỆN TẬP của S9 (q1 đúng) trong lớp — không được tính.
    Sql "UPDATE dbo.Answers SET IsCorrect=1 WHERE Id=$d1; UPDATE dbo.Questions SET ChapterId=$c2, Status=0 WHERE Id=$q1;
      INSERT dbo.QuestionOutcomes(SourceKind,SourceId,SnapshotQuestionId,UserId,SubjectId,ExamScheduleId,SourceQuestionId,ChapterId,TagIdsJson,IsCorrect,IsAnswered,SelectedAnswerIdsJson,FinishedAtUtc,ClassificationSource)
      VALUES (N'practice',NEWID(),NEWID(),'$($m[9])',$subject,NULL,$q1,NULL,N'[]',1,1,N'[]',SYSUTCDATETIME(),N'snapshot');" | Out-Null

    Write-Output '== Quyền'
    Expect (Call 'GET' "/web/teacher/classes/$class/analysis").status 401 'anonymous'
    Expect (Call 'GET' "/web/teacher/classes/$class/analysis" $studentToken).status 403 'student'
    Expect (Call 'GET' "/web/teacher/classes/$class/analysis" $tokenB).status 404 'other teacher'
    Expect (Call 'GET' "/web/teacher/classes/$class/analysis" $adminToken).status 200 'admin'
    Pass '401/403/404/200'

    Write-Output '== Báo cáo lớp'
    $r = Call 'GET' "/web/teacher/classes/$class/analysis" $tokenA
    Expect $r.status 200 'teacher A'; $d = $r.body.data
    Expect "$($d.scheduleCount)/$($d.notCountedCount)/$($d.attemptCount)/$($d.attemptsWithoutOutcomes)/$($d.studentCount)" '2/1/20/0/10' 'scope counts (sC chưa công bố bị loại)'
    Expect "$($d.questionCount)/$($d.maxAnswersPerQuestion)" '6/10' 'questions and max answers'
    $c1 = @($d.chapters | Where-Object { $_.name -eq "T12 C1 $tag" })[0]; $c2 = @($d.chapters | Where-Object { $_.name -eq "T12 C2 $tag" })[0]
    Expect "$($c1.total)/$($c1.correct)/$([decimal]$c1.rate)/$($c1.studentCount)/$($c1.weakCount)" '50/24/48/10/5' 'C1 (lượt được tính: S1 lấy lượt hòa nộp sau; Yếu = S5..S9)'
    Expect ((@($c1.weakStudents | ForEach-Object { $_.userName }) | Sort-Object) -join ',') ((@(5..9 | ForEach-Object { "t12s$_$tag" }) | Sort-Object) -join ',') 'weak names'
    Expect "$($c2.total)/$($c2.correct)/$([decimal]$c2.rate)/$($c2.weakCount)" '10/10/100/0' 'C2'
    Expect @($d.hardQuestions).Count 1 'only q1 is hard (q2 = 40% biên)'
    $h = $d.hardQuestions[0]
    Expect "$($h.questionId)/$($h.answers)/$($h.correct)/$([decimal]$h.rate)/$($h.distractor)/$($h.distractorCount)" "$q1/10/2/20/D1-$tag/5" 'hard q1 + distractor D1 theo đề lúc thi (D1 đã thành đúng trong ngân hàng)'
    Expect "$($h.chapterName)/$($h.content)/$($h.hidden)" "T12 C1 $tag/T12 $tag câu q1/True" 'question info: chương lúc thi (C1), câu đã ẩn'
    if ($r.text -match [regex]::Escape("OK-$tag") -or $r.text -match 'isCorrect|snapshotJson|selectedAnswer') { throw 'Report leaked correct answer / raw data' }
    Pass 'theo chương + Yếu đúng; câu hay sai < 40% với ≥ 10 lượt; đáp án nhiễu đúng; không lộ đáp án đúng'
    Write-Output "TOBE_T12_CLASS_REPORT_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    if ($exam -gt 0) { Sql "DELETE dbo.QuestionOutcomes WHERE SourceId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam);
      DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" | Out-Null }
    if ($class -gt 0) { Sql "DELETE dbo.ClassUsers WHERE ClassId=$class; DELETE dbo.Classes WHERE Id=$class;" | Out-Null }
    if ($subject -gt 0) { Sql "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject;
      DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Chapters WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" | Out-Null }
    $users = (@($seed, $teacherA, $teacherB, $admin, $outsider) + $clones | ForEach-Object { "'$_'" }) -join ','
    Sql "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);
      DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users);
      DELETE dbo.Users WHERE Id IN ($users) OR Email=N't12seed$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
