param([string]$Database = 'ProjectDACN_Task04Task09_Test', [string]$BaseUrl = 'https://localhost:7242')
# P1/A05 — tạo fixture 4 role rồi chạy ../DACN_FE/tests/p1RoleRegressionBrowserSmoke.mjs.
# Cần: API (Development) trỏ DB bản sao ở $BaseUrl và Vite HTTPS ở https://localhost:5173. Chạy từ thư mục DACN_Project.
$ErrorActionPreference = 'Stop'
if ($Database -ne 'ProjectDACN_Task04Task09_Test') { throw 'Only the named clone is writable.' }
function Sql([string]$query) {
    $output = sqlcmd -S . -d $Database -E -No -C -b -I -W -h -1 -Q "SET NOCOUNT ON; IF DB_NAME() <> N'$Database' THROW 51201, 'Wrong database', 1; $query"
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
$student = [Guid]::Empty; $teacher = [Guid]::NewGuid(); $examiner = [Guid]::NewGuid(); $admin = [Guid]::NewGuid()
$mail = $null; $exam = 0; $class = 0
$examName = "P1 hồi quy $tag"; $className = "P1 lớp hồi quy $tag"; $hiddenName = "P1 ẩn kết quả $tag"; $hidden = 0
try {
    $email = "p1rs$tag@example.invalid"
    if ((Auth 'register' @{ fullName = 'P1 Học viên hồi quy'; userName = "p1rs$tag"; email = $email; password = $password; address = 'QA'; dateOfBirth = '2000-01-01T00:00:00Z'; sex = $true }).status -ne 202) { throw 'register failed' }
    $mail = Get-ChildItem Project.Api/.maildrop -Filter 'verify-*.txt' | Sort-Object LastWriteTimeUtc -Descending |
        Where-Object { (Get-Content $_.FullName -Raw).Contains("To: $email") } | Select-Object -First 1
    $match = [regex]::Match((Get-Content $mail.FullName -Raw), '#token=([^\s]+)')
    if ((Auth 'verify-email' @{ token = [Uri]::UnescapeDataString($match.Groups[1].Value) }).status -ne 200) { throw 'verify failed' }
    $student = [Guid](Sql "SELECT Id FROM dbo.Users WHERE Email=N'$email';")
    foreach ($u in @(@($teacher, 'rt', 3), @($examiner, 're', 2), @($admin, 'ra', 1))) {
        Sql "INSERT dbo.Users(Id,FullName,UserName,Email,PasswordHash,LevelId,Status,EmailVerifiedAt,DateOfBirth,Address,Sex)
          SELECT '$($u[0])',N'P1 $($u[1]) $tag',N'p1$($u[1])$tag',N'p1$($u[1])$tag@example.invalid',PasswordHash,$($u[2]),1,SYSUTCDATETIME(),'2000-01-01',N'QA',1 FROM dbo.Users WHERE Id='$student';" | Out-Null
    }
    # Người ra đề chỉ có quyền 2 (quản lý câu hỏi).
    Sql "INSERT dbo.UserPermissions(UserId,PermissionId) VALUES ('$examiner',2);" | Out-Null
    $ids = Sql @"
DECLARE @subject int=(SELECT TOP 1 Id FROM dbo.Subjects WHERE Status=1 ORDER BY Id);
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'$examName',@subject,1,2,1,5,3,20,1);
DECLARE @exam int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@exam,N'P1',1,SYSUTCDATETIME(),'$teacher',SYSUTCDATETIME(),'$teacher');
DECLARE @variant int=SCOPE_IDENTITY();
INSERT dbo.Questions(Content,CreatedDate,Status,SubjectId,QuestionTypeId) VALUES (N'P1 câu hỏi hồi quy $tag',SYSUTCDATETIME(),1,@subject,2);
DECLARE @q int=SCOPE_IDENTITY();
INSERT dbo.Answers(QuestionId,Content,IsCorrect,Status) VALUES (@q,N'Phương án đúng',1,1),(@q,N'Phương án sai',0,1);
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (@variant,@q,5);
INSERT dbo.Classes(Name,ClassCode,Capacity,TeacherId,SubjectId,Status) VALUES (N'$className',N'P1R-$tag',5,'$teacher',@subject,1);
DECLARE @class int=SCOPE_IDENTITY();
INSERT dbo.ClassUsers(ClassId,UserId,Status) VALUES (@class,'$student',1);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@exam,N'P1 lịch hồi quy $tag',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,40,SYSUTCDATETIME()),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES (@class,SCOPE_IDENTITY());
-- Bài thứ hai không cho xem kết quả, cùng lớp và cùng câu hỏi.
INSERT dbo.Exams(Name,SubjectId,NumberOfQuestions,NumberOfRepeat,Status,MaximmumMark,PassMark,Duration,AllowViewResult) VALUES (N'$hiddenName',@subject,1,1,1,5,3,20,0);
DECLARE @hidden int=SCOPE_IDENTITY();
INSERT dbo.ExamDetails(ExamId,Code,Status,CreateDate,CreateBy,UpdateDate,UpdateBy) VALUES (@hidden,N'P1H',1,SYSUTCDATETIME(),'$teacher',SYSUTCDATETIME(),'$teacher');
INSERT dbo.ExamDetailQuestions(ExamDetailId,QuestionId,Point) VALUES (SCOPE_IDENTITY(),@q,5);
INSERT dbo.ExamSchedules(ExamId,Title,StartTime,EndTime,Status,IsTimeUtc) VALUES (@hidden,N'P1 lịch ẩn $tag',DATEADD(minute,-1,SYSUTCDATETIME()),DATEADD(minute,40,SYSUTCDATETIME()),1,1);
INSERT dbo.ClassExamSchedule(ClassId,ExamScheduleId) VALUES (@class,SCOPE_IDENTITY());
SELECT CONCAT(@exam,',',@class,',',@hidden);
"@
    $exam, $class, $hidden = $ids.Split(',') | ForEach-Object { [int]$_ }

    $env:DACN_QA_PASSWORD = $password; $env:DACN_QA_TAG = $tag; $env:DACN_QA_EXAM = $examName; $env:DACN_QA_CLASS = $className
    $env:DACN_QA_HIDDEN_EXAM = $hiddenName; $env:DACN_QA_EXAM_ID = $exam; $env:DACN_QA_DATABASE = $Database; $env:DACN_QA_TEACHER_NAME = "P1 rt $tag"
    $env:DACN_QA_STUDENT = $email; $env:DACN_QA_TEACHER = "p1rt$tag@example.invalid"
    $env:DACN_QA_EXAMINER = "p1re$tag@example.invalid"; $env:DACN_QA_ADMIN = "p1ra$tag@example.invalid"
    Push-Location ../DACN_FE
    try { & node tests/p1RoleRegressionBrowserSmoke.mjs; if ($LASTEXITCODE -ne 0) { throw 'Role browser smoke failed.' } }
    finally { Pop-Location }
    # Hậu kiểm SQL: bài chính 2 lượt (1 nộp, 1 hết giờ), đúng 1 lượt bị Admin của fixture hủy với lý do; bài ẩn 1 bài nộp;
    # môn UI đã đổi tên và bị ẩn (255); lớp UI thuộc giảng viên fixture, học viên đã được duyệt; bài thi UI ở trạng thái nháp (2).
    $check = Sql @"
SELECT CONCAT(
  (SELECT CONCAT(COUNT(*),'/',SUM(CASE WHEN a.State=N'submitted' THEN 1 ELSE 0 END),'/',SUM(CASE WHEN a.State=N'timedOut' THEN 1 ELSE 0 END),'/',
     SUM(CASE WHEN s.VoidedAtUtc IS NOT NULL AND s.VoidedByUserId='$admin' AND s.VoidReason=N'P1 hồi quy $tag' THEN 1 ELSE 0 END))
     FROM dbo.ExamAttempts a JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId=a.Id WHERE a.ExamId=$exam), ',',
  (SELECT COUNT(*) FROM dbo.ExamAttemptSubmissions s JOIN dbo.ExamAttempts a ON a.Id=s.ExamAttemptId WHERE a.ExamId=$hidden), ',',
  (SELECT COUNT(*) FROM dbo.Subjects WHERE Name=N'P1 môn UI $tag sửa' AND Status=255), ',',
  (SELECT COUNT(*) FROM dbo.Classes c JOIN dbo.ClassUsers m ON m.ClassId=c.Id WHERE c.ClassCode=N'P1UI-$tag' AND c.TeacherId='$teacher' AND m.UserId='$student' AND m.Status=1), ',',
  (SELECT COUNT(*) FROM dbo.Exams WHERE Name=N'P1 bài UI $tag' AND Status=2));
"@
    if ($check -ne '2/1/1/1,1,1,1,1') { throw "SQL post-check mismatch: $check" }
    Write-Output "P1_ROLE_REGRESSION_SMOKE=PASS;DB=$Database;TAG=$tag;SQL=$check"
}
finally {
    Remove-Item Env:DACN_QA_PASSWORD, Env:DACN_QA_TAG, Env:DACN_QA_EXAM, Env:DACN_QA_CLASS, Env:DACN_QA_HIDDEN_EXAM, Env:DACN_QA_EXAM_ID, Env:DACN_QA_DATABASE, Env:DACN_QA_TEACHER_NAME, Env:DACN_QA_STUDENT, Env:DACN_QA_TEACHER, Env:DACN_QA_EXAMINER, Env:DACN_QA_ADMIN -ErrorAction SilentlyContinue
    # Gồm cả dữ liệu tạo qua giao diện (bài thi/lớp/môn "UI"); mọi tên đều mang $tag.
    $exams = "SELECT Id FROM dbo.Exams WHERE Name LIKE N'P1 % $tag'"
    $classes = "SELECT Id FROM dbo.Classes WHERE ClassCode IN (N'P1R-$tag', N'P1UI-$tag')"
    Sql "DELETE dbo.AttemptVisibilityEvents WHERE AttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.ExamAttemptSubmissions WHERE ExamAttemptId IN (SELECT Id FROM dbo.ExamAttempts WHERE ExamId IN ($exams));
      DELETE dbo.ExamAttempts WHERE ExamId IN ($exams);
      DELETE dbo.ClassExamSchedule WHERE ExamScheduleId IN (SELECT Id FROM dbo.ExamSchedules WHERE ExamId IN ($exams)) OR ClassId IN ($classes);
      DELETE dbo.ExamSchedules WHERE ExamId IN ($exams);
      DECLARE @q TABLE(Id int); INSERT @q SELECT DISTINCT QuestionId FROM dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId IN ($exams));
      DELETE dbo.ExamDetailQuestions WHERE ExamDetailId IN (SELECT Id FROM dbo.ExamDetails WHERE ExamId IN ($exams));
      DELETE dbo.Answers WHERE QuestionId IN (SELECT Id FROM @q); DELETE dbo.Questions WHERE Id IN (SELECT Id FROM @q);
      DELETE dbo.ExamDetails WHERE ExamId IN ($exams); DELETE dbo.Exams WHERE Id IN ($exams);
      DELETE dbo.ClassUsers WHERE ClassId IN ($classes); DELETE dbo.Classes WHERE Id IN ($classes);
      DELETE dbo.Subjects WHERE Name LIKE N'P1 môn UI $tag%';" | Out-Null
    $users = "'$student','$teacher','$examiner','$admin'"
    Sql "DELETE dbo.UserPermissions WHERE UserId IN ($users); DELETE dbo.RefreshTokens WHERE UserId IN ($users); DELETE dbo.EmailVerificationTokens WHERE UserId IN ($users);
      DELETE dbo.Users WHERE Id IN ($users) OR Email=N'p1rs$tag@example.invalid';" | Out-Null
    if ($mail) { Remove-Item -LiteralPath $mail.FullName -ErrorAction SilentlyContinue }
}
