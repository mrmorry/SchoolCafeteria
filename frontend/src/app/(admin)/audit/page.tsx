'use client';

import { useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api-client';
import { formatDateTime } from '@/lib/format';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Input } from '@/components/ui/Input';
import type { AuditLogDto, PagedResult } from '@/lib/types';

// Read-only screen by design — there is no edit/delete action anywhere here (rule: un auditor no
// puede modificar información). Available to any role holding the "audit.read" permission.
export default function AuditPage() {
  const [entityName, setEntityName] = useState('');
  const [result, setResult] = useState<PagedResult<AuditLogDto> | null>(null);

  useEffect(() => {
    apiFetch<PagedResult<AuditLogDto>>('/api/v1/audit', { query: { entityName: entityName || undefined, page: 1, pageSize: 50 } }).then(setResult);
  }, [entityName]);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">Auditoría</h1>
      <Input label="Filtrar por entidad (ej. Wallet, Sale, RfidCredential)" value={entityName} onChange={(e) => setEntityName(e.target.value)} className="max-w-sm" />

      <Card>
        <CardHeader><CardTitle>Bitácora ({result?.totalCount ?? 0} eventos)</CardTitle></CardHeader>
        <CardBody className="overflow-x-auto p-0">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
              <tr>
                <th className="px-4 py-2">Fecha</th><th className="px-4 py-2">Usuario</th><th className="px-4 py-2">Acción</th>
                <th className="px-4 py-2">Entidad</th><th className="px-4 py-2">Id</th>
              </tr>
            </thead>
            <tbody>
              {result?.items.map((log) => (
                <tr key={log.id} className="border-t border-slate-100 dark:border-slate-800">
                  <td className="px-4 py-2">{formatDateTime(log.occurredAtUtc)}</td>
                  <td className="px-4 py-2">{log.userId ?? 'sistema'}</td>
                  <td className="px-4 py-2">{log.action}</td>
                  <td className="px-4 py-2">{log.entityName}</td>
                  <td className="px-4 py-2 font-mono text-xs">{log.entityId}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
