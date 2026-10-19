param([string]$BaseUrl='https://localhost:7255')
$ErrorActionPreference='Stop'
$db='ProjectDACN_Task04Task09_Test'
$source='ProjectDACN_LegacyFixture_Test'
function Sql([string]$query) {
    $out=sqlcmd -S . -d $db -E -No -C -b -I -f 65001 -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME()<>N'$db' THROW 51220,'Wrong DB',1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($out -join ' ')" }
    return ($out | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME()') -ne $db) { throw 'Target DB_NAME mismatch' }
if ((Sql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='20261019090000_Task12LegacyProjection'") -ne '1') { throw 'Task12 migration absent' }
$sourceCheck=sqlcmd -S . -d $source -E -No -C -b -I -f 65001 -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME()<>N'$source' THROW 51221,'Wrong source',1; SELECT COUNT(*) FROM dbo.__EFMigrationsHistory"
if ($LASTEXITCODE -ne 0 -or ($sourceCheck | Out-String).Trim() -ne '1') { throw 'Source preflight failed' }
$tag=[Guid]::NewGuid().ToString('N').Substring(0,10)
$batch=[Guid]::NewGuid()
$a=[Guid]::Empty;$b=[Guid]::NewGuid();$admin=[Guid]::NewGuid()
$password='Qa8'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$email="legacyqa$tag@example.invalid"
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf=(Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
function Auth([string]$route,$body) {
    $response=Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{Origin='https://localhost:5173';'X-CSRF-TOKEN'=$csrf} -ContentType 'application/json' -Body ($body|ConvertTo-Json -Compress)
    return @{ status=[int]$response.StatusCode; body=($response.Content|ConvertFrom-Json) }
}
function Get([string]$path,[string]$token) {
    $response=Invoke-WebRequest "$BaseUrl$path" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{Authorization="Bearer $token"}
    return @{ status=[int]$response.StatusCode; body=($response.Content|ConvertFrom-Json) }
}
function Import([string]$mode) {
    $output=sqlcmd -S . -d $db -E -No -C -b -I -f 65001 -W -i tests/Task12LegacyFixtureImport.sql -v "Mode=$mode" "BatchId=$batch" "TargetUserA=$a" "TargetUserB=$b"
    if ($LASTEXITCODE -ne 0) { throw "Import $mode failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
try {
    $registered=Auth 'register' @{fullName='Legacy QA A';userName="legacyqa$tag";email=$email;password=$password;address='QA';dateOfBirth='2000-01-01T00:00:00Z';sex=$true}
    if ($registered.status -ne 202) { throw "Register failed $($registered.status)" }
    $mail=Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    if (-not $mail) { throw 'QA verification mail absent' }
    $match=[regex]::Match((Get-Content $mail.FullName -Raw),'#token=([^\s]+)')
    if (-not $match.Success -or (Auth 'verify-email' @{token=[Uri]::UnescapeDataString($match.Groups[1].Value)}).status -ne 200) { throw 'QA verify failed' }
    $loginA=Auth 'login' @{keyword=$email;password=$password}
    if ($loginA.status -ne 200) { throw 'QA login A failed' }
    $a=[Guid]$loginA.body.data.userId;$tokenA=[string]$loginA.body.data.accessToken
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$b',N'Legacy QA B',N'legacyqb$tag',N'legacyqb$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$a';
      INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$admin',N'Legacy QA Admin',N'legacyadmin$tag',N'legacyadmin$tag@example.invalid',PasswordHash,1,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$a';" | Out-Null
    $loginB=Auth 'login' @{keyword="legacyqb$tag@example.invalid";password=$password}
    $loginAdmin=Auth 'login' @{keyword="legacyadmin$tag@example.invalid";password=$password}
    if ($loginB.status -ne 200 -or $loginAdmin.status -ne 200) { throw 'QA login B/Admin failed' }
    $tokenB=[string]$loginB.body.data.accessToken;$tokenAdmin=[string]$loginAdmin.body.data.accessToken
    Write-Output "PREFLIGHT target=$db migration=8 source=$source migration=1 sourceSubmissions=4"
    Write-Output (Import 'DRYRUN')
    if ((Sql "SELECT COUNT(*) FROM dbo.LegacyImportBatches WHERE Id='$batch'") -ne '0') { throw 'Dry-run left batch rows' }
    Write-Output (Import 'APPLY')
    $before=Sql "SELECT CONCAT(COUNT(*),',',SUM(TotalMark),',',SUM(CASE WHEN LegacyStatus=1 THEN 1 ELSE 0 END),',',SUM(CASE WHEN LegacyStatus=0 THEN 1 ELSE 0 END),',',SUM(CASE WHEN LegacyStatus IS NULL THEN 1 ELSE 0 END)) FROM dbo.LegacySubmissionRecords WHERE BatchId='$batch'"
    Write-Output (Import 'APPLY')
    $after=Sql "SELECT CONCAT(COUNT(*),',',SUM(TotalMark),',',SUM(CASE WHEN LegacyStatus=1 THEN 1 ELSE 0 END),',',SUM(CASE WHEN LegacyStatus=0 THEN 1 ELSE 0 END),',',SUM(CASE WHEN LegacyStatus IS NULL THEN 1 ELSE 0 END)) FROM dbo.LegacySubmissionRecords WHERE BatchId='$batch'"
    if ($before -ne $after -or $after -ne '4,10.02,2,1,1') { throw "Rerun changed data: $before -> $after" }
    $ids=Sql "SELECT CONCAT(MIN(CASE WHEN TargetUserId='$a' THEN Id END),',',MIN(CASE WHEN TargetUserId='$b' THEN Id END)) FROM dbo.LegacySubmissionRecords WHERE BatchId='$batch'"
    $parts=$ids.Split(',');$idA=[long]$parts[0];$idB=[long]$parts[1]
    $mineA=Get '/web/legacy-submissions/mine?page=1&pageSize=1' $tokenA
    $mineB=Get '/web/legacy-submissions/mine?page=1&pageSize=10' $tokenB
    if ($mineA.status -ne 200 -or $mineA.body.data.totalCount -ne 2 -or @($mineA.body.data.items).Count -ne 1 -or
        $mineB.status -ne 200 -or $mineB.body.data.totalCount -ne 2) { throw 'Owner list/pagination mismatch' }
    if ((Get "/web/legacy-submissions/$idA" $tokenB).status -ne 404 -or (Get "/web/legacy-submissions/$idB" $tokenA).status -ne 404) { throw 'Cross-owner read allowed' }
    if ((Get "/web/legacy-submissions/$idA/review" $tokenA).status -ne 409 -or (Get "/web/legacy-submissions/$idA/review" $tokenB).status -ne 404) { throw 'Legacy review guard failed' }
    $detail=Get "/web/legacy-submissions/$idA" $tokenA
    if ($detail.status -ne 200 -or $detail.body.data.timeZoneStatus -ne 'unknown' -or $detail.body.data.hasSnapshot -ne $false -or
        ($detail.body|ConvertTo-Json -Depth 8) -match 'isCorrect|correctAnswer') { throw 'Detail projection/response mismatch' }
    Sql "UPDATE dbo.LegacySubmissionRecords SET AllowViewResult=0 WHERE Id=$idA AND BatchId='$batch';" | Out-Null
    $hiddenDetail=Get "/web/legacy-submissions/$idA" $tokenA
    $hiddenList=Get '/web/legacy-submissions/mine?page=1&pageSize=10' $tokenA
    $hiddenItem=@($hiddenList.body.data.items | Where-Object id -EQ $idA)
    if ($hiddenDetail.status -ne 200 -or $null -ne $hiddenDetail.body.data.result -or
        $hiddenItem.Count -ne 1 -or $null -ne $hiddenItem[0].result -or
        ($hiddenDetail.body|ConvertTo-Json -Depth 8) -match 'totalMark|isPassed' -or
        ($hiddenItem[0]|ConvertTo-Json -Depth 8) -match 'totalMark|isPassed') { throw 'Hidden legacy result leaked' }
    if ((Get '/web/admin/legacy-submission-reports' $tokenA).status -ne 403) { throw 'Student accessed admin report' }
    $report=Get '/web/admin/legacy-submission-reports?page=1&pageSize=2' $tokenAdmin
    if ($report.status -ne 200 -or $report.body.data.scope -ne 'legacySubmissionsOnly' -or $report.body.data.totalCount -ne 4 -or
        @($report.body.data.items).Count -ne 2 -or $report.body.data.summary.acceptedStatusCount -ne 2 -or
        $report.body.data.summary.notRecognizedStatusCount -ne 1 -or $report.body.data.summary.pendingStatusCount -ne 1 -or
        [math]::Abs($report.body.data.summary.storedScoreSum-10.02) -gt 0.0000001) { throw 'Admin report/SQL mismatch' }
    foreach ($case in @(@('accepted',2),@('notRecognized',1),@('pending',1))) {
        $filtered=Get "/web/admin/legacy-submission-reports?status=$($case[0])" $tokenAdmin
        if ($filtered.status -ne 200 -or $filtered.body.data.totalCount -ne [int]$case[1]) { throw "Status filter $($case[0]) mismatch" }
    }
    if ((Get '/web/admin/legacy-submission-reports?status=voided' $tokenAdmin).status -ne 400) { throw 'Invalid legacy status accepted' }
    $dacn=Get '/web/admin/attempt-reports' $tokenAdmin
    if ($dacn.status -ne 200 -or $dacn.body.data.scope -ne 'dacnAttemptsOnly') { throw 'DACN report scope changed' }
    Write-Output "API PASS owner=2+2 crossOwner=404 review=409/404 hiddenResult=null studentAdmin=403 report=4 statuses=2/1/1 statusFilters=2/1/1 scoreSum=10.02 dacnScope=dacnAttemptsOnly"
    Write-Output (Import 'ROLLBACK')
    if ((Sql "SELECT COUNT(*) FROM dbo.LegacyImportBatches WHERE Id='$batch'") -ne '0' -or
        (Sql "SELECT COUNT(*) FROM dbo.LegacySubmissionRecords WHERE BatchId='$batch'") -ne '0') { throw 'Rollback failed' }
    Write-Output 'POSTFLIGHT projectionBatch=0'
} finally {
    if ($a -ne [Guid]::Empty) {
        if ((Sql "SELECT COUNT(*) FROM dbo.LegacyImportBatches WHERE Id='$batch'") -gt '0') { Import 'ROLLBACK' | Out-Null }
        # Fixture accounts are isolated and only exist on the target copy.
        Sql "DELETE FROM dbo.RefreshTokens WHERE UserId IN ('$a','$b','$admin');
             DELETE FROM dbo.EmailVerificationTokens WHERE UserId IN ('$a','$b','$admin');
             DELETE FROM dbo.Users WHERE Id IN ('$a','$b','$admin');" | Out-Null
    }
}
