# P4/F23 - API doc nhat ky: /web/admin/audit/{logs,exam-activity} va khoa route ghi (405).
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao đã áp migration P4AuditLogColumns.
# Chỉ ghi vào DB bản sao; fixture gắn tag ngẫu nhiên và được dọn trong finally.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -f 65001 -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51241, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
function Auth([string]$route, $body) {
    for ($i = 0; $i -lt 6; $i++) {
        $r = Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck `
            -Headers @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)
        if ([int]$r.StatusCode -ne 429) { return @{ status = [int]$r.StatusCode; body = ($r.Content | ConvertFrom-Json) } }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
# Gọi API; trả status, body JSON (nếu có) và bytes thô (cho tệp xlsx).
function Call([string]$method, [string]$url, [string]$token = '', $json = $null, $form = $null) {
    $args2 = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($token) { $args2.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $json) { $args2.ContentType = 'application/json'; $args2.Body = ($json | ConvertTo-Json -Compress) }
    if ($null -ne $form) { $args2.Form = $form }
    $r = Invoke-WebRequest @args2
    $text = if ($r.Headers['Content-Type'] -match 'json') { $r.Content } else { $null }
    return @{ status = [int]$r.StatusCode; body = if ($text) { $text | ConvertFrom-Json } else { $null };
        bytes = $r.RawContentStream.ToArray(); type = [string]($r.Headers['Content-Type'] | Select-Object -First 1) }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
# Tạo user bằng cách sao PasswordHash của user seed đã đăng ký; trả email để đăng nhập.
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'P4 $name',N'p4$name$tag',N'p4$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "p4$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }
function FindMail([string]$email) {
    return Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
}
function MailToken($mail) { return [Uri]::UnescapeDataString([regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)').Groups[1].Value) }
# Đọc workbook xlsx: tên sheet, các dòng (mảng ô), có công thức không, và toàn bộ văn bản mọi entry (để dò rò rỉ).
function ReadBook([byte[]]$bytes) {
    $S = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($bytes))
    try {
        $texts = @{}
        foreach ($entry in $zip.Entries) { $reader = [IO.StreamReader]::new($entry.Open()); $texts[$entry.FullName] = $reader.ReadToEnd(); $reader.Dispose() }
        $sheetName = ([xml]$texts['xl/workbook.xml']).GetElementsByTagName('sheet')[0].GetAttribute('name')
        $xml = [xml]$texts['xl/worksheets/sheet1.xml']
        $ns = [Xml.XmlNamespaceManager]::new($xml.NameTable); $ns.AddNamespace('m', $S)
        $rows = @(foreach ($row in $xml.SelectNodes('//m:row', $ns)) { , @($row.SelectNodes('m:c', $ns) | ForEach-Object { $_.InnerText }) })
        return @{ sheet = $sheetName; rows = $rows; hasFormula = [bool]($texts['xl/worksheets/sheet1.xml'] -match '<f[ >]'); all = ($texts.Values -join "`n") }
    } finally { $zip.Dispose() }
}
$seed = [Guid]::Empty; $admin = [Guid]::NewGuid(); $teacher = [Guid]::NewGuid(); $exam = 0; $mail = $null
$action = "p4test$tag"
try {
    $email = "p4auseed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'P4 Audit'; userName = "p4auseed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = FindMail $email
    Expect (Auth 'verify-email' @{ token = (MailToken $mail) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $adminToken = LoginToken (CloneUser $admin 1 'auadmin' $seed)
    $teacherToken = LoginToken (CloneUser $teacher 3 'auteacher' $seed)

    # Fixture: 5 dòng Logs cách nhau 1 phút (action riêng theo tag) và 3 dòng ExamActivityLogs.
    $base = [DateTime]::UtcNow.AddHours(-1); $base = [DateTime]::new($base.Year, $base.Month, $base.Day, $base.Hour, $base.Minute, 0, [DateTimeKind]::Utc)
    $iso = $base.ToString('yyyy-MM-ddTHH:mm:ss')
    Sql @"
INSERT dbo.Logs(Name,Description,Status,ActorUserId,CreatedAtUtc,Action,EntityType,EntityId,CorrelationId) VALUES
(N'$action',N'P4 audit fixture 0',1,'$admin','$iso','$action',N'Fixture',N'0',N'corr-0'),
(N'$action',N'P4 audit fixture 1',1,'$admin',DATEADD(minute,1,'$iso'),'$action',N'Fixture',N'1',N'corr-1'),
(N'$action',N'P4 audit fixture 2',1,'$admin',DATEADD(minute,2,'$iso'),'$action',N'Fixture',N'2',N'corr-2'),
(N'$action',N'P4 audit fixture 3',1,'$admin',DATEADD(minute,3,'$iso'),'$action',N'Fixture',N'3',N'corr-3'),
(N'$action',N'P4 audit fixture 4',1,'$admin',DATEADD(minute,4,'$iso'),'$action',N'Fixture',N'4',N'corr-4');
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'P4 audit $tag',@subject,1,0,1,10,5,60,1);
"@ | Out-Null
    $exam = [int](Sql "SELECT Id FROM dbo.Exams WHERE Name=N'P4 audit $tag';")
    Sql @"
INSERT dbo.ExamActivityLogs(ExamId,ExamDetailId,ExamScheduleId,UserId,ActionTime,ActionType) VALUES
($exam,NULL,NULL,'$seed','2026-01-01T08:00:00',N'start'),
($exam,NULL,NULL,'$seed','2026-01-01T09:00:00',N'submit'),
($exam,NULL,NULL,'$admin','2026-01-02T08:00:00',N'start');
"@ | Out-Null
    $logTotal = { [int](Sql 'SELECT COUNT(*) FROM dbo.Logs;') }
    $actTotal = { [int](Sql 'SELECT COUNT(*) FROM dbo.ExamActivityLogs;') }

    Write-Output '== Quyền và tham số'
    foreach ($url in '/web/admin/audit/logs', '/web/admin/audit/exam-activity') {
        Expect (Call 'GET' $url).status 401 "anonymous $url"
        Expect (Call 'GET' $url $studentToken).status 403 "student $url"
        Expect (Call 'GET' $url $teacherToken).status 403 "teacher $url"
        Expect (Call 'GET' "${url}?pageSize=0" $adminToken).status 400 "pageSize=0 $url"
        Expect (Call 'GET' "${url}?pageSize=101" $adminToken).status 400 "pageSize=101 $url"
        Expect (Call 'GET' "${url}?page=0" $adminToken).status 400 "page=0 $url"
    }
    Expect (Call 'GET' '/web/admin/audit/logs?fromUtc=2026-01-01T00:00:00' $adminToken).status 400 'fromUtc without Z'
    Expect (Call 'GET' '/web/admin/audit/logs?fromUtc=2026-01-02T00:00:00Z&toUtc=2026-01-01T00:00:00Z' $adminToken).status 400 'fromUtc > toUtc'
    Expect (Call 'GET' '/web/admin/audit/exam-activity?from=2026-01-02&to=2026-01-01' $adminToken).status 400 'from > to'
    Expect (Call 'GET' '/web/admin/audit/exam-activity?examId=0' $adminToken).status 400 'examId=0'
    Pass '401/403/400 cho cả hai API đọc'

    Write-Output '== Nhật ký hệ thống (Logs)'
    $q = "action=$action"
    $all = Call 'GET' "/web/admin/audit/logs?$q&pageSize=100" $adminToken
    Expect $all.status 200 'logs'; Expect $all.body.data.totalCount 5 'totalCount with action filter'
    $first = $all.body.data.items[0]
    Expect $first.correlationId 'corr-4' 'order createdAt desc'; Expect $first.actorUserName "p4auadmin$tag" 'actor user name joined'
    if ($first.createdAt -notmatch 'Z$' -and $first.createdAt -isnot [datetime]) { throw "createdAt not UTC: $($first.createdAt)" }
    $seen = @()
    foreach ($page in 1, 2, 3) {
        $p = Call 'GET' "/web/admin/audit/logs?$q&page=$page&pageSize=2" $adminToken
        Expect $p.status 200 "page $page"; Expect $p.body.data.totalCount 5 "page $page totalCount"
        $seen += @($p.body.data.items | ForEach-Object { $_.id })
    }
    Expect $seen.Count 5 'paged item count'; Expect @($seen | Select-Object -Unique).Count 5 'paged ids are distinct'
    Expect (Call 'GET' "/web/admin/audit/logs?$q&actorUserId=$admin" $adminToken).body.data.totalCount 5 'actor filter'
    Expect (Call 'GET' "/web/admin/audit/logs?$q&actorUserId=$seed" $adminToken).body.data.totalCount 0 'other actor'
    $from = [Uri]::EscapeDataString($base.AddMinutes(1).ToString('yyyy-MM-ddTHH:mm:ssZ')); $to = [Uri]::EscapeDataString($base.AddMinutes(4).ToString('yyyy-MM-ddTHH:mm:ssZ'))
    Expect (Call 'GET' "/web/admin/audit/logs?$q&fromUtc=$from&toUtc=$to" $adminToken).body.data.totalCount 3 'time range [from,to)'
    Expect (Call 'GET' "/web/admin/audit/logs?search=fixture%203&$q" $adminToken).body.data.totalCount 1 'search by description'
    Expect (Call 'GET' '/web/admin/audit/logs?pageSize=1' $adminToken).body.data.totalCount (& $logTotal) 'unfiltered totalCount vs SQL'
    Pass 'logs: lọc action/actor/khoảng giờ/search, phân trang 3 trang không trùng, totalCount khớp SQL'

    Write-Output '== Nhật ký hoạt động thi (ExamActivityLogs)'
    $byExam = Call 'GET' "/web/admin/audit/exam-activity?examId=$exam" $adminToken
    Expect $byExam.status 200 'exam-activity'; Expect $byExam.body.data.totalCount 3 'examId filter'
    Expect $byExam.body.data.items[0].userName "p4auadmin$tag" 'ordered by ActionTime desc'
    Expect $byExam.body.data.items[0].examName "P4 audit $tag" 'exam name joined'
    Expect (Call 'GET' "/web/admin/audit/exam-activity?examId=$exam&userId=$seed" $adminToken).body.data.totalCount 2 'userId filter'
    Expect (Call 'GET' "/web/admin/audit/exam-activity?examId=$exam&actionType=submit" $adminToken).body.data.totalCount 1 'actionType filter'
    Expect (Call 'GET' "/web/admin/audit/exam-activity?examId=$exam&from=2026-01-01T08:30:00&to=2026-01-02T00:00:00" $adminToken).body.data.totalCount 1 'time range keeps stored values as-is'
    Expect (Sql "SELECT COUNT(*) FROM dbo.ExamActivityLogs WHERE ExamId=$exam AND UserId='$seed';") '2' 'SQL cross-check'
    $raw = [Text.Encoding]::UTF8.GetString($byExam.bytes)
    if ($raw -notmatch '"actionTime":"2026-01-01T08:00:00"') { throw "ActionTime must be returned as stored (no Z): $raw" }
    Pass 'exam-activity: lọc examId/userId/actionType/khoảng giờ khớp SQL, ActionTime giữ nguyên không gắn Z'

    Write-Output '== Route ghi bị khóa (405), SQL không đổi'
    $logsBefore = & $logTotal; $actBefore = & $actTotal
    $logBody = @{ name = 'x'; description = 'y'; status = 1 }
    $actBody = @{ examId = $exam; userId = "$seed"; actionTime = '2026-01-01T00:00:00'; actionType = 'x' }
    $someLog = Sql "SELECT TOP 1 Id FROM dbo.Logs WHERE Action=N'$action' ORDER BY Id;"
    $someAct = Sql "SELECT TOP 1 Id FROM dbo.ExamActivityLogs WHERE ExamId=$exam ORDER BY Id;"
    Expect (Call 'POST' '/web/logs' $adminToken $logBody).status 405 'POST /web/logs'
    Expect (Call 'PUT' "/web/logs/$someLog" $adminToken $logBody).status 405 'PUT /web/logs'
    Expect (Call 'DELETE' "/web/logs/$someLog" $adminToken).status 405 'DELETE /web/logs'
    Expect (Call 'POST' '/web/exam-activity-logs' $adminToken $actBody).status 405 'POST /web/exam-activity-logs'
    Expect (Call 'PUT' "/web/exam-activity-logs/$someAct" $adminToken $actBody).status 405 'PUT /web/exam-activity-logs'
    Expect (Call 'DELETE' "/web/exam-activity-logs/$someAct" $adminToken).status 405 'DELETE /web/exam-activity-logs'
    Expect (& $logTotal) $logsBefore 'Logs unchanged'; Expect (& $actTotal) $actBefore 'ExamActivityLogs unchanged'
    Expect (Call 'GET' '/web/logs' $adminToken).status 200 'GET /web/logs still works'
    Expect (Call 'GET' '/web/exam-activity-logs' $adminToken).status 200 'GET /web/exam-activity-logs still works'
    Pass '6 route ghi trả 405 và SQL không đổi; GET cũ vẫn 200'
    Write-Output "P4_AUDIT_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    $users = "'$admin','$teacher','$seed'"
    Sql "DELETE dbo.Logs WHERE Action=N'$action' OR ActorUserId IN ($users);"  | Out-Null
    if ($exam) { Sql "DELETE dbo.ExamActivityLogs WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" | Out-Null }
    Sql "DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N'p4auseed$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
