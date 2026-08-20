'use client';

import { useEffect, useRef, useState } from 'react';
import { apiFetch, ApiError } from '@/lib/api-client';
import { formatCurrency } from '@/lib/format';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Badge } from '@/components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { PagedResult, PointOfSaleDto, ProductDto, RfidLookupResult, SaleDto, ShiftDto } from '@/lib/types';

interface CartLine {
  productId: string;
  name: string;
  unitPrice: number;
  quantity: number;
}

export default function PosPage() {
  const [pointsOfSale, setPointsOfSale] = useState<PointOfSaleDto[]>([]);
  const [registerId, setRegisterId] = useState('');
  const [shift, setShift] = useState<ShiftDto | null>(null);
  const [openingFloat, setOpeningFloat] = useState('0');

  const [products, setProducts] = useState<ProductDto[]>([]);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [buyer, setBuyer] = useState<RfidLookupResult | null>(null);
  const [rfidInput, setRfidInput] = useState('');
  const [lastSale, setLastSale] = useState<SaleDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const rfidRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    apiFetch<PointOfSaleDto[]>('/api/v1/pos/points-of-sale').then(setPointsOfSale);
    apiFetch<PagedResult<ProductDto>>('/api/v1/catalog/products', { query: { page: 1, pageSize: 100 } }).then((r) =>
      setProducts(r.items.filter((p) => p.availableForSale && p.status === 'Active'))
    );
  }, []);

  useEffect(() => {
    rfidRef.current?.focus(); // keyboard-wedge mode: the reader "types" into this input
  }, [shift]);

  async function openShift() {
    setError(null);
    try {
      const opened = await apiFetch<ShiftDto>('/api/v1/pos/shifts/open', {
        method: 'POST',
        body: { registerId, openingFloat: Number(openingFloat) }
      });
      setShift(opened);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible abrir el turno.');
    }
  }

  async function closeShift() {
    if (!shift) return;
    const counted = window.prompt('Monto contado en caja al cierre:');
    if (counted === null) return;
    try {
      await apiFetch(`/api/v1/pos/shifts/close`, { method: 'POST', body: { shiftId: shift.id, closingCounted: Number(counted) } });
      setShift(null);
      setBuyer(null);
      setCart([]);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible cerrar el turno.');
    }
  }

  async function lookupRfid() {
    if (!rfidInput) return;
    setError(null);
    try {
      const result = await apiFetch<RfidLookupResult>('/api/v1/rfid/lookup', { query: { uid: rfidInput } });
      setBuyer(result);
      setRfidInput('');
    } catch (err) {
      setBuyer(null);
      setError('Credencial no reconocida. Verifique o busque manualmente con autorización.');
    }
  }

  function addToCart(product: ProductDto) {
    setCart((prev) => {
      const existing = prev.find((l) => l.productId === product.id);
      if (existing) return prev.map((l) => (l.productId === product.id ? { ...l, quantity: l.quantity + 1 } : l));
      return [...prev, { productId: product.id, name: product.name, unitPrice: product.basePrice, quantity: 1 }];
    });
  }

  function updateQuantity(productId: string, quantity: number) {
    if (quantity <= 0) setCart((prev) => prev.filter((l) => l.productId !== productId));
    else setCart((prev) => prev.map((l) => (l.productId === productId ? { ...l, quantity } : l)));
  }

  const total = cart.reduce((sum, l) => sum + l.unitPrice * l.quantity, 0);

  async function checkout() {
    if (!shift || !buyer || cart.length === 0) return;
    setError(null);
    try {
      const sale = await apiFetch<SaleDto>('/api/v1/pos/sales', {
        method: 'POST',
        body: {
          shiftId: shift.id,
          buyerId: buyer.buyerId,
          rfidMaskedValueUsed: buyer.rfidMaskedValue,
          lines: cart.map((l) => ({ productId: l.productId, quantity: l.quantity, discountAmount: null })),
          idempotencyKey: crypto.randomUUID()
        }
      });
      setLastSale(sale);
      setCart([]);
      setBuyer(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible completar la venta.');
    }
  }

  if (!shift) {
    return (
      <div className="max-w-md">
        <h1 className="mb-4 text-2xl font-semibold">Abrir turno de caja</h1>
        <Card>
          <CardBody className="flex flex-col gap-4">
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Caja</label>
              <select className="rounded-md border border-slate-300 px-3 py-2 text-sm dark:bg-slate-900 dark:border-slate-600"
                value={registerId} onChange={(e) => setRegisterId(e.target.value)}>
                <option value="">Seleccione…</option>
                {pointsOfSale.flatMap((p) => p.registers.map((r) => (
                  <option key={r.id} value={r.id}>{p.name} — {r.name}</option>
                )))}
              </select>
            </div>
            <Input label="Fondo inicial" type="number" step="0.01" value={openingFloat} onChange={(e) => setOpeningFloat(e.target.value)} />
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            <Button onClick={openShift} disabled={!registerId}>Abrir turno</Button>
          </CardBody>
        </Card>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
      <div className="lg:col-span-2">
        <div className="mb-4 flex items-center justify-between">
          <h1 className="text-2xl font-semibold">Punto de venta — {shift.registerName}</h1>
          <Button variant="secondary" onClick={closeShift}>Cerrar turno</Button>
        </div>

        <div className="mb-4">
          <Input
            ref={rfidRef}
            label="Lector RFID (o búsqueda manual autorizada)"
            placeholder="Pase la tarjeta o escriba el UID y presione Enter…"
            value={rfidInput}
            onChange={(e) => setRfidInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && lookupRfid()}
          />
        </div>

        {buyer && (
          <Card className="mb-4">
            <CardBody className="flex items-center justify-between">
              <div>
                <p className="font-semibold">{buyer.buyerName}</p>
                <p className="text-sm text-slate-500">Balance disponible: {formatCurrency(buyer.walletBalance)}</p>
              </div>
              {!buyer.allowedToPurchase && <Badge tone="danger">Cartera bloqueada</Badge>}
            </CardBody>
          </Card>
        )}

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4">
          {products.map((p) => (
            <button
              key={p.id}
              onClick={() => addToCart(p)}
              className="flex flex-col items-start rounded-lg border border-slate-200 bg-white p-4 text-left shadow-sm hover:border-brand-500 hover:shadow-md focus-visible:outline focus-visible:outline-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <span className="font-medium">{p.name}</span>
              <span className="mt-1 text-sm text-slate-500">{formatCurrency(p.basePrice)}</span>
            </button>
          ))}
        </div>
      </div>

      <div>
        <Card>
          <CardHeader><CardTitle>Carrito</CardTitle></CardHeader>
          <CardBody className="flex flex-col gap-3">
            {cart.length === 0 && <p className="text-sm text-slate-500">Sin productos.</p>}
            {cart.map((l) => (
              <div key={l.productId} className="flex items-center justify-between text-sm">
                <span>{l.name}</span>
                <div className="flex items-center gap-2">
                  <input
                    type="number"
                    min={0}
                    aria-label={`Cantidad de ${l.name}`}
                    value={l.quantity}
                    onChange={(e) => updateQuantity(l.productId, Number(e.target.value))}
                    className="w-16 rounded-md border border-slate-300 px-2 py-1 dark:bg-slate-900 dark:border-slate-600"
                  />
                  <span className="w-16 text-right">{formatCurrency(l.unitPrice * l.quantity)}</span>
                </div>
              </div>
            ))}
            <div className="mt-2 flex justify-between border-t border-slate-200 pt-2 font-semibold dark:border-slate-700">
              <span>Total</span><span>{formatCurrency(total)}</span>
            </div>
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            <Button size="lg" disabled={!buyer || cart.length === 0 || !buyer.allowedToPurchase} onClick={checkout}>
              Cobrar {formatCurrency(total)}
            </Button>
          </CardBody>
        </Card>

        {lastSale && (
          <Card className="mt-4">
            <CardHeader><CardTitle>Última venta</CardTitle></CardHeader>
            <CardBody>
              <p className="font-mono text-xs">{lastSale.saleNumber}</p>
              <p className="text-sm">Total: {formatCurrency(lastSale.total)}</p>
              <p className="text-sm">Balance restante: {formatCurrency(lastSale.balanceAfter)}</p>
            </CardBody>
          </Card>
        )}
      </div>
    </div>
  );
}
