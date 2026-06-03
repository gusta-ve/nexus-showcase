#!/usr/bin/env bash
# Reseta a instância de DEMONSTRAÇÃO: recria o banco do zero; o web-demo
# re-aplica as migrations e o auto-seed no startup. NÃO toca na produção
# (só mexe nos serviços/volume *-demo, identificados pelo nome).
set -euo pipefail
cd "$(dirname "$0")/.."

echo "→ parando containers da demo..."
docker compose stop web-demo postgres-demo || true
docker compose rm -f postgres-demo || true

VOL=$(docker volume ls --format '{{.Name}}' | grep -E 'demo_pgdata$' | head -1 || true)
if [ -n "${VOL:-}" ]; then
  echo "→ removendo volume do banco da demo ($VOL)..."
  docker volume rm "$VOL" || true
fi

echo "→ subindo a demo do zero (re-seed automático no startup)..."
docker compose up -d postgres-demo web-demo
echo "✓ demo resetada."
