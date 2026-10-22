param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255', [switch]$Browser)
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51161, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL command failed: $($result -join ' ')" }
    return ($result | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Clone preflight failed.' }
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "asvisibility$tag@example.invalid"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$userId = [Guid]::Empty
$attemptId = [Guid]::NewGuid()
$memberId = 0
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
$authHeaders = @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf }
function Auth([string]$route, $body) {
    for ($i = 0; $i -lt 5; $i++) {
        $result = Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck -Headers $authHeaders -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress -Depth 8)
        if ([int]$result.StatusCode -ne 429) {
            return @{ status = [int]$result.StatusCode; data = ($result.Content | ConvertFrom-Json) }
        }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
function Request([string]$method, [string]$route, [string]$token = '', $body = $null) {
    $headers = if ($token) { @{ Authorization = "Bearer $token" } } else { @{} }
    $args = @{ Uri = "$BaseUrl$route"; Method = $method; Headers = $headers; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $args.ContentType = 'application/json'; $args.Body = ($body | ConvertTo-Json -Compress -Depth 8) }
    $result = Invoke-WebRequest @args
    return @{ status = [int]$result.StatusCode; data = if ($result.Content) { $result.Content | ConvertFrom-Json } else { $null } }
}
function Expect([int]$actual, [int]$expected, [string]$case) {
    if ($actual -ne $expected) { throw "$case expected HTTP $expected, got $actual" }
}
try {
    Expect (Auth 'register' @{ fullName = 'Visibility QA'; userName = "asvisibility$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    if (-not $mail) { throw 'Verification mail missing.' }
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if (-not $match.Success) { throw 'Verification token missing.' }
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    Expect $login.status 200 'login'
    $userId = [Guid]$login.data.data.userId
    $token = [string]$login.data.data.accessToken
    $memberId = [int](Sql "
DECLARE @class int, @schedule int, @exam int, @detail int;
SELECT TOP 1 @class=link.ClassId,@schedule=s.Id,@exam=s.ExamId,@detail=d.Id
FROM dbo.ClassExamSchedule link JOIN dbo.Classes c ON c.Id=link.ClassId
JOIN dbo.ExamSchedules s ON s.Id=link.ExamScheduleId
JOIN dbo.ExamDetails d ON d.ExamId=s.ExamId
WHERE c.Status=1 AND s.IsTimeUtc=1 ORDER BY s.Id;
IF @class IS NULL THROW 51162, 'No class/schedule/exam fixture', 1;
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,'$userId',1);
DECLARE @member int=CONVERT(int,SCOPE_IDENTITY());
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
VALUES ('$attemptId','$userId',@schedule,@exam,@detail,SYSUTCDATETIME(),DATEADD(minute,20,SYSUTCDATETIME()),N'inProgress',0,
REPLACE(CONCAT(N'{~version~:1,~examId~:',@exam,N',~examDetailId~:',@detail,N',~examName~:~QA~,~variantCode~:~QA~,~questions~:[]}'),N'~',CHAR(34)),N'[]',0,0);
SELECT @member;")
    if (-not $memberId) { throw 'Membership fixture missing.' }
    Expect (Request 'GET' "/web/attempts/$attemptId/visibility-count").status 401 'anonymous count'
    Expect (Request 'GET' "/web/attempts/$([Guid]::NewGuid())/visibility-count" $token).status 404 'other attempt'
    $count = Request 'GET' "/web/attempts/$attemptId/visibility-count" $token
    Expect $count.status 200 'initial count'
    if ($count.data.data.count -ne 0) { throw 'Initial count not zero.' }
    $eventId = [Guid]::NewGuid()
    $first = Request 'POST' "/web/attempts/$attemptId/visibility-hidden" $token @{ eventId = $eventId }
    Expect $first.status 200 'first hidden'
    if ($first.data.data.count -ne 1) { throw 'First count not one.' }
    $retry = Request 'POST' "/web/attempts/$attemptId/visibility-hidden" $token @{ eventId = $eventId }
    Expect $retry.status 200 'retry'
    if ($retry.data.data.count -ne 1 -or !$retry.data.data.duplicate) { throw 'Retry was not idempotent.' }
    Start-Sleep -Milliseconds 1200
    $second = Request 'POST' "/web/attempts/$attemptId/visibility-hidden" $token @{ eventId = [Guid]::NewGuid() }
    Expect $second.status 200 'second hidden'
    if ($second.data.data.count -ne 2) { throw 'Second count not two.' }
    if ((Sql "SELECT COUNT(*) FROM dbo.AttemptVisibilityEvents WHERE AttemptId='$attemptId' AND IsCounted=1;") -ne '2') { throw 'SQL count mismatch.' }
    if ($Browser) {
        $env:DACN_QA_EMAIL = $email
        $env:DACN_QA_PASSWORD = $password
        $env:DACN_QA_ATTEMPT_ID = [string]$attemptId
        try {
            Push-Location ../DACN_FE
            try { & node tests/visibilityBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'Visibility browser smoke failed.' } }
            finally { Pop-Location }
        } finally {
            Remove-Item Env:DACN_QA_EMAIL,Env:DACN_QA_PASSWORD,Env:DACN_QA_ATTEMPT_ID -ErrorAction SilentlyContinue
        }
        if ((Sql "SELECT COUNT(*) FROM dbo.AttemptVisibilityEvents WHERE AttemptId='$attemptId' AND IsCounted=1;") -ne '3')
            { throw 'Browser visibility count not persisted.' }
    }
    Sql "UPDATE dbo.ExamAttempts SET State=N'submitted' WHERE Id='$attemptId';" | Out-Null
    Expect (Request 'POST' "/web/attempts/$attemptId/visibility-hidden" $token @{ eventId = [Guid]::NewGuid() }).status 409 'closed attempt'
    Write-Output "VISIBILITY_API_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    Sql "DELETE FROM dbo.AttemptVisibilityEvents WHERE AttemptId='$attemptId'; DELETE FROM dbo.ExamAttempts WHERE Id='$attemptId';" | Out-Null
    if ($memberId) { Sql "DELETE FROM dbo.ClassUsers WHERE Id=$memberId;" | Out-Null }
    if ($userId -eq [Guid]::Empty) {
        $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$email';"
        if ($found) { $userId = [Guid]$found }
    }
    if ($userId -ne [Guid]::Empty) {
        Sql "DELETE FROM dbo.RefreshTokens WHERE UserId='$userId'; DELETE FROM dbo.EmailVerificationTokens WHERE UserId='$userId'; DELETE FROM dbo.Users WHERE Id='$userId';" | Out-Null
    }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
