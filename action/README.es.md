---
source-sha256: 53c1dd5f4fd143fb494bfc1a6009be8cdd8e672e18a115e2e1c503ac797f9ecb
---
# Acción de upload de CodeCoverage

Una acción compuesta que descarga el uploader autónomo de CodeCoverage de una **release fijada**,
comprueba su SHA-256 y envía informes de cobertura a una instancia de CodeCoverage. Solo Bash, ninguna
acción de terceros; cada entrada llega a los scripts a través de `env:`.

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write   # the upload authenticates with the OIDC token of GitHub Actions
    steps:
      - uses: actions/checkout@v6
      # ... build and test, producing coverage.cobertura.xml ...
      - uses: gaetanthouvenin/code-coverage/action@v0.1.0-beta.2
        with:
          version: 0.1.0-beta.2
          sha256: <SHA-256 of coverage-upload-linux-x64 of v0.1.0-beta.2>   # optional, recommended
          url: https://coverage-api.example.com
          files: coverage.cobertura.xml
          flags: unit
```

Fije la acción en un tag de release, `@vX.Y.Z`, y dé el mismo número a `version`: el ejemplo usa
`v0.1.0-beta.2`. Todavía no hay un tag mayor como `@v1`: el propietario lo establecerá en la primera release
estable. La vista general de un repositorio sin cobertura, en la consola, muestra este job con la
versión y la dirección de la Api de la instancia, listo para copiar.

## Entradas

| Entrada | Obligatoria | Valor predeterminado | Significado |
|---|---|---|---|
| `version` | sí | | La release del uploader, sin la `v` (`1.2.3` para el tag `v1.2.3`). |
| `sha256` | no | | El SHA-256 que debe tener el binario del runner. El `.sha256` de la release se comprueba siempre; una suma fijada protege además contra una release reemplazada. |
| `url` | sí | | La dirección pública de la Api de CodeCoverage. |
| `token` | no | | Un token de upload (`CODECOV_TOKEN` sigue funcionando una vez importado). Vacío: OIDC, que necesita `id-token: write`. |
| `files` | no | | Archivos de informe, separados por `;` o saltos de línea. Vacío: se busca en el espacio de trabajo. |
| `flags` | no | | Flags, separados por comas. |
| `name` | no | | El nombre del upload. |
| `dry-run` | no | `false` | Prepara y muestra el upload sin enviarlo. |
| `fail-on-error` | no | `false` | Hace fallar el paso cuando la instalación o el upload fallan. |
| `download-url` | no | `https://github.com/<repository of the action>/releases/download` | Dónde se leen los archivos de la release. El repositorio de la acción procede de `github.action_repository`, o de la ruta de checkout de la acción cuando aquel está vacío. |

Salida `installed`: `true` cuando el uploader se descargó y se verificó su suma.

## Runners compatibles

`linux-x64` (glibc), `linux-musl-x64` (contenedores Alpine), `win-x64` y `osx-arm64`. Cualquier otro runner
recibe una advertencia y no se hace ningún upload.

## Los fallos nunca rompen el build

Una descarga fallida, una suma que no coincide o un runner no compatible escriben un `::warning::` que indica
la causa y terminan el paso con el código de salida 0; el paso de upload se omite. El propio uploader también termina
con 0 cuando falla un upload. Defina `fail-on-error: true` para que falle en su lugar.

`v0.1.0-beta.1` advertía de una suma distinta en todos los runners de Windows y no enviaba nada: la suma se
calculaba a partir de un nombre de archivo bajo `RUNNER_TEMP` (`D:\a\_temp\…`), que `sha256sum` escapa con una
`\` inicial. A partir de `v0.1.0-beta.2` el binario se lee por stdin, de modo que la advertencia solo significa que los bytes
difieren (M4.02g).

## De dónde procede el binario

Los binarios y sus `.sha256` son los de la release de GitHub de la versión fijada, extraídos
de la imagen de la Api del mismo tag (ADR-012, modificado para M5.01): la versión y la suma cambian
juntas, cuando se actualiza el workflow. La ruta `/api/v1/uploader/{rid}` de una instancia sigue
disponible como alternativa y para uso manual, pero sirve la versión del servidor, por lo que una suma fijada
en un workflow dejaría de coincidir en la siguiente actualización del servidor.

## Diferencias con `codecov/codecov-action`

- El binario es el uploader de CodeCoverage, no la CLI de Codecov; `codecov/codecov-action` sigue funcionando
  solo con Codecov (el shim de codecov-cli, M5.07, está aplazado).
- No hay interruptor `use_oidc`: un `token` vacío significa OIDC.
- `url` es obligatoria: no hay instancia predeterminada.
- Los mantenedores mueven a mano el tag móvil `v1` a la última release `v1.x.y`.
