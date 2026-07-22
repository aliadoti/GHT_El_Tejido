# 12 — CI/CD con GitHub Actions (sin Bicep en el MVP)

**Propósito:** definir build, test y despliegue automatizado a Azure usando **GitHub Actions**. Decisión del MVP: **no** se usa Bicep/IaC; los recursos los crea un humano una sola vez siguiendo `Guias_Implementacion/Guia_Azure_Portal_Paso_a_Paso.md`. Las Actions solo **construyen y despliegan código** sobre recursos existentes.

**Implementa:** `ARQ §2.3` (un entorno), endurecimiento continuo de `ARQ §17` (IaC completa queda para post-MVP).

---

## 1. Prerrequisitos (los completa el humano antes)
1. Recursos Azure creados por el portal (App Service, Cosmos, Key Vault, Blob, App Insights) — guía de Azure.
2. App de WhatsApp configurada — guía de Meta.
3. **Managed Identity** del App Service con rol *Key Vault Secrets User* y acceso a Cosmos/Blob (guía de Azure §RBAC).
4. Secretos cargados en Key Vault (la app los lee en runtime; las Actions **no** necesitan los secretos de runtime).

---

## 2. Autenticación de GitHub → Azure (OIDC, sin secretos de larga vida)
Usar **federación de identidades (OIDC)** con `azure/login`, no un service principal con secreto. Pasos (resumen; detalle en guía de Azure §App registration / Federated credentials):
- Crear una App Registration (o usar Managed Identity de usuario) con permiso para desplegar al App Service (rol *Contributor* sobre el resource group, o más acotado: *Website Contributor*).
- Configurar **federated credential** para el repo/branch de GitHub.
- Guardar en **GitHub → Settings → Secrets and variables → Actions** (como *variables*, no secretos sensibles): `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_WEBAPP_NAME`.

> Alternativa más simple si OIDC no es viable: *Publish Profile* del App Service guardado como secreto `AZURE_WEBAPP_PUBLISH_PROFILE`. OIDC es preferible por seguridad.

---

## 3. Pipelines

### 3.1 CI — `.github/workflows/ci.yml` (en cada PR y push a ramas)
Objetivo: build + test + lint. **Bloquea el merge** si falla.

```yaml
name: CI
on:
  pull_request:
  push:
    branches: [main]
jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release -warnaserror
      - run: dotnet format --verify-no-changes
      - run: dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '22.x', cache: 'npm', cache-dependency-path: src/ElTejido.Web/package-lock.json }
      - working-directory: src/ElTejido.Web
        run: npm ci
      - working-directory: src/ElTejido.Web
        run: npm run lint
      - working-directory: src/ElTejido.Web
        run: npm run test -- --watch=false --browsers=ChromeHeadless
      - working-directory: src/ElTejido.Web
        run: npm run build -- --configuration production
```

### 3.2 CD — `.github/workflows/deploy.yml` (al hacer merge a `main`)
Objetivo: construir el artefacto único (API + SPA embebido) y desplegar al App Service.

```yaml
name: Deploy
on:
  push:
    branches: [main]
  workflow_dispatch:
permissions:
  id-token: write      # requerido para OIDC
  contents: read
jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    environment: production   # habilita aprobación manual (gate) si se configura
    steps:
      - uses: actions/checkout@v4

      # 1) Build SPA Angular
      - uses: actions/setup-node@v4
        with: { node-version: '22.x', cache: 'npm', cache-dependency-path: src/ElTejido.Web/package-lock.json }
      - working-directory: src/ElTejido.Web
        run: npm ci && npm run build -- --configuration production
      # copiar dist al wwwroot del API
      - run: |
          rm -rf src/ElTejido.Api/wwwroot
          mkdir -p src/ElTejido.Api/wwwroot
          cp -r src/ElTejido.Web/dist/*/browser/* src/ElTejido.Api/wwwroot/

      # 2) Build + publish backend (incluye wwwroot)
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet publish src/ElTejido.Api/ElTejido.Api.csproj -c Release -o ./publish

      # 3) Login a Azure (OIDC) y deploy
      - uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_WEBAPP_NAME }}
          package: ./publish

      # 4) Smoke test
      - run: curl -fsS https://${{ vars.AZURE_WEBAPP_NAME }}.azurewebsites.net/health
```

> La ruta exacta del build de Angular 22 (`dist/<app>/browser/`) la confirma el agente de frontend; ajustar el `cp` si el output difiere. El paso de smoke test usa `/health` (`04 §7`).

---

## 4. Configuración de la app en Azure (no en el repo)
- Las **Application settings** del App Service (endpoints de Cosmos/Blob/Key Vault, `Llm:Provider`, nombres de secretos, etc.) las define el humano en el portal (guía de Azure §App Service → Configuration). Las Actions **no** las escriben en el MVP.
- Si se quiere automatizar settings más adelante, se añade un paso `azure/cli` o se migra a Bicep (post-MVP, `ARQ §17`).

---

## 5. Estrategia de ramas y entornos (`01 §6`)
- `main` desplegable; CD se dispara al merge.
- Un entorno (`production`/`mvp`). Opcional: usar un **deployment slot** `staging` y `slot swap` para zero-downtime (`ARQ §2.3`). MVP puede desplegar directo a producción con el gate de `environment`.
- Proteger `main`: requiere CI verde + 1 review.

---

## 6. Seguridad del pipeline
- OIDC en vez de secretos de larga vida.
- Permisos mínimos del token (`id-token: write`, `contents: read`).
- Ningún secreto de runtime (API keys del LLM, tokens de WhatsApp) pasa por GitHub: viven solo en Key Vault y los lee la app con Managed Identity.
- Dependabot/actualización de acciones ancladas por versión mayor (`@v4`).

---

## 7. Criterios de aceptación
- Un PR con fallo de build/test/lint no se puede mergear.
- Un merge a `main` despliega automáticamente y el smoke test de `/health` pasa.
- Ningún secreto de runtime aparece en logs de Actions ni en el repo.

*Fin del documento.*
