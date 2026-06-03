#!/usr/bin/env bash
# nexus-restore — restaura um backup do Nexus.
# Uso: nexus-restore <arquivo.sql.gz>   ou apenas o nome dentro de /var/backups/nexus/
# Para o nexus_web durante o restore pra evitar conexões abertas.
set -euo pipefail

BACKUP_DIR=/var/backups/nexus

if [ $# -lt 1 ]; then
    echo "Uso: $0 <arquivo.sql.gz>"
    echo
    echo "Backups disponíveis:"
    ls -lh "$BACKUP_DIR"/nexus_*.sql.gz 2>/dev/null | awk '{print "  " $9 "  ·  " $5 "  ·  " $6, $7, $8}' || echo "  (nenhum)"
    exit 1
fi

FILE="$1"
[ -f "$FILE" ] || FILE="$BACKUP_DIR/$FILE"
[ -f "$FILE" ] || { echo "✗ Arquivo não encontrado: $1"; exit 1; }

if [ -f /opt/nexus/.env ]; then
    set -a
    # shellcheck disable=SC1091
    source /opt/nexus/.env
    set +a
fi
DB_NAME="${POSTGRES_DB:-nexus_db}"
DB_USER="${POSTGRES_USER:-nexus_user}"

echo
echo "⚠ Vai SOBRESCREVER o banco '$DB_NAME' com:"
echo "   $FILE"
echo "   ($(du -h "$FILE" | awk '{print $1}'))"
echo
read -r -p "Tem certeza? Digite 'yes' pra continuar: " ans
[ "$ans" = "yes" ] || { echo "Abortado."; exit 0; }

echo "▶ Parando nexus_web (evita conexões durante restore)..."
docker compose -f /opt/nexus/docker-compose.yml stop web

echo "▶ Restaurando..."
gunzip -c "$FILE" | docker exec -i nexus_postgres psql -U "$DB_USER" -d "$DB_NAME"

echo "▶ Subindo nexus_web..."
docker compose -f /opt/nexus/docker-compose.yml start web

echo
echo "✔ Restore concluído. Confira via 'nexus-status'."
