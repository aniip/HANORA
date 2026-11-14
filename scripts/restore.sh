#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-$ROOT_DIR/.env.production}"

if [[ $# -lt 1 ]]; then
  echo "Pemakaian: $0 <backup-db.sql.gz> [backup-uploads.tar.gz]" >&2
  exit 1
fi

DB_BACKUP="$1"
UPLOADS_BACKUP="${2:-}"

if [[ ! -f "$DB_BACKUP" ]]; then
  echo "File backup database tidak ditemukan: $DB_BACKUP" >&2
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Env file tidak ditemukan: $ENV_FILE" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

if [[ -n "${DATABASE_BACKUP_URL:-}" ]]; then
  echo "PERINGATAN: restore akan menimpa database external (Azure/remote)."
else
  echo "PERINGATAN: restore akan menimpa database lokal compose $POSTGRES_DB."
fi
read -r -p "Ketik RESTORE untuk melanjutkan: " CONFIRM

if [[ "$CONFIRM" != "RESTORE" ]]; then
  echo "Dibatalkan."
  exit 1
fi

echo "==> Restore database"
if [[ -n "${DATABASE_BACKUP_URL:-}" ]]; then
  gunzip -c "$DB_BACKUP" | docker run --rm -i postgres:18-alpine \
    psql "$DATABASE_BACKUP_URL" -v ON_ERROR_STOP=1
else
  gunzip -c "$DB_BACKUP" | docker compose \
    --env-file "$ENV_FILE" \
    -f "$COMPOSE_FILE" \
    exec -T postgres \
    psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"
fi

if [[ -n "$UPLOADS_BACKUP" ]]; then
  if [[ ! -f "$UPLOADS_BACKUP" ]]; then
    echo "File backup uploads tidak ditemukan: $UPLOADS_BACKUP" >&2
    exit 1
  fi

  echo "==> Restore volume uploads"
  docker run --rm \
    -v hanora_prod_uploads:/data \
    -v "$(cd "$(dirname "$UPLOADS_BACKUP")" && pwd)":/backup:ro \
    alpine:3.20 \
    sh -c "rm -rf /data/* && tar xzf /backup/$(basename "$UPLOADS_BACKUP") -C /data"
fi

echo "Restore selesai. Jalankan health check dan login uji."
