# P4 - GET /web/teacher/overview: pham vi lop cua giang vien, khong nhan doi luot thi, Admin thay moi lop.
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao đã áp migration P4AuditLogColumns.
# Chỉ ghi vào DB bản sao; fixture gắn tag ngẫu nhiên và được dọn trong finally.
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
# Gọi API; trả status, body JSON (nếu có) và bytes thô (cho tệp xlsx).
function Call([string]$method, [string]$url, [string]$token = '', $json = $null, $form = $null) {
    $args2 = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($token) { $args2.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $json) { $args2.ContentType = 'application/json'; $args2.Body = ($json | ConvertTo-Json -Compress) }
    if ($null -ne $form) { $args2.Form = $form }
    $r = Invoke-WebRequest @args2
    $text = if ($r.Headers['Content-Type'] -match 'json') { $r.Content } else { $null }
    return @{ status = [int]$r.StatusCode; body = if ($text) { $text | ConvertFrom-Json } else { $null };
        bytes = $r.RawContentStream.ToArray(); type = [string]($r.Headers['Content-Type'] | Select-Object -First 1) }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
# Tạo user bằng cách sao PasswordHash của user seed đã đăng ký; trả email để đăng nhập.
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'P4 $name',N'p4$name$tag',N'p4$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "p4$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }
function FindMail([string]$email) {
    return Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
}
function MailToken($mail) { return [Uri]::UnescapeDataString([regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)').Groups[1].Value) }
# Đọc workbook xlsx: tên sheet, các dòng (mảng ô), có công thức không, và toàn bộ văn bản mọi entry (để dò rò rỉ).
function ReadBook([byte[]]$bytes) {
    $S = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($bytes))
    try {
        $texts = @{}
        foreach ($entry in $zip.Entries) { $reader = [IO.StreamReader]::new($entry.Open()); $texts[$entry.FullName] = $reader.ReadToEnd(); $reader.Dispose() }
        $sheetName = ([xml]$texts['xl/workbook.xml']).GetElementsByTagName('sheet')[0].GetAttribute('name')
        $xml = [xml]$texts['xl/worksheets/sheet1.xml']
        $ns = [Xml.XmlNamespaceManager]::new($xml.NameTable); $ns.AddNamespace('m', $S)
        $rows = @(foreach ($row in $xml.SelectNodes('//m:row', $ns)) { , @($row.SelectNodes('m:c', $ns) | ForEach-Object { $_.InnerText }) })
        return @{ sheet = $sheetName; rows = $rows; hasFormula = [bool]($texts['xl/worksheets/sheet1.xml'] -match '<f[ >]'); all = ($texts.Values -join "`n") }
    } finally { $zip.Dispose() }
}
$seed = [Guid]::Empty; $teacherA = [Guid]::NewGuid(); $teacherB = [Guid]::NewGuid(); $admin = [Guid]::NewGuid()
$pending = [Guid]::NewGuid(); $outsider = [Guid]::NewGuid(); $exam = 0; $class1 = 0; $class2 = 0; $mail = $null
try {
    $email = "p4toseed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'P4 Overview'; userName = "p4toseed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = FindMail $email
    Expect (Auth 'verify-email' @{ token = (MailToken $mail) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    # Học viên X = user seed: thành viên đã duyệt ở cả hai lớp của giảng viên A.
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $tokenA = LoginToken (CloneUser $teacherA 3 'toteachera' $seed)
    $tokenB = LoginToken (CloneUser $teacherB 3 'toteacherb' $seed)
    $adminToken = LoginToken (CloneUser $admin 1 'toadmin' $seed)
    CloneUser $pending 4 'topending' $seed | Out-Null
    CloneUser $outsider 4 'tooutsider' $seed | Out-Null

    # 2 lớp của A; X duyệt ở cả hai lớp; Y chờ duyệt ở lớp 1; 4 lịch đang mở cùng gán cho cả hai lớp.
    # Lượt của X: S1 đạt (8), S2 trượt (3), S3 đã hủy (7), S4 đang làm. Z (không phải thành viên) có 1 lượt đạt trên S1.
    $ids = Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'P4 overview $tag',@subject,1,0,1,10,5,60,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@exam,N'QA',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'P4 one $tag',N'P4A-$tag',10,'$teacherA',@subject,1);
DECLARE @class1 int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'P4 two $tag',N'P4B-$tag',10,'$teacherA',@subject,1);
DECLARE @class2 int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class1,'$seed',1),(@class2,'$seed',1),(@class1,'$pending',2);
DECLARE @now datetime2=SYSUTCDATETIME();
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES
(@exam,N'P4 s1',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1),(@exam,N'P4 s2',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1),
(@exam,N'P4 s3',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1),(@exam,N'P4 s4',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class1,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class2,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
DECLARE @s1 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P4 s1'),@s2 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P4 s2'),
 @s3 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P4 s3'),@s4 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P4 s4');
DECLARE @a1 uniqueidentifier=NEWID(),@a2 uniqueidentifier=NEWID(),@a3 uniqueidentifier=NEWID(),@a4 uniqueidentifier=NEWID(),@a5 uniqueidentifier=NEWID();
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult) VALUES
(@a1,'$seed',@s1,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'submitted',0,N'{}',N'[]',5,1),
(@a2,'$seed',@s2,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'submitted',0,N'{}',N'[]',5,1),
(@a3,'$seed',@s3,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'submitted',0,N'{}',N'[]',5,1),
(@a4,'$seed',@s4,@exam,@variant,@now,DATEADD(minute,30,@now),NULL,N'inProgress',0,N'{}',N'[]',5,1),
(@a5,'$outsider',@s1,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'submitted',0,N'{}',N'[]',5,1);
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson,VoidedAtUtc,VoidReason)
VALUES(@a1,@now,60,8,1,N'[]',NULL,NULL),(@a2,@now,60,3,0,N'[]',NULL,NULL),(@a3,@now,60,7,1,N'[]',@now,N'P4 QA'),(@a5,@now,60,9,1,N'[]',NULL,NULL);
SELECT CONCAT(@exam,',',@class1,',',@class2);
"@
    $parts = $ids.Split(','); $exam = [int]$parts[0]; $class1 = [int]$parts[1]; $class2 = [int]$parts[2]

    Write-Output '== Quyền và tham số'
    Expect (Call 'GET' '/web/teacher/overview').status 401 'anonymous'
    Expect (Call 'GET' '/web/teacher/overview' $studentToken).status 403 'student'
    foreach ($bad in 0, 51, -1) { Expect (Call 'GET' "/web/teacher/overview?upcomingLimit=$bad" $tokenA).status 400 "upcomingLimit=$bad" }
    Pass '401/403/400'

    Write-Output '== Giảng viên A (2 lớp)'
    $r = Call 'GET' '/web/teacher/overview' $tokenA
    Expect $r.status 200 'teacher A'; $d = $r.body.data
    Expect $d.scope 'dacnAttemptsOnly' 'scope'
    Expect $d.classes.total 2 'classes.total'; Expect $d.classes.active 2 'classes.active'
    Expect $d.members.approvedStudents 1 'approvedStudents (distinct across both classes)'; Expect $d.members.pendingRequests 1 'pendingRequests'
    Expect $d.schedules.openCount 4 'openCount'; Expect $d.schedules.upcomingCount 0 'upcomingCount'; Expect $d.schedules.unknownTimeZoneCount 0 'unknownTimeZoneCount'
    $sqlAttempts = [int](Sql @"
SELECT COUNT(*) FROM dbo.ExamAttempts a WHERE EXISTS (SELECT 1 FROM dbo.ClassExamSchedule cs JOIN dbo.Classes c ON c.Id=cs.ClassId
  WHERE c.TeacherId='$teacherA' AND cs.ExamScheduleId=a.ExamScheduleId
    AND EXISTS (SELECT 1 FROM dbo.ClassUsers cu WHERE cu.ClassId=cs.ClassId AND cu.UserId=a.UserId AND cu.Status=1));
"@)
    Expect $sqlAttempts 4 'SQL: distinct attempts of approved members (not doubled by 2 classes, outsider excluded)'
    $a = $d.attempts
    Expect $a.totalAttempts $sqlAttempts 'attempts.totalAttempts vs SQL'; Expect $a.inProgressCount 1 'inProgress'
    Expect $a.finalizedCount 2 'finalized'; Expect $a.passedCount 1 'passed'; Expect $a.failedCount 1 'failed'; Expect $a.voidedCount 1 'voided'
    Expect ([decimal]$a.averageScore) ([decimal]5.5) 'averageScore'; Expect ([decimal]$a.passPercent) ([decimal]50) 'passPercent'
    Expect @($d.perClass).Count 2 'perClass rows'
    foreach ($row in $d.perClass) {
        Expect $row.approvedStudents 1 "perClass approved $($row.classId)"
        Expect $row.pendingRequests ($(if ($row.classId -eq $class1) { 1 } else { 0 })) "perClass pending $($row.classId)"
        Expect $row.finalizedCount 2 "perClass finalized $($row.classId)"; Expect $row.passedCount 1 "perClass passed $($row.classId)"
        Expect ([decimal]$row.averageScore) ([decimal]5.5) "perClass average $($row.classId)"
    }
    Expect $d.perClassMeaning 'attemptMayCountInEveryOwnedClassOfStudent' 'perClassMeaning'
    Expect @($d.upcoming).Count 4 'upcoming'; foreach ($u in $d.upcoming) { Expect $u.scheduleState 'open' "state $($u.title)"; Expect @($u.classes).Count 2 "classes $($u.title)" }
    Expect @((Call 'GET' '/web/teacher/overview?upcomingLimit=2' $tokenA).body.data.upcoming).Count 2 'upcomingLimit=2'
    $text = [Text.Encoding]::UTF8.GetString($r.bytes)
    if ($text -match 'email|passwordHash|answersJson|snapshotJson|@example') { throw 'Overview leaked student email/answers/snapshot' }
    Pass 'A: số liệu khớp SQL, lượt không nhân đôi qua hai lớp, không lộ email/đáp án'

    Write-Output '== Giảng viên B (không có lớp), Admin'
    $b = (Call 'GET' '/web/teacher/overview' $tokenB)
    Expect $b.status 200 'teacher B'; $bd = $b.body.data
    Expect $bd.classes.total 0 'B classes'; Expect $bd.members.approvedStudents 0 'B approved'; Expect $bd.members.pendingRequests 0 'B pending'
    Expect $bd.attempts.totalAttempts 0 'B attempts'; Expect @($bd.perClass).Count 0 'B perClass'; Expect @($bd.upcoming).Count 0 'B upcoming'
    $ad = (Call 'GET' '/web/teacher/overview' $adminToken)
    Expect $ad.status 200 'admin'
    if ($ad.body.data.attempts.totalAttempts -lt 4 -or $ad.body.data.classes.total -lt 2 -or $ad.body.data.members.approvedStudents -lt 1) { throw 'Admin overview must cover at least the fixture classes' }
    Pass 'B: mọi số 0, mảng rỗng, vẫn 200; Admin 200 và thấy ít nhất dữ liệu của A'
    Write-Output "P4_TEACHER_OVERVIEW_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    if ($exam -gt 0) { Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam);
      DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" | Out-Null }
    if ($class1 -gt 0) { Sql "DELETE dbo.ClassUsers WHERE ClassId IN ($class1,$class2); DELETE dbo.Classes WHERE Id IN ($class1,$class2);" | Out-Null }
    $users = "'$seed','$teacherA','$teacherB','$admin','$pending','$outsider'"
    Sql "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users);
      DELETE dbo.Users WHERE Id IN ($users) OR Email=N'p4toseed$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
