#!/bin/bash
# ERP SaaS - PostgreSQL Backup Script
# Add to cron: 0 3 * * * /opt/erp/deploy/backup.sh
# Retains BACKUP_RETENTION_DAYS days of backups (default 30).

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/../.env"

if [ ! -f "$ENV_FILE" ]; then
  echo "❌ .env not found at $ENV_FILE"
  exit 1
fi

# Read credentials directly from .env — no hardcoded values here
DB_NAME=$(grep '^POSTGRES_DB=' "$ENV_FILE" | cut -d= -f2-)
DB_USER=$(grep '^POSTGRES_USER=' "$ENV_FILE" | cut -d= -f2-)
RETENTION_DAYS=$(grep '^BACKUP_RETENTION_DAYS=' "$ENV_FILE" | cut -d= -f2-)
RETENTION_DAYS="${RETENTION_DAYS:-30}"

if [ -z "$DB_NAME" ] || [ -z "$DB_USER" ]; then
  echo "❌ POSTGRES_DB or POSTGRES_USER missing in .env"
  exit 1
fi

BACKUP_DIR="/opt/erp/backups"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p "$BACKUP_DIR"

echo "📦 Backing up PostgreSQL $DB_NAME..."
docker compose -f "$SCRIPT_DIR/../docker-compose.yml" exec -T postgres \
  pg_dump -U "$DB_USER" "$DB_NAME" | gzip > "$BACKUP_DIR/${DB_NAME}_${DATE}.sql.gz"

echo "🗑 Cleaning old backups (>${RETENTION_DAYS} days)..."
find "$BACKUP_DIR" -name "*.sql.gz" -mtime +"$RETENTION_DAYS" -delete

BACKUP_SIZE=$(du -sh "$BACKUP_DIR/${DB_NAME}_${DATE}.sql.gz" | cut -f1)
echo "✅ Backup completed: ${DB_NAME}_${DATE}.sql.gz (${BACKUP_SIZE})"
