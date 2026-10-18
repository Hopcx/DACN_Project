param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255')
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named SQL Server copy is writable.' }
function Sql([string]$query) {
    $out=sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51101, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL fixture failed: $($out -join ' ')" }
    return ($out | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Wrong target database.' }
if ((Sql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='20261018063239_Task10TimeoutAndDuration';") -ne '1') { throw 'Task10 migration missing.' }
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf=(Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
$authHeaders=@{Origin='https://localhost:5173';'X-CSRF-TOKEN'=$csrf}
function Auth([string]$route,$body) {
    for($i=0;$i -lt 5;$i++) {
        $r=Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck -Headers $authHeaders -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 8 -Compress)
        if([int]$r.StatusCode -ne 429) {return @{status=[int]$r.StatusCode;data=($r.Content | ConvertFrom-Json).data}}
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
$tag=[Guid]::NewGuid().ToString('N').Substring(0,10)
$email="deadlineqa$tag@example.invalid"
$password='Qa7'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
if((Auth 'register' @{fullName='Deadline QA';userName="deadlineqa$tag";email=$email;password=$password;address='QA';dateOfBirth='2000-01-01T00:00:00Z';sex=$true}).status -ne 202){throw 'Register failed'}
$mail=Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending | Where-Object {(Get-Content $_.FullName -Raw).Contains("To: $email")} | Select-Object -First 1
if(-not $mail){throw 'Verification mail missing'}
$match=[regex]::Match((Get-Content $mail.FullName -Raw),'#token=([^\s]+)')
if(-not $match.Success -or (Auth 'verify-email' @{token=[Uri]::UnescapeDataString($match.Groups[1].Value)}).status -ne 200){throw 'Verify failed'}
$login=Auth 'login' @{keyword=$email;password=$password}
if($login.status -ne 200){throw 'Login failed'}
$user=[Guid]$login.data.userId
$jwt=[string]$login.data.accessToken
function Request([string]$method,[string]$route,$body=$null) {
    $args=@{Uri="$BaseUrl$route";Method=$method;Headers=@{Authorization="Bearer $jwt"};SkipCertificateCheck=$true;SkipHttpErrorCheck=$true}
    if($null -ne $body){$args.ContentType='application/json';$args.Body=($body | ConvertTo-Json -Depth 10 -Compress)}
    $r=Invoke-WebRequest @args
    return @{status=[int]$r.StatusCode;data=($r.Content | ConvertFrom-Json).data}
}
$exam=0;$class=0;$question=0;$holder=$null
$marker=Join-Path $env:TEMP "dacn-task10-lock-$tag.marker"
$holderOut=Join-Path $env:TEMP "dacn-task10-lock-$tag.out"
$holderErr=Join-Path $env:TEMP "dacn-task10-lock-$tag.err"
try {
    $fixture=Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'Deadline QA $tag',@subject,1,1,1,5,3,30,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy)
VALUES(@exam,N'QA',1,SYSUTCDATETIME(),'$user',SYSUTCDATETIME(),'$user');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId)
VALUES(N'Deadline QA $tag',SYSUTCDATETIME(),1,@subject,2);
DECLARE @question int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES(@question,N'Yes',1,1),(@question,N'No',0,1);
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES(@variant,@question,5);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES(N'Deadline QA $tag',N'DL-$tag',2,'$user',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,'$user',1);
DECLARE @now datetime2=SYSUTCDATETIME();
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES
(@exam,N'before',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'after',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'race',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
SELECT CONCAT(@exam,',',@class,',',@question);
"@
    $parts=$fixture.Split(',');$exam=[int]$parts[0];$class=[int]$parts[1];$question=[int]$parts[2]
    $schedules=@{}; foreach($line in ((Sql "SELECT CONCAT(Title,':',Id) FROM dbo.ExamSchedules WHERE ExamId=$exam;") -split "`n")){$p=$line.Trim().Split(':');if($p.Length -eq 2){$schedules[$p[0]]=[int]$p[1]}}
    $ids=@{}
    foreach($name in @('before','after','race')) {
        $started=Request 'POST' "/web/exam-schedules/$($schedules[$name])/attempts"
        if($started.status -ne 200){throw "$name start $($started.status)"}
        $id=[string]$started.data.attemptId;$ids[$name]=$id
        $q=@($started.data.questions)[0];$good=@($q.options | Where-Object content -Eq 'Yes')[0]
        $save=Request 'PUT' "/web/attempts/$id/answers" @{revision=0;answers=@(@{questionId=$q.id;selectedAnswerIds=@($good.id)})}
        if($save.status -ne 200 -or $save.data.revision -ne 1){throw "$name save failed"}
    }
    $before=Request 'POST' "/web/attempts/$($ids.before)/submit" @{revision=1}
    if($before.status -ne 200 -or $before.data.state -ne 'submitted'){throw 'Before-deadline submit failed'}
    $beforeDb=Sql "SELECT CASE WHEN s.SubmittedAtUtc<a.DeadlineUtc AND a.State='submitted' THEN 1 ELSE 0 END FROM dbo.ExamAttempts a JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId=a.Id WHERE a.Id='$($ids.before)';"
    if($beforeDb -ne '1'){throw 'Before-deadline persisted clock/state mismatch'}
    Sql "UPDATE dbo.ExamAttempts SET StartedAtUtc=DATEADD(second,-65,SYSUTCDATETIME()),DeadlineUtc=DATEADD(second,-2,SYSUTCDATETIME()) WHERE Id='$($ids.after)';" | Out-Null
    $after=Request 'POST' "/web/attempts/$($ids.after)/submit" @{revision=1}
    if($after.status -ne 200 -or $after.data.state -ne 'timedOut'){throw 'After-deadline submit failed'}
    $afterDb=Sql "SELECT CASE WHEN s.SubmittedAtUtc=a.DeadlineUtc AND a.State='timedOut' THEN 1 ELSE 0 END FROM dbo.ExamAttempts a JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId=a.Id WHERE a.Id='$($ids.after)';"
    if($afterDb -ne '1'){throw 'After-deadline persisted clock/state mismatch'}
    $env:ConnectionStrings__Default='Server=.;Database=ProjectDACN_Task04Task09_Test;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
    $holder=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @('tests/WorkerSweepSmoke/bin/Debug/net8.0/WorkerSweepSmoke.dll','--hold-lock',$marker,'8000') -WorkingDirectory (Get-Location).Path -PassThru -WindowStyle Hidden -RedirectStandardOutput $holderOut -RedirectStandardError $holderErr
    $held=$false;for($i=0;$i -lt 80;$i++){if(Test-Path -LiteralPath $marker){$held=$true;break};Start-Sleep -Milliseconds 100}
    if(-not $held){throw 'SQL lock holder did not start'}
    $deadlineText=Sql "UPDATE dbo.ExamAttempts SET StartedAtUtc=DATEADD(second,-60,SYSUTCDATETIME()),DeadlineUtc=DATEADD(second,4,SYSUTCDATETIME()) WHERE Id='$($ids.race)'; SELECT CONVERT(varchar(33),DeadlineUtc,126) FROM dbo.ExamAttempts WHERE Id='$($ids.race)';"
    $deadline=[DateTime]::SpecifyKind([DateTime]::Parse($deadlineText),[DateTimeKind]::Utc)
    $submitJob=Start-ThreadJob -ArgumentList $BaseUrl,$jwt,$ids.race -ScriptBlock {
        param($url,$token,$id)
        $sent=[DateTime]::UtcNow
        $r=Invoke-WebRequest "$url/web/attempts/$id/submit" -Method Post -Headers @{Authorization="Bearer $token"} -ContentType 'application/json' -Body '{"revision":1}' -SkipCertificateCheck -SkipHttpErrorCheck
        @{sent=$sent;status=[int]$r.StatusCode;state=($r.Content | ConvertFrom-Json).data.state}
    }
    Start-Sleep -Seconds 5
    $sweepJob=Start-ThreadJob -ArgumentList (Get-Location).Path -ScriptBlock {
        param($root)
        $env:ConnectionStrings__Default='Server=.;Database=ProjectDACN_Task04Task09_Test;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
        & dotnet (Join-Path $root 'tests/WorkerSweepSmoke/bin/Debug/net8.0/WorkerSweepSmoke.dll') | Select-String 'WORKER_SWEEP_COMPLETED'
        if($LASTEXITCODE -ne 0){throw 'Concurrent sweep failed'}
    }
    $null=@($submitJob,$sweepJob) | Wait-Job -Timeout 35
    $submitted=Receive-Job $submitJob
    $sweep=Receive-Job $sweepJob
    Remove-Job $submitJob,$sweepJob -Force
    if($submitted.status -ne 200 -or $submitted.state -ne 'timedOut' -or -not $sweep){throw 'Submit/worker race response failed'}
    if($submitted.sent -ge $deadline){throw 'Test request was not sent before deadline'}
    $raceDb=Sql "SELECT CONCAT(COUNT(*),',',MIN(a.State),',',MIN(CASE WHEN s.SubmittedAtUtc=a.DeadlineUtc AND a.FinishedAtUtc>=a.DeadlineUtc THEN 1 ELSE 0 END)) FROM dbo.ExamAttempts a JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId=a.Id WHERE a.Id='$($ids.race)';"
    if($raceDb -ne '1,timedOut,1'){throw "Race final state wrong: $raceDb"}
    $retry=Request 'POST' "/web/attempts/$($ids.race)/submit" @{revision=1}
    if($retry.status -ne 200 -or $retry.data.state -ne 'timedOut'){throw 'Repeated submit changed race result'}
    if((Sql "SELECT COUNT(*) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$($ids.race)';") -ne '1'){throw 'Duplicate race submission'}
    Write-Output "TASK10_DEADLINE_SQL=PASS tag=$tag before=submitted after=timedOut race=timedOut singleSubmission=1 sentBeforeDeadline=1"
} finally {
    if($holder -and -not $holder.HasExited){$holder.WaitForExit(12000) | Out-Null; if(-not $holder.HasExited){Stop-Process -Id $holder.Id -Force}}
    foreach($path in @($marker,$holderOut,$holderErr)){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}
    if($exam -gt 0){Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam); DELETE dbo.ExamAttempts WHERE ExamId=$exam; DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ClassUsers WHERE ClassId=$class; DELETE dbo.Classes WHERE Id=$class; DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.Answers WHERE QuestionId=$question; DELETE dbo.Questions WHERE Id=$question; DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" | Out-Null}
}
