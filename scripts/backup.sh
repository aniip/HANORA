#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-$ROOT_DIR/.env.production}"
BACKUP_DIR="${BACKUP_DIR:-$ROOT_DIR/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Env file tidak ditemukan: $ENV_FILE" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

mkdir -p "$BACKUP_DIR"

DB_FILE="$BACKUP_DIR/hanora-db-$STAMP.sql.gz"
UPLOADS_FILE="$BACKUP_DIR/hanora-uploads-$STAMP.tar.gz"

if [[ -n "${DATABASE_BACKUP_URL:-}" ]]; then
  echo "==> Backup database external (Azure/remote)"
  docker run --rm postgres:18-alpine \
    pg_dump "$DATABASE_BACKUP_URL" \
      --no-owner \
      --no-privileges \
      --clean \
      --if-exists \
    | gzip > "$DB_FILE"
else
  echo "==> Backup database lokal compose $POSTGRES_DB"
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    pg_dump \
      -U "$POSTGRES_USER" \
      -d "$POSTGRES_DB" \
      --no-owner \
      --no-privileges \
      --clean \
      --if-exists \
    | gzip > "$DB_FILE"
fi

echo "==> Backup volume uploads"
docker run --rm \
  -v hanora_prod_uploads:/data:ro \
  -v "$BACKUP_DIR":/backup \
  alpine:3.20 \
  tar czf "/backup/hanora-uploads-$STAMP.tar.gz" -C /data .

echo "==> Hapus backup lebih dari $RETENTION_DAYS hari"
find "$BACKUP_DIR" -type f -name 'hanora-*' -mtime +"$RETENTION_DAYS" -delete

echo "Selesai:"
echo "  $DB_FILE"
echo "  $UPLOADS_FILE"
echo
echo "Simpan salinan di lokasi terpisah dan terenkripsi."
