#!/usr/bin/env bash
# Database migrations for School Calendar, powered by grate (https://erikbra.github.io/grate/).
#
#   ./db/grate.sh migrate   Apply every new or changed script (safe to run repeatedly)
#   ./db/grate.sh status    Report whether the database is up to date (exit 0 = yes, 2 = changes pending)
#   ./db/grate.sh dryrun    Show what would run without touching the database
#   ./db/grate.sh watch     Migrate now, then again every time a .sql file changes (local development)
#   ./db/grate.sh reset     Drop and rebuild the database from scratch (LOCAL environment only)
#
# Configuration (environment variables):
#   DB_CONNECTION   Connection string        (default: local SQL Server from docker-compose)
#   DB_ENV          grate environment name   (default: LOCAL); scripts named *.ENV.<name>.sql only run there
#   DB_TYPE         grate database type      (default: sqlserver)
#   DB_SQL_DIR      Scripts folder           (default: db/sql)
#   WATCH_INTERVAL  Seconds between checks   (default: 2)
set -euo pipefail

DB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQL_DIR="${DB_SQL_DIR:-$DB_DIR/sql}"
DB_CONNECTION="${DB_CONNECTION:-Server=localhost,1433;Database=SchoolCalendar;User Id=sa;Password=Local_Dev_Passw0rd!;TrustServerCertificate=True}"
DB_ENV="${DB_ENV:-LOCAL}"
DB_TYPE="${DB_TYPE:-sqlserver}"
OUTPUT_DIR="${GRATE_OUTPUT:-$DB_DIR/.grate}"

grate() {
  # The tool version is pinned in /dotnet-tools.json; `dotnet tool restore` installs it.
  (cd "$DB_DIR" && dotnet tool run grate -- \
    --connectionstring "$DB_CONNECTION" \
    --files "$SQL_DIR" \
    --databasetype "$DB_TYPE" \
    --environment "$DB_ENV" \
    --output "$OUTPUT_DIR" \
    --silent \
    "$@")
}

migrate() { grate --transaction "$@"; }

status() {
  local out
  out="$(grate --isuptodate 2>&1)" || { echo "$out"; return 1; }
  if grep -q "Up to date: True" <<<"$out"; then
    echo "Database is up to date."
  else
    echo "Database has pending changes. Run: ./db/grate.sh migrate"
    return 2
  fi
}

# Hash of every script's path + content; changes when any .sql file is added, edited, renamed or deleted.
snapshot() {
  (cd "$SQL_DIR" && find . -type f -name '*.sql' -print0 | sort -z | xargs -0 -r sha1sum) | sha1sum
}

watch() {
  echo "Watching $SQL_DIR for changes (Ctrl+C to stop)..."
  local last="" current
  while true; do
    current="$(snapshot)"
    if [[ "$current" != "$last" ]]; then
      [[ -n "$last" ]] && echo "$(date +%T) Change detected, migrating..."
      if migrate; then echo "$(date +%T) Database is up to date."; else echo "$(date +%T) Migration failed; fix the script and save again."; fi
      last="$current"
    fi
    sleep "${WATCH_INTERVAL:-2}"
  done
}

reset() {
  if [[ "$DB_ENV" != "LOCAL" ]]; then
    echo "Refusing to drop a database outside the LOCAL environment (DB_ENV=$DB_ENV)." >&2
    exit 1
  fi
  grate --drop
}

case "${1:-migrate}" in
  migrate) shift || true; migrate "$@" ;;
  status)  status ;;
  dryrun)  grate --dryrun ;;
  watch)   watch ;;
  reset)   reset ;;
  *) sed -n '2,16p' "$0"; exit 1 ;;
esac
