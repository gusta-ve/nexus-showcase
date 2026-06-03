#!/usr/bin/env bash
# nexus-backup — pg_dump diário do Nexus com retention de 7 dias.
# Roda via cron (/etc/cron.d/nexus-backup) às 03:10 UTC.
set -euo pipefail

BACKUP_DIR=/var/backups/nexus
RETENTION_DAYS=7
LOG=/var/log/nexus-backup.log

# Carrega POSTGRES_* do .env do compose
if [ -f /opt/nexus/.env ]; then
    set -a
    # shellcheck disable=SC1091
    source /opt/nexus/.env
    set +a
fi
DB_NAME="${POSTGRES_DB:-nexus_db}"
DB_USER="${POSTGRES_USER:-nexus_user}"

DATE=$(date +%Y%m%d_%H%M%S)
FILE="$BACKUP_DIR/nexus_${DATE}.sql.gz"

log() { echo "[$(date '+%F %T')] $*" | tee -a "$LOG"; }

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"   # contém dados do banco — só dono lê

log "─── backup ─── $DB_NAME → $FILE"

if docker exec nexus_postgres pg_dump -U "$DB_USER" -d "$DB_NAME" 2>>"$LOG" | gzip -9 > "$FILE"; then
    SIZE=$(du -h "$FILE" | awk '{print $1}')
    log "ok · $SIZE"
else
    log "ERRO pg_dump — arquivo pode estar corrompido, removendo"
    rm -f "$FILE"
    exit 1
fi

# Retention: apaga > N dias
DELETED=$(find "$BACKUP_DIR" -name 'nexus_*.sql.gz' -type f -mtime +"$RETENTION_DAYS" -delete -print | wc -l)
[ "$DELETED" -gt 0 ] && log "purga: $DELETED arquivo(s) > ${RETENTION_DAYS}d removido(s)"

# Marca timestamp pro nexus-status ler
date +%s > "$BACKUP_DIR/.last_success"

# Resumo
TOTAL_FILES=$(find "$BACKUP_DIR" -name 'nexus_*.sql.gz' -type f | wc -l)
TOTAL_SIZE=$(du -sh "$BACKUP_DIR" 2>/dev/null | awk '{print $1}')
log "estado: $TOTAL_FILES arquivo(s), $TOTAL_SIZE total"
