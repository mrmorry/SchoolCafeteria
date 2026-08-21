# Matriz de roles y permisos

Los permisos son datos (tabla `Permission`/`RolePermission`), no nombres de rol codificados en el
código de autorización — la API valida claims `permission` embebidos en el JWT en el momento del
login (`Api/Auth/PermissionAuthorization.cs`). Esta matriz refleja el seed de demostración
(`DemoDataSeeder.cs`) y es completamente editable desde la pantalla **`/roles`** (requiere el
permiso `users.manage`, típicamente solo Administrador): crear/eliminar roles, marcar/desmarcar
permisos por rol, crear cuentas de personal y asignarles roles. Los endpoints subyacentes son
`/api/v1/roles` y `/api/v1/users` (`RoleService`/`UserAdminService`).

| Permiso | Administrador | Finanzas | Supervisor | Operador | Auditor | Tutor |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| students.read / write / import | ✅ | — | — | — | — | — |
| guardians.read / write | ✅ | — | — | — | — | — |
| employees.read / write | ✅ | — | — | — | — | — |
| wallets.read | ✅ | ✅ | ✅ | — | ✅ | ✅ (solo propios) |
| wallets.adjust | ✅ | ✅ | — | — | — | — |
| recharges.create.presential | ✅ | ✅ | — | ✅ | — | — |
| recharges.create.digital | ✅ | — | — | — | — | ✅ (solo propios) |
| recharges.read | ✅ | ✅ | — | — | ✅ | — |
| rfid.manage | ✅ | — | — | — | — | — |
| rfid.manual_lookup | ✅ | — | ✅ | ✅ | — | ✅ |
| products.read | ✅ | ✅ | — | ✅ | — | — |
| products.write / prices.write | ✅ | ✅ (solo prices) | — | — | — | — |
| inventory.read | ✅ | — | ✅ | — | — | — |
| inventory.write / adjust | ✅ | — | — | — | — | — |
| pos.sell | ✅ | — | — | ✅ | — | — |
| pos.refund | ✅ | — | ✅ | — | — | — |
| pos.shift.manage | ✅ | — | ✅ | ✅ | — | — |
| reports.read / export | ✅ | ✅ | ✅ | — | ✅ | — |
| audit.read | ✅ | — | — | — | ✅ | — |
| settings.write | ✅ | — | — | — | — | — |
| users.manage | ✅ | — | — | — | — | — |

Notas:
- El **Auditor** no tiene ningún permiso de escritura en ninguna fila — el `AuditController` no
  expone verbos `POST/PUT/DELETE` en absoluto (no es una restricción de UI, es la ausencia del
  endpoint).
- El **Operador** solo puede vender/abrir-cerrar turno; ajustes de cartera e inventario requieren
  `wallets.adjust` / `inventory.adjust`, que no posee.
- El **Tutor** se autentica con una cuenta de usuario vinculada a `Guardian.Id`
  (`User.GuardianId`); el backend resuelve sus estudiantes exclusivamente por esa relación
  (`GuardiansController.GetMyStudents`), nunca por un id que el cliente pueda manipular.
- "Comprador" (estudiante/profesor/empleado) no es un rol de acceso administrativo — es el sujeto
  de la cartera. El autoservicio de estudiante (portal propio) usa el mismo mecanismo de
  `User.BuyerId` que el de tutor, listo para habilitarse.
