# Pruebas implementadas

## Backend (`backend/tests`)

**SchoolCafeteria.UnitTests** (SQLite en memoria — soporta transacciones reales, a diferencia del
proveedor InMemory de EF Core):

| Archivo | Casos cubiertos |
|---|---|
| `WalletLedgerServiceTests` | Débito con saldo suficiente; débito que excede el saldo (rechazado, balance sin cambios — regla 1); crédito repetido con la misma `IdempotencyKey` (no duplica — reglas 8/9); crédito que excede `MaxBalance`. |
| `InventoryLedgerServiceTests` | Salida de inventario reduce balance; salida que excede existencias se rechaza sin alterar el balance. |
| `SaleServiceTests` | Checkout descuenta cartera y reduce inventario correctamente; doble clic en "Cobrar" con la misma `IdempotencyKey` no duplica el cobro; venta con stock insuficiente revierte el débito de cartera ya aplicado (transacción atómica) sin dejar una venta huérfana. |
| `RechargeServiceTests` | Webhook de pago entregado dos veces solo acredita una vez (idempotencia de webhook); webhook con monto manipulado no completa la recarga (validación de importe). |
| `RoleServiceTests` | `SetPermissionsAsync` reemplaza el conjunto completo de permisos de un rol (agrega y quita en la misma llamada); rechaza claves de permiso desconocidas; no permite eliminar un rol con usuarios asignados ni un rol predefinido del sistema. |
| `AuthServiceEntraIdTests` | Login con Entra ID sin cuenta de personal previamente creada lanza `auth.not_provisioned`; primer login vincula la cuenta local por correo y guarda el `EntraObjectId`; logins posteriores resuelven por `EntraObjectId` aunque el correo en Entra haya cambiado; una cuenta inactiva no puede iniciar sesión por ninguna vía. |

**SchoolCafeteria.IntegrationTests** (`WebApplicationFactory` + SQLite, pipeline HTTP real):

| Archivo | Casos cubiertos |
|---|---|
| `AuthorizationTests` | `/health/live` responde 200 sin autenticación; un endpoint protegido responde 401 sin token; login con credenciales inválidas responde 422 con `application/problem+json`. |

Ejecución: `dotnet test backend/SchoolCafeteria.sln`.

## Frontend (`frontend`)

- `Button.test.tsx` (Vitest + Testing Library): renderizado, interacción por click y estado
  deshabilitado (accesible por teclado — el rol `button` y el estado `disabled` se verifican
  explícitamente).

Ejecución: `npm test` dentro de `frontend/`.

## Cobertura frente a los casos críticos del pedido (sección 19)

| Caso crítico solicitado | Cubierto por |
|---|---|
| Dos compras simultáneas contra la misma cartera | Diseño: `WalletLedgerService` usa `RowVersion` + reintento acotado; validado indirectamente por `Debit_ExceedingBalance_*`. Una prueba de concurrencia con dos `DbContext` reales queda como próximo paso (requiere SQL Server real, no SQLite, para reproducir el `rowversion` nativo). |
| Doble envío de una recarga | `Credit_CalledTwiceWithSameIdempotencyKey_OnlyAppliesOnce` |
| Webhook repetido | `Webhook_ReceivedTwiceForSameEvent_OnlyCreditsWalletOnce` |
| Fallo del correo tras una recarga | Por diseño: `NotificationOutboxService` desacopla el envío (outbox); no revierte la recarga. Prueba unitaria directa pendiente como próximo paso. |
| Fallo de inventario durante una venta | `Checkout_InsufficientStock_RollsBackWalletDebit` |
| RFID bloqueado | Por diseño: `RfidService.LookupAsync` solo resuelve credenciales `Active`; prueba unitaria dedicada pendiente. |
| Precio modificado mientras una venta está abierta | Por diseño: `SaleLine.UnitPrice` se copia en el momento de la venta, nunca referencia el precio vigente. |
| Importación con duplicados | Por diseño: `ImportService` marca `Duplicate` tanto por código repetido en el archivo como por coincidencia con un estudiante existente en modo `Create`. |
| Usuario que intenta acceder a otro colegio | Por diseño: todo query de aplicación filtra por `SchoolId` del JWT (`ApiControllerBase.SchoolId`), nunca por un id de colegio enviado por el cliente. |
| Tutor accediendo a estudiante no asociado | Por diseño: `GuardiansController.GetMyStudents` / `WalletsController.EnsureCanAccessBuyerAsync` resuelven la identidad del tutor solo desde el JWT. |
| Operador emitiendo un ajuste sin permiso | Por diseño: `wallets.adjust` no está en los permisos del rol Operador (ver matriz); `[RequirePermission]` lo bloquea con 403 antes de llegar al servicio. |

Las filas marcadas "por diseño" están garantizadas por la arquitectura (imposibilidad estructural,
no solo validación) pero no todas tienen todavía una prueba automatizada dedicada — quedan listadas
explícitamente en `docs/06-runbook.md` §7 como próximos pasos para no sobrestatear la cobertura.
