#!/bin/sh
set -eu

project_dir="${1:-.}"
backup_dir="${2:-./backups}"
timestamp="$(date +%Y%m%d_%H%M%S)"

mkdir -p "$backup_dir"
cd "$project_dir"

docker compose exec -T db \
  pg_dump --clean --if-exists --no-owner --username mealprep mealprep \
  | gzip > "$backup_dir/mealprep_${timestamp}.sql.gz"

find "$backup_dir" -type f -name "mealprep_*.sql.gz" -mtime +30 -delete
echo "Backup written to $backup_dir/mealprep_${timestamp}.sql.gz"
