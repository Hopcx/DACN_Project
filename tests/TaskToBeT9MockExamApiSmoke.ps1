param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T9: Thi thử — tạo (thời lượng, revealMode, giới hạn 1 phiên mock), không lộ đáp án/lời giải, hạn nộp đúng,
# ân hạn 5 giây, quá hạn chốt bằng nháp đã lưu (lười qua API và bởi worker), review, retry cùng thời lượng, người khác 404.
# Dời DeadlineUtc bằng SQL trên bản sao để không phải chờ. Chạy từ thư mục gốc DACN_Project. Fixture trong transaction, dọn theo tag.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t9qa$tag@example.invalid"
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
    if ($null -ne $body) { $p.ContentType = 'application/json'; $p.Body = ($body | ConvertTo-Json -Compress -Depth 8) }
    $r = Invoke-WebRequest @p
    return @{ status = [int]$r.StatusCode; raw = [string]$r.Content; body = if ($r.Content) { $r.Content | ConvertFrom-Json -DateKind String } else { $null } }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected $expected, got $actual" } }
function NoLeak($response, [string]$case) {
    foreach ($needle in 'isCorrect', 'explanation', "EXPL-$tag", 'correctAnswerIds') {
        if ($response.raw -match [regex]::Escape($needle)) { throw "$case lộ '$needle'." }
    }
}
function Opt($question, [string]$text) { @($question.options | Where-Object { $_.content -eq $text })[0].id }
function SetDeadline($id, [int]$secondsFromNow) { Sql "UPDATE dbo.PracticeSessions SET DeadlineUtc=DATEADD(second,$secondsFromNow,SYSUTCDATETIME()) WHERE Id='$id';" | Out-Null }
$base = '/web/student/practice'

$student = [Guid]::Empty; $other = [Guid]::NewGuid(); $mail = $null
try {
    Expect (Auth 'register' @{ fullName = 'T9 Học viên'; userName = "t9qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $token = [string]$login.body.data.accessToken

    $subject = [int](Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$student';
INSERT dbo.Subjects(Name,Status) VALUES (N'T9 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int;
WHILE @i <= 4 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,Explanation)
  VALUES (CONCAT(N'T9 $tag n',@i),SYSUTCDATETIME(),1,@s,2,N'EXPL-$tag');
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T9 $tag',N'T9-$tag',5,@user,@s,1); DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,@user,1);
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$other',N'T9 other',N't9other$tag',N't9other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id=@user;
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,'$other',1);
COMMIT;
SELECT @s;
"@)
    $otherLogin = Auth 'login' @{ keyword = "t9other$tag@example.invalid"; password = $password }
    Expect $otherLogin.status 200 'login other'
    $otherToken = [string]$otherLogin.body.data.accessToken

    # tạo: lỗi tiêu chí
    Expect (Call 'POST' $base $token @{ subjectId = $subject; questionCount = 2; kind = 'mock'; durationMinutes = 4 }).status 400 'duration < 5'
    Expect (Call 'POST' $base $token @{ subjectId = $subject; questionCount = 2; kind = 'mock'; durationMinutes = 181 }).status 400 'duration > 180'
    Expect (Call 'POST' $base $token @{ subjectId = $subject; questionCount = 2; kind = 'mock' }).status 400 'missing duration'
    Expect (Call 'POST' $base $token @{ subjectId = $subject; questionCount = 2; kind = 'mock'; durationMinutes = 10; revealMode = 'perQuestion' }).status 400 'mock perQuestion'

    # mock 1: hạn nộp đúng, không lộ đáp án
    $m1 = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 2; kind = 'mock'; durationMinutes = 10 }
    Expect $m1.status 201 'create mock'
    NoLeak $m1 'create mock'
    Expect $m1.body.data.kind 'mock' 'kind'
    Expect $m1.body.data.durationMinutes 10 'durationMinutes'
    if (-not "$($m1.body.data.deadlineUtc)".EndsWith('Z')) { throw "deadlineUtc phải có hậu tố Z: $($m1.body.data.deadlineUtc)" }
    $span = ([DateTime]::Parse($m1.body.data.deadlineUtc).ToUniversalTime() - [DateTime]::Parse($m1.body.data.createdAtUtc).ToUniversalTime()).TotalSeconds
    if ([Math]::Abs($span - 600) -gt 2) { throw "Hạn nộp lệch: $span giây" }
    $m1Id = $m1.body.data.id; $q = @($m1.body.data.questions)
    $second = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 1; kind = 'mock'; durationMinutes = 5 }
    Expect $second.status 409 'second open mock'
    Expect $second.body.message 'MOCK_ALREADY_OPEN' 'mock limit code'
    $practice = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 1 }
    Expect $practice.status 201 'practice limit is separate'
    if ($null -ne $practice.body.data.deadlineUtc) { throw 'Phiên luyện tập không có hạn nộp.' }
    Expect (Call 'DELETE' "$base/$($practice.body.data.id)" $token).status 200 'abandon practice'
    $checkMock = Call 'POST' "$base/$m1Id/questions/$($q[0].id)/check" $token @{ revision = 0; selectedAnswerIds = @((Opt $q[0] 'OK')) }
    Expect $checkMock.status 409 'check on mock'
    Expect (Call 'GET' "$base/$m1Id" $otherToken).status 404 'other GET mock'
    Expect (Call 'GET' "$base/$m1Id/review" $token).status 409 'review before finish'

    # lưu câu 1; ân hạn: hạn đã qua 2 giây vẫn lưu được câu 2; quá ân hạn → 409, tự chốt bằng nháp đã lưu
    Expect (Call 'PUT' "$base/$m1Id/answers" $token @{ revision = 0; answers = @(@{ questionId = $q[0].id; selectedAnswerIds = @((Opt $q[0] 'OK')) }) }).status 200 'save q1'
    SetDeadline $m1Id -2
    $grace = Call 'PUT' "$base/$m1Id/answers" $token @{ revision = 1; answers = @(@{ questionId = $q[0].id; selectedAnswerIds = @((Opt $q[0] 'OK')) }, @{ questionId = $q[1].id; selectedAnswerIds = @((Opt $q[1] 'OK')) }) }
    Expect $grace.status 200 'save within 5s grace'
    SetDeadline $m1Id -10
    $late = Call 'PUT' "$base/$m1Id/answers" $token @{ revision = 2; answers = @(@{ questionId = $q[0].id; selectedAnswerIds = @((Opt $q[0] 'NO')) }) }
    Expect $late.status 409 'save after grace'
    Expect $late.body.message 'PRACTICE_EXPIRED' 'expired code'
    $after = Call 'GET' "$base/$m1Id" $token
    Expect $after.body.data.state 'finished' 'expired mock finalized'
    Expect $after.body.data.correctCount 2 'score uses saved draft (late save ignored)'
    Expect (Sql "SELECT CASE WHEN FinishedAtUtc=DeadlineUtc THEN 1 ELSE 0 END FROM dbo.PracticeSessions WHERE Id='$m1Id';") '1' 'FinishedAtUtc = DeadlineUtc'
    $fin = Call 'POST' "$base/$m1Id/finish" $token @{ revision = 0 }
    Expect "$($fin.status)|$($fin.body.data.correctCount)" '200|2' 'finish after expiry returns result'
    $review = Call 'GET' "$base/$m1Id/review" $token
    Expect $review.status 200 'review after expiry'
    Expect (($review.body.data.questions | ForEach-Object { "$($_.isCorrect)" }) -join ',') 'True,True' 'review correctness'

    # worker: mock 2 hết giờ, không gọi API → tự chốt trong ≤ 45 giây
    $m2 = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 1; kind = 'mock'; durationMinutes = 5 }
    Expect $m2.status 201 'create mock 2'
    $m2Id = $m2.body.data.id
    SetDeadline $m2Id -10
    $state = ''
    for ($i = 0; $i -lt 23; $i++) {
        $state = Sql "SELECT CONCAT(State,':',ISNULL(CAST(CorrectCount AS varchar(5)),'-'),':',CASE WHEN FinishedAtUtc=DeadlineUtc THEN 'deadline' ELSE 'other' END) FROM dbo.PracticeSessions WHERE Id='$m2Id';"
        if ($state.StartsWith('finished')) { break }
        Start-Sleep -Seconds 2
    }
    Expect $state 'finished:0:deadline' 'worker finalized expired mock'

    # retry mock cùng thời lượng; lịch sử; bỏ phiên mock đang làm
    $retry = Call 'POST' "$base/$m1Id/retry" $token
    Expect $retry.status 201 'retry mock'
    Expect "$($retry.body.data.kind)|$($retry.body.data.durationMinutes)" 'mock|10' 'retry keeps kind and duration'
    NoLeak $retry 'retry mock'
    $mine = Call 'GET' "$base/mine?page=1&pageSize=10" $token
    Expect (@($mine.body.data.items | Where-Object { $_.kind -eq 'mock' })).Count 3 'history has 3 mocks'
    Expect (Call 'DELETE' "$base/$($retry.body.data.id)" $token).status 200 'abandon open mock'
    # T19 (nợ L5): thi thử hết giờ (chưa chốt hoặc worker vừa chốt) có thời gian làm bài = hạn − lúc tạo, không phải "—"
    $m3 = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 1; kind = 'mock'; durationMinutes = 5 }
    Expect $m3.status 201 'create mock 3'
    # phiên 5 phút tạo từ 5 phút trước, hạn đã qua 10 giây (quá ân hạn 5 giây)
    Sql "UPDATE dbo.PracticeSessions SET CreatedAtUtc=DATEADD(second,-310,SYSUTCDATETIME()), DeadlineUtc=DATEADD(second,-10,SYSUTCDATETIME()) WHERE Id='$($m3.body.data.id)';" | Out-Null
    $row = @((Call 'GET' "$base/mine?page=1&pageSize=10" $token).body.data.items | Where-Object { $_.id -eq $m3.body.data.id })[0]
    $expectSeconds = Sql "SELECT DATEDIFF(second,CreatedAtUtc,DeadlineUtc) FROM dbo.PracticeSessions WHERE Id='$($m3.body.data.id)';"
    if ($row.state -notin 'expired', 'finished') { throw "mock 3 state $($row.state)" }
    if ([Math]::Abs([long]$row.durationSeconds - [long]$expectSeconds) -gt 1) { throw "expired mock duration $($row.durationSeconds), expected ~$expectSeconds" }
    Write-Output "  PASS thi thử hết giờ ($($row.state)): durationSeconds = $($row.durationSeconds)"
    Write-Output "TOBE_T9_MOCK_EXAM_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "SELECT Id FROM dbo.Users WHERE Email IN (N'$email', N't9other$tag@example.invalid') OR Id='$other'"
    $subjects = "SELECT Id FROM dbo.Subjects WHERE Name=N'T9 môn $tag'"
    Clean "DELETE dbo.ReviewFlags WHERE UserId IN ($users);"
    Clean "DELETE dbo.PracticeSessions WHERE UserId IN ($users) OR SubjectId IN ($subjects);"
    Clean "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects); DELETE dbo.Questions WHERE SubjectId IN ($subjects);"
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T9-$tag') OR UserId IN ($users);
      DELETE dbo.Classes WHERE ClassCode=N'T9-$tag'; DELETE dbo.Subjects WHERE Id IN ($subjects);"
    Clean "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
