param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T14 — runner smoke trình duyệt media câu hỏi: tạo người ra đề (quyền 2) + môn + 1 câu hỏi tự do; tạo PNG thật 1×1 (đuôi
# ngẫu nhiên sau IEND để SHA khác mỗi lần) và file HTML giả đuôi .png; chạy ../DACN_FE/tests/questionMediaBrowserSmoke.mjs; dọn
# DB + file media trên đĩa (Media:RootPath mặc định ../dacn-media-dev). Cần API ở $BaseUrl và Vite https://localhost:5173.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
if (-not (Test-Path -LiteralPath '../DACN_FE/tests/questionMediaBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
$mediaRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location) 'dacn-media-dev'))
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "t14feseed$tag@example.invalid"; $examinerEmail = "t14feexaminer$tag@example.invalid"
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
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t14fe-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
$png = Join-Path $work 'anh-1x1.png'; $fake = Join-Path $work 'gia-mao.png'
[IO.File]::WriteAllBytes($png, [byte[]]([Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==') +
    [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)))
[IO.File]::WriteAllText($fake, '<html><script>alert(1)</script></html>')
$mail = $null; $examiner = [Guid]::NewGuid(); $subjectName = "T14 FE môn $tag"
try {
    $r = Auth 'register' @{ fullName = 'T14 FE seed'; userName = "t14feseed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    if ($r.status -ne 202) { throw "register $($r.status)" }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $seed = Sql "SELECT Id FROM dbo.Users WHERE Email=N'$email';"
    Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$examiner',N'T14 FE examiner',N't14feexaminer$tag',N'$examinerEmail',PasswordHash,2,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';
INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);
INSERT dbo.Subjects(Name,Status) VALUES (N'$subjectName',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T14 FE câu $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @q int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
COMMIT;
"@ | Out-Null
    $env:DACN_QA_EXAMINER_EMAIL = $examinerEmail; $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag; $env:DACN_QA_DATABASE = $Database
    $env:DACN_QA_PNG = $png; $env:DACN_QA_FAKE_PNG = $fake
    Push-Location ../DACN_FE
    try { & node tests/questionMediaBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'T14 media browser smoke failed.' } }
    finally { Pop-Location; Remove-Item Env:DACN_QA_EXAMINER_EMAIL, Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_DATABASE, Env:DACN_QA_PNG, Env:DACN_QA_FAKE_PNG -ErrorAction SilentlyContinue }
    $count = Sql "SELECT COUNT(*) FROM dbo.QuestionMedia qm JOIN dbo.Questions q ON q.Id=qm.QuestionId JOIN dbo.Subjects s ON s.Id=q.SubjectId WHERE s.Name=N'$subjectName';"
    if ($count -ne '1') { throw "Hậu kiểm QuestionMedia lệch: $count" }
    Write-Output "TOBE_T14_MEDIA_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $subjects = "SELECT Id FROM dbo.Subjects WHERE Name=N'$subjectName'"
    $users = "SELECT Id FROM dbo.Users WHERE Email IN (N'$email',N'$examinerEmail')"
    $ids = try { Sql "SELECT STRING_AGG(CONVERT(nvarchar(36),Id),',') FROM dbo.MediaFiles WHERE UploadedBy='$examiner';" } catch { '' }
    Clean "DELETE dbo.QuestionMedia WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE SubjectId IN ($subjects)) OR MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner');
      DELETE dbo.MediaFiles WHERE UploadedBy='$examiner';"
    foreach ($id in ("$ids" -split ',' | Where-Object { $_ })) {
        $n = $id.Replace('-', '').ToLowerInvariant(); Remove-Item -LiteralPath (Join-Path (Join-Path $mediaRoot $n.Substring(0, 2)) $n) -ErrorAction SilentlyContinue
    }
    Clean "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects); DELETE dbo.Questions WHERE SubjectId IN ($subjects);
      DELETE dbo.Subjects WHERE Id IN ($subjects);"
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users);"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
