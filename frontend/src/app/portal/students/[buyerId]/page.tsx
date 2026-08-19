'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { apiFetch, ApiError } from '@/lib/api-client';
import { formatCurrency, formatDateTime } from '@/lib/format';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Badge } from '@/components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { WalletDto, WalletTransactionDto } from '@/lib/types';

export default function PortalStudentDetailPage() {
  const params = useParams<{ buyerId: string }>();
  const [wallet, setWallet] = useState<WalletDto | null>(null);
  const [lastPurchases, setLastPurchases] = useState<WalletTransactionDto[]>([]);
  const [amount, setAmount] = useState('');
  const [threshold, setThreshold] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function reload() {
    const w = await apiFetch<WalletDto>(`/api/v1/wallets/by-buyer/${params.buyerId}`);
    setWallet(w);
    setThreshold(w.lowBalanceThreshold?.toString() ?? '');
    setLastPurchases(await apiFetch<WalletTransactionDto[]>(`/api/v1/wallets/${w.id}/last-purchases`));
  }

  useEffect(() => {
    reload().catch(() => setError('No fue posible cargar la información del estudiante.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params.buyerId]);

  async function requestDigitalRecharge() {
    if (!wallet) return;
    setError(null);
    setMessage(null);
    try {
      const result = await apiFetch<{ checkoutUrl: string }>('/api/v1/recharges/digital', {
        method: 'POST',
        body: { walletId: wallet.id, amount: Number(amount), idempotencyKey: crypto.randomUUID(), returnUrl: window.location.href }
      });
      setMessage('Recarga iniciada. Complete el pago en la pasarela (entorno sandbox de demostración).');
      window.open(result.checkoutUrl, '_blank', 'noopener');
      setAmount('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible iniciar la recarga.');
    }
  }

  async function saveThreshold() {
    if (!wallet) return;
    try {
      await apiFetch(`/api/v1/wallets/${wallet.id}/low-balance-threshold`, {
        method: 'PUT',
        body: { threshold: threshold ? Number(threshold) : null }
      });
      setMessage('Umbral de alerta actualizado.');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible actualizar el umbral.');
    }
  }

  if (!wallet) return <p className="text-slate-500">{error ?? 'Cargando…'}</p>;

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">{wallet.buyerName}</h1>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Balance disponible</CardTitle></CardHeader>
          <CardBody><p className="text-3xl font-bold">{formatCurrency(wallet.balance, wallet.currency)}</p></CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Estado de la cartera</CardTitle></CardHeader>
          <CardBody><Badge tone={wallet.status === 'Active' ? 'success' : 'danger'}>{wallet.status}</Badge></CardBody>
        </Card>
      </div>

      {message && <p className="text-sm text-green-700">{message}</p>}
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Recargar cartera</CardTitle></CardHeader>
          <CardBody className="flex items-end gap-2">
            <Input label="Monto" type="number" min="1" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
            <Button onClick={requestDigitalRecharge} disabled={!amount}>Recargar</Button>
          </CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Alerta de balance bajo</CardTitle></CardHeader>
          <CardBody className="flex items-end gap-2">
            <Input label="Umbral" type="number" min="0" step="0.01" value={threshold} onChange={(e) => setThreshold(e.target.value)} />
            <Button variant="secondary" onClick={saveThreshold}>Guardar</Button>
          </CardBody>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Últimas 5 compras</CardTitle></CardHeader>
        <CardBody className="overflow-x-auto p-0">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
              <tr><th className="px-4 py-2">Fecha</th><th className="px-4 py-2">Monto</th><th className="px-4 py-2">Balance posterior</th></tr>
            </thead>
            <tbody>
              {lastPurchases.map((t) => (
                <tr key={t.id} className="border-t border-slate-100 dark:border-slate-800">
                  <td className="px-4 py-2">{formatDateTime(t.occurredAtUtc)}</td>
                  <td className="px-4 py-2">{formatCurrency(t.amount, wallet.currency)}</td>
                  <td className="px-4 py-2">{formatCurrency(t.balanceAfter, wallet.currency)}</td>
                </tr>
              ))}
              {lastPurchases.length === 0 && (
                <tr><td colSpan={3} className="px-4 py-4 text-center text-slate-500">Sin compras registradas.</td></tr>
              )}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
