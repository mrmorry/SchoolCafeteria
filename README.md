# SchoolCafeteria

Plataforma web para administrar compras, recargas, inventario y operación de la cafetería de un
colegio: carteras digitales, identificación RFID, punto de venta, portal de padres/tutores,
reportes financieros y auditoría completa. Construida para operar con un solo colegio en esta
versión, con aislamiento por `SchoolId` ya presente en el modelo de datos para soportar múltiples
colegios en el futuro.

> **Estado**: MVP funcional, validado con `dotnet build`/`dotnet test`/`npm run build`/`npm test`
> reales (no solo generado) — ver **Limitaciones conocidas** más abajo antes de desplegar a
> producción (sin Azure real ni tenant de Entra ID conectados todavía). Datos de demostración
> **sintéticos** claramente identificados.

## Documentación

| Documento | Contenido |
|---|---|
| [`docs/01-analisis.md`](docs/01-analisis.md) | Resumen funcional, actores, casos de uso, supuestos, riesgos, alcance del MVP |
| [`docs/02-arquitectura.md`](docs/02-arquitectura.md) | Diagramas de contexto/contenedores/componentes/despliegue, ADRs, seguridad |
| [`docs/03-modelo-datos.md`](docs/03-modelo-datos.md) | Entidad-relación, índices, concurrencia, auditoría |
| [`docs/04-flujos-criticos.md`](docs/04-flujos-criticos.md) | Recarga digital, venta POS, anulaciones, importación |
| [`docs/05-roles-permisos.md`](docs/05-roles-permisos.md) | Matriz de roles y permisos |
| [`docs/06-runbook.md`](docs/06-runbook.md) | Observabilidad, backup/restore, rollback, **limitaciones conocidas** |
| [`docs/07-pruebas.md`](docs/07-pruebas.md) | Pruebas implementadas y su cobertura frente a los casos críticos pedidos |
| [`docs/manual-instalacion.md`](docs/manual-instalacion.md) | Instalación local, Docker, despliegue en Azure |
| [`docs/manual-usuario.md`](docs/manual-usuario.md) | Uso del portal del tutor, POS y backoffice |
| [`docs/manual-administrador.md`](docs/manual-administrador.md) | Configuración, roles, integraciones, importación |

## Estructura del repositorio

```
SchoolCafeteria/
├── backend/
│   ├── src/
│   │   ├── SchoolCafeteria.Domain/          # Entidades y reglas de negocio invariantes
│   │   ├── SchoolCafeteria.Application/     # Casos de uso, DTOs, interfaces de integración
│   │   ├── SchoolCafeteria.Infrastructure/  # EF Core, adaptadores (pagos, correo, storage, RFID)
│   │   └── SchoolCafeteria.Api/             # Controllers REST, auth JWT, middleware
│   ├── tests/
│   │   ├── SchoolCafeteria.UnitTests/
│   │   └── SchoolCafeteria.IntegrationTests/
│   └── SchoolCafeteria.sln
├── frontend/                                # Next.js 14 + TypeScript + Tailwind
│   └── src/
│       ├── app/(admin)/…                    # Backoffice: estudiantes, POS, inventario, reportes…
│       ├── app/portal/…                     # Portal del padre/tutor
│       ├── components/                      # UI reutilizable + layout
│       └── lib/                             # Cliente API, autenticación, i18n, formato
├── infra/bicep/                             # Infraestructura como código (Azure)
├── docs/                                    # Documentación de las 6 fases
├── .github/workflows/                       # CI (build+test) y CD (build/push/deploy)
├── docker-compose.yml
└── .env.example
```

## Stack

- **Frontend**: Next.js 14 (App Router) + React + TypeScript + Tailwind CSS + React Hook Form + Zod.
- **Backend**: ASP.NET Core 8 Web API, Clean Architecture (Domain/Application/Infrastructure/Api),
  Entity Framework Core, JWT + MFA (TOTP), Swagger/OpenAPI, Serilog, Application Insights.
- **Base de datos**: SQL Server / Azure SQL Database.
- **Azure**: App Service (contenedor personalizado) o Container Apps, ACR, Key Vault, Storage,
  Service Bus, Application Insights, Log Analytics — ver `infra/bicep/`.

## Ejecución local (Docker Compose)

```bash
cp .env.example .env      # edite los valores ChangeMe_* antes de continuar
docker compose up --build
```

- Frontend: http://localhost:3000
- API + Swagger: http://localhost:5000/swagger
- Correo de prueba (Mailhog): http://localhost:8025

Detalles y ejecución sin Docker: [`docs/manual-instalacion.md`](docs/manual-instalacion.md).

## Usuarios de demostración (datos sintéticos)

Contraseña para todos: `Demo#2026!`

| Correo | Rol |
|---|---|
| admin@demo.schoolcafeteria.local | Administrador |
| finanzas@demo.schoolcafeteria.local | Finanzas |
| supervisor@demo.schoolcafeteria.local | Supervisor |
| operador@demo.schoolcafeteria.local | Operador |
| auditor@demo.schoolcafeteria.local | Auditor |
| tutor1@demo.schoolcafeteria.local | Tutor (padre de los estudiantes S-1001 y S-1002) |

Ninguna de estas credenciales es real; se generan únicamente en el sembrado de datos de demostración
(`DemoDataSeeder`) al iniciar el contenedor por primera vez.

## Endpoints principales (`/api/v1/…`, documentados en Swagger)

`auth` (incluye `auth/entra-login` para el personal), `students`, `guardians`, `employees`,
`wallets`, `recharges`, `payments/webhooks/{provider}`, `rfid`, `catalog` (categorías/productos/
precios), `inventory`, `pos` (puntos de venta, cajas, turnos, ventas), `reports`, `audit`,
`settings`, `imports`, `roles` y `users` (administración de roles/permisos/personal).

## Roles, permisos e inicio de sesión con Microsoft Entra ID

- `/roles` (permiso `users.manage`): crear/editar roles y su matriz de permisos, crear cuentas de
  personal y asignarles roles. Ver [`docs/05-roles-permisos.md`](docs/05-roles-permisos.md).
- El personal interno puede además iniciar sesión con Microsoft Entra ID una vez que el colegio
  conecta un tenant real — coexiste con el login local, nunca lo reemplaza. Ver
  [`docs/06-runbook.md` §8](docs/06-runbook.md#8-conectar-un-tenant-real-de-entra-id-personal-interno)
  para el paso a paso.

## Despliegue en Azure

Tres opciones equivalentes cubiertas por `infra/bicep/`:

1. **App Service con contenedor personalizado** (por defecto, `hostingModel=appservice`): incluye
   slots `staging`/producción.
2. **Azure Container Apps** (`hostingModel=containerapps`): escalado por revisiones.
3. La misma imagen Docker es compatible con cualquier registro Azure Container Registry.

Ver [`docs/manual-instalacion.md`](docs/manual-instalacion.md) para el comando de despliegue y
`.github/workflows/cd.yml` para el pipeline de referencia.

## Pruebas implementadas

```bash
dotnet test backend/SchoolCafeteria.sln   # 19 unitarias + 3 integración (SQLite en memoria)
cd frontend && npm test                    # componentes UI
```

Cobertura detallada, incluyendo los casos críticos del pedido (doble clic, webhook repetido,
concurrencia de cartera, etc.): [`docs/07-pruebas.md`](docs/07-pruebas.md).

## Limitaciones conocidas

Ver [`docs/06-runbook.md` §6](docs/06-runbook.md#6-limitaciones-conocidas-de-esta-entrega-léase-antes-de-producción).
En resumen: no hay migraciones de EF Core generadas (se usa `EnsureCreated` para desarrollo),
ninguna integración externa real está conectada (todas tienen contrato + mock/sandbox), y Entra ID
está integrado en código pero sin un tenant real conectado todavía.

## Próximos pasos recomendados

1. Generar las migraciones reales de EF Core y reemplazar `EnsureCreated`.
2. Conectar las integraciones reales (pasarela de pago, correo, RFID físico, SIS del colegio).
3. Conectar un tenant real de Entra ID para el personal, si el colegio lo requiere (`docs/06-runbook.md` §8).
4. Añadir pruebas E2E (Playwright) y de carga (k6/Artillery) para hora pico.
5. Evaluar Entra External ID para el portal de tutores/estudiantes si se decide reemplazar el login local ahí también.
