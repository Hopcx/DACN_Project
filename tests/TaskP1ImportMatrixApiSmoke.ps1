param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# P1/A02 — ma trận lỗi import câu hỏi Excel v1 và tính nguyên tử (không ghi dở dang).
# Chạy từ thư mục gốc DACN_Project khi API trỏ DB bản sao. Fixture/tệp tạm được dọn trong finally.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51191, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-p1-import-$tag"; New-Item -ItemType Directory $work | Out-Null
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
$S = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
$defaultHeader = @('SubjectId', 'QuestionLevelId', 'QuestionTypeId', 'Content', 'Answers', 'CorrectIndexes')
function Cell([string]$ref, $value) {
    if ($value -is [int]) { return "<c r=`"$ref`"><v>$value</v></c>" }
    $escaped = [Security.SecurityElement]::Escape([string]$value)
    return "<c r=`"$ref`" t=`"inlineStr`"><is><t>$escaped</t></is></c>"
}
# rows: mảng các mảng giá trị cột A..F. Tùy chọn: header, sheetName, formula (thêm công thức vào A2), padBytes.
function Book([string]$name, [object[]]$rows, [string[]]$header = $defaultHeader, [string]$sheetName = 'Questions', [switch]$formula, [int]$padBytes = 0) {
    $path = Join-Path $work $name
    $xmlRows = New-Object Text.StringBuilder
    $cols = 'ABCDEF'
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
                # Dữ liệu ngẫu nhiên không nén được, chỉ để vượt giới hạn dung lượng tệp.
                $stream = $zip.CreateEntry('docProps/padding.bin', [IO.Compression.CompressionLevel]::NoCompression).Open()
                try { $stream.Write([Security.Cryptography.RandomNumberGenerator]::GetBytes($padBytes)) } finally { $stream.Dispose() }
            }
        } finally { $zip.Dispose() }
    } finally { $file.Dispose() }
    return $path
}
function Upload([string]$path, [string]$token) {
    $r = Invoke-WebRequest "$BaseUrl/web/questions/import" -Method Post -Headers @{ Authorization = "Bearer $token" } -Form @{ file = Get-Item -LiteralPath $path } -SkipCertificateCheck -SkipHttpErrorCheck
    return @{ status = [int]$r.StatusCode; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
function Codes($result) { (@($result.body.data.errors) | ForEach-Object { "$($_.row):$($_.code)" }) -join ',' }
$imported = { [int](Sql "SELECT COUNT(*) FROM dbo.Questions WHERE Content LIKE N'P1 import $tag%';") }

$admin = [Guid]::NewGuid(); $levelId = 0; $mail = $null; $seedUser = [Guid]::Empty
try {
    $email = "p1imp$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'P1 Import'; userName = "p1imp$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $seed = Auth 'login' @{ keyword = $email; password = $password }
    $seedUser = [Guid]$seed.body.data.userId; $studentToken = [string]$seed.body.data.accessToken
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$admin',N'P1 import admin',N'p1impadmin$tag',N'p1impadmin$tag@example.invalid',PasswordHash,1,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seedUser';" | Out-Null
    $token = [string](Auth 'login' @{ keyword = "p1impadmin$tag@example.invalid"; password = $password }).body.data.accessToken
    $levelId = [int](Sql "INSERT dbo.QuestionLevels(Name,Description,Status) VALUES (N'P1 import $tag',N'QA',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    $subject = [int](Sql 'SELECT TOP 1 Id FROM dbo.Subjects WHERE Status=1 ORDER BY Id;')
    $ok = @($subject, $levelId, 2, "P1 import $tag hợp lệ", 'A;B;C', '2')

    Write-Output '== A02 Lỗi cấp tệp (400, không ghi gì)'
    Expect (Upload (Book 'ok.xlsx' @(, $ok)) $studentToken).status 403 'Student import'
    $cases = @(
        @('công thức trong ô', (Book 'formula.xlsx' @(, $ok) -formula)),
        @('thiếu cột tiêu đề', (Book 'header.xlsx' @(, $ok[0..4]) -header $defaultHeader[0..4])),
        @('sai tên sheet', (Book 'sheet.xlsx' @(, $ok) -sheetName 'Sheet1')),
        @('501 dòng dữ liệu', (Book 'rows.xlsx' (1..501 | ForEach-Object { , @($subject, $levelId, 2, "P1 import $tag dòng $_", 'A;B', '1') }))),
        # Lớn hơn 2 MiB nhưng dưới RequestSizeLimit (2 MiB + 64 KiB) để đi tới kiểm tra của controller.
        @('tệp > 2 MiB', (Book 'big.xlsx' @(, $ok) -padBytes (2080 * 1024)))
    )
    foreach ($case in $cases) {
        $r = Upload $case[1] $token
        Expect $r.status 400 $case[0]
        Pass "$($case[0]): 400 $((Codes $r) -replace '^:?$', 'FILE_SIZE')"
    }
    $csv = Join-Path $work 'wrong.csv'; Copy-Item (Book 'csvsource.xlsx' @(, $ok)) $csv
    Expect (Upload $csv $token).status 400 'wrong extension'
    Pass 'đuôi tệp không phải .xlsx: 400'
    Expect (& $imported) 0 'nothing written after file errors'

    Write-Output '== A02 Lỗi theo dòng (400, báo đúng dòng/mã)'
    $rowErrors = Upload (Book 'rows-bad.xlsx' @(
        @($subject, $levelId, 7, "P1 import $tag loại sai", 'A;B', '1'),
        @($subject, $levelId, 2, "P1 import $tag chỉ số sai", 'A;B', '5'),
        @($subject, $levelId, 2, "P1 import $tag trùng", 'A;B', '1'),
        @($subject, $levelId, 2, "P1 import $tag trùng", 'A;B', '1'),
        @($subject, $levelId, 2, "P1 import $tag một đáp án", 'A', '1'),
        @($subject, $levelId, 2, "P1 import $tag hai đáp án đúng", 'A;B;C', '1,2'),
        @($subject, $levelId, 2, '', 'A;B', '1'),
        $ok)) $token
    Expect $rowErrors.status 400 'row-level errors'
    $codes = Codes $rowErrors
    foreach ($expected in '2:TYPE_ID', '3:CORRECT_INDEXES', '5:DUPLICATE_FILE', '6:ANSWERS', '7:INVALID_ANSWERS', '8:CONTENT') {
        if ($codes -notmatch [regex]::Escape($expected)) { throw "Missing $expected in $codes" }
    }
    Expect $rowErrors.body.data.imported 0 'imported 0'
    Expect (& $imported) 0 'valid row 9 not written when others fail'
    Pass "báo lỗi đúng dòng ($codes); dòng hợp lệ cũng không được ghi"

    Write-Output '== A02 Xung đột dữ liệu (409, nguyên tử)'
    $r = Upload (Book 'subject.xlsx' @(, @(999999, $levelId, 2, "P1 import $tag môn lạ", 'A;B', '1'))) $token
    Expect $r.status 409 'unknown subject'; if ((Codes $r) -notmatch 'SUBJECT_NOT_FOUND') { throw "subject code: $(Codes $r)" }
    $r = Upload (Book 'level.xlsx' @(, @($subject, 999999, 2, "P1 import $tag mức lạ", 'A;B', '1'))) $token
    Expect $r.status 409 'unknown level'; if ((Codes $r) -notmatch 'LEVEL_NOT_FOUND') { throw "level code: $(Codes $r)" }
    Pass 'môn/mức độ không tồn tại: 409 SUBJECT_NOT_FOUND / LEVEL_NOT_FOUND'
    Expect (Upload (Book 'seed.xlsx' @(, $ok)) $token).status 200 'seed one question'
    $r = Upload (Book 'atomic.xlsx' @(
        @($subject, $levelId, 1, "P1 import $tag đúng sai", 'Đúng;Sai', '1'),
        @($subject, $levelId, 3, "P1 import $tag nhiều đáp án", 'A;B;C;D', '1,3'),
        $ok)) $token
    Expect $r.status 409 'duplicate with bank'; if ((Codes $r) -notmatch '4:DUPLICATE_DB') { throw "dup code: $(Codes $r)" }
    Expect (& $imported) 1 'no partial write on conflict'
    Pass 'một dòng trùng ngân hàng → 409, hai dòng hợp lệ khác không bị ghi lẻ'

    Write-Output '== A02 Nhập hợp lệ nhiều loại'
    $r = Upload (Book 'valid.xlsx' @(
        @($subject, $levelId, 1, "P1 import $tag đúng sai", 'Đúng;Sai', '1'),
        @($subject, $levelId, 3, "P1 import $tag nhiều đáp án", 'A;B;C;D', '1,3'),
        @($subject, $levelId, 2, "P1 import $tag một đáp án đúng", 'A;B;C', '3'))) $token
    Expect $r.status 200 'valid mixed import'; Expect $r.body.data.imported 3 'imported count'
    $agg = Sql "SELECT CONCAT(COUNT(DISTINCT q.Id),',',COUNT(a.Id),',',SUM(CASE WHEN a.IsCorrect=1 THEN 1 ELSE 0 END)) FROM dbo.Questions q JOIN dbo.Answers a ON a.QuestionId=q.Id WHERE q.Content LIKE N'P1 import $tag%';"
    Expect $agg '4,12,5' 'SQL aggregate (4 câu, 12 đáp án, 5 đúng)'
    Pass "loại 1/2/3 nhập 200; SQL $agg"
    Write-Output "P1_IMPORT_MATRIX_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    Sql "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.Content LIKE N'P1 import $tag%'; DELETE dbo.Questions WHERE Content LIKE N'P1 import $tag%';" | Out-Null
    if ($levelId) { Sql "DELETE dbo.QuestionLevels WHERE Id=$levelId;" | Out-Null }
    $users = "'$admin','$seedUser'"
    Sql "DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users);
      DELETE dbo.Users WHERE Id IN ($users) OR Email=N'p1imp$tag@example.invalid';" | Out-Null
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
