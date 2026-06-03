#!/usr/bin/env bash
# nexus-status — diagnóstico simples
set +e
APP_DIR=/opt/nexus
DOMAIN=gustavoti.com

C='\033[38;5;51m'    # ciano
G='\033[1;32m'       # verde
Y='\033[1;33m'       # amarelo
R='\033[1;31m'       # vermelho
D='\033[1;90m'       # cinza
W='\033[1;37m'       # branco
N='\033[0m'          # reset

ok()   { printf "${G}✔${N} "; }
warn() { printf "${Y}!${N} "; }
fail() { printf "${R}✗${N} "; }

kv() { printf "  ${D}%-9s${N} %b\n" "$1" "$2"; }
hd() { printf "\n${C}%s${N}\n" "$1"; }

# ── BANNER ──
upt=$(uptime -p | sed 's/up //')
echo
printf "  ${W}┌──────────┐${N}    ${C}nexus${N} ${G}●${N}\n"
printf "  ${W}│${N}██ ${C}●${N} ${C}●${N} ${D}░░░${W}│${N}    ${D}%s · %s${N}\n" "$(hostname)" "$DOMAIN"
printf "  ${W}└──────────┘${N}    ${D}up %s${N}\n" "$upt"

# ── sistema ──
hd "sistema"
ip4=$(curl -s -4 --max-time 3 ifconfig.me 2>/dev/null || echo "?")
sys=$(. /etc/os-release; echo $PRETTY_NAME)
load=$(cut -d' ' -f1-3 /proc/loadavg)
mem=$(free -m | awk '/^Mem:/{printf "%d/%d MB (%d%%)", $3, $2, $3*100/$2}')
disk=$(df -h / | awk 'NR==2{printf "%s/%s (%s)", $3, $2, $5}')
kv "so"    "$sys"
kv "ip"    "$ip4"
kv "load"  "$load"
kv "ram"   "$mem"
kv "disco" "$disk"

# ── containers ──
hd "containers"
while IFS='|' read -r name st; do
  if echo "$st" | grep -q "Up"; then
    if echo "$st" | grep -q "unhealthy"; then fail; else ok; fi
  else fail; fi
  short=$(echo "$st" | sed 's/Up /up /; s/ (healthy)//; s/ (unhealthy)/ ✗/')
  printf "${W}%-16s${N} ${D}%s${N}\n" "$name" "$short"
done < <(sudo -u nexus docker ps -a --format '{{.Names}}|{{.Status}}')

# ── endpoints ──
hd "endpoints"
for p in / /login /dashboard /clientes /chamados /financeiro; do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "https://$DOMAIN$p" 2>/dev/null)
  if [ "$code" = "200" ] || [ "$code" = "302" ]; then ok
  elif [ -z "$code" ] || [ "$code" = "000" ]; then fail
  else warn; fi
  printf "${W}%-14s${N} ${D}HTTP %s${N}\n" "$p" "$code"
done

# ── tls ──
hd "tls"
not_after=$(echo | openssl s_client -connect $DOMAIN:443 -servername $DOMAIN 2>/dev/null | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2)
if [ -n "$not_after" ]; then
  exp_epoch=$(date -d "$not_after" +%s 2>/dev/null)
  days=$(( (exp_epoch - $(date +%s)) / 86400 ))
  if   [ "$days" -gt 21 ]; then ok
  elif [ "$days" -gt 7 ];  then warn
  else fail; fi
  printf "expira em ${W}%d${N} dias\n" "$days"
else
  fail; printf "cert unreachable\n"
fi

# ── postgres ──
hd "postgres"
if sudo -u nexus docker exec nexus_postgres pg_isready -U nexus_user -d nexus_db >/dev/null 2>&1; then
  size=$(sudo -u nexus docker exec nexus_postgres psql -U nexus_user -d nexus_db -tAc "SELECT pg_size_pretty(pg_database_size('nexus_db'));" 2>/dev/null | tr -d ' \r\n')
  cli=$(sudo -u nexus docker exec nexus_postgres psql -U nexus_user -d nexus_db -tAc "SELECT count(*) FROM clients WHERE NOT \"IsDeleted\";" 2>/dev/null | tr -d ' \r\n')
  tck=$(sudo -u nexus docker exec nexus_postgres psql -U nexus_user -d nexus_db -tAc "SELECT count(*) FROM tickets WHERE NOT \"IsDeleted\";" 2>/dev/null | tr -d ' \r\n')
  ok; printf "${W}%s${N} · ${W}%s${N} clientes · ${W}%s${N} chamados\n" "$size" "${cli:-0}" "${tck:-0}"
else
  fail; printf "postgres unreachable\n"
fi

# ── repo ──
hd "repo"
if [ -d "$APP_DIR/.git" ]; then
  cd $APP_DIR
  branch=$(sudo -u nexus git rev-parse --abbrev-ref HEAD 2>/dev/null)
  commit=$(sudo -u nexus git log --oneline -1 2>/dev/null | head -c 70)
  sudo -u nexus git fetch --quiet origin 2>/dev/null
  ahead=$(sudo -u nexus git rev-list --count HEAD..origin/$branch 2>/dev/null)
  if [ "$ahead" = "0" ] || [ -z "$ahead" ]; then
    ok; printf "${W}%s${N} sync\n" "$branch"
  else
    warn; printf "${W}%s${N} %s commit(s) atrás\n" "$branch" "$ahead"
  fi
  printf "  ${D}%s${N}\n" "$commit"
fi

# ── logs ──
hd "logs (últimas 500)"
err=$(sudo -u nexus docker logs nexus_web --tail 500 2>&1 | grep -cE "\[ERR\]|Exception")
wrn=$(sudo -u nexus docker logs nexus_web --tail 500 2>&1 | grep -cE "\[WRN\]")
err=${err:-0}; wrn=${wrn:-0}
if [ "$err" -eq 0 ]; then ok
elif [ "$err" -lt 5 ]; then warn
else fail; fi
printf "${W}%s${N} erros · ${W}%s${N} avisos\n" "$err" "$wrn"

# ── backup ──
hd "backup"
BACKUP_DIR=/var/backups/nexus
if [ -f "$BACKUP_DIR/.last_success" ]; then
  last_ts=$(cat "$BACKUP_DIR/.last_success")
  now_ts=$(date +%s)
  age_h=$(( (now_ts - last_ts) / 3600 ))
  last_date=$(date -d "@$last_ts" '+%d/%m %H:%M')
  count=$(find "$BACKUP_DIR" -name 'nexus_*.sql.gz' -type f 2>/dev/null | wc -l)
  total=$(du -sh "$BACKUP_DIR" 2>/dev/null | awk '{print $1}')
  if [ "$age_h" -lt 26 ]; then ok
  elif [ "$age_h" -lt 50 ]; then warn
  else fail; fi
  printf "último: ${W}%s${N} · ${W}%dh${N} atrás · ${W}%s${N} arquivos · ${W}%s${N} total\n" \
    "$last_date" "$age_h" "$count" "$total"
else
  fail; printf "nenhum backup ainda — rode ${C}nexus-backup${N} manualmente ou aguarde cron\n"
fi

# ── segurança ──
hd "segurança"
if systemctl is-active --quiet ufw; then
  ok; printf "ufw ativo\n"
else
  fail; printf "ufw inativo\n"
fi
if systemctl is-active --quiet fail2ban; then
  banned=$(fail2ban-client status sshd 2>/dev/null | grep "Currently banned" | awk -F: '{print $2}' | tr -d ' \t')
  ok; printf "fail2ban · ${W}%s${N} IPs banidos\n" "${banned:-0}"
else
  warn; printf "fail2ban inativo\n"
fi

echo
