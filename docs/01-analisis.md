# Fase 1 — Análisis funcional

## 1. Resumen funcional

SchoolCafeteria es una plataforma para administrar compras, recargas, inventario y operación
de la cafetería de un colegio, con carteras digitales por comprador (estudiante, profesor,
empleado), identificación por RFID, punto de venta (POS), portal de padres/tutores, reportes
financieros/operativos y auditoría completa. La v1 opera para **un colegio** pero el modelo de
datos incluye `SchoolId` en toda entidad que requiera aislamiento, de modo que soportar múltiples
colegios en el futuro sea un cambio de alcance (habilitar filtro multi-tenant), no de diseño.

## 2. Actores

- **Comprador**: Estudiante, Profesor, Empleado administrativo.
- **Padre/Tutor**: uno o varios estudiantes asociados, tutores secundarios con permisos limitados.
- **Operador POS**: cajero en punto de venta.
- **Supervisor**: autoriza anulaciones/ajustes, revisa cajas.
- **Finanzas**: precios, reportes, conciliación, recargas de oficina.
- **Administrador**: configuración, usuarios, roles, integraciones.
- **Auditor**: solo lectura sobre todo el sistema.
- **Sistema externo**: integra estudiantes vía API/CSV/sincronización.

## 3. Casos de uso (MVP)

1. Alta manual e importación masiva (CSV) de estudiantes.
2. Creación automática de cartera digital al crear comprador.
3. Asociación de credencial RFID a comprador.
4. Recarga presencial (oficina de cobros / POS) y recarga digital (pasarela sandbox).
5. Notificación por correo tras recarga y tras compra.
6. Venta en POS con lectura RFID (modo teclado), descuento atómico de cartera y de inventario.
7. Consulta de tutor: balances y últimas 5 compras por estudiante, configuración de alerta.
8. Reportes de finanzas: recargas y ventas, exportables a CSV.
9. Alertas de inventario bajo.
10. Control de acceso por rol/permiso.
11. Auditoría de solo lectura para el rol Auditor.
12. Ejecución local con Docker Compose; imagen lista para Azure (App Service contenedor / ACR).

## 4. Supuestos técnicos y funcionales

Estos supuestos se adoptan como valores por defecto configurables (nunca hardcodeados) para poder
avanzar sin bloquear la construcción; todos son reemplazables sin cambiar el dominio:

| Decisión abierta | Supuesto adoptado en v1 |
|---|---|
| Proveedor de pagos | `IPaymentGateway` con implementación `SandboxPaymentGateway` (simula orden pendiente → webhook firmado HMAC). Listo para reemplazar por Stripe/Azure/adquirente local. |
| Lector RFID | Modo teclado (keyboard-wedge): el POS trata el foco de un input como lectura. Contrato `IRfidReaderProvider` permite añadir WebUSB/WebSerial/agente local después. |
| Proveedor de correo | `IEmailSender` con implementación SMTP (Mailhog en local) y adaptador de Azure Communication Services documentado, no implementado en v1. |
| Software escolar externo | Adaptador genérico `IStudentSourceAdapter` + endpoint API con API-Key para recepción; sin integración real a un SIS específico. |
| Moneda e impuestos | Moneda y tasa de impuesto por defecto configurables en `SystemSetting` (tabla clave/valor por colegio); no se fija ningún símbolo en código. |
| Política de devoluciones | Requiere autorización de Supervisor y motivo obligatorio; genera movimiento compensatorio, nunca borra el original. |
| Restricciones alimentarias | Campo de texto libre/etiquetas en `Product.Allergens`; sin motor de reglas en v1. |

## 5. Riesgos

- Concurrencia sobre el balance de cartera bajo alta carga en hora pico (mitigado con
  transacciones + `RowVersion` optimista + validación de balance dentro de la misma transacción).
- Doble envío de recargas/ventas por reintentos de red (mitigado con `IdempotencyKey`).
- Dependencia de un proveedor de pago no definido: se aísla completamente detrás de una interfaz.
- Hardware RFID variado: se evita acoplar el backend a un fabricante.
- Alcance muy amplio del pedido original: se prioriza un **MVP realmente funcional y auditable**
  sobre cobertura superficial de todas las pantallas.

## 6. Preguntas abiertas (requieren decisión de negocio antes de producción)

1. ¿Qué pasarela de pago se contratará (Stripe, adquirente local, PSP regional)?
2. ¿Modelo exacto de lector/tarjeta RFID (ISO14443, EM4100, marca de POS)?
3. ¿Proveedor de correo/SMS definitivo (ACS, SendGrid, proveedor local)?
4. ¿Cuál es el SIS/sistema escolar que entregará estudiantes, y su formato/API?
5. ¿Moneda base e IVA/impuesto aplicable, y si varía por producto?
6. ¿Política formal de devoluciones/anulaciones (ventanas de tiempo, montos máximos)?
7. ¿Catálogo de alérgenos/restricciones alimentarias formal?

## 7. Alcance del MVP (implementado en esta entrega)

Incluye: estudiantes/tutores/empleados, cartera + libro mayor de movimientos, RFID, recarga
presencial y digital (sandbox), POS con carrito/checkout, inventario básico (almacén único o
múltiple, movimientos, alertas de stock), notificaciones (outbox + SMTP mock), portal del tutor
(balances, últimas 5 compras, alerta de balance bajo), reportes de recargas/ventas exportables,
auditoría automática de operaciones sensibles, roles y permisos configurables, autenticación
JWT con MFA opcional (TOTP), Docker Compose, Dockerfiles, CI, IaC Bicep para Azure.

## 8. Fuera de alcance del MVP (post-MVP, contratos ya definidos)

SignalR en tiempo real, PWA offline, WebUSB/WebSerial para RFID, Service Bus real (se usa una
cola en base de datos como *outbox* en v1), Azure Blob real (adaptador de almacenamiento con
implementación local de archivo en v1), programación de reportes, SMS/WhatsApp, multi-colegio
activo (el aislamiento por `SchoolId` ya existe pero la UI asume un solo colegio), lotes/vencimiento
de inventario, listas de precio múltiples con vigencia programada avanzada.
