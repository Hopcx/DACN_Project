# To-be T15 - Media phia hoc vien: snapshot v4 + MediaReferences, URL ky tam khi lam bai/xem lai, media mo coi (Admin).
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development, Media:RootPath mặc định ../dacn-media-dev) trỏ vào DB bản sao đã áp migration T15.
# Chỉ ghi vào bản sao; fixture gắn tag, dọn trong finally (kể cả file media trên đĩa).
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
$mediaRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location) 'dacn-media-dev'))
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
function Call([string]$method, [string]$url, [string]$token = '', $json = $null, $form = $null, [hashtable]$headers = @{}) {
    $h = @{} + $headers
    if ($token) { $h.Authorization = "Bearer $token" }
    $p = @{ Uri = "$BaseUrl$url"; Method = $method; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true; Headers = $h }
    if ($null -ne $json) { $p.ContentType = 'application/json'; $p.Body = ($json | ConvertTo-Json -Compress -Depth 8) }
    if ($null -ne $form) { $p.Form = $form }
    $r = Invoke-WebRequest @p
    $type = [string]($r.Headers['Content-Type'] | Select-Object -First 1)
    return @{ status = [int]$r.StatusCode; body = if ($type -match 'json' -and $r.Content) { $r.Content | ConvertFrom-Json } else { $null };
        bytes = $r.RawContentStream.ToArray(); type = $type; headers = $r.Headers; raw = $(if ($type -match 'json') { [string]$r.Content } else { '' }) }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected '$expected', got '$actual'" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'T15 $name',N't15$name$tag',N't15$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t15$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }

# File mẫu: header thật + đuôi ngẫu nhiên (mỗi lần chạy khác SHA-256)
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t15-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
function Rand([int]$n) { return [Security.Cryptography.RandomNumberGenerator]::GetBytes($n) }
function Png([int]$w, [int]$h) {
    $b = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0,0,13,0x49,0x48,0x44,0x52) + [BitConverter]::GetBytes([uint32]$w)[3..0] + [BitConverter]::GetBytes([uint32]$h)[3..0]
    return [byte[]]($b + (Rand 64))
}
function Save([string]$name, [byte[]]$bytes) { $p = Join-Path $work $name; [IO.File]::WriteAllBytes($p, $bytes); return Get-Item $p }
$mp3 = [byte[]][Text.Encoding]::ASCII.GetBytes("ID3`u{4}`0`0`0`0`0`0") + (Rand 2000)
$files = @{ png = Save 'a.png' (Png 640 480); mp3 = Save 'b.mp3' $mp3; png2 = Save 'c.png' (Png 32 32); png3 = Save 'd.png' (Png 48 48) }

$seed = [Guid]::Empty; $studentB = [Guid]::NewGuid(); $examiner = [Guid]::NewGuid(); $admin = [Guid]::NewGuid(); $teacher = [Guid]::NewGuid()
$subject = 0; $exam = 0; $mail = $null
try {
    $email = "t15seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T15 A'; userName = "t15seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $tokenA = [string]$login.body.data.accessToken
    $tokenB = LoginToken (CloneUser $studentB 4 'b' $seed)
    $examinerEmail = CloneUser $examiner 2 'examiner' $seed
    Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);" | Out-Null
    $tokenE = LoginToken $examinerEmail
    $adminToken = LoginToken (CloneUser $admin 1 'admin' $seed)
    $teacherToken = LoginToken (CloneUser $teacher 3 'teacher' $seed)
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @user uniqueidentifier='$seed';
INSERT dbo.Subjects(Name,Status) VALUES (N'T15 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T15 câu đề $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @q1 int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q1,N'OK',1,1),(@q1,N'NO',0,1);
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T15 câu luyện $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @q2 int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q2,N'OK',1,1),(@q2,N'NO',0,1);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'T15 $tag',N'T15-$tag',5,'$teacher',@s,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (SCOPE_IDENTITY(),@user,1);
INSERT dbo.ClassUsers(ClassId,UserId,Status) SELECT Id,'$studentB',1 FROM dbo.Classes WHERE ClassCode=N'T15-$tag';
COMMIT;
SELECT CONCAT(@s,'|',@q1,'|',@q2);
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $q1 = [int]$p[1]; $q2 = [int]$p[2]
    function Up($f) { $r = Call 'POST' '/web/media' $tokenE $null @{ file = $f }; if ($r.status -notin 200, 201) { throw "upload $($f.Name): $($r.status)" }; return $r.body.data }
    $png = Up $files.png; $aud = Up $files.mp3; $png2 = Up $files.png2
    foreach ($m in $png, $aud) { Expect (Call 'POST' "/web/questions/$q1/media" $tokenE @{ mediaId = $m.id }).status 200 "attach q1 $($m.kind)" }
    Expect (Call 'POST' "/web/questions/$q2/media" $tokenE @{ mediaId = $png2.id }).status 200 'attach q2 png'
    # Đề thi chứa q1 (sau khi gắn media), lịch đang mở, cho xem kết quả
    $sched = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T15 đề $tag',$subject,1,1,1,1,1,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'T15',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (SCOPE_IDENTITY(),$q1,1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@e,N'T15 lịch',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,30,SYSUTCDATETIME()),1,1);
DECLARE @sc int=SCOPE_IDENTITY();
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) SELECT Id,@sc FROM dbo.Classes WHERE ClassCode=N'T15-$tag';
COMMIT;
SELECT CONCAT(@e,'|',@sc);
"@
    $exam = [int]$sched.Split('|')[0]; $schedule = [int]$sched.Split('|')[1]

    Write-Output '== Bài thi: snapshot v4 + MediaReferences + URL cho chính học viên'
    $start = Call 'POST' "/web/exam-schedules/$schedule/attempts" $tokenA
    Expect $start.status 200 'start exam'
    $attempt = $start.body.data.attemptId; $qm = @($start.body.data.questions[0].media)
    Expect (($qm | ForEach-Object { "$($_.id)/$($_.kind)" }) -join ',') "$($png.id)/image,$($aud.id)/audio" 'media in order'
    Expect (Sql "SELECT JSON_VALUE(SnapshotJson,'$.version') FROM dbo.ExamAttempts WHERE Id='$attempt';") '7' 'snapshot v7 (T18)'
    Expect (Sql "SELECT COUNT(*) FROM dbo.MediaReferences WHERE SourceKind='exam' AND SourceId='$attempt';") '2' 'MediaReferences for attempt'
    $g = Call 'GET' $qm[0].url
    Expect "$($g.status)|$($g.type)" '200|image/png' 'student URL serves image'
    $t = [Uri]::UnescapeDataString(($qm[0].url -split 't=')[1])
    Expect (Call 'GET' "/web/media/$($png2.id)?t=$([Uri]::EscapeDataString($t))").status 403 'student token not valid for other media'
    Expect (Call 'GET' "/web/attempts/$attempt" $tokenB).status 404 'other student cannot read attempt (no URLs)'
    Expect (Call 'DELETE' "/web/questions/$q1/media/$($png.id)" $tokenE).body.message 'QUESTION_LOCKED' 'exam question media locked'
    if ($start.raw -match 'isCorrect|sourceQuestionId|"point"') { throw 'Attempt DTO leaked' }
    $save = Call 'PUT' "/web/attempts/$attempt/answers" $tokenA @{ revision = $start.body.data.revision; answers = @(@{ questionId = $start.body.data.questions[0].id; selectedAnswerIds = @(@($start.body.data.questions[0].options | Where-Object content -eq 'OK')[0].id) }) }
    Expect (Call 'POST' "/web/attempts/$attempt/submit" $tokenA @{ revision = $save.body.data.revision }).status 200 'submit'
    # Review T15 (Q11b): URL là bearer — vẫn dùng được sau khi nộp cho tới khi hết hạn (đã ghi trong runbook)
    Expect (Call 'GET' $qm[0].url).status 200 'bearer URL still valid after submit until expiry'
    Expect @((Call 'GET' "/web/attempts/$attempt" $tokenA).body.data.questions).Count 0 'finished attempt: no questions/URLs'
    Expect (Call 'GET' "/web/attempts/$attempt/review" $tokenA).status 403 'review before schedule end'
    Sql "UPDATE dbo.ExamSchedules SET StartTime=DATEADD(hour,-2,SYSUTCDATETIME()), EndTime=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id=$schedule;" | Out-Null
    $rv = Call 'GET' "/web/attempts/$attempt/review" $tokenA
    Expect "$($rv.status)|$(@($rv.body.data.questions[0].media).Count)" '200|2' 'review has media'
    Expect (Call 'GET' $rv.body.data.questions[0].media[1].url).status 200 'review URL serves audio'
    Sql "UPDATE dbo.ExamAttemptSubmissions SET VoidedAtUtc=SYSUTCDATETIME(),VoidReason=N'T15 QA' WHERE ExamAttemptId='$attempt';" | Out-Null
    Expect (Call 'GET' "/web/attempts/$attempt/review" $tokenA).status 403 'voided attempt: no review/URLs'
    Pass 'v4 + 2 MediaReferences; URL chỉ cho media trong snapshot; người khác 404; đang làm/xem lại có media; đã nộp/chưa công bố/hủy thì không'

    Write-Output '== Luyện tập: snapshot giữ media cũ khi sửa câu; xem lại; làm lại; bỏ phiên'
    $pr = Call 'POST' '/web/student/practice' $tokenA @{ subjectId = $subject; questionCount = 2 }
    Expect $pr.status 201 'create practice'
    $prId = $pr.body.data.id
    Expect (Sql "SELECT COUNT(*) FROM dbo.MediaReferences WHERE SourceKind='practice' AND SourceId='$prId';") '3' 'practice references (q1: 2, q2: 1)'
    Expect (Call 'DELETE' "/web/questions/$q2/media/$($png2.id)" $tokenE).status 200 'detach q2 media after start'
    $get = (Call 'GET' "/web/student/practice/$prId" $tokenA).body.data
    $q2dto = @($get.questions | Where-Object { $_.content -eq "T15 câu luyện $tag" })[0]
    Expect "$(@($q2dto.media).Count)|$($q2dto.media[0].id)" "1|$($png2.id)" 'snapshot keeps media detached later'
    Expect (Call 'GET' $q2dto.media[0].url).status 200 'snapshot media still served'
    Expect (Call 'POST' "/web/student/practice/$prId/finish" $tokenA @{ revision = 0 }).status 200 'finish practice'
    $prv = (Call 'GET' "/web/student/practice/$prId/review" $tokenA).body.data
    Expect (@($prv.questions | ForEach-Object { @($_.media).Count }) | Measure-Object -Sum).Sum 3 'practice review media'
    $retry = Call 'POST' "/web/student/practice/$prId/retry" $tokenA
    Expect $retry.status 201 'retry'
    $rId = $retry.body.data.id
    Expect (Sql "SELECT COUNT(*) FROM dbo.MediaReferences WHERE SourceKind='practice' AND SourceId='$rId';") '2' 'retry snapshots current media (q2 now has none)'
    Expect (Call 'DELETE' "/web/student/practice/$rId" $tokenA).status 200 'abandon retry'
    Expect (Sql "SELECT COUNT(*) FROM dbo.MediaReferences WHERE SourceId='$rId';") '0' 'abandon removes references'
    Pass 'tham chiếu ghi lúc tạo; sửa câu sau đó không đổi phiên; xem lại có media; làm lại chụp media hiện tại; bỏ phiên xóa tham chiếu'

    Write-Output '== Media mồ côi (Admin)'
    $png3 = Up $files.png3
    $orph = { param($tk) Call 'GET' '/web/admin/media/orphans?pageSize=100' $tk }
    Expect (& $orph $tokenE).status 403 'examiner cannot list orphans'
    Expect (& $orph $teacherToken).status 403 'teacher cannot list orphans'
    Expect (@((& $orph $adminToken).body.data.items | Where-Object id -eq $png3.id).Count) 0 'fresh upload (< 24h) not listed'
    Expect (Call 'DELETE' "/web/admin/media/$($png3.id)" $adminToken).body.message 'MEDIA_TOO_RECENT' 'fresh orphan cannot be deleted'
    Sql "UPDATE dbo.MediaFiles SET UploadedAtUtc=DATEADD(day,-2,SYSUTCDATETIME()) WHERE UploadedBy='$examiner';" | Out-Null
    $list = (& $orph $adminToken).body.data
    $listed = @($list.items | ForEach-Object { $_.id })
    if ($listed -notcontains $png3.id) { throw 'old orphan not listed' }
    if ($listed -contains $png.id -or $listed -contains $png2.id) { throw 'media still referenced listed as orphan' }   # png: QuestionMedia+refs; png2: refs only
    Expect (Call 'DELETE' "/web/admin/media/$($png2.id)" $adminToken).body.message 'MEDIA_IN_USE' 'referenced by snapshot → 409'
    # Review T15: tải lại đúng nội dung của bản mồ côi → dùng lại bản cũ và làm mới mốc 24 giờ (không bị xóa ngay sau khi trả Id)
    $re3 = Up $files.png3
    Expect "$($re3.id)|$($re3.duplicate)" "$($png3.id)|True" 'dedupe reuses orphan'
    Expect (Call 'DELETE' "/web/admin/media/$($png3.id)" $adminToken).body.message 'MEDIA_TOO_RECENT' 're-upload refreshes 24h guard'
    Sql "UPDATE dbo.MediaFiles SET UploadedAtUtc=DATEADD(day,-2,SYSUTCDATETIME()) WHERE Id='$($png3.id)';" | Out-Null
    $file3 = Join-Path (Join-Path $mediaRoot $png3.id.Replace('-', '').Substring(0, 2)) $png3.id.Replace('-', '')
    if (-not (Test-Path -LiteralPath $file3)) { throw 'orphan file missing before delete' }
    Expect (Call 'DELETE' "/web/admin/media/$($png3.id)" $adminToken).status 200 'delete orphan'
    Expect "$(Sql "SELECT COUNT(*) FROM dbo.MediaFiles WHERE Id='$($png3.id)';")|$(Test-Path -LiteralPath $file3)" '0|False' 'row and file removed'
    Expect (Sql "SELECT COUNT(*) FROM dbo.Logs WHERE Action=N'media.delete' AND EntityId=N'$($png3.id)' AND ActorUserId='$admin';") '1' 'audit media.delete'
    Expect (Call 'DELETE' "/web/admin/media/$($png3.id)" $adminToken).status 404 'delete missing media → 404'
    Pass 'chỉ Admin; < 24 giờ không liệt kê/không xóa; còn tham chiếu → 409; xóa mất dòng + file; audit'
    Write-Output "TOBE_T15_STUDENT_MEDIA_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $mediaIds = try { Sql "SELECT STRING_AGG(CONVERT(nvarchar(36),Id),',') FROM dbo.MediaFiles WHERE UploadedBy='$examiner';" } catch { '' }
    $users = "'$seed','$studentB','$examiner','$admin','$teacher'"
    Clean "DELETE dbo.MediaReferences WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner');"
    Clean "DELETE dbo.QuestionOutcomes WHERE UserId IN ($users); DELETE dbo.ReviewFlags WHERE UserId IN ($users); DELETE dbo.PracticeSessions WHERE UserId IN ($users);"
    if ($exam -gt 0) { Clean "DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId=$exam);
      DELETE dbo.ExamAttempts WHERE ExamId=$exam;
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId=$exam); DELETE dbo.ExamSchedules WHERE ExamId=$exam;
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId=$exam); DELETE dbo.ExamDetails WHERE ExamId=$exam; DELETE dbo.Exams WHERE Id=$exam;" }
    if ($subject -gt 0) { Clean "DELETE dbo.QuestionMedia WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE SubjectId=$subject);
      DELETE dbo.ClassUsers WHERE ClassId IN (SELECT Id FROM dbo.Classes WHERE ClassCode=N'T15-$tag'); DELETE dbo.Classes WHERE ClassCode=N'T15-$tag';
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" }
    Clean "DELETE dbo.QuestionMedia WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner'); DELETE dbo.MediaFiles WHERE UploadedBy='$examiner';"
    foreach ($id in ("$mediaIds" -split ',' | Where-Object { $_ })) {
        $n = $id.Replace('-', '').ToLowerInvariant(); Remove-Item -LiteralPath (Join-Path (Join-Path $mediaRoot $n.Substring(0, 2)) $n) -ErrorAction SilentlyContinue
    }
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N't15seed$tag@example.invalid';"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
