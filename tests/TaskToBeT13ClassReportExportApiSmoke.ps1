# To-be T13 - Xuat bao cao lop ra Excel: GET /web/teacher/classes/{id}/report.xlsx (4 sheet, so lieu khop API ranking/analysis).
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development) trỏ vào DB bản sao. Chỉ ghi vào bản sao; fixture gắn tag, dọn trong finally.
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
function Call([string]$url, [string]$token = '') {
    $args2 = @{ Uri = "$BaseUrl$url"; Method = 'GET'; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($token) { $args2.Headers = @{ Authorization = "Bearer $token" } }
    $r = Invoke-WebRequest @args2
    $text = if ($r.Headers['Content-Type'] -match 'json') { $r.Content } else { $null }
    return @{ status = [int]$r.StatusCode; body = if ($text) { $text | ConvertFrom-Json } else { $null }; bytes = $r.RawContentStream.ToArray();
        type = [string]($r.Headers['Content-Type'] | Select-Object -First 1); disposition = [string]($r.Headers['Content-Disposition'] | Select-Object -First 1) }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected '$expected', got '$actual'" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'T13 $name',N't13$name$tag',N't13$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t13$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }
# Đọc xlsx: tên sheet + mỗi sheet là map "A1" -> @{ t = 'n'|'s'; v = giá trị }
function ReadBook([byte[]]$bytes) {
    $S = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($bytes))
    try {
        $texts = @{}
        foreach ($entry in $zip.Entries) { $reader = [IO.StreamReader]::new($entry.Open()); $texts[$entry.FullName] = $reader.ReadToEnd(); $reader.Dispose() }
        $names = @(([xml]$texts['xl/workbook.xml']).GetElementsByTagName('sheet') | ForEach-Object { $_.GetAttribute('name') })
        $sheets = @(for ($i = 1; $i -le $names.Count; $i++) {
            $xml = [xml]$texts["xl/worksheets/sheet$i.xml"]; $map = @{}
            foreach ($c in $xml.GetElementsByTagName('c', $S)) {
                $isText = $c.GetAttribute('t') -eq 'inlineStr'
                $map[$c.GetAttribute('r')] = @{ t = $(if ($isText) { 's' } else { 'n' }); v = $c.InnerText }
            }
            , $map
        })
        return @{ names = $names; sheets = $sheets; all = ($texts.Values -join "`n") }
    } finally { $zip.Dispose() }
}

$seed = [Guid]::Empty; $teacherA = [Guid]::NewGuid(); $teacherB = [Guid]::NewGuid(); $admin = [Guid]::NewGuid()
$clones = 1..4 | ForEach-Object { [Guid]::NewGuid() }
$exam = 0; $class = 0; $subject = 0; $mail = $null
try {
    $email = "t13seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T13 s0'; userName = "t13seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $tokenA = LoginToken (CloneUser $teacherA 3 'teachera' $seed)
    $tokenB = LoginToken (CloneUser $teacherB 3 'teacherb' $seed)
    $adminToken = LoginToken (CloneUser $admin 1 'admin' $seed)
    for ($i = 0; $i -lt 4; $i++) { CloneUser $clones[$i] 4 "s$($i + 1)" $seed | Out-Null }
    $m = @($seed) + $clones
    $members = (($m | ForEach-Object { "(@class,'$_',1)" }) -join ',')
    $marks = (($m | ForEach-Object -Begin { $k = 0 } -Process { "('$_',$(10 - 2 * $k))"; $k++ }) -join ',')   # 10, 8, 6, 4, 2

    # 27 lịch đã kết thúc + 1 lịch đang mở (không tính). Mỗi học viên 1 lượt/lịch; S4 bỏ lịch L3. Lịch L1: 1 câu chương C (OK đúng).
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$seed';
INSERT dbo.Subjects(Name,Status) VALUES (N'T13 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Chapters(SubjectId,Name,SortOrder,Status,CreatedAt) VALUES (@s,N'T13 C $tag',1,1,SYSUTCDATETIME()); DECLARE @c int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId,ChapterId) VALUES (N'T13 $tag câu',SYSUTCDATETIME(),1,@s,2,@c); DECLARE @q int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK-$tag',1,1); DECLARE @ok int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'NO-$tag',0,1); DECLARE @no int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult)
VALUES(N'T13 đề $tag',@s,1,1,1,10,5,30,1); DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES(@e,N'T13',1,SYSUTCDATETIME(),@user,SYSUTCDATETIME(),@user);
DECLARE @v int=SCOPE_IDENTITY();
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES(N'T13 lớp $tag',N'T13-$tag',20,'$teacherA',@s,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES $members;
DECLARE @past datetime2=DATEADD(day,-3,SYSUTCDATETIME()), @i int=1;
WHILE @i <= 27 BEGIN
  INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,CONCAT(N'L',@i),@past,DATEADD(minute,@i,DATEADD(day,1,@past)),1,1);
  SET @i+=1;
END
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'Đang mở',@past,DATEADD(day,2,SYSUTCDATETIME()),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT @class,Id FROM dbo.ExamSchedules WHERE ExamId=@e;
DECLARE @snap nvarchar(max)=(SELECT 3 AS version, (SELECT 10 AS point, @q AS sourceQuestionId,
  (SELECT x.Id AS sourceAnswerId, CAST(x.Ok AS bit) AS isCorrect FROM (VALUES (@ok,1),(@no,0)) x(Id,Ok) FOR JSON PATH) AS options FOR JSON PATH) AS questions
  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
DECLARE @M TABLE(U uniqueidentifier, Mark int);
INSERT @M VALUES $marks;
DECLARE @AT TABLE(Id uniqueidentifier, U uniqueidentifier, SId int, Title nvarchar(50), Mark int);
INSERT @AT SELECT NEWID(),m.U,s.Id,s.Title,m.Mark FROM @M m CROSS JOIN dbo.ExamSchedules s
  WHERE s.ExamId=@e AND s.Title<>N'Đang mở' AND NOT (m.Mark=2 AND s.Title=N'L3');
INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,FinishedAtUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
SELECT Id,U,SId,@e,@v,@past,DATEADD(minute,30,@past),DATEADD(minute,20,@past),N'submitted',0,@snap,N'[]',5,1 FROM @AT;
INSERT dbo.ExamAttemptSubmissions(ExamAttemptId,SubmittedAtUtc,TimeTakenSeconds,TotalMark,IsPassed,AnswersJson)
SELECT Id,DATEADD(minute,20,@past),60,Mark,CASE WHEN Mark>=5 THEN 1 ELSE 0 END,N'[]' FROM @AT;
INSERT dbo.QuestionOutcomes(SourceKind,SourceId,SnapshotQuestionId,UserId,SubjectId,ExamScheduleId,SourceQuestionId,ChapterId,TagIdsJson,IsCorrect,IsAnswered,SelectedAnswerIdsJson,FinishedAtUtc,ClassificationSource)
SELECT N'exam',Id,NEWID(),U,@s,SId,@q,@c,N'[]',CASE WHEN Mark>=6 THEN 1 ELSE 0 END,1,CONCAT('[',CASE WHEN Mark>=6 THEN @ok ELSE @no END,']'),DATEADD(minute,20,@past),N'snapshot'
FROM @AT WHERE Title=N'L1';
COMMIT;
SELECT CONCAT(@s,'|',@e,'|',@class);
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $exam = [int]$p[1]; $class = [int]$p[2]
    $url = "/web/teacher/classes/$class/report.xlsx"

    Write-Output '== Quyền'
    $auditCount = { [int](Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'class.report.export' AND EntityId=N'$class';") }
    Expect (Call $url).status 401 'anonymous'
    Expect (Call $url $studentToken).status 403 'student'
    Expect (Call $url $tokenB).status 404 'other teacher'
    Expect (& $auditCount) 0 'denied calls write no audit'
    Expect (Call $url $adminToken).status 200 'admin'
    Expect ([int](Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'class.report.export' AND EntityId=N'$class' AND ActorUserId='$admin' AND EntityType=N'Class';")) 1 'admin export audited'
    Pass '401/403/404 không ghi audit; Admin 200 + audit'

    Write-Output '== File Excel khớp API ranking/analysis'
    $before = [int](Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'class.report.export' AND EntityId=N'$class' AND ActorUserId='$teacherA';")
    $r = Call $url $tokenA
    Expect $r.status 200 'teacher A'
    Expect $r.type 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 'content type'
    if ($r.disposition -notmatch "bao-cao-lop-T13-$tag-\d{8}-\d{4}\.xlsx") { throw "file name: $($r.disposition)" }
    Expect ([int](Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'class.report.export' AND EntityId=N'$class' AND ActorUserId='$teacherA' AND EntityType=N'Class';")) ($before + 1) 'audit row'
    $book = ReadBook $r.bytes
    Expect ($book.names -join '|') 'Xếp hạng lớp|Theo chương|Câu hay sai|Thông tin' 'sheet names'
    if ($book.all -match '<f>|<f ' -or $book.all -match [regex]::Escape("OK-$tag")) { throw 'Workbook has a formula or the correct answer' }
    $rk = $book.sheets[0]
    if (-not $rk['AH1'].v.StartsWith('L27 (kết thúc ')) { throw "AH1 header: $($rk['AH1'].v)" }   # 7 cột cố định + 27 lịch → H..AH
    Expect $rk['AI1'].v 'Điểm tổng (thang 10)' 'total header after schedule columns'
    Expect "$($rk['AJ1'].v)|$($rk['AK1'].v)|$($null -eq $rk['AL1'])" 'Số lần đạt|Nhóm|True' 'last headers AJ/AK, nothing after'
    $api = (Call "/web/teacher/classes/$class/ranking" $tokenA).body.data
    for ($row = 0; $row -lt 5; $row++) {
        $s = $api.students[$row]; $n = $row + 2
        Expect "$($rk["B$n"].v)|$($rk["C$n"].v)|$($rk["E$n"].v)" "$($s.rank)|$($s.userName)|$($s.email)" "ranking row $n"
        Expect ([decimal]$rk["AI$n"].v) ([decimal]$s.total) "total row $n"
        Expect $rk["AI$n"].t 'n' "total is a number row $n"
        Expect "$($rk["AJ$n"].v)|$($rk["AJ$n"].t)" "$($s.passedCount)|n" "passed count row $n"
        $group = if ($s.group -eq 'top') { 'Top' } elseif ($s.group -eq 'bottom') { 'Bottom' } else { '—' }
        Expect $rk["AK$n"].v $group "group row $n"
    }
    Expect $rk['J6'].v $null 'S4 skipped L3 → empty score cell (J = L3)'
    Expect "$($rk['AK2'].v)|$($rk['AK6'].v)" 'Top|Bottom' 'groups'
    $an = (Call "/web/teacher/classes/$class/analysis" $tokenA).body.data
    $ch = $book.sheets[1]
    Expect "$($ch['A2'].v)|$($ch['B2'].v)|$([decimal]$ch['D2'].v)|$($ch['F2'].v)" "T13 C $tag|$($an.chapters[0].total)|$([decimal]$an.chapters[0].rate)|$($an.chapters[0].weakCount)" 'chapter row = analysis'
    Expect $ch['D2'].t 'n' 'rate is a number'
    Expect @($book.sheets[2].Keys | Where-Object { $_ -match '^[A-Z]+2$' }).Count 0 'no hard question rows (< 10 lượt)'
    $info = $book.sheets[3]
    $infoText = (@($info.Values | ForEach-Object { $_.v }) -join "`n")
    foreach ($needle in "T13 lớp $tag", "T13-$tag", "T13 môn $tag", 'T13 teachera', 'Chưa có câu nào đủ 10 lượt', 'không chia sẻ', 'Đang mở (chưa kết thúc)') {
        if (-not $infoText.Contains($needle)) { throw "Info sheet missing '$needle'" }
    }
    Pass '4 sheet; 27 cột lịch vượt Z; hạng/điểm/nhóm khớp ranking; chương khớp analysis; ô số là số; không công thức/đáp án đúng; audit 1 dòng'
    Write-Output "TOBE_T13_CLASS_REPORT_EXPORT_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    if ($exam -gt 0) { Sql "DELETE dbo.QuestionOutcomes WHERE SourceId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam);
      DELETE dbo.ExamSchedules WHERE ExamId=$exam; DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" | Out-Null }
    if ($class -gt 0) { Sql "DELETE dbo.ClassUsers WHERE ClassId=$class; DELETE dbo.Classes WHERE Id=$class;" | Out-Null }
    if ($subject -gt 0) { Sql "DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject;
      DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Chapters WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" | Out-Null }
    $users = (@($seed, $teacherA, $teacherB, $admin) + $clones | ForEach-Object { "'$_'" }) -join ','
    Sql "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.QuestionOutcomes WHERE UserId IN ($users);
      DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users);
      DELETE dbo.Users WHERE Id IN ($users) OR Email=N't13seed$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
