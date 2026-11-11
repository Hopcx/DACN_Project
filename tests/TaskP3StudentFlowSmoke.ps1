param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242',
    [string]$HoldFile = '', [int]$OpenMinutes = 4)
# P3: trường mới của /web/student/schedules (examName, classNames, numberOfRepeat, usedAttempts),
# /web/attempts/{id} (examName) và /web/attempts/mine (attemptNumber, numberOfRepeat, reviewAvailable).
# -HoldFile giữ fixture (lịch mở $OpenMinutes phút, 6 câu) để kiểm giao diện; xóa file để dọn.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "p3qa$tag@example.invalid"
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
function Call([string]$method, [string]$url, [string]$token, $body = $null) {
    $args = @{ Uri = "$BaseUrl$url"; Method = $method; Headers = @{ Authorization = "Bearer $token" }; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $args.ContentType = 'application/json'; $args.Body = ($body | ConvertTo-Json -Compress -Depth 6) }
    $r = Invoke-WebRequest @args
    return @{ status = [int]$r.StatusCode; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }

$student = [Guid]::Empty; $exam = 0; $class = 0; $mail = $null
try {
    Expect (Auth 'register' @{ fullName = 'P3 Học viên'; userName = "p3qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $token = [string]$login.body.data.accessToken

    # Bài thi 6 câu (5 một đáp án, 1 nhiều đáp án), 2 lượt, xem kết quả. Lịch A đang mở, B sắp diễn ra, C mở để thử API.
    $ids = Sql @"
DECLARE @user uniqueidentifier='$student';
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'Kiểm tra giữa kỳ P3 $tag',@subject,6,2,1,10,5,$OpenMinutes,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@exam,N'P3',1,SYSUTCDATETIME(),@user,SYSUTCDATETIME(),@user);
DECLARE @variant int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int;
WHILE @i <= 6 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId)
  VALUES(CONCAT(N'Câu hỏi P3 số ',@i,N' — trong mô hình client-server, thành phần nào lưu trữ dữ liệu? ($tag)'),SYSUTCDATETIME(),1,@subject,CASE WHEN @i=6 THEN 3 ELSE 2 END);
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES
    (@q,N'Trình duyệt (Client)',0,1),(@q,N'Máy chủ (Server)',1,1),(@q,N'Giao thức HTTP',CASE WHEN @i=6 THEN 1 ELSE 0 END,1),(@q,N'Hệ điều hành',0,1);
  INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES(@variant,@q,CASE WHEN @i=6 THEN 2.5 ELSE 1.5 END);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'CS101 P3 $tag',N'P3-$tag',5,@user,@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,@user,1);
DECLARE @now datetime2=SYSUTCDATETIME();
DECLARE @room int=(SELECT TOP 1 Id FROM dbo.Rooms ORDER BY Id);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc,RoomId) VALUES
(@exam,N'P3 đang mở',DATEADD(minute,-1,@now),DATEADD(minute,$OpenMinutes,@now),1,1,@room),
(@exam,N'P3 sắp diễn ra',DATEADD(hour,2,@now),DATEADD(hour,3,@now),1,1,@room),
(@exam,N'P3 kiểm API',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1,NULL);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
SELECT CONCAT(@exam,',',@class,',',(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'P3 kiểm API'));
"@
    $parts = $ids.Split(','); $exam = [int]$parts[0]; $class = [int]$parts[1]; $apiSchedule = [int]$parts[2]

    $schedules = Call 'GET' '/web/student/schedules' $token
    Expect $schedules.status 200 'student schedules'
    $mine = @($schedules.body.data | Where-Object { $_.examName -eq "Kiểm tra giữa kỳ P3 $tag" })
    Expect $mine.Count 3 'schedules with examName'
    foreach ($row in $mine) {
        Expect $row.numberOfRepeat 2 "numberOfRepeat $($row.title)"; Expect $row.usedAttempts 0 "usedAttempts $($row.title)"
        Expect (@($row.classNames) -join ',') "CS101 P3 $tag" "classNames $($row.title)"
    }

    $start = Call 'POST' "/web/exam-schedules/$apiSchedule/attempts" $token
    Expect $start.status 200 'start'
    Expect $start.body.data.examName "Kiểm tra giữa kỳ P3 $tag" 'attempt examName'
    $attemptId = $start.body.data.attemptId
    $submit = Call 'POST' "/web/attempts/$attemptId/submit" $token @{ revision = $start.body.data.revision }
    Expect $submit.status 200 'submit'
    $again = Call 'POST' "/web/exam-schedules/$apiSchedule/attempts" $token
    Expect $again.status 200 'second start'
    $used = @((Call 'GET' '/web/student/schedules' $token).body.data | Where-Object { $_.id -eq $apiSchedule })[0]
    Expect $used.usedAttempts 2 'usedAttempts after 2 starts'
    $history = (Call 'GET' '/web/attempts/mine?page=1&pageSize=10' $token).body.data
    $items = @($history.items | Sort-Object attemptNumber)
    Expect $items.Count 2 'history count'
    Expect $items[0].attemptNumber 1 'first attemptNumber'; Expect $items[1].attemptNumber 2 'second attemptNumber'
    Expect $items[0].numberOfRepeat 2 'history numberOfRepeat'
    Expect $items[0].reviewAvailable $false 'review not before schedule end'
    # Đưa lịch API về quá khứ: lượt đã nộp được xem lại, lượt đang làm thì không.
    Sql "UPDATE dbo.ExamSchedules SET StartTime=DATEADD(hour,-2,SYSUTCDATETIME()), EndTime=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=$apiSchedule;" | Out-Null
    $history2 = @((Call 'GET' '/web/attempts/mine?page=1&pageSize=10' $token).body.data.items | Sort-Object attemptNumber)
    Expect $history2[0].reviewAvailable $true 'review after end'
    Expect (Call 'GET' "/web/attempts/$attemptId/review" $token).status 200 'review endpoint agrees'
    Write-Output "P3_STUDENT_FLOW_SMOKE=PASS;DB=$Database;TAG=$tag"
    if ($HoldFile) {
        Set-Content -LiteralPath $HoldFile -Value "$email`n$password" -NoNewline
        while (Test-Path -LiteralPath $HoldFile) { Start-Sleep -Seconds 2 }
    }
}
finally {
    if ($exam -gt 0) { Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam);
      DELETE dbo.ExamSchedules WHERE ExamId=$exam;
      DECLARE @q TABLE(Id int); INSERT @q SELECT QuestionId FROM dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam);
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam);
      DELETE dbo.Answers WHERE QuestionId IN (SELECT Id FROM @q); DELETE dbo.Questions WHERE Id IN (SELECT Id FROM @q);
      DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;
      DELETE dbo.ClassUsers WHERE ClassId=$class; DELETE dbo.Classes WHERE Id=$class;" | Out-Null }
    if ($student -ne [Guid]::Empty) { Sql "DELETE dbo.RefreshTokens WHERE UserId='$student'; DELETE dbo.EmailVerificationTokens WHERE UserId='$student'; DELETE dbo.Users WHERE Id='$student';" | Out-Null }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
