#!/usr/bin/env sh
set -eu

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
target_dir="${1:-./backups}"
mkdir -p "$target_dir"

archive_path="${target_dir}/bizhub-db-${timestamp}.tar.gz"
docker compose exec -T api sh -lc 'test -f /data/auraprints.db' >/dev/null 2>&1
docker compose exec -T api sh -lc "tar -C /data -czf - auraprints.db" > "$archive_path"

echo "Backup written to: $archive_path"
