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

## Usuarios, roles y permisos (`/roles`, requiere permiso `users.manage`)

- **Roles**: crear un rol nuevo, y para cada rol (predefinido o propio) marcar/desmarcar los
  permisos agrupados por módulo; "Guardar permisos" reemplaza el conjunto completo asignado a ese
  rol. Los roles predefinidos del sistema (Administrador, Finanzas, Supervisor, Operador, Auditor,
  Tutor) no pueden eliminarse; ningún rol puede eliminarse mientras tenga usuarios asignados.
- **Usuarios de personal**: crear una cuenta de staff (correo, nombre, contraseña temporal de al
  menos 12 caracteres, rol inicial opcional), asignarle roles adicionales, y activar/desactivar la
  cuenta (clic sobre la insignia de estado). Esta sección es solo para personal interno — los
  tutores se crean automáticamente al vincularlos a un estudiante, y los estudiantes/empleados no
  tienen cuenta de login propia salvo que se configure explícitamente.
- Para un nuevo tutor: crear el `Guardian` desde `/guardians` (o al dar de alta un estudiante),
  luego un `User` con `GuardianId` apuntando a él y el rol `Tutor` — este flujo aún se hace por
  API/base de datos, no tiene pantalla dedicada en este build.

### Inicio de sesión con Microsoft Entra ID (personal)

El personal (no tutores ni estudiantes) puede iniciar sesión con su cuenta de Microsoft Entra ID
además de correo/contraseña, una vez que el colegio conecta un tenant real — ver
`docs/06-runbook.md` §8 para el paso a paso completo. Puntos clave:

- El login con Entra ID **nunca crea una cuenta automáticamente**: la cuenta debe existir primero
  en `/roles` → "Usuarios de personal", con el mismo correo que la persona usa en Microsoft.
- La primera vez que alguien inicia sesión con Microsoft, el sistema vincula esa cuenta a su
  `User` local por correo; de ahí en adelante el vínculo es por el identificador de objeto de
  Entra ID, no por correo.
- Mientras no haya un tenant configurado (`EntraId:TenantId`/`ClientId` vacíos), el botón
  "Iniciar sesión con Microsoft" simplemente no aparece — el login local sigue funcionando sin
  ningún cambio.

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
