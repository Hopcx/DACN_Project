param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# To-be T2: API chương/tag, metadata câu hỏi (chapterId, explanation, tagIds), bộ lọc, khóa mức độ hệ thống, quyền, audit.
# Chạy từ thư mục gốc DACN_Project (cần Project.Api/.maildrop). Fixture theo tag, dọn trong finally.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $result = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51151, 'Wrong database', 1; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL command failed: $($result -join ' ')" }
    return ($result | Out-String).Trim()
}
$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$studentEmail = "t2student$tag@example.invalid"
$password = 'Qa7' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$adminId = [Guid]::NewGuid(); $qId = [Guid]::NewGuid(); $sId = [Guid]::NewGuid(); $studentId = [Guid]::Empty
$subjectA = 0; $subjectB = 0; $mail = $null
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrf = (Invoke-RestMethod "$BaseUrl/web/auth/csrf" -WebSession $session -SkipCertificateCheck).data.csrfToken
$authHeaders = @{ Origin = 'https://localhost:5173'; 'X-CSRF-TOKEN' = $csrf }
function Auth([string]$route, $body) {
    for ($i = 0; $i -lt 5; $i++) {
        $result = Invoke-WebRequest "$BaseUrl/web/auth/$route" -Method Post -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck -Headers $authHeaders -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress -Depth 8)
        if ([int]$result.StatusCode -ne 429) { return @{ status = [int]$result.StatusCode; data = ($result.Content | ConvertFrom-Json) } }
        Start-Sleep -Seconds 15
    }
    throw 'Auth rate limit persisted.'
}
function Call([string]$method, [string]$route, [string]$token, $body = $null) {
    $p = @{ Uri = "$BaseUrl$route"; Method = $method; Headers = @{ Authorization = "Bearer $token" }; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $p.ContentType = 'application/json; charset=utf-8'; $p.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Compress -Depth 8)) }
    $result = Invoke-WebRequest @p
    return @{ status = [int]$result.StatusCode; data = if ($result.Content -and $result.Headers.'Content-Type' -match 'json') { $result.Content | ConvertFrom-Json } else { $null } }
}
function Expect([int]$actual, [int]$expected, [string]$case) {
    if ($actual -ne $expected) { throw "$case expected HTTP $expected, got $actual" }
}
function Question([int]$subject, $chapter, $tags, [string]$suffix, [string]$explanation = $null) {
    $q = @{ content = "T2 câu $tag $suffix"; subjectId = $subject; questionTypeId = 2; questionLevelId = 2
        answers = @(@{ content = 'A'; isCorrect = $true }, @{ content = 'B'; isCorrect = $false }) }
    if ($null -ne $chapter) { $q.chapterId = $chapter }
    if ($null -ne $tags) { $q.tagIds = @($tags) }
    if ($null -ne $explanation) { $q.explanation = $explanation }
    return $q
}
try {
    $register = Auth 'register' @{ fullName = 'T2 QA Student'; userName = "t2student$tag"; email = $studentEmail; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00'; sex = $true }
    Expect $register.status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $studentEmail") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $studentLogin = Auth 'login' @{ keyword = $studentEmail; password = $password }
    Expect $studentLogin.status 200 'login Student'
    $studentId = [Guid]$studentLogin.data.data.userId
    $student = [string]$studentLogin.data.data.accessToken
    # Admin; người ra đề quyền 2 (câu hỏi); người quản lý môn quyền 3.
    foreach ($row in @(@($adminId, 'admin', 1, 0), @($qId, 'qman', 2, 2), @($sId, 'sman', 2, 3))) {
        Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
SELECT '$($row[0])',N'T2 $($row[1])',N't2$($row[1])$tag',N't2$($row[1])$tag@example.invalid',PasswordHash,$($row[2]),1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$studentId';" | Out-Null
        if ($row[3]) { Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$($row[0])',$($row[3]));" | Out-Null }
    }
    $tokens = @{}
    foreach ($who in 'admin', 'qman', 'sman') {
        $login = Auth 'login' @{ keyword = "t2$who$tag@example.invalid"; password = $password }
        Expect $login.status 200 "login $who"
        $tokens[$who] = [string]$login.data.data.accessToken
    }
    $admin = $tokens.admin; $qman = $tokens.qman; $sman = $tokens.sman
    $subjectA = [int](Sql "INSERT dbo.Subjects(Name,Status) VALUES (N'T2 môn A $tag',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    $subjectB = [int](Sql "INSERT dbo.Subjects(Name,Status) VALUES (N'T2 môn B $tag',1); SELECT CONVERT(int,SCOPE_IDENTITY());")

    # Quyền
    foreach ($route in '/web/chapters', '/web/tags', '/web/questions') { Expect (Call 'GET' $route $student).status 403 "Student GET $route" }
    Expect (Call 'POST' '/web/chapters' $student @{ subjectId = $subjectA; name = 'x'; sortOrder = 0 }).status 403 'Student tạo chương'
    Expect (Call 'POST' '/web/tags' $student @{ name = "x$tag" }).status 403 'Student tạo tag'
    Expect (Call 'POST' '/web/chapters' $qman @{ subjectId = $subjectA; name = "Chương $tag"; sortOrder = 0 }).status 403 'Quyền 2 tạo chương'
    Expect (Call 'POST' '/web/tags' $sman @{ name = "Tag $tag" }).status 403 'Quyền 3 tạo tag'
    Expect (Call 'GET' "/web/chapters?subjectId=$subjectA" $qman).status 200 'Quyền 2 xem chương'
    Expect (Call 'GET' '/web/tags' $sman).status 403 'Quyền 3 xem tag'

    # Chương
    $a1 = Call 'POST' '/web/chapters' $sman @{ subjectId = $subjectA; name = "  Chương 1   $tag "; sortOrder = 1 }
    Expect $a1.status 201 'tạo chương A1'
    $a1Id = [int]$a1.data.data.id
    if ($a1.data.data.name -ne "Chương 1 $tag" -or $a1.data.data.status -ne 1) { throw "Chương A1 chưa chuẩn hóa: '$($a1.data.data.name)' / $($a1.data.data.status)" }
    Expect (Call 'POST' '/web/chapters' $sman @{ subjectId = $subjectA; name = "Chương 1 $tag"; sortOrder = 2 }).status 409 'trùng tên chương'
    $a2 = Call 'POST' '/web/chapters' $sman @{ subjectId = $subjectA; name = "Chương 2 $tag"; sortOrder = 2 }
    Expect $a2.status 201 'tạo chương A2'
    $a2Id = [int]$a2.data.data.id
    $b1 = Call 'POST' '/web/chapters' $admin @{ subjectId = $subjectB; name = "Chương 1 $tag"; sortOrder = 1 }
    Expect $b1.status 201 'Admin tạo chương B1 (trùng tên ở môn khác)'
    $b1Id = [int]$b1.data.data.id
    Expect (Call 'POST' '/web/chapters' $sman @{ subjectId = $subjectA; name = ''; sortOrder = 0 }).status 400 'tên chương rỗng'
    Expect (Call 'PUT' "/web/chapters/$a2Id" $sman @{ subjectId = $subjectA; name = "Chương 2 sửa $tag"; sortOrder = 3 }).status 200 'sửa chương'
    $list = Call 'GET' "/web/chapters?subjectId=$subjectA" $qman
    if (@($list.data.data).Count -ne 2) { throw "Môn A phải có 2 chương, nhận $(@($list.data.data).Count)" }

    # Tag
    $t1 = Call 'POST' '/web/tags' $qman @{ name = "Đại số $tag" }
    Expect $t1.status 201 'tạo tag 1'
    $t1Id = [int]$t1.data.data.id
    $dup = Call 'POST' '/web/tags' $qman @{ name = "  dai   SO $tag" }
    Expect $dup.status 409 'trùng tag sau chuẩn hóa'
    if ($dup.data.message -notmatch "id $t1Id") { throw "409 tag phải nêu id có sẵn: $($dup.data.message)" }
    $t2 = Call 'POST' '/web/tags' $qman @{ name = "Hình-học $tag" }
    Expect $t2.status 201 'tạo tag 2'
    $t2Id = [int]$t2.data.data.id
    Expect (Call 'POST' '/web/tags' $qman @{ name = "Hinh hoc $tag" }).status 201 'gạch nối không bị chuẩn hóa'
    $t3Id = [int]((Call 'GET' "/web/tags?textSearch=hinh%20hoc%20$tag" $qman).data.data | Where-Object { $_.name -eq "Hinh hoc $tag" }).id
    if (-not $t3Id) { throw 'Tìm tag theo tên chuẩn hóa thất bại.' }
    Expect (Call 'PUT' "/web/tags/$t3Id" $qman @{ name = "Hinh hoc 2 $tag" }).status 403 'quyền 2 sửa tag'
    Expect (Call 'DELETE' "/web/tags/$t3Id" $qman).status 403 'quyền 2 ẩn tag'
    Expect (Call 'DELETE' "/web/tags/$t3Id" $admin).status 200 'Admin ẩn tag 3'
    Expect (Call 'POST' '/web/tags' $qman @{ name = "HINH HOC $tag" }).status 409 'trùng tag đã ẩn'

    # Câu hỏi có metadata
    $q1 = Call 'POST' '/web/questions' $qman (Question $subjectA $a1Id @($t1Id, $t2Id, $t1Id) 'q1' "  Lời giải $tag  ")
    Expect $q1.status 201 'tạo câu q1'
    $q1Id = [int]$q1.data.data.id
    if ($q1.data.data.chapterId -ne $a1Id -or $q1.data.data.explanation -ne "Lời giải $tag" -or (@($q1.data.data.tagIds) -join ',') -ne (@($t1Id, $t2Id | Sort-Object) -join ',')) {
        throw "Metadata q1 sai: $($q1.data.data | ConvertTo-Json -Compress -Depth 4)"
    }
    $q2 = Call 'POST' '/web/questions' $qman (Question $subjectA $a2Id @($t2Id) 'q2')
    Expect $q2.status 201 'tạo câu q2'
    $q2Id = [int]$q2.data.data.id
    $q3 = Call 'POST' '/web/questions' $qman (Question $subjectA $null $null 'q3')
    Expect $q3.status 201 'tạo câu q3 không metadata'
    $q3Id = [int]$q3.data.data.id
    Expect (Call 'POST' '/web/questions' $qman (Question $subjectA $b1Id $null 'sai-chuong')).status 400 'chương thuộc môn khác'
    Expect (Call 'POST' '/web/questions' $qman (Question $subjectA $null @(1..11 | ForEach-Object { 900000000 + $_ }) 'qua-10')).status 400 'quá 10 tag'
    Expect (Call 'POST' '/web/questions' $qman (Question $subjectA $null @($t3Id) 'tag-an')).status 400 'tag đã ẩn'
    Expect (Call 'POST' '/web/questions' $qman (Question $subjectA 0 $null 'chuong-0')).status 400 'chapterId 0'
    Expect (Call 'POST' '/web/questions' $qman (Question $subjectA $null $null 'loi-giai-dai' ('x' * 4001))).status 400 'lời giải > 4000'

    # Bộ lọc
    $f = Call 'GET' "/web/questions?subjectId=$subjectA&chapterId=$a1Id" $qman
    if ((@($f.data.data).id -join ',') -ne "$q1Id") { throw "Lọc chương sai: $(@($f.data.data).id -join ',')" }
    $f = Call 'GET' "/web/questions?subjectId=$subjectA&tagIds=$t2Id" $qman
    if (((@($f.data.data).id | Sort-Object) -join ',') -ne ((@($q1Id, $q2Id) | Sort-Object) -join ',')) { throw "Lọc tag sai: $(@($f.data.data).id -join ',')" }
    $f = Call 'GET' "/web/questions?subjectId=$subjectA&tagIds=$t1Id&tagIds=$t2Id&questionLevelId=2" $qman
    if (@($f.data.data).Count -ne 2) { throw "Lọc nhiều tag + mức độ phải ra 2 câu, nhận $(@($f.data.data).Count)" }
    $f = Call 'GET' "/web/questions?subjectId=$subjectA&tagIds=$t1Id&questionLevelId=1" $qman
    if (@($f.data.data).Count -ne 0) { throw 'Lọc tag + mức độ khác phải rỗng.' }
    Expect (Call 'GET' '/web/questions?chapterId=-1' $qman).status 400 'chapterId âm'
    Expect (Call 'GET' '/web/questions?tagIds=0' $qman).status 400 'tagIds 0'

    # PUT thay toàn bộ: không gửi tagIds/explanation/chapterId = xóa
    $put = Call 'PUT' "/web/questions/$q1Id" $qman (Question $subjectA $null $null 'q1')
    Expect $put.status 200 'PUT bỏ metadata'
    if ($null -ne $put.data.data.chapterId -or $null -ne $put.data.data.explanation -or @($put.data.data.tagIds).Count -ne 0) { throw 'PUT không xóa metadata.' }
    if ((Sql "SELECT COUNT(*) FROM dbo.QuestionTags WHERE QuestionId=$q1Id;") -ne '0') { throw 'QuestionTags còn sót sau PUT.' }
    Expect (Call 'PUT' "/web/questions/$q1Id" $qman (Question $subjectA $a1Id @($t1Id) 'q1')).status 200 'PUT gắn lại metadata'
    Expect (Call 'DELETE' "/web/questions/$q3Id" $qman).status 200 'ẩn câu q3'
    $restore = Question $subjectA $null $null 'q3'; $restore.status = 1
    Expect (Call 'PUT' "/web/questions/$q3Id" $qman $restore).status 200 'hiện lại câu q3'

    # Tag ẩn chỉ chặn gắn mới: câu đang gắn sửa được với tag cũ; Admin hiện lại tag ẩn bằng PUT status 1
    Expect (Call 'DELETE' "/web/tags/$t2Id" $admin).status 200 'Admin ẩn tag 2'
    $keep = Call 'PUT' "/web/questions/$q2Id" $qman (Question $subjectA $a2Id @($t2Id) 'q2')
    Expect $keep.status 200 'PUT giữ tag ẩn đang gắn'
    if ((@($keep.data.data.tagIds) -join ',') -ne "$t2Id") { throw 'Tag ẩn đang gắn bị mất sau PUT.' }
    Expect (Call 'PUT' "/web/questions/$q3Id" $qman (Question $subjectA $null @($t2Id) 'q3')).status 400 'gắn mới tag ẩn khi PUT'
    $back = Call 'PUT' "/web/tags/$t2Id" $admin @{ name = "Hình-học $tag"; status = 1 }
    Expect $back.status 200 'Admin hiện lại tag 2'
    if ($back.data.data.status -ne 1) { throw 'Tag 2 chưa hiện lại.' }
    Expect (Call 'PUT' "/web/questions/$q3Id" $qman (Question $subjectA $null @($t2Id) 'q3')).status 200 'gắn tag đã hiện lại'

    # Ẩn chương: câu đang gắn giữ liên kết; không gắn mới được
    Expect (Call 'DELETE' "/web/chapters/$a2Id" $qman).status 403 'quyền 2 ẩn chương'
    Expect (Call 'DELETE' "/web/chapters/$a2Id" $sman).status 200 'ẩn chương A2'
    Expect (Call 'POST' '/web/questions' $qman (Question $subjectA $a2Id $null 'chuong-an')).status 400 'gắn chương đã ẩn'
    if ((Sql "SELECT ChapterId FROM dbo.Questions WHERE Id=$q2Id;") -ne "$a2Id") { throw 'Câu q2 mất liên kết chương sau khi ẩn chương.' }
    Expect (Call 'PUT' "/web/chapters/$a1Id" $sman @{ subjectId = $subjectB; name = "Chương 1 khác $tag"; sortOrder = 1 }).status 409 'đổi môn của chương có câu'

    # Mức độ hệ thống 1..3
    foreach ($id in 1, 2, 3) {
        Expect (Call 'PUT' "/web/question-levels/$id" $admin @{ name = 'X'; description = 'X'; status = $true }).status 409 "PUT mức độ $id"
        Expect (Call 'DELETE' "/web/question-levels/$id" $admin).status 409 "DELETE mức độ $id"
    }
    if ((Sql "SELECT COUNT(*) FROM dbo.QuestionLevels WHERE Id BETWEEN 1 AND 3 AND Status=1;") -ne '3') { throw 'Mức độ hệ thống bị thay đổi.' }

    # Audit (best-effort, ghi sau khi thành công)
    $ids = "'$adminId','$qId','$sId'"
    $audit = Sql "SELECT STRING_AGG(CONCAT(Action,':',c),',') WITHIN GROUP (ORDER BY Action) FROM (SELECT Action, COUNT(*) c FROM dbo.Logs WHERE ActorUserId IN ($ids) GROUP BY Action) x;"
    foreach ($need in 'chapter.create:3', 'chapter.hide:1', 'chapter.update:1', 'question.create:3', 'question.hide:1', 'question.restore:1', 'question.update:4', 'tag.create:3', 'tag.hide:2', 'tag.update:1') {
        if ($audit -notmatch [regex]::Escape($need)) { throw "Thiếu audit $need (có: $audit)" }
    }
    if ((Sql "SELECT COUNT(*) FROM dbo.Logs WHERE ActorUserId IN ($ids) AND (Description LIKE N'%T2 câu%' OR Description LIKE N'%Lời giải%');") -ne '0') { throw 'Audit chứa nội dung câu hỏi.' }
    Write-Output "Audit: $audit"
    Write-Output "TOBE_T2_QUESTION_METADATA_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    $ids = "'$studentId','$adminId','$qId','$sId'"
    if ($subjectA -or $subjectB) {
        $subjects = (@($subjectA, $subjectB) | Where-Object { $_ }) -join ','
        Sql "DELETE qt FROM dbo.QuestionTags qt JOIN dbo.Questions q ON q.Id=qt.QuestionId WHERE q.SubjectId IN ($subjects);
DELETE a FROM dbo.Answers a JOIN dbo.Questions q ON q.Id=a.QuestionId WHERE q.SubjectId IN ($subjects);
DELETE dbo.Questions WHERE SubjectId IN ($subjects); DELETE dbo.Chapters WHERE SubjectId IN ($subjects); DELETE dbo.Subjects WHERE Id IN ($subjects);" | Out-Null
    }
    Sql "DELETE qt FROM dbo.QuestionTags qt JOIN dbo.Tags t ON t.Id=qt.TagId WHERE t.Name LIKE N'%$tag'; DELETE dbo.Tags WHERE Name LIKE N'%$tag';" | Out-Null
    if ($studentId -eq [Guid]::Empty) {
        $found = Sql "SELECT TOP 1 Id FROM dbo.Users WHERE Email=N'$studentEmail';"
        if ($found) { $studentId = [Guid]$found; $ids = "'$studentId','$adminId','$qId','$sId'" }
    }
    Sql "DELETE dbo.Logs WHERE ActorUserId IN ($ids); DELETE dbo.UserPermissions WHERE UserId IN ($ids); DELETE dbo.RefreshTokens WHERE UserId IN ($ids); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($ids); DELETE dbo.Users WHERE Id IN ($ids);" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
