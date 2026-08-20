'use client';

import { FormEvent, useEffect, useState } from 'react';
import Link from 'next/link';
import { apiFetch, ApiError } from '@/lib/api-client';
import { formatCurrency } from '@/lib/format';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { PagedResult, EmployeeDto } from '@/lib/types';

export default function EmployeesPage() {
  const [result, setResult] = useState<PagedResult<EmployeeDto> | null>(null);
  const [form, setForm] = useState({ employeeCode: '', fullName: '', email: '', employeeType: 'Teacher' });
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function reload() {
    apiFetch<PagedResult<EmployeeDto>>('/api/v1/employees', { query: { page: 1, pageSize: 50 } }).then(setResult);
  }

  useEffect(reload, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await apiFetch('/api/v1/employees', { method: 'POST', body: form });
      setForm({ employeeCode: '', fullName: '', email: '', employeeType: 'Teacher' });
      reload();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible crear el empleado.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">Empleados</h1>

      <Card>
        <CardHeader><CardTitle>Nuevo empleado o profesor</CardTitle></CardHeader>
        <CardBody>
          <form onSubmit={onSubmit} className="grid grid-cols-1 gap-4 sm:grid-cols-4" noValidate>
            <Input label="Código" required value={form.employeeCode} onChange={(e) => setForm({ ...form, employeeCode: e.target.value })} />
            <Input label="Nombre completo" required value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
            <Input label="Correo" type="email" required value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Tipo</label>
              <select
                className="rounded-md border border-slate-300 px-3 py-2 text-sm dark:bg-slate-900 dark:border-slate-600"
                value={form.employeeType}
                onChange={(e) => setForm({ ...form, employeeType: e.target.value })}
              >
                <option value="Teacher">Profesor</option>
                <option value="Administrative">Administrativo</option>
              </select>
            </div>
            {error && <p role="alert" className="sm:col-span-4 text-sm text-red-600">{error}</p>}
            <div className="sm:col-span-4 flex justify-end">
              <Button type="submit" disabled={isSubmitting}>Crear</Button>
            </div>
          </form>
        </CardBody>
      </Card>

      <div className="overflow-x-auto rounded-lg border border-slate-200 dark:border-slate-700">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
            <tr>
              <th className="px-4 py-3">Código</th><th className="px-4 py-3">Nombre</th>
              <th className="px-4 py-3">Tipo</th><th className="px-4 py-3">Balance</th>
            </tr>
          </thead>
          <tbody>
            {result?.items.map((e) => (
              <tr key={e.id} className="border-t border-slate-100 dark:border-slate-800">
                <td className="px-4 py-3">
                  <Link href={`/wallets/${e.buyerId}`} className="text-brand-700 hover:underline dark:text-brand-400">{e.employeeCode}</Link>
                </td>
                <td className="px-4 py-3">{e.fullName}</td>
                <td className="px-4 py-3">{e.employeeType}</td>
                <td className="px-4 py-3">{formatCurrency(e.walletBalance)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
