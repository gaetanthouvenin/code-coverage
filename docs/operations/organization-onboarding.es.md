---
source-sha256: 73f1dd082c287d4922d9bf73ab66f3b052f44fb78c286fced2d9be7b50470848
---
# Incorporación de una organización

Cómo una cuenta de GitHub (una organización o un usuario) se une a una instancia de CodeCoverage compartida por varias
organizaciones aisladas (ADR-013). El administrador de la instancia la admite; luego un administrador de la organización
la configura. Nada de una organización es visible desde otra.

## 1. Instalar la App

Un propietario de la cuenta de GitHub instala la GitHub App de la instancia desde
`https://github.com/apps/<slug>/installations/new`, en todos sus repositorios o en una selección. El
administrador de la instancia encuentra ese enlace en el encabezado de `/admin/organizations` (y al final de
`/setup`) y se lo envía al propietario. La App solicita:

| Permiso | Por qué |
|---|---|
| Contents: read, Metadata: read | El diff, los árboles, el `codecov.yml`, el código fuente anotado. |
| Commit statuses: read & write | Los estados `coverage/*` (luego `codecov/*`). |
| Pull requests: read & write | Los datos de la pull request y el comentario. |
| Checks: read & write | Los Check Runs, una vez que la organización activa `Notifications:ChecksEnabled` (M5.05). Una App creada antes de M5.05 lo obtiene cuando los mantenedores lo añaden; cada instalación lo acepta entonces desde GitHub. |
| Organization members: read | Los roles de la organización (admin, miembro) en la consola. |

La instalación crea una organización **pendiente**: todavía no sigue nada y se rechaza todo upload
(401 `Upload.TokenInvalid` con un token, `Repository.NotFound` con OIDC; el uploader sigue terminando con 0).
GitHub devuelve luego al propietario a la consola. Si la organización pendiente no aparece (se perdió su
webhook), el administrador de la instancia hace clic en **Comprobar las instalaciones** en `/admin/organizations`:
el worker enumera de inmediato las instalaciones de la App, en lugar de esperar la ejecución nocturna.

## 2. Admitirla

El administrador de la instancia admite la organización pendiente en `/admin/organizations` (**Admitir** en su
fila), o admite una cuenta antes de su instalación con **Admitir una cuenta**: escribe el login, la
consola lee el id de la cuenta en GitHub (`GET /users/{login}`) y muestra «login → id» para
confirmación. Un login desconocido se rechaza (`Organization.UnknownGitHubLogin`) y no se guarda nada.
El administrador que admite ve la organización de inmediato, sin volver a iniciar sesión. Procedimiento de emergencia,
sin la consola (docs/operations.md, «La línea de comandos de administración»):

```bash
dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
```

El id de la cuenta es el id numérico de la cuenta de GitHub (`gh api users/my-org --jq .id`). La admisión
inicia la sincronización de los repositorios de la instalación.

## 3. Configurarla

Un administrador de la organización (un propietario de la organización de GitHub, o el usuario de una cuenta
personal) abre `/orgs/github/<org>/settings`. Los cinco ajustes de una organización:

| Ajuste | Valor predeterminado | Cuándo cambiarlo |
|---|---|---|
| `Notifications:StatusContextPrefix` | `coverage` | `codecov` en el cambio (docs/cutover-runbook.md). |
| `Notifications:StatusesEnabled` | `true` | Desactivado para que CodeCoverage no publique nada en GitHub. |
| `Notifications:CommentsEnabled` | `true` | Desactivado para conservar solo los comentarios de pull request de Codecov durante el piloto. |
| `Notifications:ChecksEnabled` | `false` | Activado en el cambio, una vez que la instalación haya aceptado el permiso Checks. |
| `Coverage:OrganizationYaml` | ninguno | Un YAML compartido por todos los repositorios (por debajo del propio `codecov.yml` de cada repositorio). |

## 4. Tokens y badges

- **OIDC** no necesita nada: un job de GitHub Actions con `id-token: write` demuestra su repositorio.
- **Tokens de upload:** importe los valores de `CODECOV_TOKEN` (organización y repositorios) en
  `/orgs/github/<org>/upload-tokens`, mediante la API o con `tools/rollout/Import-CodecovTokens.ps1`.
  Un valor es único solo dentro de la organización.
- Los **tokens de API** (`cvp_…`, para los scripts) los crea cada usuario en la consola; actúan con los
  derechos de su usuario, en una organización.
- **Tokens de badge** de los repositorios privados: importe el graphing token de Codecov en la configuración de cada
  repositorio, o genere uno nuevo.

## 5. Lo que una organización no puede configurar (ADR-013)

Estos puntos se derivan del aislamiento de las organizaciones y son los mismos para todos:

- la App debe estar instalada y la organización admitida antes de que se acepte nada;
- no hay token de upload global: un token solo funciona en la organización que lo creó o lo importó;
- un repositorio transferido empieza de cero en su nueva organización (historial vacío, sin token de upload, nuevo
  token de badge); su historial se queda en la organización anterior;
- tras renombrar la cuenta de GitHub, el login anterior deja de resolverse y nada redirige;
- el administrador de la instancia solo ve metadatos (`/admin/operations`, `/admin/organizations`), nunca
  la cobertura de una organización; los operadores de la base de datos, de las copias de seguridad y de la supervisión lo ven
  todo (docs/operations.md).

Los logins reservados del primer diseño (un login igual a un segmento de ruta del producto) han desaparecido:
el propietario ya no es el primer segmento de una ruta (ADR-014).

## 6. Salir

Desinstalar la App suspende la organización: sus datos se conservan (todavía sin borrado), sus uploads se
rechazan, y reinstalarla desde la misma cuenta la vuelve a vincular.
