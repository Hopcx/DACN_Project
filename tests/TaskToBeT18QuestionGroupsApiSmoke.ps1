# To-be T18 - Doan dung chung: CRUD + media cua doan, ma de (chon tay du doan / ngau nhien nguyen doan), bai thi (doan + media, snapshot v7),
# khoa khi da vao de, luyen tap (vuot nhe, lam lai, doan an), Excel bo qua cau thuoc doan, media doan khong mo coi.
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao đã áp migration T18. Chỉ ghi bản sao; dọn trong finally.
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
      SELECT '$id',N'T18 $name',N't18$name$tag',N't18$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t18$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }

# Workbook tối giản (định dạng v1 + cột G tùy chọn)
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t18-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
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

$mediaRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location) 'dacn-media-dev'))
function Png([int]$w, [int]$h) {
    $b = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0,0,13,0x49,0x48,0x44,0x52) + [BitConverter]::GetBytes([uint32]$w)[3..0] + [BitConverter]::GetBytes([uint32]$h)[3..0]
    return [byte[]]($b + [Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
}
$seed = [Guid]::Empty; $examiner = [Guid]::NewGuid()
$subject = 0; $subject2 = 0; $exam = 0; $exam2 = 0; $mail = $null
try {
    $email = "t18seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T18 A'; userName = "t18seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $tokenA = [string]$login.body.data.accessToken
    $examinerEmail = CloneUser $examiner 2 'examiner' $seed
    Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',1),('$examiner',2);" | Out-Null
    $tokenE = LoginToken $examinerEmail
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Subjects(Name,Status) VALUES (N'T18 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Subjects(Name,Status) VALUES (N'T18 môn khác $tag',1); DECLARE @s2 int=SCOPE_IDENTITY();
DECLARE @i int=1, @q int, @list nvarchar(200)=N'';
WHILE @i <= 6 BEGIN
  INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (CONCAT(N'T18 câu ',@i,N' $tag'),SYSUTCDATETIME(),1,@s,2); SET @q=SCOPE_IDENTITY();
  INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
  SET @list = CONCAT(@list, @q, N','); SET @i += 1;
END
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T18 câu môn khác $tag',SYSUTCDATETIME(),1,@s2,2); SET @q=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
COMMIT;
SELECT CONCAT(@s,'|',@s2,'|',@list,@q);
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $subject2 = [int]$p[1]; $q = @($p[2].Split(',') | ForEach-Object { [int]$_ })
    # $q[0..5] = câu 1–6 của môn; $q[6] = câu môn khác

    Write-Output '== Đoạn dùng chung: tạo, kiểm tra, media, sắp xếp, ẩn/hiện'
    function GroupBody([int[]]$questionIds, [string]$title = "T18 đoạn $tag", [string]$content = 'Đọc đoạn văn sau và trả lời các câu hỏi.', [int]$subjectId = $subject) {
        return @{ subjectId = $subjectId; title = $title; content = $content; questionIds = @($questionIds) }
    }
    $g = Call 'POST' '/web/question-groups' $tokenE (GroupBody @($q[0], $q[1], $q[2]))
    Expect "$($g.status)|$(@($g.body.data.questions | ForEach-Object { $_.id }) -join ',')|$($g.body.data.locked)" "201|$($q[0]),$($q[1]),$($q[2])|False" 'create group'
    $groupId = [int]$g.body.data.id
    $bad = @(
        @('1 question', (GroupBody @($q[3])), 400),
        @('duplicate ids', (GroupBody @($q[3], $q[3])), 400),
        @('empty title', (GroupBody @($q[3], $q[4]) ' '), 400),
        @('content > 8000', (GroupBody @($q[3], $q[4]) "T18 x $tag" ('x' * 8001)), 400),
        @('other subject question', (GroupBody @($q[3], $q[6])), 400),
        @('question in other group', (GroupBody @($q[0], $q[3])), 409))
    foreach ($case in $bad) { Expect (Call 'POST' '/web/question-groups' $tokenE $case[1]).status $case[2] $case[0] }
    $pngPath = Join-Path $work 'doan.png'; [IO.File]::WriteAllBytes($pngPath, (Png 320 200))
    $up = Call 'POST' '/web/media' $tokenE $null @{ file = Get-Item $pngPath }
    if ($up.status -notin 200, 201) { throw "upload $($up.status)" }
    $mediaId = $up.body.data.id
    $att = Call 'POST' "/web/question-groups/$groupId/media" $tokenE @{ mediaId = $mediaId }
    Expect "$($att.status)|$(@($att.body.data.media).Count)" '200|1' 'attach group media'
    Expect (Call 'POST' "/web/question-groups/$groupId/media" $tokenE @{ mediaId = $mediaId }).body.message 'MEDIA_ALREADY_ATTACHED' 'attach twice'
    $re = Call 'PUT' "/web/question-groups/$groupId" $tokenE (GroupBody @($q[2], $q[0], $q[1]))
    Expect "$($re.status)|$(@($re.body.data.questions | ForEach-Object { "$($_.id):$($_.order)" }) -join ',')" "200|$($q[2]):1,$($q[0]):2,$($q[1]):3" 'reorder group'
    $list = (Call 'GET' "/web/questions?subjectId=$subject" $tokenE).body.data
    Expect (@($list | Where-Object { $_.groupId -eq $groupId -and $_.groupTitle -eq "T18 đoạn $tag" }).Count) 3 'question list carries group'
    Expect (Call 'DELETE' "/web/questions/$($q[0])" $tokenE).status 409 'cannot delete grouped question'
    Expect (Call 'PUT' "/web/question-groups/$groupId/status" $tokenE @{ status = 0 }).body.data.status 0 'hide group'
    Pass 'tạo 201 (thứ tự câu con), 400/409 cho dữ liệu sai, media của đoạn, sắp xếp lại, câu thuộc đoạn không xóa được'

    Write-Output '== Mã đề: chọn tay phải đủ đoạn; ngẫu nhiên lấy nguyên đoạn'
    $exams = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T18 đề $tag',$subject,4,1,2,4,1,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T18 đề 2 $tag',$subject,4,1,2,4,1,30,1);
COMMIT;
SELECT CONCAT(@e,'|',SCOPE_IDENTITY());
"@
    $exam = [int]$exams.Split('|')[0]; $exam2 = [int]$exams.Split('|')[1]
    Expect (Call 'POST' "/web/exams/$exam/variants" $tokenE @{ code = 'H'; status = 2; questionIds = @($q[0], $q[1], $q[2], $q[3]); randomSelections = @() }).status 400 'hidden group not selectable'
    Expect (Call 'PUT' "/web/question-groups/$groupId/status" $tokenE @{ status = 1 }).body.data.status 1 'show group'
    $partial = Call 'POST' "/web/exams/$exam/variants" $tokenE @{ code = 'P'; status = 2; questionIds = @($q[0], $q[3], $q[4], $q[5]); randomSelections = @() }
    Expect "$($partial.status)|$($partial.body.message -like 'GROUP_INCOMPLETE*')" '400|True' 'manual partial group rejected'
    $manual = Call 'POST' "/web/exams/$exam/variants" $tokenE @{ code = 'M'; status = 2; questionIds = @($q[3], $q[0], $q[4], $q[1]); randomSelections = @() }
    # q[2] chưa chọn → vẫn thiếu đoạn
    Expect $manual.status 400 'manual missing one child rejected'
    $manual = Call 'POST' "/web/exams/$exam/variants" $tokenE @{ code = 'M'; status = 2; questionIds = @($q[3], $q[1], $q[0], $q[2]); randomSelections = @() }
    Expect "$($manual.status)|$(@($manual.body.data.questions | ForEach-Object { $_.questionId }) -join ',')" "201|$($q[3]),$($q[2]),$($q[0]),$($q[1])" 'manual whole group, contiguous in group order'
    # Ngẫu nhiên 4 câu từ 3 câu lẻ + đoạn 3 câu → bắt buộc lấy nguyên đoạn + 1 câu lẻ
    foreach ($k in 1..3) {
        $rnd = Call 'POST' "/web/exams/$exam2/variants" $tokenE @{ code = "R$k"; status = 2; questionIds = @(); randomSelections = @(@{ count = 4 }) }
        $picked = [int[]]@($rnd.body.data.questions | ForEach-Object { [int]$_.questionId })
        $pos = @($q[2], $q[0], $q[1] | ForEach-Object { [array]::IndexOf($picked, $_) })
        if ($rnd.status -ne 201 -or ($pos | Where-Object { $_ -lt 0 }) -or ($pos[1] -ne $pos[0] + 1) -or ($pos[2] -ne $pos[1] + 1)) { throw "random variant R$k must contain whole group contiguous: $($picked -join ',')" }
    }
    Pass 'đoạn ẩn không chọn được; chọn tay thiếu câu con → GROUP_INCOMPLETE; đủ đoạn → câu con liền nhau theo thứ tự; ngẫu nhiên lấy nguyên đoạn'

    Write-Output '== Bài thi: đoạn văn + media cho học viên, snapshot v7, xem lại'
    $sched = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE dbo.Exams SET Status=1 WHERE Id=$exam; UPDATE dbo.ExamDetails SET Status=1 WHERE ExamId=$exam;
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T18 $tag',N'T18-$tag',5,'$examiner',$subject,1);
DECLARE @c int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@c,'$seed',1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES ($exam,N'T18 lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,30,SYSUTCDATETIME()),1,1);
DECLARE @sc int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES (@c,@sc);
COMMIT;
SELECT @sc;
"@
    $schedule = [int]$sched
    $start = Call 'POST' "/web/exam-schedules/$schedule/attempts" $tokenA
    Expect $start.status 200 'start exam'
    $attempt = $start.body.data.attemptId
    $grp = @($start.body.data.groups)
    $order = @($start.body.data.questions | ForEach-Object { $_.content -replace " $tag", '' })
    Expect "$($grp.Count)|$($grp[0].title)|$(@($grp[0].media).Count)|$($order -join ',')" "1|T18 đoạn $tag|1|T18 câu 4,T18 câu 3,T18 câu 1,T18 câu 2" 'groups + order'
    Expect (@($start.body.data.questions | Where-Object { $_.groupId -eq $grp[0].id }).Count) 3 'children carry groupId'
    Expect (Call 'GET' $grp[0].media[0].url).status 200 'group media served to student'
    Expect (Sql "SELECT CONCAT(JSON_VALUE(SnapshotJson,'$.version'),'|',(SELECT COUNT(*) FROM OPENJSON(SnapshotJson,'$.groups'))) FROM dbo.ExamAttempts WHERE Id='$attempt';") '7|1' 'snapshot v7 with group'
    Expect (Sql "SELECT COUNT(*) FROM dbo.MediaReferences WHERE SourceKind='exam' AND SourceId='$attempt' AND MediaId='$mediaId';") '1' 'group media referenced'
    $answers = @($start.body.data.questions | ForEach-Object { @{ questionId = $_.id; selectedAnswerIds = @(@($_.options | Where-Object content -eq 'OK')[0].id) } })
    $save = Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $start.body.data.revision; answers = $answers }
    Expect (Call 'POST' "/web/attempts/$attempt/submit" $tokenA @{ revision = $save.body.data.revision }).status 200 'submit'
    Expect (Sql "SELECT CAST(TotalMark AS decimal(5,2)) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$attempt';") '4.00' 'score unaffected by groups'
    Expect (@((Call 'GET' "/web/attempts/$attempt" $tokenA).body.data.groups).Count) 0 'finished attempt state has no groups'
    Sql "UPDATE dbo.ExamSchedules SET StartTime=DATEADD(hour,-2,SYSUTCDATETIME()), EndTime=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=$schedule;" | Out-Null
    $rv = (Call 'GET' "/web/attempts/$attempt/review" $tokenA).body.data
    Expect "$(@($rv.groups).Count)|$(@($rv.questions | Where-Object { $_.groupId -eq $rv.groups[0].id }).Count)" '1|3' 'review has groups'
    Pass 'học viên thấy đoạn văn + media (URL ký), câu con liền nhau; snapshot v7; MediaReferences gồm media của đoạn; điểm không đổi; xem lại có đoạn'

    Write-Output '== Đoạn đã vào đề bị khóa; media đoạn không mồ côi'
    Expect (Call 'PUT' "/web/question-groups/$groupId" $tokenE (GroupBody @($q[0], $q[1], $q[2]))).body.message 'GROUP_LOCKED' 'locked update'
    Expect (Call 'DELETE' "/web/question-groups/$groupId" $tokenE).body.message 'GROUP_LOCKED' 'locked delete'
    Expect (Call 'DELETE' "/web/question-groups/$groupId/media/$mediaId" $tokenE).body.message 'GROUP_LOCKED' 'locked media'
    Expect (Call 'GET' "/web/question-groups/$groupId" $tokenE).body.data.locked 'True' 'view locked'
    Sql "UPDATE dbo.MediaFiles SET UploadedAtUtc=DATEADD(day,-2,SYSUTCDATETIME()) WHERE Id='$mediaId';" | Out-Null
    Pass 'sửa/xóa/media → GROUP_LOCKED khi câu con đã vào đề'

    Write-Output '== Luyện tập: vượt nhẹ để không cắt đoạn; làm lại giữ đoạn; đoạn ẩn bị loại'
    $pr = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 5 }
    $n = @($pr.body.data.questions).Count
    # Review T18: ghép được đúng số câu thì không vượt (đoạn 3 + 2 câu lẻ = 5)
    if ($pr.status -ne 201 -or $n -ne 5) { throw "practice 5 → expected exactly 5 questions, got $($pr.status)/$n" }
    $pgroups = @($pr.body.data.groups)
    $idx = @(0..($n - 1) | Where-Object { $pr.body.data.questions[$_].groupId })
    if ($pgroups.Count -ne 1 -or $idx.Count -ne 3 -or $idx[2] - $idx[0] -ne 2) { throw "practice group must be whole and contiguous: $($idx -join ',')" }
    Expect (Call 'POST' "/web/student/practice/$($pr.body.data.id)/finish" $tokenA @{ revision = $pr.body.data.revision }).status 200 'finish practice'
    $retry = Call 'POST' "/web/student/practice/$($pr.body.data.id)/retry" $tokenA
    $ridx = @(0..(@($retry.body.data.questions).Count - 1) | Where-Object { $retry.body.data.questions[$_].groupId })
    Expect "$($retry.status)|$(@($retry.body.data.groups).Count)|$($ridx.Count)|$($ridx[2] - $ridx[0])" '201|1|3|2' 'retry keeps group contiguous'
    Expect (Call 'DELETE' "/web/student/practice/$($retry.body.data.id)" $tokenA).status 200 'abandon retry'
    $tooMany = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 7 }
    Expect "$($tooMany.body.message)|$($tooMany.body.data.available)" 'PRACTICE_NOT_ENOUGH_QUESTIONS|6' 'not enough'
    Sql "UPDATE dbo.QuestionGroups SET Status=0 WHERE Id=$groupId;" | Out-Null
    $hidden = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 3 }
    Expect "$($hidden.status)|$(@($hidden.body.data.groups).Count)|$(@($hidden.body.data.questions | Where-Object { $_.groupId }).Count)" '201|0|0' 'hidden group excluded'
    Expect (Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 4 }).body.message 'PRACTICE_NOT_ENOUGH_QUESTIONS' 'hidden group children not used alone'
    Sql "UPDATE dbo.QuestionGroups SET Status=1 WHERE Id=$groupId;" | Out-Null
    Pass 'số câu 5 → đúng 5 (đoạn liền khối); làm lại giữ đoạn; thiếu câu → 409; đoạn ẩn bị loại cả khối'

    Write-Output '== Review T18: câu con bị ẩn, mức độ lẫn nhau, xuất toàn câu thuộc đoạn'
    # Câu con bị ẩn → đoạn không bao giờ đủ: không hiện trong trình chọn mã đề, form luyện tập không đếm, không có hasGroups
    Sql "UPDATE dbo.Questions SET Status=0 WHERE Id=$($q[0]);" | Out-Null
    $options = (Call 'GET' "/web/exams/$exam2/question-options" $tokenE).body.data
    Expect (@($options | Where-Object { $_.groupId }).Count) 0 'options exclude group with hidden child'
    $popt = @((Call 'GET' '/web/student/practice/options' $tokenA).body.data.subjects | Where-Object id -eq $subject)[0]
    Expect "$($popt.questionCount)|$($popt.hasGroups)" '3|False' 'practice options count units only'
    Sql "UPDATE dbo.Questions SET Status=1 WHERE Id=$($q[0]);" | Out-Null
    $popt = @((Call 'GET' '/web/student/practice/options' $tokenA).body.data.subjects | Where-Object id -eq $subject)[0]
    Expect "$($popt.questionCount)|$($popt.hasGroups)" '6|True' 'practice options with whole group'
    # Mức độ: câu lẻ + 1 câu con ở mức L1, 2 câu con còn lại ở L2 → luật L1 lấy 4 câu thiếu vì phải bỏ đoạn
    $levels = @((Sql "SELECT STRING_AGG(CONVERT(nvarchar(10),Id),',') WITHIN GROUP (ORDER BY Id) FROM (SELECT TOP 2 Id FROM dbo.QuestionLevels WHERE Status=1 ORDER BY Id) l;") -split ',')
    Sql "UPDATE dbo.Questions SET QuestionLevelId=$($levels[0]) WHERE SubjectId=$subject; UPDATE dbo.Questions SET QuestionLevelId=$($levels[1]) WHERE Id IN ($($q[0]),$($q[2]));" | Out-Null
    $exam3 = Sql "INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T18 đề 3 $tag',$subject,4,1,2,4,1,30,1); SELECT SCOPE_IDENTITY();"
    $lv = Call 'POST' "/web/exams/$([int]$exam3)/variants" $tokenE @{ code = 'L'; status = 2; questionIds = @(); randomSelections = @(@{ questionLevelId = [int]$levels[0]; count = 4 }) }
    Expect "$($lv.status)|$($lv.body.message -like 'NOT_ENOUGH_QUESTIONS_FOR_GROUPS*')" '409|True' 'mixed-level group excluded → group-specific error'
    Sql "DELETE dbo.Exams WHERE Id=$([int]$exam3);" | Out-Null
    # Xuất theo mức độ L2 sau khi cả đoạn ở L2 → chỉ có câu thuộc đoạn
    Sql "UPDATE dbo.Questions SET QuestionLevelId=$($levels[1]) WHERE Id=$($q[1]);" | Out-Null
    $og = Invoke-WebRequest "$BaseUrl/web/questions/export?subjectId=$subject&questionLevelId=$($levels[1])" -Headers @{ Authorization = "Bearer $tokenE" } -SkipCertificateCheck -SkipHttpErrorCheck
    Expect "$([int]$og.StatusCode)|$(([Text.Encoding]::UTF8.GetString($og.RawContentStream.ToArray()) | ConvertFrom-Json).message)|$($og.Headers['X-Skipped-Grouped'])" '404|ONLY_GROUPED_QUESTIONS|3' 'export only grouped'
    Pass 'đoạn có câu con bị ẩn không hiện/không đếm; mức độ lẫn → NOT_ENOUGH_QUESTIONS_FOR_GROUPS; xuất toàn câu thuộc đoạn → ONLY_GROUPED_QUESTIONS'

    Write-Output '== Excel bỏ qua câu thuộc đoạn; media đoạn không mồ côi'
    # Xuất Excel yêu cầu câu có mức độ (luật P4)
    Sql "UPDATE dbo.Questions SET QuestionLevelId=(SELECT TOP 1 Id FROM dbo.QuestionLevels WHERE Status=1 ORDER BY Id) WHERE SubjectId=$subject;" | Out-Null
    $r = Invoke-WebRequest "$BaseUrl/web/questions/export?subjectId=$subject" -Headers @{ Authorization = "Bearer $tokenE" } -SkipCertificateCheck -SkipHttpErrorCheck
    Expect "$([int]$r.StatusCode)|$($r.Headers['X-Skipped-Grouped'])" '200|3' 'export skips grouped'
    $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($r.RawContentStream.ToArray()))
    $sheet = [IO.StreamReader]::new($zip.GetEntry('xl/worksheets/sheet1.xml').Open()).ReadToEnd(); $zip.Dispose()
    if ($sheet.Contains("T18 câu 1 $tag") -or -not $sheet.Contains("T18 câu 4 $tag")) { throw 'export content wrong' }
    $adminEmail = CloneUser ([Guid]::NewGuid()) 1 'admin' $seed
    $adminToken = LoginToken $adminEmail
    $orphans = (Call 'GET' '/web/admin/media/orphans?pageSize=100' $adminToken).body.data.items
    if (@($orphans | Where-Object id -eq $mediaId).Count -ne 0) { throw 'group media listed as orphan' }
    Expect (Call 'DELETE' "/web/admin/media/$mediaId" $adminToken).body.message 'MEDIA_IN_USE' 'group media in use'
    Pass 'Excel bỏ qua 3 câu thuộc đoạn (header X-Skipped-Grouped); media của đoạn không bị coi là mồ côi'
    Write-Output "TOBE_T18_QUESTION_GROUPS_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $users = "'$seed','$examiner'"
    $extra = try { Sql "SELECT STRING_AGG(CONCAT('''',CONVERT(nvarchar(36),Id),''''),',') FROM dbo.Users WHERE UserName LIKE N't18%$tag';" } catch { '' }
    if ($extra) { $users = "$users,$extra" }
    $mediaIds = try { Sql "SELECT STRING_AGG(CONVERT(nvarchar(36),Id),',') FROM dbo.MediaFiles WHERE UploadedBy='$examiner';" } catch { '' }
    Clean "DELETE dbo.MediaReferences WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner');"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    foreach ($e in @($exam, $exam2) | Where-Object { $_ -gt 0 }) {
        Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$e);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$e); DELETE dbo.ExamAttempts WHERE ExamId=$e;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$e); DELETE dbo.ExamSchedules WHERE ExamId=$e;
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$e); DELETE dbo.ExamDetails WHERE ExamId=$e; DELETE dbo.Exams WHERE Id=$e;"
    }
    foreach ($s in @($subject, $subject2) | Where-Object { $_ -gt 0 }) {
        Clean "DELETE gm FROM dbo.QuestionGroupMedia gm JOIN dbo.QuestionGroups g ON g.Id=gm.GroupId WHERE g.SubjectId=$s;
      UPDATE dbo.Questions SET GroupId=NULL, GroupOrder=NULL WHERE SubjectId=$s; DELETE dbo.QuestionGroups WHERE SubjectId=$s;
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$s; DELETE dbo.Questions WHERE SubjectId=$s;"
    }
    Clean "DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T18-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T18-$tag';
      DELETE dbo.Subjects WHERE Id IN ($subject,$subject2);"
    Clean "DELETE dbo.QuestionMedia WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner'); DELETE dbo.MediaFiles WHERE UploadedBy='$examiner';"
    foreach ($id in ("$mediaIds" -split ',' | Where-Object { $_ })) {
        $n2 = $id.Replace('-', '').ToLowerInvariant(); Remove-Item -LiteralPath (Join-Path (Join-Path $mediaRoot $n2.Substring(0, 2)) $n2) -ErrorAction SilentlyContinue
    }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N't18seed$tag@example.invalid';"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
