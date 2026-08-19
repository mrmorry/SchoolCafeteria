'use client';

import { useState } from 'react';
import { apiDownload, apiFetch } from '@/lib/api-client';
import { formatCurrency, formatDateTime } from '@/lib/format';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { PagedResult } from '@/lib/types';

interface RechargeRow { occurredAtUtc: string; transactionNumber: string; buyerName: string; amount: number; channel: string; paymentMethod: string }
interface SalesRow { occurredAtUtc: string; saleNumber: string; buyerName: string; pointOfSale: string; total: number }

function lastNDays(n: number) {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - n);
  return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
}

export default function ReportsPage() {
  const [range] = useState(lastNDays(30));
  const [recharges, setRecharges] = useState<PagedResult<RechargeRow> | null>(null);
  const [sales, setSales] = useState<PagedResult<SalesRow> | null>(null);

  async function loadRecharges() {
    setRecharges(await apiFetch<PagedResult<RechargeRow>>('/api/v1/reports/recharges', { query: { ...range, page: 1, pageSize: 50 } }));
  }
  async function loadSales() {
    setSales(await apiFetch<PagedResult<SalesRow>>('/api/v1/reports/sales', { query: { ...range, page: 1, pageSize: 50 } }));
  }

  async function exportCsv(kind: 'recharges' | 'sales') {
    const blob = await apiDownload(`/api/v1/reports/${kind}/export`, { ...range, page: 1, pageSize: 10000 });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${kind}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">Reportes financieros</h1>
      <p className="text-sm text-slate-500">Últimos 30 días. Paginación del lado del servidor; use exportar para el detalle completo en CSV.</p>

      <Card>
        <CardHeader className="flex items-center justify-between">
          <CardTitle>Recargas</CardTitle>
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" onClick={loadRecharges}>Cargar</Button>
            <Button size="sm" variant="secondary" onClick={() => exportCsv('recharges')}>Exportar CSV</Button>
          </div>
        </CardHeader>
        <CardBody className="overflow-x-auto p-0">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
              <tr><th className="px-4 py-2">Fecha</th><th className="px-4 py-2">Comprador</th><th className="px-4 py-2">Monto</th><th className="px-4 py-2">Canal</th></tr>
            </thead>
            <tbody>
              {recharges?.items.map((r) => (
                <tr key={r.transactionNumber} className="border-t border-slate-100 dark:border-slate-800">
                  <td className="px-4 py-2">{formatDateTime(r.occurredAtUtc)}</td>
                  <td className="px-4 py-2">{r.buyerName}</td>
                  <td className="px-4 py-2">{formatCurrency(r.amount)}</td>
                  <td className="px-4 py-2">{r.channel}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>

      <Card>
        <CardHeader className="flex items-center justify-between">
          <CardTitle>Ventas</CardTitle>
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" onClick={loadSales}>Cargar</Button>
            <Button size="sm" variant="secondary" onClick={() => exportCsv('sales')}>Exportar CSV</Button>
          </div>
        </CardHeader>
        <CardBody className="overflow-x-auto p-0">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
              <tr><th className="px-4 py-2">Fecha</th><th className="px-4 py-2">Comprador</th><th className="px-4 py-2">Punto de venta</th><th className="px-4 py-2">Total</th></tr>
            </thead>
            <tbody>
              {sales?.items.map((s) => (
                <tr key={s.saleNumber} className="border-t border-slate-100 dark:border-slate-800">
                  <td className="px-4 py-2">{formatDateTime(s.occurredAtUtc)}</td>
                  <td className="px-4 py-2">{s.buyerName}</td>
                  <td className="px-4 py-2">{s.pointOfSale}</td>
                  <td className="px-4 py-2">{formatCurrency(s.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
