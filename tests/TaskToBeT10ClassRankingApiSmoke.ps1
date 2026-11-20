# To-be T10 - Xep hang hoc vien trong lop: GET /web/teacher/classes/{id}/ranking va /web/student/classes/{id}/ranking.
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
      SELECT '$id',N'T10 $name',N't10$name$tag',N't10$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t10$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }
function FindMail([string]$email) {
    return Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
}
function MailToken($mail) { return [Uri]::UnescapeDataString([regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)').Groups[1].Value) }

$seed = [Guid]::Empty; $teacherA = [Guid]::NewGuid(); $teacherB = [Guid]::NewGuid(); $admin = [Guid]::NewGuid()
$st = 1..6 | ForEach-Object { [Guid]::NewGuid() }   # S1..S6 (S0 = seed)
$pending = [Guid]::NewGuid(); $outsider = [Guid]::NewGuid(); $exams = ''; $class = 0; $mail = $null
try {
    $email = "t10seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T10 Seed'; userName = "t10seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = FindMail $email
    Expect (Auth 'verify-email' @{ token = (MailToken $mail) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $s0Token = [string]$login.body.data.accessToken
    $tokenA = LoginToken (CloneUser $teacherA 3 'teachera' $seed)
    $tokenB = LoginToken (CloneUser $teacherB 3 'teacherb' $seed)
    $adminToken = LoginToken (CloneUser $admin 1 'admin' $seed)
    for ($i = 0; $i -lt 6; $i++) { CloneUser $st[$i] 4 "s$($i + 1)" $seed | Out-Null }
    $s6Token = LoginToken "t10s6$tag@example.invalid"
    $pendingToken = LoginToken (CloneUser $pending 4 'pending' $seed)
    $outsiderToken = LoginToken (CloneUser $outsider 4 'outsider' $seed)

    # Lịch của lớp:
    #   sA (E1, đã kết thúc, snapshot tối đa 10) - tính
    #   sB (E2, đã kết thúc, snapshot tối đa 20, MaximmumMark ghi sai 100) - tính, mẫu số = 20 (QT10-2)
    #   sC (E1, chưa kết thúc) - notEnded; sD (E1, đã kết thúc, có lượt AllowViewResult=0) - resultNotReleased
    #   sE (E3 AllowViewResult=0, đã kết thúc, không lượt) - resultNotReleased
    # Điểm thang 10: S0 sA 10 (2 lượt: 6 và 10), sB 8 -> 9 | S1 9,9 -> 9 | S2 6,6 -> 6 | S3 5,5 -> 5
    #                S4 sA 2 (lượt 8 bị hủy), sB không làm -> 1 | S5 4,4 -> 4 | S6 không làm -> chưa có điểm
    $ids = Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T10 E1 $tag',@subject,2,0,1,10,5,60,1);
DECLARE @e1 int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T10 E2 $tag',@subject,2,0,1,100,10,60,1);
DECLARE @e2 int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T10 E3 $tag',@subject,2,0,1,10,5,60,0);
DECLARE @e3 int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@e1,N'QA',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
DECLARE @v1 int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@e2,N'QA',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
DECLARE @v2 int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'T10 class $tag',N'T10-$tag',20,'$teacherA',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,'$seed',1),(@class,'$($st[0])',1),(@class,'$($st[1])',1),(@class,'$($st[2])',1),
 (@class,'$($st[3])',1),(@class,'$($st[4])',1),(@class,'$($st[5])',1),(@class,'$pending',2);
DECLARE @now datetime2=SYSUTCDATETIME(), @past datetime2=DATEADD(hour,-3,SYSUTCDATETIME()), @ended datetime2=DATEADD(hour,-1,SYSUTCDATETIME());
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES
(@e1,N'T10 sA',@past,@ended,1,1),(@e2,N'T10 sB',@past,DATEADD(minute,10,@ended),1,1),(@e1,N'T10 sC',@past,DATEADD(hour,2,@now),1,1),
(@e1,N'T10 sD',@past,@ended,1,1),(@e3,N'T10 sE',@past,@ended,1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId IN (@e1,@e2,@e3);
DECLARE @sA int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@e1 AND Title=N'T10 sA'),@sB int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@e2),
 @sC int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@e1 AND Title=N'T10 sC'),@sD int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@e1 AND Title=N'T10 sD');
DECLARE @snap10 nvarchar(max)=REPLACE(N'{~version~:2,~questions~:[{~point~:4},{~point~:6}]}',N'~',NCHAR(34)), @snap20 nvarchar(max)=REPLACE(N'{~version~:2,~questions~:[{~point~:10},{~point~:10}]}',N'~',NCHAR(34)),
 @snapEmpty nvarchar(max)=REPLACE(N'{~version~:2,~questions~:[]}',N'~',NCHAR(34));
-- Review T10: S2 làm sA bằng mã đề tổng 20 (12/20 = 6); S6 có lượt snapshot tổng 0 (bị bỏ, skipped=1);
-- lượt đã hủy có AllowViewResult=0 trong sA không chặn sA.
DECLARE @t TABLE(U uniqueidentifier, S int, V int, Snap nvarchar(max), Mark decimal(18,2), Passed bit, Voided bit, Allow bit);
INSERT @t VALUES
('$seed',@sA,@v1,@snap10,6,1,0,1),('$seed',@sA,@v1,@snap10,10,1,0,1),('$seed',@sB,@v2,@snap20,16,1,0,1),
('$($st[0])',@sA,@v1,@snap10,9,1,0,1),('$($st[0])',@sB,@v2,@snap20,18,1,0,1),
('$($st[1])',@sA,@v2,@snap20,12,1,0,1),('$($st[5])',@sA,@v1,@snapEmpty,0,0,0,1),('$($st[0])',@sA,@v1,@snap10,1,0,1,0),('$($st[1])',@sB,@v2,@snap20,12,1,0,1),
('$($st[2])',@sA,@v1,@snap10,5,1,0,1),('$($st[2])',@sB,@v2,@snap20,10,1,0,1),
('$($st[3])',@sA,@v1,@snap10,8,1,1,1),('$($st[3])',@sA,@v1,@snap10,2,0,0,1),
('$($st[4])',@sA,@v1,@snap10,4,0,0,1),('$($st[4])',@sB,@v2,@snap20,8,0,0,1),
('$outsider',@sA,@v1,@snap10,10,1,0,1),('$pending',@sA,@v1,@snap10,10,1,0,1),
('$seed',@sC,@v1,@snap10,1,0,0,1),('$seed',@sD,@v1,@snap10,1,0,0,0),('$($st[0])',@sD,@v1,@snap10,10,1,0,1);
DECLARE @rows TABLE(Id uniqueidentifier, U uniqueidentifier, S int, V int, Snap nvarchar(max), Mark decimal(18,2), Passed bit, Voided bit, Allow bit);
INSERT @rows SELECT NEWID(),* FROM @t;
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
SELECT r.Id,r.U,r.S,s.ExamId,r.V,@past,DATEADD(minute,30,@past),DATEADD(minute,20,@past),N'submitted',0,r.Snap,N'[]',5,r.Allow
FROM @rows r JOIN dbo.ExamSchedules s ON s.Id=r.S;
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson,VoidedAtUtc,VoidReason)
SELECT Id,DATEADD(minute,20,@past),60,Mark,Passed,N'[]',CASE WHEN Voided=1 THEN @now END,CASE WHEN Voided=1 THEN N'T10 QA' END FROM @rows;
SELECT CONCAT(@e1,',',@e2,',',@e3,'|',@class,'|',@sA,',',@sB);
"@
    $parts = $ids.Split('|'); $exams = $parts[0]; $class = [int]$parts[1]; $sA = [int]$parts[2].Split(',')[0]; $sB = [int]$parts[2].Split(',')[1]

    Write-Output '== Quyền'
    Expect (Call 'GET' "/web/teacher/classes/$class/ranking").status 401 'anonymous teacher'
    Expect (Call 'GET' "/web/student/classes/$class/ranking").status 401 'anonymous student'
    Expect (Call 'GET' "/web/teacher/classes/$class/ranking" $s0Token).status 403 'student on teacher endpoint'
    Expect (Call 'GET' "/web/teacher/classes/$class/ranking" $tokenB).status 404 'other teacher'
    Expect (Call 'GET' '/web/teacher/classes/2147483000/ranking' $tokenA).status 404 'missing class'
    Expect (Call 'GET' "/web/student/classes/$class/ranking" $outsiderToken).status 404 'outsider student'
    Expect (Call 'GET' "/web/student/classes/$class/ranking" $pendingToken).status 404 'pending student'
    Expect (Call 'GET' "/web/teacher/classes/$class/ranking" $adminToken).status 200 'admin'
    Pass '401/403/404 đúng; Admin 200'

    Write-Output '== Bảng giảng viên'
    $r = Call 'GET' "/web/teacher/classes/$class/ranking" $tokenA
    Expect $r.status 200 'teacher A'; $d = $r.body.data
    Expect $d.scale 10 'scale'; Expect $d.scoreMethod 'highestAttempt' 'scoreMethod'; Expect $d.scope 'classExamSchedulesOnly' 'scope'
    Expect ((@($d.schedules) | ForEach-Object { $_.id }) -join ",") "$sA,$sB" 'counted schedules ordered by end time'
    $reasons = (@($d.notCounted) | Sort-Object title | ForEach-Object { "$($_.title)=$($_.reason)" }) -join ';'
    Expect $reasons 'T10 sC=notEnded;T10 sD=resultNotReleased;T10 sE=resultNotReleased' 'notCounted reasons'
    Expect $d.scoredCount 6 'scoredCount'; Expect $d.rankingVisible $true 'rankingVisible'; Expect $d.skippedAttempts 1 'skipped (snapshot tổng điểm 0)'
    Expect @($d.students).Count 7 'approved members only (pending/outsider excluded)'
    $expected = [ordered]@{
        "t10seed$tag" = '1|9|2|top|10,8'; "t10s1$tag" = '1|9|2|top|9,9'; "t10s2$tag" = '3|6|2||6,6'; "t10s3$tag" = '4|5|2||5,5'
        "t10s5$tag" = '5|4|2|bottom|4,4'; "t10s4$tag" = '6|1|1|bottom|2,'; "t10s6$tag" = '||0||,'
    }
    $order = @($d.students | ForEach-Object { $_.userName })
    Expect (($order[0..1] | Sort-Object) -join ",") ((@("t10s1$tag", "t10seed$tag") | Sort-Object) -join ",") 'tied pair first'
    Expect ($order[2..6] -join ',') ("t10s2$tag,t10s3$tag,t10s5$tag,t10s4$tag,t10s6$tag") 'order by rank, unranked last'
    foreach ($s in $d.students) {
        $scores = (@($s.scores) | ForEach-Object { if ($null -eq $_.score) { '' } else { [string]([decimal]$_.score) } }) -join ','
        $total = if ($null -eq $s.total) { '' } else { [string]([decimal]$s.total) }
        $line = "$($s.rank)|$total|$($s.attemptedCount)|$($s.group)|$scores"
        Expect $line $expected[$s.userName] "row $($s.userName)"
        Expect $s.scheduleCount 2 "scheduleCount $($s.userName)"
    }
    Expect (@($d.students | Where-Object userName -eq "t10seed$tag")[0].passedCount) 2 'passedCount'
    if ($r.text -match 'snapshotJson|answersJson|isCorrect|passwordHash') { throw 'Teacher ranking leaked snapshot/answers' }
    Pass 'thang 10 theo snapshot, lượt cao nhất, bỏ lượt hủy, lịch không làm = 0, 1-1-3, Top/Bottom 20%, lịch chưa tính có lý do'

    Write-Output '== Học viên'
    $m = Call 'GET' "/web/student/classes/$class/ranking" $s0Token
    Expect $m.status 200 'S0'; $md = $m.body.data
    Expect ([decimal]$md.total) ([decimal]9) 'S0 total'; Expect $md.rank 1 'S0 rank'; Expect $md.scoredCount 6 'S0 scoredCount'
    Expect $md.rankHiddenReason $null 'S0 no hidden reason'; Expect $md.attemptedCount 2 'S0 attempted'; Expect $md.notCountedCount 3 'S0 notCounted'
    Expect ((@($md.schedules) | ForEach-Object { [string]([decimal]$_.score) }) -join ',') '10,8' 'S0 per-schedule'
    foreach ($leak in @("t10s1$tag", "t10s2$tag", 'top', 'bottom', 'userName', 'email', 'students', 'group')) {
        if ($m.text -match [regex]::Escape($leak)) { throw "Student ranking leaked '$leak'" }
    }
    $n = (Call 'GET' "/web/student/classes/$class/ranking" $s6Token).body.data
    Expect $n.total $null 'S6 total'; Expect $n.rank $null 'S6 rank'; Expect $n.rankHiddenReason 'NO_SCORE' 'S6 reason'
    Pass 'chỉ thấy điểm/hạng của mình; không lộ tên/nhóm bạn khác; chưa có điểm -> NO_SCORE'

    Write-Output '== Lớp < 5 học viên có điểm'
    Sql "UPDATE s SET VoidedAtUtc=SYSUTCDATETIME(),VoidReason=N'T10 QA' FROM dbo.ExamAttemptSubmissions s JOIN dbo.ExamAttempts a ON a.Id=s.ExamAttemptId
      WHERE a.UserId IN ('$($st[3])','$($st[4])') AND a.ExamId IN ($exams);" | Out-Null
    $small = (Call 'GET' "/web/student/classes/$class/ranking" $s0Token).body.data
    Expect $small.scoredCount 4 'small scoredCount'; Expect $small.rank $null 'small rank hidden'; Expect $small.rankHiddenReason 'CLASS_TOO_SMALL' 'small reason'
    Expect ([decimal]$small.total) ([decimal]9) 'small total still shown'
    $t = (Call 'GET' "/web/teacher/classes/$class/ranking" $tokenA).body.data
    Expect $t.rankingVisible $false 'teacher rankingVisible false'
    if (@($t.students | Where-Object { $null -ne $_.group }).Count -ne 0) { throw 'Groups must be hidden below 5 scored students' }
    Expect (@($t.students | Where-Object userName -eq "t10s3$tag")[0].rank) 4 'teacher still sees rank'
    Pass 'học viên ẩn hạng (CLASS_TOO_SMALL); giảng viên vẫn thấy hạng, không có Top/Bottom'
    Write-Output "TOBE_T10_CLASS_RANKING_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    if ($exams) { Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.ExamAttempts WHERE ExamId IN ($exams);
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId IN ($exams));
      DELETE dbo.ExamSchedules WHERE ExamId IN ($exams); DELETE dbo.ExamDetails WHERE ExamId IN ($exams); DELETE dbo.Exams WHERE Id IN ($exams);" | Out-Null }
    if ($class -gt 0) { Sql "DELETE dbo.ClassUsers WHERE ClassId=$class; DELETE dbo.Classes WHERE Id=$class;" | Out-Null }
    $users = (@($seed, $teacherA, $teacherB, $admin, $pending, $outsider) + $st | ForEach-Object { "'$_'" }) -join ','
    Sql "DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);
      DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users);
      DELETE dbo.Users WHERE Id IN ($users) OR Email=N't10seed$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
