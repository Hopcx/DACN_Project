param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T18 — runner smoke trình duyệt đoạn dùng chung: học viên + người ra đề, 2 môn + lớp, 1 đoạn (2 câu + ảnh) trong đề và 1 đoạn chưa vào đề,
# học viên bắt đầu bài thi + luyện tập qua API; chạy ../DACN_FE/tests/questionGroupBrowserSmoke.mjs; dọn DB + file media.
# Cần API ở $BaseUrl (DB bản sao đã áp migration T18) và Vite https://localhost:5173.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
if (-not (Test-Path -LiteralPath '../DACN_FE/tests/questionGroupBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t18fe$tag@example.invalid"
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
function Api([string]$method, [string]$url, [string]$token, $json = $null, $form = $null) {
    $p = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true; Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $json) { $p.ContentType = 'application/json; charset=utf-8'; $p.Body = [Text.Encoding]::UTF8.GetBytes(($json | ConvertTo-Json -Compress -Depth 8)) }
    if ($null -ne $form) { $p.Form = $form }
    $r = Invoke-WebRequest @p
    $text = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
    if ([int]$r.StatusCode -ge 300) { throw "$method $url → $([int]$r.StatusCode) $text" }
    return ($text | ConvertFrom-Json).data
}
$mail = $null; $examiner = [Guid]::NewGuid(); $subject = 0; $subject2 = 0; $exam = 0; $student = [Guid]::Empty
$mediaRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location) 'dacn-media-dev'))
$pngBase = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==')
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t18fe-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
$pngFile = Join-Path $work 'doan.png'; [IO.File]::WriteAllBytes($pngFile, [byte[]]($pngBase + [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)))
$mail = $null; $examiner = [Guid]::NewGuid(); $subject = 0; $subject2 = 0; $exam = 0; $student = [Guid]::Empty
try {
    $r = Auth 'register' @{ fullName = 'T18 FE học viên'; userName = "t18fe$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    if ($r.status -ne 202) { throw "register $($r.status)" }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $examinerEmail = "t18feexaminer$tag@example.invalid"
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$examiner',N'T18 FE examiner',N't18feexaminer$tag',N'$examinerEmail',PasswordHash,2,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';
INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);
INSERT dbo.Subjects(Name,Status) VALUES (N'T18 FE môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Subjects(Name,Status) VALUES (N'T18 FE môn soạn $tag',1); DECLARE @s2 int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int, @list nvarchar(200)=N'';
WHILE @i <= 3 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (CONCAT(N'T18 FE câu ',@i,N' $tag'),SYSUTCDATETIME(),1,@s,2); SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  SET @list = CONCAT(@list, @q, N','); SET @i += 1;
END
SET @i = 1;
WHILE @i <= 2 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (CONCAT(N'T18 FE câu soạn ',@i,N' $tag'),SYSUTCDATETIME(),1,@s2,2); SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  SET @list = CONCAT(@list, @q, N','); SET @i += 1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T18 FE $tag',N'T18FE-$tag',5,'$examiner',@s,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (SCOPE_IDENTITY(),'$student',1);
COMMIT;
SELECT CONCAT(@s,'|',@s2,'|',@list);
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $subject2 = [int]$p[1]; $q = @($p[2].TrimEnd(',').Split(',') | ForEach-Object { [int]$_ })
    $examinerToken = [string](Auth 'login' @{ keyword = $examinerEmail; password = $password }).body.data.accessToken
    $group = Api 'POST' '/web/question-groups' $examinerToken @{ subjectId = $subject; title = "T18 FE đoạn $tag"; content = 'Đọc đoạn văn sau và trả lời câu 1–2.'; questionIds = @($q[0], $q[1]) }
    $media = Api 'POST' '/web/media' $examinerToken $null @{ file = Get-Item $pngFile }
    Api 'POST' "/web/question-groups/$($group.id)/media" $examinerToken @{ mediaId = $media.id } | Out-Null
    Api 'POST' '/web/question-groups' $examinerToken @{ subjectId = $subject2; title = "T18 FE đoạn soạn $tag"; content = 'Đoạn để thử trình soạn.'; questionIds = @($q[3], $q[4]) } | Out-Null
    $sched = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T18 FE đề $tag',$subject,3,1,1,3,1,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'T18FE',1,SYSUTCDATETIME(),'$student',SYSUTCDATETIME(),'$student');
DECLARE @d int=SCOPE_IDENTITY();
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@d,$($q[0]),1);
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@d,$($q[1]),1);
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@d,$($q[2]),1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'T18 FE lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,60,SYSUTCDATETIME()),1,1);
DECLARE @sc int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT Id,@sc FROM dbo.Classes WHERE ClassCode=N'T18FE-$tag';
COMMIT;
SELECT CONCAT(@e,'|',@sc);
"@
    $exam = [int]$sched.Split('|')[0]; $schedule = [int]$sched.Split('|')[1]
    $attempt = Api 'POST' "/web/exam-schedules/$schedule/attempts" $studentToken
    $practice = Api 'POST' '/web/student/practice' $studentToken @{ subjectId = $subject; questionCount = 3 }
    $env:DACN_QA_STUDENT_EMAIL = $email; $env:DACN_QA_EXAMINER_EMAIL = $examinerEmail; $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag
    $env:DACN_QA_DATABASE = $Database; $env:DACN_QA_ATTEMPT_ID = $attempt.attemptId; $env:DACN_QA_PRACTICE_ID = $practice.id
    Push-Location ../DACN_FE
    try { & node tests/questionGroupBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'T18 question group browser smoke failed.' } }
    finally {
        Pop-Location
        Remove-Item Env:DACN_QA_STUDENT_EMAIL, Env:DACN_QA_EXAMINER_EMAIL, Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_DATABASE,
            Env:DACN_QA_ATTEMPT_ID, Env:DACN_QA_PRACTICE_ID -ErrorAction SilentlyContinue
    }
    Write-Output "TOBE_T18_QUESTION_GROUPS_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "'$student','$examiner'"
    $mediaIds = try { Sql "SELECT STRING_AGG(CONVERT(nvarchar(36),Id),',') FROM dbo.MediaFiles WHERE UploadedBy='$examiner';" } catch { '' }
    Clean "DELETE dbo.MediaReferences WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner');"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    if ($exam -gt 0) { Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam); DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam;
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" }
    foreach ($s in @($subject, $subject2) | Where-Object { $_ -gt 0 }) {
        Clean "DELETE gm FROM dbo.QuestionGroupMedia gm JOIN dbo.QuestionGroups g ON g.Id=gm.GroupId WHERE g.SubjectId=$s;
      UPDATE dbo.Questions SET GroupId=NULL, GroupOrder=NULL WHERE SubjectId=$s; DELETE dbo.QuestionGroups WHERE SubjectId=$s;
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$s; DELETE dbo.Questions WHERE SubjectId=$s;"
    }
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T18FE-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T18FE-$tag';
      DELETE dbo.Subjects WHERE Id IN ($subject,$subject2);"
    Clean "DELETE dbo.MediaFiles WHERE UploadedBy='$examiner';"
    foreach ($id in ("$mediaIds" -split ',' | Where-Object { $_ })) {
        $n2 = $id.Replace('-', '').ToLowerInvariant(); Remove-Item -LiteralPath (Join-Path (Join-Path $mediaRoot $n2.Substring(0, 2)) $n2) -ErrorAction SilentlyContinue
    }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N'$email';"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
