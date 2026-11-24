# To-be T17 - Ghep cap (loai 5) va Dung/Sai nhieu menh de (loai 6): soan cau + validator, thi (khong lo cap/gia tri dung, cham, xem lai),
# luyen tap kiem tra tung cau, thi thu, outcome, Excel import/export, khoa loai he thong.
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao đã áp migration T17. Chỉ ghi bản sao; dọn trong finally.
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -f 65001 -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51241, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join ' ')" }
    return ($output | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
function Auth([string]$route, $body) {
    for ($i = 0; $i -lt 6; $i++) {
        $r = Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck `
            -Headers @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)
        if ([int]$r.StatusCode -ne 429) { return @{ status = [int]$r.StatusCode; body = ($r.Content | ConvertFrom-Json) } }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
function Call([string]$method, [string]$url, [string]$token = '', $json = $null, $form = $null) {
    $h = @{}
    if ($token) { $h.Authorization = "Bearer $token" }
    $p = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true; Headers = $h }
    if ($null -ne $json) { $p.ContentType = 'application/json; charset=utf-8'; $p.Body = [Text.Encoding]::UTF8.GetBytes(($json | ConvertTo-Json -Compress -Depth 8)) }
    if ($null -ne $form) { $p.Form = $form }
    $r = Invoke-WebRequest @p
    $type = [string]($r.Headers['Content-Type'] | Select-Object -First 1)
    $text = if ($type -match 'json') { [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) } else { '' }
    return @{ status = [int]$r.StatusCode; body = if ($text) { $text | ConvertFrom-Json } else { $null }; bytes = $r.RawContentStream.ToArray(); raw = $text }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected '$expected', got '$actual'" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'T17 $name',N't17$name$tag',N't17$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t17$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }

# Workbook tối giản (định dạng v1 + cột G tùy chọn)
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t17-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
$S = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
function Cell([string]$ref, [string]$value) { return "<c r=`"$ref`" t=`"inlineStr`"><is><t xml:space=`"preserve`">$([Security.SecurityElement]::Escape($value))</t></is></c>" }
function Book([string]$name, [object[]]$rows, [string[]]$header) {
    $path = Join-Path $work $name; $cols = 'ABCDEFG'
    $xml = '<row r="1">' + ((0..($header.Count - 1) | ForEach-Object { Cell "$($cols[$_])1" $header[$_] }) -join '') + '</row>'
    $n = 2
    foreach ($row in $rows) { $xml += "<row r=`"$n`">" + ((0..($row.Count - 1) | ForEach-Object { Cell "$($cols[$_])$n" $row[$_] }) -join '') + '</row>'; $n++ }
    $entries = [ordered]@{
        '[Content_Types].xml' = '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'
        '_rels/.rels' = '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'
        'xl/workbook.xml' = "<workbook xmlns=`"$S`" xmlns:r=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships`"><sheets><sheet name=`"Questions`" sheetId=`"1`" r:id=`"rId1`"/></sheets></workbook>"
        'xl/_rels/workbook.xml.rels' = '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'
        'xl/worksheets/sheet1.xml' = "<worksheet xmlns=`"$S`"><sheetData>$xml</sheetData></worksheet>"
    }
    $file = [IO.File]::Create($path)
    try {
        $zip = [IO.Compression.ZipArchive]::new($file, [IO.Compression.ZipArchiveMode]::Create)
        try { foreach ($k in $entries.Keys) { $w = [IO.StreamWriter]::new($zip.CreateEntry($k).Open(), [Text.UTF8Encoding]::new($false)); try { $w.Write($entries[$k]) } finally { $w.Dispose() } } }
        finally { $zip.Dispose() }
    } finally { $file.Dispose() }
    return Get-Item $path
}
function Upload($item, [string]$token) {
    $r = Invoke-WebRequest "$BaseUrl/web/questions/import" -Method Post -Headers @{ Authorization = "Bearer $token" } -Form @{ file = $item } -SkipCertificateCheck -SkipHttpErrorCheck
    return @{ status = [int]$r.StatusCode; body = ([Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) | ConvertFrom-Json) }
}
$header7 = @('SubjectId', 'QuestionLevelId', 'QuestionTypeId', 'Content', 'Answers', 'CorrectIndexes', 'ShortAnswerOptions')

$seed = [Guid]::Empty; $studentB = [Guid]::NewGuid(); $examiner = [Guid]::NewGuid()
$subject = 0; $exam = 0; $mail = $null
try {
    $email = "t17seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T17 A'; userName = "t17seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $tokenA = [string]$login.body.data.accessToken
    $examinerEmail = CloneUser $examiner 2 'examiner' $seed
    Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);" | Out-Null
    $tokenE = LoginToken $examinerEmail
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Subjects(Name,Status) VALUES (N'T17 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T17 câu chọn $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @qc int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@qc,N'OK',1,1),(@qc,N'NO',0,1);
SELECT CONCAT(@s,'|',@qc,'|',(SELECT TOP 1 Id FROM dbo.QuestionLevels WHERE Status=1 ORDER BY Id));
COMMIT;
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $qc = [int]$p[1]; $level = [int]$p[2]

    Write-Output '== Soạn câu ghép cặp / nhiều mệnh đề (validator)'
    function Body([int]$type, [string]$content, [object[]]$items) {
        return @{ subjectId = $subject; questionTypeId = $type; questionLevelId = $level; content = $content; status = 1; tagIds = @()
            answers = @($items | ForEach-Object { @{ content = $_[0]; matchContent = $_[1]; isCorrect = [bool]$_[2] } }) }
    }
    $c5 = Call 'POST' '/web/questions' $tokenE (Body 5 "T17 ghép $tag" @(@('Hà Nội', 'Việt Nam', $false), @('Tokyo', 'Nhật Bản', $false), @('Paris', 'Pháp', $false)))
    Expect "$($c5.status)|$($c5.body.data.questionTypeId)|$(@($c5.body.data.answers | ForEach-Object { "$($_.content)>$($_.matchContent)>$($_.isCorrect)" }) -join ',')" `
        '201|5|Hà Nội>Việt Nam>True,Tokyo>Nhật Bản>True,Paris>Pháp>True' 'create matching'
    $qm = [int]$c5.body.data.id
    $c6 = Call 'POST' '/web/questions' $tokenE (Body 6 "T17 mệnh đề $tag" @(@('Nước sôi ở 100°C', $null, $true), @('Mặt Trời quay quanh Trái Đất', $null, $false), @('2 + 2 = 4', $null, $true)))
    Expect "$($c6.status)|$(@($c6.body.data.answers | ForEach-Object { $_.isCorrect }) -join ',')|$($null -eq $c6.body.data.answers[0].matchContent)" '201|True,False,True|True' 'create statements'
    $qs = [int]$c6.body.data.id
    $bad = @(
        @('1 pair', (Body 5 "T17 x1 $tag" @(, @('A', '1', $true)))),
        @('duplicate right', (Body 5 "T17 x2 $tag" @(@('A', '1', $true), @('B', ' 1 ', $true)))),
        @('missing right', (Body 5 "T17 x3 $tag" @(@('A', '1', $true), @('B', '', $true)))),
        @('duplicate statements', (Body 6 "T17 x4 $tag" @(@('P', $null, $true), @('p', $null, $false)))),
        @('11 statements', (Body 6 "T17 x5 $tag" @(1..11 | ForEach-Object { , @("m$_", $null, $true) }))))
    foreach ($case in $bad) { Expect (Call 'POST' '/web/questions' $tokenE $case[1]).status 400 $case[0] }
    Expect (Sql "SELECT COUNT(*) FROM dbo.Answers WHERE QuestionId=$qs AND MatchContent IS NOT NULL;") '0' 'statements store no MatchContent'
    Pass 'tạo 201 (vế phải lưu riêng, mệnh đề Đúng/Sai); 400 cho 1 cặp/trùng/thiếu vế phải/trùng mệnh đề/11 mệnh đề'

    Write-Output '== Bài thi: không lộ cặp/giá trị đúng, chấm tất cả-hoặc-không, xem lại'
    $sched = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T17 đề $tag',$subject,3,1,1,4,1,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'T17',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
DECLARE @d int=SCOPE_IDENTITY();
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@d,$qm,2),(@d,$qs,1),(@d,$qc,1);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T17 $tag',N'T17-$tag',5,'$examiner',$subject,1);
DECLARE @c int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@c,'$seed',1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'T17 lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,30,SYSUTCDATETIME()),1,1);
DECLARE @sc int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES (@c,@sc);
COMMIT;
SELECT CONCAT(@e,'|',@sc);
"@
    $exam = [int]$sched.Split('|')[0]; $schedule = [int]$sched.Split('|')[1]
    $start = Call 'POST' "/web/exam-schedules/$schedule/attempts" $tokenA
    Expect $start.status 200 'start exam'
    $attempt = $start.body.data.attemptId
    $byContent = @{}; foreach ($q in $start.body.data.questions) { $byContent[$q.content] = $q }
    $sm = $byContent["T17 ghép $tag"]; $ss = $byContent["T17 mệnh đề $tag"]; $sc = $byContent["T17 câu chọn $tag"]
    $leftIds = @($sm.options | ForEach-Object { $_.id }); $rightIds = @($sm.matches | ForEach-Object { $_.id })
    Expect "$($sm.questionTypeId)|$($leftIds.Count)|$($rightIds.Count)|$(@($leftIds | Where-Object { $rightIds -contains $_ }).Count)|$(@($ss.options).Count)|$($null -eq $ss.matches)|$($null -eq $sc.matches)" `
        '5|3|3|0|3|True|True' 'student DTO: left + shuffled right with own ids'
    if ($start.raw -match 'isCorrect|matchId|matchContent|correctItems|sourceAnswerId') { throw 'Attempt DTO leaked pairing/correctness' }
    $order = Sql "SELECT STRING_AGG(LOWER(m.value),',') WITHIN GROUP (ORDER BY CAST(m.[key] AS int)) FROM dbo.ExamAttempts a CROSS APPLY OPENJSON(a.SnapshotJson,'$.questions') q CROSS APPLY OPENJSON(q.value,'$.matchOrder') m WHERE a.Id='$attempt' AND JSON_VALUE(q.value,'$.questionTypeId')='5';"
    Expect (($rightIds -join ',').ToLowerInvariant()) $order 'right sides in snapshot MatchOrder'
    Expect (Sql "SELECT JSON_VALUE(SnapshotJson,'$.version') FROM dbo.ExamAttempts WHERE Id='$attempt';") '6' 'snapshot v6'
    # Đáp án đúng chỉ đọc được từ snapshot phía server (để smoke dựng bài làm đúng)
    $pairs = @{}; foreach ($kv in (Sql "SELECT STRING_AGG(CONCAT(JSON_VALUE(o.value,'$.id'),'=',JSON_VALUE(o.value,'$.matchId')),',') FROM dbo.ExamAttempts a CROSS APPLY OPENJSON(a.SnapshotJson,'$.questions') q CROSS APPLY OPENJSON(q.value,'$.options') o WHERE a.Id='$attempt' AND JSON_VALUE(q.value,'$.questionTypeId')='5';") -split ',') { $x = $kv.Split('='); $pairs[$x[0]] = $x[1] }
    $truth = @{}; foreach ($kv in (Sql "SELECT STRING_AGG(CONCAT(JSON_VALUE(o.value,'$.id'),'=',JSON_VALUE(o.value,'$.isCorrect')),',') FROM dbo.ExamAttempts a CROSS APPLY OPENJSON(a.SnapshotJson,'$.questions') q CROSS APPLY OPENJSON(q.value,'$.options') o WHERE a.Id='$attempt' AND JSON_VALUE(q.value,'$.questionTypeId')='6';") -split ',') { $x = $kv.Split('='); $truth[$x[0]] = $x[1] -eq 'true' }
    $rightItems = @($leftIds | ForEach-Object { @{ optionId = $_; matchId = $pairs[$_] } })
    $stItems = @($ss.options | ForEach-Object { @{ optionId = $_.id; value = $truth[$_.id] } })
    $okChoice = @($sc.options | Where-Object content -eq 'OK')[0].id
    $rev = $start.body.data.revision
    Expect (Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(@{ questionId = $sm.id; selectedAnswerIds = @(); items = @(@{ optionId = $leftIds[0]; matchId = $rightIds[0] }, @{ optionId = $leftIds[1]; matchId = $rightIds[0] }) }) }).status 400 'right side used twice'
    Expect (Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(@{ questionId = $ss.id; selectedAnswerIds = @(); items = @(@{ optionId = $ss.options[0].id }) }) }).status 400 'statement without value'
    Expect (Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(@{ questionId = $sc.id; selectedAnswerIds = @($okChoice); items = @(@{ optionId = $okChoice; value = $true }) }) }).status 400 'items on choice'
    $save = Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(
        @{ questionId = $sm.id; selectedAnswerIds = @(); items = $rightItems },
        @{ questionId = $ss.id; selectedAnswerIds = @(); items = $stItems },
        @{ questionId = $sc.id; selectedAnswerIds = @($okChoice) }) }
    Expect $save.status 200 'save answers'
    $state = (Call 'GET' "/web/attempts/$attempt" $tokenA).body.data
    Expect @(@($state.savedAnswers | Where-Object questionId -eq $sm.id)[0].items).Count 3 'draft keeps items'
    if ($state.savedAnswers | Where-Object { $_.questionId -eq $sc.id -and $null -ne $_.items }) { throw 'choice draft must omit items' }
    Expect (Call 'POST' "/web/attempts/$attempt/submit" $tokenA @{ revision = $save.body.data.revision }).status 200 'submit'
    Expect (Sql "SELECT CAST(TotalMark AS decimal(5,2)) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$attempt';") '4.00' 'score 2 + 1 + 1'
    Expect (Sql "SELECT CONCAT(SUM(CAST(IsCorrect AS int)),'|',SUM(CAST(IsAnswered AS int))) FROM dbo.QuestionOutcomes WHERE SourceId='$attempt';") '3|3' 'outcomes agree'
    Sql "UPDATE dbo.ExamSchedules SET StartTime=DATEADD(hour,-2,SYSUTCDATETIME()), EndTime=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=$schedule;" | Out-Null
    $rv = Call 'GET' "/web/attempts/$attempt/review" $tokenA
    Expect $rv.status 200 'review'
    $rm = @($rv.body.data.questions | Where-Object id -eq $sm.id)[0]; $rs = @($rv.body.data.questions | Where-Object id -eq $ss.id)[0]
    Expect "$($rm.isCorrect)|$(@($rm.items).Count)|$(@($rm.correctItems).Count)|$(@($rm.matches).Count)|$(@($rm.options | Where-Object { $_.matchContent }).Count)" 'True|3|3|3|3' 'review matching'
    Expect "$($rs.isCorrect)|$(@($rs.correctItems | ForEach-Object { $_.value }) -join ',')" 'True|True,False,True' 'review statements'
    Pass 'DTO không có cặp/giá trị đúng; vế phải theo MatchOrder; 400 cho nháp sai dạng; điểm 4/4; outcome; xem lại có items + correctItems'

    Write-Output '== Luyện tập kiểm tra từng câu (sai một mệnh đề = sai cả câu)'
    $pr = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 3; revealMode = 'perQuestion' }
    Expect $pr.status 201 'create practice (pool includes 5, 6)'
    if ($pr.raw -match 'isCorrect|matchId|matchContent|correctItems') { throw 'Practice DTO leaked' }
    $pm = @($pr.body.data.questions | Where-Object { $_.content -eq "T17 ghép $tag" })[0]
    $ps = @($pr.body.data.questions | Where-Object { $_.content -eq "T17 mệnh đề $tag" })[0]
    $prRev = $pr.body.data.revision
    Expect (Call 'POST' "/web/student/practice/$($pr.body.data.id)/questions/$($pm.id)/check" $tokenA @{ revision = $prRev; selectedAnswerIds = @(); items = @() }).body.message 'INVALID_SELECTION' 'empty items check'
    $allTrue = @($ps.options | ForEach-Object { @{ optionId = $_.id; value = $true } })
    $chk = Call 'POST' "/web/student/practice/$($pr.body.data.id)/questions/$($ps.id)/check" $tokenA @{ revision = $prRev; selectedAnswerIds = @(); items = $allTrue }
    Expect "$($chk.status)|$($chk.body.data.result.isCorrect)|$(@($chk.body.data.result.items).Count)|$(@($chk.body.data.result.correctItems | ForEach-Object { $_.value }) -join ',')" '200|False|3|True,False,True' 'check statements (one wrong)'
    $prRev = $chk.body.data.revision
    $locked = Call 'PUT' "/web/student/practice/$($pr.body.data.id)/answers" $tokenA @{ revision = $prRev; answers = @(@{ questionId = $ps.id; selectedAnswerIds = @(); items = @(@{ optionId = $ps.options[0].id; value = $false }) }) }
    Expect $locked.body.message 'PRACTICE_QUESTION_LOCKED' 'checked items locked'
    $partial = @(@{ optionId = $pm.options[0].id; matchId = $pm.matches[0].id })
    $psave = Call 'PUT' "/web/student/practice/$($pr.body.data.id)/answers" $tokenA @{ revision = $prRev; answers = @(@{ questionId = $pm.id; selectedAnswerIds = @(); items = $partial }) }
    Expect $psave.status 200 'save partial matching'
    Expect (Call 'POST' "/web/student/practice/$($pr.body.data.id)/finish" $tokenA @{ revision = $psave.body.data.revision }).status 200 'finish practice'
    $prv = (Call 'GET' "/web/student/practice/$($pr.body.data.id)/review" $tokenA).body.data
    $vm = @($prv.questions | Where-Object id -eq $pm.id)[0]
    Expect "$($vm.isCorrect)|$(@($vm.items).Count)|$(@($vm.correctItems).Count)" 'False|1|3' 'partial matching = wrong'
    Expect (Sql "SELECT CONCAT(IsCorrect,'|',IsAnswered) FROM dbo.QuestionOutcomes WHERE SourceId='$($pr.body.data.id)' AND SourceQuestionId=$qm;") '0|1' 'outcome partial answered but wrong'
    Pass 'pool có loại 5–6; check rỗng 400; check trả items + correctItems; câu đã chốt khóa; ghép thiếu = sai'

    Write-Output '== Thi thử'
    $mock = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 3; kind = 'mock'; durationMinutes = 10 }
    Expect $mock.status 201 'create mock'
    if ($mock.raw -match 'isCorrect|matchId|matchContent|correctItems') { throw 'Mock DTO leaked' }
    $mm = @($mock.body.data.questions | Where-Object { $_.content -eq "T17 ghép $tag" })[0]
    Expect "$(@($mm.matches).Count)" '3' 'mock has matches'
    Pass 'thi thử không lộ cặp/giá trị đúng'

    Write-Output '== Excel: xuất/nhập loại 5–6'
    $ex = Call 'GET' "/web/questions/export?subjectId=$subject&questionTypeId=5" $tokenE
    Expect $ex.status 200 'export type 5'
    $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($ex.bytes))
    $sheet = [IO.StreamReader]::new($zip.GetEntry('xl/worksheets/sheet1.xml').Open()).ReadToEnd(); $zip.Dispose()
    if (-not $sheet.Contains('Hà Nội =&gt; Việt Nam;Tokyo =&gt; Nhật Bản;Paris =&gt; Pháp')) { throw 'export matching pairs missing' }
    $ex6 = Call 'GET' "/web/questions/export?subjectId=$subject&questionTypeId=6" $tokenE
    $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($ex6.bytes))
    $sheet6 = [IO.StreamReader]::new($zip.GetEntry('xl/worksheets/sheet1.xml').Open()).ReadToEnd(); $zip.Dispose()
    if (-not $sheet6.Contains('>1,3<')) { throw 'export statements correct indexes missing' }
    $good = Upload (Book 'ok.xlsx' @(@("$subject", "$level", '5', "T17 nhập ghép $tag", 'Đỏ => Red; Xanh => Blue', '', ''),
        @("$subject", "$level", '6', "T17 nhập mệnh đề $tag", 'P sai; Q sai', '', '')) $header7) $tokenE
    Expect "$($good.status)|$($good.body.data.imported)" '200|2' 'import types 5 and 6'
    Expect (Sql "SELECT STRING_AGG(CONCAT(a.Content,'>',a.MatchContent,'>',CAST(a.IsCorrect AS int)),',') WITHIN GROUP (ORDER BY a.Id) FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject AND q.Content=N'T17 nhập ghép $tag';") 'Đỏ>Red>1,Xanh>Blue>1' 'imported pairs'
    Expect (Sql "SELECT SUM(CAST(a.IsCorrect AS int)) FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject AND q.Content=N'T17 nhập mệnh đề $tag';") '0' 'imported all-false statements'
    $codes = @(
        @('pair without arrow', @("$subject", "$level", '5', "T17 y1 $tag", 'A - 1; B => 2', '', ''), 'ANSWERS'),
        @('indexes on matching', @("$subject", "$level", '5', "T17 y2 $tag", 'A => 1; B => 2', '1', ''), 'CORRECT_INDEXES'),
        @('G on statements', @("$subject", "$level", '6', "T17 y3 $tag", 'P; Q', '1', 'numeric'), 'SHORT_ANSWER_OPTIONS'),
        # Review T17: các dạng cặp sai
        @('three-part pair', @("$subject", "$level", '5', "T17 y4 $tag", 'A => B => C; D => E', '', ''), 'ANSWERS'),
        @('empty left side', @("$subject", "$level", '5', "T17 y5 $tag", '=> B; C => D', '', ''), 'ANSWERS'),
        @('trailing semicolon', @("$subject", "$level", '5', "T17 y6 $tag", 'A => 1; B => 2;', '', ''), 'ANSWERS'))
    foreach ($c in $codes) {
        $r = Upload (Book "$($c[0] -replace ' ', '_').xlsx" @(, $c[1]) $header7) $tokenE
        Expect "$($r.status)|$(@($r.body.data.errors.code) -join ',')" "400|$($c[2])" $c[0]
    }
    # Review T17: CorrectIndexes chỉ có khoảng trắng ở loại 6 = mọi mệnh đề sai; nội dung có "=" và ">" (không phải "=>") giữ nguyên
    $ws = Upload (Book 'ws.xlsx' @(@("$subject", "$level", '6', "T17 nhập trắng $tag", 'P2; Q2', '   ', ''),
        @("$subject", "$level", '5', "T17 nhập ký hiệu $tag", 'a>b => c=d; x = y => p > q', '', '')) $header7) $tokenE
    Expect "$($ws.status)|$($ws.body.data.imported)" '200|2' 'whitespace indexes + symbols'
    Expect (Sql "SELECT STRING_AGG(CONCAT(a.Content,'|',a.MatchContent),',') WITHIN GROUP (ORDER BY a.Id) FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject AND q.Content=N'T17 nhập ký hiệu $tag';") 'a>b|c=d,x = y|p > q' 'symbols kept'
    Pass 'xuất cặp "trái => phải" và chỉ số mệnh đề đúng; nhập loại 5–6 (mệnh đề sai hết được); lỗi rõ ràng'

    Write-Output '== Loại câu hệ thống 5–6 bị khóa'
    Expect (Call 'PUT' '/web/question-types/5' $tokenE @{ name = 'X'; description = 'X'; status = $true }).status 409 'cannot edit type 5'
    Expect (Call 'DELETE' '/web/question-types/6' $tokenE).status 409 'cannot delete type 6'
    Pass 'sửa/xóa loại 5–6 → 409'
    Write-Output "TOBE_T17_MATCHING_STATEMENTS_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "'$seed','$studentB','$examiner'"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    if ($exam -gt 0) { Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam;
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" }
    if ($subject -gt 0) { Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T17-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T17-$tag';
      DELETE dbo.QuestionTags WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE SubjectId=$subject);
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N't17seed$tag@example.invalid';"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
