---
source-sha256: 9a6e1ebbd225f68090327b2f36be71d3431ba7cc31e7cb946bcadcf3c9b94246
---
# Comportamientos de GitHub que verificar en dos cuentas reales (M4.02a)

ADR-013 se basa en comportamientos de GitHub que no se pudieron ejecutar desde el repositorio («Probable» en el ADR).
El propietario ejecuta esta lista en dos cuentas reales antes del cambio (M4.08): la cuenta **A** (una
organización de GitHub) y la cuenta **B** (una segunda organización, o una cuenta personal cuando una línea lo
indica), ambas con la App de una instancia de prueba instalada. Cada línea se registra como **confirmada**,
**corregida** (con el cambio del plan que abre) o **no verificable** (con su riesgo), y se fecha. Una
línea confirmada pasa de «Probable» a «Probado» en ADR-013.

| # | Comportamiento | Procedimiento | Esperado (según el código) | Evidencia que conservar | Resultado | Fecha |
|---|---|---|---|---|---|---|
| 1 | SAML y `GET /user/installations` | En una organización con inicio de sesión único SAML (GitHub Enterprise Cloud), inicie sesión en la Web con un miembro que **no** ha autorizado el SSO para la OAuth App y luego con uno que sí lo ha hecho. | Sin autorización SSO, `user/installations` omite la organización: el usuario no ve ningún repositorio de ella (`GitHubUserGateway.ListInstallationsAsync`). Con ella, la organización aparece en la lista. | Las dos listas de `/` tras iniciar sesión; la respuesta de la API (`gh api user/installations` con el token del usuario). | | |
| 2 | Colaboradores externos en esa lista | Añada un colaborador externo a un repositorio de A; inicie sesión con ese usuario. | `user/installations` enumera la instalación de A con solo los repositorios que el colaborador puede leer (`user/installations/{id}/repositories`); el rol en la organización es ninguno, el acceso al repositorio es de lectura o escritura. | La lista de repositorios mostrada; el rol resuelto en `/orgs/github/A`. | | |
| 3 | Contenido del evento `transferred` | Transfiera un repositorio de A a B. | Un evento `repository` `transferred` llega a la instancia con el id del repositorio sin cambios y el nuevo propietario; `ReconcileRepositoryTransfer` cierra la fila en A, y `SyncInstallations` (clave de transferencia) la crea en B (`HandleWebhookDeliveryCommand`). | La entrega del webhook (Recent deliveries de la App) y las filas en `/admin/operations`. | | |
| 4 | `installation_repositories` removed, recibido por el origen | Durante la transferencia de la línea 3. | La instalación de A recibe `installation_repositories` con el repositorio en `repositories_removed` (cuando A tenía repositorios seleccionados). | La entrega. | | |
| 5 | `organization` renamed | Renombre la organización A. | Llega un evento `organization` `renamed`; `RewriteRepositoryOwner` reescribe el propietario de los repositorios de A; el login anterior deja de resolverse. | La entrega; la nueva URL funciona, la anterior responde no encontrado. | | |
| 6 | `installation_target` renamed, en ambos tipos de cuenta | Renombre A (organización) y B (cuenta personal, si está disponible). | Llega un evento `installation_target` `renamed` para cada una; la fila de la organización adopta el nuevo login (`GitHubInstallationTargetEvent`). | Las entregas; `/admin/organizations`. | | |
| 7 | Secretos de Actions tras una transferencia | Antes de la línea 3, añada un secreto de repositorio `CODECOV_TOKEN` al repositorio transferido. | Registre si GitHub conserva el secreto tras la transferencia. Haga lo que haga, un token de repositorio importado de A deja de funcionar en B (rechazado), y se recomienda la rotación de los tokens de organización de A (`RotationAdvisedAt`). | El rechazo del upload en B; el banner en A. | | |
| 8 | Un repositorio público de otra cuenta leído con un token de instalación | Con el token de instalación de B, lea un repositorio público de A en el que B no está instalada. | GitHub responde (datos públicos); la instancia nunca lo usa, ya que toda lectura pasa por la instalación de la propia organización del repositorio. | La respuesta de la API. | | |
| 9 | La forma `/repositories/{id}` en cada endpoint | Ejecute una notificación de una pull request en A, luego renombre el repositorio y ejecute otra. | Todas las llamadas siguientes funcionan por id de repositorio antes y después del cambio de nombre (inventario a continuación). | Las llamadas en los registros (`github_requests_total` por endpoint) y los estados tras el cambio de nombre. | | |

Cuando la línea 1 no se puede ejecutar (no hay ninguna organización de GitHub Enterprise Cloud a mano), regístrela como **no verificable**
con su riesgo: un miembro sin autorización SSO ve menos repositorios de los que puede leer en GitHub;
no se filtra nada.

## Inventario de las rutas del gateway

Las llamadas de `GitHubAppGateway` (token de instalación salvo que se indique lo contrario). Un repositorio seguido
siempre se direcciona por su id, para que un cambio de nombre no rompa nada; solo la búsqueda de un repositorio por nombre
usa propietario y nombre.

| Llamada | Ruta |
|---|---|
| Conversión del manifiesto (JWT de la App) | `POST app-manifests/{code}/conversions` |
| Instalaciones de la App (JWT de la App) | `GET app/installations`, `GET app/installations/{id}`, `POST app/installations/{id}/access_tokens` (`GitHubAppTokenProvider`) |
| Repositorios de una instalación | `GET installation/repositories` |
| Búsqueda de un repositorio por nombre | `GET repos/{owner}/{name}/installation` (JWT de la App), luego `GET repos/{owner}/{name}` |
| Pertenencia de un usuario | `GET orgs/{org}/memberships/{user}` |
| Rama | `GET repositories/{id}/branches/{branch}` |
| Commits | `GET repositories/{id}/commits?sha=…`, `GET repositories/{id}/commits/{sha}` |
| Comparación | `GET repositories/{id}/compare/{base}...{head}` |
| Pull request y sus archivos | `GET repositories/{id}/pulls/{n}`, `GET repositories/{id}/pulls/{n}/files` |
| Árboles y contenidos | `GET repositories/{id}/git/trees/{sha}`, `GET repositories/{id}/contents/{path}?ref=…` |
| Estados | `POST repositories/{id}/statuses/{sha}` |
| Check runs (M5.05) | `POST repositories/{id}/check-runs`, `PATCH repositories/{id}/check-runs/{id}` |
| Comentarios | `GET\|POST repositories/{id}/issues/{n}/comments`, `PATCH\|DELETE repositories/{id}/issues/comments/{id}` |

`GitHubUserGateway` (token de usuario): `GET user`, `GET user/installations`,
`GET user/installations/{id}/repositories`.
