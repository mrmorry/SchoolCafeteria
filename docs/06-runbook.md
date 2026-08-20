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

Este build se generó en un entorno sin SDK de .NET ni acceso a Azure real, por lo que:

1. **No hay migraciones de EF Core generadas.** El arranque usa `Database.EnsureCreated()` para
   crear el esquema directamente desde el modelo. Antes de cualquier despliegue real: instalar el
   SDK de .NET 8, ejecutar `dotnet ef migrations add InitialCreate` dentro de
   `backend/src/SchoolCafeteria.Infrastructure`, y reemplazar el bloque de arranque en
   `Program.cs` por `dbContext.Database.MigrateAsync()`.
2. **Ninguna integración externa real está conectada**: pasarela de pagos (sandbox propio),
   proveedor de correo (SMTP/Mailhog), lector RFID (modo teclado), sistema escolar externo (CSV
   manual). Todas están detrás de interfaces (`IPaymentGateway`, `IEmailSender`,
   `IRfidReaderProvider`, `IStudentSourceAdapter`) listas para una implementación real.
2. **Los proyectos .NET y Next.js no fueron compilados ni ejecutados en este entorno** (no había
   `dotnet` ni forma de validar contra Azure real): revíselos con `dotnet build` / `npm install &&
   npm run build` como primer paso antes de desplegar.
3. **Almacenamiento de archivos** usa el sistema de archivos local del contenedor en desarrollo
   (`LocalFileStorage`); en Azure debe reemplazarse por el adaptador de Blob Storage (contrato ya
   definido, implementación pendiente) — nunca depender de disco local persistente en producción.
4. **Colas** se simulan con una tabla *outbox* en base de datos en vez de Service Bus real; el
   recurso de Service Bus ya está provisto en Bicep para cuando se conecte el adaptador real.
5. **Entra ID** no está integrado (no hay tenant en este entorno); se usa JWT propio + MFA TOTP.
   La migración a Entra ID / Entra External ID es un cambio localizado en `Infrastructure.Services`
   y `Api/Auth`, sin tocar el dominio.
6. **Gestión de roles/permisos** no tiene pantalla CRUD en el frontend todavía (se administra por
   base de datos / futuros endpoints); la matriz vigente está documentada en
   `docs/05-roles-permisos.md`.
7. **Pruebas E2E de UI** (Playwright/Cypress) y **pruebas de carga** no se incluyen en esta
   entrega; sí se incluyen pruebas unitarias e de integración del backend cubriendo los flujos
   financieros críticos (ver `docs/07-pruebas.md`).

## 7. Próximos pasos recomendados

1. Compilar y ejecutar la suite completa (`dotnet test`, `npm test`) en un entorno con SDK.
2. Generar las migraciones reales de EF Core.
3. Definir y conectar: pasarela de pago real, proveedor de correo/SMS, lector RFID físico, SIS
   externo.
4. Añadir pantallas de administración de roles/permisos y de conciliación de pagos.
5. Añadir Playwright para pruebas E2E de UI y k6/Artillery para pruebas de carga en hora pico.
6. Habilitar Entra ID para usuarios internos y Entra External ID (o el mecanismo local ya
   construido) para tutores/estudiantes, según la decisión final del colegio.
7. Activar auditoría de Azure SQL y Private Link antes de manejar datos reales de menores.
