# To-be T16 - Cau dien ngan (loai 4): soan cau + validator, thi (khong lo dap an, cham, xem lai), luyen tap kiem tra tung cau,
# outcome luu bai lam, Excel import/export cot G.
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao đã áp migration T16. Chỉ ghi bản sao; dọn trong finally.
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
      SELECT '$id',N'T16 $name',N't16$name$tag',N't16$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t16$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }

# Workbook tối giản (định dạng v1 + cột G tùy chọn)
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t16-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
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
    $email = "t16seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T16 A'; userName = "t16seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
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
INSERT dbo.Subjects(Name,Status) VALUES (N'T16 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T16 câu chọn $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @qc int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@qc,N'OK',1,1),(@qc,N'NO',0,1);
SELECT CONCAT(@s,'|',@qc,'|',(SELECT TOP 1 Id FROM dbo.QuestionLevels WHERE Status=1 ORDER BY Id));
COMMIT;
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $qc = [int]$p[1]; $level = [int]$p[2]

    Write-Output '== Soạn câu điền ngắn (validator)'
    function Body([string]$content, [string[]]$accepted, $extra = @{}) {
        $b = @{ subjectId = $subject; questionTypeId = 4; questionLevelId = $level; content = $content; status = 1; tagIds = @()
            answers = @($accepted | ForEach-Object { @{ content = $_; isCorrect = $true } }) }
        foreach ($k in $extra.Keys) { $b[$k] = $extra[$k] }
        return $b
    }
    $c1 = Call 'POST' '/web/questions' $tokenE (Body "T16 thủ đô $tag" @('Hà Nội', 'Thăng Long') @{ shortAnswerIgnoreDiacritics = $true })
    Expect "$($c1.status)|$($c1.body.data.questionTypeId)|$($c1.body.data.shortAnswerIgnoreDiacritics)|$($c1.body.data.shortAnswerNumeric)" '201|4|True|False' 'create short answer'
    $qs1 = [int]$c1.body.data.id
    $c2 = Call 'POST' '/web/questions' $tokenE (Body "T16 gia tốc $tag" @('9.8') @{ shortAnswerNumeric = $true; shortAnswerTolerance = 0.1 })
    Expect "$($c2.status)|$($c2.body.data.shortAnswerTolerance)" '201|0.1' 'create numeric short answer'
    $qs2 = [int]$c2.body.data.id
    $bad = @(
        @('no accepted answer', (Body "T16 x1 $tag" @())),
        @('duplicate after normalize', (Body "T16 x2 $tag" @('Hà Nội', '  hà   nội '))),
        @('duplicate ignoring diacritics', (Body "T16 x3 $tag" @('Hà Nội', 'Ha Noi') @{ shortAnswerIgnoreDiacritics = $true })),
        @('tolerance without numeric', (Body "T16 x4 $tag" @('1') @{ shortAnswerTolerance = 0.5 })),
        @('negative tolerance', (Body "T16 x5 $tag" @('1') @{ shortAnswerNumeric = $true; shortAnswerTolerance = -1 })),
        @('11 accepted answers', (Body "T16 x6 $tag" @(1..11 | ForEach-Object { "a$_" }))),
        @('201 chars', (Body "T16 x7 $tag" @(('x' * 201)))))
    foreach ($case in $bad) { Expect (Call 'POST' '/web/questions' $tokenE $case[1]).status 400 $case[0] }
    $notAccepted = Body "T16 x8 $tag" @('A'); $notAccepted.answers[0].isCorrect = $false
    Expect (Call 'POST' '/web/questions' $tokenE $notAccepted).status 400 'answer not accepted'
    Expect (Call 'PUT' "/web/questions/$qs1" $tokenE (Body "T16 thủ đô $tag" @('Hà Nội', 'Ha Noi') @{ shortAnswerIgnoreDiacritics = $true })).status 400 'edit duplicate'
    $typeList = (Call 'GET' "/web/questions?subjectId=$subject&questionTypeId=4" $tokenE).body.data
    Expect @($typeList).Count 2 'filter by type 4'
    Expect (Sql "SELECT CONCAT(ShortAnswerIgnoreDiacritics,'|',ShortAnswerNumeric,'|',CONVERT(nvarchar(20),ShortAnswerTolerance)) FROM dbo.Questions WHERE Id=$qs2;") '0|1|0.100000' 'stored options'
    Pass '201 + tùy chọn; 400 cho thiếu/trùng/sai số/độ dài/không phải đáp án chấp nhận; lọc loại 4'

    Write-Output '== Bài thi: không lộ đáp án chấp nhận, chấm, xem lại'
    $sched = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T16 đề $tag',$subject,3,1,1,5,1,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'T16',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
DECLARE @d int=SCOPE_IDENTITY();
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@d,$qs1,2),(@d,$qs2,2),(@d,$qc,1);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T16 $tag',N'T16-$tag',5,'$examiner',$subject,1);
DECLARE @c int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@c,'$seed',1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'T16 lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,30,SYSUTCDATETIME()),1,1);
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
    $s1 = $byContent["T16 thủ đô $tag"]; $s2 = $byContent["T16 gia tốc $tag"]; $sc = $byContent["T16 câu chọn $tag"]
    Expect "$($s1.questionTypeId)|$(@($s1.options).Count)|$(@($s2.options).Count)|$(@($sc.options).Count)" '4|0|0|2' 'short answer has no options'
    if ($start.raw -match 'Thăng Long|Th\\u0103ng|"9\.8"|isCorrect|acceptedAnswers') { throw 'Attempt DTO leaked accepted answers' }
    Expect (Sql "SELECT CONCAT(JSON_VALUE(SnapshotJson,'$.version'),'|',(SELECT COUNT(*) FROM OPENJSON(SnapshotJson,'$.questions') WHERE JSON_QUERY([value],'$.shortAnswer') IS NOT NULL)) FROM dbo.ExamAttempts WHERE Id='$attempt';") '7|2' 'snapshot v7 + rules'
    $okChoice = @($sc.options | Where-Object content -eq 'OK')[0].id
    $rev = $start.body.data.revision
    Expect (Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(@{ questionId = $s1.id; selectedAnswerIds = @($okChoice) }) }).status 400 'short answer with selected ids'
    Expect (Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(@{ questionId = $s1.id; selectedAnswerIds = @(); text = ('x' * 201) }) }).status 400 'text > 200'
    Expect (Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(@{ questionId = $sc.id; selectedAnswerIds = @($okChoice); text = 'x' }) }).status 400 'choice with text'
    $save = Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $rev; answers = @(
        @{ questionId = $s1.id; selectedAnswerIds = @(); text = '  thang   LONG ' },
        @{ questionId = $s2.id; selectedAnswerIds = @(); text = '9,75' },
        @{ questionId = $sc.id; selectedAnswerIds = @($okChoice) }) }
    Expect $save.status 200 'save answers'
    $state = (Call 'GET' "/web/attempts/$attempt" $tokenA).body.data
    Expect (@($state.savedAnswers | Where-Object questionId -eq $s1.id)[0].text) 'thang   LONG' 'draft keeps trimmed text'
    if ($state.savedAnswers | Where-Object { $_.questionId -eq $sc.id -and $null -ne $_.text }) { throw 'choice draft must omit text' }
    Expect (Call 'POST' "/web/attempts/$attempt/submit" $tokenA @{ revision = $save.body.data.revision }).status 200 'submit'
    Expect (Sql "SELECT CAST(TotalMark AS decimal(5,2)) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$attempt';") '5.00' 'score 2 + 2 (±0.1) + 1'
    Expect (Sql "SELECT CONCAT(IsCorrect,'|',IsAnswered,'|',AnswerText) FROM dbo.QuestionOutcomes WHERE SourceId='$attempt' AND SourceQuestionId=$qs1;") '1|1|thang long' 'outcome normalized text'
    Expect (Sql "SELECT CONCAT(IsCorrect,'|',ISNULL(AnswerText,'-')) FROM dbo.QuestionOutcomes WHERE SourceId='$attempt' AND SourceQuestionId=$qc;") '1|-' 'choice outcome no text'
    Sql "UPDATE dbo.ExamSchedules SET StartTime=DATEADD(hour,-2,SYSUTCDATETIME()), EndTime=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=$schedule;" | Out-Null
    $rv = Call 'GET' "/web/attempts/$attempt/review" $tokenA
    Expect $rv.status 200 'review'
    $r1 = @($rv.body.data.questions | Where-Object id -eq $s1.id)[0]; $r2 = @($rv.body.data.questions | Where-Object id -eq $s2.id)[0]
    $rc = @($rv.body.data.questions | Where-Object id -eq $sc.id)[0]
    Expect "$($r1.text)|$($r1.isCorrect)|$(@($r1.acceptedAnswers) -join ';')|$(@($r1.options).Count)" 'thang   LONG|True|Hà Nội;Thăng Long|0' 'review short answer'
    Expect "$($r2.isCorrect)|$(@($r2.acceptedAnswers) -join ';')" 'True|9.8' 'review numeric'
    Expect "$($rc.isCorrect)|$($null -eq $rc.acceptedAnswers)|$(@($rc.options).Count)" 'True|True|2' 'review choice unchanged'
    Pass 'không lộ đáp án khi làm; 400 cho nháp sai dạng; điểm 5/5; outcome lưu bài làm đã chuẩn hóa; xem lại có bài làm + đáp án chấp nhận'

    Write-Output '== Luyện tập kiểm tra từng câu'
    $pr = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 3; revealMode = 'perQuestion' }
    Expect $pr.status 201 'create practice (pool includes type 4)'
    $prId = $pr.body.data.id; $prRev = $pr.body.data.revision
    if ($pr.raw -match 'Thăng Long|Th\\u0103ng|"9\.8"|acceptedAnswers') { throw 'Practice DTO leaked accepted answers' }
    $p1 = @($pr.body.data.questions | Where-Object { $_.content -eq "T16 thủ đô $tag" })[0]
    $p2 = @($pr.body.data.questions | Where-Object { $_.content -eq "T16 gia tốc $tag" })[0]
    Expect (Call 'POST' "/web/student/practice/$prId/questions/$($p1.id)/check" $tokenA @{ revision = $prRev; selectedAnswerIds = @(); text = '   ' }).body.message 'INVALID_SELECTION' 'blank check'
    $chk = Call 'POST' "/web/student/practice/$prId/questions/$($p1.id)/check" $tokenA @{ revision = $prRev; selectedAnswerIds = @(); text = 'Huế' }
    Expect "$($chk.status)|$($chk.body.data.result.isCorrect)|$($chk.body.data.result.text)|$(@($chk.body.data.result.acceptedAnswers) -join ';')" '200|False|Huế|Hà Nội;Thăng Long' 'check wrong short answer'
    $prRev = $chk.body.data.revision
    $chk2 = Call 'POST' "/web/student/practice/$prId/questions/$($p2.id)/check" $tokenA @{ revision = $prRev; selectedAnswerIds = @(); text = '9,85' }
    Expect "$($chk2.body.data.result.isCorrect)" 'True' 'check numeric within tolerance'
    $prRev = $chk2.body.data.revision
    $locked = Call 'PUT' "/web/student/practice/$prId/answers" $tokenA @{ revision = $prRev; answers = @(@{ questionId = $p1.id; selectedAnswerIds = @(); text = 'Hà Nội' }) }
    Expect $locked.body.message 'PRACTICE_QUESTION_LOCKED' 'checked text locked'
    Expect (Call 'POST' "/web/student/practice/$prId/finish" $tokenA @{ revision = $prRev }).status 200 'finish practice'
    $prv = (Call 'GET' "/web/student/practice/$prId/review" $tokenA).body.data
    $v1 = @($prv.questions | Where-Object id -eq $p1.id)[0]
    Expect "$($v1.text)|$($v1.isCorrect)|$(@($v1.acceptedAnswers).Count)|$(@($v1.options).Count)" 'Huế|False|2|0' 'practice review'
    Expect (Sql "SELECT AnswerText FROM dbo.QuestionOutcomes WHERE SourceId='$prId' AND SourceQuestionId=$qs1;") 'hue' 'practice outcome (bỏ dấu)'
    Pass 'pool có loại 4; check trống 400; check trả bài làm + đáp án chấp nhận; câu đã chốt khóa; xem lại; outcome'

    Write-Output '== Thi thử có câu điền ngắn (review T16)'
    $mock = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 3; kind = 'mock'; durationMinutes = 10 }
    Expect $mock.status 201 'create mock'
    if ($mock.raw -match 'Thăng Long|Th\\u0103ng|"9\.8"|acceptedAnswers') { throw 'Mock DTO leaked accepted answers' }
    $m1 = @($mock.body.data.questions | Where-Object { $_.content -eq "T16 thủ đô $tag" })[0]
    Expect "$($m1.questionTypeId)|$(@($m1.options).Count)" '4|0' 'mock short answer has no options'
    Expect (Call 'POST' "/web/student/practice/$($mock.body.data.id)/questions/$($m1.id)/check" $tokenA @{ revision = $mock.body.data.revision; selectedAnswerIds = @(); text = 'Hà Nội' }).body.message 'PRACTICE_CHECK_NOT_ALLOWED' 'mock cannot check'
    $msave = Call 'PUT' "/web/student/practice/$($mock.body.data.id)/answers" $tokenA @{ revision = $mock.body.data.revision; answers = @(@{ questionId = $m1.id; selectedAnswerIds = @(); text = 'Hà Nội' }) }
    Expect $msave.status 200 'mock save text'
    Expect (Call 'POST' "/web/student/practice/$($mock.body.data.id)/finish" $tokenA @{ revision = $msave.body.data.revision }).status 200 'finish mock'
    Expect (Sql "SELECT CONCAT(IsCorrect,'|',AnswerText) FROM dbo.QuestionOutcomes WHERE SourceKind='mock' AND SourceId='$($mock.body.data.id)' AND SourceQuestionId=$qs1;") '1|ha noi' 'mock outcome'
    Pass 'thi thử: không lộ đáp án, không check được, lưu/chấm câu điền ngắn'

    Write-Output '== Loại câu hệ thống 1–4 bị khóa (review T16)'
    Expect (Call 'PUT' '/web/question-types/4' $tokenE @{ name = 'X'; description = 'X'; status = $true }).status 409 'cannot edit type 4'
    Expect (Call 'DELETE' '/web/question-types/2' $tokenE).status 409 'cannot delete type 2'
    Expect (Sql "SELECT CONCAT(Name,'|',Status) FROM dbo.QuestionTypes WHERE Id=4;") 'Điền ngắn|1' 'type 4 unchanged'
    Pass 'sửa/xóa loại 1–4 → 409'

    Write-Output '== Excel: xuất/nhập loại 4 (cột G)'
    $ex = Call 'GET' "/web/questions/export?subjectId=$subject&questionTypeId=4" $tokenE
    Expect $ex.status 200 'export type 4'
    $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($ex.bytes))
    $sheet = [IO.StreamReader]::new($zip.GetEntry('xl/worksheets/sheet1.xml').Open()).ReadToEnd(); $zip.Dispose()
    foreach ($needle in 'ShortAnswerOptions', 'nodiacritics', 'numeric=0.1', 'Hà Nội;Thăng Long') { if (-not $sheet.Contains($needle)) { throw "export missing $needle" } }
    $good = Upload (Book 'ok.xlsx' @(, @("$subject", "$level", '4', "T16 nhập $tag", 'Sài Gòn;TP HCM', '', 'nodiacritics; numeric=0.5')) $header7) $tokenE
    Expect "$($good.status)|$($good.body.data.imported)" '200|1' 'import type 4'
    Expect (Sql "SELECT CONCAT(QuestionTypeId,'|',ShortAnswerIgnoreDiacritics,'|',ShortAnswerNumeric,'|',CONVERT(nvarchar(20),ShortAnswerTolerance),'|',(SELECT COUNT(*) FROM dbo.Answers a WHERE a.QuestionId=q.Id AND a.IsCorrect=1)) FROM dbo.Questions q WHERE SubjectId=$subject AND Content=N'T16 nhập $tag';") '4|1|1|0.500000|2' 'imported options'
    $old = Upload (Book 'old.xlsx' @(, @("$subject", "$level", '2', "T16 cũ $tag", 'A;B', '1')) $header7[0..5]) $tokenE
    Expect "$($old.status)|$($old.body.data.imported)" '200|1' '6-column v1 file still imports'
    $codes = @(
        @('bad G', @("$subject", "$level", '4', "T16 y1 $tag", 'A', '', 'abc'), 'SHORT_ANSWER_OPTIONS'),
        @('G on type 2', @("$subject", "$level", '2', "T16 y2 $tag", 'A;B', '1', 'numeric'), 'SHORT_ANSWER_OPTIONS'),
        @('indexes on type 4', @("$subject", "$level", '4', "T16 y3 $tag", 'A', '1', ''), 'CORRECT_INDEXES'))
    foreach ($c in $codes) {
        $r = Upload (Book "$($c[0] -replace ' ', '_').xlsx" @(, $c[1]) $header7) $tokenE
        Expect "$($r.status)|$(@($r.body.data.errors.code) -join ',')" "400|$($c[2])" $c[0]
    }
    Pass 'xuất có cột G; nhập loại 4 + tùy chọn; file 6 cột cũ vẫn nhập; lỗi cột G/CorrectIndexes rõ ràng'
    Write-Output "TOBE_T16_SHORT_ANSWER_SMOKE=PASS;DB=$Database;TAG=$tag"
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
    if ($subject -gt 0) { Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T16-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T16-$tag';
      DELETE dbo.QuestionTags WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE SubjectId=$subject);
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N't16seed$tag@example.invalid';"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
