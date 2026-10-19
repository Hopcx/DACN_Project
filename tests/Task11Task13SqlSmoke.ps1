param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255', [switch]$Browser)
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named database copy is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51101, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL fixture failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Connection preflight failed.' }
if ((Sql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='20261018063239_Task10TimeoutAndDuration';") -ne '1') { throw 'Task10 migration absent.' }
if ((Sql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='20261019090000_Task11AttemptVoidAudit';") -ne '1') { throw 'Task11 void migration absent.' }
$tag=[Guid]::NewGuid().ToString('N').Substring(0,10)
$email="reportqa$tag@example.invalid"
$password='Qa7'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf=(Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
function Auth([string]$route,$body) {
    $args=@{Uri="$BaseUrl/web/auth/$route";Method='Post';WebSession=$session;SkipCertificateCheck=$true;SkipHttpErrorCheck=$true;
        Headers=@{Origin='https://localhost:5173';'X-CSRF-TOKEN'=$csrf};ContentType='application/json';Body=($body | ConvertTo-Json -Depth 6 -Compress)}
    for ($retry=0; $retry -lt 5; $retry++) {
        $response=Invoke-WebRequest @args
        if ([int]$response.StatusCode -ne 429) { return @{status=[int]$response.StatusCode;body=($response.Content | ConvertFrom-Json)} }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
function Get([string]$url,[string]$token) {
    $response=Invoke-WebRequest "$BaseUrl$url" -Method Get -Headers @{Authorization="Bearer $token"} -SkipCertificateCheck -SkipHttpErrorCheck
    return @{status=[int]$response.StatusCode;body=($response.Content | ConvertFrom-Json)}
}
function Post([string]$url,[string]$token,$body) {
    $response=Invoke-WebRequest "$BaseUrl$url" -Method Post -Headers @{Authorization="Bearer $token"} -SkipCertificateCheck -SkipHttpErrorCheck -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)
    return @{status=[int]$response.StatusCode;body=($response.Content | ConvertFrom-Json)}
}
$student=[Guid]::Empty;$other=[Guid]::NewGuid();$admin=[Guid]::NewGuid();$exam=0;$class=0;$class2=0
try {
    $register=Auth 'register' @{fullName='Report QA';userName="reportqa$tag";email=$email;password=$password;address='QA';dateOfBirth='2000-01-01T00:00:00Z';sex=$true}
    if ($register.status -ne 202) { throw "Register HTTP $($register.status)" }
    $mail=Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    if (-not $mail) { throw 'Verification mail absent.' }
    $match=[regex]::Match((Get-Content $mail.FullName -Raw),'#token=([^\s]+)')
    if (-not $match.Success -or (Auth 'verify-email' @{token=[Uri]::UnescapeDataString($match.Groups[1].Value)}).status -ne 200) { throw 'Verify failed.' }
    $login=Auth 'login' @{keyword=$email;password=$password}
    if ($login.status -ne 200) { throw 'Student login failed.' }
    $student=[Guid]$login.body.data.userId;$studentToken=[string]$login.body.data.accessToken
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$other',N'Other report QA',N'other$tag',N'other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';
      INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$admin',N'Admin report QA',N'admin$tag',N'admin$tag@example.invalid',PasswordHash,1,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';" | Out-Null
    $otherToken=[string](Auth 'login' @{keyword="other$tag@example.invalid";password=$password}).body.data.accessToken
    $adminToken=[string](Auth 'login' @{keyword="admin$tag@example.invalid";password=$password}).body.data.accessToken
    if (-not $otherToken -or -not $adminToken) { throw 'Fixture login failed.' }
    $ids=Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'Report QA $tag',@subject,1,0,1,10,5,60,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy)
VALUES(@exam,N'QA',1,SYSUTCDATETIME(),'$student',SYSUTCDATETIME(),'$student');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES(N'Report QA $tag',N'RP-$tag',4,'$student',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES(N'Report second $tag',N'RS-$tag',4,'$student',@subject,1);
DECLARE @class2 int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,'$student',1),(@class,'$other',1);
DECLARE @now datetime2=SYSUTCDATETIME();
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc)
VALUES(@exam,N'Report one',DATEADD(minute,-1,@now),DATEADD(hour,1,@now),1,1),
(@exam,N'Report two',DATEADD(minute,-1,@now),DATEADD(hour,1,@now),1,1),
(@exam,N'Report three',DATEADD(minute,-1,@now),DATEADD(hour,1,@now),1,1),
(@exam,N'Report four',DATEADD(minute,-1,@now),DATEADD(hour,1,@now),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class2,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
DECLARE @s1 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'Report one');
DECLARE @s2 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'Report two');
DECLARE @s3 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'Report three');
DECLARE @s4 int=(SELECT Id FROM dbo.ExamSchedules WHERE ExamId=@exam AND Title=N'Report four');
DECLARE @a1 uniqueidentifier=NEWID(),@a2 uniqueidentifier=NEWID(),@a3 uniqueidentifier=NEWID(),@a4 uniqueidentifier=NEWID(),@a5 uniqueidentifier=NEWID();
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
VALUES(@a1,'$student',@s1,@exam,@variant,DATEADD(minute,-5,@now),DATEADD(hour,1,@now),@now,N'submitted',0,N'{}',N'[]',5,1),
(@a2,'$student',@s2,@exam,@variant,DATEADD(minute,-5,@now),DATEADD(hour,1,@now),@now,N'submitted',0,N'{}',N'[]',5,0),
(@a3,'$student',@s3,@exam,@variant,DATEADD(minute,-5,@now),DATEADD(hour,1,@now),@now,N'timedOut',0,N'{}',N'[]',5,1),
(@a4,'$student',@s4,@exam,@variant,@now,DATEADD(hour,1,@now),NULL,N'inProgress',0,N'{}',N'[]',5,1),
(@a5,'$other',@s1,@exam,@variant,DATEADD(minute,-5,@now),DATEADD(hour,1,@now),@now,N'submitted',0,N'{}',N'[]',5,1);
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson)
VALUES(@a1,@now,300,8,1,N'[]'),(@a2,@now,300,6,1,N'[]'),(@a3,@now,300,2,0,N'[]'),(@a5,@now,300,4,0,N'[]');
SELECT CONCAT(@exam,',',@class,',',@class2,',',@a1,',',@a2,',',@a5);
"@
    $parts=$ids.Split(',');$exam=[int]$parts[0];$class=[int]$parts[1];$class2=[int]$parts[2];$visibleId=$parts[3];$hiddenId=$parts[4];$otherId=$parts[5]
    $mine=Get '/web/attempts/mine?page=1&pageSize=2' $studentToken
    if ($mine.status -ne 200 -or $mine.body.data.totalCount -ne 4 -or @($mine.body.data.items).Count -ne 2) { throw 'Student pagination failed.' }
    $allMine=Get '/web/attempts/mine?page=1&pageSize=10' $studentToken
    if (@($allMine.body.data.items).Count -ne 4 -or @($allMine.body.data.items | Where-Object attemptId -EQ $otherId).Count -ne 0) { throw 'Student ownership failed.' }
    $hidden=$allMine.body.data.items | Where-Object attemptId -EQ $hiddenId
    if ($null -ne $hidden.result -or ($hidden | ConvertTo-Json -Depth 8) -match 'totalMark|isPassed|answersJson') { throw 'Hidden list leaked result.' }
    $detail=Get "/web/attempts/$hiddenId" $studentToken
    if ($detail.status -ne 200 -or $null -ne $detail.body.data.result) { throw 'Hidden detail leaked result.' }
    if ((Get "/web/attempts/$otherId" $studentToken).status -ne 404) { throw 'Other student detail accessible.' }
    if ((Get "/web/attempts/$otherId/review" $studentToken).status -ne 404) { throw 'Other student review accessible.' }
    if ((Get "/web/attempts/$visibleId/review" $studentToken).status -ne 403) { throw 'Review before schedule end was released.' }
    if ((Get "/web/attempts/$hiddenId/review" $studentToken).status -ne 403) { throw 'Hidden review was released.' }
    if ((Get "/web/attempts/$visibleId/review" $adminToken).status -ne 403) { throw 'Admin can read personal review.' }
    if ((Get '/web/admin/attempt-reports' $studentToken).status -ne 403) { throw 'Student can read admin report.' }
    $report=Get "/web/admin/attempt-reports?page=1&pageSize=2&examId=$exam&classId=$class" $adminToken
    $s=$report.body.data.summary
    if ($report.status -ne 200 -or $report.body.data.totalCount -ne 5 -or @($report.body.data.items).Count -ne 2 -or
        $s.finalizedCount -ne 4 -or $s.inProgressCount -ne 1 -or $s.timeoutCount -ne 1 -or
        $s.hiddenResultCount -ne 1 -or $s.passedCount -ne 2 -or $s.passPercent -ne 50 -or $s.averageScore -ne 5) { throw "Report aggregates failed: $($s | ConvertTo-Json -Compress)" }
    $second=Get "/web/admin/attempt-reports?page=2&pageSize=2&examId=$exam&classId=$class" $adminToken
    if ($second.status -ne 200 -or @($second.body.data.items).Count -ne 2) { throw 'Admin pagination failed.' }
    $subject=[int](Sql "SELECT SubjectId FROM dbo.Exams WHERE Id=$exam;")
    $filtered=Get "/web/admin/attempt-reports?examId=$exam&classId=$class2&subjectId=$subject" $adminToken
    if ($filtered.body.data.totalCount -ne 5 -or @($filtered.body.data.distribution).Count -ne 4) { throw 'Class/subject/distribution failed.' }
    $options=Get '/web/admin/attempt-reports/options' $adminToken
    if ($options.status -ne 200 -or @($options.body.data.exams | Where-Object id -EQ $exam).Count -ne 1) { throw 'Report filter options failed.' }
    $sqlCounts=Sql "SELECT CONCAT(COUNT(*),',',SUM(CASE WHEN State='inProgress' THEN 1 ELSE 0 END),',',
      (SELECT COUNT(*) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam)))
      FROM dbo.ExamAttempts WHERE ExamId=$exam;"
    if ($sqlCounts -ne '5,1,4') { throw "SQL mismatch: $sqlCounts" }
    if ($Browser) {
        $env:DACN_QA_EMAIL="admin$tag@example.invalid";$env:DACN_QA_PASSWORD=$password;$env:DACN_QA_TAG=$tag;$env:DACN_QA_ATTEMPT_ID=$visibleId
        try {
            & node ../DACN_FE/tests/adminReportBrowserSmoke.mjs
            if ($LASTEXITCODE -ne 0) { throw 'Admin browser smoke failed.' }
        } finally {
            Remove-Item Env:DACN_QA_EMAIL,Env:DACN_QA_PASSWORD,Env:DACN_QA_TAG,Env:DACN_QA_ATTEMPT_ID -ErrorAction SilentlyContinue
        }
    } else {
        if ((Post "/web/admin/attempts/$visibleId/void" $studentToken @{reason='QA audit reason'}).status -ne 403) { throw 'Student could void result.' }
        if ((Post "/web/admin/attempts/$visibleId/void" $adminToken @{reason=' '}).status -ne 400) { throw 'Blank void reason accepted.' }
        if ((Post "/web/admin/attempts/$visibleId/void" $adminToken @{reason='QA audit reason'}).status -ne 200) { throw 'Admin void failed.' }
    }
    $auditBefore=Sql "SELECT CONCAT(CONVERT(varchar(33),VoidedAtUtc,126),',',VoidedByUserId,',',VoidReason,',',TotalMark,',',IsPassed) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$visibleId';"
    if (-not $auditBefore.ToLowerInvariant().Contains("$admin,QA audit reason,8.00,1".ToLowerInvariant())) { throw "Void audit mismatch: $auditBefore" }
    if ((Post "/web/admin/attempts/$visibleId/void" $adminToken @{reason='QA audit reason'}).status -ne 200) { throw 'Repeated void failed.' }
    if ((Post "/web/admin/attempts/$visibleId/void" $adminToken @{reason='Different reason'}).status -ne 409) { throw 'Different repeat reason accepted.' }
    $auditAfter=Sql "SELECT CONCAT(CONVERT(varchar(33),VoidedAtUtc,126),',',VoidedByUserId,',',VoidReason,',',TotalMark,',',IsPassed) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$visibleId';"
    if ($auditBefore -ne $auditAfter) { throw 'Repeated void changed audit or score.' }
    $voidedDetail=Get "/web/attempts/$visibleId" $studentToken
    if ($voidedDetail.body.data.state -ne 'voided' -or $null -ne $voidedDetail.body.data.result) { throw 'Voided detail leaked result.' }
    if ((Get "/web/attempts/$visibleId/review" $studentToken).status -ne 403) { throw 'Voided review was released.' }
    $voidedList=Get '/web/attempts/mine?state=voided' $studentToken
    if ($voidedList.body.data.totalCount -ne 1 -or $voidedList.body.data.items[0].state -ne 'voided' -or $null -ne $voidedList.body.data.items[0].result) { throw 'Voided history failed.' }
    $afterReport=Get "/web/admin/attempt-reports?examId=$exam&state=voided" $adminToken
    if ($afterReport.body.data.summary.totalAttempts -ne 1 -or $afterReport.body.data.summary.voidedCount -ne 1 -or $afterReport.body.data.summary.finalizedCount -ne 0) { throw 'Voided filter failed.' }
    $fullAfter=Get "/web/admin/attempt-reports?examId=$exam" $adminToken
    $sum=$fullAfter.body.data.summary
    if ($sum.totalAttempts -ne 5 -or $sum.voidedCount -ne 1 -or $sum.finalizedCount -ne 3 -or $sum.passedCount -ne 1 -or $sum.averageScore -ne 4 -or $sum.passPercent -ne 33.33) { throw "Post-void report mismatch: $($sum | ConvertTo-Json -Compress)" }
    if ((Sql "SELECT CONCAT(COUNT(*),',',SUM(CASE WHEN VoidedAtUtc IS NOT NULL THEN 1 ELSE 0 END)) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);") -ne '4,1') { throw 'SQL void count mismatch.' }
    Write-Output 'PASS Task11 review authorization, void audit/idempotence, hidden result, history and report after void.'
    Write-Output "PASS Task11/13 API-SQL: student 4/other 1, hidden list/detail, owner 404, student admin 403; report total=5 finalized=4 active=1 timeout=1 hidden=1 pass=2 avg=5 rate=50; distribution=4; class/exam/subject and paging."
}
finally {
    if ($exam -gt 0) { Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam);
      DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ExamDetails WHERE ExamId=$exam;
      DELETE dbo.Exams WHERE Id=$exam; DELETE dbo.ClassUsers WHERE ClassId IN ($class,$class2);
      DELETE dbo.Classes WHERE Id IN ($class,$class2);" | Out-Null }
    if ($student -ne [Guid]::Empty) { Sql "DELETE dbo.RefreshTokens WHERE UserId IN ('$student','$other','$admin');
      DELETE dbo.Users WHERE Id IN ('$student','$other','$admin');" | Out-Null }
    if ($mail) { Remove-Item -LiteralPath $mail.FullName }
}
