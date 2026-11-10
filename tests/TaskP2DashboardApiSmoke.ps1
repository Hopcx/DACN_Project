param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242', [string]$HoldFile = '')
# P2: GET /web/admin/dashboard và GET /web/admin/attempt-reports/score-groups.
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao. Fixture được dọn trong finally.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51161, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "p2qa$tag@example.invalid"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
function Auth([string]$route, $body) {
    for ($i = 0; $i -lt 5; $i++) {
        $r = Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck `
            -Headers @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)
        if ([int]$r.StatusCode -ne 429) { return @{ status = [int]$r.StatusCode; body = ($r.Content | ConvertFrom-Json) } }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
function Get([string]$url, [string]$token = '') {
    $headers = if ($token) { @{ Authorization = "Bearer $token" } } else { @{} }
    $r = Invoke-WebRequest "$BaseUrl$url" -Headers $headers -SkipCertificateCheck -SkipHttpErrorCheck
    return @{ status = [int]$r.StatusCode; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }

$student = [Guid]::Empty; $other = [Guid]::NewGuid(); $admin = [Guid]::NewGuid(); $exam = 0; $class = 0; $class2 = 0; $mail = $null
try {
    Expect (Auth 'register' @{ fullName = 'P2 QA'; userName = "p2qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$other',N'P2 other',N'p2other$tag',N'p2other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';
      INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$admin',N'P2 admin',N'p2admin$tag',N'p2admin$tag@example.invalid',PasswordHash,1,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';" | Out-Null
    $adminToken = [string](Auth 'login' @{ keyword = "p2admin$tag@example.invalid"; password = $password }).body.data.accessToken
    if (-not $adminToken) { throw 'Admin login failed.' }

    # Quyền và validate.
    Expect (Get '/web/admin/dashboard').status 401 'anonymous dashboard'
    Expect (Get '/web/admin/dashboard' $studentToken).status 403 'student dashboard'
    Expect (Get '/web/admin/attempt-reports/score-groups' $studentToken).status 403 'student score-groups'
    Expect (Get '/web/admin/dashboard?upcomingLimit=0' $adminToken).status 400 'upcomingLimit=0'
    Expect (Get '/web/admin/attempt-reports/score-groups?groupBy=room' $adminToken).status 400 'groupBy invalid'

    $before = (Get '/web/admin/dashboard?upcomingLimit=50' $adminToken)
    Expect $before.status 200 'dashboard baseline'
    $b = $before.body.data
    Expect $b.counts.students ([int](Sql 'SELECT COUNT(*) FROM dbo.Users WHERE LevelId=4 AND Status=1;')) 'students vs SQL'
    Expect $b.counts.teachers ([int](Sql 'SELECT COUNT(*) FROM dbo.Users WHERE LevelId=3 AND Status=1;')) 'teachers vs SQL'
    Expect $b.counts.classes ([int](Sql 'SELECT COUNT(*) FROM dbo.Classes WHERE Status=1;')) 'classes vs SQL'
    Expect $b.attempts.totalAttempts ([int](Sql 'SELECT COUNT(*) FROM dbo.ExamAttempts;')) 'attempts vs SQL'

    # Fixture: 1 bài thi thang 10, 4 lịch đang mở gán 2 lớp; chỉ lớp 1 có 2 học viên đã duyệt.
    # Lượt: 8 (đạt), 6 (đạt, kết quả ẩn), 2 (hết giờ, trượt), đang làm, 4 (học viên khác, trượt).
    $ids = Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'P2 QA $tag',@subject,1,0,1,10,5,60,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@exam,N'QA',1,SYSUTCDATETIME(),'$student',SYSUTCDATETIME(),'$student');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'P2 one $tag',N'P2A-$tag',4,'$student',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'P2 two $tag',N'P2B-$tag',4,'$student',@subject,1);
DECLARE @class2 int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,'$student',1),(@class,'$other',1);
DECLARE @now datetime2=SYSUTCDATETIME();
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES
(@exam,N'P2 one',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1),(@exam,N'P2 two',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1),
(@exam,N'P2 three',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1),(@exam,N'P2 four',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class2,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
DECLARE @s1 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P2 one'),@s2 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P2 two'),
 @s3 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P2 three'),@s4 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P2 four');
DECLARE @a1 uniqueidentifier=NEWID(),@a2 uniqueidentifier=NEWID(),@a3 uniqueidentifier=NEWID(),@a4 uniqueidentifier=NEWID(),@a5 uniqueidentifier=NEWID();
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult) VALUES
(@a1,'$student',@s1,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'submitted',0,N'{}',N'[]',5,1),
(@a2,'$student',@s2,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'submitted',0,N'{}',N'[]',5,0),
(@a3,'$student',@s3,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'timedOut',0,N'{}',N'[]',5,1),
(@a4,'$student',@s4,@exam,@variant,@now,DATEADD(minute,30,@now),NULL,N'inProgress',0,N'{}',N'[]',5,1),
(@a5,'$other',@s1,@exam,@variant,DATEADD(minute,-1,@now),DATEADD(minute,30,@now),@now,N'submitted',0,N'{}',N'[]',5,1);
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson)
VALUES(@a1,@now,60,8,1,N'[]'),(@a2,@now,60,6,1,N'[]'),(@a3,@now,60,2,0,N'[]'),(@a5,@now,60,4,0,N'[]');
SELECT CONCAT(@exam,',',@class,',',@class2);
"@
    $parts = $ids.Split(','); $exam = [int]$parts[0]; $class = [int]$parts[1]; $class2 = [int]$parts[2]

    $after = (Get '/web/admin/dashboard?upcomingLimit=50' $adminToken).body.data
    Expect ($after.attempts.totalAttempts - $b.attempts.totalAttempts) 5 'delta total'
    Expect ($after.attempts.finalizedCount - $b.attempts.finalizedCount) 4 'delta finalized'
    Expect ($after.attempts.inProgressCount - $b.attempts.inProgressCount) 1 'delta inProgress'
    Expect ($after.attempts.timeoutCount - $b.attempts.timeoutCount) 1 'delta timeout'
    Expect ($after.attempts.passedCount - $b.attempts.passedCount) 2 'delta passed'
    $vnHour = ([DateTime]::UtcNow.AddHours(7)).Hour
    if ($vnHour -lt 23) { Expect ($after.todaySchedules - $b.todaySchedules) 4 'delta todaySchedules' }
    $mine = @($after.upcoming | Where-Object { $_.examId -eq $exam })
    Expect $mine.Count 4 'upcoming fixture schedules'
    foreach ($row in $mine) {
        Expect $row.scheduleState 'open' "state $($row.title)"
        Expect @($row.classes).Count 2 "classes $($row.title)"
        Expect $row.candidateCount 2 "candidates $($row.title)"
    }

    $byExam = (Get "/web/admin/attempt-reports/score-groups?groupBy=exam&examId=$exam" $adminToken).body.data
    $g = @($byExam.groups); Expect $g.Count 1 'exam groups'
    Expect $g[0].finalizedCount 4 'exam finalized'; Expect $g[0].passedCount 2 'exam passed'
    Expect ([decimal]$g[0].averageScore) ([decimal]5) 'exam average'; Expect ([decimal]$g[0].passPercent) ([decimal]50) 'exam pass%'
    $counts = @($g[0].buckets | ForEach-Object { $_.count }) -join ','
    Expect $counts '0,0,1,0,1,0,1,0,1,0' 'exam buckets'

    $byClass = (Get "/web/admin/attempt-reports/score-groups?groupBy=class&examId=$exam" $adminToken).body.data
    $cg = @($byClass.groups); Expect $cg.Count 1 'class groups (lớp 2 không có thành viên)'
    Expect $cg[0].id $class 'class id'; Expect $cg[0].finalizedCount 4 'class finalized'
    Expect @((Get "/web/admin/attempt-reports/score-groups?groupBy=class&classId=$class2" $adminToken).body.data.groups).Count 0 'class2 empty'

    $bySubject = (Get "/web/admin/attempt-reports/score-groups?groupBy=subject&examId=$exam" $adminToken).body.data
    Expect @($bySubject.groups).Count 1 'subject groups'; Expect @($bySubject.groups)[0].finalizedCount 4 'subject finalized'

    # Hủy một lượt: rời khỏi mẫu số phổ điểm, tăng voided.
    $a1 = Sql "SELECT TOP 1 a.Id FROM dbo.ExamAttempts a JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId=a.Id WHERE a.ExamId=$exam AND s.TotalMark=8;"
    $void = Invoke-WebRequest "$BaseUrl/web/admin/attempts/$a1/void" -Method Post -Headers @{ Authorization = "Bearer $adminToken" } -SkipCertificateCheck -SkipHttpErrorCheck -ContentType 'application/json' -Body '{"reason":"P2 QA"}'
    Expect ([int]$void.StatusCode) 200 'void'
    $g2 = @((Get "/web/admin/attempt-reports/score-groups?groupBy=exam&examId=$exam" $adminToken).body.data.groups)[0]
    Expect $g2.finalizedCount 3 'after void finalized'; Expect $g2.passedCount 1 'after void passed'
    $after2 = (Get '/web/admin/dashboard' $adminToken).body.data
    Expect ($after2.attempts.voidedCount - $b.attempts.voidedCount) 1 'delta voided'
    Write-Output "P2_DASHBOARD_SMOKE=PASS;DB=$Database;TAG=$tag"
    if ($HoldFile) {
        # Giữ fixture để kiểm giao diện; xóa file này để dọn fixture và kết thúc.
        Set-Content -LiteralPath $HoldFile -Value "p2admin$tag@example.invalid`n$password" -NoNewline
        while (Test-Path -LiteralPath $HoldFile) { Start-Sleep -Seconds 2 }
    }
}
finally {
    if ($exam -gt 0) { Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam);
      DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;
      DELETE dbo.ClassUsers WHERE ClassId IN ($class,$class2); DELETE dbo.Classes WHERE Id IN ($class,$class2);" | Out-Null }
    if ($student -ne [Guid]::Empty) { Sql "DELETE dbo.RefreshTokens WHERE UserId IN ('$student','$other','$admin');
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ('$student','$other','$admin');
      DELETE dbo.Users WHERE Id IN ('$student','$other','$admin');" | Out-Null }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
