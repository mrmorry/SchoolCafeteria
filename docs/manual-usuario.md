# Manual de usuario

## Portal del tutor (`/portal`)

1. Inicie sesión con su correo y contraseña.
2. La pantalla principal muestra una tarjeta por cada estudiante asociado a su cuenta, con el
   balance actual.
3. Al entrar al detalle de un estudiante puede:
   - Ver el balance disponible y el estado de la cartera.
   - **Recargar**: indique un monto y será dirigido a la pasarela de pago (entorno de
     demostración/sandbox en este build).
   - **Configurar alerta de balance bajo**: defina el umbral; recibirá un correo cuando el saldo
     cruce por debajo de ese valor (no se envían correos repetidos en cada consulta).
   - Consultar las **últimas 5 compras** de ese estudiante.
4. Solo verá los estudiantes explícitamente vinculados a su cuenta — el sistema lo garantiza a
   nivel de servidor, no solo ocultando la información en pantalla.

## Punto de venta (`/pos`, requiere rol Operador o superior)

1. Al ingresar, seleccione la caja y registre el fondo inicial para **abrir turno**.
2. Pase la tarjeta/brazalete RFID del comprador por el lector (o escriba el identificador y
   presione Enter) en el campo correspondiente.
3. Verifique el nombre y balance mostrado.
4. Toque los productos para agregarlos al carrito; ajuste cantidades si es necesario.
5. Presione **Cobrar**. El sistema valida el saldo, descuenta la cartera y el inventario de forma
   atómica — si algo falla, no queda ninguna operación parcial.
6. Al finalizar el turno, presione **Cerrar turno** e ingrese el monto contado en caja; el sistema
   calculará la diferencia contra lo esperado.

## Backoffice administrativo (`/dashboard` y siguientes, según permisos)

- **Estudiantes**: alta manual, edición, e importación masiva desde CSV con vista previa y
  validación antes de confirmar.
- **Tutores**: alta y vinculación a estudiantes con permisos configurables por vínculo.
- **Cartera de un comprador** (desde la ficha de estudiante/empleado): balance, historial completo
  de movimientos, recarga presencial, emisión de credencial RFID.
- **Productos**: alta de productos y categorías, precios.
- **Inventario**: existencias por almacén, alertas de nivel mínimo.
- **Reportes**: recargas y ventas filtrables por fecha, exportables a CSV.
- **Auditoría** (rol Auditor u otro con `audit.read`): consulta de solo lectura de la bitácora de
  cambios del sistema.

## Accesibilidad

- Toda la interfaz es navegable por teclado (`Tab`/`Enter`/flechas en controles nativos) y expone
  un enlace "Saltar al contenido principal" al inicio de cada pantalla administrativa.
- Los estados (activo/bloqueado/bajo mínimo, etc.) siempre se comunican con texto o ícono, nunca
  solo con color.
- Los formularios muestran mensajes de error específicos junto a cada campo, anunciados a
  lectores de pantalla mediante `aria-describedby`/`role="alert"`.
