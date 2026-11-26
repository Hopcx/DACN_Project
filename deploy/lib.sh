#!/usr/bin/env bash
# T20: hàm dùng chung cho deploy.sh / restore-db.sh / backup.sh. Chạy từ thư mục deploy/.
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$DEPLOY_DIR"

# Git Bash trên Windows (diễn tập): không tự đổi /opt/... thành đường dẫn Windows khi gọi docker
export MSYS_NO_PATHCONV=1

die() { echo "LỖI: $*" >&2; exit 1; }
info() { echo "== $*"; }

load_env() {
  [[ -f .env ]] || die "Chưa có deploy/.env (cp .env.example .env rồi điền giá trị)."
  set -a
  # shellcheck disable=SC1091
  source ./.env
  set +a
  DB_NAME="${DB_NAME:-ProjectDACN}"
  DB_APP_USER="${DB_APP_USER:-dacn_app}"
  BACKUP_DIR="${BACKUP_DIR:-./backups}"
  BACKUP_KEEP="${BACKUP_KEEP:-7}"
  # Thư mục tạm cho file .bak bên trong container SQL (trong volume dữ liệu; file được chép ra/vào rồi xóa)
  SQL_BACKUP_DIR="/var/opt/mssql/dacn-backup"
  [[ "$DB_NAME" =~ ^[A-Za-z0-9_]+$ ]] || die "DB_NAME chỉ gồm chữ, số, gạch dưới."
  [[ "$DB_APP_USER" =~ ^[A-Za-z0-9_]+$ ]] || die "DB_APP_USER chỉ gồm chữ, số, gạch dưới."
  # Review T20 T2: mật khẩu API nằm trong chuỗi kết nối (dấu ; hoặc nháy sẽ phá chuỗi) và được bash/compose đọc khác nhau khi có $
  if [[ -n "${DB_APP_PASSWORD:-}" && ! "$DB_APP_PASSWORD" =~ ^[A-Za-z0-9+/=_@#%.!-]+$ ]]; then
    die "DB_APP_PASSWORD chỉ dùng chữ, số và các ký hiệu + / = _ @ # % . ! - (không dùng ; nháy, khoảng trắng hay \$)."
  fi
}

# Review T20 T6: tên volume thật của compose (vd. dacn_media) để đọc trực tiếp, không phụ thuộc container đang chạy
volume_name() {
  local name
  name="$(docker volume ls -q --filter "label=com.docker.compose.project=dacn" --filter "label=com.docker.compose.volume=$1")"
  [[ -n "$name" ]] || die "Không thấy volume $1 của compose dacn."
  printf '%s' "$name"
}

compose() { docker compose --env-file .env "$@"; }

# Chạy T-SQL (đọc từ stdin) bằng tài khoản sa bên trong container SQL.
# Mật khẩu sa lấy từ biến môi trường của chính container (không đưa lên dòng lệnh của máy chủ).
# Tham số thêm (vd. -s '|') được chuyển thẳng cho sqlcmd. -x: không thay biến $(…) của sqlcmd trong nội dung (review T20 T1).
sql_sa() {
  compose exec -T sql bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -x -I -h -1 -W -f 65001 -i /dev/stdin "$@"' _ "$@"
}

# Bọc chuỗi thành literal N'...' cho T-SQL (nhân đôi dấu nháy đơn)
sql_literal() { local v="${1//\'/\'\'}"; printf "N'%s'" "$v"; }

wait_healthy() {
  local service="$1" timeout="${2:-180}" waited=0 id status
  id="$(compose ps -q "$service")"
  [[ -n "$id" ]] || die "Service $service chưa chạy."
  while true; do
    status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$id")"
    [[ "$status" == "healthy" ]] && return 0
    (( waited >= timeout )) && die "$service chưa healthy sau ${timeout}s (trạng thái: $status). Xem: docker compose logs $service"
    sleep 5; waited=$((waited + 5))
  done
}
