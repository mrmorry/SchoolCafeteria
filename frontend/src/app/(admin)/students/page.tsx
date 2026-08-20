'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { apiFetch } from '@/lib/api-client';
import { formatCurrency } from '@/lib/format';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Badge } from '@/components/ui/Badge';
import type { PagedResult, StudentDto } from '@/lib/types';

const STATUS_TONE: Record<string, 'success' | 'neutral' | 'warning' | 'danger'> = {
  Active: 'success',
  Inactive: 'neutral',
  Suspended: 'warning',
  Graduated: 'neutral'
};

export default function StudentsPage() {
  const [result, setResult] = useState<PagedResult<StudentDto> | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    setIsLoading(true);
    apiFetch<PagedResult<StudentDto>>('/api/v1/students', { query: { search, page, pageSize: 20 } })
      .then(setResult)
      .finally(() => setIsLoading(false));
  }, [search, page]);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Estudiantes</h1>
        <div className="flex gap-2">
          <Link href="/students/import">
            <Button variant="secondary">Importar masivamente</Button>
          </Link>
          <Link href="/students/new">
            <Button>Nuevo estudiante</Button>
          </Link>
        </div>
      </div>

      <Input
        aria-label="Buscar estudiantes"
        placeholder="Buscar por código, nombre o apellido…"
        value={search}
        onChange={(e) => {
          setPage(1);
          setSearch(e.target.value);
        }}
        className="max-w-sm"
      />

      <div className="overflow-x-auto rounded-lg border border-slate-200 dark:border-slate-700">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-100 text-xs uppercase text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th scope="col" className="px-4 py-3">Código</th>
              <th scope="col" className="px-4 py-3">Nombre</th>
              <th scope="col" className="px-4 py-3">Nivel / Sección</th>
              <th scope="col" className="px-4 py-3">Estado</th>
              <th scope="col" className="px-4 py-3">RFID</th>
              <th scope="col" className="px-4 py-3">Balance</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={6} className="px-4 py-6 text-center text-slate-500">Cargando…</td></tr>
            )}
            {!isLoading && result?.items.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-6 text-center text-slate-500">Sin resultados.</td></tr>
            )}
            {result?.items.map((s) => (
              <tr key={s.id} className="border-t border-slate-100 hover:bg-slate-50 dark:border-slate-800 dark:hover:bg-slate-800/50">
                <td className="px-4 py-3">
                  <Link href={`/wallets/${s.buyerId}`} className="font-medium text-brand-700 hover:underline dark:text-brand-400">
                    {s.studentCode}
                  </Link>
                </td>
                <td className="px-4 py-3">{s.firstName} {s.lastName}</td>
                <td className="px-4 py-3">{s.schoolLevelName ?? '—'} {s.schoolSectionName ? `/ ${s.schoolSectionName}` : ''}</td>
                <td className="px-4 py-3"><Badge tone={STATUS_TONE[s.status] ?? 'neutral'}>{s.status}</Badge></td>
                <td className="px-4 py-3">{s.hasRfid ? <Badge tone="info">Asignado</Badge> : <Badge tone="neutral">Sin asignar</Badge>}</td>
                <td className="px-4 py-3">{formatCurrency(s.walletBalance)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {result && result.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm">
          <span>Página {result.page} de {result.totalPages} ({result.totalCount} estudiantes)</span>
          <div className="flex gap-2">
            <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
            <Button variant="secondary" size="sm" disabled={page >= result.totalPages} onClick={() => setPage((p) => p + 1)}>Siguiente</Button>
          </div>
        </div>
      )}
    </div>
  );
}
