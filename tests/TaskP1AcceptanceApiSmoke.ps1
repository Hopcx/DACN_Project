param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# P1 — các ca nghiệm thu còn thiếu của A01, A03, A04 (16-as-is-acceptance.md), chạy qua HTTPS API trỏ DB bản sao.
# Chạy từ thư mục gốc DACN_Project. Mọi fixture mang $tag và được dọn trong finally.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51181, 'Wrong database', 1; $query"
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
function Call([string]$method, [string]$url, [string]$token, $body = $null) {
    $args = @{ Uri = "$BaseUrl$url"; Method = $method; Headers = @{ Authorization = "Bearer $token" }; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $args.ContentType = 'application/json'; $args.Body = ($body | ConvertTo-Json -Compress -Depth 6) }
    $r = Invoke-WebRequest @args
    return @{ status = [int]$r.StatusCode; body = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null } }
}
# Gửi nhiều request cùng lúc (rào chắn chung) và trả mã HTTP theo thứ tự đầu vào.
function Race([object[]]$requests) {
    $gate = [System.Threading.ManualResetEventSlim]::new($false); $jobs = @()
    try {
        foreach ($req in $requests) {
            $jobs += Start-ThreadJob -ArgumentList $gate, $BaseUrl, $req -ScriptBlock {
                param($barrier, $base, $req)
                $args = @{ Uri = "$base$($req.url)"; Method = $req.method; Headers = @{ Authorization = "Bearer $($req.token)" }; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
                if ($null -ne $req.body) { $args.ContentType = 'application/json'; $args.Body = ($req.body | ConvertTo-Json -Compress -Depth 6) }
                $barrier.Wait()
                $r = Invoke-WebRequest @args
                [pscustomobject]@{ status = [int]$r.StatusCode; content = $r.Content }
            }
        }
        Start-Sleep -Milliseconds 300; $gate.Set()
        $jobs | Wait-Job | Out-Null
        return @($jobs | ForEach-Object { Receive-Job $_ -ErrorAction Stop })
    } finally { $gate.Dispose(); $jobs | Remove-Job -Force -ErrorAction SilentlyContinue }
}
function Expect($actual, $expected, [string]$case) { if ($actual -ne $expected) { throw "$case expected $expected, got $actual" } }
function Pass([string]$case) { Write-Output "  PASS $case" }
$utc = { param($d) $d.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ') }

$student = [Guid]::Empty; $s2 = [Guid]::NewGuid(); $s3 = [Guid]::NewGuid(); $teacher = [Guid]::NewGuid(); $admin = [Guid]::NewGuid()
$mail = $null; $subject2 = 0
try {
    $email = "p1s1$tag@example.invalid"
    Expect (Auth 'register' @{ fullName = 'P1 Student 1'; userName = "p1s1$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status 202 'register'
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    Expect (Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status 200 'verify'
    $login = Auth 'login' @{ keyword = $email; password = $password }
    $student = [Guid]$login.body.data.userId; $studentToken = [string]$login.body.data.accessToken
    foreach ($u in @(@($s2, 's2', 4, 'SYSUTCDATETIME()'), @($s3, 's3', 4, 'NULL'), @($teacher, 'teacher', 3, 'SYSUTCDATETIME()'), @($admin, 'admin', 1, 'SYSUTCDATETIME()'))) {
        Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
          SELECT '$($u[0])',N'P1 $($u[1])',N'p1$($u[1])$tag',N'p1$($u[1])$tag@example.invalid',PasswordHash,$($u[2]),1,$($u[3]),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';" | Out-Null
    }
    $teacherToken = [string](Auth 'login' @{ keyword = "p1teacher$tag@example.invalid"; password = $password }).body.data.accessToken
    $adminToken = [string](Auth 'login' @{ keyword = "p1admin$tag@example.invalid"; password = $password }).body.data.accessToken
    if (-not $teacherToken -or -not $adminToken) { throw 'Fixture login failed.' }
    $subject1 = [int](Sql 'SELECT TOP 1 Id FROM dbo.Subjects WHERE Status=1 ORDER BY Id;')
    $subject2 = [int](Sql "INSERT dbo.Subjects(Name,Status) VALUES (N'P1 môn phụ $tag',1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    # Hai phòng riêng của fixture để không vướng lịch thật (kể cả lịch unknown) của bản sao.
    $rooms = @(1, 2 | ForEach-Object { [int](Sql "INSERT dbo.Rooms(Name,Capacity,Address,Status) VALUES (N'P1 phòng $_ $tag',40,N'QA',1); SELECT CONVERT(int,SCOPE_IDENTITY());") })
    function NewClass([string]$name, [int]$capacity) {
        [int](Sql "INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'P1 $name $tag',N'P1$name-$tag',$capacity,'$teacher',$subject1,1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    }
    function NewExam([string]$name, [int]$subjectId) {
        [int](Sql @"
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'P1 $name $tag',$subjectId,1,2,1,5,3,20,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@exam,N'P1',1,SYSUTCDATETIME(),'$teacher',SYSUTCDATETIME(),'$teacher');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'P1 câu hỏi $name $tag',SYSUTCDATETIME(),1,$subjectId,2);
DECLARE @q int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'Đúng',1,1),(@q,N'Sai',0,1);
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@variant,@q,5);
SELECT @exam;
"@)
    }
    # Lịch chèn trực tiếp (giờ UTC tương đối theo ngày) và gán lớp.
    function NewSchedule([int]$examId, [string]$title, [int]$startMin, [int]$endMin, [string]$classIds = '', [int]$isUtc = 1, $roomId = 'NULL') {
        $id = [int](Sql "INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc,RoomId) VALUES ($examId,N'P1 $title $tag',DATEADD(minute,$startMin,'$base'),DATEADD(minute,$endMin,'$base'),1,$isUtc,$roomId); SELECT CONVERT(int,SCOPE_IDENTITY());")
        foreach ($c in ($classIds -split ',' | Where-Object { $_ })) { Sql "INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES ($c,$id);" | Out-Null }
        return $id
    }
    # Mốc giờ: 3 ngày sau, 00:00 UTC (không đụng lịch thật của bản sao).
    $baseDate = [DateTime]::UtcNow.Date.AddDays(3 + (Get-Random -Maximum 300))
    $base = $baseDate.ToString('yyyy-MM-ddTHH:mm:ss')
    $examA = NewExam 'A' $subject1; $examB = NewExam 'B' $subject2

    Write-Output '== A01 Teacher quản lý học viên'
    $c1 = NewClass 'C1' 1
    Expect (Call 'GET' "/web/teacher/classes/$c1/students/lookup?email=p1s3$tag@example.invalid" $teacherToken).status 404 'lookup unverified email'
    Expect (Call 'POST' "/web/teacher/classes/$c1/members" $teacherToken @{ email = "p1s3$tag@example.invalid" }).status 404 'add unverified email'
    Pass 'email chưa xác minh: lookup/add 404'
    Expect (Call 'POST' "/web/teacher/classes/$c1/members" $teacherToken @{ email = $email }).status 201 'add S1 into cap-1 class'
    Expect (Call 'POST' "/web/teacher/classes/$c1/members" $teacherToken @{ email = "p1s2$tag@example.invalid" }).status 409 'add S2 into full class'
    Pass 'lớp đầy: thêm học viên thứ hai 409'
    $c2 = NewClass 'C2' 1
    $m1 = [int](Sql "INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES ($c2,'$student',2); SELECT CONVERT(int,SCOPE_IDENTITY());")
    $m2 = [int](Sql "INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES ($c2,'$s2',2); SELECT CONVERT(int,SCOPE_IDENTITY());")
    $race = Race @(@{ method = 'POST'; url = "/web/teacher/classes/$c2/members/$m1/approve"; token = $teacherToken },
                   @{ method = 'POST'; url = "/web/teacher/classes/$c2/members/$m2/approve"; token = $teacherToken })
    Expect ((@($race.status) | Sort-Object) -join ',') '200,409' 'concurrent approve last seat'
    Expect (Sql "SELECT COUNT(*) FROM dbo.ClassUsers WHERE ClassId=$c2 AND Status=1;") '1' 'approved count after race'
    Pass 'duyệt đồng thời chỗ cuối: 200/409, sĩ số 1'
    # Lịch giao nhau: S2 đã duyệt ở C3 có lịch [480,540); C4 có lịch [510,570) → thêm S2 vào C4 phải 409; C5 chạm mép [540,600) → 201.
    $c3 = NewClass 'C3' 5; $c4 = NewClass 'C4' 5; $c5 = NewClass 'C5' 5
    Sql "INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES ($c3,'$s2',1);" | Out-Null
    NewSchedule $examA 'a01-x' 480 540 "$c3" | Out-Null
    NewSchedule $examB 'a01-y' 510 570 "$c4" | Out-Null
    NewSchedule $examB 'a01-z' 540 600 "$c5" | Out-Null
    Expect (Call 'POST' "/web/teacher/classes/$c4/members" $teacherToken @{ email = "p1s2$tag@example.invalid" }).status 409 'add S2 overlapping schedule'
    Expect (Call 'POST' "/web/teacher/classes/$c5/members" $teacherToken @{ email = "p1s2$tag@example.invalid" }).status 201 'add S2 touching schedule'
    Pass 'lịch học viên giao nhau 409, chạm mép 201'
    Expect (Call 'GET' "/web/teacher/classes/$c1/members" $adminToken).status 200 'Admin reads Teacher class'
    Expect (Call 'GET' "/web/teacher/classes/$c1/members" $studentToken).status 403 'Student reads Teacher class'
    Pass 'Admin toàn quyền vào route Teacher 200; Student 403'

    Write-Output '== A04 Tránh trùng môn/lịch'
    $sched = { param($examId, $title, $s, $e, $room) @{ examId = $examId; title = "P1 $title $tag"; startTime = (& $utc $baseDate.AddMinutes($s)); endTime = (& $utc $baseDate.AddMinutes($e)); status = 1; roomId = $room } }
    $r1 = Call 'POST' '/web/exam-schedules' $adminToken (& $sched $examA 'a04-room1' 900 960 $rooms[0])
    Expect $r1.status 201 'create schedule room 1'
    Expect (Call 'POST' '/web/exam-schedules' $adminToken (& $sched $examB 'a04-room1-other-subject' 930 990 $rooms[0])).status 409 'other subject same room overlapping'
    Expect (Call 'POST' '/web/exam-schedules' $adminToken (& $sched $examB 'a04-room2' 930 990 $rooms[1])).status 201 'other room overlapping'
    Pass 'khác môn cùng phòng giao giờ 409; khác phòng 201'
    # Cùng học viên ở hai lớp khác nhau, hai bài khác môn giao giờ.
    $c6 = NewClass 'C6' 5; $c7 = NewClass 'C7' 5
    Sql "INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES ($c6,'$s2',1),($c7,'$s2',1);" | Out-Null
    $p = NewSchedule $examA 'a04-p' 1200 1260 "$c6"
    $q = NewSchedule $examB 'a04-q' 1230 1290
    Expect (Call 'POST' '/web/class-exam-schedules' $adminToken @{ classId = $c7; examScheduleId = $q }).status 409 'same student other class overlapping'
    Pass 'cùng học viên, khác lớp, khác môn, giao giờ 409'
    # Hai request gán đồng thời hai lịch cùng môn giao giờ vào cùng một lớp.
    $c8 = NewClass 'C8' 5
    $g1 = NewSchedule $examA 'a04-g1' 1500 1560; $g2 = NewSchedule $examA 'a04-g2' 1530 1590
    $race = Race @(@{ method = 'POST'; url = '/web/class-exam-schedules'; token = $adminToken; body = @{ classId = $c8; examScheduleId = $g1 } },
                   @{ method = 'POST'; url = '/web/class-exam-schedules'; token = $adminToken; body = @{ classId = $c8; examScheduleId = $g2 } })
    Expect ((@($race.status) | Sort-Object) -join ',') '201,409' 'concurrent same-subject assignment'
    Expect (Sql "SELECT COUNT(*) FROM dbo.ClassExamSchedule WHERE ClassId=$c8;") '1' 'assignment count after race'
    Pass 'gán đồng thời hai lịch cùng môn giao giờ: 201/409, còn 1 bản ghi'
    # Lịch cũ chưa rõ múi giờ.
    $c9 = NewClass 'C9' 5
    $unknown = NewSchedule $examA 'a04-unknown' 1800 1860 '' 0
    $body = (& $sched $examA 'a04-unknown' 1800 1860 $null)
    Expect (Call 'PUT' "/web/exam-schedules/$unknown" $adminToken $body).status 409 'update unknown schedule'
    Expect (Call 'DELETE' "/web/exam-schedules/$unknown" $adminToken).status 409 'delete unknown schedule'
    Expect (Call 'POST' '/web/class-exam-schedules' $adminToken @{ classId = $c9; examScheduleId = $unknown }).status 409 'assign unknown schedule'
    Pass 'lịch unknown: sửa/xóa/gán 409'
    # Lịch đã có lượt thi.
    $withAttempt = NewSchedule $examA 'a04-attempt' 2100 2160 "$c9"
    $variant = [int](Sql "SELECT TOP 1 Id FROM dbo.ExamDetails WHERE ExamId=$examA;")
    Sql "INSERT dbo.ExamAttempts(Id,UserId,ExamScheduleId,ExamId,ExamDetailId,StartedAtUtc,DeadlineUtc,State,Revision,SnapshotJson,DraftJson,PassMark,AllowViewResult)
      VALUES (NEWID(),'$s2',$withAttempt,$examA,$variant,SYSUTCDATETIME(),DATEADD(minute,20,SYSUTCDATETIME()),N'inProgress',0,N'{}',N'[]',3,1);" | Out-Null
    Expect (Call 'PUT' "/web/exam-schedules/$withAttempt" $adminToken (& $sched $examA 'a04-attempt' 2110 2170 $null)).status 409 'move schedule with attempts'
    Expect (Call 'DELETE' "/web/exam-schedules/$withAttempt" $adminToken).status 409 'delete schedule with attempts'
    Expect (Call 'POST' '/web/class-exam-schedules' $adminToken @{ classId = $c1; examScheduleId = $withAttempt }).status 409 'assign class to schedule with attempts'
    Pass 'lịch đã có lượt thi: dời giờ/xóa/gán thêm lớp 409'

    Write-Output '== A03 Rời tab'
    $c10 = NewClass 'C10' 5
    Sql "INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES ($c10,'$student',1);" | Out-Null
    $open = [int](Sql "INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES ($examB,N'P1 a03-open $tag',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,30,SYSUTCDATETIME()),1,1); SELECT CONVERT(int,SCOPE_IDENTITY());")
    Sql "INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES ($c10,$open);" | Out-Null
    $start = Call 'POST' "/web/exam-schedules/$open/attempts" $studentToken
    Expect $start.status 200 'start attempt'
    $attempt = $start.body.data.attemptId
    $race = Race @(@{ method = 'POST'; url = "/web/attempts/$attempt/visibility-hidden"; token = $studentToken; body = @{ eventId = [Guid]::NewGuid() } },
                   @{ method = 'POST'; url = "/web/attempts/$attempt/visibility-hidden"; token = $studentToken; body = @{ eventId = [Guid]::NewGuid() } })
    Expect ((@($race.status) | Sort-Object) -join ',') '200,200' 'two-tab hidden statuses'
    Expect (Call 'GET' "/web/attempts/$attempt/visibility-count" $studentToken).body.data.count 1 'two tabs count once'
    Pass 'hai tab ẩn cùng lúc chỉ tính 1 lần'
    Start-Sleep -Milliseconds 1200
    $event = [Guid]::NewGuid()
    Expect (Call 'POST' "/web/attempts/$attempt/visibility-hidden" $studentToken @{ eventId = $event }).body.data.count 2 'later event counts'
    $retry = Call 'POST' "/web/attempts/$attempt/visibility-hidden" $studentToken @{ eventId = $event }
    Expect $retry.body.data.count 2 'offline retry same event'; Expect $retry.body.data.duplicate $true 'retry duplicate flag'
    Pass 'gửi lại sự kiện sau mất mạng (cùng eventId) không đếm đôi'
    $race = Race @(@{ method = 'POST'; url = "/web/attempts/$attempt/submit"; token = $studentToken; body = @{ revision = $start.body.data.revision } },
                   @{ method = 'POST'; url = "/web/attempts/$attempt/visibility-hidden"; token = $studentToken; body = @{ eventId = [Guid]::NewGuid() } })
    Expect $race[0].status 200 'submit during visibility race'
    if ($race[1].status -notin 200, 409) { throw "visibility during submit returned $($race[1].status)" }
    Expect (Sql "SELECT COUNT(*) FROM dbo.ExamAttemptSubmissions WHERE ExamAttemptId='$attempt';") '1' 'one submission'
    Expect (Call 'POST' "/web/attempts/$attempt/visibility-hidden" $studentToken @{ eventId = [Guid]::NewGuid() }).status 409 'event after submit'
    Pass "nộp bài và rời tab đồng thời: submit 200, sự kiện $($race[1].status); sau nộp 409"
    $second = Call 'POST' "/web/exam-schedules/$open/attempts" $studentToken
    Expect $second.status 200 'second attempt'
    $attempt2 = $second.body.data.attemptId
    Sql "UPDATE dbo.ExamAttempts SET DeadlineUtc=DATEADD(second,-5,SYSUTCDATETIME()) WHERE Id='$attempt2';" | Out-Null
    Expect (Call 'POST' "/web/attempts/$attempt2/visibility-hidden" $studentToken @{ eventId = [Guid]::NewGuid() }).status 409 'event after deadline'
    Expect (Call 'GET' "/web/attempts/$attempt2" $studentToken).body.data.state 'timedOut' 'attempt finalized by read after deadline'
    Pass 'sự kiện sau hạn 409; lượt được chốt timedOut'

    Write-Output "P1_ACCEPTANCE_SMOKE=PASS;DB=$Database;TAG=$tag"
}
finally {
    $exams = "SELECT Id FROM dbo.Exams WHERE Name LIKE N'P1 % $tag'"
    $classes = "SELECT Id FROM dbo.Classes WHERE ClassCode LIKE N'P1%-$tag'"
    Sql "DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.ExamAttempts WHERE ExamId IN ($exams);
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId IN ($exams)) OR ClassId IN ($classes);
      DELETE dbo.ExamSchedules WHERE ExamId IN ($exams);
      DECLARE @q TABLE(Id int); INSERT @q SELECT QuestionId FROM dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId IN ($exams));
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId IN ($exams));
      DELETE dbo.Answers WHERE QuestionId IN (SELECT Id FROM @q); DELETE dbo.Questions WHERE Id IN (SELECT Id FROM @q);
      DELETE dbo.ExamDetails WHERE ExamId IN ($exams); DELETE dbo.Exams WHERE Id IN ($exams);
      DELETE dbo.ClassUsers WHERE ClassId IN ($classes); DELETE dbo.Classes WHERE Id IN ($classes);
      DELETE dbo.Subjects WHERE Name=N'P1 môn phụ $tag'; DELETE dbo.Rooms WHERE Name LIKE N'P1 phòng % $tag';" | Out-Null
    $users = "'$student','$s2','$s3','$teacher','$admin'"
    Sql "DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users);
      DELETE dbo.Users WHERE Id IN ($users) OR Email=N'p1s1$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
