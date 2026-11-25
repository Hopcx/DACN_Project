# Đợt tổng kiểm (QTK-4): seed dữ liệu lớn lên DB bản sao, đo p50/p95 các endpoint nặng, rồi dọn sạch.
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242', [int]$Runs = 20, [int]$ThresholdMs = 2000,
      [string]$ResultPath = '')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao. Không nằm trong RunUatSuite (chạy lâu).
# Dữ liệu: 500 câu loại 1–6, 20 đoạn có media, 80 câu có media, 300 media mồ côi; 5 lớp × 40 học viên;
# 10 lịch đã kết thúc (mỗi lịch giao cho cả 5 lớp, 200 lượt/lịch, 20 câu/lượt); 200 phiên luyện tập (outcome, 20 câu/phiên).
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -f 65001 -W -h -1 -t 600 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51241, 'Wrong database', 1; $query"
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
function Call([string]$method, [string]$url, [string]$token = '', $body = $null, $form = $null) {
    $a = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($token) { $a.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $body) { $a.ContentType = 'application/json'; $a.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 8 -Compress)) }
    if ($null -ne $form) { $a.Form = $form }
    $r = Invoke-WebRequest @a
    $json = $r.Headers['Content-Type'] -match 'json'
    return @{ status = [int]$r.StatusCode; body = if ($json) { [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) | ConvertFrom-Json } else { $null }; bytes = $r.RawContentLength }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected '$expected', got '$actual'" } }
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    $n = Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'PF $name',N'pf$name$tag',N'pf$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';
      SELECT @@ROWCOUNT;"
    if ($n -ne '1') { throw "CloneUser ${name}: chèn $n dòng (người dùng gốc không có trong $Database?)" }
    return "pf$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }
function Png([int]$w, [int]$h) {
    $b = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0,0,13,0x49,0x48,0x44,0x52) + [BitConverter]::GetBytes([uint32]$w)[3..0] + [BitConverter]::GetBytes([uint32]$h)[3..0]
    return [byte[]]($b + [Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
}
$results = [Collections.Generic.List[object]]::new()
function Bench([string]$name, [scriptblock]$call, [scriptblock]$after = $null) {
    $null = & $call 0    # khởi động (không tính)
    if ($after) { & $after }
    $times = [Collections.Generic.List[double]]::new()
    for ($i = 1; $i -le $Runs; $i++) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $r = & $call $i
        $sw.Stop()
        if ($r.status -notin 200, 201) { throw "$name lần $i trả $($r.status) $($r.body.message)" }
        $times.Add($sw.Elapsed.TotalMilliseconds)
        if ($after) { & $after }
    }
    $sorted = @($times | Sort-Object)
    $p50 = $sorted[[Math]::Ceiling(0.50 * $sorted.Count) - 1]; $p95 = $sorted[[Math]::Ceiling(0.95 * $sorted.Count) - 1]
    $row = [pscustomobject]@{ Endpoint = $name; P50 = [Math]::Round($p50); P95 = [Math]::Round($p95); Max = [Math]::Round($sorted[-1]); Pass = $p95 -lt $ThresholdMs }
    $results.Add($row)
    Write-Output ("  {0,-46} p50 {1,5} ms  p95 {2,5} ms  max {3,5} ms  {4}" -f $name, $row.P50, $row.P95, $row.Max, $(if ($row.Pass) { 'PASS' } else { 'FAIL' }))
}

$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-perf-$tag"; New-Item -ItemType Directory $work | Out-Null
$mediaRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location) 'dacn-media-dev'))
$seed = [Guid]::Empty; $teacher = [Guid]::NewGuid(); $examiner = [Guid]::NewGuid(); $admin = [Guid]::NewGuid()
$subject = 0; $exam1 = 0; $exam2 = 0; $mail = $null
try {
    $email = "pfseed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'PF seed'; userName = "pfseed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    # API phải trỏ vào đúng DB bản sao: tài khoản vừa đăng ký qua API phải thấy được qua sqlcmd trước mọi bước ghi khác
    if ((Sql "SELECT COUNT(*) FROM dbo.Users WHERE Id='$seed'") -ne '1') { throw "API không trỏ vào $Database (không thấy user $seed). Dừng, không seed." }
    $teacherToken = LoginToken (CloneUser $teacher 3 'teacher' $seed)
    $examinerEmail = CloneUser $examiner 2 'examiner' $seed
    Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',1),('$examiner',2);" | Out-Null
    $examinerToken = LoginToken $examinerEmail
    $adminToken = LoginToken (CloneUser $admin 1 'admin' $seed)

    Write-Output '== Seed: ngân hàng câu hỏi, lớp, học viên'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Subjects(Name,Status) VALUES (N'PF môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
DECLARE @k int=1;
WHILE @k <= 5 BEGIN INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,CONCAT(N'PF C',@k,N' $tag'),@k,1,SYSUTCDATETIME()); SET @k+=1; END
DECLARE @n int=1, @q int, @t int, @ch int;
WHILE @n <= 500 BEGIN
  SET @t = CASE WHEN @n<=80 THEN 1 WHEN @n<=280 THEN 2 WHEN @n<=360 THEN 3 WHEN @n<=420 THEN 4 WHEN @n<=460 THEN 5 ELSE 6 END;
  SELECT @ch=Id FROM dbo.Chapters WHERE SubjectId=@s AND SortOrder=(@n % 5)+1;
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,ChapterId,QuestionLevelId)
  VALUES (CONCAT(N'PF $tag câu ',@n,N' — ',REPLICATE(N'nội dung ',20)),SYSUTCDATETIME(),1,@s,@t,@ch,(@n % 3)+1);
  SET @q=SCOPE_IDENTITY();
  IF @t=1 INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'Đúng',1,1),(@q,N'Sai',0,1);
  IF @t=2 INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'D1',0,1),(@q,N'D2',0,1),(@q,N'D3',0,1);
  IF @t=3 INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK1',1,1),(@q,N'OK2',1,1),(@q,N'D1',0,1),(@q,N'D2',0,1);
  IF @t=4 INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,CONCAT(N'đáp án ',@n),1,1),(@q,CONCAT(N'dap an ',@n),1,1);
  IF @t=5 INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status,MatchContent) VALUES (@q,N'A',1,1,N'1'),(@q,N'B',1,1,N'2'),(@q,N'C',1,1,N'3');
  IF @t=6 INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'Mệnh đề 1',1,1),(@q,N'Mệnh đề 2',0,1),(@q,N'Mệnh đề 3',1,1);
  SET @n+=1;
END
DECLARE @ST TABLE(N int IDENTITY(1,1), Id uniqueidentifier);
INSERT @ST(Id) SELECT TOP (199) NEWID() FROM sys.all_objects;
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT x.Id,CONCAT(N'PF học viên ',x.N),CONCAT(N'pfs',x.N,N'$tag'),CONCAT(N'pfs',x.N,N'$tag@example.invalid'),u.PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1
FROM @ST x CROSS JOIN dbo.Users u WHERE u.Id='$seed';
SET @k=1;
WHILE @k <= 5 BEGIN
  INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (CONCAT(N'PF lớp ',@k,N' $tag'),CONCAT(N'PF',@k,N'-$tag'),50,'$teacher',@s,1);
  SET @k+=1;
END
-- Lớp 1: học viên seed + 39 học viên; lớp 2–5: mỗi lớp 40 học viên
INSERT dbo.ClassUsers(ClassId,UserId,Status) SELECT c.Id,'$seed',1 FROM dbo.Classes c WHERE c.ClassCode=N'PF1-$tag';
INSERT dbo.ClassUsers(ClassId,UserId,Status)
SELECT c.Id,x.Id,1 FROM @ST x JOIN dbo.Classes c ON c.ClassCode=CONCAT(N'PF',((x.N) / 40) + 1,N'-$tag') AND c.SubjectId=@s;
COMMIT;
SELECT CONCAT(@s,'|',(SELECT Id FROM dbo.Classes WHERE ClassCode=N'PF1-$tag'));
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $class1 = [int]$p[1]
    Expect (Sql "SELECT CONCAT((SELECT COUNT(*) FROM dbo.Questions WHERE SubjectId=$subject),'|',(SELECT COUNT(*) FROM dbo.ClassUsers cu JOIN dbo.Classes c ON c.Id=cu.ClassId WHERE c.SubjectId=$subject))") '500|200' 'seed counts'
    # Lấy theo từng dòng: sqlcmd cắt cột nvarchar(max) ở 256 ký tự
    $qIds = @((Sql "SELECT Id FROM dbo.Questions WHERE SubjectId=$subject ORDER BY Id") -split '\r?\n' | ForEach-Object { [int]$_ })
    Expect $qIds.Count 500 'question ids'

    Write-Output '== Seed: 20 đoạn dùng chung có media (qua API), 80 câu có media, 300 media mồ côi'
    $media = @()
    for ($i = 1; $i -le 20; $i++) {
        $png = Join-Path $work "g$i.png"; [IO.File]::WriteAllBytes($png, (Png (300 + $i) 200))
        $up = Call 'POST' '/web/media' $examinerToken $null @{ file = Get-Item $png }
        if ($up.status -notin 200, 201) { throw "upload $($up.status)" }
        $media += [string]$up.body.data.id
        # câu 101..160 (loại 2) → 20 đoạn × 3 câu
        $children = @($qIds[(100 + ($i - 1) * 3)..(102 + ($i - 1) * 3)])
        $g = Call 'POST' '/web/question-groups' $examinerToken @{ subjectId = $subject; title = "PF đoạn $i $tag"; content = ('Đọc đoạn văn sau. ' * 40); questionIds = $children }
        if ($g.status -ne 201) { throw "group ${i}: $($g.status) $($g.body.message)" }
        Expect (Call 'POST' "/web/question-groups/$($g.body.data.id)/media" $examinerToken @{ mediaId = $media[-1] }).status 200 "group media $i"
    }
    $mediaValues = ((0..79) | ForEach-Object { "($($qIds[199 + $_]),'$($media[$_ % 20])',1)" }) -join ','
    Sql "INSERT dbo.QuestionMedia(QuestionId,MediaId,SortOrder) VALUES $mediaValues;
      INSERT dbo.MediaFiles(Id,Kind,ContentType,Extension,SizeBytes,Sha256,Width,Height,OriginalName,UploadedBy,UploadedAtUtc,Status)
      SELECT TOP (300) NEWID(),N'image',N'image/png',N'.png',1000,LOWER(CONVERT(nvarchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(36),NEWID())),2)),10,10,N'pf-orphan.png','$examiner',DATEADD(day,-3,SYSUTCDATETIME()),1
      FROM sys.all_objects;" | Out-Null

    Write-Output '== Seed: 10 lịch đã kết thúc × 200 lượt × 20 câu, 200 phiên luyện tập × 20 câu'
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @s int=$subject, @user uniqueidentifier='$examiner';
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES (N'PF đề cũ $tag',@s,20,1,1,20,10,30,1); DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'PF',1,SYSUTCDATETIME(),@user,SYSUTCDATETIME(),@user);
DECLARE @v int=SCOPE_IDENTITY();
DECLARE @QT TABLE(N int, QId int, ChapterId int, OkId int, D1Id int);
INSERT @QT SELECT ROW_NUMBER() OVER (ORDER BY q.Id), q.Id, q.ChapterId,
  (SELECT MIN(Id) FROM dbo.Answers WHERE QuestionId=q.Id AND IsCorrect=1), (SELECT MIN(Id) FROM dbo.Answers WHERE QuestionId=q.Id AND IsCorrect=0)
FROM (SELECT TOP (20) * FROM dbo.Questions WHERE SubjectId=@s AND QuestionTypeId=2 AND GroupId IS NULL ORDER BY Id) q;
DECLARE @snap nvarchar(max)=(SELECT 3 AS version, (SELECT 1 AS point, q.QId AS sourceQuestionId,
  (SELECT x.Id AS sourceAnswerId, CAST(x.Ok AS bit) AS isCorrect FROM (VALUES (q.OkId,1),(q.D1Id,0)) x(Id,Ok) FOR JSON PATH) AS options
  FROM @QT q ORDER BY q.N FOR JSON PATH) AS questions FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
DECLARE @k int=1;
WHILE @k <= 10 BEGIN
  INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc)
  VALUES (@e,CONCAT(N'PF lịch ',@k),DATEADD(day,-30+@k,SYSUTCDATETIME()),DATEADD(hour,2,DATEADD(day,-30+@k,SYSUTCDATETIME())),1,1);
  SET @k+=1;
END
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT c.Id,s.Id FROM dbo.Classes c CROSS JOIN dbo.ExamSchedules s WHERE c.SubjectId=@s AND s.ExamId=@e;
CREATE TABLE #A(Id uniqueidentifier PRIMARY KEY, U uniqueidentifier, Sch int, StartedAt datetime2);
INSERT #A SELECT NEWID(), cu.UserId, s.Id, s.StartTime FROM dbo.ClassUsers cu JOIN dbo.Classes c ON c.Id=cu.ClassId CROSS JOIN dbo.ExamSchedules s WHERE c.SubjectId=@s AND s.ExamId=@e;
CREATE TABLE #O(AId uniqueidentifier, N int, Ok bit);
INSERT #O SELECT a.Id, q.N, CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 60 THEN 1 ELSE 0 END FROM #A a CROSS JOIN @QT q;
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
SELECT a.Id,a.U,a.Sch,@e,@v,a.StartedAt,DATEADD(minute,30,a.StartedAt),DATEADD(minute,20,a.StartedAt),N'submitted',0,@snap,N'[]',10,1 FROM #A a;
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson)
SELECT a.Id,DATEADD(minute,20,a.StartedAt),1200,t.Total,CASE WHEN t.Total>=10 THEN 1 ELSE 0 END,N'[]'
FROM #A a CROSS APPLY (SELECT SUM(CAST(o.Ok AS int)) AS Total FROM #O o WHERE o.AId=a.Id) t;
INSERT dbo.QuestionOutcomes(SourceKind,SourceId,SnapshotQuestionId,UserId,SubjectId,ExamScheduleId,SourceQuestionId,ChapterId,TagIdsJson,IsCorrect,IsAnswered,SelectedAnswerIdsJson,FinishedAtUtc,ClassificationSource)
SELECT N'exam',a.Id,NEWID(),a.U,@s,a.Sch,q.QId,q.ChapterId,N'[]',o.Ok,1,CONCAT('[',CASE WHEN o.Ok=1 THEN q.OkId ELSE q.D1Id END,']'),DATEADD(minute,20,a.StartedAt),N'snapshot'
FROM #O o JOIN #A a ON a.Id=o.AId JOIN @QT q ON q.N=o.N;
-- 200 phiên luyện tập: 50 của học viên seed, 150 rải cho các học viên khác; mỗi phiên 20 câu bất kỳ của môn
CREATE TABLE #P(Id uniqueidentifier, U uniqueidentifier, At datetime2);
INSERT #P SELECT TOP (50) NEWID(), '$seed', DATEADD(hour,-ROW_NUMBER() OVER (ORDER BY (SELECT 1))*10,SYSUTCDATETIME()) FROM sys.all_objects;
INSERT #P SELECT TOP (150) NEWID(), cu.UserId, DATEADD(hour,-ABS(CHECKSUM(NEWID())) % 1400,SYSUTCDATETIME())
FROM dbo.ClassUsers cu JOIN dbo.Classes c ON c.Id=cu.ClassId WHERE c.SubjectId=@s AND cu.UserId<>'$seed' ORDER BY NEWID();
INSERT dbo.QuestionOutcomes(SourceKind,SourceId,SnapshotQuestionId,UserId,SubjectId,ExamScheduleId,SourceQuestionId,ChapterId,TagIdsJson,IsCorrect,IsAnswered,SelectedAnswerIdsJson,FinishedAtUtc,ClassificationSource)
SELECT N'practice',p.Id,NEWID(),p.U,@s,NULL,q.Id,q.ChapterId,N'[]',CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 55 THEN 1 ELSE 0 END,1,N'[]',p.At,N'snapshot'
FROM #P p CROSS APPLY (SELECT TOP (20) Id, ChapterId FROM dbo.Questions WHERE SubjectId=@s ORDER BY CHECKSUM(NEWID(), p.Id)) q;
-- Đề mới cho bài đo tạo mã đề ngẫu nhiên và bắt đầu bài thi (không dùng dấu nháy kép: sqlcmd -Q)
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES (N'PF đề mới $tag',@s,20,1,2,20,10,30,1);
COMMIT;
SELECT CONCAT(@e,'|',SCOPE_IDENTITY());
"@
    $p = $ids.Split('|'); $exam1 = [int]$p[0]; $exam2 = [int]$p[1]
    $counts = Sql "SELECT CONCAT((SELECT COUNT(*) FROM dbo.ExamAttempts WHERE ExamId=$exam1),'|',(SELECT COUNT(*) FROM dbo.QuestionOutcomes WHERE SubjectId=$subject))"
    Expect $counts '2000|44000' 'attempts/outcomes'
    $sw.Stop(); Write-Output "  seed xong sau $([Math]::Round($sw.Elapsed.TotalSeconds)) s: 500 câu, 20 đoạn, 200 học viên, 2000 lượt, 44000 outcome"

    Write-Output "== Đo ($Runs lần mỗi endpoint, sau 1 lần khởi động; ngưỡng p95 < $ThresholdMs ms)"
    Bench 'GET teacher/classes/{id}/ranking' { Call 'GET' "/web/teacher/classes/$class1/ranking" $teacherToken }
    Bench 'GET teacher/classes/{id}/results' { Call 'GET' "/web/teacher/classes/$class1/results" $teacherToken }
    Bench 'GET teacher/classes/{id}/analysis' { Call 'GET' "/web/teacher/classes/$class1/analysis" $teacherToken }
    Bench 'GET teacher/classes/{id}/report.xlsx' { Call 'GET' "/web/teacher/classes/$class1/report.xlsx" $teacherToken }
    Bench 'GET student/classes/{id}/ranking' { Call 'GET' "/web/student/classes/$class1/ranking" $studentToken }
    Bench 'GET student/analysis?days=365' { Call 'GET' '/web/student/analysis?days=365' $studentToken }
    $script:lastPractice = $null
    Bench 'POST student/practice (50 câu)' {
        $r = Call 'POST' '/web/student/practice' $studentToken @{ subjectId = $subject; questionCount = 50; revealMode = 'end'; kind = 'practice' }
        $script:lastPractice = $r.body.data.id; $r
    } { if ($script:lastPractice) { Expect (Call 'DELETE' "/web/student/practice/$($script:lastPractice)" $studentToken).status 200 'discard practice' } }
    Bench 'GET student/practice/mine' { Call 'GET' '/web/student/practice/mine' $studentToken }
    Bench 'POST exams/{id}/variants (ngẫu nhiên 20, có đoạn)' { param($i) Call 'POST' "/web/exams/$exam2/variants" $examinerToken @{ code = "R$i"; status = 2; questionIds = @(); randomSelections = @(@{ count = 20 }) } }
    $groupedVariants = Sql "SELECT COUNT(DISTINCT d.Id) FROM dbo.ExamDetails d JOIN dbo.ExamDetailQuestions x ON x.ExamDetailId=d.Id JOIN dbo.Questions q ON q.Id=x.QuestionId WHERE d.ExamId=$exam2 AND q.GroupId IS NOT NULL"
    Write-Output "  ($groupedVariants/$($Runs + 1) mã đề có đoạn)"
    $schedules = @((Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE dbo.Exams SET Status=1 WHERE Id=$exam2; UPDATE dbo.ExamDetails SET Status=1 WHERE ExamId=$exam2;
DECLARE @k int=0;
WHILE @k <= $Runs BEGIN
  INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES ($exam2,CONCAT(N'PF mở ',@k),DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(hour,1,SYSUTCDATETIME()),1,1);
  INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES ($class1,SCOPE_IDENTITY());
  SET @k+=1;
END
COMMIT;
SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam2 ORDER BY Id;
"@) -split '\r?\n' | ForEach-Object { [int]$_ })
    Bench 'POST exam-schedules/{id}/attempts (bắt đầu)' { param($i) Call 'POST' "/web/exam-schedules/$($schedules[$i])/attempts" $studentToken }
    Bench 'GET attempts/mine' { Call 'GET' '/web/attempts/mine' $studentToken }
    Bench 'GET admin/media/orphans' { Call 'GET' '/web/admin/media/orphans?page=1&pageSize=50' $adminToken }
    Bench 'GET questions?subjectId (danh sách 500 câu)' { Call 'GET' "/web/questions?subjectId=$subject" $examinerToken }
    Bench 'GET exams/{id}/question-options' { Call 'GET' "/web/exams/$exam2/question-options" $examinerToken }

    $failed = @($results | Where-Object { -not $_.Pass })
    if ($ResultPath) { $results | ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding utf8 }
    if ($failed.Count -gt 0) { throw "PERF: vượt ngưỡng p95: $(($failed | ForEach-Object Endpoint) -join ', ')" }
    Write-Output "TOBE_PERF_BENCHMARK=PASS;ENDPOINTS=$($results.Count);RUNS=$Runs;DB=$Database;TAG=$tag"
}
finally {
    Write-Output '== Dọn dữ liệu'
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    # Truy vấn con, không dựng danh sách Id: hơn 200 học viên vượt giới hạn chuỗi của STRING_AGG/sqlcmd
    $users = "SELECT Id FROM dbo.Users WHERE Id IN ('$seed','$teacher','$examiner','$admin') OR UserName LIKE N'pf%$tag' OR Email=N'pfseed$tag@example.invalid'"
    $mediaIds = try { Sql "SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner';" } catch { '' }
    Clean "DELETE dbo.MediaReferences WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner');"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    # Tìm lại theo tag nếu bước tách id lỗi sau khi batch đã COMMIT
    if ($subject -le 0) { $subject = [int](Sql "SELECT ISNULL(MAX(Id),0) FROM dbo.Subjects WHERE Name=N'PF môn $tag'") }
    $examIds = @((Sql "SELECT Id FROM dbo.Exams WHERE Name IN (N'PF đề cũ $tag',N'PF đề mới $tag')") -split '\r?\n' | Where-Object { $_ } | ForEach-Object { [int]$_ })
    foreach ($e in @(@($exam1, $exam2) + $examIds | Where-Object { $_ -gt 0 } | Select-Object -Unique)) {
        Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$e);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$e); DELETE dbo.ExamAttempts WHERE ExamId=$e;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$e); DELETE dbo.ExamSchedules WHERE ExamId=$e;
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$e); DELETE dbo.ExamDetails WHERE ExamId=$e; DELETE dbo.Exams WHERE Id=$e;"
    }
    if ($subject -gt 0) {
        Clean "DELETE gm FROM dbo.QuestionGroupMedia gm JOIN dbo.QuestionGroups g ON g.Id=gm.GroupId WHERE g.SubjectId=$subject;
      UPDATE dbo.Questions SET GroupId=NULL, GroupOrder=NULL WHERE SubjectId=$subject; DELETE dbo.QuestionGroups WHERE SubjectId=$subject;
      DELETE qm FROM dbo.QuestionMedia qm JOIN dbo.Questions q ON q.Id=qm.QuestionId WHERE q.SubjectId=$subject;
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject;
      DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE SubjectId=$subject); DELETE dbo.Classes WHERE SubjectId=$subject;
      DELETE dbo.Chapters WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;"
    }
    Clean "DELETE dbo.QuestionMedia WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner'); DELETE dbo.MediaFiles WHERE UploadedBy='$examiner';"
    foreach ($id in ("$mediaIds" -split '\r?\n' | Where-Object { $_ })) {
        $n2 = $id.Replace('-', '').ToLowerInvariant(); Remove-Item -LiteralPath (Join-Path (Join-Path $mediaRoot $n2.Substring(0, 2)) $n2) -ErrorAction SilentlyContinue
    }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    $left = try { Sql "SELECT CONCAT((SELECT COUNT(*) FROM dbo.Users WHERE UserName LIKE N'pf%$tag'),'|',(SELECT COUNT(*) FROM dbo.Subjects WHERE Name=N'PF môn $tag'),'|',(SELECT COUNT(*) FROM dbo.MediaFiles WHERE UploadedBy='$examiner'),'|',(SELECT COUNT(*) FROM dbo.Exams WHERE Name LIKE N'PF đề % $tag'),'|',(SELECT COUNT(*) FROM dbo.Classes WHERE ClassCode LIKE N'PF_-$tag'),'|',(SELECT COUNT(*) FROM dbo.Questions WHERE Content LIKE N'PF $tag câu %'),'|',(SELECT COUNT(*) FROM dbo.Logs WHERE ActorUserId IN ('$seed','$teacher','$examiner','$admin')))" } catch { '?' }
    Write-Output "  còn lại (users|subjects|media|exams|classes|questions|logs): $left"
    if ($left -ne '0|0|0|0|0|0|0') { Write-Warning 'Còn dữ liệu seed chưa dọn — kiểm tra theo TAG.' }
}
