param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T15 — runner smoke trình duyệt media phía học viên: tạo học viên/người ra đề/Admin, môn + lớp, 1 câu có ảnh PNG thật + MP3,
# đề + lịch đang mở; học viên bắt đầu bài thi và 1 phiên luyện tập qua API; 1 ảnh mồ côi (lùi thời điểm tải 2 ngày);
# chạy ../DACN_FE/tests/studentMediaBrowserSmoke.mjs; dọn DB + file media. Cần API ở $BaseUrl và Vite https://localhost:5173.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
if (-not (Test-Path -LiteralPath '../DACN_FE/tests/studentMediaBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
$mediaRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location) 'dacn-media-dev'))
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t15fe$tag@example.invalid"
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
    if ($null -ne $json) { $p.ContentType = 'application/json'; $p.Body = ($json | ConvertTo-Json -Compress -Depth 8) }
    if ($null -ne $form) { $p.Form = $form }
    $r = Invoke-WebRequest @p
    if ([int]$r.StatusCode -ge 300) { throw "$method $url → $([int]$r.StatusCode) $($r.Content)" }
    return ($r.Content | ConvertFrom-Json).data
}
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t15fe-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
$pngBase = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==')
function Save([string]$name, [byte[]]$bytes) { $p = Join-Path $work $name; [IO.File]::WriteAllBytes($p, $bytes); return Get-Item $p }
$pngFile = Save 'hinh-cau-hoi.png' ([byte[]]($pngBase + [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)))
$mp3File = Save 'am-thanh.mp3' ([byte[]]([Text.Encoding]::ASCII.GetBytes("ID3`u{4}`0`0`0`0`0`0") + [Security.Cryptography.RandomNumberGenerator]::GetBytes(2000)))
$orphanName = "mo-coi-$tag.png"
$orphanFile = Save $orphanName ([byte[]]($pngBase + [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)))
$mail = $null; $examiner = [Guid]::NewGuid(); $admin = [Guid]::NewGuid(); $subject = 0; $exam = 0; $student = [Guid]::Empty
try {
    $r = Auth 'register' @{ fullName = 'T15 FE học viên'; userName = "t15fe$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    if ($r.status -ne 202) { throw "register $($r.status)" }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $examinerEmail = "t15feexaminer$tag@example.invalid"; $adminEmail = "t15feadmin$tag@example.invalid"
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$examiner',N'T15 FE examiner',N't15feexaminer$tag',N'$examinerEmail',PasswordHash,2,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';
INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$admin',N'T15 FE admin',N't15feadmin$tag',N'$adminEmail',PasswordHash,1,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';
INSERT dbo.Subjects(Name,Status) VALUES (N'T15 FE môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T15 FE câu $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @q int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T15 FE $tag',N'T15FE-$tag',5,'$admin',@s,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (SCOPE_IDENTITY(),'$student',1);
COMMIT;
SELECT CONCAT(@s,'|',@q);
"@
    $subject = [int]$ids.Split('|')[0]; $q = [int]$ids.Split('|')[1]
    $examinerToken = [string](Auth 'login' @{ keyword = $examinerEmail; password = $password }).body.data.accessToken
    $png = Api 'POST' '/web/media' $examinerToken $null @{ file = $pngFile }
    $mp3 = Api 'POST' '/web/media' $examinerToken $null @{ file = $mp3File }
    $orphan = Api 'POST' '/web/media' $examinerToken $null @{ file = $orphanFile }
    Api 'POST' "/web/questions/$q/media" $examinerToken @{ mediaId = $png.id } | Out-Null
    Api 'POST' "/web/questions/$q/media" $examinerToken @{ mediaId = $mp3.id } | Out-Null
    Sql "UPDATE dbo.MediaFiles SET UploadedAtUtc=DATEADD(day,-2,SYSUTCDATETIME()) WHERE Id='$($orphan.id)';" | Out-Null
    $sched = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T15 FE đề $tag',$subject,1,1,1,1,1,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'T15FE',1,SYSUTCDATETIME(),'$student',SYSUTCDATETIME(),'$student');
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (SCOPE_IDENTITY(),$q,1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'T15 FE lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,60,SYSUTCDATETIME()),1,1);
DECLARE @sc int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT Id,@sc FROM dbo.Classes WHERE ClassCode=N'T15FE-$tag';
COMMIT;
SELECT CONCAT(@e,'|',@sc);
"@
    $exam = [int]$sched.Split('|')[0]; $schedule = [int]$sched.Split('|')[1]
    $attempt = Api 'POST' "/web/exam-schedules/$schedule/attempts" $studentToken
    $practice = Api 'POST' '/web/student/practice' $studentToken @{ subjectId = $subject; questionCount = 1 }
    $env:DACN_QA_STUDENT_EMAIL = $email; $env:DACN_QA_ADMIN_EMAIL = $adminEmail; $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag
    $env:DACN_QA_DATABASE = $Database; $env:DACN_QA_ATTEMPT_ID = $attempt.attemptId; $env:DACN_QA_PRACTICE_ID = $practice.id; $env:DACN_QA_ORPHAN_NAME = $orphanName
    Push-Location ../DACN_FE
    try { & node tests/studentMediaBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'T15 student media browser smoke failed.' } }
    finally {
        Pop-Location
        Remove-Item Env:DACN_QA_STUDENT_EMAIL, Env:DACN_QA_ADMIN_EMAIL, Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_DATABASE,
            Env:DACN_QA_ATTEMPT_ID, Env:DACN_QA_PRACTICE_ID, Env:DACN_QA_ORPHAN_NAME -ErrorAction SilentlyContinue
    }
    if ((Sql "SELECT COUNT(*) FROM dbo.MediaFiles WHERE Id='$($orphan.id)';") -ne '0') { throw 'orphan row still exists' }
    Write-Output "TOBE_T15_STUDENT_MEDIA_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $mediaIds = try { Sql "SELECT STRING_AGG(CONVERT(nvarchar(36),Id),',') FROM dbo.MediaFiles WHERE UploadedBy='$examiner';" } catch { '' }
    $users = "'$student','$examiner','$admin'"
    Clean "DELETE dbo.MediaReferences WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner');"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    if ($exam -gt 0) { Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam); DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam;
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" }
    if ($subject -gt 0) { Clean "DELETE dbo.QuestionMedia WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE SubjectId=$subject);
      DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T15FE-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T15FE-$tag';
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" }
    Clean "DELETE dbo.QuestionMedia WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner'); DELETE dbo.MediaFiles WHERE UploadedBy='$examiner';"
    foreach ($id in ("$mediaIds" -split ',' | Where-Object { $_ })) {
        $n = $id.Replace('-', '').ToLowerInvariant(); Remove-Item -LiteralPath (Join-Path (Join-Path $mediaRoot $n.Substring(0, 2)) $n) -ErrorAction SilentlyContinue
    }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N'$email';"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
