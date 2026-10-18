param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7255')
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named database copy is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51101, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL fixture failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
if ((Sql 'SELECT DB_NAME();') -ne $Database) { throw 'Connection preflight failed.' }
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
$email="attemptqa$tag@example.invalid"
$password='Qa7'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$registered=Auth 'register' @{ fullName='Attempt QA'; userName="attemptqa$tag"; email=$email; password=$password; address='QA'; dateOfBirth='2000-01-01T00:00:00Z'; sex=$true }
if ($registered.status -ne 202) { throw "Register HTTP $($registered.status)" }
$mail=Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending | Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
if (-not $mail) { throw 'Verification mail absent.' }
$match=[regex]::Match((Get-Content $mail.FullName -Raw),'#token=([^\s]+)')
if (-not $match.Success -or (Auth 'verify-email' @{token=[Uri]::UnescapeDataString($match.Groups[1].Value)}).status -ne 200) { throw 'Verify failed.' }
$login=Auth 'login' @{keyword=$email; password=$password}
if ($login.status -ne 200) { throw 'Login failed.' }
$user=[Guid]$login.data.data.userId
$token=[string]$login.data.data.accessToken
$exam=0
function Request([string]$method,[string]$url,$body=$null,[string]$bearer=$token) {
    $h=@{Authorization="Bearer $bearer"}
    $args=@{Uri="$BaseUrl$url";Method=$method;Headers=$h;SkipCertificateCheck=$true;SkipHttpErrorCheck=$true}
    if ($null -ne $body) { $args.ContentType='application/json'; $args.Body=($body | ConvertTo-Json -Depth 15 -Compress) }
    $r=Invoke-WebRequest @args
    return @{status=[int]$r.StatusCode; data=($r.Content | ConvertFrom-Json)}
}
try {
    $fixture=Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'Attempt QA $tag',@subject,2,1,1,10,5,30,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy)
VALUES(@exam,N'QA',1,SYSUTCDATETIME(),'$user',SYSUTCDATETIME(),'$user');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId)
VALUES(N'Single source $tag',SYSUTCDATETIME(),1,@subject,2);
DECLARE @q1 int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId)
VALUES(N'Multi source $tag',SYSUTCDATETIME(),1,@subject,3);
DECLARE @q2 int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES
(@q1,N'Correct A',1,1),(@q1,N'Wrong B',0,1),
(@q2,N'Correct C',1,1),(@q2,N'Correct D',1,1),(@q2,N'Wrong E',0,1);
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES(@variant,@q1,5),(@variant,@q2,5);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status)
VALUES(N'Attempt QA $tag',N'AT-$tag',2,'$user',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES(@class,'$user',1);
DECLARE @now datetime2=SYSUTCDATETIME();
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc)
VALUES(@exam,N'Open $tag',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'Unknown $tag',DATEADD(minute,-1,@now),DATEADD(minute,10,@now),1,0),
(@exam,N'Early $tag',DATEADD(minute,5,@now),DATEADD(minute,10,@now),1,1),
(@exam,N'Ended $tag',DATEADD(minute,-10,@now),DATEADD(minute,-5,@now),1,1),
(@exam,N'Timeout $tag',DATEADD(minute,-1,@now),DATEADD(minute,1,@now),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@exam;
SELECT CONCAT(@exam,',',@q1,',',@q2,',',@class);
"@
    $parts=$fixture.Split(','); $exam=[int]$parts[0];$q1=[int]$parts[1];$q2=[int]$parts[2];$class=[int]$parts[3]
    $schedules=Sql "SELECT CONCAT(Id,':',Title) FROM dbo.ExamSchedules WHERE ExamId=$exam ORDER BY Id;"
    $ids=@{}; foreach($line in ($schedules -split "`n")) { $p=$line.Trim().Split(':'); if($p.Length -eq 2){$ids[$p[1].Split(' ')[0]]=[int]$p[0]} }
    foreach($name in @('Unknown','Early','Ended')) {
        $r=Request 'POST' "/web/exam-schedules/$($ids[$name])/attempts"
        if($r.status -ne 409){throw "$name start returned $($r.status)"}
    }
    $other=[Guid]::NewGuid()
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex) SELECT '$other',N'Other QA',N'other$tag',N'other$tag@example.invalid',PasswordHash,4,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$user';" | Out-Null
    $otherLogin=Auth 'login' @{keyword="other$tag@example.invalid";password=$password}
    if($otherLogin.status -ne 200){throw 'Other login failed'}
    $otherToken=[string]$otherLogin.data.data.accessToken
    if((Request 'POST' "/web/exam-schedules/$($ids.Open)/attempts" $null $otherToken).status -ne 403){throw 'Nonmember start not forbidden'}
    $endpoint="$BaseUrl/web/exam-schedules/$($ids.Open)/attempts"
    $handler=[Net.Http.HttpClientHandler]::new();$handler.ServerCertificateCustomValidationCallback=[Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
    $client=[Net.Http.HttpClient]::new($handler);$client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)
    $a=$client.PostAsync($endpoint,[Net.Http.StringContent]::new('',[Text.Encoding]::UTF8,'application/json'))
    $b=$client.PostAsync($endpoint,[Net.Http.StringContent]::new('',[Text.Encoding]::UTF8,'application/json'))
    [Threading.Tasks.Task]::WaitAll(@($a,$b))
    $first=($a.Result.Content.ReadAsStringAsync().Result | ConvertFrom-Json).data
    $second=($b.Result.Content.ReadAsStringAsync().Result | ConvertFrom-Json).data
    if($a.Result.StatusCode -ne 200 -or $b.Result.StatusCode -ne 200 -or $first.attemptId -ne $second.attemptId){throw 'Concurrent start created two attempts'}
    $id=$first.attemptId
    $scheduleEnd=[DateTime]::SpecifyKind([DateTime]::Parse((Sql "SELECT CONVERT(varchar(33),EndTime,126) FROM dbo.ExamSchedules WHERE Id=$($ids.Open);")),[DateTimeKind]::Utc)
    $deadlineUtc=([DateTime]$first.deadline).ToUniversalTime()
    if([Math]::Abs(($deadlineUtc-$scheduleEnd).TotalSeconds) -gt 1){throw "Deadline did not use schedule end as minimum: deadline=$deadlineUtc end=$scheduleEnd"}
    if((Sql "SELECT COUNT(*) FROM dbo.ExamAttempts WHERE UserId='$user' AND ExamScheduleId=$($ids.Open);") -ne '1'){throw 'Attempt count exceeded'}
    $raw=$first | ConvertTo-Json -Depth 20
    if($raw -match '"(isCorrect|sourceQuestionId|sourceAnswerId|point|passMark|snapshotJson|draftJson)"'){throw 'Unsafe attempt response'}
    $questions=@($first.questions)
    Sql "UPDATE dbo.Questions SET Content=N'Changed after start' WHERE Id=$q1; UPDATE dbo.Answers SET Content=N'Changed option' WHERE QuestionId=$q1;" | Out-Null
    $reloaded=Request 'GET' "/web/attempts/$id"
    if($reloaded.status -ne 200 -or ($reloaded.data.data.questions | ConvertTo-Json -Depth 10) -match 'Changed after start|Changed option'){throw 'Snapshot changed with source'}
    if((Request 'GET' "/web/attempts/$id" $null $otherToken).status -ne 404){throw 'Other student accessed attempt'}
    if((Request 'PUT' "/web/attempts/$id/answers" @{revision=0;answers=@()} $otherToken).status -ne 404 -or
        (Request 'POST' "/web/attempts/$id/submit" @{revision=0} $otherToken).status -ne 404){throw 'Other student changed attempt'}
    $invalid=Request 'PUT' "/web/attempts/$id/answers" @{revision=0;answers=@(@{questionId=$questions[0].id;selectedAnswerIds=@([Guid]::NewGuid())})}
    if($invalid.status -ne 400){throw 'Outside-snapshot selection accepted'}
    $single=$questions[0];$multi=$questions[1]
    $draft=@(@{questionId=$single.id;selectedAnswerIds=@($single.options[0].id)},@{questionId=$multi.id;selectedAnswerIds=@($multi.options[0].id,$multi.options[1].id)})
    $saveBody=(@{revision=0;answers=$draft} | ConvertTo-Json -Depth 15 -Compress)
    $putUrl="$BaseUrl/web/attempts/$id/answers"
    $saveA=$client.PutAsync($putUrl,[Net.Http.StringContent]::new($saveBody,[Text.Encoding]::UTF8,'application/json'))
    $saveB=$client.PutAsync($putUrl,[Net.Http.StringContent]::new($saveBody,[Text.Encoding]::UTF8,'application/json'))
    [Threading.Tasks.Task]::WaitAll(@($saveA,$saveB))
    $saveStatuses=@([int]$saveA.Result.StatusCode,[int]$saveB.Result.StatusCode) | Sort-Object
    if(($saveStatuses -join ',') -ne '200,409'){throw "Concurrent save statuses: $($saveStatuses -join ',')"}
    $saved=Request 'GET' "/web/attempts/$id"
    if($saved.status -ne 200 -or $saved.data.data.revision -ne 1){throw 'Saved revision missing'}
    if((Request 'PUT' "/web/attempts/$id/answers" @{revision=0;answers=@()}).status -ne 409){throw 'Stale save overwrote draft'}
    if((Request 'POST' "/web/attempts/$id/submit" @{revision=0}).status -ne 409){throw 'Stale submit accepted'}
    $submitted=Request 'POST' "/web/attempts/$id/submit" @{revision=1}
    $again=Request 'POST' "/web/attempts/$id/submit" @{revision=1}
    if($submitted.status -ne 200 -or $again.status -ne 200 -or $submitted.data.data.result.totalMark -ne 10){throw 'Submit/grade/retry failed'}
    if((Sql "SELECT COUNT(*) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$id';") -ne '1'){throw 'Duplicate submission'}
    if((Request 'POST' "/web/exam-schedules/$($ids.Open)/attempts").status -ne 409){throw 'Attempt limit failed'}
    if((Request 'PUT' "/web/attempts/$id/answers" @{revision=1;answers=@()}).status -ne 409){throw 'Save after submit accepted'}
    Sql "UPDATE dbo.Exams SET AllowViewResult=0 WHERE Id=$exam;" | Out-Null
    $timeout=Request 'POST' "/web/exam-schedules/$($ids.Timeout)/attempts"
    if($timeout.status -ne 200){throw 'Timeout fixture did not start'}
    $timeoutId=$timeout.data.data.attemptId
    Sql "UPDATE dbo.ExamAttempts SET DeadlineUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE Id='$timeoutId';" | Out-Null
    $resumed=Request 'POST' "/web/exam-schedules/$($ids.Timeout)/attempts"
    if($resumed.status -ne 200 -or $resumed.data.data.attemptId -ne $timeoutId -or $resumed.data.data.state -ne 'timedOut'){
        throw 'Expired start did not commit timeout before checking attempt limit'
    }
    $expired=Request 'POST' "/web/attempts/$timeoutId/submit" @{revision=0}
    if($expired.status -ne 200 -or $expired.data.data.state -ne 'timedOut' -or $null -ne $expired.data.data.result){throw 'Timeout finalization/result policy failed'}
    if((Request 'PUT' "/web/attempts/$timeoutId/answers" @{revision=0;answers=@()}).status -ne 409){throw 'Late save accepted'}
    Sql "UPDATE dbo.ClassUsers SET Status=2 WHERE ClassId=$class AND UserId='$user';" | Out-Null
    if((Request 'GET' "/web/attempts/$id").status -ne 404 -or
        (Request 'POST' "/web/attempts/$id/submit" @{revision=1}).status -ne 404){throw 'Revoked membership still accessed attempt'}
    Write-Output 'TASK10_SQL_API_START_CONCURRENT_LIMIT_MEMBERSHIP_SNAPSHOT_SAVE_REVISION_SUBMIT_TIMEOUT=PASS'
} finally {
    if($exam -gt 0) {
        Sql "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam); DELETE dbo.ExamAttempts WHERE ExamId=$exam; DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ClassUsers WHERE ClassId=$class; DELETE dbo.Classes WHERE Id=$class; DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.Answers WHERE QuestionId IN ($q1,$q2); DELETE dbo.Questions WHERE Id IN ($q1,$q2); DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" | Out-Null
    }
}
