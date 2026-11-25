param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T8: đánh dấu câu ở server cho lượt thi và phiên luyện tập — PUT/GET/DELETE idempotent, thứ tự theo snapshot,
# câu không thuộc nguồn 400, loại nguồn sai 400, người khác 404, đánh dấu sau khi nộp, bỏ phiên luyện tập xóa đánh dấu.
# Chạy từ thư mục gốc DACN_Project (cần Project.Api/.maildrop). Fixture trong transaction, dọn theo tag.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t8qa$tag@example.invalid"
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
    return @{ status = [int]$r.StatusCode; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected $expected, got $actual" } }
function Flags([string]$kind, $sourceId, [string]$token) {
    $r = Call 'GET' "/web/review-flags?sourceKind=$kind&sourceId=$sourceId" $token
    Expect $r.status 200 "GET flags $kind"
    return (@($r.body.data.questionIds) -join ',')
}
function FlagOn([string]$kind, $sourceId, $questionId, [string]$token) { Call 'PUT' '/web/review-flags' $token @{ sourceKind = $kind; sourceId = $sourceId; questionId = $questionId } }
function FlagOff([string]$kind, $sourceId, $questionId, [string]$token) { Call 'DELETE' "/web/review-flags?sourceKind=$kind&sourceId=$sourceId&questionId=$questionId" $token }

$student = [Guid]::Empty; $other = [Guid]::NewGuid(); $mail = $null
try {
    Expect (Auth 'register' @{ fullName = 'T8 Học viên'; userName = "t8qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $token = [string]$login.body.data.accessToken

    # Môn có lớp đã duyệt (2 học viên), 2 câu; bài thi 2 câu với lịch đang mở cho lớp.
    $schedule = [int](Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$student';
INSERT dbo.Subjects(Name,Status) VALUES (N'T8 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES (N'T8 bài thi $tag',@s,2,1,1,10,5,30,1); DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@exam,N'T8',1,SYSUTCDATETIME(),@user,SYSUTCDATETIME(),@user);
DECLARE @variant int=SCOPE_IDENTITY(), @i int=1, @q int;
WHILE @i <= 2 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (CONCAT(N'T8 $tag n',@i),SYSUTCDATETIME(),1,@s,2);
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@variant,@q,5);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T8 $tag',N'T8-$tag',5,@user,@s,1); DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,@user,1);
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$other',N'T8 other',N't8other$tag',N't8other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id=@user;
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,'$other',1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc,RoomId) VALUES (@exam,N'T8 lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,30,SYSUTCDATETIME()),1,1,NULL);
DECLARE @schedule int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES (@class,@schedule);
COMMIT;
SELECT @schedule;
"@)
    $subject = [int](Sql "SELECT Id FROM dbo.Subjects WHERE Name=N'T8 môn $tag';")
    $otherLogin = Auth 'login' @{ keyword = "t8other$tag@example.invalid"; password = $password }
    Expect $otherLogin.status 200 'login other'
    $otherToken = [string]$otherLogin.body.data.accessToken

    # ---- exam
    $start = Call 'POST' "/web/exam-schedules/$schedule/attempts" $token
    Expect $start.status 200 'start attempt'
    $attempt = $start.body.data.attemptId; $eq = @($start.body.data.questions)
    Expect (Flags 'exam' $attempt $token) '' 'no exam flags initially'
    Expect (FlagOn 'exam' $attempt $eq[1].id $token).status 200 'flag exam q2'
    Expect (FlagOn 'EXAM' $attempt $eq[1].id $token).status 200 'flag exam q2 again (kind case-insensitive)'
    Expect (Flags 'exam' $attempt $token) "$($eq[1].id)" 'exam flags after flag'
    Expect (FlagOn 'exam' $attempt ([Guid]::NewGuid()) $token).status 400 'flag question outside snapshot'
    Expect (FlagOn 'mock' $attempt $eq[0].id $token).status 400 'bad source kind'
    Expect (FlagOn 'exam' ([Guid]::Empty) $eq[0].id $token).status 400 'empty source id'
    Expect (FlagOn 'exam' ([Guid]::NewGuid()) $eq[0].id $token).status 404 'unknown attempt'
    Expect (Call 'GET' "/web/review-flags?sourceKind=exam&sourceId=$attempt" $otherToken).status 404 'other GET exam'
    Expect (FlagOn 'exam' $attempt $eq[0].id $otherToken).status 404 'other PUT exam'
    Expect (FlagOff 'exam' $attempt $eq[1].id $otherToken).status 404 'other DELETE exam'
    Expect (Call 'POST' "/web/attempts/$attempt/submit" $token @{ revision = $start.body.data.revision }).status 200 'submit attempt'
    Expect (FlagOn 'exam' $attempt $eq[0].id $token).status 200 'flag after submit'
    Expect (Flags 'exam' $attempt $token) "$($eq[0].id),$($eq[1].id)" 'exam flags in snapshot order'
    Expect (FlagOff 'exam' $attempt $eq[1].id $token).status 200 'unflag exam q2'
    Expect (FlagOff 'exam' $attempt $eq[1].id $token).status 200 'unflag exam q2 again'
    Expect (Flags 'exam' $attempt $token) "$($eq[0].id)" 'exam flags after unflag'

    # ---- practice
    $p1 = Call 'POST' '/web/student/practice' $token @{ subjectId = $subject; questionCount = 2 }
    Expect $p1.status 201 'create practice'
    $pq = @($p1.body.data.questions)
    Expect (FlagOn 'practice' $p1.body.data.id $pq[0].id $token).status 200 'flag practice'
    Expect (FlagOn 'practice' $p1.body.data.id $eq[0].id $token).status 400 'flag exam question on practice source'
    Expect (FlagOn 'practice' $attempt $eq[0].id $token).status 404 'exam id as practice source'
    Expect (Flags 'practice' $p1.body.data.id $token) "$($pq[0].id)" 'practice flags'
    Expect (Call 'GET' "/web/review-flags?sourceKind=practice&sourceId=$($p1.body.data.id)" $otherToken).status 404 'other GET practice'
    Expect (Call 'POST' "/web/student/practice/$($p1.body.data.id)/finish" $token @{ revision = 0 }).status 200 'finish practice'
    Expect (FlagOn 'practice' $p1.body.data.id $pq[1].id $token).status 200 'flag practice after finish'
    Expect (@((Flags 'practice' $p1.body.data.id $token).Split(','))).Count 2 'two practice flags'
    $p2 = Call 'POST' '/web/student/practice' $token @{ subjectId = $subject; questionCount = 1 }
    Expect (FlagOn 'practice' $p2.body.data.id $p2.body.data.questions[0].id $token).status 200 'flag abandoned session'
    Expect (Call 'DELETE' "/web/student/practice/$($p2.body.data.id)" $token).status 200 'abandon session'
    Expect (Sql "SELECT COUNT(*) FROM dbo.ReviewFlags WHERE SourceKind='practice' AND SourceId='$($p2.body.data.id)';") '0' 'abandon removes flags'
    Expect (Sql "SELECT COUNT(*) FROM dbo.ReviewFlags WHERE UserId='$student';") '3' 'remaining flags (1 exam + 2 practice)'

    # ---- T19 (nợ X7): các ca còn thiếu
    Expect (FlagOn 'practice' $p2.body.data.id $p2.body.data.questions[0].id $token).status 404 'PUT on abandoned session'
    Expect (FlagOn 'practice' $p1.body.data.id $pq[0].id $otherToken).status 404 'other PUT practice'
    Expect (FlagOff 'practice' $p1.body.data.id $pq[0].id $otherToken).status 404 'other DELETE practice'
    Expect (@((Flags 'practice' $p1.body.data.id $token).Split(','))).Count 2 'other cannot change practice flags'
    $member = "UPDATE dbo.ClassUsers SET Status={0} WHERE UserId='$student' AND ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T8-$tag');"
    Sql ($member -f 0) | Out-Null   # thành viên chưa duyệt
    Expect (Call 'GET' "/web/review-flags?sourceKind=exam&sourceId=$attempt" $token).status 404 'pending member GET exam'
    Expect (FlagOn 'exam' $attempt $eq[1].id $token).status 404 'pending member PUT exam'
    Expect (FlagOff 'exam' $attempt $eq[0].id $token).status 404 'pending member DELETE exam'
    Sql ($member -f 1) | Out-Null
    $classStatus = "UPDATE dbo.Classes SET Status={0} WHERE ClassCode=N'T8-$tag';"
    Sql ($classStatus -f 0) | Out-Null   # lớp đóng
    Expect (Call 'GET' "/web/review-flags?sourceKind=exam&sourceId=$attempt" $token).status 404 'closed class GET exam'
    Expect (FlagOn 'exam' $attempt $eq[1].id $token).status 404 'closed class PUT exam'
    Sql ($classStatus -f 1) | Out-Null
    Expect (Flags 'exam' $attempt $token) "$($eq[0].id)" 'exam flags unchanged after 404s'
    # PUT song song với bỏ phiên: app lock theo học viên xếp hàng hai thao tác → không còn cờ mồ côi
    foreach ($round in 1..3) {
        $p3 = Call 'POST' '/web/student/practice' $token @{ subjectId = $subject; questionCount = 2 }
        Expect $p3.status 201 "create practice for race $round"
        $p3Id = $p3.body.data.id; $qIds = @($p3.body.data.questions | ForEach-Object { $_.id })
        # Review T19 N5: mọi runspace chờ chung một mốc giờ rồi mới gửi, để PUT và bỏ phiên thật sự xen kẽ
        $at = [DateTime]::UtcNow.AddSeconds(3)
        $statuses = 0..7 | ForEach-Object -ThrottleLimit 8 -Parallel {
            $u = "$($using:BaseUrl)/web/review-flags"; $h = @{ Authorization = "Bearer $($using:token)" }
            $wait = ($using:at) - [DateTime]::UtcNow
            if ($wait -gt [TimeSpan]::Zero) { Start-Sleep -Milliseconds ([int]$wait.TotalMilliseconds) }
            if ($_ -eq 4) {
                $r = Invoke-WebRequest "$($using:BaseUrl)/web/student/practice/$($using:p3Id)" -Method Delete -Headers $h -SkipCertificateCheck -SkipHttpErrorCheck
                "abandon:$([int]$r.StatusCode)"
            } else {
                $body = @{ sourceKind = 'practice'; sourceId = $using:p3Id; questionId = ($using:qIds)[$_ % 2] } | ConvertTo-Json -Compress
                $r = Invoke-WebRequest $u -Method Put -Headers $h -ContentType 'application/json' -Body $body -SkipCertificateCheck -SkipHttpErrorCheck
                "put:$([int]$r.StatusCode)"
            }
        }
        if ('abandon:200' -notin $statuses) { throw "race $round abandon failed: $($statuses -join ',')" }
        if (@($statuses | Where-Object { $_ -notin 'abandon:200', 'put:200', 'put:404' }).Count -gt 0) { throw "race $round unexpected: $($statuses -join ',')" }
        Expect (Sql "SELECT CONCAT((SELECT COUNT(*) FROM dbo.PracticeSessions WHERE Id='$p3Id'),'|',(SELECT COUNT(*) FROM dbo.ReviewFlags WHERE SourceKind='practice' AND SourceId='$p3Id'));") '0|0' "race $round leaves no orphan flag"
        Write-Output "  race ${round}: $(($statuses | Sort-Object) -join ',')"
    }
    Write-Output "TOBE_T8_REVIEW_FLAGS_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "SELECT Id FROM dbo.Users WHERE Email IN (N'$email', N't8other$tag@example.invalid') OR Id='$other'"
    $subjects = "SELECT Id FROM dbo.Subjects WHERE Name=N'T8 môn $tag'"
    $exams = "SELECT Id FROM dbo.Exams WHERE Name=N'T8 bài thi $tag'"
    Clean "DELETE dbo.ReviewFlags WHERE UserId IN ($users);"
    Clean "DELETE dbo.PracticeSessions WHERE UserId IN ($users) OR SubjectId IN ($subjects);"
    Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.ExamAttempts WHERE ExamId IN ($exams);
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId IN ($exams));
      DELETE dbo.ExamSchedules WHERE ExamId IN ($exams);
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId IN ($exams));
      DELETE dbo.ExamDetails WHERE ExamId IN ($exams); DELETE dbo.Exams WHERE Id IN ($exams);"
    Clean "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects); DELETE dbo.Questions WHERE SubjectId IN ($subjects);"
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T8-$tag') OR UserId IN ($users);
      DELETE dbo.Classes WHERE ClassCode=N'T8-$tag'; DELETE dbo.Subjects WHERE Id IN ($subjects);"
    Clean "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
