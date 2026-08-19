'use client';

import { useEffect, useState } from 'react';
import { apiFetch, ApiError } from '@/lib/api-client';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { InventoryBalanceDto } from '@/lib/types';

export default function InventoryPage() {
  const [balances, setBalances] = useState<InventoryBalanceDto[]>([]);
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function reload() {
    apiFetch<InventoryBalanceDto[]>('/api/v1/inventory/balances', { query: { lowStockOnly } })
      .then(setBalances)
      .catch(() => setError('No fue posible cargar el inventario.'));
  }
  useEffect(reload, [lowStockOnly]);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Inventario</h1>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={lowStockOnly} onChange={(e) => setLowStockOnly(e.target.checked)} />
          Solo existencias bajas
        </label>
      </div>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

      <Card>
        <CardHeader><CardTitle>Existencias por almacén</CardTitle></CardHeader>
        <CardBody className="overflow-x-auto p-0">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
              <tr>
                <th className="px-4 py-3">Producto</th><th className="px-4 py-3">Almacén</th>
                <th className="px-4 py-3">Existencia</th><th className="px-4 py-3">Mínimo</th><th className="px-4 py-3">Estado</th>
              </tr>
            </thead>
            <tbody>
              {balances.map((b) => (
                <tr key={`${b.warehouseId}-${b.productId}`} className="border-t border-slate-100 dark:border-slate-800">
                  <td className="px-4 py-3">{b.productCode} — {b.productName}</td>
                  <td className="px-4 py-3">{b.warehouseName}</td>
                  <td className="px-4 py-3">{b.quantityOnHand}</td>
                  <td className="px-4 py-3">{b.minStockLevel}</td>
                  <td className="px-4 py-3">
                    {b.isLow ? <Badge tone="danger">Bajo mínimo</Badge> : <Badge tone="success">Normal</Badge>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
