#!/usr/bin/env bash
# ============================================================================
# 心灯数据库恢复脚本 — deploy/restore.sh
# 用法：BACKUP_FILE=/var/backups/light/light-XXX.sql.gz ./restore.sh
# 警告：会覆盖目标库数据！恢复前确认 AES_DATA_KEY/JWT_SECRET 与备份时一致（密文不可跨密钥恢复）。
# ============================================================================
set -euo pipefail

CONTAINER="${CONTAINER:-light-mysql}"
DB_USER="${DB_USER:-root}"
DB_PASSWORD="${DB_ROOT_PASSWORD:?请导出 DB_ROOT_PASSWORD}"
DB_NAME="${DB_NAME:-light}"
BACKUP_FILE="${BACKUP_FILE:?请指定 BACKUP_FILE}"

[ -f "$BACKUP_FILE" ] || { echo "备份文件不存在：$BACKUP_FILE"; exit 1; }

echo "！！即将覆盖数据库 $DB_NAME，10 秒内按 Ctrl+C 取消"
sleep 10

echo "[$(date -u +%FT%TZ)] 恢复 $BACKUP_FILE → $DB_NAME"
gunzip -c "$BACKUP_FILE" | docker exec -i "$CONTAINER" mysql -u"$DB_USER" -p"$DB_PASSWORD" "$DB_NAME"
echo "[$(date -u +%FT%TZ)] 恢复完成。请验证：管理员登录、危机工单、日记密文可解密（AES_DATA_KEY 一致）。"
