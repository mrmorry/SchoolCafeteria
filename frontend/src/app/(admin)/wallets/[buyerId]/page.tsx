'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { apiFetch, ApiError } from '@/lib/api-client';
import { formatCurrency, formatDateTime } from '@/lib/format';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Badge } from '@/components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { useAuth } from '@/lib/auth-context';
import type { PagedResult, WalletDto, WalletTransactionDto } from '@/lib/types';

const TX_TONE: Record<string, 'success' | 'danger' | 'warning' | 'neutral'> = {
  Recharge: 'success',
  Purchase: 'neutral',
  Refund: 'warning',
  AdjustmentPositive: 'success',
  AdjustmentNegative: 'danger',
  Reversal: 'warning'
};

export default function WalletDetailPage() {
  const params = useParams<{ buyerId: string }>();
  const { hasPermission } = useAuth();
  const [wallet, setWallet] = useState<WalletDto | null>(null);
  const [transactions, setTransactions] = useState<PagedResult<WalletTransactionDto> | null>(null);
  const [amount, setAmount] = useState('');
  const [rfidUid, setRfidUid] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function reload() {
    const w = await apiFetch<WalletDto>(`/api/v1/wallets/by-buyer/${params.buyerId}`);
    setWallet(w);
    const tx = await apiFetch<PagedResult<WalletTransactionDto>>(`/api/v1/wallets/${w.id}/transactions`, { query: { page: 1, pageSize: 25 } });
    setTransactions(tx);
  }

  useEffect(() => {
    reload().catch(() => setError('No fue posible cargar la cartera.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params.buyerId]);

  async function recharge() {
    if (!wallet) return;
    setError(null);
    setMessage(null);
    try {
      await apiFetch('/api/v1/recharges/presential', {
        method: 'POST',
        body: { walletId: wallet.id, amount: Number(amount), paymentMethod: 'Cash', idempotencyKey: crypto.randomUUID(), comment: 'Recarga desde backoffice' }
      });
      setAmount('');
      setMessage('Recarga registrada correctamente.');
      await reload();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible registrar la recarga.');
    }
  }

  async function issueRfid() {
    if (!wallet) return;
    setError(null);
    try {
      await apiFetch('/api/v1/rfid/issue', { method: 'POST', body: { buyerId: wallet.buyerId, rawUid: rfidUid } });
      setRfidUid('');
      setMessage('Credencial RFID emitida.');
      await reload();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible emitir la credencial.');
    }
  }

  if (!wallet) {
    return <p className="text-slate-500">{error ?? 'Cargando…'}</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">Cartera de {wallet.buyerName}</h1>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Card>
          <CardHeader><CardTitle>Balance disponible</CardTitle></CardHeader>
          <CardBody><p className="text-3xl font-bold">{formatCurrency(wallet.balance, wallet.currency)}</p></CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Estado</CardTitle></CardHeader>
          <CardBody><Badge tone={wallet.status === 'Active' ? 'success' : 'danger'}>{wallet.status}</Badge></CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Umbral de alerta</CardTitle></CardHeader>
          <CardBody>{wallet.lowBalanceThreshold ? formatCurrency(wallet.lowBalanceThreshold, wallet.currency) : 'No configurado'}</CardBody>
        </Card>
      </div>

      {message && <p className="text-sm text-green-700">{message}</p>}
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {hasPermission('recharges.create.presential') && (
          <Card>
            <CardHeader><CardTitle>Recarga presencial (efectivo)</CardTitle></CardHeader>
            <CardBody className="flex items-end gap-2">
              <Input label="Monto" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
              <Button onClick={recharge} disabled={!amount}>Recargar</Button>
            </CardBody>
          </Card>
        )}
        {hasPermission('rfid.manage') && (
          <Card>
            <CardHeader><CardTitle>Emitir credencial RFID</CardTitle></CardHeader>
            <CardBody className="flex items-end gap-2">
              <Input label="UID leído" value={rfidUid} onChange={(e) => setRfidUid(e.target.value)} placeholder="Pase la tarjeta por el lector…" />
              <Button onClick={issueRfid} disabled={!rfidUid}>Emitir</Button>
            </CardBody>
          </Card>
        )}
      </div>

      <Card>
        <CardHeader><CardTitle>Movimientos recientes</CardTitle></CardHeader>
        <CardBody className="overflow-x-auto p-0">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
              <tr>
                <th className="px-4 py-3">Fecha</th><th className="px-4 py-3">Transacción</th>
                <th className="px-4 py-3">Tipo</th><th className="px-4 py-3">Monto</th>
                <th className="px-4 py-3">Balance posterior</th>
              </tr>
            </thead>
            <tbody>
              {transactions?.items.map((t) => (
                <tr key={t.id} className="border-t border-slate-100 dark:border-slate-800">
                  <td className="px-4 py-3">{formatDateTime(t.occurredAtUtc)}</td>
                  <td className="px-4 py-3 font-mono text-xs">{t.transactionNumber}</td>
                  <td className="px-4 py-3"><Badge tone={TX_TONE[t.type] ?? 'neutral'}>{t.type}</Badge></td>
                  <td className="px-4 py-3">{formatCurrency(t.amount, wallet.currency)}</td>
                  <td className="px-4 py-3">{formatCurrency(t.balanceAfter, wallet.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
