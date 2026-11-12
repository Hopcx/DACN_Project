# P4/F13 - nhap/xuat tai khoan bang Excel: /web/users/excel/{template,import,export}.
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
$S = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-p4-accounts-$tag"; New-Item -ItemType Directory $work | Out-Null
$accountHeader = @('FullName', 'DateOfBirth', 'Email', 'PhoneNumber', 'Address', 'Sex', 'UserName', 'Password')
$exportHeader = @('FullName', 'DateOfBirth', 'Email', 'PhoneNumber', 'Address', 'Sex', 'UserName', 'LevelId', 'Status', 'EmailVerified')
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
function Upload([string]$path, [string]$token, $level) {
    $form = @{ file = Get-Item -LiteralPath $path }
    if ($null -ne $level) { $form.levelId = [string]$level }
    return Call 'POST' '/web/users/excel/import' $token -form $form
}
function Codes($result) { (@($result.body.data.errors) | ForEach-Object { "$($_.row):$($_.code)" }) -join ',' }
$created = { [int](Sql "SELECT COUNT(*) FROM dbo.Users WHERE UserName LIKE N'p4a%$tag';") }
$pw = "Aa1$tag"

$seed = [Guid]::Empty; $admin = [Guid]::NewGuid(); $teacher = [Guid]::NewGuid(); $mails = @(); $mail = $null
try {
    $email = "p4qseed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'P4 Accounts'; userName = "p4qseed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = FindMail $email
    Expect (Auth 'verify-email' @{ token = (MailToken $mail) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $adminToken = LoginToken (CloneUser $admin 1 'qadmin' $seed)
    $teacherToken = LoginToken (CloneUser $teacher 3 'qteacher' $seed)
    $good = @(
        @("P4 Nguyễn Văn A $tag", '2001-02-03', "p4a1$tag@example.invalid", "09$(([string]([Convert]::ToUInt32($tag, 16) % 100000000)).PadLeft(8, '0'))", 'Hà Nội', '1', "p4a1$tag", $pw),
        @("P4 Trần Thị B $tag", [int]36526, "P4A2$tag@Example.Invalid", '', 'Huế', '0', "p4a2$tag", $pw))

    Write-Output '== Quyền'
    foreach ($route in @(@('GET', '/web/users/excel/template'), @('GET', '/web/users/excel/export'))) {
        Expect (Call $route[0] $route[1]).status 401 "anonymous $($route[1])"
        Expect (Call $route[0] $route[1] $studentToken).status 403 "student $($route[1])"
        Expect (Call $route[0] $route[1] $teacherToken).status 403 "teacher $($route[1])"
    }
    Expect (Upload (Book 'ok.xlsx' $good) $studentToken 4).status 403 'student import'
    Expect (Upload (Book 'ok.xlsx' $good) $teacherToken 4).status 403 'teacher import'
    Pass '401/403 cho template, import, export'

    Write-Output '== Template'
    $t = Call 'GET' '/web/users/excel/template' $adminToken
    Expect $t.status 200 'template'; $tb = ReadBook $t.bytes
    Expect $tb.sheet 'Accounts' 'template sheet'; Expect ($tb.rows[0] -join '|') ($accountHeader -join '|') 'template header'; Expect $tb.rows.Count 1 'template rows'
    Pass 'template 200, sheet Accounts, 8 header'

    Write-Output '== Lỗi cấp tệp / levelId (400, không ghi gì)'
    foreach ($bad in 1, 2, 5) {
        $r = Upload (Book 'ok.xlsx' $good) $adminToken $bad
        Expect $r.status 400 "levelId=$bad"; if ((Codes $r) -notmatch 'LEVEL_ID') { throw "levelId=$bad codes: $(Codes $r)" }
    }
    Expect (Upload (Book 'ok.xlsx' $good) $adminToken $null).status 400 'levelId missing'
    $cases = @(
        @('công thức trong ô', (Book 'formula.xlsx' $good -formula)),
        @('sai tên sheet', (Book 'sheet.xlsx' $good -sheetName 'Questions')),
        @('sai tiêu đề', (Book 'header.xlsx' $good -header $exportHeader[0..7])),
        @('501 dòng dữ liệu', (Book 'rows.xlsx' (1..501 | ForEach-Object { , @("P4 N $_", '2001-02-03', "p4a$_-x$tag@example.invalid", '', 'HN', '1', "p4ax$_$tag", $pw) }))),
        @('tệp > 2 MiB', (Book 'big.xlsx' $good -padBytes (2080 * 1024)))
    )
    foreach ($case in $cases) { Expect (Upload $case[1] $adminToken 4).status 400 $case[0]; Pass "$($case[0]): 400" }
    Expect (& $created) 0 'nothing written after file errors'

    Write-Output '== Lỗi từng cột và trùng trong file (400, không ghi dở dang)'
    $r = Upload (Book 'cols.xlsx' @(, @('', '31/12/2000', 'bad', '123', '', '2', 'ab', 'abc'))) $adminToken 4
    Expect $r.status 400 'column errors'
    foreach ($expected in '2:FULL_NAME', '2:DATE_OF_BIRTH', '2:EMAIL', '2:PHONE', '2:ADDRESS', '2:SEX', '2:USER_NAME', '2:PASSWORD') {
        if ((Codes $r) -notmatch [regex]::Escape($expected)) { throw "Missing $expected in $(Codes $r)" }
    }
    if (@($r.body.data.errors | Where-Object { $_.message -match "abc" }).Count -gt 0) { throw "Password value echoed in message" }
    $dupFile = Upload (Book 'dup.xlsx' @(
        @('P4 D1', '2001-02-03', "p4a3$tag@example.invalid", '', 'HN', '1', "p4a3$tag", $pw),
        @('P4 D2', '2001-02-03', "P4A3$tag@example.invalid", '', 'HN', '1', "p4a3x$tag", $pw),
        @('P4 D3', '2001-02-03', "p4a4$tag@example.invalid", '', 'HN', '1', "P4A3$tag", $pw))) $adminToken 4
    Expect $dupFile.status 400 'dup in file'
    foreach ($expected in '3:DUPLICATE_FILE', '4:DUPLICATE_FILE') { if ((Codes $dupFile) -notmatch $expected) { throw "Missing $expected in $(Codes $dupFile)" } }
    $mixed = Upload (Book 'mixed.xlsx' @($good[0], @('P4 Bad', '2001-02-03', "p4a5$tag@example.invalid", '', 'HN', '9', "p4a5$tag", $pw), $good[1])) $adminToken 4
    Expect $mixed.status 400 'one bad row among good rows'
    Expect (& $created) 0 'atomic: good rows not written when another row fails'
    $blank = Upload (Book 'blank.xlsx' @($good[0], @('', '', '', '', '', '', '', ''), $good[1])) $adminToken 4
    Expect $blank.status 400 'blank row'; if ((Codes $blank) -notmatch '3:EMPTY_ROW') { throw "blank codes: $(Codes $blank)" }
    Pass 'đúng row:code cho 8 cột, DUPLICATE_FILE, EMPTY_ROW; 0 user mới được ghi'

    Write-Output '== Nhập hợp lệ'
    $ok = Upload (Book 'valid.xlsx' $good) $adminToken 4
    Expect $ok.status 200 'valid import'; Expect $ok.body.data.imported 2 'imported'; Expect $ok.body.data.emailFailed 0 'emailFailed'
    if (($ok.body | ConvertTo-Json -Depth 6) -match '\$2[aby]\$|passwordHash|token') { throw 'Import response leaked hash/token' }
    Expect (Sql "SELECT COUNT(*) FROM dbo.Users WHERE UserName LIKE N'p4a%$tag' AND LevelId=4 AND Status=1 AND EmailVerifiedAt IS NULL AND PasswordHash LIKE N'`$2%';") '2' 'SQL users (level, unverified, bcrypt)'
    Expect (Sql "SELECT COUNT(*) FROM dbo.EmailVerificationTokens t JOIN dbo.Users u ON u.Id=t.UserId WHERE u.UserName LIKE N'p4a%$tag' AND t.ConsumedAt IS NULL;") '2' 'verification tokens'
    Expect (Sql "SELECT CONCAT(CONVERT(varchar(10),DateOfBirth,23),'/',Sex,'/',ISNULL(PhoneNumber,'NULL'),'/',Email) FROM dbo.Users WHERE UserName=N'p4a2$tag';") "2000-01-01/0/NULL/p4a2$tag@example.invalid" 'row 2 (serial 36526, phone empty, email lowercased)'
    Expect (Sql "SELECT CONVERT(varchar(10),DateOfBirth,23) FROM dbo.Users WHERE UserName=N'p4a1$tag';") '2001-02-03' 'row 1 date'
    Expect (Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'account.import' AND ActorUserId='$admin' AND CreatedAtUtc > DATEADD(minute,-5,SYSUTCDATETIME());") '1' 'import audit'
    foreach ($name in "p4a1$tag@example.invalid", "p4a2$tag@example.invalid") { $mails += (FindMail $name) }
    if ($mails | Where-Object { -not $_ }) { throw 'Verification mails were not delivered to maildrop' }
    Expect (Auth 'login' @{ keyword = "p4a1$tag@example.invalid"; password = $pw }).status 401 'login before verification'
    Expect (Auth 'verify-email' @{ token = (MailToken $mails[0]) }).status 200 'verify imported account'
    Expect (Auth 'login' @{ keyword = "p4a1$tag@example.invalid"; password = $pw }).status 200 'login after verification'
    Pass 'import 200: SQL đúng level/bcrypt/EmailVerifiedAt NULL/2 token; login 401 rồi 200 sau xác minh; audit 1 dòng'

    Write-Output '== Xung đột DB (409)'
    $r = Upload (Book 'dupdb.xlsx' @(, @('P4 X', '2001-02-03', "P4A1$tag@EXAMPLE.invalid", '', 'HN', '1', "p4a6$tag", $pw))) $adminToken 4
    Expect $r.status 409 'email duplicate (different case)'; if ((Codes $r) -notmatch '2:DUPLICATE_DB') { throw "dup db codes: $(Codes $r)" }
    $r = Upload (Book 'dupdb2.xlsx' @(@('P4 Y', '2001-02-03', "p4a7$tag@example.invalid", '', 'HN', '1', "p4a6$tag", $pw), @('P4 Z', '2001-02-03', "p4a8$tag@example.invalid", '', 'HN', '1', "p4a1$tag", $pw))) $adminToken 4
    Expect $r.status 409 'username duplicate'
    Expect (& $created) 2 'conflict wrote nothing'
    Pass 'trùng email (khác hoa thường) / username với DB: 409 DUPLICATE_DB, không ghi lẻ'

    Write-Output '== Export'
    $e = Call 'GET' '/web/users/excel/export' $adminToken
    Expect $e.status 200 'export all'; $eb = ReadBook $e.bytes
    Expect $eb.sheet 'Accounts' 'export sheet'; Expect ($eb.rows[0] -join '|') ($exportHeader -join '|') 'export header'
    if ($eb.all -match 'PasswordHash|\$2[aby]\$' -or $eb.rows[0] -contains 'Password') { throw 'Export leaked password material' }
    Expect ($eb.rows.Count - 1) ([int](Sql 'SELECT COUNT(*) FROM dbo.Users;')) 'export rows vs SQL (all levels)'
    $e4 = Call 'GET' '/web/users/excel/export?levelId=4' $adminToken
    $e4b = ReadBook $e4.bytes
    Expect ($e4b.rows.Count - 1) ([int](Sql 'SELECT COUNT(*) FROM dbo.Users WHERE LevelId=4;')) 'export rows vs SQL (level 4)'
    $mine = @($e4b.rows | Where-Object { $_[6] -eq "p4a1$tag" })
    Expect $mine.Count 1 'imported student present'; Expect ($mine[0][7]) '4' 'LevelId cell'; Expect ($mine[0][9]) '1' 'EmailVerified cell (verified in this run)'
    Expect (Call 'GET' '/web/users/excel/export?levelId=9' $adminToken).status 400 'levelId=9'
    Expect (Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'account.export' AND ActorUserId='$admin';") '2' 'export audit rows'
    Pass 'export 200: 10 header, không có mật khẩu/hash, số dòng khớp SQL, levelId=9 → 400, audit 2 dòng'
    Write-Output '== Vòng sửa 1: trùng DB với user chèn bằng SQL (email lưu hoa/thường khác file), trùng phone, 3 lỗi một dòng, DUPLICATE_FILE, 500 dòng'
    $collation = Sql "SELECT STRING_AGG(CONCAT(name,'=',collation_name),';') FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Users') AND name IN ('Email','UserName','PhoneNumber');"
    Write-Output "  collation: $collation"
    $dbPhone = '08' + ([string]([Convert]::ToUInt32($tag, 16) % 1000000000)).PadLeft(9, '0')
    $preId = [Guid]::NewGuid()
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PhoneNumber,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$preId',N'P4 Pre',N'P4AQpre$tag',N'P4Aq-Pre$tag@Example.Invalid','$dbPhone',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    Expect (Sql "SELECT Email FROM dbo.Users WHERE Id='$preId';") "P4Aq-Pre$tag@Example.Invalid" 'fixture email stored with mixed case'
    $before = [int](Sql "SELECT COUNT(*) FROM dbo.Users;")
    function ColCodes($result) { (@($result.body.data.errors) | ForEach-Object { "$($_.row):$($_.column):$($_.code)" }) -join ',' }
    # (a) email có sẵn trong DB nhưng khác hoa/thường -> DUPLICATE_DB đúng dòng 3 (dòng 2 hợp lệ), không user mới
    $r = Upload (Book 'a.xlsx' @(@('P4 Fresh', '2001-02-03', "p4aq0$tag@example.invalid", '', 'HN', '1', "p4aq0$tag", $pw), @('P4 Dup', '2001-02-03', "p4aq-pre$tag@example.invalid", '', 'HN', '1', "p4aq1$tag", $pw))) $adminToken 4
    Expect $r.status 409 '(a) email case-different vs DB'; Expect (ColCodes $r) '3:C:DUPLICATE_DB' '(a) codes'
    Expect ([int](Sql "SELECT COUNT(*) FROM dbo.Users;")) $before '(a) no new user'
    Pass '(a) email khác hoa/thường với DB: 409 3:C:DUPLICATE_DB, 0 user mới'
    # (b) trùng phone với DB
    $r = Upload (Book 'b.xlsx' @(, @('P4 Dup', '2001-02-03', "p4aq2$tag@example.invalid", $dbPhone, 'HN', '1', "p4aq2$tag", $pw))) $adminToken 4
    Expect $r.status 409 '(b) phone vs DB'; Expect (ColCodes $r) '2:D:DUPLICATE_DB' '(b) codes'
    Expect ([int](Sql "SELECT COUNT(*) FROM dbo.Users;")) $before '(b) no new user'
    Pass '(b) trùng phone với DB: 409 2:D:DUPLICATE_DB, 0 user mới'
    # (c) một dòng trùng cả email + username (khác hoa/thường) + phone -> 3 lỗi cột C, G, D
    $r = Upload (Book 'c.xlsx' @(, @('P4 Dup', '2001-02-03', "p4aq-pre$tag@example.invalid", $dbPhone, 'HN', '1', "p4aqpre$tag", $pw))) $adminToken 4
    Expect $r.status 409 '(c) triple'
    $got = (@($r.body.data.errors) | ForEach-Object { "$($_.row):$($_.column):$($_.code)" } | Sort-Object) -join ','
    Expect $got '2:C:DUPLICATE_DB,2:D:DUPLICATE_DB,2:G:DUPLICATE_DB' '(c) codes'
    Expect ([int](Sql "SELECT COUNT(*) FROM dbo.Users;")) $before '(c) no new user'
    Pass '(c) trùng cả 3 trường: đúng 3 lỗi DUPLICATE_DB ở C, G, D'
    # (d) trùng trong file (kèm một dòng cũng trùng DB) -> DUPLICATE_FILE (400), không DUPLICATE_DB
    $r = Upload (Book 'd.xlsx' @(
        @('P4 F1', '2001-02-03', "p4aq3$tag@example.invalid", '', 'HN', '1', "p4aq3$tag", $pw),
        @('P4 F2', '2001-02-03', "P4AQ3$tag@example.invalid", '', 'HN', '1', "p4aq4$tag", $pw),
        @('P4 F3', '2001-02-03', "p4aq-pre$tag@example.invalid", '', 'HN', '1', "p4aq5$tag", $pw))) $adminToken 4
    Expect $r.status 400 '(d) dup in file'; $dc = ColCodes $r
    if ($dc -notmatch '3:C:DUPLICATE_FILE') { throw "(d) missing DUPLICATE_FILE: $dc" }
    if ($dc -match 'DUPLICATE_DB') { throw "(d) unexpected DUPLICATE_DB: $dc" }
    Expect ([int](Sql "SELECT COUNT(*) FROM dbo.Users;")) $before '(d) no new user'
    Pass '(d) trùng trong file: 400 DUPLICATE_FILE (không phải DUPLICATE_DB), 0 user mới'
    # (e) 500 dòng hợp lệ: thành công, đo thời gian thực
    $bulk = 1..500 | ForEach-Object { , @("P4 Bulk $_", '2001-02-03', "p4ab$_-$tag@example.invalid", '', 'HN', ($_ % 2).ToString(), "p4ab$_$tag", $pw) }
    $bulkPath = Book 'bulk.xlsx' $bulk
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $r = Upload $bulkPath $adminToken 4
    $sw.Stop()
    Expect $r.status 200 '(e) 500 rows'; Expect $r.body.data.imported 500 '(e) imported'
    Expect (Sql "SELECT COUNT(*) FROM dbo.Users WHERE UserName LIKE N'p4ab%$tag' AND LevelId=4 AND EmailVerifiedAt IS NULL AND PasswordHash LIKE N'`$2%';") '500' '(e) SQL 500 users'
    Expect (Sql "SELECT COUNT(*) FROM dbo.EmailVerificationTokens t JOIN dbo.Users u ON u.Id=t.UserId WHERE u.UserName LIKE N'p4ab%$tag';") '500' '(e) 500 tokens'
    Write-Output ("  IMPORT_500_ROWS_MS={0} emailFailed={1}" -f $sw.ElapsedMilliseconds, $r.body.data.emailFailed)
    Pass "(e) import 500 dòng 200 trong $($sw.ElapsedMilliseconds) ms; SQL 500 user + 500 token"
    Sql "DELETE dbo.EmailVerificationTokens WHERE UserId IN (SELECT Id FROM dbo.Users WHERE UserName LIKE N'p4ab%$tag');
      DELETE dbo.Users WHERE UserName LIKE N'p4ab%$tag';" | Out-Null
    Expect (Sql "SELECT COUNT(*) FROM dbo.Users WHERE UserName LIKE N'p4ab%$tag';") '0' '(e) bulk cleanup'
    Write-Output "P4_ACCOUNT_EXCEL_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    $users = "'$admin','$teacher','$seed'"
    Sql "DELETE dbo.Logs WHERE ActorUserId IN ($users);
      DELETE dbo.RefreshTokens WHERE UserId IN (SELECT Id FROM dbo.Users WHERE UserName LIKE N'p4a%$tag');
      DELETE dbo.EmailVerificationTokens WHERE UserId IN (SELECT Id FROM dbo.Users WHERE UserName LIKE N'p4a%$tag');
      DELETE dbo.Users WHERE UserName LIKE N'p4a%$tag';
      DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N'p4qseed$tag@example.invalid';" | Out-Null
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' -ErrorAction SilentlyContinue | Where-Object { (Get-Content $_.FullName -Raw) -match "To: p4a[a-z0-9-]*$tag@" } | Remove-Item -ErrorAction SilentlyContinue
    foreach ($m in @($mail) + $mails) { if ($m) { Remove-Item -LiteralPath $m.FullName -ErrorAction SilentlyContinue } }
}
