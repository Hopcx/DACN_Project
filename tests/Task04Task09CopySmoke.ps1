param(
    [string]$Database = 'ProjectDACN_Task04Task09_Test',
    [string]$BaseUrl = 'https://localhost:7255'
)

$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'This test only writes to the named database copy.' }
$connected = (sqlcmd -S . -d $Database -E -No -C -b -W -h -1 -Q 'SET NOCOUNT ON; SELECT DB_NAME();' | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $connected -ne $Database) { throw 'Database copy connection check failed.' }
Write-Output "TARGET_DB=$connected"

function SqlRead([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -W -h -1 -Q "SET NOCOUNT ON; $query"
    if ($LASTEXITCODE -ne 0) { throw 'SQL read failed.' }
    return ($result | Out-String).Trim()
}
function SqlWrite([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51101, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw 'SQL fixture write failed.' }
    return ($result | Out-String).Trim()
}

$webSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod -Uri "$BaseUrl/web/auth/csrf" -Method Get -WebSession $webSession -SkipCertificateCheck).data.csrfToken
if (-not $csrf) { throw 'CSRF token missing.' }
$authHeaders = @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf }

function AuthPost([string]$route, $body, [string]$accessToken = '') {
    $headers = $authHeaders.Clone()
    if ($accessToken) { $headers['Authorization'] = "Bearer $accessToken" }
    $response = Invoke-WebRequest -Uri "$BaseUrl/web/auth/$route" -Method Post -WebSession $webSession `
        -SkipCertificateCheck -SkipHttpErrorCheck -ContentType 'application/json' `
        -Headers $headers -Body (ConvertTo-Json $body -Depth 8 -Compress)
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

if ((SqlRead 'SELECT COUNT(*) FROM dbo.Permissions WHERE Id = 4;') -ne '1') { throw 'Schedule permission lookup missing.' }
SqlWrite "INSERT INTO dbo.UserPermissions (UserId,PermissionId) VALUES ('$userId',4);" | Out-Null
$managementLogin = AuthPost 'login' @{ keyword = $email; password = $password }
if ($managementLogin.Status -ne 200 -or -not ($managementLogin.Payload.data.permissionIds -contains 4)) {
    throw 'Permission 4 did not appear in new JWT.'
}
$accessToken = [string]$managementLogin.Payload.data.accessToken

$fixture = SqlWrite @"
DECLARE @subjectId int = (SELECT TOP (1) Id FROM dbo.Subjects ORDER BY Id);
IF @subjectId IS NULL THROW 51102, 'No subject fixture', 1;
INSERT INTO dbo.Exams (Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration)
VALUES (N'Task09 QA $tag',@subjectId,1,1,2,10,5,30);
DECLARE @examId int = CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.Exams (Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration)
VALUES (N'Task09 QA second $tag',@subjectId,1,1,2,10,5,30);
DECLARE @examId2 int = CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.Rooms (Name,Capacity,Address,Status) VALUES (N'Task09 QA $tag',30,N'Test only',1);
DECLARE @roomId int = CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.Rooms (Name,Capacity,Address,Status) VALUES (N'Task09 QA second $tag',30,N'Test only',1);
DECLARE @roomId2 int = CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.Classes (Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES (N'Task09 A $tag',N'QA-A-$tag',30,'$userId',@subjectId,1);
DECLARE @classA int = CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.Classes (Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES (N'Task09 B $tag',N'QA-B-$tag',30,'$userId',@subjectId,1);
DECLARE @classB int = CONVERT(int,SCOPE_IDENTITY());
INSERT INTO dbo.ClassUsers (ClassId,UserId,Status) VALUES (@classA,'$userId',1),(@classB,'$userId',1);
SELECT CONCAT(@examId,',',@examId2,',',@roomId,',',@roomId2,',',@classA,',',@classB);
"@
$fixtureIds = $fixture.Split(',')
if ($fixtureIds.Count -ne 6) { throw 'Fixture IDs not returned.' }
$examId = [int]$fixtureIds[0]; $examId2 = [int]$fixtureIds[1]
$roomId = [int]$fixtureIds[2]; $roomId2 = [int]$fixtureIds[3]
$classA = [int]$fixtureIds[4]; $classB = [int]$fixtureIds[5]

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.ServerCertificateCustomValidationCallback = [System.Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
$http = [System.Net.Http.HttpClient]::new($handler)
$http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $accessToken)
function PostSchedule($start, $end, $room) {
    $json = ConvertTo-Json @{ examId = $examId; title = 'Task09 QA'; startTime = $start; endTime = $end
        status = 1; roomId = $room } -Compress
    return $http.PostAsync("$BaseUrl/web/exam-schedules", [System.Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json'))
}
function PostAssignment([int]$classId, [int]$scheduleId) {
    $json = ConvertTo-Json @{ classId = $classId; examScheduleId = $scheduleId } -Compress
    return $http.PostAsync("$BaseUrl/web/class-exam-schedules", [System.Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json'))
}
function ResponseData($response) { return ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json -DateKind String).data }
function SendSchedule([string]$method, [int]$scheduleId, [int]$exam, [Nullable[int]]$room,
    [string]$start, [string]$end, [int]$status = 1) {
    $json = ConvertTo-Json @{ examId = $exam; title = 'Task09 acceptance';
        startTime = $start; endTime = $end; status = $status; roomId = $room } -Compress
    $content = [System.Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')
    if ($method -eq 'POST') { return $http.PostAsync("$BaseUrl/web/exam-schedules", $content).GetAwaiter().GetResult() }
    return $http.PutAsync("$BaseUrl/web/exam-schedules/$scheduleId", $content).GetAwaiter().GetResult()
}
function RequireStatus($response, [int]$expected, [string]$label) {
    if ([int]$response.StatusCode -ne $expected) {
        throw "$label expected $expected, got $([int]$response.StatusCode)"
    }
}
$examOptions = $http.GetAsync("$BaseUrl/web/exam-schedules/exam-options").GetAwaiter().GetResult()
$roomOptions = $http.GetAsync("$BaseUrl/web/exam-schedules/room-options").GetAwaiter().GetResult()
$classOptions = $http.GetAsync("$BaseUrl/web/exam-schedules/class-options").GetAwaiter().GetResult()
RequireStatus $examOptions 200 'Exam options'
RequireStatus $roomOptions 200 'Room options'
RequireStatus $classOptions 200 'Class options'
if (-not (@(ResponseData $examOptions).id -contains $examId) -or
    -not (@(ResponseData $roomOptions).id -contains $roomId) -or
    -not (@(ResponseData $classOptions).id -contains $classA)) {
    throw 'Schedule form options did not include fixture values.'
}
Write-Output 'SCHEDULE_FORM_OPTIONS=PASS'

$roomStart = '2027-01-01T08:00:00Z'; $roomEnd = '2027-01-01T09:00:00Z'
$room1 = PostSchedule $roomStart $roomEnd $roomId
$room2 = PostSchedule $roomStart $roomEnd $roomId
$roomResults = @([int]$room1.GetAwaiter().GetResult().StatusCode, [int]$room2.GetAwaiter().GetResult().StatusCode) | Sort-Object
if (($roomResults -join ',') -ne '201,409') { throw "Concurrent room conflict failed: $($roomResults -join ',')" }
$touch = (PostSchedule '2027-01-01T09:00:00Z' '2027-01-01T10:00:00Z' $roomId).GetAwaiter().GetResult()
if ([int]$touch.StatusCode -ne 201) { throw "Touching interval rejected: $([int]$touch.StatusCode)" }
$twoSchedules = SqlRead "SELECT COUNT(*) FROM dbo.ExamSchedules WHERE ExamId = $examId;"
if ($twoSchedules -ne '2') { throw 'One exam to two schedules fixture failed.' }
Write-Output 'ROOM_CONCURRENT=201,409;TOUCHING_INTERVAL=201;ONE_EXAM_TWO_SCHEDULES=PASS'
$newId = [int](SqlRead "SELECT TOP (1) Id FROM dbo.ExamSchedules WHERE ExamId=$examId ORDER BY Id;")
$reloaded = $http.GetAsync("$BaseUrl/web/exam-schedules/$newId").GetAwaiter().GetResult()
RequireStatus $reloaded 200 'Reload UTC schedule'
$newRow = ResponseData $reloaded
if ($newRow.timeZoneStatus -ne 'utc' -or $newRow.startTime -notmatch 'Z$' -or
    (SqlRead "SELECT COUNT(*) FROM dbo.ExamSchedules WHERE Id=$newId AND IsTimeUtc=1;") -ne '1') {
    throw "UTC round-trip failed: status=$($newRow.timeZoneStatus), start=$($newRow.startTime), id=$newId"
}
$legacyId = SqlRead 'SELECT TOP (1) s.Id FROM dbo.ExamSchedules s WHERE s.IsTimeUtc=0 AND NOT EXISTS (SELECT 1 FROM dbo.DoingExams d WHERE d.ExamScheduleId=s.Id) AND NOT EXISTS (SELECT 1 FROM dbo.Submissions sub WHERE sub.ExamScheduleId=s.Id) ORDER BY s.Id;'
if ($legacyId) {
    $legacy = $http.GetAsync("$BaseUrl/web/exam-schedules/$legacyId").GetAwaiter().GetResult()
    RequireStatus $legacy 200 'Reload unknown schedule'
    $legacyRow = ResponseData $legacy
    if ($legacyRow.timeZoneStatus -ne 'unknown' -or $legacyRow.startTime -match 'Z$') {
        throw 'Legacy datetime2 was inferred as UTC.'
    }
    RequireStatus (SendSchedule PUT ([int]$legacyId) ([int]$legacyRow.examId) $legacyRow.roomId "$($legacyRow.startTime)Z" "$($legacyRow.endTime)Z") 409 'Unknown-zone schedule update'
    RequireStatus ((PostAssignment $classA ([int]$legacyId)).GetAwaiter().GetResult()) 409 'Unknown-zone class assignment'
    RequireStatus ($http.DeleteAsync("$BaseUrl/web/exam-schedules/$legacyId").GetAwaiter().GetResult()) 409 'Unknown-zone schedule soft delete'
    $legacyLinkId = SqlRead 'SELECT TOP (1) l.Id FROM dbo.ClassExamSchedule l JOIN dbo.ExamSchedules s ON s.Id=l.ExamScheduleId WHERE s.IsTimeUtc=0 AND NOT EXISTS (SELECT 1 FROM dbo.DoingExams d WHERE d.ExamScheduleId=s.Id) AND NOT EXISTS (SELECT 1 FROM dbo.Submissions sub WHERE sub.ExamScheduleId=s.Id) ORDER BY l.Id;'
    if ($legacyLinkId) {
        RequireStatus ($http.DeleteAsync("$BaseUrl/web/class-exam-schedules/$legacyLinkId").GetAwaiter().GetResult()) 409 'Unknown-zone class unassignment'
        $legacyLinkJson = ConvertTo-Json @{ classId = $classA; examScheduleId = [int]$legacyId } -Compress
        RequireStatus ($http.PutAsync("$BaseUrl/web/class-exam-schedules/$legacyLinkId", [System.Net.Http.StringContent]::new($legacyLinkJson,[Text.Encoding]::UTF8,'application/json')).GetAwaiter().GetResult()) 409 'Unknown-zone class assignment update'
        if ((SqlRead "SELECT COUNT(*) FROM dbo.ClassExamSchedule WHERE Id=$legacyLinkId;") -ne '1') {
            throw 'Unknown-zone class assignment changed after rejected request.'
        }
    }
    if ((SqlRead "SELECT COUNT(*) FROM dbo.ExamSchedules WHERE Id=$legacyId AND Status=$($legacyRow.status);") -ne '1') {
        throw 'Unknown-zone schedule changed after rejected request.'
    }
}
Write-Output 'UTC_SQL_ROUNDTRIP_AND_LEGACY_UNKNOWN_LOCK=PASS'

$studentA = (PostSchedule '2027-01-02T08:00:00Z' '2027-01-02T09:00:00Z' $null).GetAwaiter().GetResult()
$studentB = (PostSchedule '2027-01-02T08:00:00Z' '2027-01-02T09:00:00Z' $null).GetAwaiter().GetResult()
if ([int]$studentA.StatusCode -ne 201 -or [int]$studentB.StatusCode -ne 201) { throw 'Student conflict fixture schedules failed.' }
$scheduleA = [int](ResponseData $studentA).id
$scheduleB = [int](ResponseData $studentB).id
$assign1 = PostAssignment $classA $scheduleA
$assign2 = PostAssignment $classB $scheduleB
$assignResults = @([int]$assign1.GetAwaiter().GetResult().StatusCode, [int]$assign2.GetAwaiter().GetResult().StatusCode) | Sort-Object
if (($assignResults -join ',') -ne '201,409') { throw "Concurrent student conflict failed: $($assignResults -join ',')" }
Write-Output 'STUDENT_ASSIGNMENT_CONCURRENT=201,409'

$studentSchedules = $http.GetAsync("$BaseUrl/web/student/schedules").GetAwaiter().GetResult()
if ([int]$studentSchedules.StatusCode -ne 200) { throw 'Student schedule read failed.' }
$scheduleRows = (ResponseData $studentSchedules)
if (@($scheduleRows).Count -lt 1) { throw 'Approved student has no visible assigned schedule.' }

$assignedLink = (SqlRead "SELECT TOP (1) CONCAT(Id,',',ExamScheduleId) FROM dbo.ClassExamSchedule WHERE ExamScheduleId IN ($scheduleA,$scheduleB);").Split(',')
if ($assignedLink.Count -ne 2) { throw 'Assigned schedule fixture missing.' }
$linkId = [int]$assignedLink[0]; $lockedScheduleId = [int]$assignedLink[1]
SqlWrite "INSERT INTO dbo.DoingExams (UserId,ExamId,ExamScheduleId,StartTime) VALUES ('$userId',$examId,$lockedScheduleId,'2027-01-02T08:00:00');" | Out-Null
$lockedBefore = SqlRead "SELECT CONCAT(ExamId,',',ISNULL(CONVERT(varchar(20),RoomId),'NULL'),',',CONVERT(varchar(33),StartTime,126),',',CONVERT(varchar(33),EndTime,126),',',Status) FROM dbo.ExamSchedules WHERE Id=$lockedScheduleId;"
RequireStatus (SendSchedule PUT $lockedScheduleId $examId $null '2027-01-02T08:30:00Z' '2027-01-02T09:30:00Z') 409 'DoingExam time change'
RequireStatus (SendSchedule PUT $lockedScheduleId $examId $roomId2 '2027-01-02T08:00:00Z' '2027-01-02T09:00:00Z') 409 'DoingExam room change'
RequireStatus (SendSchedule PUT $lockedScheduleId $examId2 $null '2027-01-02T08:00:00Z' '2027-01-02T09:00:00Z') 409 'DoingExam exam change'
RequireStatus ((PostAssignment $classB $lockedScheduleId).GetAwaiter().GetResult()) 409 'DoingExam class assignment'
RequireStatus ($http.DeleteAsync("$BaseUrl/web/class-exam-schedules/$linkId").GetAwaiter().GetResult()) 409 'DoingExam class unassignment'
RequireStatus ($http.DeleteAsync("$BaseUrl/web/exam-schedules/$lockedScheduleId").GetAwaiter().GetResult()) 409 'DoingExam soft delete'
$lockedAfter = SqlRead "SELECT CONCAT(ExamId,',',ISNULL(CONVERT(varchar(20),RoomId),'NULL'),',',CONVERT(varchar(33),StartTime,126),',',CONVERT(varchar(33),EndTime,126),',',Status) FROM dbo.ExamSchedules WHERE Id=$lockedScheduleId;"
if ($lockedBefore -ne $lockedAfter -or (SqlRead "SELECT COUNT(*) FROM dbo.ClassExamSchedule WHERE Id=$linkId;") -ne '1') {
    throw 'DoingExam rejection changed persisted schedule or assignment.'
}
Write-Output 'DOING_EXAM_MUTATIONS=409;PERSISTED_DATA_UNCHANGED=PASS'

$editable = SendSchedule POST 0 $examId $roomId2 '2027-02-01T08:00:00Z' '2027-02-01T09:00:00Z'
RequireStatus $editable 201 'Create editable schedule'
$editableId = [int](ResponseData $editable).id
$editableLink = (PostAssignment $classA $editableId).GetAwaiter().GetResult()
RequireStatus $editableLink 201 'Assign editable schedule'
$editableLinkId = [int](ResponseData $editableLink).id
RequireStatus (SendSchedule PUT $editableId $examId2 $roomId '2027-02-01T10:00:00Z' '2027-02-01T11:00:00Z') 200 'Update time/exam/room without attempts'
$editableReload = $http.GetAsync("$BaseUrl/web/exam-schedules/$editableId").GetAwaiter().GetResult()
RequireStatus $editableReload 200 'Reload edited schedule'
$editableRow = ResponseData $editableReload
if ($editableRow.examId -ne $examId2 -or $editableRow.roomId -ne $roomId -or
    $editableRow.startTime -ne '2027-02-01T10:00:00Z' -or $editableRow.timeZoneStatus -ne 'utc' -or
    $editableRow.hasAttempts -ne $false) { throw 'Editable schedule did not round-trip through SQL.' }
RequireStatus ($http.DeleteAsync("$BaseUrl/web/class-exam-schedules/$editableLinkId").GetAwaiter().GetResult()) 200 'Unassign without attempts'
RequireStatus ($http.DeleteAsync("$BaseUrl/web/exam-schedules/$editableId").GetAwaiter().GetResult()) 200 'Soft delete without attempts'
if ((SqlRead "SELECT COUNT(*) FROM dbo.ClassExamSchedule WHERE Id=$editableLinkId;") -ne '0' -or
    (SqlRead "SELECT COUNT(*) FROM dbo.ExamSchedules WHERE Id=$editableId AND Status=255 AND IsTimeUtc=1;") -ne '1') {
    throw 'Allowed writes did not persist as expected.'
}
Write-Output 'NO_ATTEMPT_CREATE_UPDATE_ASSIGN_UNASSIGN_DELETE=PASS;UTC_RELOAD=PASS'

$submissionSchedule = SendSchedule POST 0 $examId $roomId2 '2027-02-02T08:00:00Z' '2027-02-02T09:00:00Z'
RequireStatus $submissionSchedule 201 'Create submission schedule'
$submissionScheduleId = [int](ResponseData $submissionSchedule).id
$submissionLink = (PostAssignment $classA $submissionScheduleId).GetAwaiter().GetResult()
RequireStatus $submissionLink 201 'Assign submission schedule'
$submissionLinkId = [int](ResponseData $submissionLink).id
$detailId = SqlWrite @"
INSERT INTO dbo.ExamDetails (ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy)
VALUES ($examId,N'QA-$tag',1,'2027-02-02T07:00:00','$userId','2027-02-02T07:00:00','$userId');
SELECT CONVERT(int,SCOPE_IDENTITY());
"@
SqlWrite "INSERT INTO dbo.Submissions (UserId,ExamDetailId,ExamScheduleId,SubmitTime,TimeTaken,TotalMark,IsPassed,UnAnswered,Answered,Status) VALUES ('$userId',$detailId,$submissionScheduleId,'2027-02-02T08:30:00','00:30:00',8,1,0,1,1);" | Out-Null
$submissionBefore = SqlRead "SELECT CONCAT(ExamId,',',RoomId,',',CONVERT(varchar(33),StartTime,126),',',CONVERT(varchar(33),EndTime,126),',',Status) FROM dbo.ExamSchedules WHERE Id=$submissionScheduleId;"
RequireStatus (SendSchedule PUT $submissionScheduleId $examId $roomId2 '2027-02-02T08:30:00Z' '2027-02-02T09:30:00Z') 409 'Submission time change'
RequireStatus (SendSchedule PUT $submissionScheduleId $examId $roomId '2027-02-02T08:00:00Z' '2027-02-02T09:00:00Z') 409 'Submission room change'
RequireStatus (SendSchedule PUT $submissionScheduleId $examId2 $roomId2 '2027-02-02T08:00:00Z' '2027-02-02T09:00:00Z') 409 'Submission exam change'
RequireStatus ((PostAssignment $classB $submissionScheduleId).GetAwaiter().GetResult()) 409 'Submission class assignment'
RequireStatus ($http.DeleteAsync("$BaseUrl/web/class-exam-schedules/$submissionLinkId").GetAwaiter().GetResult()) 409 'Submission class unassignment'
RequireStatus ($http.DeleteAsync("$BaseUrl/web/exam-schedules/$submissionScheduleId").GetAwaiter().GetResult()) 409 'Submission soft delete'
$submissionAfter = SqlRead "SELECT CONCAT(ExamId,',',RoomId,',',CONVERT(varchar(33),StartTime,126),',',CONVERT(varchar(33),EndTime,126),',',Status) FROM dbo.ExamSchedules WHERE Id=$submissionScheduleId;"
$submissionReload = ResponseData ($http.GetAsync("$BaseUrl/web/exam-schedules/$submissionScheduleId").GetAwaiter().GetResult())
if ($submissionBefore -ne $submissionAfter -or $submissionReload.hasAttempts -ne $true -or
    (SqlRead "SELECT COUNT(*) FROM dbo.ClassExamSchedule WHERE Id=$submissionLinkId;") -ne '1' -or
    (SqlRead "SELECT COUNT(*) FROM dbo.Submissions WHERE ExamScheduleId=$submissionScheduleId;") -ne '1') {
    throw 'Submission rejection changed persisted schedule, assignment or submission.'
}
Write-Output 'SUBMISSION_MUTATIONS=409;PERSISTED_DATA_UNCHANGED=PASS'

$refresh = AuthPost 'refresh' @{}
if ($refresh.Status -ne 200 -or -not $refresh.Payload.data.accessToken) { throw 'Refresh failed.' }
$refreshedToken = [string]$refresh.Payload.data.accessToken
$authHeaders['X-CSRF-TOKEN'] = (Invoke-RestMethod -Uri "$BaseUrl/web/auth/csrf" -Method Get `
    -WebSession $webSession -SkipCertificateCheck -Headers @{ Authorization = "Bearer $refreshedToken" }).data.csrfToken
if (-not $authHeaders['X-CSRF-TOKEN']) { throw 'Authenticated CSRF token missing.' }
$logout = AuthPost 'logout' @{} $refreshedToken
if ($logout.Status -ne 200) { throw "Logout failed: HTTP $($logout.Status), message=$($logout.Payload.message)" }
$afterLogout = Invoke-WebRequest -Uri "$BaseUrl/web/auth/me" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck `
    -Headers @{ Authorization = "Bearer $refreshedToken" }
if ([int]$afterLogout.StatusCode -ne 401) { throw 'Logged-out JWT still works.' }
Write-Output 'AUTH_REFRESH_LOGOUT_REVOKE=PASS;STUDENT_SCHEDULE_READ=PASS'

Write-Output "TEST_ACCOUNT_TAG=$tag"
