# Security policy

## Supported versions

Only the latest release receives security fixes. Fixes are made on `main` and shipped with the next release; older versions are not patched, upgrade instead.

## Reporting a vulnerability

Do not report a security vulnerability through a GitHub issue, pull request, discussion or commit message.

Contact the maintainers of this repository privately (GitHub private vulnerability reporting) and include:

- the affected component and version, or the commit;
- a description of the vulnerability and its impact;
- the steps to reproduce it, or a proof of concept;
- any known mitigation or suggested fix.

The maintainers confirm receipt, keep you informed of the progress of the fix and tell you when it is released. Keep the details confidential until then.

## Scope

This policy covers CodeCoverage: the self-hosted code coverage service (coverage report ingestion and processing, GitHub commit statuses and pull request comments, badges and coverage history, its API, its web console) and the coverage-upload tool. Vulnerabilities in a third-party dependency are in scope when they affect this repository; report them to the dependency's maintainers as well.

## Secrets

Never commit secrets: `.env` files, connection strings, passwords, API keys, certificates or private keys. If a secret has been committed or exposed, report it right away as a vulnerability so that it can be revoked and rotated: removing it from the Git history is not enough.
