#!/usr/bin/env bash
# T20 (QT20-6): backup DB + media cùng thời điểm vào BACKUP_DIR, kiểm CHECKSUM, giữ BACKUP_KEEP bản mới nhất.
#   ./backup.sh
# Cron hằng ngày lúc 02:30 (sửa đường dẫn):
#   30 2 * * * cd /opt/dacn/DACN_Project/deploy && ./backup.sh >> backups/backup.log 2>&1
# Tải bản backup ra máy khác (bắt buộc — backup chỉ nằm trên VPS là chưa đủ): xem runbook §7.
# SQL Server ghi file vào thư mục tạm TRONG container rồi chép ra bằng docker compose cp (không cần bind mount/chown;
# chạy giống nhau trên VPS Linux và Docker Desktop).
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_env
# Review T20 T5: file backup chứa toàn bộ DB (kể cả hash mật khẩu) → chỉ chủ sở hữu đọc được
umask 077
mkdir -p "$BACKUP_DIR"
stamp="$(date -u +%Y%m%d_%H%M%S)"
bak="${DB_NAME}_${stamp}.bak"
media="media_${stamp}.tar.gz"
inner="$SQL_BACKUP_DIR/$bak"

info "Backup DB → $BACKUP_DIR/$bak"
compose exec -T sql mkdir -p "$SQL_BACKUP_DIR"
# SQL Express không hỗ trợ COMPRESSION; COPY_ONLY để không ảnh hưởng chuỗi backup khác. Kiểm CHECKSUM ở lệnh riêng (chỉ khi backup thành công).
echo "BACKUP DATABASE [$DB_NAME] TO DISK = N'$inner' WITH COPY_ONLY, CHECKSUM, INIT;" | sql_sa
echo "RESTORE VERIFYONLY FROM DISK = N'$inner' WITH CHECKSUM;" | sql_sa
compose cp "sql:$inner" "$BACKUP_DIR/$bak"
compose exec -T sql rm -f "$inner"
[[ -s "$BACKUP_DIR/$bak" ]] || die "Không chép được $bak ra $BACKUP_DIR"
chmod 600 "$BACKUP_DIR/$bak"   # docker compose cp giữ quyền từ container, không theo umask

info "Backup media → $BACKUP_DIR/$media"
# Review T20 T6: đọc thẳng volume media (chỉ đọc), không phụ thuộc container api đang chạy; dùng image API sẵn có (có tar)
docker run --rm --network none -v "$(volume_name media):/m:ro" --entrypoint tar "dacn-api:${IMAGE_TAG:-latest}" czf - -C /m . > "$BACKUP_DIR/$media"
tar tzf "$BACKUP_DIR/$media" >/dev/null || die "File media nén bị hỏng: $media"
chmod 600 "$BACKUP_DIR/$media"

info "Giữ $BACKUP_KEEP bản mới nhất"
prune() { ls -1t "$BACKUP_DIR"/$1 2>/dev/null | tail -n +"$((BACKUP_KEEP + 1))" | while read -r old; do rm -f -- "$old"; echo "  xóa $old"; done; }
# Review T20 T4: chỉ các bản do backup.sh tạo (tên có ngày giờ), không đụng file khác như bản chuyển dữ liệu
prune "${DB_NAME}_[0-9]*_[0-9]*.bak"
prune "media_[0-9]*_[0-9]*.tar.gz"
info "BACKUP_OK: $bak + $media ($(du -h "$BACKUP_DIR/$bak" | cut -f1) + $(du -h "$BACKUP_DIR/$media" | cut -f1))"
