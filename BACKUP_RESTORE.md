# Backup and Restore

## Backup

- Run `./backup-db.sh` from the repository root.
- Optional custom target folder: `./backup-db.sh ./backups`.
- Store resulting archives in offsite storage and keep at least 7 daily + 4 weekly backups.

## Restore Drill (staging first)

1. Stop the app stack: `docker compose down`.
2. Extract one archive into a temporary folder.
3. Replace `auraprints.db` inside the `bizhub-data` volume with the extracted DB.
4. Start services: `docker compose up -d`.
5. Verify login, project list, finance, and roadmap data.

## Minimum Policy

- Run one backup every 6 hours.
- Encrypt archives at rest in your backup destination.
- Perform a restore test at least once per month.
