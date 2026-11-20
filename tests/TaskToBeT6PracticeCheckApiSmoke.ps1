param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T6: Luyện tập "kiểm tra từng câu" — chỉ phiên perQuestion; check lưu + chốt câu, trả đáp án/lời giải của câu đó;
# câu chưa chốt không lộ; check lại idempotent; lưu nháp không đổi được câu đã chốt; không bỏ phiên sau khi đã check;
# retry chỉ phiên đã nộp; finish/review dùng lựa chọn đã chốt; chỉ chủ phiên.
# Dấu ASCII EXPL-<tag> trong lời giải. Chạy từ thư mục gốc DACN_Project. Fixture trong transaction, dọn theo tag.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t6qa$tag@example.invalid"
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
    return @{ status = [int]$r.StatusCode; raw = [string]$r.Content; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected $expected, got $actual" } }
function NoLeak($response, [string]$case) {
    foreach ($needle in 'isCorrect', 'explanation', "EXPL-$tag", 'correctAnswerIds') {
        if ($response.raw -match [regex]::Escape($needle)) { throw "$case lộ '$needle'." }
    }
}
function Opt($question, [string]$text) { @($question.options | Where-Object { $_.content -eq $text })[0].id }
$base = '/web/student/practice'

$student = [Guid]::Empty; $other = [Guid]::NewGuid(); $mail = $null
try {
    Expect (Auth 'register' @{ fullName = 'T6 Học viên'; userName = "t6qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $token = [string]$login.body.data.accessToken

    # Môn có lớp đã duyệt, 4 câu loại 2 (OK đúng / NO sai), câu nào cũng có lời giải. Học viên thứ hai cùng lớp.
    $subject = [int](Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$student';
INSERT dbo.Subjects(Name,Status) VALUES (N'T6 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int;
WHILE @i <= 4 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,Explanation)
  VALUES (CONCAT(N'T6 $tag n',@i),SYSUTCDATETIME(),1,@s,2,CONCAT(N'EXPL-$tag n',@i));
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T6 $tag',N'T6-$tag',5,@user,@s,1); DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,@user,1);
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$other',N'T6 other',N't6other$tag',N't6other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id=@user;
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,'$other',1);
COMMIT;
SELECT @s;
"@)
    $otherLogin = Auth 'login' @{ keyword = "t6other$tag@example.invalid"; password = $password }
    Expect $otherLogin.status 200 'login other'
    $otherToken = [string]$otherLogin.body.data.accessToken

    # phiên 'end': không check được
    $end = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 2; revealMode = 'end' }
    Expect $end.status 201 'create end'
    $notAllowed = Call 'POST' "$base/$($end.body.data.id)/questions/$($end.body.data.questions[0].id)/check" $token @{ revision = 0; selectedAnswerIds = @((Opt $end.body.data.questions[0] 'OK')) }
    Expect $notAllowed.status 409 'check on end session'
    Expect $notAllowed.body.message 'PRACTICE_CHECK_NOT_ALLOWED' 'check not allowed code'

    # phiên perQuestion 3 câu; một phiên perQuestion khác bỏ được khi chưa check
    $p = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 3; revealMode = 'perQuestion' }
    Expect $p.status 201 'create perQuestion'
    NoLeak $p 'create perQuestion'
    Expect @($p.body.data.checked).Count 0 'no checked initially'
    $pid_ = $p.body.data.id; $q = @($p.body.data.questions)
    $p2 = Call 'POST' $base $token @{ subjectId = $subject; questionCount = 1; revealMode = 'perQuestion' }
    Expect (Call 'DELETE' "$base/$($p2.body.data.id)" $token).status 200 'abandon before any check'
    $early = Call 'POST' "$base/$pid_/retry" $token
    Expect $early.status 409 'retry in-progress session'
    Expect $early.body.message 'PRACTICE_NOT_FINISHED' 'retry not finished code'

    # check
    $url1 = "$base/$pid_/questions/$($q[0].id)/check"
    Expect (Call 'POST' $url1 $token @{ revision = 0; selectedAnswerIds = @() }).status 400 'check empty selection'
    Expect (Call 'POST' $url1 $token @{ revision = 0; selectedAnswerIds = @((Opt $q[0] 'OK'), (Opt $q[0] 'NO')) }).status 400 'check two options on single'
    Expect (Call 'POST' $url1 $token @{ revision = 5; selectedAnswerIds = @((Opt $q[0] 'OK')) }).status 409 'check stale revision'
    Expect (Call 'POST' "$base/$pid_/questions/$([Guid]::NewGuid())/check" $token @{ revision = 0; selectedAnswerIds = @() }).status 404 'check unknown question'
    Expect (Call 'POST' $url1 $otherToken @{ revision = 0; selectedAnswerIds = @((Opt $q[0] 'OK')) }).status 404 'check by other student'
    $c1 = Call 'POST' $url1 $token @{ revision = 0; selectedAnswerIds = @((Opt $q[0] 'OK')) }
    Expect $c1.status 200 'check q1'
    Expect $c1.body.data.result.isCorrect $true 'q1 correct'
    Expect (@($c1.body.data.result.correctAnswerIds) -join ',') (Opt $q[0] 'OK') 'q1 correct answer ids'
    Expect $c1.body.data.result.explanation "EXPL-$tag $($q[0].content.Split(' ')[-1])" 'q1 explanation'
    Expect $c1.body.data.revision 1 'revision after check'
    $c2 = Call 'POST' "$base/$pid_/questions/$($q[1].id)/check" $token @{ revision = 1; selectedAnswerIds = @((Opt $q[1] 'NO')) }
    Expect $c2.status 200 'check q2'
    Expect $c2.body.data.result.isCorrect $false 'q2 wrong'
    $again = Call 'POST' $url1 $token @{ revision = 0; selectedAnswerIds = @((Opt $q[0] 'NO')) }
    Expect $again.status 200 'check q1 again'
    Expect "$($again.body.data.result.isCorrect)|$(@($again.body.data.result.selectedAnswerIds) -join ',')|$($again.body.data.revision)" "True|$(Opt $q[0] 'OK')|2" 'check idempotent'

    # trạng thái: chỉ câu đã chốt có đáp án
    $get = Call 'GET' "$base/$pid_" $token
    $checkedIds = @($get.body.data.checked | ForEach-Object { $_.questionId }) | Sort-Object
    Expect ($checkedIds -join ',') ((@($q[0].id, $q[1].id) | Sort-Object) -join ',') 'checked list'
    if ($get.raw -match [regex]::Escape("EXPL-$tag $($q[2].content.Split(' ')[-1])")) { throw 'Câu chưa chốt bị lộ lời giải.' }
    $unchecked = @($get.body.data.questions | Where-Object { $_.id -eq $q[2].id })[0]
    if ($unchecked.PSObject.Properties.Name -contains 'isCorrect' -or @($unchecked.options | Where-Object { $_.PSObject.Properties.Name -contains 'isCorrect' }).Count) { throw 'Câu chưa chốt có isCorrect.' }

    # lưu nháp: không đổi được câu đã chốt; bỏ qua câu đã chốt thì giữ
    $locked = Call 'PUT' "$base/$pid_/answers" $token @{ revision = 2; answers = @(@{ questionId = $q[0].id; selectedAnswerIds = @((Opt $q[0] 'NO')) }) }
    Expect $locked.status 409 'change checked answer'
    Expect $locked.body.message 'PRACTICE_QUESTION_LOCKED' 'locked code'
    $save = Call 'PUT' "$base/$pid_/answers" $token @{ revision = 2; answers = @(@{ questionId = $q[2].id; selectedAnswerIds = @((Opt $q[2] 'OK')) }) }
    Expect $save.status 200 'save unchecked'
    $kept = @($save.body.data.answers | Where-Object { $_.questionId -eq $q[0].id })[0]
    Expect (@($kept.selectedAnswerIds) -join ',') (Opt $q[0] 'OK') 'checked answer kept'
    $abandon = Call 'DELETE' "$base/$pid_" $token
    Expect $abandon.status 409 'abandon after check'
    Expect $abandon.body.message 'PRACTICE_HAS_CHECKED' 'has checked code'
    # Nợ sau T9: lịch sử báo số câu đã kiểm tra để FE ẩn "Bỏ phiên"
    $hist = @(@((Call 'GET' "$base/mine?page=1&pageSize=10" $token).body.data.items) | Where-Object { $_.id -eq $pid_ })[0]
    Expect $hist.checkedCount 2 'history checkedCount'

    # finish, sau finish, review, retry
    $fin = Call 'POST' "$base/$pid_/finish" $token @{ revision = 3 }
    Expect $fin.status 200 'finish'
    Expect $fin.body.data.correctCount 2 'score uses checked + saved answers'
    $closed = Call 'POST' "$base/$pid_/questions/$($q[2].id)/check" $token @{ revision = 4; selectedAnswerIds = @((Opt $q[2] 'OK')) }
    Expect $closed.status 409 'check unchecked after finish'
    Expect $closed.body.message 'PRACTICE_CLOSED' 'closed code'
    Expect (Call 'POST' $url1 $token @{ revision = 0; selectedAnswerIds = @() }).body.data.result.isCorrect $true 'checked result after finish'
    $review = Call 'GET' "$base/$pid_/review" $token
    Expect (($review.body.data.questions | ForEach-Object { "$($_.isCorrect)" }) -join ',') 'True,False,True' 'review correctness'
    $retry = Call 'POST' "$base/$pid_/retry" $token
    Expect $retry.status 201 'retry finished session'
    Expect $retry.body.data.revealMode 'perQuestion' 'retry keeps perQuestion'
    Expect @($retry.body.data.checked).Count 0 'retry starts unchecked'
    NoLeak $retry 'retry'
    Write-Output "TOBE_T6_PRACTICE_CHECK_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "SELECT Id FROM dbo.Users WHERE Email IN (N'$email', N't6other$tag@example.invalid') OR Id='$other'"
    $subjects = "SELECT Id FROM dbo.Subjects WHERE Name=N'T6 môn $tag'"
    Clean "DELETE dbo.PracticeSessions WHERE UserId IN ($users) OR SubjectId IN ($subjects);"
    Clean "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects);
      DELETE dbo.Questions WHERE SubjectId IN ($subjects);"
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T6-$tag') OR UserId IN ($users);
      DELETE dbo.Classes WHERE ClassCode=N'T6-$tag'; DELETE dbo.Subjects WHERE Id IN ($subjects);"
    Clean "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
