'use client';

import { useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api-client';
import { formatCurrency } from '@/lib/format';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { DashboardSummaryDto } from '@/lib/types';

export default function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<DashboardSummaryDto>('/api/v1/reports/dashboard')
      .then(setSummary)
      .catch(() => setError('No fue posible cargar el panel.'));
  }, []);

  const tiles = summary
    ? [
        { label: 'Ventas de hoy', value: formatCurrency(summary.todaySales) },
        { label: 'Recargas de hoy', value: formatCurrency(summary.todayRecharges) },
        { label: 'Transacciones de hoy', value: summary.todayTransactions.toString() },
        { label: 'Productos con inventario bajo', value: summary.lowStockProducts.toString() },
        { label: 'Carteras activas', value: summary.activeWallets.toString() },
        { label: 'Balance total en carteras', value: formatCurrency(summary.totalWalletBalance) }
      ]
    : [];

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">Panel administrativo</h1>
      <p className="text-sm text-slate-500">
        Indicadores del día. Para el detalle completo con filtros y exportación, use la sección de Reportes.
      </p>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {tiles.map((tile) => (
          <Card key={tile.label}>
            <CardHeader>
              <CardTitle>{tile.label}</CardTitle>
            </CardHeader>
            <CardBody>
              <p className="text-2xl font-bold">{tile.value}</p>
            </CardBody>
          </Card>
        ))}
      </div>
    </div>
  );
}
