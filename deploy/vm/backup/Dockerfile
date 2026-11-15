# The backup of deploy/vm (M4.02) as an image of its own, for a registry: the same scripts as the
# compose file mounts, in the same pinned PostgreSQL image (pg_dump of the server's major version).
# scripts/test-backup-restore.ps1 builds it and runs a dump-and-restore drill with it.
# postgres:18-alpine (18.7)
FROM postgres:18-alpine@sha256:b6972e1115a4ff8eba5d7f7d19184926d1e16d7d1136ae1e1a7ebf286da0f015

COPY backup.sh healthcheck.sh /opt/backup/
RUN mkdir -p /backups && chown postgres:postgres /backups

USER postgres
VOLUME ["/backups"]
HEALTHCHECK --interval=5m --timeout=10s --start-period=10m --retries=1 CMD ["/bin/sh", "/opt/backup/healthcheck.sh"]
ENTRYPOINT ["/bin/sh", "/opt/backup/backup.sh"]
