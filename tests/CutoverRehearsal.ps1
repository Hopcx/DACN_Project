param(
    [string]$Source = 'ProjectDACN',
    [string]$Target = 'ProjectDACN_CutoverRehearsal',
    [string]$Server = '.',
    [string]$ApiBaseUrl = '',
    [int]$ApiWaitSeconds = 180,
    [switch]$KeepArtifacts
)
# G7 - diễn tập cutover trên bản sao: backup chỉ đọc DB nguồn -> verify -> restore thành DB diễn tập -> áp script migration
# (từ migration cuối DB đang có) -> hậu kiểm -> (tuỳ chọn) API health -> thử rollback bằng restore -> dọn. KHÔNG ghi vào DB nguồn.
# Chạy từ thư mục gốc DACN_Project. Cần sqlcmd, dotnet-ef 8. Nếu truyền -ApiBaseUrl thì người điều phối tự khởi động API
# (Development) với ConnectionStrings__Default trỏ $Target; script chờ /web/health/ready.
$ErrorActionPreference = 'Stop'
if ($Target -eq $Source -or $Target -eq 'ProjectDACN') { throw 'DB diễn tập phải khác DB nguồn và khác ProjectDACN.' }
if ($Target -notmatch '^[A-Za-z0-9_]+$' -or $Source -notmatch '^[A-Za-z0-9_]+$') { throw 'Tên DB chỉ gồm chữ, số, gạch dưới.' }
if (-not (Test-Path 'DACN_Project.sln')) { throw 'Hãy chạy từ thư mục gốc DACN_Project.' }

function Sql([string]$db, [string]$query) {
    $output = sqlcmd -S $Server -d $db -E -No -C -b -I -W -h -1 -f 65001 -Q "SET NOCOUNT ON; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL lỗi trên ${db}: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
function Step([string]$text) { Write-Output "-- $text" }
$report = [ordered]@{}
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = Sql 'master' "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400));"
$dataDir = (Sql 'master' "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400));").TrimEnd('\')
$bak = Join-Path $backupDir "$($Source)_rehearsal_$stamp.bak"
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-cutover-$stamp"
New-Item -ItemType Directory -Force -Path $work | Out-Null
$migrationSql = Join-Path $work 'cutover.sql'
$created = $false
$failed = $false

try {
    Step "1. Kiểm nguồn $Source (chỉ đọc)"
    if ((Sql 'master' "SELECT COUNT(*) FROM sys.databases WHERE name = N'$Source';") -ne '1') { throw "Không có DB $Source." }
    if ((Sql 'master' "SELECT COUNT(*) FROM sys.databases WHERE name = N'$Target';") -ne '0') { throw "DB $Target đã tồn tại; dừng để không ghi đè." }
    $sourceHistory = Sql $Source "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;"
    $sourceCounts = Sql $Source "SELECT CONCAT((SELECT COUNT(*) FROM dbo.Users),'/',(SELECT COUNT(*) FROM dbo.Subjects));"
    $report.SourceHistory = $sourceHistory; $report.SourceUsersSubjects = $sourceCounts
    Write-Output "   history=$sourceHistory users/subjects=$sourceCounts"

    Step "2. Backup COPY_ONLY + CHECKSUM và RESTORE VERIFYONLY"
    Sql 'master' "BACKUP DATABASE [$Source] TO DISK = N'$bak' WITH COPY_ONLY, CHECKSUM, INIT, STATS = 100;" | Out-Null
    Sql 'master' "RESTORE VERIFYONLY FROM DISK = N'$bak' WITH CHECKSUM;" | Out-Null
    $report.Backup = $bak
    Write-Output "   $bak"

    Step "3. Restore thành $Target"
    $files = sqlcmd -S $Server -d master -E -No -C -b -W -h -1 -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$bak';"
    if ($LASTEXITCODE -ne 0) { throw 'RESTORE FILELISTONLY lỗi.' }
    $moves = @()
    foreach ($row in $files | Where-Object { $_.Trim() }) {
        $cols = $row.Split('|'); $logical = $cols[0].Trim(); $type = $cols[2].Trim()
        $ext = if ($type -eq 'L') { '_log.ldf' } else { '.mdf' }
        $moves += "MOVE N'$logical' TO N'$dataDir\$($Target)_$logical$ext'"
    }
    $created = $true
    Sql 'master' "RESTORE DATABASE [$Target] FROM DISK = N'$bak' WITH $($moves -join ', '), RECOVERY, STATS = 100;" | Out-Null
    if ((Sql $Target "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;") -ne $sourceHistory) { throw 'History sau restore khác nguồn.' }

    $migrationFiles = Get-ChildItem 'Project.Infrastructure/Migrations' -Filter '*.cs' | Where-Object { $_.Name -notmatch 'Designer|Snapshot' }
    $expectedLast = ($migrationFiles | Sort-Object Name | Select-Object -Last 1).BaseName
    $expectedCount = $migrationFiles.Count

    # Không dùng --idempotent: Task04AuthLifecycle có SQL thô nhắc cột [Token] (đã đổi tên TokenHash), SQL Server biên dịch
    # cả batch nên script idempotent lỗi trên DB đã qua Task04. Sinh script từ migration cuối DB đang có -> mới nhất.
    $from = Sql $Target "SELECT ISNULL((SELECT TOP 1 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC), N'0');"
    if ($from -eq $expectedLast) {
        Step "4. $Target đã ở migration mới nhất ($from): không có gì để áp"
        $report.MigrationScript = "không cần (đã ở $from)"
    } else {
        Step "4. Sinh script từ $from đến $expectedLast và áp lên $Target"
        # --no-build: API có thể đang chạy và giữ khóa DLL; hãy build solution trước khi chạy diễn tập
        $efLog = Join-Path $work 'ef-script.log'
        dotnet ef migrations script $from --no-build --project Project.Infrastructure --startup-project Project.Api -o $migrationSql *> $efLog
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $migrationSql)) { throw "dotnet ef migrations script lỗi, xem $efLog. Chạy 'dotnet build DACN_Project.sln' trước." }
        $applyLog = Join-Path $work 'apply.log'
        sqlcmd -S $Server -d $Target -E -No -C -b -I -f 65001 -i $migrationSql -o $applyLog
        if ($LASTEXITCODE -ne 0) { throw "Áp migration lỗi, xem $applyLog (giữ lại để điều tra)." }
        $report.MigrationScript = "$from -> $expectedLast"
    }

    Step "5. Hậu kiểm $Target"
    $history = Sql $Target "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;"
    $last = Sql $Target "SELECT TOP 1 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;"
    $counts = Sql $Target "SELECT CONCAT((SELECT COUNT(*) FROM dbo.Users),'/',(SELECT COUNT(*) FROM dbo.Subjects));"
    $tables = Sql $Target "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'ExamAttempts',N'ExamAttemptSubmissions',N'AttemptVisibilityEvents',N'LegacySubmissionRecords',N'RefreshTokens',N'EmailVerificationTokens');"
    $seed = Sql $Target "SELECT CONCAT((SELECT COUNT(*) FROM dbo.Levels WHERE Id BETWEEN 1 AND 4),'/',(SELECT COUNT(*) FROM dbo.Permissions WHERE Id BETWEEN 1 AND 4));"
    $report.TargetHistory = "$history (kỳ vọng $expectedCount)"; $report.TargetLast = $last
    $report.TargetUsersSubjects = $counts; $report.NewTables = "$tables/6"; $report.SeedLevelsPermissions = $seed
    Write-Output "   history=$history/$expectedCount last=$last users/subjects=$counts bảngMới=$tables/6 seed(levels/perms 1-4)=$seed"
    if ([int]$history -ne $expectedCount) { throw "Số migration $history khác $expectedCount." }
    if ($last -ne $expectedLast) { throw "Migration cuối $last khác $expectedLast." }
    if ($counts -ne $sourceCounts) { throw "Users/Subjects đổi sau migration: $sourceCounts -> $counts." }
    if ($tables -ne '6') { throw 'Thiếu bảng mới sau migration.' }
    if ($seed -ne '4/4') { Write-Output "   CẢNH BÁO: seed Levels/Permissions 1-4 chưa đủ ($seed) - ghi vào runbook, không tự chèn." }

    if ($ApiBaseUrl) {
        Step "6. API trỏ ${Target}: /web/health/ready và /web/auth/csrf"
        $deadline = (Get-Date).AddSeconds($ApiWaitSeconds); $status = 0
        while ((Get-Date) -lt $deadline) {
            try { $status = [int](Invoke-WebRequest "$ApiBaseUrl/web/health/ready" -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 10).StatusCode } catch { $status = 0 }
            if ($status -eq 200) { break }; Start-Sleep -Seconds 3
        }
        $csrf = [int](Invoke-WebRequest "$ApiBaseUrl/web/auth/csrf" -SkipCertificateCheck -SkipHttpErrorCheck).StatusCode
        $report.ApiReady = $status; $report.ApiCsrf = $csrf
        Write-Output "   ready=$status csrf=$csrf"
        if ($status -ne 200 -or $csrf -ne 200) { throw 'API trên DB diễn tập chưa sẵn sàng.' }
    } else { Step '6. Bỏ qua kiểm API (không truyền -ApiBaseUrl)' }

    Step "7. Thử rollback: restore lại $Target từ backup (WITH REPLACE)"
    # Một batch: OFFLINE cắt mọi kết nối (kể cả worker của API đang chạy) rồi RESTORE ngay, không để ai chen vào giữa.
    # RESTORE đưa DB online lại với chế độ truy cập của bản backup (MULTI_USER).
    Sql 'master' "ALTER DATABASE [$Target] SET OFFLINE WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$Target] FROM DISK = N'$bak' WITH $($moves -join ', '), REPLACE, RECOVERY;" | Out-Null
    $rolled = Sql $Target "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;"
    $report.Rollback = "history sau restore = $rolled"
    Write-Output "   history=$rolled (kỳ vọng $sourceHistory)"
    if ($rolled -ne $sourceHistory) { throw 'Rollback không đưa history về như nguồn.' }

    Step "8. Hậu kiểm nguồn $Source không đổi"
    $after = Sql $Source "SELECT CONCAT((SELECT COUNT(*) FROM dbo.__EFMigrationsHistory),'|',(SELECT COUNT(*) FROM dbo.Users),'/',(SELECT COUNT(*) FROM dbo.Subjects));"
    $report.SourceAfter = $after
    if ($after -ne "$sourceHistory|$sourceCounts") { throw "Nguồn bị đổi: $after" }
    Write-Output "CUTOVER_REHEARSAL=PASS;SOURCE=$Source;TARGET=$Target;HISTORY=$($report.TargetHistory);LAST=$($report.TargetLast)"
}
catch {
    Write-Output "CUTOVER_REHEARSAL=FAIL: $($_.Exception.Message)"
    $failed = $true
    if ($created) {
        # In lệnh xử lý tay (hữu ích khi chạy -KeepArtifacts hoặc khi dọn tự động cũng lỗi)
        Write-Output "   Lệnh xử lý tay trên master nếu cần:"
        Write-Output "     Khôi phục DB diễn tập: ALTER DATABASE [$Target] SET OFFLINE WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$Target] FROM DISK = N'$bak' WITH $($moves -join ', '), REPLACE, RECOVERY;"
        Write-Output "     Xóa DB diễn tập:      ALTER DATABASE [$Target] SET ONLINE; ALTER DATABASE [$Target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Target];"
    }
}
finally {
    $report.GetEnumerator() | ForEach-Object { Write-Output ("   {0,-22} {1}" -f $_.Key, $_.Value) }
    # Dọn không được ném lỗi đè lên lỗi gốc
    if (-not $KeepArtifacts) {
        try {
            if ($created -and (Sql 'master' "SELECT COUNT(*) FROM sys.databases WHERE name = N'$Target';") -eq '1') {
                Sql 'master' "IF DATABASEPROPERTYEX(N'$Target', 'Status') = 'OFFLINE' ALTER DATABASE [$Target] SET ONLINE; ALTER DATABASE [$Target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Target];" | Out-Null
            }
        } catch { Write-Output "   CHƯA xóa được DB ${Target}: $($_.Exception.Message)" }
        # File .bak nằm trong thư mục backup của SQL Server: tài khoản thường thường không có quyền xóa -> báo để xóa tay
        $bakLeft = $false
        try { if (Test-Path $bak -ErrorAction Stop) { Remove-Item -Force $bak -ErrorAction Stop } } catch { $bakLeft = $true }
        if (-not $failed) { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
        Write-Output "   Đã dọn DB $Target$(if (-not $failed) { ', thư mục tạm' } else { "; giữ log ở $work" })$(if ($bakLeft) { ". CHƯA xóa được file backup (thiếu quyền, cần quyền admin): $bak" } else { ', file backup' })."
    } else { Write-Output "   Giữ lại: DB $Target, $bak, $work" }
}
if ($failed) { exit 1 }
