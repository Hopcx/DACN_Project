param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255', [switch]$Browser)
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51171, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL command failed: $($result -join ' ')" }
    return ($result | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Clone preflight failed.' }
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "asimport$tag@example.invalid"
$content = "As-is import $tag 2+2?"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$userId = [Guid]::Empty; $levelId = 0
$templatePath = Join-Path $PWD "tests/as-import-$tag-template.xlsx"
$validPath = Join-Path $PWD "tests/as-import-$tag-valid.xlsx"
$invalidPath = Join-Path $PWD "tests/as-import-$tag-invalid.xlsx"
$browserPath = Join-Path $PWD "tests/as-import-$tag-browser.xlsx"
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
function Expect([int]$actual, [int]$expected, [string]$case) {
    if ($actual -ne $expected) { throw "$case expected HTTP $expected, got $actual" }
}
function MakeWorkbook([string]$path, [string]$correct) {
    $sourceFile = [IO.File]::OpenRead($templatePath)
    $targetFile = [IO.File]::Create($path)
    try {
        $source = [IO.Compression.ZipArchive]::new($sourceFile,[IO.Compression.ZipArchiveMode]::Read,$true)
        $target = [IO.Compression.ZipArchive]::new($targetFile,[IO.Compression.ZipArchiveMode]::Create,$true)
        try {
            foreach ($entry in $source.Entries) {
                $reader = [IO.StreamReader]::new($entry.Open())
                try { $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
                if ($entry.FullName -eq 'xl/worksheets/sheet1.xml') {
                    $row = "<row r=`"2`"><c r=`"A2`"><v>1</v></c><c r=`"B2`"><v>$levelId</v></c><c r=`"C2`"><v>2</v></c>" +
                        "<c r=`"D2`" t=`"inlineStr`"><is><t>$content</t></is></c>" +
                        "<c r=`"E2`" t=`"inlineStr`"><is><t>3;4;5</t></is></c><c r=`"F2`"><v>$correct</v></c></row>"
                    $xml = $xml.Replace('</sheetData>', "$row</sheetData>")
                }
                $writer = [IO.StreamWriter]::new($target.CreateEntry($entry.FullName).Open(), [Text.UTF8Encoding]::new($false))
                try { $writer.Write($xml) } finally { $writer.Dispose() }
            }
        } finally { $target.Dispose(); $source.Dispose() }
    } finally { $targetFile.Dispose(); $sourceFile.Dispose() }
}
function Upload([string]$path, [string]$token) {
    $result = Invoke-WebRequest "$BaseUrl/web/questions/import" -Method Post -Headers @{ Authorization = "Bearer $token" } -Form @{ file = Get-Item -LiteralPath $path } -SkipCertificateCheck -SkipHttpErrorCheck
    return @{ status = [int]$result.StatusCode; data = ($result.Content | ConvertFrom-Json) }
}
try {
    Expect (Auth 'register' @{ fullName = 'Import QA'; userName = "asimport$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
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
    $levelId = [int](Sql "INSERT dbo.QuestionLevels(Name,Description,Status) VALUES (N'As-is QA $tag',N'QA',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    Sql "UPDATE dbo.Users SET LevelId=2 WHERE Id='$userId'; INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$userId',2);" | Out-Null
    $examinerLogin = Auth 'login' @{ keyword = $email; password = $password }
    Expect $examinerLogin.status 200 'examiner login'
    $token = [string]$examinerLogin.data.data.accessToken
    $download = Invoke-WebRequest "$BaseUrl/web/questions/import/template" -Headers @{ Authorization = "Bearer $token" } -OutFile $templatePath -SkipCertificateCheck -SkipHttpErrorCheck -PassThru
    Expect ([int]$download.StatusCode) 200 'template download'
    MakeWorkbook $validPath '2'
    MakeWorkbook $invalidPath '9'
    Expect (Upload $validPath $studentToken).status 403 'Student import'
    $invalid = Upload $invalidPath $token
    Expect $invalid.status 400 'invalid index'
    if (!$invalid.data.data.errors -or $invalid.data.data.imported -ne 0) { throw 'Invalid file error detail missing.' }
    $valid = Upload $validPath $token
    Expect $valid.status 200 'valid import'
    if ($valid.data.data.imported -ne 1) { throw 'Imported count wrong.' }
    Expect (Upload $validPath $token).status 409 'duplicate import'
    $summary = Sql "SELECT CONCAT(COUNT(*),',',SUM(CASE WHEN a.IsCorrect=1 THEN 1 ELSE 0 END)) FROM dbo.Questions q JOIN dbo.Answers a ON a.QuestionId=q.Id WHERE q.Content=N'$content';"
    if ($summary -ne '3,1') { throw "SQL aggregate mismatch: $summary" }
    if ($Browser) {
        $content = "As-is import $tag browser"
        MakeWorkbook $browserPath '2'
        $env:DACN_QA_EMAIL = $email
        $env:DACN_QA_PASSWORD = $password
        $env:DACN_QA_IMPORT_FILE = $browserPath
        $env:DACN_QA_TAG = $tag
        try {
            Push-Location ../DACN_FE
            try { & node tests/questionImportBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'Question import browser smoke failed.' } }
            finally { Pop-Location }
        } finally {
            Remove-Item Env:DACN_QA_EMAIL,Env:DACN_QA_PASSWORD,Env:DACN_QA_IMPORT_FILE,Env:DACN_QA_TAG -ErrorAction SilentlyContinue
        }
        if ((Sql "SELECT COUNT(*) FROM dbo.Questions WHERE Content=N'$content';") -ne '1') { throw 'Browser import not persisted.' }
    }
    Write-Output "QUESTION_IMPORT_API_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    Sql "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.Content LIKE N'As-is import $tag%'; DELETE FROM dbo.Questions WHERE Content LIKE N'As-is import $tag%';" | Out-Null
    if ($levelId) { Sql "DELETE FROM dbo.QuestionLevels WHERE Id=$levelId;" | Out-Null }
    if ($userId -eq [Guid]::Empty) {
        $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$email';"
        if ($found) { $userId = [Guid]$found }
    }
    if ($userId -ne [Guid]::Empty) {
        Sql "DELETE FROM dbo.UserPermissions WHERE UserId='$userId'; DELETE FROM dbo.RefreshTokens WHERE UserId='$userId'; DELETE FROM dbo.EmailVerificationTokens WHERE UserId='$userId'; DELETE FROM dbo.Users WHERE Id='$userId';" | Out-Null
    }
    foreach ($path in @($templatePath,$validPath,$invalidPath,$browserPath)) { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
