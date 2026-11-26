#!/usr/bin/env bash
# T20 (QT20-4): build image trên chính máy chủ từ git, khởi động, chờ healthy. Bản qua kiểm tra được tag :good, bản tốt trước đó :previous.
#   ./deploy.sh              # git pull (ff-only) cả hai repo, build, up -d, kiểm health
#   ./deploy.sh --no-pull    # không git pull (vd. diễn tập với mã nguồn đang có)
#   ./deploy.sh --rollback   # quay về bản tốt gần nhất (:good nếu bản đang chạy chưa DEPLOY_OK, ngược lại :previous)
# Migration KHÔNG tự chạy (QT20-5): có migration mới thì backup → áp script SQL đã review → rồi mới deploy (runbook §7).
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

mode="deploy"; pull=1
for arg in "$@"; do
  case "$arg" in
    --no-pull) pull=0 ;;
    --rollback) mode="rollback" ;;
    *) die "Tham số không hỗ trợ: $arg" ;;
  esac
done

load_env

# ---- Kiểm .env trước khi đụng tới container ----
for key in SITE_HOST PUBLIC_ORIGIN ACME_EMAIL MSSQL_SA_PASSWORD DB_APP_PASSWORD JWT_SECRET_KEY MEDIA_SIGNING_KEY SMTP_HOST SMTP_FROM SMTP_USER SMTP_PASSWORD; do
  [[ -n "${!key:-}" ]] || die "Thiếu $key trong .env"
done
[[ "$PUBLIC_ORIGIN" == https://* ]] || die "PUBLIC_ORIGIN phải bắt đầu bằng https://"
for key in JWT_SECRET_KEY MEDIA_SIGNING_KEY; do
  (( $(printf '%s' "${!key}" | wc -c) >= 32 )) || die "$key phải dài ít nhất 32 byte (openssl rand -base64 48)"
done
[[ "$JWT_SECRET_KEY" != "$MEDIA_SIGNING_KEY" ]] || die "JWT_SECRET_KEY và MEDIA_SIGNING_KEY phải khác nhau"
if [[ "$(uname -s)" == "Linux" ]]; then
  perms="$(stat -c '%a' .env)"
  [[ "$perms" == "600" || "$perms" == "400" ]] || echo "CẢNH BÁO: .env đang có quyền $perms — nên chmod 600 .env"
fi

# Review T20 N3: tag chỉ trỏ vào bản đã qua kiểm tra
#   :good     = bản gần nhất in DEPLOY_OK
#   :previous = bản :good trước đó
# Bản build mới chỉ nhận :good sau khi DEPLOY_OK, nên rollback không bao giờ quay về một bản chưa từng chạy tốt.
tag="${IMAGE_TAG:-latest}"
image_id() { docker image inspect -f '{{.Id}}' "$1" 2>/dev/null || true; }

if [[ "$mode" == "rollback" ]]; then
  # Bản đang chạy chưa từng DEPLOY_OK (deploy hỏng) → về :good; bản đang chạy chính là :good → về :previous
  target="previous"
  [[ "$(image_id "dacn-api:$tag")" != "$(image_id dacn-api:good)" || "$(image_id "dacn-web:$tag")" != "$(image_id dacn-web:good)" ]] && target="good"
  for image in dacn-api dacn-web; do
    [[ -n "$(image_id "$image:$target")" ]] || die "Không có $image:$target để quay về."
    docker image tag "$image:$target" "$image:$tag"
  done
  info "Quay về image :$target"
  compose up -d --no-build
else
  if (( pull )); then
    info "git pull (ff-only)"
    git -C .. pull --ff-only
    git -C "${FE_PATH:-../../DACN_FE}" pull --ff-only
  fi
  info "Build image"
  compose build
  info "Khởi động"
  compose up -d
fi

info "Chờ SQL, API, web healthy"
wait_healthy sql 240
wait_healthy api 180
web_id="$(compose ps -q web)"
[[ "$(docker inspect -f '{{.State.Status}}' "$web_id")" == "running" ]] || die "web không chạy. Xem: docker compose logs web"

# Gọi qua Caddy (HTTPS) ngay trên máy chủ
https_port="${HTTPS_PORT:-443}"
# Review T20 T9: trên VPS phải kiểm cả chứng chỉ; chỉ bỏ qua khi diễn tập với localhost (CA nội bộ của Caddy)
insecure=(); [[ "$SITE_HOST" == "localhost" ]] && insecure=(-k)
code="$(curl -s "${insecure[@]}" -o /dev/null -w '%{http_code}' --resolve "${SITE_HOST}:${https_port}:127.0.0.1" "https://${SITE_HOST}:${https_port}/web/health/ready" || true)"
url="https://${SITE_HOST}$([[ "$https_port" == "443" ]] || echo ":$https_port")/web/health/ready"
[[ "$code" == "200" ]] || die "$url trả $code (000 = lỗi kết nối hoặc chứng chỉ chưa hợp lệ; xem docker compose logs web)"

# Bản đang chạy đã qua kiểm tra → :good; :good cũ (nếu khác) → :previous
for image in dacn-api dacn-web; do
  current="$(image_id "$image:$tag")"; good="$(image_id "$image:good")"
  if [[ "$current" != "$good" ]]; then
    # Rollback không đẩy bản vừa bị quay khỏi vào :previous (rollback lần nữa không được "tiến" lại bản có vấn đề)
    [[ "$mode" == "deploy" && -n "$good" ]] && docker image tag "$image:good" "$image:previous"
    docker image tag "$image:$tag" "$image:good"
  fi
done
info "DEPLOY_OK: $(date -Is) — $url = 200"
# Review T20 T12: digest image SQL đang chạy — ghim vào .env (MSSQL_IMAGE=...) để lần sau dùng đúng bản đã chạy tốt
echo "   SQL image: $(docker image inspect -f '{{if .RepoDigests}}{{index .RepoDigests 0}}{{else}}{{.Id}}{{end}}' "$(docker inspect -f '{{.Image}}' "$(compose ps -q sql)")")"
