param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T7 — runner smoke trình duyệt Luyện tập: tạo học viên + môn có lớp đã duyệt + 2 câu (OK đúng / NO sai, lời giải EXPL-<tag>),
# chạy ../DACN_FE/tests/practiceBrowserSmoke.mjs, hậu kiểm SQL, dọn theo tag. Cần API ở $BaseUrl và Vite https://localhost:5173.
# Chạy từ thư mục gốc DACN_Project.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
if (-not (Test-Path -LiteralPath '../DACN_FE/tests/practiceBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t7qa$tag@example.invalid"
$subjectName = "T7 môn $tag"
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
$mail = $null
try {
    $r = Auth 'register' @{ fullName = 'T7 Học viên'; userName = "t7qa$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    if ($r.status -ne 202) { throw "register $($r.status)" }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $student = Sql "SELECT Id FROM dbo.Users WHERE Email=N'$email';"
    Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Subjects(Name,Status) VALUES (N'$subjectName',1); DECLARE @s int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int;
WHILE @i <= 2 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,Explanation)
  VALUES (CONCAT(N'Câu luyện tập T7 $tag n',@i),SYSUTCDATETIME(),1,@s,2,CONCAT(N'EXPL-$tag n',@i));
  SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  SET @i+=1;
END
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T7 $tag',N'T7-$tag',5,'$student',@s,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (SCOPE_IDENTITY(),'$student',1);
COMMIT;
"@ | Out-Null
    $env:DACN_QA_EMAIL = $email; $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag
    $env:DACN_QA_SUBJECT_NAME = $subjectName; $env:DACN_QA_DATABASE = $Database
    Push-Location ../DACN_FE
    try { & node tests/practiceBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'T7 practice browser smoke failed.' } }
    finally { Pop-Location; Remove-Item Env:DACN_QA_EMAIL, Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_SUBJECT_NAME, Env:DACN_QA_DATABASE -ErrorAction SilentlyContinue }
    # Hậu kiểm: 3 phiên (2 đã nộp 2/2 và 1/2, 1 phiên làm lại đang làm)
    $summary = Sql "SELECT STRING_AGG(CONCAT(State,':',RevealMode,':',ISNULL(CAST(CorrectCount AS varchar(5)),'-')),',') WITHIN GROUP (ORDER BY CreatedAtUtc) FROM dbo.PracticeSessions WHERE UserId='$student';"
    if ($summary -ne 'finished:end:2,finished:perQuestion:1,inProgress:perQuestion:-,finished:end:1') { throw "SQL hậu kiểm lệch: $summary" }
    # To-be T9: phiên thứ 4 là Thi thử, tự nộp khi hết giờ (FinishedAtUtc = DeadlineUtc)
    $mock = Sql "SELECT CONCAT(COUNT(*),':',SUM(CASE WHEN FinishedAtUtc=DeadlineUtc THEN 1 ELSE 0 END)) FROM dbo.PracticeSessions WHERE UserId='$student' AND Kind='mock';"
    if ($mock -ne '1:1') { throw "Thi thử hậu kiểm lệch: $mock" }
    # To-be T8: 2 câu được đánh dấu (1 ở phiên Cuối phiên sau ca bấm nhanh, 1 ở phiên Từng câu)
    $flags = Sql "SELECT COUNT(*) FROM dbo.ReviewFlags WHERE UserId='$student' AND SourceKind='practice';"
    if ($flags -ne '2') { throw "ReviewFlags lệch: $flags" }
    Write-Output "TOBE_T7_PRACTICE_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag;SQL=$summary;FLAGS=$flags"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "SELECT Id FROM dbo.Users WHERE Email=N'$email'"
    $subjects = "SELECT Id FROM dbo.Subjects WHERE Name=N'$subjectName'"
    Clean "DELETE dbo.ReviewFlags WHERE UserId IN ($users);"
    Clean "DELETE dbo.PracticeSessions WHERE UserId IN ($users) OR SubjectId IN ($subjects);"
    Clean "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects); DELETE dbo.Questions WHERE SubjectId IN ($subjects);"
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T7-$tag') OR UserId IN ($users);
      DELETE dbo.Classes WHERE ClassCode=N'T7-$tag'; DELETE dbo.Subjects WHERE Id IN ($subjects);"
    Clean "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
