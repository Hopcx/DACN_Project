param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T11 — runner smoke trình duyệt trang Phân tích: tạo học viên + môn (C1, C2 ẩn, 1 tag) có lớp đã duyệt và 2 câu
# trong C1 (để Luyện tập có chương), chèn 8 dòng QuestionOutcomes luyện tập (C1 5/6, C2 0/2, tag 2/5, tổng 5/8);
# chạy ../DACN_FE/tests/analysisBrowserSmoke.mjs; dọn theo tag. Cần API ở $BaseUrl và Vite https://localhost:5173.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
if (-not (Test-Path -LiteralPath '../DACN_FE/tests/analysisBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t11fe$tag@example.invalid"
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
$mail = $null; $subjectName = "T11 FE môn $tag"
try {
    $r = Auth 'register' @{ fullName = 'T11 FE Học viên'; userName = "t11fe$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    if ($r.status -ne 202) { throw "register $($r.status)" }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $student = Sql "SELECT Id FROM dbo.Users WHERE Email=N'$email';"
    Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Subjects(Name,Status) VALUES (N'$subjectName',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,N'T11 FE C1 $tag',1,1,SYSUTCDATETIME()); DECLARE @c1 int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,N'T11 FE C2 $tag',2,0,SYSUTCDATETIME()); DECLARE @c2 int=SCOPE_IDENTITY();
INSERT dbo.Tags(Name,NormalizedName,Status,CreatedAt) VALUES (N'T11 FE tag $tag',N't11 fe tag $tag',1,SYSUTCDATETIME()); DECLARE @t int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int;
WHILE @i <= 2 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,ChapterId) VALUES (CONCAT(N'T11 FE $tag n',@i),SYSUTCDATETIME(),1,@s,2,@c1);
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  INSERT dbo.QuestionTags(QuestionId,TagId) VALUES (@q,@t);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T11 FE $tag',N'T11FE-$tag',5,'$student',@s,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (SCOPE_IDENTITY(),'$student',1);
DECLARE @src uniqueidentifier=NEWID(), @now datetime2=DATEADD(day,-1,SYSUTCDATETIME()), @tg nvarchar(50)=CONCAT('[',@t,']');
INSERT dbo.QuestionOutcomes(SourceKind,SourceId,SnapshotQuestionId,UserId,SubjectId,ExamScheduleId,SourceQuestionId,ChapterId,TagIdsJson,IsCorrect,IsAnswered,SelectedAnswerIdsJson,FinishedAtUtc,ClassificationSource)
SELECT N'practice',@src,NEWID(),'$student',@s,NULL,0,CASE WHEN n<=6 THEN @c1 ELSE @c2 END,
  CASE WHEN n IN (1,2,6,7,8) THEN @tg ELSE N'[]' END,CASE WHEN n<=5 THEN 1 ELSE 0 END,1,N'[]',@now,N'snapshot'
FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) v(n);
COMMIT;
"@ | Out-Null
    $env:DACN_QA_STUDENT_EMAIL = $email; $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag; $env:DACN_QA_DATABASE = $Database
    Push-Location ../DACN_FE
    try { & node tests/analysisBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'T11 analysis browser smoke failed.' } }
    finally { Pop-Location; Remove-Item Env:DACN_QA_STUDENT_EMAIL, Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_DATABASE -ErrorAction SilentlyContinue }
    Write-Output "TOBE_T11_ANALYSIS_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "SELECT Id FROM dbo.Users WHERE Email=N'$email'"
    $subjects = "SELECT Id FROM dbo.Subjects WHERE Name=N'$subjectName'"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    Clean "DELETE qt FROM dbo.QuestionTags qt JOIN dbo.Questions q ON q.Id=qt.QuestionId WHERE q.SubjectId IN ($subjects);
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects); DELETE dbo.Questions WHERE SubjectId IN ($subjects);"
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T11FE-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T11FE-$tag';
      DELETE dbo.Tags WHERE Name=N'T11 FE tag $tag'; DELETE dbo.Chapters WHERE SubjectId IN ($subjects); DELETE dbo.Subjects WHERE Id IN ($subjects);"
    Clean "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
