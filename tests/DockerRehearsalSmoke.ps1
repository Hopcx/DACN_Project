# T20: smoke cho bản triển khai Docker khi DIỄN TẬP trên máy dev (compose + docker-compose.rehearsal.yml, Mailpit).
# Đi qua Caddy (HTTPS) như người dùng thật. Không dùng trên VPS (cần Mailpit và quyền sa trong container).
#   pwsh -File tests/DockerRehearsalSmoke.ps1            (chạy từ thư mục gốc DACN_Project, sau deploy.sh --no-pull)
param([string]$BaseUrl = 'https://localhost:8443', [string]$MailpitUrl = 'http://127.0.0.1:8025', [string]$DeployDir = 'deploy')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
$DeployDir = (Resolve-Path $DeployDir).Path
$origin = ([Uri]$BaseUrl).GetLeftPart([UriPartial]::Authority)
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected '$expected', got '$actual'" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
function Compose([string[]]$arguments) {
    Push-Location $DeployDir
    try { $output = & docker compose --env-file .env @arguments 2>&1; if ($LASTEXITCODE -ne 0) { throw "docker compose $($arguments -join ' '): $output" }; return $output }
    finally { Pop-Location }
}
# T-SQL bằng sa bên trong container SQL (mật khẩu lấy từ biến môi trường của container)
function Sql([string]$query) {
    Push-Location $DeployDir
    try {
        $output = "SET NOCOUNT ON; USE [ProjectDACN]; $query" | & docker compose --env-file .env exec -T sql bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -I -h -1 -W -f 65001 -i /dev/stdin'
        if ($LASTEXITCODE -ne 0) { throw "SQL failed: $output" }
        return ($output | Out-String).Trim()
    } finally { Pop-Location }
}
function Web([string]$method, [string]$path, [hashtable]$headers = @{}, $body = $null, $session = $null, $form = $null) {
    $a = @{ Uri = "$BaseUrl$path"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true; Headers = $headers; MaximumRedirection = 0 }
    if ($session) { $a.WebSession = $session }
    if ($null -ne $body) { $a.ContentType = 'application/json'; $a.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Compress -Depth 6)) }
    if ($null -ne $form) { $a.Form = $form }
    $r = Invoke-WebRequest @a
    $type = [string]($r.Headers['Content-Type'] | Select-Object -First 1)
    $text = if ($type -match 'json|html|text') { [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) } else { '' }
    return @{ status = [int]$r.StatusCode; type = $type; text = $text; headers = $r.Headers; json = if ($type -match 'json' -and $text) { $text | ConvertFrom-Json } else { $null } }
}

Write-Output '== Proxy, HTTPS, header, SPA'
Expect (Web 'GET' '/web/health/live').status 200 'live qua Caddy'
$ready = Web 'GET' '/web/health/ready'
Expect $ready.status 200 'ready qua Caddy (API ↔ SQL bằng login dacn_app)'
Expect ([string]$ready.headers['X-Content-Type-Options']) 'nosniff' 'nosniff'
if (-not $ready.headers['Strict-Transport-Security']) { throw 'Thiếu Strict-Transport-Security' }
if ($ready.headers['Server']) { throw "Lộ header Server: $($ready.headers['Server'])" }
$indexPage = Web 'GET' '/'
if ($indexPage.status -ne 200 -or $indexPage.text -notmatch 'id="root"') { throw "Trang chủ FE: $($indexPage.status)" }
$deep = Web 'GET' '/student/practice'
if ($deep.status -ne 200 -or $deep.text -notmatch 'id="root"') { throw 'SPA fallback không trả index.html' }
$apiMiss = Web 'GET' '/web/khong-ton-tai'
if ($apiMiss.type -match 'html') { throw "/web/* không được rơi vào index.html (nhận $($apiMiss.status) $($apiMiss.type))" }
$httpPort = (Compose @('port','web','80')).ToString().Split(':')[-1]
$plainCode = [int](& curl.exe -s -o NUL -w '%{http_code}' "http://localhost:$httpPort/")
if ($plainCode -notin 301, 302, 307, 308) { throw "HTTP không chuyển sang HTTPS: $plainCode" }
Pass 'health 200; nosniff, HSTS, không lộ Server; SPA fallback; /web/* không ra index.html; HTTP → HTTPS'

Write-Output '== Đăng ký → email thật qua SMTP (Mailpit) → xác minh → Admin'
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Web 'GET' '/web/auth/csrf' @{} $null $session).json.data.csrfToken
$auth = @{ Origin = $origin; 'X-CSRF-TOKEN' = $csrf }
$email = "rh$tag@example.invalid"
$reg = Web 'POST' '/web/auth/register' $auth @{ fullName = 'Rehearsal Admin'; userName = "rh$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true } $session
Expect $reg.status 202 'register'
$message = $null
for ($i = 0; $i -lt 20 -and -not $message; $i++) {
    $found = Invoke-RestMethod "$MailpitUrl/api/v1/search?query=$([Uri]::EscapeDataString("to:$email"))"
    if ($found.messages.Count -gt 0) { $message = Invoke-RestMethod "$MailpitUrl/api/v1/message/$($found.messages[0].ID)" } else { Start-Sleep -Seconds 1 }
}
if (-not $message) { throw 'Không nhận được email xác minh ở Mailpit' }
$link = [regex]::Match($message.Text, 'https://\S+#token=\S+').Value
if (-not $link.StartsWith("$origin/auth/verify-email#token=")) { throw "Link xác minh sai origin: $link" }
$token = [Uri]::UnescapeDataString($link.Split('#token=')[1])
Expect (Web 'POST' '/web/auth/verify-email' $auth @{ token = $token } $session).status 200 'verify-email'
Sql "UPDATE dbo.Users SET LevelId = 1 WHERE Email = N'$email';" | Out-Null
$login = Web 'POST' '/web/auth/login' $auth @{ keyword = $email; password = $password } $session
Expect $login.status 200 'login Admin'
$bearer = @{ Authorization = "Bearer $($login.json.data.accessToken)" }
if (-not ($session.Cookies.GetCookies([Uri]"$BaseUrl/web/auth/refresh") | Where-Object Name -eq '__Secure-dacn-refresh')) { throw 'Không nhận cookie refresh __Secure- (scheme https qua proxy?)' }
Pass 'email xác minh gửi qua SMTP với link đúng origin; cookie __Secure- được đặt sau proxy'

Write-Output '== Media lưu ở volume, phục vụ qua proxy'
$png = Join-Path ([IO.Path]::GetTempPath()) "rh$tag.png"
$bytes = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0,0,13,0x49,0x48,0x44,0x52,0,0,0,40,0,0,0,30) + [Security.Cryptography.RandomNumberGenerator]::GetBytes(64)
[IO.File]::WriteAllBytes($png, $bytes)
$up = Web 'POST' '/web/media' $bearer $null $null @{ file = Get-Item $png }
if ($up.status -notin 200, 201) { throw "upload $($up.status) $($up.text)" }
$mediaId = $up.json.data.id; $mediaUrl = $up.json.data.url
if (-not $mediaUrl) { throw 'Upload không trả url' }
$path = ([Uri]::new([Uri]$BaseUrl, $mediaUrl)).PathAndQuery
$get = Invoke-WebRequest "$BaseUrl$path" -SkipCertificateCheck -SkipHttpErrorCheck
Expect "$([int]$get.StatusCode)|$($get.Headers['Content-Type'])|$($get.RawContentLength)" "200|image/png|$($bytes.Length)" 'xem media qua proxy'
$n = $mediaId.Replace('-', '').ToLowerInvariant()
Compose @('exec', '-T', 'api', 'test', '-s', "/var/lib/dacn/media/$($n.Substring(0, 2))/$n") | Out-Null
Pass 'upload → file nằm trong volume media → tải lại qua HTTPS đúng kiểu/kích thước'

Write-Output '== Tạo lại container API (như khi deploy): phiên đăng nhập, dữ liệu, media còn'
# Review T20 N1: tạo lại hẳn container (không chỉ restart) — khóa DataProtection phải còn trong volume dp-keys,
# nên cookie refresh + CSRF token cũ (FE giữ trong bộ nhớ) vẫn dùng được, người dùng không bị đăng xuất.
Compose @('up', '-d', '--force-recreate', '--no-deps', 'api') | Out-Null
for ($i = 0; $i -lt 40; $i++) { if ((Web 'GET' '/web/health/ready').status -eq 200) { break }; Start-Sleep -Seconds 3 }
Expect (Web 'GET' '/web/health/ready').status 200 'ready sau khi tạo lại container'
Expect ([int](Invoke-WebRequest "$BaseUrl$path" -SkipCertificateCheck -SkipHttpErrorCheck).StatusCode) 200 'media sau khi tạo lại container'
$refresh = Web 'POST' '/web/auth/refresh' @{ Origin = $origin; 'X-CSRF-TOKEN' = $csrf } $null $session
Expect $refresh.status 200 'refresh bằng cookie + CSRF token cũ sau khi tạo lại container (khóa DataProtection bền)'
$csrf2 = (Web 'GET' '/web/auth/csrf' @{} $null $session).json.data.csrfToken
Expect (Web 'POST' '/web/auth/login' @{ Origin = $origin; 'X-CSRF-TOKEN' = $csrf2 } @{ keyword = $email; password = $password } $session).status 200 'login sau khi tạo lại container'
Pass 'tạo lại container API: phiên đăng nhập (refresh) không mất; tài khoản, media còn nguyên'

Write-Output '== IP giả trong X-Forwarded-For bị bỏ qua (rate limit theo IP thật)'
$codes = @()
foreach ($i in 1..7) {
    $h = @{ Origin = $origin; 'X-CSRF-TOKEN' = $csrf2; 'X-Forwarded-For' = "198.51.100.$i" }
    $codes += (Web 'POST' '/web/auth/login' $h @{ keyword = "nobody$tag$i@example.invalid"; password = 'x' } $session).status
}
if ($codes -notcontains 429) { throw "Đổi X-Forwarded-For mà không bị 429: $($codes -join ',') — proxy đang tin header của client" }
Pass "7 lần đăng nhập sai với IP giả khác nhau → $($codes -join ',') (có 429)"
Remove-Item $png -ErrorAction SilentlyContinue
Write-Output "DOCKER_REHEARSAL_SMOKE=PASS;TAG=$tag"
