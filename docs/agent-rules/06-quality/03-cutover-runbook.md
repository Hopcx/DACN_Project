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
| `JwtSettings__SecretKey` | Sinh mới, ≥ 32 byte ngẫu nhiên; **không** dùng giá trị trong `appsettings.json` (T20: trùng khóa đã commit hoặc < 32 byte → API dừng khởi động). Đổi khóa làm mọi phiên cũ hết hạn. |
| `JwtSettings__Issuer`, `JwtSettings__Audience` | Giữ như `appsettings.json` nếu không có yêu cầu khác. |
| `JwtSettings__ExpiryMinutes` | Thời hạn access token; code giới hạn trong 1–15 phút (`Project.Application/Services/JwtService.cs`, `Math.Clamp(…, 1, 15)`), nên giá trị 60 trong `appsettings.json` thực tế thành 15. Phiên dài dựa vào refresh token. |
| `Security__AllowedOrigins__0` | URL HTTPS của FE (vd. `https://dacn.<miền>`). Phải là https. |
| `Mail__PublicFrontendUrl` | URL HTTPS của FE, dùng trong link xác minh email. |
| `Mail__SmtpHost`, `Mail__SmtpPort` (mặc định 587, SSL bật), `Mail__From`, `Mail__SmtpUser`, `Mail__SmtpPassword` | SMTP thật (Q3). Người dùng tự điền secret. |
| `ASPNETCORE_ENVIRONMENT` | `Production`. Swagger chỉ bật ở Development. |
| `Proxy__Enabled`, `Proxy__KnownNetworks__0` | T20: chạy sau reverse proxy thì bật và khai báo CIDR của mạng proxy (Docker: `DOCKER_SUBNET`). Bật mà thiếu mạng → dừng khởi động. Chỉ tin `X-Forwarded-*` từ mạng này; không chuyển hướng HTTPS (proxy đã ép). |
| `Media__RootPath`, `Media__SigningKey` | Thư mục media ngoài wwwroot (Docker: volume `/var/lib/dacn/media`); khóa ký URL media ≥ 32 byte. Thiếu → dừng khởi động. |
| `Mail__EnableSsl` | Mặc định `true` (STARTTLS). Chỉ đặt `false` khi diễn tập với máy nhận thư giả (log cảnh báo). |

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
- **Riêng migration `20261117021231_ToBeT1QuestionMetadata`** (To-be T1): `Down` xóa 3 dòng seed `QuestionLevels` Id 1–3; khi đã có câu hỏi dùng mức độ 1–3, `Down` **lỗi** FK `FK_Questions_QuestionLevels_QuestionLevelId`. Vì vậy rollback T1 bằng `Down` chỉ dùng khi chưa có câu hỏi gắn mức độ; sau đó chỉ rollback bằng restore backup.

## 6. Quan sát sau cutover

- Health: giám sát `GET /web/health/ready` (200 = API + SQL ổn; 503 = mất DB). Không đăng nhập; response chỉ có `Healthy`/`Unhealthy`.
- Log: Serilog ghi console và file `logs/log-<ngày>.txt` (cuộn theo ngày) dưới thư mục nội dung của API (`Project.Api/Program.cs`). Kiểm lỗi start/save/submit attempt, 409 trùng lịch, lỗi SMTP.
- Lượt thi quá hạn chưa chốt (worker `ExpiredAttemptFinalizerService` phải về 0 sau vài phút):
  `SELECT COUNT(*) FROM dbo.ExamAttempts WHERE State = N'inProgress' AND DeadlineUtc < SYSUTCDATETIME();`
- Thi thử quá hạn chưa chốt (worker `ExpiredPracticeFinalizerService`, 30 giây/lần, ân hạn 5 giây — phải về 0 sau khoảng 1 phút):
  `SELECT COUNT(*) FROM dbo.PracticeSessions WHERE State = N'inProgress' AND DeadlineUtc < DATEADD(second, -5, SYSUTCDATETIME());`
- Lệnh SQL tay (sqlcmd/SSMS script) ghi vào `ExamAttempts`, `PracticeSessions` hoặc bảng có index lọc khác phải bật `SET QUOTED_IDENTIFIER ON` (sqlcmd dùng `-I`), nếu không sẽ lỗi 1934. EF/API tự bật.
- Backup định kỳ theo chính sách được chọn và thử restore định kỳ bằng `tests/CutoverRehearsal.ps1`.

## 7. Triển khai bằng Docker trên VPS Linux (Q1 = Docker, T20 — 25/11/2026)

Đây là cách chạy thật đã chốt (QT20-1…6 = a). Bố cục:

- **Container:**
  - `sql`: SQL Server 2025 Express. Cổng 1433 **không** mở ra ngoài.
  - `api`: ASP.NET Core, HTTP 8080, chỉ chạy trong mạng nội bộ của Docker.
  - `web`: Caddy. Phục vụ FE tĩnh, tự lấy và gia hạn chứng chỉ HTTPS Let's Encrypt, chuyển `/web/*` sang `api`.
- **Volume:**
  - `sql-data`: dữ liệu SQL.
  - `media`: file media.
  - `api-logs`: log của Serilog.
  - `dp-keys`: khóa DataProtection (mã hóa cookie CSRF). Phải giữ qua các lần deploy, nếu mất thì mọi người đang đăng nhập bị đăng xuất một lần. Không cần backup.
  - `caddy-data`, `caddy-config`: chứng chỉ và cấu hình của Caddy.
  - Thư mục backup trên máy chủ: `deploy/backups` (không gắn vào container). SQL Server ghi `.bak` vào thư mục tạm trong container rồi script chép ra/vào bằng `docker compose cp`, nên không cần chown và chạy giống nhau trên Linux lẫn Docker Desktop.
- **File cấu hình:**
  - `DACN_Project/deploy/`: `docker-compose.yml`, `api.Dockerfile`, `.env.example`, `deploy.sh`, `restore-db.sh`, `backup.sh`.
  - `DACN_FE/`: `Dockerfile`, `deploy/Caddyfile`.
- **Secret** chỉ nằm trong `deploy/.env` trên máy chủ. File này không commit và nên có quyền `600`.

Diễn tập toàn bộ trên Docker Desktop thì thêm `docker-compose.rehearsal.yml` (Mailpit thay SMTP) và chạy `tests/DockerRehearsalSmoke.ps1`. Đừng dùng file compose diễn tập này trên VPS.

### 7.1 Chuẩn bị (một lần)
1. **VPS:** Ubuntu 22.04 hoặc 24.04, ≥ 2 vCPU, **≥ 4 GB RAM** (SQL Server cần tối thiểu 2 GB), ≥ 40 GB đĩa. Cài Docker Engine và plugin compose theo hướng dẫn chính thức của Docker, rồi thêm user vào nhóm `docker`.
2. **Tường lửa:** chỉ mở 22 (SSH), 80 và 443 (TCP), 443/udp. Ví dụ `ufw allow OpenSSH; ufw allow 80/tcp; ufw allow 443; ufw enable`.
3. **DNS:** tạo bản ghi **A** (IPv4) cho tên miền, trỏ về IP của VPS. Caddy chỉ lấy được chứng chỉ khi tên miền đã trỏ đúng và cổng 80/443 đã mở.
   - **Không tạo bản ghi AAAA (IPv6).** Mạng Docker trong compose chỉ có IPv4, nên kết nối IPv6 đi qua docker-proxy với IP nguồn là gateway. Khi đó mọi người dùng IPv6 sẽ dùng chung một hạn mức đăng nhập (review T20 N5). Muốn hỗ trợ IPv6 thì phải bật `enable_ipv6` kèm một subnet v6 cho mạng `dacn` và khai báo subnet đó trong `Proxy__KnownNetworks`.
4. **Mã nguồn:** clone **cạnh nhau**: `/opt/dacn/DACN_Project` và `/opt/dacn/DACN_FE`, nhánh `developer`.
5. **Cấu hình:**
   - Chạy `cd /opt/dacn/DACN_Project/deploy && cp .env.example .env && chmod 600 .env` rồi điền đủ các khóa.
   - Sinh `JWT_SECRET_KEY` và `MEDIA_SIGNING_KEY` bằng `openssl rand -base64 48`. Hai khóa phải khác nhau và **không** được là khóa có trong `appsettings.json`; nếu trùng, API sẽ từ chối khởi động.
   - Mật khẩu có ký tự đặc biệt thì đặt trong nháy đơn.
   - `PUBLIC_ORIGIN` phải là `https://<tên miền>`, không kèm dấu `/` ở cuối.
6. **Thư mục backup:** `deploy/backups` được script tự tạo; chỉ cần user chạy script ghi được vào đó.

### 7.2 Chuyển dữ liệu (QT20-3)
1. **Trên máy cũ:**
   - Dừng ghi dữ liệu.
   - Backup: `BACKUP DATABASE [ProjectDACN] TO DISK = N'…\ProjectDACN_move.bak' WITH COPY_ONLY, CHECKSUM, INIT;` rồi `RESTORE VERIFYONLY … WITH CHECKSUM;`.
   - Nén thư mục media, tức `Media:RootPath` của máy cũ (mặc định Development là `dacn-media-dev` nằm cạnh repo).
2. **Chép lên VPS** bằng `scp`:
   - File `.bak` → `deploy/backups/`.
   - File media nén → thư mục tạm.
3. **Khôi phục DB:** `./restore-db.sh ProjectDACN_move.bak`.
   - Script kiểm CHECKSUM, khôi phục DB, rồi tạo login `DB_APP_USER` chỉ có quyền `db_datareader`, `db_datawriter`, `EXECUTE`.
   - Cuối cùng script in `migrations=… users=… subjects=… questions=… media_rows=…`. So các số này với máy cũ bằng cùng truy vấn.
   - DB đã tồn tại thì script dừng. Chỉ thêm `--replace` khi chắc chắn, và nên chạy `backup.sh` trước.
4. **Đưa media vào volume:** chạy `./deploy.sh` một lần để tạo các container, rồi chép file media vào volume:
   ```bash
   docker compose --env-file .env cp <thư-mục-media-đã-giải-nén>/. api:/var/lib/dacn/media/
   docker compose --env-file .env exec -u 0 api chown -R 1654:1654 /var/lib/dacn/media
   ```
   Uid 1654 là user `app` của image `aspnet:8.0`. Kiểm số file trong volume khớp với `media_rows`.
5. **File backup từ SQL Server 2025** chỉ khôi phục được lên container 2025 trở lên. Máy cũ hiện chạy 17.0, compatibility level 170.

### 7.3 Triển khai, cập nhật, quay về (QT20-4)
- **Triển khai hoặc cập nhật:** chạy `./deploy.sh`. Script làm lần lượt:
  1. Kiểm `.env`: đủ khóa, PUBLIC_ORIGIN là https, mỗi khóa ≥ 32 byte, hai khóa khác nhau.
  2. `git pull --ff-only` cả hai repo.
  3. Gắn tag `:previous` cho image đang chạy.
  4. Build image mới rồi `up -d`.
  5. Chờ `sql` và `api` healthy, rồi gọi `https://<miền>/web/health/ready` qua Caddy.

  Script in `DEPLOY_OK` khi đạt, kèm digest của image SQL đang chạy. Ghi digest đó vào `MSSQL_IMAGE=` trong `.env` để lần deploy sau và lúc rollback dùng đúng bản đã chạy tốt.
- **Có migration mới (QT20-5)** thì **không** để API tự migrate. Theo thứ tự:
  1. Chạy `./backup.sh`.
  2. Ở máy dev, sinh script: `dotnet ef migrations script <migration-cuối-trên-VPS> --no-build --project Project.Infrastructure --startup-project Project.Api -o cutover.sql`, rồi review. **Không** dùng `--idempotent` (xem §4).
  3. Chép `cutover.sql` lên VPS rồi áp: `docker compose --env-file .env exec -T sql bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -d ProjectDACN -b -I -f 65001 -i /dev/stdin' < cutover.sql`.
  4. Chạy `./deploy.sh`.
- **Quay về bản trước:** chạy `./deploy.sh --rollback`.
  - Script chỉ quay về bản **đã từng in `DEPLOY_OK`**: bản đang chạy chưa qua kiểm tra thì về `:good`; bản đang chạy chính là `:good` thì về `:previous`. Tag `:good` chỉ được gắn sau khi `DEPLOY_OK`.
  - Nếu bản lỗi đã áp migration, phải khôi phục DB từ backup trước khi quay về: `./restore-db.sh <bản backup trước migration> --replace`. Script tự dừng API trong lúc khôi phục, rồi trả DB về MULTI_USER và khởi động lại API, kể cả khi khôi phục lỗi.
- **Xem log:** `docker compose --env-file .env logs -f api`. Serilog còn ghi file trong volume `api-logs`.

### 7.4 Backup (QT20-6)
- Chạy `./backup.sh`. Mỗi lần script:
  - backup DB (`COPY_ONLY, CHECKSUM`) và `RESTORE VERIFYONLY`;
  - nén media cùng thời điểm;
  - chỉ giữ `BACKUP_KEEP` bản mới nhất (mặc định 7).
- **Chạy tự động hằng ngày** bằng cron:
  `30 2 * * * cd /opt/dacn/DACN_Project/deploy && ./backup.sh >> backups/backup.log 2>&1`.
- **Bắt buộc có bản sao ngoài VPS.** Mất VPS thì mất luôn mọi backup nằm trên đó. Ví dụ, từ máy khác chạy định kỳ `scp vps:/opt/dacn/DACN_Project/deploy/backups/*_$(date -u +%Y%m%d)_* <thư mục an toàn>`.
- **Thử khôi phục mỗi tháng một lần** trên máy khác (hoặc Docker Desktop) bằng `restore-db.sh`. Một bản backup chưa từng khôi phục thử thì chưa đủ tin cậy.

### 7.5 Go / no-go cho Docker (bổ sung §2)
1. `tests/DockerRehearsalSmoke.ps1` PASS trên Docker Desktop, với dữ liệu khôi phục từ backup của DB bản sao.
2. `deploy.sh` in `DEPLOY_OK` trên VPS; `https://<miền>/` mở được, chứng chỉ hợp lệ.
3. Smoke tay trên VPS:
   - đăng ký tài khoản thử và **nhận email thật** qua SMTP;
   - đăng nhập Admin;
   - tải lên và xem một ảnh;
   - mở báo cáo lớp và tải `report.xlsx`;
   - xóa tài khoản thử.
4. `./backup.sh` chạy được, và đã có một bản sao lưu nằm ngoài VPS.

**Lưu ý vận hành:**
- API chạy sau proxy với `Proxy:Enabled=true`. API chỉ tin header `X-Forwarded-*` từ `Proxy:KnownNetworks` (mạng Docker `DOCKER_SUBNET`), nên rate limit tính theo IP thật của người dùng.
- Khi chạy sau proxy, API **không** chuyển hướng HTTPS, vì Caddy đã ép HTTPS. Probe health trong container gọi `http://localhost:8080`.
- Đổi `DOCKER_SUBNET` thì phải `docker compose down` rồi `up` lại để mạng Docker được tạo lại. Volume giữ nguyên dữ liệu.

## 8. Ghi chú vận hành từ diễn tập

- File `.bak` diễn tập nằm trong thư mục backup mặc định của SQL Server (`SERVERPROPERTY('InstanceDefaultBackupPath')`); tài khoản Windows thường **không xóa được** — cần quyền admin để dọn.
- `CutoverRehearsal.ps1` dùng `dotnet ef … --no-build` vì API đang chạy giữ khóa DLL; luôn build solution trước.
- **Seed mức độ của To-be T1** dùng `HasData` với Id cố định 1 "Dễ", 2 "Trung bình", 3 "Khó" (`Project.Infrastructure/Persistence/Seed/QuestionLevelSeed.cs`), không kiểm tồn tại. Nếu DB đích **đã có** dòng `QuestionLevels` Id 1–3, migration lỗi khóa chính và rollback cả khối. Trước cutover chạy: `SELECT Id, Name FROM dbo.QuestionLevels WHERE Id IN (1,2,3);` — nếu có dòng: dừng, không tự xóa; quyết định cùng người dùng (đổi tên dòng có sẵn cho khớp 3 mức rồi đánh dấu migration đã áp, hoặc chuyển câu hỏi sang Id khác). Tại 16–17/11/2026 `ProjectDACN` và bản sao QA đều 0 dòng.
- **To-be T11 (migration `ToBeT11QuestionOutcomes`)**: áp migration **trước** khi chạy code T11. Code T11 ghi `QuestionOutcomes` lúc chốt bài; nếu DB chưa có bảng thì nộp bài trả 500. Hai worker chốt quá hạn và worker bù `QuestionOutcomeBackfillService` tự tạm dừng (log cảnh báo) khi chưa có migration này. Sau cutover, worker bù ghi kết quả từng câu cho bài đã chốt trước đó (mỗi 60 giây, 200 nguồn mỗi loại một đợt).
- **To-be T14 (migration `ToBeT14Media`, media)**: ngoài Development bắt buộc cấu hình `Media:RootPath` (thư mục ngoài wwwroot, tài khoản chạy API có quyền ghi) và `Media:SigningKey` (≥ 32 ký tự, bí mật, không commit); thiếu thì API không khởi động. **Sao lưu thư mục `Media:RootPath` cùng thời điểm với backup DB** (file media không nằm trong DB). Development dùng mặc định `../dacn-media-dev` (đã .gitignore) và khóa ngẫu nhiên mỗi lần chạy.
  - URL media do API trả là **đường dẫn tương đối** `/web/media/{id}?t=…`. FE gọi API qua `/web` cùng origin (axios `baseURL: '/web'`, proxy Vite khi dev, reverse proxy khi triển khai) nên ảnh/âm thanh chạy đúng; nếu sau này đổi `baseURL` sang URL tuyệt đối, FE (`resolveMediaUrl`) tự ghép origin của API. Header `Cross-Origin-Resource-Policy: same-site` → FE và API phải cùng site.
  - URL ký tạm là **bearer**: ai có URL xem được file tới khi hết hạn (`Media:UrlMinutes`, mặc định 10, tối đa 60). `Media:SigningKey` nên là chuỗi ngẫu nhiên ≥ 32 byte (ví dụ `openssl rand -base64 48`); đổi khóa làm mọi URL cũ hết hiệu lực ngay.
  - `MediaFiles.UploadedBy` có FK Restrict tới `Users` (như `ExamAttempts`/`ReviewFlags`): không xóa cứng được người đã tải media — dùng khóa/tắt tài khoản.
- **To-be T15 (migration `ToBeT15MediaReferences`)**: snapshot v4 lưu media của câu; bảng `MediaReferences` ghi lúc bắt đầu lượt thi/tạo phiên luyện tập. Media còn được câu hỏi hoặc snapshot dùng thì **không xóa được** (FK Restrict). Admin dọn media mồ côi (≥ 24 giờ, không ai dùng) ở trang **Media mồ côi** — xóa cả dòng lẫn file, có audit `media.delete`; hệ thống không tự xóa (Q15). **URL media là bearer** (Q11b): URL ký tạm gắn với người xem và media, ai có URL đều xem được cho tới khi hết hạn (`Media:UrlMinutes`, mặc định 10), kể cả sau khi học viên đã nộp bài — không đưa URL vào log/ảnh chụp chia sẻ. Tải lại đúng nội dung của một media cũ sẽ dùng lại bản đó và làm mới mốc tải lên (lại được bảo vệ 24 giờ).
- **To-be T16 (migration `ToBeT16ShortAnswer`)**: thêm loại câu 4 "Điền ngắn" (chỉ chèn khi chưa có Id 4), 3 cột tùy chọn trên `Questions` và `QuestionOutcomes.AnswerText`; snapshot v5. **Rollback bị chặn khi đã có câu loại 4** (THROW 50016) — muốn rollback phải xóa/đổi các câu đó trước. File Excel câu hỏi có thêm cột G `ShortAnswerOptions` (tùy chọn; file 6 cột cũ vẫn nhập được).
- **To-be T17 (migration `ToBeT17MatchingStatements`)**: thêm loại câu 5 "Ghép cặp" và 6 "Đúng/Sai nhiều mệnh đề" (dừng nếu Id 5/6 đã dùng cho loại khác, THROW 50017) và cột `Answers.MatchContent`; snapshot v6 (vế phải có Id riêng, xáo khi bắt đầu bài). **Rollback bị chặn khi đã có câu loại 5/6** (THROW 50018). Excel: ghép cặp ghi `vế trái => vế phải; …` ở cột Answers, CorrectIndexes để trống; nhiều mệnh đề ghi chỉ số mệnh đề đúng (để trống nếu tất cả sai).
- **To-be T18 (migration `ToBeT18QuestionGroups`)**: bảng `QuestionGroups` (đoạn dùng chung) và `QuestionGroupMedia`, cột `Questions.GroupId/GroupOrder` (FK Restrict, check cùng có/cùng NULL); snapshot v7 chụp đoạn (chữ + media). **Media của đoạn cũng phải sao lưu cùng thư mục media** (đếm vào tham chiếu, không bị coi là mồ côi). **Rollback bị chặn khi đã có đoạn** (THROW 50019). Excel chưa hỗ trợ đoạn: xuất bỏ qua câu thuộc đoạn (header `X-Skipped-Grouped`).

## 9. Lưu trữ và dọn dữ liệu (Q15 — đợt tổng kiểm 25/11/2026)
Chính sách hiện tại: **không xóa tự động** bất kỳ dữ liệu nào. Phần dưới đây là cách đo dung lượng và dọn tay an toàn khi cần.

**Đo dung lượng** (chỉ đọc):
```sql
-- 15 bảng lớn nhất: số dòng lấy từ heap/clustered index; dung lượng tính mọi index (gồm LOB của nvarchar(max))
SELECT TOP 15 OBJECT_NAME(s.object_id) AS name,
  SUM(CASE WHEN s.index_id < 2 THEN s.row_count END) AS rows,
  SUM(s.reserved_page_count) * 8 / 1024 AS mb
FROM sys.dm_db_partition_stats s JOIN sys.tables t ON t.object_id = s.object_id
GROUP BY s.object_id ORDER BY mb DESC;
-- Media: số file và tổng byte (thư mục Media:RootPath phải tương ứng)
SELECT COUNT(*) AS files, SUM(SizeBytes) / 1048576 AS mb FROM dbo.MediaFiles;
```
Thư mục media đo bằng `Get-ChildItem <Media:RootPath> -Recurse -File | Measure-Object Length -Sum`. **Sao lưu DB và thư mục media cùng thời điểm**: file và dòng `MediaFiles` phải khớp nhau.

**Không được xóa** (bài làm và báo cáo dựa trên các bảng này; nhiều FK là Restrict):
- `ExamAttempts`, `ExamAttemptSubmissions`: snapshot, điểm, lượt bị hủy.
- `PracticeSessions`: chỉ học viên tự bỏ phiên đang làm qua API.
- `QuestionOutcomes`: phân tích, báo cáo lớp. Dữ liệu cũ được worker bù tạo lại khi thiếu, nhưng xóa thì mất lịch sử phân loại. Lưu ý FK `QuestionOutcomes→Users` là Cascade: xóa cứng user sẽ kéo theo outcome của họ.
- `MediaReferences`: tham chiếu từ snapshot; xóa thì media còn dùng sẽ bị coi là mồ côi.
- `Questions`/`Answers` đã vào đề: chỉ ẩn (Status 255) qua API.
- `QuestionGroups`/`QuestionGroupMedia`: đoạn đã vào đề bị khóa; chỉ ẩn được.
- `Users`: dùng khóa/tắt tài khoản thay vì xóa cứng (FK Restrict từ bài làm, media, cờ xem lại).

**Có thể dọn tay** (sau khi đã sao lưu, ngoài giờ thi):
- **Media mồ côi:** dùng trang Admin "Media mồ côi". Trang này xóa cả dòng lẫn file và ghi audit `media.delete`. Không xóa thẳng bằng SQL, vì file trên đĩa sẽ còn lại.
- **Nhật ký `Logs`:** có thể xóa bản ghi cũ hơn N tháng nếu nhà trường cho phép:
  ```sql
  DELETE TOP (10000) FROM dbo.Logs WHERE CreatedAtUtc < DATEADD(month, -12, SYSUTCDATETIME());
  ```
  Chạy lặp cho tới khi hết. Audit là bằng chứng thao tác (xuất dữ liệu, hủy lượt, xóa media), nên hỏi người có thẩm quyền trước khi xóa.
  - `CreatedAtUtc` chỉ có từ migration P4 (cột nullable). Log ghi trước P4 mang giá trị NULL nên lệnh trên **không** xóa chúng. Bảng không có cột thời gian nào khác, nên không xác định được tuổi của các dòng này. Đếm bằng `SELECT COUNT(*) FROM dbo.Logs WHERE CreatedAtUtc IS NULL`; muốn dọn thì người có thẩm quyền quyết theo `Id` (Id nhỏ hơn = cũ hơn).
- **Token hết hạn quá 30 ngày** (mọi cột đều lưu giờ UTC):
  ```sql
  DELETE TOP (10000) FROM dbo.RefreshTokens WHERE ExpiryDate < DATEADD(day, -30, SYSUTCDATETIME());
  DELETE TOP (10000) FROM dbo.BlackListTokens WHERE ExpiryDate < DATEADD(day, -30, SYSUTCDATETIME());
  DELETE TOP (10000) FROM dbo.EmailVerificationTokens WHERE ExpiresAt < DATEADD(day, -30, SYSUTCDATETIME());
  ```
  Token đã hết hạn không còn dùng được, nên xóa không ảnh hưởng người dùng. `BlackListTokens` chỉ cần giữ tới khi JWT tương ứng hết hạn.
- **File maildrop** trong Development (`Project.Api/.maildrop`).

Muốn xóa tự động theo lịch thì cần chốt lại Q15: bảng nào, giữ bao lâu, ai duyệt. Khi đó làm một lát riêng có worker, cấu hình và audit.
