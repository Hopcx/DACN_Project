param(
    [string]$Database = 'ProjectDACN_Task04Task09_Test',
    [string]$BaseUrl = 'https://localhost:7242',
    [string]$FrontendUrl = 'https://localhost:5173/',
    [int]$PauseSeconds = 60,
    [string[]]$Only = @(),
    [string]$LogDir = ''
)
# G7 - bộ UAT tự động: chạy lần lượt các smoke hiện có (mỗi smoke tự tạo và dọn fixture theo tag), in bảng tổng kết.
# Cần: API (Development) trỏ DB bản sao ở $BaseUrl, Vite HTTPS ở https://localhost:5173 (các smoke trình duyệt cố định cổng này;
# $FrontendUrl chỉ dùng cho bước kiểm trước), sqlcmd, node, Chrome.
# Không tự khởi động API/Vite. Chạy từ thư mục gốc DACN_Project. Exit code 1 nếu có smoke FAIL.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Các smoke chỉ được ghi vào bản sao ProjectDACN_Task04Task09_Test.' }
if (-not (Test-Path 'tests/RunUatSuite.ps1')) { throw 'Hãy chạy từ thư mục gốc DACN_Project.' }

# Thứ tự: API trước, trình duyệt sau, giới hạn tần suất cuối cùng (vì làm đầy bộ đếm 429).
$suite = @(
    @{ Name = 'TaskAdminPermissionApiSmoke';   Browser = $false; Marker = 'ADMIN_PERMISSION_SMOKE' },
    @{ Name = 'TaskP1AcceptanceApiSmoke';      Browser = $false; Marker = 'P1_ACCEPTANCE_SMOKE' },
    @{ Name = 'TaskP1ImportMatrixApiSmoke';    Browser = $false; Marker = 'P1_IMPORT_MATRIX_SMOKE' },
    @{ Name = 'TaskP2DashboardApiSmoke';       Browser = $false; Marker = 'P2_DASHBOARD_SMOKE' },
    @{ Name = 'TaskP3StudentFlowSmoke';        Browser = $false; Marker = 'P3_STUDENT_FLOW_SMOKE' },
    @{ Name = 'TaskP4AccountExcelApiSmoke';    Browser = $false; Marker = 'P4_ACCOUNT_EXCEL_SMOKE' },
    @{ Name = 'TaskP4AuditApiSmoke';           Browser = $false; Marker = 'P4_AUDIT_SMOKE' },
    @{ Name = 'TaskP4QuestionExportApiSmoke';  Browser = $false; Marker = 'P4_QUESTION_EXPORT_SMOKE' },
    @{ Name = 'TaskP4TeacherOverviewApiSmoke'; Browser = $false; Marker = 'P4_TEACHER_OVERVIEW_SMOKE' },
    @{ Name = 'TaskToBeT2QuestionMetadataApiSmoke'; Browser = $false; Marker = 'TOBE_T2_QUESTION_METADATA_SMOKE' },
    @{ Name = 'TaskToBeT4SnapshotExplanationApiSmoke'; Browser = $false; Marker = 'TOBE_T4_SNAPSHOT_EXPLANATION_SMOKE' },
    @{ Name = 'TaskToBeT5PracticeApiSmoke'; Browser = $false; Marker = 'TOBE_T5_PRACTICE_SMOKE' },
    @{ Name = 'TaskToBeT6PracticeCheckApiSmoke'; Browser = $false; Marker = 'TOBE_T6_PRACTICE_CHECK_SMOKE' },
    @{ Name = 'TaskToBeT8ReviewFlagsApiSmoke'; Browser = $false; Marker = 'TOBE_T8_REVIEW_FLAGS_SMOKE' },
    @{ Name = 'TaskToBeT9MockExamApiSmoke'; Browser = $false; Marker = 'TOBE_T9_MOCK_EXAM_SMOKE' },
    @{ Name = 'TaskToBeT10ClassRankingApiSmoke'; Browser = $false; Marker = 'TOBE_T10_CLASS_RANKING_SMOKE' },
    @{ Name = 'TaskToBeT11StudentAnalysisApiSmoke'; Browser = $false; Marker = 'TOBE_T11_STUDENT_ANALYSIS_SMOKE' },
    @{ Name = 'TaskP1RoleRegressionSmoke';     Browser = $true;  Marker = 'P1_ROLE_REGRESSION_SMOKE' },
    @{ Name = 'TaskP4FrontendSmoke';           Browser = $true;  Marker = 'P4_FRONTEND_SMOKE' },
    @{ Name = 'TaskToBeT7PracticeFrontendSmoke'; Browser = $true; Marker = 'TOBE_T7_PRACTICE_FRONTEND_SMOKE' },
    @{ Name = 'TaskToBeT10RankingFrontendSmoke'; Browser = $true; Marker = 'TOBE_T10_RANKING_FRONTEND_SMOKE' },
    @{ Name = 'TaskToBeT11AnalysisFrontendSmoke'; Browser = $true; Marker = 'TOBE_T11_ANALYSIS_FRONTEND_SMOKE' },
    @{ Name = 'TaskSessionRateLimitApiSmoke';  Browser = $false; Marker = 'SESSION_RATE_LIMIT_SMOKE' }
)
if ($Only.Count -gt 0) { $suite = @($suite | Where-Object { $Only -contains $_.Name }) }
if ($suite.Count -eq 0) { throw 'Không có smoke nào khớp -Only.' }

# Kiểm trước: API sẵn sàng (health ready), Vite trả lời nếu có smoke trình duyệt.
try { $ready = Invoke-WebRequest "$BaseUrl/web/health/ready" -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 15 }
catch { throw "Không gọi được API tại $BaseUrl ($($_.Exception.Message))." }
if ([int]$ready.StatusCode -ne 200) { throw "API chưa sẵn sàng: /web/health/ready trả $([int]$ready.StatusCode)." }
if ($suite | Where-Object Browser) {
    try { $fe = Invoke-WebRequest $FrontendUrl -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 15 }
    catch { throw "Không gọi được Vite tại $FrontendUrl ($($_.Exception.Message))." }
    if ([int]$fe.StatusCode -ne 200) { throw "Vite trả $([int]$fe.StatusCode)." }
}

if (-not $LogDir) { $LogDir = Join-Path ([IO.Path]::GetTempPath()) ("dacn-uat-" + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$pwsh = (Get-Process -Id $PID).Path
$results = @()
for ($i = 0; $i -lt $suite.Count; $i++) {
    $item = $suite[$i]
    $script = "tests/$($item.Name).ps1"
    $log = Join-Path $LogDir "$($item.Name).log"
    if ($i -gt 0) { Write-Host "Nghỉ $PauseSeconds giây trước $($item.Name) (giới hạn đăng nhập)..."; Start-Sleep -Seconds $PauseSeconds }
    Write-Host "== $($item.Name)"
    $started = Get-Date
    if (-not (Test-Path $script)) {
        $results += [pscustomobject]@{ Smoke = $item.Name; KetQua = 'THIẾU'; Giay = 0; DongKetLuan = 'Không có file script' }
        continue
    }
    # Mỗi smoke chạy trong tiến trình riêng: throw/exit của nó không dừng cả bộ.
    & $pwsh -NoProfile -File $script -Database $Database -BaseUrl $BaseUrl *> $log
    $code = $LASTEXITCODE
    $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
    $line = ($text -split "`r?`n" | Where-Object { $_ -match "^$($item.Marker)=" } | Select-Object -Last 1)
    $pass = ($code -eq 0) -and $line -and $line.StartsWith("$($item.Marker)=PASS")
    if (-not $line) { $line = ($text -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 1) }
    $results += [pscustomobject]@{
        Smoke = $item.Name; KetQua = $(if ($pass) { 'PASS' } else { 'FAIL' })
        Giay = [int]((Get-Date) - $started).TotalSeconds; DongKetLuan = "$line".Trim()
    }
}

$results | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host
$failed = @($results | Where-Object KetQua -ne 'PASS').Count
Write-Host "Log từng smoke: $LogDir"
Write-Output "UAT_SUITE=$(if ($failed -eq 0) { 'PASS' } else { 'FAIL' });TOTAL=$($results.Count);FAILED=$failed;DB=$Database"
if ($failed -gt 0) { exit 1 }
