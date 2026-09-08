# Immagine Postgres con pgBackRest incluso, per backup fisici (full) + WAL archiving continuo
# (PITR). pgBackRest deve girare nello stesso container di Postgres: archive_command legge
# PGDATA localmente, non può farlo da un sidecar separato. Vedi docs/backup-restore.md.
FROM postgres:17-alpine

RUN apk add --no-cache pgbackrest \
    && mkdir -p /var/lib/pgbackrest /var/log/pgbackrest \
    && chown -R postgres:postgres /var/lib/pgbackrest /var/log/pgbackrest

COPY pgbackrest.conf /etc/pgbackrest/pgbackrest.conf
