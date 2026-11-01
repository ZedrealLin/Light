#!/usr/bin/env bash
# ============================================================================
# 心灯数据库备份脚本 — deploy/backup.sh（建议 crontab 每日执行）
#   0 3 * * * /opt/light/deploy/backup.sh >> /var/log/light-backup.log 2>&1
# 备份内容：MySQL 全库（gzip）+ 密钥提示文件（不包含密钥本身，密钥另行离线保管）
# 保留策略：RETAIN 天，超期自动删除
# ============================================================================
set -euo pipefail

CONTAINER="${CONTAINER:-light-mysql}"
DB_USER="${DB_USER:-root}"
DB_PASSWORD="${DB_ROOT_PASSWORD:?请导出 DB_ROOT_PASSWORD}"
DB_NAME="${DB_NAME:-light}"
RETAIN="${RETAIN:-14}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/light}"

mkdir -p "$BACKUP_DIR"
STAMP=$(date -u +"%Y%m%dT%H%M%SZ")
TARGET="$BACKUP_DIR/light-$STAMP.sql.gz"

echo "[$(date -u +%FT%TZ)] 开始备份 $DB_NAME → $TARGET"
docker exec "$CONTAINER" mysqldump -u"$DB_USER" -p"$DB_PASSWORD" \
  --single-transaction --routines --triggers --set-gtid-purged=OFF \
  "$DB_NAME" | gzip -9 > "$TARGET"
echo "[$(date -u +%FT%TZ)] 备份完成：$(du -h "$TARGET" | cut -f1)"

# 保留策略
find "$BACKUP_DIR" -name "light-*.sql.gz" -mtime "+$RETAIN" -print -delete

echo "提醒：恢复备份还需 AES_DATA_KEY 与 JWT_SECRET（离线保管，勿与备份同机存放）。"
