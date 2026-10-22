param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255', [switch]$Browser)
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51151, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL command failed: $($result -join ' ')" }
    return ($result | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Clone preflight failed.' }
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$studentEmail = "asstudent$tag@example.invalid"
$teacherEmail = "asteacher$tag@example.invalid"
$studentName = "asstudent$tag"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$teacherId = [Guid]::NewGuid()
$otherTeacherId = [Guid]::NewGuid()
$studentId = [Guid]::Empty
$classA = 0; $classB = 0
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
$authHeaders = @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf }
function Auth([string]$route, $body) {
    for ($i = 0; $i -lt 5; $i++) {
        $result = Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck -Headers $authHeaders -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress -Depth 8)
        if ([int]$result.StatusCode -ne 429) { return @{ status = [int]$result.StatusCode; data = ($result.Content | ConvertFrom-Json) } }
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
    $register = Auth 'register' @{ fullName = 'As-is QA Student'; userName = $studentName; email = $studentEmail; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }
    Expect $register.status 202 'register Student'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $studentEmail") } | Select-Object -First 1
    if (-not $mail) { throw 'Verification mail not found.' }
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if (-not $match.Success) { throw 'Verification token not found.' }
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify Student'
    $studentLogin = Auth 'login' @{ keyword = $studentEmail; password = $password }
    Expect $studentLogin.status 200 'login Student'
    $studentId = [Guid]$studentLogin.data.data.userId
    $studentToken = [string]$studentLogin.data.data.accessToken

    $ids = Sql "
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$teacherId',N'As-is QA Teacher',N'asteacher$tag',N'$teacherEmail',PasswordHash,3,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$studentId';
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$otherTeacherId',N'Other QA Teacher',N'asother$tag',N'asother$tag@example.invalid',PasswordHash,3,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$studentId';
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects WHERE Status=1 ORDER BY Id);
INSERT dbo.Classes(Name,ClassCode,Description,Capacity,TeacherId,SubjectId,Status) VALUES
(N'As-is QA $tag A',N'as$tag-a',N'QA',2,'$teacherId',@subject,1);
DECLARE @a int=CONVERT(int,SCOPE_IDENTITY());
INSERT dbo.Classes(Name,ClassCode,Description,Capacity,TeacherId,SubjectId,Status) VALUES
(N'As-is QA $tag B',N'as$tag-b',N'QA',2,'$otherTeacherId',@subject,1);
SELECT CONCAT(@a,',',CONVERT(int,SCOPE_IDENTITY()));"
    $classA, $classB = $ids.Split(',') | ForEach-Object { [int]$_ }
    if (-not $classA -or -not $classB) { throw 'Class fixture missing.' }
    $teacherLogin = Auth 'login' @{ keyword = $teacherEmail; password = $password }
    Expect $teacherLogin.status 200 'login Teacher'
    $teacherToken = [string]$teacherLogin.data.data.accessToken
    Expect (Request 'GET' '/web/teacher/classes').status 401 'anonymous Teacher list'
    Expect (Request 'GET' '/web/teacher/classes' $studentToken).status 403 'Student Teacher list'
    $mine = Request 'GET' '/web/teacher/classes' $teacherToken
    Expect $mine.status 200 'Teacher list'
    if (@($mine.data.data).Count -ne 1 -or $mine.data.data[0].id -ne $classA) { throw 'Teacher owner list mismatch.' }
    Expect (Request 'GET' "/web/teacher/classes/$classB/members" $teacherToken).status 404 'other Teacher class'
    Expect (Request 'GET' "/web/teacher/classes/$classA/students/lookup?email=invalid" $teacherToken).status 400 'invalid email'
    $lookup = Request 'GET' "/web/teacher/classes/$classA/students/lookup?email=$studentEmail" $teacherToken
    Expect $lookup.status 200 'exact email lookup'
    if ($lookup.data.data.id -ne [string]$studentId) { throw 'Lookup returned wrong Student.' }
    $added = Request 'POST' "/web/teacher/classes/$classA/members" $teacherToken @{ email = $studentEmail }
    Expect $added.status 201 'Teacher add Student'
    $membershipId = [int]$added.data.data.id
    Expect (Request 'POST' "/web/teacher/classes/$classA/members" $teacherToken @{ email = $studentEmail }).status 409 'duplicate Student'
    Sql "UPDATE dbo.ClassUsers SET Status=2 WHERE Id=$membershipId;" | Out-Null
    Expect (Request 'POST' "/web/teacher/classes/$classA/members/$membershipId/approve" $teacherToken).status 200 'approve Student'
    if ((Sql "SELECT Status FROM dbo.ClassUsers WHERE Id=$membershipId;") -ne '1') { throw 'Approval not persisted.' }
    Expect (Request 'DELETE' "/web/teacher/classes/$classA/members/$membershipId" $teacherToken).status 200 'remove Student'
    if ((Sql "SELECT COUNT(*) FROM dbo.ClassUsers WHERE Id=$membershipId;") -ne '0') { throw 'Membership not removed.' }
    if ($Browser) {
        $env:DACN_QA_EMAIL = $teacherEmail
        $env:DACN_QA_PASSWORD = $password
        $env:DACN_QA_STUDENT_EMAIL = $studentEmail
        $env:DACN_QA_TAG = $tag
        try {
            Push-Location ../DACN_FE
            try { & node tests/teacherClassesBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'Teacher browser smoke failed.' } }
            finally { Pop-Location }
        } finally {
            Remove-Item Env:DACN_QA_EMAIL,Env:DACN_QA_PASSWORD,Env:DACN_QA_STUDENT_EMAIL,Env:DACN_QA_TAG -ErrorAction SilentlyContinue
        }
        if ((Sql "SELECT COUNT(*) FROM dbo.ClassUsers WHERE ClassId=$classA AND UserId='$studentId' AND Status=1;") -ne '1')
            { throw 'Browser add not persisted.' }
    }
    Write-Output "TEACHER_API_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    Sql "DELETE FROM dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode LIKE N'as$tag-%'); DELETE FROM dbo.Classes WHERE ClassCode LIKE N'as$tag-%';" | Out-Null
    if ($studentId -eq [Guid]::Empty) {
        $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$studentEmail';"
        if ($found) { $studentId = [Guid]$found }
    }
    if ($studentId -ne [Guid]::Empty) {
        Sql "DELETE FROM dbo.RefreshTokens WHERE UserId IN ('$studentId','$teacherId','$otherTeacherId'); DELETE FROM dbo.EmailVerificationTokens WHERE UserId IN ('$studentId','$teacherId','$otherTeacherId'); DELETE FROM dbo.Users WHERE Id IN ('$studentId','$teacherId','$otherTeacherId');" | Out-Null
    }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
