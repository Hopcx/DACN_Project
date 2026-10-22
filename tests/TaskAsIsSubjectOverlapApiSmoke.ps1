param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255')
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51181, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL command failed: $($result -join ' ')" }
    return ($result | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Clone preflight failed.' }
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "asoverlap$tag@example.invalid"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$userId = [Guid]::Empty; $teacherId = [Guid]::NewGuid()
$classA = 0; $classB = 0; $scheduleA = 0; $scheduleB = 0; $scheduleTouch = 0
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
function Request([string]$method, [string]$route, [string]$token, $body) {
    $result = Invoke-WebRequest "$BaseUrl$route" -Method $method -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress -Depth 8) -SkipCertificateCheck -SkipHttpErrorCheck
    return @{ status = [int]$result.StatusCode; data = if ($result.Content) { $result.Content | ConvertFrom-Json } else { $null } }
}
function Expect([int]$actual, [int]$expected, [string]$case) {
    if ($actual -ne $expected) { throw "$case expected HTTP $expected, got $actual" }
}
try {
    Expect (Auth 'register' @{ fullName = 'Overlap QA'; userName = "asoverlap$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    if (-not $mail) { throw 'Verification mail missing.' }
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if (-not $match.Success) { throw 'Verification token missing.' }
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $studentLogin = Auth 'login' @{ keyword = $email; password = $password }
    Expect $studentLogin.status 200 'student login'
    $userId = [Guid]$studentLogin.data.data.userId
    $studentToken = [string]$studentLogin.data.data.accessToken
    Sql "UPDATE dbo.Users SET LevelId=2 WHERE Id='$userId'; INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$userId',4);" | Out-Null
    $examinerLogin = Auth 'login' @{ keyword = $email; password = $password }
    Expect $examinerLogin.status 200 'examiner login'
    $token = [string]$examinerLogin.data.data.accessToken
    $ids = Sql "
INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$teacherId',N'Overlap QA Teacher',N'asoverlapteacher$tag',N'asoverlapteacher$tag@example.invalid',PasswordHash,3,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$userId';
DECLARE @exam int=(SELECT TOP 1 Id FROM dbo.Exams WHERE Status<>255 ORDER BY Id);
DECLARE @subject int=(SELECT SubjectId FROM dbo.Exams WHERE Id=@exam);
INSERT dbo.Classes(Name,ClassCode,Description,Capacity,TeacherId,SubjectId,Status) VALUES (N'Overlap QA A',N'asoverlap$tag-a',N'QA',2,'$teacherId',@subject,1);
DECLARE @a int=CONVERT(int,SCOPE_IDENTITY());
INSERT dbo.Classes(Name,ClassCode,Description,Capacity,TeacherId,SubjectId,Status) VALUES (N'Overlap QA B',N'asoverlap$tag-b',N'QA',2,'$teacherId',@subject,1);
DECLARE @b int=CONVERT(int,SCOPE_IDENTITY());
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,SubjectId,IsTimeUtc)
VALUES (@exam,N'Overlap QA $tag A','2027-04-01T08:00:00','2027-04-01T09:00:00',1,@subject,1);
DECLARE @sa int=CONVERT(int,SCOPE_IDENTITY());
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,SubjectId,IsTimeUtc)
VALUES (@exam,N'Overlap QA $tag B','2027-04-01T08:30:00','2027-04-01T09:30:00',1,@subject,1);
DECLARE @sb int=CONVERT(int,SCOPE_IDENTITY());
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,SubjectId,IsTimeUtc)
VALUES (@exam,N'Overlap QA $tag Touch','2027-04-01T09:00:00','2027-04-01T10:00:00',1,@subject,1);
SELECT CONCAT(@a,',',@b,',',@sa,',',@sb,',',CONVERT(int,SCOPE_IDENTITY()),',',@exam,',',@subject);"
    $classA,$classB,$scheduleA,$scheduleB,$scheduleTouch,$examId,$subjectId = $ids.Split(',') | ForEach-Object { [int]$_ }
    if (-not $classA -or -not $scheduleTouch) { throw 'Schedule fixture missing.' }
    Expect (Request 'POST' '/web/class-exam-schedules' $studentToken @{ classId=$classA; examScheduleId=$scheduleA }).status 403 'Student assign'
    Expect (Request 'POST' '/web/class-exam-schedules' $token @{ classId=$classA; examScheduleId=$scheduleA }).status 201 'first assign'
    $same = Request 'POST' '/web/class-exam-schedules' $token @{ classId=$classA; examScheduleId=$scheduleB }
    Expect $same.status 409 'same class same subject overlap'
    if ($same.data.message -notmatch 'cùng môn') { throw '409 did not identify same-subject conflict.' }
    Expect (Request 'POST' '/web/class-exam-schedules' $token @{ classId=$classB; examScheduleId=$scheduleB }).status 201 'other class overlap'
    Expect (Request 'POST' '/web/class-exam-schedules' $token @{ classId=$classA; examScheduleId=$scheduleTouch }).status 201 'touching boundary'
    $update = @{ examId=$examId; subjectId=$subjectId; title="Overlap QA $tag Touch"; startTime='2027-04-01T08:59:00Z'; endTime='2027-04-01T10:00:00Z'; status=1; roomId=$null; description='QA' }
    Expect (Request 'PUT' "/web/exam-schedules/$scheduleTouch" $token $update).status 409 'update into overlap'
    Write-Output "SUBJECT_OVERLAP_API_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    Sql "DELETE FROM dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE Title LIKE N'Overlap QA $tag%'); DELETE FROM dbo.ExamSchedules WHERE Title LIKE N'Overlap QA $tag%'; DELETE FROM dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode LIKE N'asoverlap$tag-%'); DELETE FROM dbo.Classes WHERE ClassCode LIKE N'asoverlap$tag-%';" | Out-Null
    if ($userId -eq [Guid]::Empty) {
        $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$email';"
        if ($found) { $userId = [Guid]$found }
    }
    if ($userId -ne [Guid]::Empty) {
        Sql "DELETE FROM dbo.UserPermissions WHERE UserId IN ('$userId','$teacherId'); DELETE FROM dbo.RefreshTokens WHERE UserId IN ('$userId','$teacherId'); DELETE FROM dbo.EmailVerificationTokens WHERE UserId IN ('$userId','$teacherId'); DELETE FROM dbo.Users WHERE Id IN ('$userId','$teacherId');" | Out-Null
    }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
