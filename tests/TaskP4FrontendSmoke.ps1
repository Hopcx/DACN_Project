# P4 - browser smoke FE: tạo fixture, chạy ../DACN_FE/tests/p4AdminFeaturesBrowserSmoke.mjs, hậu kiểm SQL, dọn theo tag.
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Cần: API (Development) trỏ DB bản sao ở $BaseUrl, Vite HTTPS ở https://localhost:5173, Chrome, sqlcmd, node. Chạy từ thư mục gốc DACN_Project.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -f 65001 -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51251, 'Wrong database', 1; $query"
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
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
# Tạo user bằng cách sao PasswordHash của user seed đã đăng ký; trả email để đăng nhập.
# Tạo user bằng cách sao PasswordHash của user seed đã đăng ký; trả email để đăng nhập.
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'P4 $name',N'p4$name$tag',N'p4$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "p4$name$tag@example.invalid"
}
function FindMail([string]$email) {
    return Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
}
function MailToken($mail) { return [Uri]::UnescapeDataString([regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)').Groups[1].Value) }
$S = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-p4-fe-$tag"
$accountHeader = @('FullName', 'DateOfBirth', 'Email', 'PhoneNumber', 'Address', 'Sex', 'UserName', 'Password')
function Cell([string]$ref, $value) {
    if ($value -is [int]) { return "<c r=`"$ref`"><v>$value</v></c>" }
    $escaped = [Security.SecurityElement]::Escape([string]$value)
    return "<c r=`"$ref`" t=`"inlineStr`"><is><t xml:space=`"preserve`">$escaped</t></is></c>"
}
# rows: mảng các mảng giá trị cột A..H (số nguyên [int] thành ô số). Tùy chọn: header, sheetName, formula, padBytes.
function Book([string]$name, [object[]]$rows, [string[]]$header = $accountHeader, [string]$sheetName = 'Accounts', [switch]$formula, [int]$padBytes = 0) {
    $path = Join-Path $work $name
    $xmlRows = New-Object Text.StringBuilder
    $cols = 'ABCDEFGH'
    [void]$xmlRows.Append('<row r="1">' + (($header | ForEach-Object -Begin { $i = 0 } -Process { Cell "$($cols[$i++])1" $_ }) -join '') + '</row>')
    $n = 2
    foreach ($row in $rows) {
        $cells = for ($i = 0; $i -lt $row.Count; $i++) { Cell "$($cols[$i])$n" $row[$i] }
        if ($formula -and $n -eq 2) { $cells[0] = '<c r="A2"><f>1+0</f><v>1</v></c>' }
        [void]$xmlRows.Append("<row r=`"$n`">$($cells -join '')</row>"); $n++
    }
    $entries = [ordered]@{
        '[Content_Types].xml' = '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'
        '_rels/.rels' = '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'
        'xl/workbook.xml' = "<workbook xmlns=`"$S`" xmlns:r=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships`"><sheets><sheet name=`"$sheetName`" sheetId=`"1`" r:id=`"rId1`"/></sheets></workbook>"
        'xl/_rels/workbook.xml.rels' = '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'
        'xl/worksheets/sheet1.xml' = "<worksheet xmlns=`"$S`"><sheetData>$($xmlRows.ToString())</sheetData></worksheet>"
    }
    $file = [IO.File]::Create($path)
    try {
        $zip = [IO.Compression.ZipArchive]::new($file, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($key in $entries.Keys) {
                $writer = [IO.StreamWriter]::new($zip.CreateEntry($key).Open(), [Text.UTF8Encoding]::new($false))
                try { $writer.Write($entries[$key]) } finally { $writer.Dispose() }
            }
            if ($padBytes -gt 0) {
                $stream = $zip.CreateEntry('docProps/padding.bin', [IO.Compression.CompressionLevel]::NoCompression).Open()
                try { $stream.Write([Security.Cryptography.RandomNumberGenerator]::GetBytes($padBytes)) } finally { $stream.Dispose() }
            }
        } finally { $zip.Dispose() }
    } finally { $file.Dispose() }
    return $path
}

$seed = [Guid]::Empty; $admin = [Guid]::NewGuid(); $teacher = [Guid]::NewGuid(); $mail = $null; $startUtc = $null
$seedEmail = "p4feseed$tag@example.invalid"
$badEmail1 = "p4fb1$tag@example.invalid"; $badEmail2 = "p4fb2$tag@example.invalid"
$levelName = "P4 FE mức $tag"
$subjectName = "0000 P4 FE môn $tag"
$className = "P4 FE lớp $tag"
try {
    # Bước 0: kiểm sẵn sàng (không tự khởi động API/Vite). API chưa chạy thì dòng $csrf ở đầu file đã dừng.
    if (-not (Test-Path -LiteralPath '../DACN_FE/tests/p4AdminFeaturesBrowserSmoke.mjs')) { throw 'Run from DACN_Project root; ../DACN_FE smoke not found.' }
    try { Invoke-WebRequest 'https://localhost:5173/' -SkipCertificateCheck -SkipHttpErrorCheck | Out-Null }
    catch { throw 'Vite HTTPS is not running at https://localhost:5173.' }

    # Seed: đăng ký + xác minh một user thật để sao PasswordHash cho Admin/Giảng viên.
    Expect (Auth 'register' @{ fullName = 'P4 FE seed'; userName = "p4feseed$tag"; email = $seedEmail; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = FindMail $seedEmail
    if (-not $mail) { throw 'Verification mail not found in Project.Api/.maildrop.' }
    Expect (Auth 'verify-email' @{ token = (MailToken $mail) }).status 200 'verify'
    $seed = [Guid](Sql "SELECT Id FROM dbo.Users WHERE Email=N'$seedEmail';")
    $adminEmail = CloneUser $admin 1 'feadmin' $seed
    $teacherEmail = CloneUser $teacher 3 'feteacher' $seed

    # Fixture nội dung: mức độ, môn, 2 câu hỏi (một đáp án + đúng/sai), lớp của giảng viên.
    Sql @"
INSERT dbo.QuestionLevels(Name,Description,Status) VALUES (N'$levelName',N'QA',1);
DECLARE @level int=SCOPE_IDENTITY();
INSERT dbo.Subjects(Name,Status) VALUES (N'$subjectName',1);
DECLARE @subject int=SCOPE_IDENTITY(), @q int;
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId) VALUES (N'P4 FE $tag một đáp án',SYSUTCDATETIME(),1,@subject,2,@level); SET @q=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'A',0,1),(@q,N'B',1,1);
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,QuestionLevelId) VALUES (N'P4 FE $tag đúng sai',SYSUTCDATETIME(),1,@subject,1,@level); SET @q=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'Đúng',1,1),(@q,N'Sai',0,1);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'$className',N'P4FE-$tag',5,'$teacher',@subject,1);
"@ | Out-Null
    $subject = [int](Sql "SELECT Id FROM dbo.Subjects WHERE Name=N'$subjectName';")

    # Danh sách môn ở /admin/questions là virtual list: môn fixture phải nằm trong 5 vị trí đầu theo Name.
    $rank = [int](Sql "SELECT COUNT(*) FROM dbo.Subjects WHERE Status=1 AND Name < N'$subjectName';")
    if ($rank -ge 5) { throw "Subject '$subjectName' is option #$($rank + 1); antd virtual list may not render it. Remove leftover subjects named '0000 ...'." }

    # File import lỗi: dòng 2 hợp lệ, dòng 3 sai cột Sex -> BE từ chối nguyên khối (400), không ghi gì.
    New-Item -ItemType Directory $work | Out-Null
    $badFile = Book 'bad-accounts.xlsx' @(
        @("P4 FE Hợp lệ $tag", '2001-02-03', $badEmail1, '', 'Hà Nội', '1', "p4fb1$tag", "Aa1$tag"),
        @("P4 FE Lỗi $tag", '2001-02-03', $badEmail2, '', 'Huế', '9', "p4fb2$tag", "Aa1$tag"))

    $startUtc = Sql "SELECT CONVERT(varchar(27),SYSUTCDATETIME(),121);"

    $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag; $env:DACN_QA_ADMIN = $adminEmail; $env:DACN_QA_TEACHER = $teacherEmail
    $env:DACN_QA_DATABASE = $Database; $env:DACN_QA_SUBJECT_NAME = $subjectName; $env:DACN_QA_CLASS = $className
    $env:DACN_QA_BAD_ACCOUNTS_FILE = $badFile; $env:DACN_QA_BAD_ACCOUNTS_EMAIL = $badEmail1
    Push-Location ../DACN_FE
    try { & node tests/p4AdminFeaturesBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'P4 FE browser smoke failed.' } }
    finally { Pop-Location }

    # Hậu kiểm SQL: 2 audit của Admin (export câu hỏi + export tài khoản; tải mẫu và import lỗi không ghi audit),
    # mô tả audit khớp số bản ghi, giảng viên không có audit, file lỗi không tạo user, cả hai đã đăng nhập (có refresh token).
    $check = Sql @"
SELECT CONCAT(
  (SELECT COUNT(*) FROM dbo.Logs WHERE ActorUserId='$admin'), ',',
  (SELECT COUNT(*) FROM dbo.Logs WHERE ActorUserId='$admin' AND Action=N'question.export' AND Name=N'question.export' AND EntityType=N'Subject' AND EntityId=N'$subject'
     AND Description=N'Xuất 2 câu hỏi; questionTypeId=-; questionLevelId=-' AND Status=1 AND CorrelationId IS NOT NULL AND CreatedAtUtc >= '$startUtc'), ',',
  (SELECT COUNT(*) FROM dbo.Logs WHERE ActorUserId='$admin' AND Action=N'account.export' AND EntityType=N'User' AND EntityId IS NULL
     AND Description=CONCAT(N'Xuất ',(SELECT COUNT(*) FROM dbo.Users),N' tài khoản; levelId=-') AND CorrelationId IS NOT NULL AND CreatedAtUtc >= '$startUtc'), ',',
  (SELECT COUNT(*) FROM dbo.Logs WHERE ActorUserId IN ('$teacher','$seed')), ',',
  (SELECT COUNT(*) FROM dbo.Users WHERE Email IN (N'$badEmail1',N'$badEmail2') OR UserName IN (N'p4fb1$tag',N'p4fb2$tag')), ',',
  (CASE WHEN EXISTS(SELECT 1 FROM dbo.RefreshTokens WHERE UserId='$admin') THEN 1 ELSE 0 END), ',',
  (CASE WHEN EXISTS(SELECT 1 FROM dbo.RefreshTokens WHERE UserId='$teacher') THEN 1 ELSE 0 END));
"@
    if ($check -ne '2,1,1,0,0,1,1') { throw "SQL post-check mismatch: $check" }
    Write-Output "P4_FRONTEND_SMOKE=PASS;DB=$Database;TAG=$tag;SQL=$check"
}
finally {
    Remove-Item Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_ADMIN, Env:DACN_QA_TEACHER, Env:DACN_QA_DATABASE, Env:DACN_QA_SUBJECT_NAME, Env:DACN_QA_CLASS, Env:DACN_QA_BAD_ACCOUNTS_FILE, Env:DACN_QA_BAD_ACCOUNTS_EMAIL -ErrorAction SilentlyContinue
    # Dọn theo tag/tên fixture (không dựa vào biến id đã gán hay chưa); Logs chỉ xóa theo actor fixture.
    Sql @"
DECLARE @u TABLE(Id uniqueidentifier);
INSERT @u SELECT Id FROM dbo.Users WHERE Id IN ('$seed','$admin','$teacher') OR Email IN (N'$seedEmail',N'$badEmail1',N'$badEmail2') OR UserName IN (N'p4fb1$tag',N'p4fb2$tag');
DELETE dbo.Logs WHERE ActorUserId IN (SELECT Id FROM @u);
DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'P4FE-$tag');
DELETE dbo.Classes WHERE ClassCode=N'P4FE-$tag';
DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId JOIN dbo.Subjects s ON s.Id=q.SubjectId WHERE s.Name=N'$subjectName';
DELETE q FROM dbo.Questions q JOIN dbo.Subjects s ON s.Id=q.SubjectId WHERE s.Name=N'$subjectName';
DELETE dbo.Subjects WHERE Name=N'$subjectName';
DELETE dbo.QuestionLevels WHERE Name=N'$levelName';
DELETE dbo.UserPermissions WHERE UserId IN (SELECT Id FROM @u); DELETE dbo.RefreshTokens WHERE UserId IN (SELECT Id FROM @u);
DELETE dbo.EmailVerificationTokens WHERE UserId IN (SELECT Id FROM @u); DELETE dbo.Users WHERE Id IN (SELECT Id FROM @u);
"@ | Out-Null
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
    Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' -ErrorAction SilentlyContinue | Where-Object { (Get-Content $_.FullName -Raw) -match "To: p4f[a-z0-9]*$tag@" } | Remove-Item -ErrorAction SilentlyContinue
}
