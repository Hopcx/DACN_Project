param(
    [string]$Database = 'ProjectDACN_Task04Task09_Test',
    [string]$BaseUrl = 'https://localhost:7255'
)

$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'This test only writes to the named database copy.' }
$connected = (sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q 'SET NOCOUNT ON; SELECT DB_NAME();' | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $connected -ne $Database) { throw 'Database copy connection check failed.' }
Write-Output "TARGET_DB=$connected"

function SqlRead([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; $query"
    if ($LASTEXITCODE -ne 0) { throw 'SQL read failed.' }
    return ($result | Out-String).Trim()
}
function SqlWrite([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51101, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL fixture write failed: $($result -join ' ')" }
    return ($result | Out-String).Trim()
}

$webSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod -Uri "$BaseUrl/web/auth/csrf" -Method Get -WebSession $webSession -SkipCertificateCheck).data.csrfToken
if (-not $csrf) { throw 'CSRF token missing.' }
$authHeaders = @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf }

function AuthPost([string]$route, $body, [string]$accessToken = '') {
    $headers = $authHeaders.Clone()
    if ($accessToken) { $headers['Authorization'] = "Bearer $accessToken" }
    for ($retry = 0; $retry -le 4; $retry++) {
        $response = Invoke-WebRequest -Uri "$BaseUrl/web/auth/$route" -Method Post -WebSession $webSession `
            -SkipCertificateCheck -SkipHttpErrorCheck -ContentType 'application/json' `
            -Headers $headers -Body (ConvertTo-Json $body -Depth 8 -Compress)
        if ([int]$response.StatusCode -ne 429 -or $retry -eq 4) { break }
        Write-Output 'AUTH_RATE_LIMIT_RETRY=15s' | Out-Host
        Start-Sleep -Seconds 15
    }
    return @{ Status = [int]$response.StatusCode; Payload = ($response.Content | ConvertFrom-Json) }
}

$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$userName = "qa$tag"
$email = "$userName@example.invalid"
$password = 'Qa7' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$registration = AuthPost 'register' @{
    fullName = 'Task04 Test Student'; userName = $userName; email = $email; password = $password
    address = 'Test only'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true
}
if ($registration.Status -ne 202) { throw "Register failed: HTTP $($registration.Status)" }

$mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' |
    Sort-Object LastWriteTimeUtc -Descending |
    Where-Object { (Get-Content -LiteralPath $_.FullName -Raw).Contains("To: $email") } |
    Select-Object -First 1
if (-not $mail) { throw 'Development verification mail was not written.' }
$mailContent = Get-Content -LiteralPath $mail.FullName -Raw
$tokenMatch = [regex]::Match($mailContent, '#token=([^\s]+)')
if (-not $tokenMatch.Success) { throw 'Verification token missing from maildrop.' }
$verificationToken = [Uri]::UnescapeDataString($tokenMatch.Groups[1].Value)
$verification = AuthPost 'verify-email' @{ token = $verificationToken }
if ($verification.Status -ne 200) { throw "Verify failed: HTTP $($verification.Status)" }

$login = AuthPost 'login' @{ keyword = $email; password = $password }
if ($login.Status -ne 200 -or -not $login.Payload.data.accessToken) { throw "Login failed: HTTP $($login.Status)" }
$accessToken = [string]$login.Payload.data.accessToken
$userId = [Guid]::Parse([string]$login.Payload.data.userId)
$me = Invoke-WebRequest -Uri "$BaseUrl/web/auth/me" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck `
    -Headers @{ Authorization = "Bearer $accessToken" }
if ([int]$me.StatusCode -ne 200) { throw 'Authenticated /me failed.' }
$withoutPermission = Invoke-WebRequest -Uri "$BaseUrl/web/exam-schedules" -Method Get `
    -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = "Bearer $accessToken" }
if ([int]$withoutPermission.StatusCode -ne 403) { throw 'Student without schedule permission was not rejected.' }
$oldUserUnverified = SqlRead 'SELECT COUNT(*) FROM dbo.Users WHERE EmailVerifiedAt IS NULL AND Id IN (SELECT Id FROM ProjectDACN.dbo.Users);'
$newUserVerified = SqlRead "SELECT COUNT(*) FROM dbo.Users WHERE Id = '$userId' AND EmailVerifiedAt IS NOT NULL;"
$originHasNewUser = (sqlcmd -S . -d ProjectDACN -E -No -C -b -W -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Users WHERE Id = '$userId';" | Out-String).Trim()
if ($oldUserUnverified -ne '1' -or $newUserVerified -ne '1' -or $originHasNewUser -ne '0') {
    throw 'Auth row reconciliation or source isolation failed.'
}
Write-Output 'AUTH_REGISTER_VERIFY_LOGIN_ME=PASS;SCHEDULE_PERMISSION_403=PASS;OLD_USER_UNVERIFIED=1;ORIGIN_NEW_USER=0'

# Promote only the newly registered QA actor in the disposable copy. The two
# synthetic students remain level 4; no existing account is modified.
SqlWrite "UPDATE dbo.Users SET LevelId=1 WHERE Id='$userId' AND UserName='$userName';" | Out-Null
$adminLogin = AuthPost 'login' @{ keyword = $email; password = $password }
if ($adminLogin.Status -ne 200) { throw 'QA admin login failed.' }
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.ServerCertificateCustomValidationCallback = [System.Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
$http = [System.Net.Http.HttpClient]::new($handler)
$http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', [string]$adminLogin.Payload.data.accessToken)
$studentA = [Guid]::NewGuid(); $studentB = [Guid]::NewGuid()
SqlWrite @"
INSERT INTO dbo.Users (Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$studentA',N'QA membership A',N'ma$tag',N'ma$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'Test only',1
FROM dbo.Users WHERE Id='$userId';
INSERT INTO dbo.Users (Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$studentB',N'QA membership B',N'mb$tag',N'mb$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'Test only',1
FROM dbo.Users WHERE Id='$userId';
"@ | Out-Null

function Approve([int]$membershipId, [int]$classId, [Guid]$studentId) {
    $json = ConvertTo-Json @{ classId=$classId; userId=$studentId; status=1 } -Compress
    return $http.PutAsync("$BaseUrl/web/class-users/$membershipId", [System.Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json'))
}

# Two cross-class approval races and a separate last-seat race.
foreach ($scenario in @('overlap','touching','capacity')) {
    $secondStart = if ($scenario -eq 'touching') { '2027-03-01T09:00:00' } else { '2027-03-01T08:30:00' }
    $fixture = SqlWrite @"
DECLARE @subject int=(SELECT TOP (1) Id FROM dbo.Subjects ORDER BY Id);
INSERT INTO dbo.Exams (Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration)
VALUES (N'QA membership $tag $scenario',@subject,1,1,2,10,5,30);
DECLARE @exam int=CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.Classes (Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES (N'QA membership A $tag $scenario',N'MA-$tag-$scenario',1,'$userId',@subject,1);
DECLARE @classA int=CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.Classes (Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES (N'QA membership B $tag $scenario',N'MB-$tag-$scenario',1,'$userId',@subject,1);
DECLARE @classB int=CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.ExamSchedules (ExamId,Title,StartTime,EndTime,Status,IsTimeUtc)
VALUES (@exam,N'QA membership $tag A','2027-03-01T08:00:00','2027-03-01T09:00:00',1,1);
DECLARE @scheduleA int=CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.ExamSchedules (ExamId,Title,StartTime,EndTime,Status,IsTimeUtc)
VALUES (@exam,N'QA membership $tag B','$secondStart','2027-03-01T10:00:00',1,1);
DECLARE @scheduleB int=CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.ClassExamSchedule (ClassId,ExamScheduleId) VALUES (@classA,@scheduleA),(@classB,@scheduleB);
INSERT INTO dbo.ClassUsers (ClassId,UserId,Status) VALUES (@classA,'$studentA',2);
DECLARE @memberA int=CONVERT(int,SCOPE_IDENTITY());
DECLARE @secondClass int=CASE WHEN '$scenario'='capacity' THEN @classA ELSE @classB END;
DECLARE @secondStudent uniqueidentifier=CASE WHEN '$scenario'='capacity' THEN '$studentB' ELSE '$studentA' END;
INSERT INTO dbo.ClassUsers (ClassId,UserId,Status) VALUES (@secondClass,@secondStudent,2);
DECLARE @memberB int=CONVERT(int,SCOPE_IDENTITY());
SELECT CONCAT(@exam,',',@classA,',',@classB,',',@scheduleA,',',@scheduleB,',',@memberA,',',@memberB,',',@secondClass);
"@
    $ids=$fixture.Split(',') | ForEach-Object { [int]$_ }
    if ($ids.Count -ne 8) { throw 'Membership fixture IDs missing.' }
    $stateQuery="SELECT CONCAT('membership=',STRING_AGG(CONCAT(Id,':',Status),',')) FROM (SELECT TOP (2) Id,Status FROM dbo.ClassUsers WHERE Id IN ($($ids[5]),$($ids[6])) ORDER BY Id) q; SELECT CONCAT('class=',c.Id,':approved=',(SELECT COUNT(*) FROM dbo.ClassUsers m WHERE m.ClassId=c.Id AND m.Status=1),':capacity=',c.Capacity) FROM dbo.Classes c WHERE c.Id IN ($($ids[1]),$($ids[2])) ORDER BY c.Id;"
    $before=SqlRead $stateQuery
    if ((SqlRead "SELECT COUNT(*) FROM dbo.ClassUsers WHERE Id IN ($($ids[5]),$($ids[6])) AND Status=2;") -ne '2') { throw 'Both memberships must begin pending.' }
    Write-Output "SCENARIO=$scenario;FIXTURE_IDS=$fixture;BEFORE=$before"
    $requestA=Approve $ids[5] $ids[1] $studentA
    $secondStudent=if ($scenario -eq 'capacity') { $studentB } else { $studentA }
    $requestB=Approve $ids[6] $ids[7] $secondStudent
    $responses=@($requestA.GetAwaiter().GetResult(),$requestB.GetAwaiter().GetResult())
    $statuses=($responses | ForEach-Object { [int]$_.StatusCode } | Sort-Object) -join ','
    $expected=if ($scenario -eq 'touching') { '200,200' } else { '200,409' }
    if ($statuses -ne $expected) { throw "$scenario expected $expected, got $statuses" }
    $expectedApproved=if ($scenario -eq 'touching') { '2' } else { '1' }
    if ((SqlRead "SELECT COUNT(*) FROM dbo.ClassUsers WHERE Id IN ($($ids[5]),$($ids[6])) AND Status=1;") -ne $expectedApproved) { throw 'Final approved memberships inconsistent.' }
    if ((SqlRead "SELECT COUNT(*) FROM dbo.Classes c WHERE c.Id IN ($($ids[1]),$($ids[2])) AND (SELECT COUNT(*) FROM dbo.ClassUsers m WHERE m.ClassId=c.Id AND m.Status=1)>c.Capacity;") -ne '0') { throw 'Class capacity exceeded.' }
    for ($i=0; $i -lt 2; $i++) {
        $expectedStatus=if ([int]$responses[$i].StatusCode -eq 200) { 1 } else { 2 }
        if ((SqlRead "SELECT Status FROM dbo.ClassUsers WHERE Id=$($ids[5+$i]);") -ne [string]$expectedStatus) { throw 'Rejected approval changed membership.' }
    }
    Write-Output "SCENARIO=$scenario;HTTP=$statuses;AFTER=$(SqlRead $stateQuery);PASS"
    # Remove only this scenario's fixture by IDs while retaining the QA actors
    # in the disposable copy for audit. No source DB writes.
    SqlWrite "DELETE FROM dbo.ClassUsers WHERE Id IN ($($ids[5]),$($ids[6])); DELETE FROM dbo.ClassExamSchedule WHERE ExamScheduleId IN ($($ids[3]),$($ids[4])); DELETE FROM dbo.ExamSchedules WHERE Id IN ($($ids[3]),$($ids[4])); DELETE FROM dbo.Classes WHERE Id IN ($($ids[1]),$($ids[2])); DELETE FROM dbo.Exams WHERE Id=$($ids[0]);" | Out-Null
    if ((SqlRead "SELECT COUNT(*) FROM dbo.ExamSchedules WHERE Id IN ($($ids[3]),$($ids[4]));") -ne '0') { throw 'Fixture cleanup failed.' }
    Write-Output "SCENARIO=$scenario;FIXTURE_CLEANUP=PASS"
}
$http.Dispose(); $handler.Dispose()
Write-Output "MEMBERSHIP_CONCURRENCY=PASS;QA_TAG=$tag"
