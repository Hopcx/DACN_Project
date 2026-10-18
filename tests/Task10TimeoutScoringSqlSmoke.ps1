param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255')
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named database copy is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51101, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL fixture failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Connection preflight failed.' }
if ((Sql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='20261018063239_Task10TimeoutAndDuration';") -ne '1') { throw 'Task10 timeout migration absent.' }
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
$headers = @{ Origin='https://localhost:5173'; 'X-CSRF-TOKEN'=$csrf }
function Auth([string]$route, $data) {
    for ($i=0; $i -lt 5; $i++) {
        $r=Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck -Headers $headers -ContentType 'application/json' -Body ($data | ConvertTo-Json -Depth 8 -Compress)
        if ([int]$r.StatusCode -ne 429) { return @{ status=[int]$r.StatusCode; data=($r.Content | ConvertFrom-Json) } }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
$tag=[Guid]::NewGuid().ToString('N').Substring(0,10)
$email="timeoutqa$tag@example.invalid"
$password='Qa7'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$registered=Auth 'register' @{fullName='Timeout QA';userName="timeoutqa$tag";email=$email;password=$password;address='QA';dateOfBirth='2000-01-01T00:00:00Z';sex=$true}
if ($registered.status -ne 202) { throw "Register HTTP $($registered.status)" }
$mail=Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending | Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
if (-not $mail) { throw 'Verification mail absent.' }
$match=[regex]::Match((Get-Content $mail.FullName -Raw),'#token=([^\s]+)')
if (-not $match.Success -or (Auth 'verify-email' @{token=[Uri]::UnescapeDataString($match.Groups[1].Value)}).status -ne 200) { throw 'Verify failed.' }
$login=Auth 'login' @{keyword=$email;password=$password}
if ($login.status -ne 200) { throw 'Login failed.' }
$user=[Guid]$login.data.data.userId
$token=[string]$login.data.data.accessToken
function Request([string]$method,[string]$url,$body=$null) {
    $args=@{Uri="$BaseUrl$url";Method=$method;Headers=@{Authorization="Bearer $token"};SkipCertificateCheck=$true;SkipHttpErrorCheck=$true}
    if ($null -ne $body) { $args.ContentType='application/json'; $args.Body=($body | ConvertTo-Json -Depth 15 -Compress) }
    $r=Invoke-WebRequest @args
    return @{status=[int]$r.StatusCode;data=($r.Content | ConvertFrom-Json).data}
}
$exam=0;$class=0;$q1=0;$q2=0;$q3=0
try {
    $fixture=Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'Timeout QA $tag',@subject,3,1,1,4.52,4.52,30,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy)
VALUES(@exam,N'QA',1,SYSUTCDATETIME(),'$user',SYSUTCDATETIME(),'$user');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES
(N'Single A $tag',SYSUTCDATETIME(),1,@subject,2),
(N'Single B $tag',SYSUTCDATETIME(),1,@subject,2),
(N'Multi $tag',SYSUTCDATETIME(),1,@subject,3);
DECLARE @q1 int=(SELECT Id FROM dbo.Questions WHERE Content=N'Single A $tag');
DECLARE @q2 int=(SELECT Id FROM dbo.Questions WHERE Content=N'Single B $tag');
DECLARE @q3 int=(SELECT Id FROM dbo.Questions WHERE Content=N'Multi $tag');
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES
(@q1,N'Q1 yes',1,1),(@q1,N'Q1 no',0,1),
(@q2,N'Q2 yes',1,1),(@q2,N'Q2 no',0,1),
(@q3,N'Q3 yes A',1,1),(@q3,N'Q3 yes B',1,1),(@q3,N'Q3 no',0,1);
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES(@variant,@q1,1.005),(@variant,@q2,1.015),(@variant,@q3,2.5);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES(N'Timeout QA $tag',N'TO-$tag',2,'$user',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,'$user',1);
DECLARE @now datetime2=SYSUTCDATETIME();
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc)
VALUES
(@exam,N'full',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'partial',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'extra',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'blank',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'tie',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'second',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'hidden',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'broken',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'race',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
SELECT CONCAT(@exam,',',@class,',',@q1,',',@q2,',',@q3);
"@
    $parts=$fixture.Split(',');$exam=[int]$parts[0];$class=[int]$parts[1];$q1=[int]$parts[2];$q2=[int]$parts[3];$q3=[int]$parts[4]
    $scheduleRows=Sql "SELECT CONCAT(Title,':',Id) FROM dbo.ExamSchedules WHERE ExamId=$exam;"
    $schedule=@{}; foreach ($line in ($scheduleRows -split "`n")) { $p=$line.Trim().Split(':');if($p.Length -eq 2){$schedule[$p[0]]=[int]$p[1]} }
    $attempts=@{}
    foreach ($name in @('full','partial','extra','blank','tie','second','hidden','broken','race')) {
        if ($name -eq 'tie') { Sql "UPDATE dbo.Exams SET PassMark=1.004 WHERE Id=$exam;" | Out-Null }
        if ($name -eq 'second') { Sql "UPDATE dbo.Exams SET PassMark=1.016 WHERE Id=$exam;" | Out-Null }
        if ($name -eq 'hidden') { Sql "UPDATE dbo.Exams SET PassMark=4.52,AllowViewResult=0 WHERE Id=$exam;" | Out-Null }
        $response=Request 'POST' "/web/exam-schedules/$($schedule[$name])/attempts"
        if ($response.status -ne 200 -or $response.data.state -ne 'inProgress') { throw "$name start HTTP $($response.status)" }
        $attempts[$name]=$response.data
        if ($name -eq 'hidden') { Sql "UPDATE dbo.Exams SET AllowViewResult=1 WHERE Id=$exam;" | Out-Null }
    }
    foreach ($name in @('full','partial','extra','tie','second','hidden','race')) {
        $a=$attempts[$name]
        $questions=@($a.questions)
        $qA=$questions | Where-Object content -Like 'Single A*' | Select-Object -First 1
        $qB=$questions | Where-Object content -Like 'Single B*' | Select-Object -First 1
        $qM=$questions | Where-Object content -Like 'Multi*' | Select-Object -First 1
        $draft=@()
        if ($name -ne 'second') { $draft+=@{questionId=$qA.id;selectedAnswerIds=@(($qA.options | Where-Object content -Eq 'Q1 yes' | Select-Object -First 1).id)} }
        if ($name -notin @('tie')) { $draft+=@{questionId=$qB.id;selectedAnswerIds=@(($qB.options | Where-Object content -Eq 'Q2 yes' | Select-Object -First 1).id)} }
        $multi=@()
        if ($name -in @('full','hidden','race')) { $multi=@($qM.options | Where-Object content -Like 'Q3 yes*' | ForEach-Object id) }
        if ($name -eq 'partial') { $multi=@(($qM.options | Where-Object content -Eq 'Q3 yes A' | Select-Object -First 1).id) }
        if ($name -eq 'extra') { $multi=@($qM.options | ForEach-Object id) }
        $draft+=@{questionId=$qM.id;selectedAnswerIds=$multi}
        $saved=Request 'PUT' "/web/attempts/$($a.attemptId)/answers" @{revision=0;answers=$draft}
        if ($saved.status -ne 200 -or $saved.data.revision -ne 1) { throw "$name save HTTP $($saved.status)" }
    }
    Sql "UPDATE dbo.Questions SET Content=N'Changed after start',Status=0 WHERE Id IN ($q1,$q2,$q3); UPDATE dbo.Answers SET Content=N'Changed after start',IsCorrect=0,Status=0 WHERE QuestionId IN ($q1,$q2,$q3);" | Out-Null
    $brokenId=$attempts.broken.attemptId
    Sql "UPDATE dbo.ExamAttempts SET SnapshotJson=N'not-json' WHERE Id='$brokenId';" | Out-Null
    $ids=@($attempts.Values | ForEach-Object { "'$($_.attemptId)'" }) -join ','
    Sql "UPDATE dbo.ExamAttempts SET StartedAtUtc=DATEADD(second,-65,SYSUTCDATETIME()),DeadlineUtc=DATEADD(second,-2,SYSUTCDATETIME()) WHERE Id IN ($ids);" | Out-Null
    # The first six attempts receive no HTTP request. The last is submitted while an
    # independent worker sweep competes with the API's own background sweep.
    $raceId=[string]$attempts.race.attemptId
    $raceJob=Start-ThreadJob -ArgumentList $BaseUrl,$token,$raceId -ScriptBlock {
        param($url,$jwt,$id)
        $r=Invoke-WebRequest "$url/web/attempts/$id/submit" -Method Post -Headers @{Authorization="Bearer $jwt"} -ContentType 'application/json' -Body '{"revision":1}' -SkipCertificateCheck -SkipHttpErrorCheck
        [int]$r.StatusCode
    }
    $sweepJob=Start-ThreadJob -ArgumentList (Get-Location).Path -ScriptBlock {
        param($root)
        $env:ConnectionStrings__Default='Server=.;Database=ProjectDACN_Task04Task09_Test;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
        & dotnet (Join-Path $root 'tests/WorkerSweepSmoke/bin/Debug/net8.0/WorkerSweepSmoke.dll') | Select-String 'WORKER_SWEEP_COMPLETED'
        if ($LASTEXITCODE -ne 0) { throw 'Concurrent worker sweep failed.' }
    }
    $null=@($raceJob,$sweepJob) | Wait-Job -Timeout 35
    $raceStatus=Receive-Job $raceJob
    $sweepStatus=Receive-Job $sweepJob
    Remove-Job $raceJob,$sweepJob -Force
    if ($raceStatus -ne 200 -or -not $sweepStatus) { throw "Concurrent submit/sweep failed: HTTP $raceStatus worker=$sweepStatus" }
    $due=@('full','partial','extra','blank','tie','second','hidden','race')
    $done=$false
    for ($i=0;$i -lt 25;$i++) {
        $count=[int](Sql "SELECT COUNT(*) FROM dbo.ExamAttempts WHERE Id IN ($ids) AND State='timedOut';")
        if ($count -eq $due.Count) { $done=$true;break }
        Start-Sleep -Seconds 1
    }
    if (-not $done) { throw "No-request timeout worker finalized only $count/$($due.Count)" }
    $checks=@{full='4.52,1';partial='2.02,0';extra='2.02,0';blank='0.00,0';tie='1.00,1';second='1.02,0';hidden='4.52,1';race='4.52,1'}
    foreach ($name in $due) {
        $id=$attempts[$name].attemptId
        $dbRow=Sql "SELECT CONCAT(CONVERT(varchar(32),s.TotalMark),',',CONVERT(int,s.IsPassed)) FROM dbo.ExamAttemptSubmissions s WHERE s.ExamAttemptId='$id';"
        if ($dbRow -ne $checks[$name]) { throw "$name score $dbRow expected $($checks[$name])" }
        $clock=Sql "SELECT CASE WHEN s.SubmittedAtUtc=a.DeadlineUtc AND a.FinishedAtUtc>=a.DeadlineUtc AND s.TimeTakenSeconds=FLOOR(DATEDIFF_BIG(millisecond,a.StartedAtUtc,a.DeadlineUtc)/1000.0) THEN 1 ELSE 0 END FROM dbo.ExamAttempts a JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId=a.Id WHERE a.Id='$id';"
        if ($clock -ne '1') {
            $detail=Sql "SELECT CONCAT(CONVERT(varchar(33),a.StartedAtUtc,126),'|',CONVERT(varchar(33),a.DeadlineUtc,126),'|',CONVERT(varchar(33),a.FinishedAtUtc,126),'|',CONVERT(varchar(33),s.SubmittedAtUtc,126),'|',s.TimeTakenSeconds) FROM dbo.ExamAttempts a JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId=a.Id WHERE a.Id='$id';"
            throw "$name timeout clock mismatch $detail"
        }
    }
    $rawTie=Sql "SELECT CONVERT(varchar(30),CAST(JSON_VALUE(question.value,'$.point') AS decimal(18,3))) FROM dbo.ExamAttempts a CROSS APPLY OPENJSON(a.SnapshotJson,'$.questions') question WHERE a.Id='$($attempts.tie.attemptId)' AND JSON_VALUE(question.value,'$.content') LIKE 'Single A%';"
    $rawSecond=Sql "SELECT CONVERT(varchar(30),CAST(JSON_VALUE(question.value,'$.point') AS decimal(18,3))) FROM dbo.ExamAttempts a CROSS APPLY OPENJSON(a.SnapshotJson,'$.questions') question WHERE a.Id='$($attempts.second.attemptId)' AND JSON_VALUE(question.value,'$.content') LIKE 'Single B%';"
    if($rawTie -ne '1.005' -or $rawSecond -ne '1.015' -or [Math]::Round([double]1.005,2) -ne 1.00 -or [Math]::Round([double]1.015,2) -ne 1.01){throw 'Snapshot raw/Testify double comparison changed'}
    if ((Sql "SELECT COUNT(*) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$brokenId';") -ne '0') { throw 'Malformed attempt unexpectedly finalized' }
    $hidden=Request 'GET' "/web/attempts/$($attempts.hidden.attemptId)"
    if ($hidden.status -ne 200 -or $null -ne $hidden.data.result) { throw 'AllowViewResult=false leaked result' }
    $full=Request 'GET' "/web/attempts/$($attempts.full.attemptId)"
    if ($full.status -ne 200 -or $full.data.result.totalMark -ne 4.52 -or $full.data.result.timeTakenSeconds -lt 0) { throw 'Visible result/TimeTaken incorrect' }
    $before=Sql "SELECT CONCAT(COUNT(*),':',SUM(CAST(TotalMark AS decimal(18,2))),':',SUM(TimeTakenSeconds)) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN ($ids);"
    $repeat=Request 'POST' "/web/attempts/$($attempts.full.attemptId)/submit" @{revision=1}
    if ($repeat.status -ne 200 -or $repeat.data.state -ne 'timedOut') { throw 'Repeated submit changed timeout' }
    Start-Sleep -Seconds 11
    $after=Sql "SELECT CONCAT(COUNT(*),':',SUM(CAST(TotalMark AS decimal(18,2))),':',SUM(TimeTakenSeconds)) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN ($ids);"
    if ($before -ne $after) { throw 'Repeated worker changed finalized results' }
    Write-Output "TASK10_TIMEOUT_SCORING_SQL_API=PASS tag=$tag due=$($due.Count) raw=1.005/1.015 stored=1.00/1.02 passed=1/0 testifyDouble=1.00/1.01 malformedOpen=1 fingerprint=$after"
} finally {
    if ($exam -gt 0) {
        Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam); DELETE dbo.ExamAttempts WHERE ExamId=$exam; DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ClassUsers WHERE ClassId=$class; DELETE dbo.Classes WHERE Id=$class; DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.Answers WHERE QuestionId IN ($q1,$q2,$q3); DELETE dbo.Questions WHERE Id IN ($q1,$q2,$q3); DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" | Out-Null
    }
}
