#!/usr/bin/env bash
# MySQL + app files backup for the Contabo VPS.
# Daily compressed dump per database, local retention, optional offsite copy via rclone.
# Install: /usr/local/bin/mysql-backup.sh (chmod 700), run from root's cron.
set -uo pipefail

# ---- Config ----------------------------------------------------------------------------
BACKUP_ROOT="/var/backups/mysql"
RETENTION_DAYS=14                       # local copies to keep
EXCLUDE_DBS="information_schema|performance_schema|mysql|sys"
FILE_PATHS=(                            # app files that live outside MySQL (empty = skip)
  "/var/www/pcuganda-admin/uploads"
)
RCLONE_REMOTE=""                        # e.g. "contabo-s3:pca-backups" ; empty = local only
REMOTE_RETENTION_DAYS=60
LOG="/var/log/mysql-backup.log"
# ----------------------------------------------------------------------------------------

STAMP="$(date +%Y-%m-%d_%H%M)"
DEST="$BACKUP_ROOT/$STAMP"
FAILED=0

log() { echo "$(date '+%F %T') $*" | tee -a "$LOG"; }

exec 9>/var/lock/mysql-backup.lock
flock -n 9 || { log "Another backup is running, exiting."; exit 1; }

mkdir -p "$DEST" && chmod 700 "$BACKUP_ROOT" "$DEST"
log "Backup start -> $DEST"

DBS=$(mysql -N -B -e "SHOW DATABASES;" | grep -Ev "^(${EXCLUDE_DBS})$")
if [ -z "$DBS" ]; then
  log "ERROR: no databases found (MySQL down or no access)"; exit 1
fi

for DB in $DBS; do
  OUT="$DEST/${DB}.sql.gz"
  if mysqldump --single-transaction --quick --routines --triggers --events \
       --hex-blob --no-tablespaces --set-gtid-purged=OFF --databases "$DB" \
       2>>"$LOG" | gzip -6 > "$OUT" \
     && gzip -t "$OUT" && [ "$(zcat "$OUT" | tail -1 | grep -c 'Dump completed')" -eq 1 ]; then
    log "OK   $DB ($(du -h "$OUT" | cut -f1))"
  else
    log "FAIL $DB"; FAILED=1
  fi
done

for P in "${FILE_PATHS[@]}"; do
  [ -d "$P" ] || continue
  NAME="files_$(echo "$P" | tr '/' '_' | sed 's/^_//').tar.gz"
  if tar -czf "$DEST/$NAME" -C "$(dirname "$P")" "$(basename "$P")" 2>>"$LOG"; then
    log "OK   files $P"
  else
    log "FAIL files $P"; FAILED=1
  fi
done

(cd "$DEST" && sha256sum ./* > SHA256SUMS)

find "$BACKUP_ROOT" -mindepth 1 -maxdepth 1 -type d -mtime +"$RETENTION_DAYS" -exec rm -rf {} +

if [ -n "$RCLONE_REMOTE" ]; then
  if rclone copy "$DEST" "$RCLONE_REMOTE/$STAMP" --log-file "$LOG" --log-level NOTICE; then
    log "OK   offsite copy to $RCLONE_REMOTE/$STAMP"
    rclone delete "$RCLONE_REMOTE" --min-age "${REMOTE_RETENTION_DAYS}d" --log-file "$LOG" --log-level NOTICE
    rclone rmdirs "$RCLONE_REMOTE" --leave-root --log-file "$LOG" --log-level NOTICE
  else
    log "FAIL offsite copy"; FAILED=1
  fi
fi

if [ "$FAILED" -eq 0 ]; then log "Backup finished OK"; else log "Backup finished WITH ERRORS"; fi
exit "$FAILED"
