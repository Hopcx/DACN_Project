param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Giới hạn tần suất /web/auth/refresh (SessionRateLimits) với cấu hình mặc định:
# 10/phút cho mỗi refresh cookie, 20/phút/IP khi không có cookie, trần 300/phút/IP cho refresh+logout.
# 1) 25 phiên khác nhau từ cùng một IP (mô phỏng phòng thi chung NAT) đều refresh được 200 — trước đây lần thứ 21 bị 429.
# 2) Một phiên dùng lại cùng cookie: 10 lần đầu bị từ chối 401 (token đã xoay vòng), lần 11 là 429.
# 3) Không có cookie: tối đa 20 lần, sau đó 429.
# 4) Cookie ngẫu nhiên dồn dập từ một IP chạm trần và nhận 429.
# Chạy từ thư mục DACN_Project khi API trỏ DB bản sao; mọi refresh token kiểm thử được xóa trong finally.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51221, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
function Hash([string]$raw) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($raw))) }
function NewRaw { [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_') }

$tag = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$userId = [Guid]::NewGuid()
# CSRF: lấy token và cookie csrf một lần, gắn thủ công vào từng request cùng refresh cookie riêng.
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
$csrfCookies = (# cookie csrf có Path=/web/auth
    $session.Cookies.GetCookies([Uri]"$BaseUrl/web/auth/") | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '; '
function Refresh([string]$raw) {
    $cookie = if ($raw) { "$csrfCookies; __Secure-dacn-refresh=$raw" } else { $csrfCookies }
    $r = Invoke-WebRequest "$BaseUrl/web/auth/refresh" -Method Post -SkipCertificateCheck -SkipHttpErrorCheck `
        -Headers @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf; Cookie = $cookie } -ContentType 'application/json' -Body '{}'
    return [int]$r.StatusCode
}
# Bắt đầu ở đầu một cửa sổ phút mới để các bộ đếm cố định không bị cắt ngang giữa chừng.
function FreshWindow { $wait = 61 - [DateTime]::UtcNow.Second; Start-Sleep -Seconds $wait }

try {
    $hash = Sql 'SELECT TOP 1 PasswordHash FROM dbo.Users WHERE Status=1 ORDER BY Id;'
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      VALUES ('$userId',N'Rate QA $tag',N'rateqa$tag',N'rateqa$tag@example.invalid',N'$hash',4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1);" | Out-Null
    $tokens = 1..27 | ForEach-Object { NewRaw }
    $values = ($tokens | ForEach-Object { "('$userId',N'$(Hash $_)',DATEADD(day,1,SYSUTCDATETIME()),SYSUTCDATETIME(),0,NEWID())" }) -join ','
    Sql "INSERT dbo.RefreshTokens(UserId,TokenHash,ExpiryDate,CreatedAt,IsRevoked,FamilyId) VALUES $values;" | Out-Null

    FreshWindow
    $statuses = $tokens[0..24] | ForEach-Object { Refresh $_ }
    Expect ((@($statuses | Where-Object { $_ -eq 200 })).Count) 25 '25 sessions from one IP'
    Pass '25 phiên khác nhau chung một IP đều refresh 200 (cách cũ: lần 21 bị 429)'

    # Token chưa dùng: lần 1 thành công (rồi bị xoay vòng), 9 lần sau 401, lần 11 vượt hạn mức của phiên.
    $same = 1..11 | ForEach-Object { Refresh $tokens[25] }
    Expect $same[0] 200 'first use of the session token'
    Expect (($same[1..9] | Sort-Object -Unique) -join ',') '401' 'reused rotated token rejected'
    Expect $same[10] 429 '11th request with the same cookie'
    Pass 'một phiên tự lặp: lần 1 200, 9 lần 401 (token đã xoay vòng), lần 11 bị 429'
    # Phiên khác vẫn không bị ảnh hưởng bởi phiên bị chặn.
    Expect (Refresh $tokens[26]) 200 'other session unaffected'
    Pass 'phiên khác vẫn refresh được ngay sau đó'

    FreshWindow
    $none = 1..21 | ForEach-Object { Refresh '' }
    Expect (($none[0..19] | Sort-Object -Unique) -join ',') '401' 'no-cookie within limit'
    Expect $none[20] 429 '21st no-cookie request'
    Pass 'không có cookie: 20 lần 401, lần 21 bị 429 (giữ như cũ)'

    FreshWindow
    $count = 0; $limited = $false
    while ($count -lt 320 -and -not $limited) { $count++; if ((Refresh (NewRaw)) -eq 429) { $limited = $true } }
    if (-not $limited -or $count -lt 300) { throw "IP ceiling not enforced as expected (first 429 at $count)" }
    Pass "cookie ngẫu nhiên dồn dập: request thứ $count từ cùng IP bị 429 (trần 300/phút)"
    Write-Output "SESSION_RATE_LIMIT_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    Sql "DELETE dbo.RefreshTokens WHERE UserId='$userId'; DELETE dbo.Users WHERE Id='$userId';" | Out-Null
}
