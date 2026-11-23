param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242', [string]$HoldFile = '')
# Dấu ASCII EXPL-<tag> trong lời giải: API escape tiếng Việt thành \uXXXX nên dò lộ theo chữ ASCII.
# To-be T4: snapshot v2 chụp lời giải lúc bắt đầu; làm bài không lộ lời giải/đáp án; review trả explanation
# cùng điều kiện mở đáp án; sửa ngân hàng sau khi bắt đầu không đổi snapshot; snapshot v1 vẫn review được (null).
# Chạy từ thư mục gốc DACN_Project (cần Project.Api/.maildrop). Fixture theo tag, dọn trong finally.
# -HoldFile giữ fixture (2 lượt đã có thể xem lại) để kiểm giao diện; xóa file để dọn.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
# SnapshotJson dài: -y 0 không cắt cột nvarchar(max) (không dùng chung với -W/-h), lấy dòng JSON
function SqlJson([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -y 0 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return (@($output) | Where-Object { $_.TrimStart().StartsWith('{') } | Select-Object -First 1).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t4qa$tag@example.invalid"
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
    $p = @{ Uri = "$BaseUrl$url"; Method = $method; Headers = @{ Authorization = "Bearer $token" }; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $p.ContentType = 'application/json'; $p.Body = ($body | ConvertTo-Json -Compress -Depth 6) }
    $r = Invoke-WebRequest @p
    return @{ status = [int]$r.StatusCode; raw = [string]$r.Content; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }
function NoLeak($response, [string]$case) {
    foreach ($needle in 'explanation', 'isCorrect', "EXPL-$tag") {
        if ($response.raw -match [regex]::Escape($needle)) { throw "$case lộ '$needle' trước khi được xem lại." }
    }
}

$student = [Guid]::Empty; $exam = 0; $class = 0; $mail = $null
try {
    Expect (Auth 'register' @{ fullName = 'T4 Học viên'; userName = "t4qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $token = [string]$login.body.data.accessToken

    # Đề 2 câu: câu 1 có lời giải (có khoảng trắng đầu/cuối, xuống dòng), câu 2 không có. 2 lượt, cho xem kết quả.
    $ids = Sql @"
DECLARE @user uniqueidentifier='$student';
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T4 lời giải $tag',@subject,2,2,1,10,5,30,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@exam,N'T4',1,SYSUTCDATETIME(),@user,SYSUTCDATETIME(),@user);
DECLARE @variant int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int;
WHILE @i <= 2 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,Explanation)
  VALUES(CONCAT(N'Câu T4 số ',@i,N' ($tag)'),SYSUTCDATETIME(),1,@subject,2,
    CASE WHEN @i=1 THEN CONCAT(N'  EXPL-$tag lời giải', CHAR(10), N'Dòng 2  ') ELSE NULL END);
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'Đúng',1,1),(@q,N'Sai',0,1);
  INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES(@variant,@q,5);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'T4 $tag',N'T4-$tag',5,@user,@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,@user,1);
DECLARE @now datetime2=SYSUTCDATETIME();
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc,RoomId) VALUES
(@exam,N'T4 kiểm API',DATEADD(minute,-1,@now),DATEADD(minute,30,@now),1,1,NULL);
DECLARE @schedule int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES(@class,@schedule);
SELECT CONCAT(@exam,',',@class,',',@schedule);
"@
    $parts = $ids.Split(','); $exam = [int]$parts[0]; $class = [int]$parts[1]; $schedule = [int]$parts[2]

    # Lượt 1 (v2): làm bài không lộ lời giải/đáp án
    $start = Call 'POST' "/web/exam-schedules/$schedule/attempts" $token
    Expect $start.status 200 'start 1'
    NoLeak $start 'start'
    $attempt1 = $start.body.data.attemptId
    $get = Call 'GET' "/web/attempts/$attempt1" $token
    Expect $get.status 200 'get attempt'
    NoLeak $get 'get attempt'
    Expect (Sql "SELECT JSON_VALUE(SnapshotJson,'$.version') FROM dbo.ExamAttempts WHERE Id='$attempt1';") '4' 'snapshot version in DB (T15: v4)'
    # Sửa ngân hàng sau khi bắt đầu: snapshot không đổi
    Sql "UPDATE dbo.Questions SET Explanation=N'Đã sửa sau khi thi $tag' WHERE Content=N'Câu T4 số 1 ($tag)';" | Out-Null
    $submit = Call 'POST' "/web/attempts/$attempt1/submit" $token @{ revision = $start.body.data.revision }
    Expect $submit.status 200 'submit 1'
    NoLeak $submit 'submit'

    # Lượt 2: chuyển SnapshotJson về dạng v1 (bỏ explanation, version 1) như lượt trước khi triển khai
    $start2 = Call 'POST' "/web/exam-schedules/$schedule/attempts" $token
    Expect $start2.status 200 'start 2'
    $attempt2 = $start2.body.data.attemptId
    # JSON_MODIFY lax + NULL xóa thuộc tính; làm trên server để không truyền JSON qua dòng lệnh
    Sql "UPDATE dbo.ExamAttempts SET SnapshotJson=JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(SnapshotJson,'$.version',1),'$.questions[0].explanation',NULL),'$.questions[1].explanation',NULL) WHERE Id='$attempt2';" | Out-Null
    $v1 = SqlJson "SELECT SnapshotJson FROM dbo.ExamAttempts WHERE Id='$attempt2';"
    if ($v1 -match 'explanation' -or $v1 -notmatch '"version":1') { throw "Không chuyển được snapshot về v1: $v1" }
    Expect (Call 'POST' "/web/attempts/$attempt2/submit" $token @{ revision = $start2.body.data.revision }).status 200 'submit 2'

    # Trước giờ kết thúc lịch: chưa được xem lại
    Expect (Call 'GET' "/web/attempts/$attempt1/review" $token).status 403 'review before schedule end'
    Sql "UPDATE dbo.ExamSchedules SET StartTime=DATEADD(hour,-2,SYSUTCDATETIME()), EndTime=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=$schedule;" | Out-Null

    $review = Call 'GET' "/web/attempts/$attempt1/review" $token
    Expect $review.status 200 'review v2'
    Expect $review.body.data.snapshotVersion 4 'snapshotVersion v4'
    $q1 = @($review.body.data.questions | Where-Object { $_.content -eq "Câu T4 số 1 ($tag)" })[0]
    $q2 = @($review.body.data.questions | Where-Object { $_.content -eq "Câu T4 số 2 ($tag)" })[0]
    Expect $q1.explanation "EXPL-$tag lời giải`nDòng 2" 'explanation trimmed, kept newline, not changed by later edit'
    if ($null -ne $q2.explanation) { throw "Câu không có lời giải phải trả null, nhận '$($q2.explanation)'" }
    if (-not ($q1.PSObject.Properties.Name -contains 'explanation')) { throw 'Review thiếu trường explanation.' }

    $review2 = Call 'GET' "/web/attempts/$attempt2/review" $token
    Expect $review2.status 200 'review v1'
    Expect $review2.body.data.snapshotVersion 1 'snapshotVersion v1'
    if (@($review2.body.data.questions | Where-Object { $null -ne $_.explanation }).Count -ne 0) { throw 'Snapshot v1 không được có lời giải.' }
    Expect @($review2.body.data.questions).Count 2 'v1 question count'
    Write-Output "TOBE_T4_SNAPSHOT_EXPLANATION_SMOKE=PASS;DB=$Database;TAG=$tag"
    if ($HoldFile) {
        Set-Content -LiteralPath $HoldFile -Value "$email`n$password`n$attempt1`n$attempt2" -NoNewline
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
    if ($student -eq [Guid]::Empty) { $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$email';"; if ($found) { $student = [Guid]$found } }
    if ($student -ne [Guid]::Empty) { Sql "DELETE dbo.ReviewFlags WHERE UserId='$student'; DELETE dbo.PracticeSessions WHERE UserId='$student'; DELETE dbo.RefreshTokens WHERE UserId='$student'; DELETE dbo.EmailVerificationTokens WHERE UserId='$student'; DELETE dbo.Users WHERE Id='$student';" | Out-Null }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
