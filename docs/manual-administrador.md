# Manual del administrador

## Configuración del colegio

Toda la configuración vive en la tabla `SystemSetting` (pantalla `/settings`, requiere el permiso
`settings.write`) y nunca está codificada en el software:

| Clave | Descripción | Valor sembrado |
|---|---|---|
| `currency.default` | Moneda por defecto del colegio (código ISO 4217) | `USD` |
| `wallet.default_low_balance_threshold` | Umbral sugerido de alerta de balance bajo | `5.00` |
| `wallet.allow_negative_balance` | Si se permiten compras que dejen saldo negativo | `false` |
| `pos.allow_sales_without_stock` | Si el POS puede vender sin existencias | `false` |

## Usuarios, roles y permisos

- Los roles y su matriz de permisos se administran hoy directamente en base de datos
  (`Role`, `Permission`, `RolePermission`, `UserRole`) — ver `docs/05-roles-permisos.md` para la
  matriz vigente y `docs/06-runbook.md` §6 para el estado de la pantalla de administración (aún no
  incluida en el frontend de este build).
- Para crear un nuevo usuario interno: insertar en `User` con `PasswordHash` generado por
  `IPasswordHasher` (nunca en texto plano) y asociar `UserRole`.
- Para un nuevo tutor: crear el `Guardian`, luego un `User` con `GuardianId` apuntando a él y el
  rol `Tutor`.

## Integraciones

Todas las integraciones externas están detrás de una interfaz reemplazable por configuración —
ningún cambio de proveedor requiere tocar el dominio:

| Integración | Interfaz | Implementación en este build | Para producción |
|---|---|---|---|
| Pagos | `IPaymentGateway` | `SandboxPaymentGateway` (simulado, firma HMAC) | Adaptador del PSP elegido |
| Correo | `IEmailSender` | `SmtpEmailSender` → Mailhog en local | Azure Communication Services / SendGrid |
| Almacenamiento de archivos | `IFileStorage` | `LocalFileStorage` (disco del contenedor, no persistente) | Adaptador de Azure Blob Storage |
| Fuente de estudiantes externa | `IStudentSourceAdapter` | `CsvStudentSourceAdapter` (carga manual) | Adaptador API del SIS del colegio |
| Lector RFID | `IRfidReaderProvider` | Modo teclado (sin servidor) | WebUSB/WebSerial o agente local, según hardware elegido |

## Importación masiva de estudiantes

1. `/students/import` → descargar plantilla CSV.
2. Completar y cargar el archivo.
3. Revisar la vista previa: filas válidas, duplicadas y con error se listan explícitamente.
4. Confirmar — el proceso es idempotente: repetir la confirmación (o re-subir el mismo archivo) no
   duplica estudiantes, se identifica por `StudentCode`.
5. El resultado y quién ejecutó la importación quedan en `ImportJob`/`AuditLog`.

## Auditoría y cumplimiento

- Todo cambio sobre entidades auditables genera automáticamente un `AuditLog` en la misma
  transacción de base de datos que el cambio de negocio (interceptor de EF Core), por lo que nunca
  hay una operación sin su rastro correspondiente.
- Los campos marcados `[Sensitive]` en el dominio (contraseñas, secretos MFA, hash de RFID,
  payload crudo de webhooks) se excluyen explícitamente de lo que el interceptor persiste.
- El identificador RFID nunca se almacena ni se muestra completo — solo un hash (para
  unicidad/búsqueda) y un valor enmascarado (`****1234`).

## Configuración de puntos de venta

`/pos` (creación vía API `/api/v1/pos/points-of-sale` y `/registers`, permiso `users.manage`):
cada punto de venta tiene un almacén por defecto, uno o más registros (cajas), y cada caja abre
turnos que agrupan sus ventas y recargas presenciales para el cierre y la conciliación.
