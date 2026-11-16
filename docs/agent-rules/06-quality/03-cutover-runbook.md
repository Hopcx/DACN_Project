# Runbook cutover DACN (G7)

Cập nhật 16/11/2026, BE `dev/Hop`. Quyết định người dùng (kế hoạch G7, mục 0): **nơi chạy chưa chốt** (phần máy chủ để "chờ quyết định"); **nâng cấp tại chỗ `ProjectDACN`**; **SMTP thật** khi chạy thật; **không ETL** dữ liệu Testify (nguồn có 0 lượt thi lịch sử, `05-migration/14-task12-testify-reconciliation.md`).

## 1. Trạng thái DB tại thời điểm viết (đo thật, chỉ đọc)

| DB | Migration | Ghi chú |
|---|---|---|
| `ProjectDACN` (đích chạy thật) | 12 / 12, cuối `20261111155021_P4AuditLogColumns` | 2 tài khoản Admin (1 đã xác minh email), 1 môn; Levels/Permissions 1–4 đủ. **Đã ở bản mới nhất** — khác với `12-data-readiness.md` (ghi 1 migration ngày 19/10): DB gốc đã được cập nhật bằng tay sau mốc đó (API không tự migrate). |
| `ProjectDACN_Task04Task09_Test` | 12 / 12 | Bản sao QA, chỉ dùng cho smoke/UAT. |
| `ProjectDACN_LegacyFixture_Test` | 1 | Bản sao cũ từ Task12; dùng làm nguồn diễn tập đường nâng cấp 1 → 12. |

## 2. Điều kiện go / no-go

Chỉ cutover khi **tất cả** đạt:
1. `dotnet build DACN_Project.sln` 0 lỗi; FE `npm run lint` + `npm run build` đạt.
2. `tests/RunUatSuite.ps1` trên bản sao QA: `UAT_SUITE=PASS` (xem `ket-qua` mới nhất trong `.bangiao` hoặc chạy lại).
3. `tests/CutoverRehearsal.ps1` đạt với nguồn là **chính DB đích** ngay trước cutover (`CUTOVER_REHEARSAL=PASS`).
4. Checklist UAT thủ công (`04-uat-checklist.md`) không còn dòng "Không đạt" ở mức chặn.
5. Cấu hình chạy thật (mục 3) đã điền và đã thử khởi động API ở môi trường không phải Development.
6. Có backup mới của `ProjectDACN` (COPY_ONLY + CHECKSUM) và `RESTORE VERIFYONLY` đạt.

## 3. Cấu hình bắt buộc khi không phải Development

API **từ chối khởi động** nếu thiếu (FACT: `Project.Api/Program.cs` khối `!IsDevelopment()`, `Project.Api/Services/VerificationEmailSender.cs`). Đặt bằng biến môi trường hoặc secret store của nơi chạy; **không commit** vào repo:

| Khóa | Ghi chú |
|---|---|
| `ConnectionStrings__Default` | Trỏ `ProjectDACN` trên máy chủ thật; tài khoản DB chỉ có quyền cần thiết (không `sysadmin`). |
| `JwtSettings__SecretKey` | Sinh mới, ≥ 32 byte ngẫu nhiên; **không** dùng giá trị trong `appsettings.json`. Đổi khóa làm mọi phiên cũ hết hạn. |
| `JwtSettings__Issuer`, `JwtSettings__Audience` | Giữ như `appsettings.json` nếu không có yêu cầu khác. |
| `JwtSettings__ExpiryMinutes` | Thời hạn access token; code giới hạn trong 1–15 phút (`Project.Application/Services/JwtService.cs`, `Math.Clamp(…, 1, 15)`), nên giá trị 60 trong `appsettings.json` thực tế thành 15. Phiên dài dựa vào refresh token. |
| `Security__AllowedOrigins__0` | URL HTTPS của FE (vd. `https://dacn.<miền>`). Phải là https. |
| `Mail__PublicFrontendUrl` | URL HTTPS của FE, dùng trong link xác minh email. |
| `Mail__SmtpHost`, `Mail__SmtpPort` (mặc định 587, SSL bật), `Mail__From`, `Mail__SmtpUser`, `Mail__SmtpPassword` | SMTP thật (Q3). Người dùng tự điền secret. |
| `ASPNETCORE_ENVIRONMENT` | `Production`. Swagger chỉ bật ở Development. |

## 4. Trình tự cutover (nâng cấp tại chỗ `ProjectDACN`)

1. Thông báo dừng sử dụng; dừng API cũ (nếu có) để không còn ghi.
2. `dotnet build DACN_Project.sln`.
3. Chạy diễn tập trên chính DB đích: `pwsh tests/CutoverRehearsal.ps1` (thêm `-ApiBaseUrl https://localhost:7242` nếu đã dựng API Development trỏ `ProjectDACN_CutoverRehearsal`). Phải `PASS`.
4. Backup thật: `BACKUP DATABASE [ProjectDACN] TO DISK = N'<thư mục backup>\ProjectDACN_cutover_<ngày>.bak' WITH COPY_ONLY, CHECKSUM, INIT;` rồi `RESTORE VERIFYONLY FROM DISK = N'…' WITH CHECKSUM;`. Giữ file ở nơi an toàn ngoài máy chủ.
5. Kiểm migration còn thiếu: `SELECT TOP 1 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;`
   - Nếu đã là migration mới nhất trong `Project.Infrastructure/Migrations` → bỏ qua bước 6 (trường hợp hiện tại).
   - Nếu chưa: sinh script **từ migration cuối đang có**: `dotnet ef migrations script <MigrationCuối> --no-build --project Project.Infrastructure --startup-project Project.Api -o cutover.sql`, review SQL.
   - **Không dùng `--idempotent`**: `Task04AuthLifecycle` có SQL thô nhắc cột `[Token]` (đã đổi tên `TokenHash`); SQL Server biên dịch cả batch nên script idempotent **lỗi** trên DB đã qua Task04 (đã tái hiện khi diễn tập 16/11).
6. Áp: `sqlcmd -S <server> -d ProjectDACN -E -b -I -f 65001 -i cutover.sql`.
7. Hậu kiểm SQL: số migration = số file migration; Users/Subjects không đổi; 6 bảng `ExamAttempts`, `ExamAttemptSubmissions`, `AttemptVisibilityEvents`, `LegacySubmissionRecords`, `RefreshTokens`, `EmailVerificationTokens` tồn tại; Levels/Permissions 1–4 đủ.
8. Khởi động API Production với cấu hình mục 3; `GET /web/health/live` = 200, `GET /web/health/ready` = 200.
9. Smoke đọc bằng tay: đăng nhập Admin, mở Tổng quan, Danh sách bài làm, Quản lý lịch thi; đăng ký một tài khoản thử và nhận email xác minh thật; xóa tài khoản thử.
10. Mở lại sử dụng; theo dõi mục 6 trong 24 giờ đầu.

## 5. Rollback

- **Trước khi mở lại sử dụng**: dừng API → chạy **một batch** trên `master` để cắt mọi kết nối còn lại rồi restore ngay: `ALTER DATABASE [ProjectDACN] SET OFFLINE WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [ProjectDACN] FROM DISK = N'…cutover….bak' WITH REPLACE, RECOVERY;` (tách thành nhiều lệnh thì kết nối khác có thể chen vào làm RESTORE lỗi) → khởi động bản API cũ. Diễn tập 16/11 xác nhận restore đưa history về đúng trạng thái trước (12 → 12 với nguồn hiện tại; 12 → 1 với nguồn cũ).
- **Sau khi đã có dữ liệu mới**: restore làm mất dữ liệu phát sinh; chỉ làm khi lỗi chặn và đã thông báo. `Down` của migration **không** khôi phục dữ liệu bị ghi đè (`12-data-readiness.md`).

## 6. Quan sát sau cutover

- Health: giám sát `GET /web/health/ready` (200 = API + SQL ổn; 503 = mất DB). Không đăng nhập; response chỉ có `Healthy`/`Unhealthy`.
- Log: Serilog ghi console và file `logs/log-<ngày>.txt` (cuộn theo ngày) dưới thư mục nội dung của API (`Project.Api/Program.cs`). Kiểm lỗi start/save/submit attempt, 409 trùng lịch, lỗi SMTP.
- Lượt thi quá hạn chưa chốt (worker `ExpiredAttemptFinalizerService` phải về 0 sau vài phút):
  `SELECT COUNT(*) FROM dbo.ExamAttempts WHERE State = N'inProgress' AND DeadlineUtc < SYSUTCDATETIME();`
- Backup định kỳ theo chính sách được chọn và thử restore định kỳ bằng `tests/CutoverRehearsal.ps1`.

## 7. Chờ quyết định (Q1 = chưa chốt nơi chạy)

Cách host API (IIS + ASP.NET Core Module / Windows Service / Docker), cách phục vụ FE tĩnh với fallback SPA (không trả `index.html` cho `/web/*`), chứng chỉ HTTPS, tài khoản dịch vụ SQL và lịch backup. Viết thêm mục này khi người dùng chốt máy đích.

Lưu ý khi chốt: API bật `UseHttpsRedirection`, nên probe health gọi bằng **HTTP** sẽ nhận 307 thay vì 200 — cấu hình giám sát gọi thẳng HTTPS, hoặc cho probe nội bộ đi qua cổng HTTPS.

## 8. Ghi chú vận hành từ diễn tập

- File `.bak` diễn tập nằm trong thư mục backup mặc định của SQL Server (`SERVERPROPERTY('InstanceDefaultBackupPath')`); tài khoản Windows thường **không xóa được** — cần quyền admin để dọn.
- `CutoverRehearsal.ps1` dùng `dotnet ef … --no-build` vì API đang chạy giữ khóa DLL; luôn build solution trước.
