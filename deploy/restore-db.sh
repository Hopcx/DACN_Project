#!/usr/bin/env bash
# T20 (QT20-3): khôi phục ProjectDACN từ file .bak vào container SQL, tạo login riêng cho API, hậu kiểm.
#   1) Trên máy cũ: BACKUP DATABASE [ProjectDACN] TO DISK = N'...\ProjectDACN_move.bak' WITH COPY_ONLY, CHECKSUM, INIT;
#                   RESTORE VERIFYONLY FROM DISK = N'...\ProjectDACN_move.bak' WITH CHECKSUM;
#   2) Chép file .bak vào BACKUP_DIR trên máy chủ (vd. scp) — script tự chép vào container; chép media riêng (runbook §7).
#   3) ./restore-db.sh ProjectDACN_move.bak [--replace]
# --replace: ghi đè DB đang có (chỉ dùng khi chắc chắn; nên chạy backup.sh trước).
# File .bak phải từ SQL Server cùng phiên bản hoặc cũ hơn container (2025).
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

file="${1:-}"; replace="${2:-}"
[[ -n "$file" ]] || die "Cách dùng: ./restore-db.sh <tên file .bak trong BACKUP_DIR> [--replace]"
[[ "$file" =~ ^[A-Za-z0-9._-]+\.bak$ ]] || die "Tên file chỉ gồm chữ, số, . _ - và kết thúc .bak"
[[ -z "$replace" || "$replace" == "--replace" ]] || die "Tham số thứ hai chỉ nhận --replace"
load_env
[[ -n "${DB_APP_PASSWORD:-}" ]] || die "Thiếu DB_APP_PASSWORD trong .env"
[[ -f "$BACKUP_DIR/$file" ]] || die "Không thấy $BACKUP_DIR/$file"

info "Khởi động SQL Server (nếu chưa chạy)"
compose up -d sql
wait_healthy sql 240

path="$SQL_BACKUP_DIR/$file"
exists="$(echo "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'$DB_NAME';" | sql_sa | tr -d '[:space:]')"
if [[ "$exists" != "0" && "$replace" != "--replace" ]]; then
  die "DB $DB_NAME đã tồn tại. Chạy backup.sh trước, rồi thêm --replace nếu thật sự muốn ghi đè."
fi

# Review T20 N2: API đang chạy có thể chiếm suất SINGLE_USER → RESTORE lỗi, DB kẹt SINGLE_USER. Dừng API trong lúc khôi phục
# và luôn dọn dẹp khi kết thúc (kể cả lỗi): xóa file tạm, trả DB về MULTI_USER, khởi động lại API nếu đã dừng.
api_was_running=0
if [[ -n "$(compose ps -q --status running api 2>/dev/null)" ]]; then api_was_running=1; fi
cleanup() {
  compose exec -T sql rm -f "$path" >/dev/null 2>&1 || true
  echo "IF DB_ID(N'$DB_NAME') IS NOT NULL AND DATABASEPROPERTYEX(N'$DB_NAME', 'UserAccess') = 'SINGLE_USER' ALTER DATABASE [$DB_NAME] SET MULTI_USER;" | sql_sa >/dev/null 2>&1 || true
  if (( api_was_running )); then info "Khởi động lại API"; compose start api >/dev/null 2>&1 || echo "CẢNH BÁO: không khởi động lại được api (docker compose start api)"; fi
}
trap cleanup EXIT
if (( api_was_running )); then info "Dừng API trong lúc khôi phục"; compose stop api; fi

# Chép file vào thư mục tạm trong container (không cần bind mount); xóa khi script kết thúc
compose exec -T sql mkdir -p "$SQL_BACKUP_DIR"
compose cp "$BACKUP_DIR/$file" "sql:$path"

info "Kiểm file backup (CHECKSUM)"
echo "RESTORE VERIFYONLY FROM DISK = N'$path' WITH CHECKSUM;" | sql_sa

info "Khôi phục $DB_NAME"
# Tên logic của file dữ liệu/log lấy từ chính bản backup (cột 1 = LogicalName, 3 = Type, 7 = FileId; số cột khác nhau
# giữa các phiên bản nên đọc dạng văn bản), đặt file vật lý vào thư mục dữ liệu mặc định của container
move=""
while IFS='|' read -r logical _ type _ _ _ fileid _; do
  [[ -n "$logical" && "$type" =~ ^[DL]$ ]] || continue
  logical_sql="${logical//\'/\'\'}"
  if [[ "$type" == "L" ]]; then target="${DB_NAME}_log${fileid}.ldf"; else target="${DB_NAME}_${fileid}.mdf"; fi
  move+=", MOVE N'${logical_sql}' TO N'/var/opt/mssql/data/${target}'"
done < <(echo "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$path';" | sql_sa -s '|' | tr -d '\r')
[[ -n "$move" ]] || die "Không đọc được danh sách file trong bản backup."
sql_sa <<SQL
SET NOCOUNT ON;
IF DB_ID(N'$DB_NAME') IS NOT NULL ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$DB_NAME] FROM DISK = N'$path' WITH CHECKSUM, REPLACE, RECOVERY$move;
ALTER DATABASE [$DB_NAME] SET MULTI_USER;
SQL

info "Tạo/cập nhật login $DB_APP_USER (chỉ db_datareader + db_datawriter)"
password="$(sql_literal "$DB_APP_PASSWORD")"
sql_sa <<SQL
SET NOCOUNT ON;
IF SUSER_ID(N'$DB_APP_USER') IS NULL
BEGIN
  CREATE LOGIN [$DB_APP_USER] WITH PASSWORD = $password, CHECK_POLICY = ON, DEFAULT_DATABASE = [$DB_NAME];
END
ELSE
BEGIN
  ALTER LOGIN [$DB_APP_USER] WITH PASSWORD = $password;
  ALTER LOGIN [$DB_APP_USER] WITH DEFAULT_DATABASE = [$DB_NAME];
END
USE [$DB_NAME];
-- User cùng tên còn sót trong bản backup (SID khác) thì gắn lại vào login mới
IF DATABASE_PRINCIPAL_ID(N'$DB_APP_USER') IS NOT NULL ALTER USER [$DB_APP_USER] WITH LOGIN = [$DB_APP_USER];
ELSE CREATE USER [$DB_APP_USER] FOR LOGIN [$DB_APP_USER];
ALTER ROLE db_datareader ADD MEMBER [$DB_APP_USER];
ALTER ROLE db_datawriter ADD MEMBER [$DB_APP_USER];
-- Review T20 T3: đúng hai role đọc/ghi; gỡ quyền thừa (EXECUTE, role khác còn sót trong bản backup)
REVOKE EXECUTE TO [$DB_APP_USER];
DECLARE @drop nvarchar(max) = N'';
SELECT @drop += N'ALTER ROLE ' + QUOTENAME(r.name) + N' DROP MEMBER [$DB_APP_USER];'
FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID(N'$DB_APP_USER') AND r.name NOT IN (N'db_datareader', N'db_datawriter');
EXEC (@drop);
-- Chủ sở hữu DB trong bản backup là tài khoản Windows của máy cũ (SID mồ côi trên Linux) → chuyển cho sa
ALTER AUTHORIZATION ON DATABASE::[$DB_NAME] TO sa;
SQL

info "Hậu kiểm"
sql_sa <<SQL
SET NOCOUNT ON;
USE [$DB_NAME];
SELECT CONCAT('migrations=', (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory),
  ' last=', (SELECT TOP 1 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC),
  ' users=', (SELECT COUNT(*) FROM dbo.Users), ' subjects=', (SELECT COUNT(*) FROM dbo.Subjects),
  ' questions=', (SELECT COUNT(*) FROM dbo.Questions), ' media_rows=', (SELECT COUNT(*) FROM dbo.MediaFiles),
  ' compat=', (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()));
SQL
echo "So các số trên với máy cũ (cùng truy vấn). Nhớ chép thư mục media tương ứng (media_rows > 0) — runbook §7."
info "RESTORE_OK"
