# Runbook operativo

## 1. Salud del sistema

- `GET /health/live` — liveness, no toca dependencias externas. Falla solo si el proceso está
  colgado; un fallo aquí justifica reiniciar la instancia.
- `GET /health/ready` — readiness, verifica conectividad a Azure SQL. Falla aquí = sacar la
  instancia del balanceo, no reiniciarla ciegamente; investigar la base de datos primero.
- App Service / Container Apps deben apuntar sus *health probes* a `/health/ready` y su *startup
  probe* a `/health/live`.

## 2. Observabilidad

- **Application Insights**: latencia por endpoint, tasa de error 5xx, dependencias (SQL, SMTP).
- **Log Analytics**: logs estructurados (Serilog) con `CorrelationId` por request — todo
  `WalletTransaction`, `Sale`, `Notification` y `AuditLog` relevante guarda ese mismo
  `CorrelationId`, permitiendo reconstruir un incidente de punta a punta.
- **Alertas técnicas recomendadas** (a configurar en Azure Monitor):
  - Tasa de errores 5xx > 2% en 5 minutos.
  - Latencia p95 de `/api/v1/pos/sales` > 2s.
  - `NotificationDispatcherService`: notificaciones en `DeadLettered` > 0 en 15 minutos.
  - SQL DTU/vCore > 80% sostenido.
  - Fallos de autenticación (`auth.invalid_credentials`) > umbral por IP — señal de fuerza bruta.

## 3. Backup y restauración

- **Azure SQL**: backups automáticos (7–35 días según SKU) + *point-in-time restore* nativo de
  Azure SQL Database. Para un punto de restauración a demanda: `az sql db export` a Blob Storage.
- **Azure Blob Storage** (imports, imágenes, reportes): habilitar *soft delete* de blobs y
  *versioning* en el Storage Account.
- **Procedimiento de restauración**:
  1. Restaurar la base de datos a un servidor lógico temporal con `az sql db restore`.
  2. Validar integridad (conteos de `WalletTransaction`, `Sale` contra el respaldo de reportes).
  3. Cambiar la cadena de conexión en Key Vault al servidor restaurado o renombrar bases.
  4. Nunca sobrescribir la base de datos productiva directamente sin el paso 2.

## 4. Estrategia de rollback

- **App Service**: usar el slot `staging` — desplegar la nueva imagen ahí, verificar
  `/health/ready` y humo funcional, luego `az webapp deployment slot swap`. Rollback = swap
  inverso (instantáneo, sin rebuild).
- **Container Apps**: cada despliegue crea una revisión nueva; rollback = mover el 100% del
  tráfico a la revisión anterior con `az containerapp revision set-mode` / `az containerapp
  ingress traffic set`.
- **Base de datos**: los cambios de esquema deben ser *backward-compatible* con la revisión
  anterior de la API durante la ventana de despliegue (regla general de migraciones aditivas).

## 5. Incidentes comunes

| Síntoma | Causa probable | Acción |
|---|---|---|
| `/health/ready` en rojo | SQL inaccesible / firewall | Revisar reglas de firewall de Azure SQL y managed identity |
| Notificaciones no llegan | Proveedor SMTP caído | Revisar `Notification.Status = DeadLettered`; no afecta operaciones financieras ya completadas |
| Venta rechazada con 422 `wallet.insufficient_funds` | Comportamiento esperado | No es un bug — validar con el usuario |
| `409 Conflict` en cartera | Alta concurrencia sobre la misma cartera | Reintentar; si persiste, revisar picos de tráfico en el POS |
| Import atascado en `Importing` | Proceso interrumpido a mitad de ejecución | Revisar `ImportJobRow` para filas sin `Imported`/`Skipped`, re-ejecutar `confirm` (es idempotente) |

## 6. Limitaciones conocidas de esta entrega (léase antes de producción)

1. **No hay migraciones de EF Core generadas.** El arranque usa `Database.EnsureCreated()` para
   crear el esquema directamente desde el modelo. Antes de cualquier despliegue real: ejecutar
   `dotnet ef migrations add InitialCreate` dentro de `backend/src/SchoolCafeteria.Infrastructure`,
   y reemplazar el bloque de arranque en `Program.cs` por `dbContext.Database.MigrateAsync()`.
2. **Ninguna integración externa real está conectada**: pasarela de pagos (sandbox propio),
   proveedor de correo (SMTP/Mailhog), lector RFID (modo teclado), sistema escolar externo (CSV
   manual). Todas están detrás de interfaces (`IPaymentGateway`, `IEmailSender`,
   `IRfidReaderProvider`, `IStudentSourceAdapter`) listas para una implementación real.
3. **Almacenamiento de archivos** usa el sistema de archivos local del contenedor en desarrollo
   (`LocalFileStorage`); en Azure debe reemplazarse por el adaptador de Blob Storage (contrato ya
   definido, implementación pendiente) — nunca depender de disco local persistente en producción.
4. **Colas** se simulan con una tabla *outbox* en base de datos en vez de Service Bus real; el
   recurso de Service Bus ya está provisto en Bicep para cuando se conecte el adaptador real.
5. **Microsoft Entra ID** está integrado para el personal interno (ver §8) pero **sin un tenant
   real configurado** — `EntraId:TenantId`/`ClientId` quedan vacíos por defecto, lo que oculta el
   botón "Iniciar sesión con Microsoft" y hace que `/api/v1/auth/entra-login` responda
   `auth.entra_not_configured`. El login local (correo/contraseña + MFA TOTP) sigue siendo la vía
   principal y funciona sin ninguna configuración adicional. Entra External ID (portal de
   tutores/estudiantes) no está integrado — se evaluó y se decidió mantener esos dos roles en el
   login local únicamente.
6. **Pruebas E2E de UI** (Playwright/Cypress) y **pruebas de carga** no se incluyen en esta
   entrega; sí se incluyen pruebas unitarias e de integración del backend cubriendo los flujos
   financieros críticos y de autenticación (ver `docs/07-pruebas.md`).
7. **Next.js 14.2.35 / eslint-config-next**: quedan algunas vulnerabilidades de baja/moderada
   severidad que solo se resuelven saltando a Next 15/16 (cambio mayor no incluido en este MVP).

## 7. Próximos pasos recomendados

1. Generar las migraciones reales de EF Core (ver §6.1).
2. Definir y conectar: pasarela de pago real, proveedor de correo/SMS, lector RFID físico, SIS
   externo.
3. Conectar un tenant real de Entra ID para el personal (ver §8) si el colegio lo requiere.
4. Añadir Playwright para pruebas E2E de UI y k6/Artillery para pruebas de carga en hora pico.
5. Evaluar Entra External ID para el portal de tutores/estudiantes si se decide reemplazar el
   login local ahí también.
6. Activar auditoría de Azure SQL y Private Link antes de manejar datos reales de menores.

## 8. Conectar un tenant real de Entra ID (personal interno)

El backend y el frontend ya están listos para autenticar al personal (Administrador, Finanzas,
Supervisor, Operador, Auditor) contra Microsoft Entra ID — falta únicamente registrar la
aplicación en un tenant real y completar la configuración. Tutores y estudiantes **no** usan este
mecanismo, siguen con el login local.

1. **Registrar la aplicación** en [Azure Portal → Microsoft Entra ID → App registrations → New
   registration]. Tipo de cuenta: "Accounts in this organizational directory only" (single-tenant)
   salvo que el colegio requiera otra cosa.
2. **Plataforma**: agregar "Single-page application (SPA)" con el redirect URI de la app web (p.
   ej. `https://cafeteria.micolegio.edu/` en producción, `http://localhost:3000/` en local). MSAL
   usa `loginPopup`, por lo que basta con el origen — no se necesita un callback path específico.
3. **Anotar** el *Application (client) ID* y el *Directory (tenant) ID* de la página "Overview".
4. **Backend** (`backend/src/SchoolCafeteria.Api/appsettings.json` o, mejor, variables de entorno /
   Key Vault en producción):
   ```json
   "EntraId": { "Instance": "https://login.microsoftonline.com/", "TenantId": "<tenant-id>", "ClientId": "<client-id>" }
   ```
   En Docker Compose, definir `ENTRA_TENANT_ID` y `ENTRA_CLIENT_ID` en `.env`.
5. **Frontend**: definir `NEXT_PUBLIC_ENTRA_CLIENT_ID` y `NEXT_PUBLIC_ENTRA_TENANT_ID` (mismos
   valores) — son variables `NEXT_PUBLIC_*`, se inyectan en tiempo de build (ver
   `frontend/.env.local.example` y los `ARG` del `Dockerfile`).
6. **Provisionar las cuentas de staff primero.** El login con Entra ID nunca crea una cuenta
   automáticamente — un Administrador debe crear cada usuario de personal desde `/roles`
   (sección "Usuarios de personal") con el mismo correo que tiene en Entra ID, y asignarle su rol,
   **antes** de que esa persona pueda iniciar sesión con Microsoft. La primera vez que inicia
   sesión con Entra ID, el sistema vincula automáticamente esa cuenta por correo (guardando el
   `oid` de Entra); a partir de ahí el vínculo es por `oid`, no por correo, así que un cambio de
   correo en Entra ID no rompe el acceso.
7. Reiniciar/redesplegar la API y el frontend. Verificar en `/login` que aparezca el botón
   "Iniciar sesión con Microsoft".
