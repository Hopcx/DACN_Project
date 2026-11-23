param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T16 — runner smoke trình duyệt câu điền ngắn: tạo học viên + người ra đề, môn + lớp, 1 câu loại 4 (qua API, bỏ dấu),
# đề + lịch đang mở; học viên bắt đầu bài thi và 1 phiên luyện tập perQuestion qua API; chạy ../DACN_FE/tests/shortAnswerBrowserSmoke.mjs; dọn DB.
# Cần API ở $BaseUrl (DB bản sao đã áp migration T16) và Vite https://localhost:5173.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
if (-not (Test-Path -LiteralPath '../DACN_FE/tests/shortAnswerBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t16fe$tag@example.invalid"
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
function Api([string]$method, [string]$url, [string]$token, $json = $null) {
    $p = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true; Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $json) { $p.ContentType = 'application/json; charset=utf-8'; $p.Body = [Text.Encoding]::UTF8.GetBytes(($json | ConvertTo-Json -Compress -Depth 8)) }
    $r = Invoke-WebRequest @p
    $text = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
    if ([int]$r.StatusCode -ge 300) { throw "$method $url → $([int]$r.StatusCode) $text" }
    return ($text | ConvertFrom-Json).data
}
$mail = $null; $examiner = [Guid]::NewGuid(); $subject = 0; $exam = 0; $student = [Guid]::Empty
try {
    $r = Auth 'register' @{ fullName = 'T16 FE học viên'; userName = "t16fe$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    if ($r.status -ne 202) { throw "register $($r.status)" }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $examinerEmail = "t16feexaminer$tag@example.invalid"
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$examiner',N'T16 FE examiner',N't16feexaminer$tag',N'$examinerEmail',PasswordHash,2,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';
INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);
INSERT dbo.Subjects(Name,Status) VALUES (N'T16 FE môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T16 FE $tag',N'T16FE-$tag',5,'$examiner',@s,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (SCOPE_IDENTITY(),'$student',1);
COMMIT;
SELECT @s;
"@
    $subject = [int]$ids
    $examinerToken = [string](Auth 'login' @{ keyword = $examinerEmail; password = $password }).body.data.accessToken
    $q = (Api 'POST' '/web/questions' $examinerToken @{ subjectId = $subject; questionTypeId = 4; content = "T16 FE thủ đô $tag"; status = 1; tagIds = @()
        answers = @(@{ content = 'Hà Nội'; isCorrect = $true }, @{ content = 'Thăng Long'; isCorrect = $true }); shortAnswerIgnoreDiacritics = $true }).id
    # Câu chưa dùng trong đề để mở "Sửa" (câu đã vào đề thì khóa sửa)
    Api 'POST' '/web/questions' $examinerToken @{ subjectId = $subject; questionTypeId = 4; content = "T16 FE soạn $tag"; status = 1; tagIds = @()
        answers = @(@{ content = 'Hà Nội'; isCorrect = $true }, @{ content = 'Thăng Long'; isCorrect = $true }); shortAnswerIgnoreDiacritics = $true } | Out-Null
    $sched = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T16 FE đề $tag',$subject,1,1,1,1,1,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'T16FE',1,SYSUTCDATETIME(),'$student',SYSUTCDATETIME(),'$student');
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (SCOPE_IDENTITY(),$q,1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'T16 FE lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,60,SYSUTCDATETIME()),1,1);
DECLARE @sc int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT Id,@sc FROM dbo.Classes WHERE ClassCode=N'T16FE-$tag';
COMMIT;
SELECT CONCAT(@e,'|',@sc);
"@
    $exam = [int]$sched.Split('|')[0]; $schedule = [int]$sched.Split('|')[1]
    $attempt = Api 'POST' "/web/exam-schedules/$schedule/attempts" $studentToken
    $practice = Api 'POST' '/web/student/practice' $studentToken @{ subjectId = $subject; questionCount = 1; revealMode = 'perQuestion' }
    $env:DACN_QA_STUDENT_EMAIL = $email; $env:DACN_QA_EXAMINER_EMAIL = $examinerEmail; $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag
    $env:DACN_QA_DATABASE = $Database; $env:DACN_QA_ATTEMPT_ID = $attempt.attemptId; $env:DACN_QA_PRACTICE_ID = $practice.id
    Push-Location ../DACN_FE
    try { & node tests/shortAnswerBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'T16 short answer browser smoke failed.' } }
    finally {
        Pop-Location
        Remove-Item Env:DACN_QA_STUDENT_EMAIL, Env:DACN_QA_EXAMINER_EMAIL, Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_DATABASE,
            Env:DACN_QA_ATTEMPT_ID, Env:DACN_QA_PRACTICE_ID -ErrorAction SilentlyContinue
    }
    $draft = Sql "SELECT JSON_VALUE(DraftJson,'$[0].text') FROM dbo.ExamAttempts WHERE Id='$($attempt.attemptId)';"
    if ($draft -ne 'ha noi') { throw "exam draft text expected 'ha noi', got '$draft'" }
    Write-Output "TOBE_T16_SHORT_ANSWER_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "'$student','$examiner'"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    if ($exam -gt 0) { Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam); DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam;
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" }
    if ($subject -gt 0) { Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T16FE-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T16FE-$tag';
      DELETE dbo.QuestionTags WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE SubjectId=$subject);
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N'$email';"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
