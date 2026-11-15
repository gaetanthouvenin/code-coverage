---
source-sha256: 1017bca7ee8867d68b1870da6385f7ac4e0614ff9838d3986a279b3acd887306
---
# Migrar un repositorio desde Codecov

Esta guía traslada los pipelines de un repositorio de Codecov a una instancia de CodeCoverage. Supone que la
organización ya está incorporada (la GitHub App instalada y la organización admitida, consulte
[la guía de incorporación de una organización](operations/organization-onboarding.md)). El `codecov.yml` del
repositorio se queda como está: CodeCoverage lo lee (variantes `codecov.yml`, `.codecov.yml`, `.github/` y
`dev/`) con la misma semántica.

En los comandos siguientes, `https://coverage-api.example.com` es la dirección pública de la Api de su
instancia y `https://coverage.example.com` la de la consola Web (dos nombres de host, ADR-011).

## 1. Durante el piloto: enviar a ambos servicios

Conserve el paso de Codecov y añada un paso de CodeCoverage a su lado, para que ambos servicios vean cada commit.
CodeCoverage publica sus estados como `coverage/project` y `coverage/patch` mientras su organización
conserve el prefijo predeterminado `coverage`: nunca colisionan con `codecov/*`. `tools/rollout/Add-CoverageUpload.ps1`
abre estas pull requests por usted.

### GitHub Actions (OIDC, sin secreto)

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write          # the upload authenticates with the OIDC token of the job
    steps:
      - uses: actions/checkout@v6          # a shallow clone is enough: the diff comes from GitHub
      # ... build and test ...
      - uses: codecov/codecov-action@v5    # kept during the pilot
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
      - uses: gaetanthouvenin/code-coverage/action@v0.1.0-beta.2   # @vX.Y.Z: no @v1 before the first stable release
        if: ${{ !cancelled() }}
        with:
          version: 0.1.0-beta.2            # the same release of CodeCoverage
          sha256: <SHA-256 of coverage-upload-linux-x64 of that release>
          url: https://coverage-api.example.com
          files: coverage.cobertura.xml
          flags: unit
```

La acción descarga el uploader de la release fijada, comprueba su SHA-256 y nunca hace fallar el build
(una advertencia, código de salida 0) salvo con `fail-on-error: true`. Consulte [action/README.md](../action/README.md).

Sin la acción, lo mismo en un paso simple (el binario de la release, con su suma comprobada):

```yaml
      - name: Upload coverage to CodeCoverage
        if: ${{ !cancelled() }}
        env:
          VERSION: 1.2.3
          SHA256: <SHA-256 of coverage-upload-linux-x64>
          COVERAGE_URL: https://coverage-api.example.com
          COVERAGE_FLAGS: unit
        run: |
          curl -fsSL -o coverage-upload "https://github.com/<owner>/code-coverage/releases/download/v$VERSION/coverage-upload-linux-x64"
          echo "$SHA256  coverage-upload" | sha256sum -c - && chmod +x coverage-upload && ./coverage-upload
```

### Azure Pipelines

```yaml
resources:
  repositories:
    - repository: coverage
      type: github
      name: <owner>/code-coverage
      endpoint: <GitHub service connection>
      ref: refs/tags/v1.2.3

variables:
  - group: coverage            # COVERAGE_TOKEN (secret), UPLOADER_SHA256_LINUX_X64...

steps:
  # ... build and test ...
  - template: pipelines/templates/coverage-upload.yml@coverage
    parameters:
      version: 1.2.3
      url: https://coverage-api.example.com
      files: $(Agent.TempDirectory)/**/coverage.cobertura.xml
      flags: unit
```

La plantilla asigna `COVERAGE_TOKEN` de forma explícita (Azure Pipelines nunca pasa un secreto de forma implícita), comprueba
la suma de la release y la fijada en el grupo de variables (`UPLOADER_SHA256_{RID}`), y advierte
en lugar de fallar. Se admiten los builds de Azure Pipelines de un repositorio alojado en GitHub; los repositorios
alojados en Azure Repos no, antes de la forja Azure DevOps (ADR-014, M7). Consulte
[pipelines/demo/azure-pipelines.yml](../pipelines/demo/azure-pipelines.yml).

### Cualquier otro caso: curl

```bash
git ls-files > network.txt
curl -fsS --retry 5 -X POST "https://coverage-api.example.com/api/v1/uploads" \
  -H "Authorization: Bearer $COVERAGE_TOKEN" \
  -F slug=my-org/my-repo -F sha="$COMMIT_SHA" -F branch="$BRANCH" -F pr="$PR_NUMBER" \
  -F flags=frontend -F network=@network.txt -F file=@coverage/lcov.info || true
```

### La herramienta dotnet

En un agente con .NET 10: `dotnet tool install --global CodeCoverage.Uploader`, luego `coverage-upload`.
Cada opción recurre a una variable `COVERAGE_*` (`--flags` a `COVERAGE_FLAGS`, `--file` a
`COVERAGE_FILE`, archivos separados por `;`).

## 2. Tokens

- **OIDC (recomendado en GitHub Actions):** nada que almacenar. El token del job demuestra el
  repositorio; la instancia confía en el id numérico de la cuenta de la organización admitida.
- **Tokens de upload:** conserve sus valores de `CODECOV_TOKEN`. Un administrador de la organización los importa, y
  los pipelines conservan su secreto sin cambios (el uploader lee `COVERAGE_TOKEN` y luego `CODECOV_TOKEN`):
  - consola: `/orgs/{forge}/{org}/upload-tokens` (token de organización) o la configuración de un repositorio;
  - API: `POST /api/v1/orgs/{forge}/{org}/upload-tokens` o `POST /api/v1/repos/{forge}/{owner}/{repo}/upload-tokens`
    con `{"label": "...", "importedValue": "<CODECOV_TOKEN>"}` y un token de API `cvp_` de un administrador;
  - script: `tools/rollout/Import-CodecovTokens.ps1` (simulación de forma predeterminada, lee un CSV guardado fuera del
    repositorio).
  Un valor puede importarse una vez por organización; una segunda importación responde 409.
- **Badges de los repositorios privados:** importe el «graphing token» de Codecov como token de badge, para que
  las URL de badge de sus README solo cambien de host: `PUT /api/v1/repos/{forge}/{owner}/{repo}/badge-token`
  con `{"value": "<graphing token>"}`, o la configuración del repositorio en la consola.

## 3. Monorepos y varios uploads por commit

- `after_n_builds` por flag (`flags.<name>.after_n_builds`) funciona como en Codecov. Un
  `codecov.notify.after_n_builds` global también funciona, pero cuenta todos los uploads del commit: con jobs que
  envían flags distintos, prefiera la forma por flag.
- Cuando el último job sabe que todos los uploads han terminado, añade `--finalize` a su propio upload: el uploader
  envía sus informes y luego declara completos los uploads del commit, lo que notifica de inmediato, ignorando
  `after_n_builds` y el debounce. `--finalize` solo actúa tras un upload de la misma ejecución; un job
  sin informe llama a `POST /api/v1/repos/{forge}/{owner}/{repo}/commits/{sha}/uploads-complete`
  con la misma credencial que los uploads. Es el equivalente del `upload-complete` de Codecov.
- `manual_trigger: true` espera esa llamada.

## 4. Badges

Cambie el host de las URL de badge, nada más: `https://codecov.io/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`
pasa a ser `https://coverage-api.example.com/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`.
También se aceptan el segmento de servicio `github` y la variante `graphs`. La consola Web ofrece los
fragmentos Markdown y HTML de cada repositorio.

## 5. Historial

Antes de cancelar Codecov, importe los totales que Codecov conserva de cada commit, para que el gráfico de historial
se remonte a antes de la migración: `tools/rollout/Start-CodecovImport.ps1` llama a
`POST /api/v1/repos/{forge}/{owner}/{repo}/codecov-import` con un token de API de Codecov, que la instancia
mantiene protegido hasta que termina la importación (siete días como máximo). La importación lee la API de Codecov, que
la cancelación cierra: ejecútela primero.

## 6. El cambio

Cuando la paridad del piloto está explicada (`tools/parity`, [parity-log.md](parity-log.md)), un
administrador de la organización cambia la organización: detiene las notificaciones de Codecov y luego
establece `Notifications:StatusContextPrefix` en `codecov` (y activa `Notifications:ChecksEnabled`) en la
configuración de la organización. Los estados conservan los nombres que exigen sus protecciones de rama. La secuencia
completa está en [el runbook del cambio](cutover-runbook.md).

## Diferencias con Codecov

Cada diferencia se nombra aquí. Las del aislamiento entre organizaciones (ADR-013) y de la
identidad multiforja (ADR-014) no son configurables por diseño; las demás sí.

| Diferencia | Configurable | Por qué |
|---|---|---|
| La App debe estar instalada y la organización admitida antes de que se acepten uploads (401 `Upload.TokenInvalid` con un token, `Repository.NotFound` con OIDC; el uploader sigue terminando con 0). | No (ADR-013) | La admisión es cerrada. |
| No hay token de upload global: un token solo funciona en la organización que lo creó o lo importó. | No (ADR-013) | Aislamiento. |
| Un repositorio transferido empieza con un historial vacío, sin token de upload y con un nuevo token de badge. | No (ADR-013) | El historial anterior se queda en la organización anterior. |
| Tras renombrar la cuenta de GitHub, el propietario anterior deja de resolverse (los badges indican `unknown`); sin redirección. | No (ADR-013) | Un login reutilizado nunca debe llegar a la organización anterior. |
| La API de gestión y las páginas Web llevan la forja en sus rutas (`/api/v1/repos/github/...`, `/github/my-org/my-repo`); la API de upload y las URL de badge conservan las formas de Codecov. | No (ADR-014) | Varias forjas comparten una instancia. |
| Los estados se llaman `coverage/*` hasta que la organización establece `Notifications:StatusContextPrefix` en `codecov`. | Sí, por organización | El piloto y Codecov funcionan en paralelo. |
| Estados, no Check Runs, hasta que la organización activa `Notifications:ChecksEnabled` (tras aceptar el permiso Checks de la App); después, Check Runs con los mismos nombres y anotaciones de patch, como Codecov. | Sí, por organización | Cada organización concede el permiso a su propio ritmo. |
| Valores predeterminados de un repositorio sin `codecov.yml`: el perfil histórico (estados project y patch), no los valores predeterminados centrados en el patch de Codecov de 2024. | Sí (`codecov.yml`, o el YAML de la organización) | ADR-009, PLAN §11-7. |
| Historial importado: los commits importados de Codecov solo tienen totales (sin archivos, sin sesiones); sus páginas no muestran ningún archivo, solo el gráfico de historial los muestra, y nunca se comparan. | No (la API v2 solo da totales) | M5.06. |
| `comment.behavior: spammy` y `new` pueden publicar comentarios idénticos con más frecuencia: CodeCoverage vuelve a notificar cuando llega tarde un informe de base, en el barrido y en una renotificación manual. | No | Se prioriza la actualidad frente al silencio. |
| `comment.behavior: new`: cuando GitHub se niega a eliminar el comentario anterior (403, 422), CodeCoverage lo registra y publica el nuevo comentario de todos modos, por lo que el anterior puede quedarse a su lado. Codecov probablemente se detiene en su lugar (`no_permissions`); esto no está probado. | No | Se mantiene hasta comprobar el comportamiento de Codecov (ARCHITECTURE §8.4). |
| `codecov/codecov-action` y codecov-cli no se comunican con CodeCoverage (el shim de compatibilidad está aplazado hasta que el piloto mida la necesidad). | — | M5.07. |
