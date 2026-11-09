param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Admin (LevelId = 1) phải vượt qua mọi policy: Student, Teacher, QuestionManagement, ScheduleManagement...
# Giảng viên thường vẫn bị giới hạn. Chạy từ thư mục gốc DACN_Project (cần Project.Api/.maildrop).
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51151, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL command failed: $($result -join ' ')" }
    return ($result | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$studentEmail = "adstudent$tag@example.invalid"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$adminId = [Guid]::NewGuid(); $teacherId = [Guid]::NewGuid(); $studentId = [Guid]::Empty
$classId = 0; $mail = $null
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
function Request([string]$method, [string]$route, [string]$token = '') {
    $headers = if ($token) { @{ Authorization = "Bearer $token" } } else { @{} }
    $result = Invoke-WebRequest "$BaseUrl$route" -Method $method -Headers $headers -SkipCertificateCheck -SkipHttpErrorCheck
    return @{ status = [int]$result.StatusCode; data = if ($result.Content -and $result.Headers.'Content-Type' -match 'json') { $result.Content | ConvertFrom-Json } else { $null } }
}
function Expect([int]$actual, [int]$expected, [string]$case) {
    if ($actual -ne $expected) { throw "$case expected HTTP $expected, got $actual" }
}
try {
    $register = Auth 'register' @{ fullName = 'Admin QA Student'; userName = "adstudent$tag"; email = $studentEmail; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00'; sex = $true }
    Expect $register.status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $studentEmail") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $studentLogin = Auth 'login' @{ keyword = $studentEmail; password = $password }
    Expect $studentLogin.status 200 'login Student'
    $studentId = [Guid]$studentLogin.data.data.userId
    $studentToken = [string]$studentLogin.data.data.accessToken
    # Admin và Teacher mới, không có bản ghi UserPermissions nào.
    foreach ($row in @(@($adminId, 'admin', 1), @($teacherId, 'teacher', 3))) {
        Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$($row[0])',N'QA $($row[1])',N'ad$($row[1])$tag',N'ad$($row[1])$tag@example.invalid',PasswordHash,$($row[2]),1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$studentId';" | Out-Null
    }
    $teacherLogin = Auth 'login' @{ keyword = "adteacher$tag@example.invalid"; password = $password }
    Expect $teacherLogin.status 200 'login Teacher'
    $teacherToken = [string]$teacherLogin.data.data.accessToken
    $adminLogin = Auth 'login' @{ keyword = "adadmin$tag@example.invalid"; password = $password }
    Expect $adminLogin.status 200 'login Admin'
    $adminToken = [string]$adminLogin.data.data.accessToken
    if (-not ($adminLogin.data.data.permissions.Count -eq 0)) { Write-Output 'Lưu ý: Admin có permission rows (vẫn hợp lệ).' }

    # Lớp thuộc giảng viên khác: Admin vẫn xem/ thao tác được, Teacher khác thì không.
    $classId = [int](Sql "INSERT dbo.Classes(Name,ClassCode,Description,Capacity,TeacherId,SubjectId,Status) SELECT TOP 1 N'Admin QA',N'ad$tag-1',N'QA',10,'$teacherId',Id,1 FROM dbo.Subjects; SELECT SCOPE_IDENTITY();")
    $matrix = @(
        @('GET', '/web/rooms?page=1&pageSize=5', 'ScheduleManagement'),
        @('GET', '/web/questions/import/template', 'QuestionManagement'),
        @('GET', '/web/student/classes', 'Student'),
        @('GET', '/web/student/schedules', 'Student'),
        @('GET', '/web/teacher/classes', 'Teacher'),
        @('GET', "/web/teacher/classes/$classId/members", 'Teacher (lớp của giảng viên khác)'),
        @('GET', '/web/admin/attempt-reports?page=1&pageSize=5', 'AdminManagement')
    )
    $locked = @('/web/users/get-all-users', '/web/classes', '/web/class-users', '/web/levels', '/web/permissions', '/web/user-permissions',
        '/web/exams', '/web/exam-details', '/web/questions', '/web/question-levels', '/web/question-types', '/web/subjects',
        '/web/exam-schedules', '/web/class-exam-schedules', '/web/logs', '/web/submissions', '/web/answers')
    foreach ($route in $locked) {
        $r = Request 'GET' $route $adminToken
        if ($r.status -in 401, 403) { throw "Admin bị chặn tại $route" }
        foreach ($who in @(@('Student', $studentToken), @('Teacher', $teacherToken))) {
            $d = Request 'GET' $route $who[1]
            if ($d.status -ne 403) { throw "$($who[0]) phải bị 403 tại $route, nhận $($d.status)" }
        }
    }
    Write-Output "Đã siết $($locked.Count) nhóm API: Admin qua, Student/Teacher bị 403."
    foreach ($case in $matrix) {
        $r = Request $case[0] $case[1] $adminToken
        if ($r.status -in 401, 403) { throw "Admin bị chặn tại $($case[2]) ($($case[1])): HTTP $($r.status)" }
        Write-Output ("Admin {0,-45} -> {1} ({2})" -f $case[1], $r.status, $case[2])
    }
    $all = Request 'GET' '/web/teacher/classes' $adminToken
    if (-not (@($all.data.data) | Where-Object { $_.id -eq $classId })) { throw 'Admin không thấy lớp của giảng viên khác.' }
    # Đối chứng: người không phải Admin vẫn bị giới hạn.
    Expect (Request 'GET' '/web/rooms?page=1&pageSize=5' $teacherToken).status 403 'Teacher không có quyền ScheduleManagement'
    Expect (Request 'GET' '/web/questions/import/template' $teacherToken).status 403 'Teacher không có quyền QuestionManagement'
    Expect (Request 'GET' '/web/student/classes' $teacherToken).status 403 'Teacher vào API học viên'
    Expect (Request 'GET' '/web/teacher/classes' $studentToken).status 403 'Student vào API giảng viên'
    Expect (Request 'GET' '/web/admin/attempt-reports?page=1&pageSize=5' $studentToken).status 403 'Student vào API Admin'
    Write-Output "ADMIN_PERMISSION_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    if ($classId) { Sql "DELETE FROM dbo.ClassUsers WHERE ClassId=$classId; DELETE FROM dbo.Classes WHERE Id=$classId;" | Out-Null }
    if ($studentId -eq [Guid]::Empty) {
        $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$studentEmail';"
        if ($found) { $studentId = [Guid]$found }
    }
    if ($studentId -ne [Guid]::Empty) {
        Sql "DELETE FROM dbo.RefreshTokens WHERE UserId IN ('$studentId','$teacherId','$adminId'); DELETE FROM dbo.EmailVerificationTokens WHERE UserId IN ('$studentId','$teacherId','$adminId'); DELETE FROM dbo.Users WHERE Id IN ('$studentId','$teacherId','$adminId');" | Out-Null
    }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
