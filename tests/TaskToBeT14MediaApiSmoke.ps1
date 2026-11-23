# To-be T14 - Media: tai len (magic bytes, gioi han), dung lai khi trung, gan vao cau hoi (<=5, <=1 audio), URL ky tam (Range, header an toan).
param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# Chạy từ thư mục gốc DACN_Project khi API (Development, Media:RootPath mặc định ../dacn-media-dev) trỏ vào DB bản sao đã áp migration T14.
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
    if ($null -ne $json) { $p.ContentType = 'application/json'; $p.Body = ($json | ConvertTo-Json -Compress) }
    if ($null -ne $form) { $p.Form = $form }
    $r = Invoke-WebRequest @p
    $type = [string]($r.Headers['Content-Type'] | Select-Object -First 1)
    return @{ status = [int]$r.StatusCode; body = if ($type -match 'json' -and $r.Content) { $r.Content | ConvertFrom-Json } else { $null };
        bytes = $r.RawContentStream.ToArray(); type = $type; headers = $r.Headers }
}
function Expect($actual, $expected, [string]$case) { if ("$actual" -cne "$expected") { throw "$case expected '$expected', got '$actual'" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
function CloneUser([Guid]$id, [int]$level, [string]$name, [Guid]$seed) {
    Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
      SELECT '$id',N'T14 $name',N't14$name$tag',N't14$name$tag@example.invalid',PasswordHash,$level,1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$seed';" | Out-Null
    return "t14$name$tag@example.invalid"
}
function LoginToken([string]$email) { return [string](Auth 'login' @{ keyword = $email; password = $password }).body.data.accessToken }

# File mẫu: header thật + đuôi ngẫu nhiên (mỗi lần chạy khác SHA-256)
$work = Join-Path ([IO.Path]::GetTempPath()) "dacn-t14-$tag"; New-Item -ItemType Directory -Force $work | Out-Null
function Rand([int]$n) { return [Security.Cryptography.RandomNumberGenerator]::GetBytes($n) }
function Png([int]$w, [int]$h) {
    $b = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0,0,13,0x49,0x48,0x44,0x52) + [BitConverter]::GetBytes([uint32]$w)[3..0] + [BitConverter]::GetBytes([uint32]$h)[3..0]
    return [byte[]]($b + (Rand 64))
}
function Save([string]$name, [byte[]]$bytes) { $p = Join-Path $work $name; [IO.File]::WriteAllBytes($p, $bytes); return Get-Item $p }
$jpeg = [byte[]](0xFF,0xD8,0xFF,0xE0,0x00,0x04,0x00,0x00,0xFF,0xC0,0x00,0x11,0x08,0x01,0xE0,0x02,0x80,3) + (Rand 40)   # 640×480
$mp3 = [byte[]][Text.Encoding]::ASCII.GetBytes("ID3`u{4}`0`0`0`0`0`0") + (Rand 2000)
$mp3b = [byte[]][Text.Encoding]::ASCII.GetBytes("ID3`u{4}`0`0`0`0`0`0") + (Rand 2000)
$files = @{
    png = Save 'a.png' (Png 640 480); png2 = Save 'b.png' (Png 32 32); png3 = Save 'c.png' (Png 64 64); png4 = Save 'd.png' (Png 128 128)
    jpg = Save 'e.jpg' $jpeg; mp3 = Save 'f.mp3' $mp3; mp3b = Save 'g.mp3' $mp3b
    svg = Save 'x.svg' ([Text.Encoding]::UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"))
    fake = Save 'evil.png' ([Text.Encoding]::UTF8.GetBytes('<html><script>alert(1)</script></html>'))
    huge = Save 'huge.png' ([byte[]]((Png 10 10) + (New-Object byte[] (2 * 1024 * 1024))))
    wide = Save 'wide.png' (Png 5000 10)
}

$seed = [Guid]::Empty; $examiner = [Guid]::NewGuid(); $teacher = [Guid]::NewGuid(); $subject = 0; $mail = $null; $media = @()
try {
    $email = "t14seed$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'T14 seed'; userName = "t14seed$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $seed = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    $examinerEmail = CloneUser $examiner 2 'examiner' $seed
    Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);" | Out-Null
    $token = LoginToken $examinerEmail
    $teacherToken = LoginToken (CloneUser $teacher 3 'teacher' $seed)
    $ids = Sql @"
SET XACT_ABORT ON; BEGIN TRAN;
INSERT dbo.Subjects(Name,Status) VALUES (N'T14 môn $tag',1); DECLARE @s int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T14 câu tự do $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @q int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'OK',1,1),(@q,N'NO',0,1);
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T14 câu trong đề $tag',SYSUTCDATETIME(),1,@s,2); DECLARE @locked int=SCOPE_IDENTITY();
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'T14 đề $tag',@s,1,1,1,10,5,30,1);
DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@e,N'T14',1,SYSUTCDATETIME(),'$seed',SYSUTCDATETIME(),'$seed');
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (SCOPE_IDENTITY(),@locked,1);
COMMIT;
SELECT CONCAT(@s,'|',@q,'|',@locked);
"@
    $p = $ids.Split('|'); $subject = [int]$p[0]; $q = [int]$p[1]; $locked = [int]$p[2]
    function Upload($file, [string]$who = $token) { return Call 'POST' '/web/media' $who $null @{ file = $file } }

    Write-Output '== Tải lên: magic bytes, giới hạn, quyền'
    Expect (Call 'POST' '/web/media' '' $null @{ file = $files.png }).status 401 'anonymous upload'
    Expect (Upload $files.png $studentToken).status 403 'student upload'
    Expect (Upload $files.png $teacherToken).status 403 'teacher without permission 2'
    $up = @{}
    foreach ($k in 'png', 'png2', 'png3', 'png4', 'jpg', 'mp3', 'mp3b') {
        $r = Upload $files[$k]; Expect $r.status 201 "upload $k"; $up[$k] = $r.body.data; $media += $r.body.data.id
    }
    Expect "$($up.png.kind)|$($up.png.contentType)|$($up.png.width)x$($up.png.height)|$($up.png.duplicate)" 'image|image/png|640x480|False' 'png meta'
    Expect "$($up.jpg.contentType)|$($up.jpg.width)x$($up.jpg.height)" 'image/jpeg|640x480' 'jpeg meta'
    Expect "$($up.mp3.kind)|$($up.mp3.contentType)" 'audio|audio/mpeg' 'mp3 meta'
    $dup = Upload $files.png
    Expect "$($dup.status)|$($dup.body.data.id)|$($dup.body.data.duplicate)" "200|$($up.png.id)|True" 'duplicate reused'
    # Review T14: trùng nội dung với tên khác → trả tên của lần tải này (không lộ tên gốc của người trước)
    $renamed = Save 'ten-khac.png' ([IO.File]::ReadAllBytes($files.png.FullName))
    Expect (Upload $renamed).body.data.originalName 'ten-khac.png' 'duplicate keeps caller file name'
    # Review T14: vượt trần thân request (giới hạn cấu hình + phần dư) → 413/400, không 500
    $over = Save 'qua-tran.mp3' ([byte[]]([Text.Encoding]::ASCII.GetBytes("ID3`u{4}`0`0`0`0`0`0") + (New-Object byte[] (10 * 1024 * 1024 + 128 * 1024))))
    # Kestrel có thể trả 413 hoặc cắt kết nối ngay khi thân request vượt trần (không đọc hết) — cả hai đều là "từ chối"
    $overStatus = try { (Upload $over).status } catch { 'closed' }
    if ($overStatus -notin 400, 413, 'closed') { throw "over-cap upload returned $overStatus" }
    Expect (Call 'GET' '/web/health/live').status 200 'API alive after over-cap upload'
    Expect (Upload $files.svg).body.message 'MEDIA_TYPE_NOT_ALLOWED' 'svg rejected'
    Expect (Upload $files.fake).body.message 'MEDIA_TYPE_NOT_ALLOWED' 'html named .png rejected'
    $big = Upload $files.huge; Expect "$($big.status)|$($big.body.message)" '413|MEDIA_TOO_LARGE' 'image > 2 MB'
    Expect (Upload $files.wide).body.message 'MEDIA_DIMENSIONS_TOO_LARGE' 'image > 4096 px'
    $path = Join-Path (Join-Path $mediaRoot $up.png.id.Replace('-', '').Substring(0, 2)) $up.png.id.Replace('-', '')
    if (-not (Test-Path -LiteralPath $path)) { throw "File not stored at $path" }
    if ((Get-ChildItem -Recurse -File Project.Api/wwwroot -ErrorAction SilentlyContinue | Where-Object Name -like "*$($up.png.id.Replace('-', ''))*")) { throw 'Media stored in wwwroot' }
    Expect (Sql "SELECT COUNT(*) FROM dbo.MediaFiles WHERE Id IN ('$($media -join "','")') AND UploadedBy='$examiner';") 7 'rows by uploader'
    Pass '401/403; PNG/JPEG/MP3 nhận đúng loại + kích thước; trùng → dùng lại; SVG/HTML giả/quá lớn/quá kích thước bị từ chối; file ngoài wwwroot'

    Write-Output '== Gắn vào câu hỏi'
    $base = "/web/questions/$q/media"
    foreach ($k in 'png', 'jpg', 'mp3') { Expect (Call 'POST' $base $token @{ mediaId = $up[$k].id }).status 200 "attach $k" }
    Expect (Call 'POST' $base $token @{ mediaId = $up.png.id }).body.message 'MEDIA_ALREADY_ATTACHED' 'attach twice'
    Expect (Call 'POST' $base $token @{ mediaId = $up.mp3b.id }).body.message 'MEDIA_AUDIO_LIMIT_REACHED' 'second audio'
    foreach ($k in 'png2', 'png3') { Expect (Call 'POST' $base $token @{ mediaId = $up[$k].id }).status 200 "attach $k" }
    Expect (Call 'POST' $base $token @{ mediaId = $up.png4.id }).body.message 'MEDIA_LIMIT_REACHED' 'sixth media'
    Expect (Call 'POST' "/web/questions/$locked/media" $token @{ mediaId = $up.png.id }).body.message 'QUESTION_LOCKED' 'question in exam locked'
    Expect (Call 'POST' $base $token @{ mediaId = [Guid]::NewGuid() }).status 404 'unknown media'
    Expect (Call 'POST' $base $teacherToken @{ mediaId = $up.png4.id }).status 403 'teacher cannot attach'
    $view = (Call 'GET' $base $token).body.data; $list = $view.items
    Expect $view.locked False 'free question not locked'
    Expect (@($list | ForEach-Object { $_.kind }) -join ',') 'image,image,audio,image,image' 'ordered list'
    Expect (Call 'GET' "/web/questions/$q" $token).body.data.mediaCount 5 'question mediaCount'
    Expect (Call 'DELETE' "$base/$($up.png3.id)" $token).status 200 'detach'
    Expect (Call 'DELETE' "$base/$($up.png3.id)" $token).status 404 'detach again'
    Expect (Sql "SELECT CONCAT(SUM(CASE WHEN Action=N'question.media.attach' THEN 1 ELSE 0 END),'/',SUM(CASE WHEN Action=N'question.media.detach' THEN 1 ELSE 0 END)) FROM dbo.Logs WHERE EntityType=N'Question' AND EntityId=N'$q';") '5/1' 'audit attach/detach'
    # Review T14: câu bị khóa báo locked trong GET và không gỡ được; câu đã xóa → 404
    Sql "INSERT dbo.QuestionMedia(QuestionId,MediaId,SortOrder) VALUES ($locked,'$($up.png4.id)',1);" | Out-Null
    Expect (Call 'GET' "/web/questions/$locked/media" $token).body.data.locked True 'locked flag in list'
    Expect (Call 'DELETE' "/web/questions/$locked/media/$($up.png4.id)" $token).body.message 'QUESTION_LOCKED' 'detach on locked question'
    Sql "UPDATE dbo.Questions SET Status=255 WHERE Id=$locked;" | Out-Null
    Expect (Call 'POST' "/web/questions/$locked/media" $token @{ mediaId = $up.png2.id }).status 404 'deleted question'
    # Review T14: gắn đồng thời trên cùng câu → không có 500 (khóa dòng câu hỏi, chạy lần lượt)
    $q2 = [int](Sql "INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'T14 câu song song $tag',SYSUTCDATETIME(),1,$subject,2); SELECT SCOPE_IDENTITY();")
    $jobs = @('png', 'png2', 'png3', 'png4', 'jpg', 'mp3', 'mp3b') | ForEach-Object {
        $mid = $up[$_].id
        Start-ThreadJob -ArgumentList $BaseUrl, $token, $q2, $mid -ScriptBlock {
            param($b, $tk, $qid, $m)
            (Invoke-WebRequest "$b/web/questions/$qid/media" -Method Post -SkipCertificateCheck -SkipHttpErrorCheck -ContentType 'application/json' `
                -Headers @{ Authorization = "Bearer $tk" } -Body (@{ mediaId = $m } | ConvertTo-Json)).StatusCode
        }
    }
    $codes = @($jobs | Wait-Job | Receive-Job); $jobs | Remove-Job
    if ($codes | Where-Object { $_ -notin 200, 409 }) { throw "concurrent attach codes: $($codes -join ',')" }
    $after = Sql "SELECT CONCAT(COUNT(*),'/',SUM(CASE WHEN m.Kind='audio' THEN 1 ELSE 0 END),'/',COUNT(DISTINCT qm.SortOrder)) FROM dbo.QuestionMedia qm JOIN dbo.MediaFiles m ON m.Id=qm.MediaId WHERE qm.QuestionId=$q2;"
    if ($after -notin '5/1/5', '5/0/5') { throw "concurrent attach broke limits: $after (count/audio/distinct order)" }
    Pass '≤ 5, ≤ 1 audio, không gắn trùng, câu bị khóa (gắn/gỡ/locked), câu đã xóa, gắn đồng thời không 500, thứ tự, mediaCount, gỡ, audit'

    Write-Output '== URL ký tạm'
    $list = (Call 'GET' $base $token).body.data.items
    $img = $list[0]; $aud = @($list | Where-Object kind -eq 'audio')[0]
    $g = Call 'GET' $img.url
    Expect "$($g.status)|$($g.type)" '200|image/png' 'signed URL serves without Bearer'
    $cc = "$($g.headers['Cache-Control'])"
    Expect "$($g.headers['Content-Security-Policy'])|$($g.headers['Cross-Origin-Resource-Policy'])" "default-src 'none'; sandbox|same-site" 'CSP + CORP'
    Expect "$($g.headers['X-Content-Type-Options'])|$($cc -match 'no-store' -and $cc -match 'private')|$($g.headers['Content-Disposition'])" 'nosniff|True|inline' 'safe headers'
    Expect ([Convert]::ToBase64String($g.bytes)) ([Convert]::ToBase64String([IO.File]::ReadAllBytes($files.png.FullName))) 'same bytes'
    $range = Call 'GET' $aud.url '' $null $null @{ Range = 'bytes=0-9' }
    Expect "$($range.status)|$($range.bytes.Length)|$($range.type)" '206|10|audio/mpeg' 'Range request (audio)'
    $mediaId = $img.id; $t = [Uri]::UnescapeDataString(($img.url -split 't=')[1])
    Expect (Call 'GET' "/web/media/$mediaId").status 401 'no token'
    Expect (Call 'GET' "/web/media/$($aud.id)?t=$([Uri]::EscapeDataString($t))").status 403 'token of another media'
    $bad = $t.Substring(0, $t.Length - 2) + $(if ($t.EndsWith('AA')) { 'BB' } else { 'AA' })
    Expect (Call 'GET' "/web/media/${mediaId}?t=$([Uri]::EscapeDataString($bad))").status 403 'tampered signature'
    $parts = $t.Split('.'); $expired = "$($parts[0]).$([DateTimeOffset]::UtcNow.AddMinutes(-1).ToUnixTimeSeconds()).$($parts[2])"
    Expect (Call 'GET' "/web/media/${mediaId}?t=$([Uri]::EscapeDataString($expired))").status 403 'edited expiry'
    Pass 'không cần Bearer; nosniff/no-store/inline; đúng nội dung; Range 206; thiếu/sai/khác media/sửa hạn → 401/403'
    Write-Output "TOBE_T14_MEDIA_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    function Clean([string]$query) { try { Sql $query | Out-Null } catch { Write-Warning "Dọn lỗi: $($_.Exception.Message)" } }
    $ids = Sql "SELECT STRING_AGG(CONVERT(nvarchar(36),Id),',') FROM dbo.MediaFiles WHERE UploadedBy='$examiner';"
    if ($subject -gt 0) { Clean "DELETE dbo.QuestionMedia WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE SubjectId=$subject);
      DELETE edq FROM dbo.ExamDetailQuestions edq JOIN dbo.ExamDetails d ON d.Id=edq.ExamDetailId JOIN dbo.Exams e ON e.Id=d.ExamId WHERE e.SubjectId=$subject;
      DELETE d FROM dbo.ExamDetails d JOIN dbo.Exams e ON e.Id=d.ExamId WHERE e.SubjectId=$subject; DELETE dbo.Exams WHERE SubjectId=$subject;
      DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId=$subject; DELETE dbo.Questions WHERE SubjectId=$subject; DELETE dbo.Subjects WHERE Id=$subject;" }
    Clean "DELETE dbo.QuestionMedia WHERE MediaId IN (SELECT Id FROM dbo.MediaFiles WHERE UploadedBy='$examiner'); DELETE dbo.MediaFiles WHERE UploadedBy='$examiner';"
    foreach ($id in ("$ids" -split ',' | Where-Object { $_ })) {
        $n = $id.Replace('-', '').ToLowerInvariant(); Remove-Item -LiteralPath (Join-Path (Join-Path $mediaRoot $n.Substring(0, 2)) $n) -ErrorAction SilentlyContinue
    }
    $users = "'$seed','$examiner','$teacher'"
    Clean "DELETE dbo.Logs WHERE ActorUserId IN ($users); DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users);
      DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users); DELETE dbo.Users WHERE Id IN ($users) OR Email=N't14seed$tag@example.invalid';"
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
