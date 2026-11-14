#!/bin/sh
# Healthcheck of the backup of deploy/vm (M4.02): unhealthy when no backup succeeded in the last
# BACKUP_INTERVAL_HOURS (24) plus two hours of margin, so that a stopped backup shows in docker ps.
set -eu

directory=${BACKUP_DIRECTORY:-/backups}
interval_hours=${BACKUP_INTERVAL_HOURS:-24}
case "$interval_hours" in
  ''|*[!0-9]*) interval_hours=24 ;;
esac
max_age=$(((interval_hours + 2) * 3600))

[ -f "$directory/.last-success" ] || exit 1
last=$(cat "$directory/.last-success")
now=$(date -u +%s)

[ $((now - last)) -le "$max_age" ]
