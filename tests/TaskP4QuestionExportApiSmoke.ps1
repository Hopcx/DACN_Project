# P4/F12 - GET /web/questions/export: dinh dang v1, nhap lai duoc, loi 409 theo cau, audit.
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
$sheetHeader = @('SubjectId', 'QuestionLevelId', 'QuestionTypeId', 'Content', 'Answers', 'CorrectIndexes')

$seed = [Guid]::Empty; $admin = [Guid]::NewGuid(); $teacher = [Guid]::NewGuid(); $examiner = [Guid]::NewGuid(); $noPerm = [Guid]::NewGuid()
$subject = 0; $subjectBig = 0; $empty = 0; $levelId = 0; $mail = $null
try {
    $email = "p4qx$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'P4 Export'; userName = "p4qx$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = FindMail $email
    Expect (Auth 'verify-email' @{ token = (MailToken $mail) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $adminToken = LoginToken (CloneUser $admin 1 'qxadmin' $seed)
    $teacherToken = LoginToken (CloneUser $teacher 3 'qxteacher' $seed)
    $examinerEmail = CloneUser $examiner 2 'qxexaminer' $seed
    Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);" | Out-Null
    $examinerToken = LoginToken $examinerEmail
    $noPermToken = LoginToken (CloneUser $noPerm 2 'qxnoperm' $seed)

    $levelId = [int](Sql "INSERT dbo.QuestionLevels(Name,Description,Status) VALUES (N'P4 export $tag',N'QA',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    $subject = [int](Sql "INSERT dbo.Subjects(Name,Status) VALUES (N'P4 môn export $tag',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    $subjectBig = [int](Sql "INSERT dbo.Subjects(Name,Status) VALUES (N'P4 môn lớn $tag',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    $empty = [int](Sql "INSERT dbo.Subjects(Name,Status) VALUES (N'P4 môn rỗng $tag',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    # Ba câu: loại 1 (Đúng/Sai), 2 (một đáp án), 3 (nhiều đáp án); nội dung có ký tự cần escape XML và chuỗi bắt đầu bằng = hoặc +.
    Sql @"
DECLARE @q int;
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId) VALUES (N'P4 export $tag đúng sai & <b>',SYSUTCDATETIME(),1,$subject,1,$levelId); SET @q=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'Đúng',1,1),(@q,N'Sai',0,1);
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId) VALUES (N'P4 export $tag một đáp án =1+1',SYSUTCDATETIME(),1,$subject,2,$levelId); SET @q=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'A',0,1),(@q,N'+B',0,1),(@q,N'C',1,1);
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId) VALUES (N'P4 export $tag nhiều đáp án',SYSUTCDATETIME(),1,$subject,3,$levelId); SET @q=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'A',1,1),(@q,N'B',0,1),(@q,N'C',1,1),(@q,N'D',0,1);
"@ | Out-Null
    $sample = [int](Sql "SELECT TOP 1 Id FROM dbo.Questions WHERE SubjectId=$subject ORDER BY Id;")
    $logCount = { [int](Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'question.export' AND EntityId=N'$subject';") }

    Write-Output '== Quyền và tham số'
    Expect (Call 'GET' "/web/questions/export?subjectId=$subject").status 401 'anonymous'
    Expect (Call 'GET' "/web/questions/export?subjectId=$subject" $studentToken).status 403 'student'
    Expect (Call 'GET' "/web/questions/export?subjectId=$subject" $teacherToken).status 403 'teacher'
    Expect (Call 'GET' "/web/questions/export?subjectId=$subject" $noPermToken).status 403 'level 2 without permission 2'
    Expect (Call 'GET' '/web/questions/export' $adminToken).status 400 'missing subjectId'
    Expect (Call 'GET' '/web/questions/export?subjectId=0' $adminToken).status 400 'subjectId=0'
    Expect (Call 'GET' "/web/questions/export?subjectId=$subject&questionTypeId=-1" $adminToken).status 400 'questionTypeId<0'
    Expect (Call 'GET' '/web/questions/export?subjectId=2000000000' $adminToken).status 404 'unknown subject'
    Expect (Call 'GET' "/web/questions/export?subjectId=$empty" $adminToken).status 404 'subject without questions'
    Expect (& $logCount) 0 'no audit for rejected requests'
    Pass '401/403/400/404 và không ghi audit khi bị từ chối'

    Write-Output '== Export hợp lệ'
    $r = Call 'GET' "/web/questions/export?subjectId=$subject" $examinerToken
    Expect $r.status 200 'examiner export'
    Expect ($r.type -like 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet*') $true 'content-type'
    $book = ReadBook $r.bytes
    Expect $book.sheet 'Questions' 'sheet name'; Expect $book.hasFormula $false 'no formula'
    Expect ($book.rows[0] -join '|') ($sheetHeader -join '|') 'v1 header'
    Expect $book.rows.Count 4 'row count (header + 3)'
    $ids = @((Sql "SELECT Id FROM dbo.Questions WHERE SubjectId=$subject ORDER BY Id;") -split "`r?`n" | Where-Object { $_ })
    for ($i = 0; $i -lt 3; $i++) {
        $q = $ids[$i]; $row = $book.rows[$i + 1]
        Expect $row[0] "$subject" "row $($i + 2) subject"; Expect $row[1] "$levelId" "row $($i + 2) level"
        Expect $row[2] (Sql "SELECT QuestionTypeId FROM dbo.Questions WHERE Id=$q;") "row $($i + 2) type"
        Expect $row[3] (Sql "SELECT Content FROM dbo.Questions WHERE Id=$q;") "row $($i + 2) content"
        $answers = (Sql "SELECT Content FROM dbo.Answers WHERE QuestionId=$q ORDER BY Id;") -split "`r?`n"
        Expect $row[4] ($answers -join ';') "row $($i + 2) answers"
        $positions = (Sql "SELECT CONVERT(varchar(10),rn) FROM (SELECT ROW_NUMBER() OVER (ORDER BY Id) rn, IsCorrect FROM dbo.Answers WHERE QuestionId=$q) t WHERE IsCorrect=1 ORDER BY rn;") -split "`r?`n"
        Expect $row[5] ($positions -join ',') "row $($i + 2) correct indexes"
    }
    Expect (& $logCount) 1 'one audit row after successful export'
    Expect (Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'question.export' AND EntityId=N'$subject' AND ActorUserId='$examiner' AND EntityType=N'Subject' AND CreatedAtUtc > DATEADD(minute,-5,SYSUTCDATETIME()) AND CorrelationId IS NOT NULL;") '1' 'audit actor/time/correlation'
    Pass 'export 200: sheet Questions, header v1, A–F khớp SQL, audit đúng actor và giờ'
    Expect (Call 'GET' "/web/questions/$sample" $adminToken).status 200 'GET /web/questions/{id} not swallowed by export route'
    Pass 'GET /web/questions/{id} vẫn hoạt động'

    Write-Output '== Nhập lại file vừa xuất'
    $file = Join-Path ([IO.Path]::GetTempPath()) "dacn-p4-export-$tag.xlsx"
    [IO.File]::WriteAllBytes($file, $r.bytes)
    try {
        Sql "UPDATE dbo.Questions SET Status=255 WHERE SubjectId=$subject;" | Out-Null
        $import = Call 'POST' '/web/questions/import' $adminToken -form @{ file = Get-Item -LiteralPath $file }
        Expect $import.status 200 're-import'; Expect $import.body.data.imported 3 'imported 3'
        Expect (Sql "SELECT COUNT(*) FROM dbo.Questions WHERE SubjectId=$subject AND Status=1;") '3' 'SQL after re-import'
        $again = Call 'POST' '/web/questions/import' $adminToken -form @{ file = Get-Item -LiteralPath $file }
        Expect $again.status 409 'second import conflicts'
        if ((@($again.body.data.errors) | ForEach-Object { $_.code }) -notcontains 'DUPLICATE_DB') { throw 'Missing DUPLICATE_DB' }
    } finally { Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue }
    Pass 'ẩn 3 câu rồi nhập lại: 200 imported=3; nhập lần nữa: 409 DUPLICATE_DB'

    Write-Output '== Câu không xuất được (409, không file)'
    $bad = [int](Sql "INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId) VALUES (N'P4 export $tag không mức',SYSUTCDATETIME(),1,$subject,2,NULL); DECLARE @q int=SCOPE_IDENTITY();
      INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'A',1,1),(@q,N'B',0,1); SELECT @q;")
    $r = Call 'GET' "/web/questions/export?subjectId=$subject" $adminToken
    Expect $r.status 409 'level missing'; Expect $r.body.data.exported 0 'exported 0'
    $hit = @($r.body.data.errors | Where-Object { $_.questionId -eq $bad -and $_.code -eq 'LEVEL_MISSING' }); Expect $hit.Count 1 'LEVEL_MISSING for the bad question'
    Sql "UPDATE dbo.Questions SET QuestionLevelId=$levelId WHERE Id=$bad; UPDATE dbo.Answers SET Content=N'a;b' WHERE QuestionId=$bad AND IsCorrect=1;" | Out-Null
    $r = Call 'GET' "/web/questions/export?subjectId=$subject" $adminToken
    Expect $r.status 409 'separator'
    $hit = @($r.body.data.errors | Where-Object { $_.questionId -eq $bad -and $_.code -eq 'ANSWER_SEPARATOR' }); Expect $hit.Count 1 'ANSWER_SEPARATOR'
    Sql "UPDATE dbo.Questions SET Status=255 WHERE Id=$bad;" | Out-Null
    Expect (& $logCount) 1 'failed exports wrote no audit'
    Pass 'LEVEL_MISSING và ANSWER_SEPARATOR: 409 kèm questionId, không audit'

    Write-Output '== Quá 500 câu'
    Sql @"
;WITH n AS (SELECT TOP 501 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) i FROM sys.all_objects a CROSS JOIN sys.all_objects b)
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId) SELECT N'P4 export $tag lớn ' + CONVERT(nvarchar(10),i),SYSUTCDATETIME(),1,$subjectBig,2,$levelId FROM n;
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) SELECT Id,N'A',1,1 FROM dbo.Questions WHERE SubjectId=$subjectBig UNION ALL SELECT Id,N'B',0,1 FROM dbo.Questions WHERE SubjectId=$subjectBig;
"@ | Out-Null
    Expect (Call 'GET' "/web/questions/export?subjectId=$subjectBig" $adminToken).status 409 'too large'
    Pass '501 câu: 409 EXPORT_TOO_LARGE'
    Write-Output "P4_QUESTION_EXPORT_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    $subjects = (@($subject, $subjectBig, $empty) | Where-Object { $_ }) -join ','
    if ($subjects) { Sql "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects);
      DELETE dbo.Questions WHERE SubjectId IN ($subjects); DELETE dbo.Subjects WHERE Id IN ($subjects);" | Out-Null }
    if ($levelId) { Sql "DELETE dbo.QuestionLevels WHERE Id=$levelId;" | Out-Null }
    $users = "'$admin','$teacher','$examiner','$noPerm','$seed'"
    Sql "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N'p4qx$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
