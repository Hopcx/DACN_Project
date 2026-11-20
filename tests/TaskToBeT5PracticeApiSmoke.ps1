param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T5: Luyện tập — options theo lớp đã duyệt, tạo phiên + lọc, thiếu câu 409, không lộ đáp án/lời giải,
# lưu nháp + revision, finish idempotent + chấm, review sau finish, chỉ chủ phiên, giới hạn 3 phiên mở, bỏ phiên, retry.
# Dấu ASCII EXPL-<tag> trong lời giải để dò lộ (API escape tiếng Việt thành \uXXXX).
# Chạy từ thư mục gốc DACN_Project (cần Project.Api/.maildrop). Fixture theo tag, dọn trong finally.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t5qa$tag@example.invalid"
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
    foreach ($needle in 'isCorrect', 'explanation', "EXPL-$tag", '"point"') {
        if ($response.raw -match [regex]::Escape($needle)) { throw "$case lộ '$needle' trước khi finish." }
    }
}
function Create($token, [hashtable]$body) { Call 'POST' '/web/student/practice' $token $body }

$student = [Guid]::Empty; $other = [Guid]::NewGuid(); $subjectA = 0; $subjectB = 0; $class = 0; $mail = $null
try {
    Expect (Auth 'register' @{ fullName = 'T5 Học viên'; userName = "t5qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $token = [string]$login.body.data.accessToken

    # Môn A (lớp đã duyệt): 12 câu dùng được — chương 1 (6 câu, mức 1 ở câu lẻ), chương 2 (6 câu); tag gắn 3 câu đầu chương 1;
    # câu lẻ có lời giải. Thêm 1 câu ngừng (Status 0) và 1 câu tập đáp án lỗi (loại 2, hai đáp án đúng) — không được tính.
    # Môn B: không có lớp. Học viên thứ hai (chèn SQL, cùng mật khẩu) để kiểm chủ phiên.
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$student';
INSERT dbo.Subjects(Name,Status) VALUES (N'T5 môn A $tag',1); DECLARE @a int=SCOPE_IDENTITY();
INSERT dbo.Subjects(Name,Status) VALUES (N'T5 môn B $tag',1); DECLARE @b int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@a,N'T5 c1 $tag',1,1,SYSUTCDATETIME()); DECLARE @c1 int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@a,N'T5 c2 $tag',2,1,SYSUTCDATETIME()); DECLARE @c2 int=SCOPE_IDENTITY();
INSERT dbo.Tags(Name,NormalizedName,Status,CreatedAt) VALUES (N'T5 tag $tag',N't5 tag $tag',1,SYSUTCDATETIME()); DECLARE @t int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int, @ch int, @lv int;
WHILE @i <= 14 BEGIN
  SET @ch = CASE WHEN @i <= 6 OR @i = 13 THEN @c1 ELSE @c2 END; SET @lv = CASE WHEN @i % 2 = 1 THEN 1 ELSE 2 END;
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId,ChapterId,Explanation)
  VALUES (CONCAT(N'T5 $tag c',CASE WHEN @ch=@c1 THEN 1 ELSE 2 END,N' l',@lv,N' n',@i),SYSUTCDATETIME(),
    CASE WHEN @i = 13 THEN 0 ELSE 1 END,@a,2,@lv,@ch,CASE WHEN @i % 2 = 1 THEN N'EXPL-$tag lời giải' ELSE NULL END);
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',CASE WHEN @i = 14 THEN 1 ELSE 0 END,1);
  IF @i <= 3 INSERT dbo.QuestionTags(QuestionId,TagId) VALUES (@q,@t);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T5 $tag',N'T5-$tag',5,@user,@a,1); DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,@user,1);
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$other',N'T5 other',N't5other$tag',N't5other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id=@user;
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,'$other',1);
COMMIT;
SELECT CONCAT(@a,',',@b,',',@c1,',',@c2,',',@t,',',@class);
"@
    $parts = $ids.Split(','); $subjectA = [int]$parts[0]; $subjectB = [int]$parts[1]; $c1 = [int]$parts[2]; $c2 = [int]$parts[3]; $tagId = [int]$parts[4]; $class = [int]$parts[5]
    $otherLogin = Auth 'login' @{ keyword = "t5other$tag@example.invalid"; password = $password }
    Expect $otherLogin.status 200 'login other'
    $otherToken = [string]$otherLogin.body.data.accessToken

    # options
    $options = Call 'GET' '/web/student/practice/options' $token
    Expect $options.status 200 'options'
    Expect $options.body.data.maxQuestions 50 'maxQuestions'
    $a = @($options.body.data.subjects | Where-Object { $_.id -eq $subjectA })[0]
    if (-not $a) { throw 'Môn A phải có trong options.' }
    if (@($options.body.data.subjects | Where-Object { $_.id -eq $subjectB }).Count -ne 0) { throw 'Môn B (không có lớp) không được có trong options.' }
    Expect $a.questionCount 12 'usable count (bỏ câu ngừng và câu lỗi)'
    Expect (@($a.chapters | Where-Object { $_.id -eq $c1 })[0].questionCount) 6 'chapter 1 count'
    Expect (@($a.tags | Where-Object { $_.id -eq $tagId })[0].questionCount) 3 'tag count'

    # tạo: lỗi tiêu chí, môn không được phép, thiếu câu
    Expect (Create $token @{ subjectId = $subjectA; questionCount = 0 }).status 400 'questionCount 0'
    Expect (Create $token @{ subjectId = $subjectA; questionCount = 51 }).status 400 'questionCount 51'
    Expect (Create $token @{ subjectId = $subjectA; questionCount = 5; revealMode = 'all' }).status 400 'bad revealMode'
    Expect (Create $token @{ subjectId = $subjectA; questionCount = 5; tagIds = @(0) }).status 400 'tag id 0'
    Expect (Create $token @{ subjectId = $subjectB; questionCount = 1 }).status 403 'subject without class'
    Expect (Create $token @{ subjectId = $subjectA; chapterIds = @($c1 + 1000000); questionCount = 1 }).status 400 'unknown chapter'
    Sql "UPDATE dbo.Chapters SET Status=255 WHERE Id=$c2;" | Out-Null
    Expect (Create $token @{ subjectId = $subjectA; chapterIds = @($c2); questionCount = 1 }).status 400 'hidden chapter'
    Sql "UPDATE dbo.Chapters SET Status=1 WHERE Id=$c2;" | Out-Null
    $short = Create $token @{ subjectId = $subjectA; questionCount = 13 }
    Expect $short.status 409 'not enough (all)'
    Expect $short.body.message 'PRACTICE_NOT_ENOUGH_QUESTIONS' 'not enough code'
    Expect $short.body.data.available 12 'available all'
    $short2 = Create $token @{ subjectId = $subjectA; chapterIds = @($c1); tagIds = @($tagId); questionCount = 4 }
    Expect $short2.body.data.available 3 'available chapter+tag'

    # phiên 1: chương 1 + mức 1 → 3 câu (n1, n3, n5)
    $s1 = Create $token @{ subjectId = $subjectA; chapterIds = @($c1); questionLevelIds = @(1); questionCount = 3 }
    Expect $s1.status 201 'create s1'
    NoLeak $s1 'create'
    $s1Id = $s1.body.data.id
    $contents = @($s1.body.data.questions | ForEach-Object { $_.content }) | Sort-Object
    Expect ($contents -join '|') "T5 $tag c1 l1 n1|T5 $tag c1 l1 n3|T5 $tag c1 l1 n5" 'filtered questions'
    Expect $s1.body.data.revision 0 'initial revision'
    Expect $s1.body.data.criteria.questionCount 3 'criteria stored'
    $get = Call 'GET' "/web/student/practice/$s1Id" $token
    Expect $get.status 200 'get s1'
    NoLeak $get 'get'

    # chủ phiên
    foreach ($case in @(@('GET', "/web/student/practice/$s1Id", $null), @('GET', "/web/student/practice/$s1Id/review", $null),
        @('PUT', "/web/student/practice/$s1Id/answers", @{ revision = 0; answers = @() }),
        @('POST', "/web/student/practice/$s1Id/finish", @{ revision = 0 }), @('DELETE', "/web/student/practice/$s1Id", $null))) {
        Expect (Call $case[0] $case[1] $otherToken $case[2]).status 404 "other student $($case[0]) $($case[1])"
    }

    # lưu nháp: câu 1 đúng, câu 2 sai, câu 3 bỏ trống
    $qs = @($s1.body.data.questions)
    $answers = @(
        @{ questionId = $qs[0].id; selectedAnswerIds = @(@($qs[0].options | Where-Object { $_.content -eq 'OK' })[0].id) },
        @{ questionId = $qs[1].id; selectedAnswerIds = @(@($qs[1].options | Where-Object { $_.content -eq 'NO' })[0].id) })
    $save = Call 'PUT' "/web/student/practice/$s1Id/answers" $token @{ revision = 0; answers = $answers }
    Expect $save.status 200 'save'
    Expect $save.body.data.revision 1 'revision after save'
    NoLeak $save 'save'
    Expect (Call 'PUT' "/web/student/practice/$s1Id/answers" $token @{ revision = 0; answers = $answers }).status 409 'stale revision save'
    Expect (Call 'PUT' "/web/student/practice/$s1Id/answers" $token @{ revision = 1; answers = @(@{ questionId = [Guid]::NewGuid(); selectedAnswerIds = @() }) }).status 400 'invalid selection'
    $early = Call 'GET' "/web/student/practice/$s1Id/review" $token
    Expect $early.status 409 'review before finish'
    Expect $early.body.message 'PRACTICE_NOT_FINISHED' 'review before finish code'

    # finish + idempotent
    Expect (Call 'POST' "/web/student/practice/$s1Id/finish" $token @{ revision = 0 }).status 409 'finish stale revision'
    $fin = Call 'POST' "/web/student/practice/$s1Id/finish" $token @{ revision = 1 }
    Expect $fin.status 200 'finish'
    Expect $fin.body.data.correctCount 1 'correctCount'
    Expect $fin.body.data.state 'finished' 'finished state'
    $fin2 = Call 'POST' "/web/student/practice/$s1Id/finish" $token @{ revision = 0 }
    Expect $fin2.status 200 'finish again'
    Expect "$($fin2.body.data.correctCount)|$($fin2.body.data.revision)|$($fin2.body.data.finishedAtUtc)" "$($fin.body.data.correctCount)|$($fin.body.data.revision)|$($fin.body.data.finishedAtUtc)" 'finish idempotent'
    Expect (Call 'PUT' "/web/student/practice/$s1Id/answers" $token @{ revision = 2; answers = @() }).status 409 'save after finish'

    # review
    $review = Call 'GET' "/web/student/practice/$s1Id/review" $token
    Expect $review.status 200 'review'
    $rq = @($review.body.data.questions)
    Expect $rq.Count 3 'review count'
    Expect (($rq | ForEach-Object { "$($_.isCorrect)" }) -join ',') 'True,False,False' 'per-question correctness'
    if (@($rq | Where-Object { $_.explanation -ne "EXPL-$tag lời giải" }).Count -ne 0) { throw 'Câu lẻ phải có lời giải trong review.' }
    if (@($rq[0].options | Where-Object { $_.isCorrect }).Count -ne 1) { throw 'Review phải có đáp án đúng.' }

    # giới hạn 3 phiên mở, bỏ phiên
    $open = @()
    foreach ($n in 1..3) { $r = Create $token @{ subjectId = $subjectA; questionCount = 1 }; Expect $r.status 201 "open $n"; $open += $r.body.data.id }
    $limit = Create $token @{ subjectId = $subjectA; questionCount = 1 }
    Expect $limit.status 409 '4th open session'
    Expect $limit.body.message 'PRACTICE_TOO_MANY_OPEN' 'too many open code'
    Expect (Call 'DELETE' "/web/student/practice/$s1Id" $token).status 409 'delete finished'
    Expect (Call 'DELETE' "/web/student/practice/$($open[2])" $token).status 200 'abandon'
    Expect (Call 'GET' "/web/student/practice/$($open[2])" $token).status 404 'abandoned gone'

    # retry: bỏ câu đã ngừng, cùng revealMode, criteria.retryOf
    Sql "UPDATE dbo.Questions SET Status=0 WHERE Content=N'T5 $tag c1 l1 n5';" | Out-Null
    $retry = Call 'POST' "/web/student/practice/$s1Id/retry" $token
    Expect $retry.status 201 'retry'
    NoLeak $retry 'retry'
    Expect $retry.body.data.questionCount 2 'retry drops inactive question'
    Expect $retry.body.data.criteria.retryOf $s1Id 'retryOf'
    Expect $retry.body.data.revealMode 'end' 'retry revealMode'
    Expect (Call 'POST' "/web/student/practice/$s1Id/retry" $token).status 409 'retry over open limit'

    # perQuestion được lưu (T6 mới có kiểm tra từng câu)
    Expect (Call 'DELETE' "/web/student/practice/$($open[1])" $token).status 200 'abandon 2'
    $pq = Create $token @{ subjectId = $subjectA; questionCount = 2; revealMode = 'perQuestion' }
    Expect $pq.status 201 'create perQuestion'
    Expect $pq.body.data.revealMode 'perQuestion' 'perQuestion stored'
    NoLeak $pq 'perQuestion'

    # lịch sử
    $mine = Call 'GET' '/web/student/practice/mine?page=1&pageSize=2' $token
    Expect $mine.status 200 'mine'
    Expect $mine.body.data.totalCount 4 'history total (s1, open0, retry, perQuestion)'
    Expect @($mine.body.data.items).Count 2 'page size'
    Expect $mine.body.data.items[0].id $pq.body.data.id 'newest first'
    Expect (Call 'GET' '/web/student/practice/mine?page=0' $token).status 400 'bad page'
    # Nợ sau T9: thời gian làm bài và số câu đã kiểm tra trong lịch sử
    $all = @((Call 'GET' '/web/student/practice/mine?page=1&pageSize=10' $token).body.data.items)
    $doneItem = @($all | Where-Object { $_.id -eq $s1Id })[0]; $openItem = @($all | Where-Object { $_.id -eq $pq.body.data.id })[0]
    if ($null -eq $doneItem.durationSeconds -or [long]$doneItem.durationSeconds -lt 0) { throw "durationSeconds của phiên đã nộp sai: $($doneItem.durationSeconds)" }
    if ($null -ne $openItem.durationSeconds) { throw 'Phiên đang làm không có durationSeconds.' }
    Expect "$($doneItem.checkedCount)|$($openItem.checkedCount)" '0|0' 'checkedCount (end mode)'
    Expect (Call 'GET' '/web/student/practice/mine' $otherToken).body.data.totalCount 0 'other history empty'
    Write-Output "TOBE_T5_PRACTICE_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    # Dọn theo tag (không phụ thuộc biến ID); mỗi bước try riêng để lỗi dọn không che lỗi gốc.
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    if ($student -eq [Guid]::Empty) { try { $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$email';"; if ($found) { $student = [Guid]$found } } catch {} }
    $users = "SELECT Id FROM dbo.Users WHERE Email IN (N'$email', N't5other$tag@example.invalid') OR Id='$other'"
    $subjects = "SELECT Id FROM dbo.Subjects WHERE Name IN (N'T5 môn A $tag', N'T5 môn B $tag')"
    Clean "DELETE dbo.PracticeSessions WHERE UserId IN ($users) OR SubjectId IN ($subjects);"
    Clean "DELETE qt FROM dbo.QuestionTags qt JOIN dbo.Questions q ON q.Id=qt.QuestionId WHERE q.SubjectId IN ($subjects);
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects);
      DELETE dbo.Questions WHERE SubjectId IN ($subjects); DELETE dbo.Chapters WHERE SubjectId IN ($subjects);"
    Clean "DELETE dbo.Tags WHERE Name=N'T5 tag $tag';"
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T5-$tag') OR UserId IN ($users);
      DELETE dbo.Classes WHERE ClassCode=N'T5-$tag'; DELETE dbo.Subjects WHERE Id IN ($subjects);"
    Clean "DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
