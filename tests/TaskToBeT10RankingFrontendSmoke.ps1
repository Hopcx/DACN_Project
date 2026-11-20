param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T10 — runner smoke trình duyệt Xếp hạng lớp: tạo giảng viên + 6 học viên có điểm (thang 10: 10,9,8,7,6,5) trong 1 lịch
# đã kết thúc và 1 lịch chưa kết thúc; chạy ../DACN_FE/tests/rankingBrowserSmoke.mjs; dọn theo tag.
# Cần API ở $BaseUrl và Vite https://localhost:5173. Chạy từ thư mục gốc DACN_Project.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
if (-not (Test-Path -LiteralPath '../DACN_FE/tests/rankingBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t10feseed$tag@example.invalid"
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
$mail = $null; $teacher = [Guid]::NewGuid(); $students = 1..5 | ForEach-Object { [Guid]::NewGuid() }
try {
    $r = Auth 'register' @{ fullName = 'T10 FE seed'; userName = "t10feseed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    if ($r.status -ne 202) { throw "register $($r.status)" }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $seed = Sql "SELECT Id FROM dbo.Users WHERE Email=N'$email';"
    $clones = "('$teacher',3,N'teacher')" + (($students | ForEach-Object -Begin { $n = 0 } -Process { $n++; ",('$_',4,N's$n')" }) -join '')
    Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT c.Id,CONCAT(N'T10 FE ',c.N),CONCAT(N't10fe',c.N,N'$tag'),CONCAT(N't10fe',c.N,N'$tag@example.invalid'),u.PasswordHash,c.L,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1
FROM (VALUES $clones) c(Id,L,N) CROSS JOIN dbo.Users u WHERE u.Id='$seed';
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T10 FE exam $tag',@subject,2,0,1,10,5,60,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@e,N'QA',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
DECLARE @v int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'T10 FE $tag',N'T10FE-$tag',20,'$teacher',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) SELECT @class,Id,1 FROM dbo.Users WHERE Id='$seed' OR (UserName LIKE N't10fes%$tag' AND LevelId=4);
DECLARE @past datetime2=DATEADD(hour,-3,SYSUTCDATETIME());
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES
(@e,N'T10 FE ended',@past,DATEADD(hour,-1,SYSUTCDATETIME()),1,1),(@e,N'T10 FE open',@past,DATEADD(hour,2,SYSUTCDATETIME()),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@e;
DECLARE @s int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@e AND Title=N'T10 FE ended');
DECLARE @snap nvarchar(max)=REPLACE(N'{~version~:2,~questions~:[{~point~:4},{~point~:6}]}',N'~',NCHAR(34));
DECLARE @rows TABLE(Id uniqueidentifier, U uniqueidentifier, Mark decimal(18,2));
INSERT @rows SELECT NEWID(),U,Mark FROM (VALUES ('$seed',10)$((0..4 | ForEach-Object { ",('$($students[$_])',$(9 - $_))" }) -join '')) r(U,Mark);
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
SELECT Id,U,@s,@e,@v,@past,DATEADD(minute,30,@past),DATEADD(minute,20,@past),N'submitted',0,@snap,N'[]',5,1 FROM @rows;
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson)
SELECT Id,DATEADD(minute,20,@past),60,Mark,CASE WHEN Mark>=5 THEN 1 ELSE 0 END,N'[]' FROM @rows;
COMMIT;
"@ | Out-Null
    $marks = Sql "SELECT STRING_AGG(CAST(s.TotalMark AS int),',') WITHIN GROUP (ORDER BY s.TotalMark DESC) FROM dbo.ExamAttemptSubmissions s JOIN dbo.ExamAttempts a ON a.Id=s.ExamAttemptId JOIN dbo.Exams e ON e.Id=a.ExamId WHERE e.Name=N'T10 FE exam $tag';"
    if ($marks -ne '10,9,8,7,6,5') { throw "Fixture lệch: $marks" }
    $env:DACN_QA_TEACHER_EMAIL = "t10feteacher$tag@example.invalid"; $env:DACN_QA_STUDENT_EMAIL = $email
    $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag; $env:DACN_QA_DATABASE = $Database
    Push-Location ../DACN_FE
    try { & node tests/rankingBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'T10 ranking browser smoke failed.' } }
    finally { Pop-Location; Remove-Item Env:DACN_QA_TEACHER_EMAIL, Env:DACN_QA_STUDENT_EMAIL, Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_DATABASE -ErrorAction SilentlyContinue }
    Write-Output "TOBE_T10_RANKING_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $exams = "SELECT Id FROM dbo.Exams WHERE Name=N'T10 FE exam $tag'"
    $users = "SELECT Id FROM dbo.Users WHERE Email=N'$email' OR UserName LIKE N't10fe%$tag'"
    Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.ExamAttempts WHERE ExamId IN ($exams);
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId IN ($exams));
      DELETE dbo.ExamSchedules WHERE ExamId IN ($exams); DELETE dbo.ExamDetails WHERE ExamId IN ($exams); DELETE dbo.Exams WHERE Id IN ($exams);"
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T10FE-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T10FE-$tag';"
    Clean "DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);
      DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
