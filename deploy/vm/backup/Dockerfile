# The backup of deploy/vm (M4.02) as an image of its own, for a registry: the same scripts as the
# compose file mounts, in the same pinned PostgreSQL image (pg_dump of the server's major version).
# scripts/test-backup-restore.ps1 builds it and runs a dump-and-restore drill with it.
# postgres:17-alpine (17.11)
FROM postgres:17-alpine@sha256:3540c0362c809df084ffac0ae69fe794515f9de92692f02178bcdce0ef0302ac

COPY backup.sh healthcheck.sh /opt/backup/
RUN mkdir -p /backups && chown postgres:postgres /backups

USER postgres
VOLUME ["/backups"]
HEALTHCHECK --interval=5m --timeout=10s --start-period=10m --retries=1 CMD ["/bin/sh", "/opt/backup/healthcheck.sh"]
ENTRYPOINT ["/bin/sh", "/opt/backup/backup.sh"]
