#!/bin/sh
# Backup loop of deploy/vm (M4.02): pg_dump -Fc of the database every BACKUP_INTERVAL_HOURS (24),
# written atomically to /backups, dumps older than BACKUP_RETENTION_DAYS (14) removed, and the time
# of the last success recorded for the healthcheck. BACKUP_HOOK, when set, runs on each new dump with
# its path as $1 (the off-machine copy). The connection comes from PGHOST, PGUSER, PGDATABASE and
# PGPASSWORD; the password is never printed. BACKUP_ONCE=true makes one backup and exits (drills).
set -eu

interval_hours=${BACKUP_INTERVAL_HOURS:-24}
retention_days=${BACKUP_RETENTION_DAYS:-14}
directory=${BACKUP_DIRECTORY:-/backups}

backup() {
  stamp=$(date -u +%Y%m%dT%H%M%SZ)
  target="$directory/codecoverage-$stamp.dump"

  if ! pg_dump --format=custom --no-owner --file="$target.partial"; then
    rm -f "$target.partial"
    echo "backup: pg_dump failed at $stamp" >&2
    return 1
  fi

  mv "$target.partial" "$target"
  date -u +%s > "$directory/.last-success"
  echo "backup: $target written ($(wc -c < "$target") bytes)"

  find "$directory" -maxdepth 1 -name 'codecoverage-*.dump' -mtime "+$retention_days" -print -delete

  if [ -n "${BACKUP_HOOK:-}" ]; then
    # The hook is the operator's: a failure is reported, the dump stays.
    sh -c "$BACKUP_HOOK" backup-hook "$target" || echo "backup: the hook failed for $target" >&2
  fi
}

mkdir -p "$directory"

if [ "${BACKUP_ONCE:-false}" = "true" ]; then
  backup
  exit $?
fi

while true; do
  backup || true
  sleep "$((interval_hours * 3600))"
done
