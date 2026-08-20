# Manual de instalación

## Requisitos previos

- Docker y Docker Compose v2 (para ejecución local completa).
- Opcional para desarrollo fuera de contenedores: .NET SDK 8, Node.js 20+, SQL Server local.
- Para despliegue en Azure: suscripción activa, Azure CLI (`az`), permisos para crear grupos de
  recursos, y (si usa el pipeline) un *service principal* con OIDC configurado en GitHub Actions.

## Instalación local con Docker Compose

```bash
git clone <url-del-repositorio>
cd SchoolCafeteria
cp .env.example .env
# Edite .env y reemplace los valores "ChangeMe_*" por valores propios (no reales/productivos).

docker compose up --build
```

Servicios expuestos:

| Servicio | URL |
|---|---|
| Frontend (web) | http://localhost:3000 |
| API (Swagger en Development) | http://localhost:5000/swagger |
| Mailhog (bandeja de correo de pruebas) | http://localhost:8025 |
| SQL Server | localhost:1433 |

La primera vez que la API arranca crea el esquema de base de datos y siembra datos de
demostración sintéticos (ver `docs/06-runbook.md` sobre por qué se usa `EnsureCreated` en vez de
migraciones en este build, y qué reemplazar antes de producción).

## Ejecución sin Docker (desarrollo)

Backend:

```bash
cd backend
dotnet restore SchoolCafeteria.sln
dotnet user-secrets set "Jwt:SigningKey" "un-valor-aleatorio-de-32-caracteres-o-mas" --project src/SchoolCafeteria.Api
dotnet run --project src/SchoolCafeteria.Api
```

Requiere una instancia de SQL Server accesible y `ConnectionStrings:Default` configurada (por
`appsettings.Development.json`, variable de entorno, o `dotnet user-secrets`).

Frontend:

```bash
cd frontend
cp .env.local.example .env.local
npm install
npm run dev
```

## Pruebas

```bash
# Backend
dotnet test backend/SchoolCafeteria.sln

# Frontend
cd frontend && npm test
```

## Despliegue en Azure

Ver `docs/06-runbook.md` para observabilidad, backup y rollback, y `.github/workflows/cd.yml` para
el pipeline de referencia. Resumen manual con Azure CLI:

```bash
az login
az group create --name rg-schoolcafeteria-dev --location eastus2

az deployment group create \
  --resource-group rg-schoolcafeteria-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.example.json \
  --parameters sqlAdminPassword="<secreto-real>" jwtSigningKey="<secreto-real>"
```

Antes de este paso: construya y publique las imágenes en el Azure Container Registry provisionado
por el propio Bicep (o uno existente), y pase su `loginServer` en los parámetros `apiImage`/`webImage`.
