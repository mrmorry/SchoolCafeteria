'use client';

import { FormEvent, useEffect, useState } from 'react';
import { apiFetch, ApiError } from '@/lib/api-client';
import { formatCurrency } from '@/lib/format';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { PagedResult, ProductCategoryDto, ProductDto } from '@/lib/types';

export default function ProductsPage() {
  const [categories, setCategories] = useState<ProductCategoryDto[]>([]);
  const [products, setProducts] = useState<PagedResult<ProductDto> | null>(null);
  const [form, setForm] = useState({ code: '', name: '', categoryId: '', basePrice: '', cost: '', taxRate: '0.07' });
  const [error, setError] = useState<string | null>(null);

  function reload() {
    apiFetch<ProductCategoryDto[]>('/api/v1/catalog/categories').then(setCategories);
    apiFetch<PagedResult<ProductDto>>('/api/v1/catalog/products', { query: { page: 1, pageSize: 50 } }).then(setProducts);
  }
  useEffect(reload, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await apiFetch('/api/v1/catalog/products', {
        method: 'POST',
        body: {
          code: form.code, name: form.name, categoryId: form.categoryId, unitOfMeasure: 'Unit',
          cost: Number(form.cost || 0), basePrice: Number(form.basePrice), taxRate: Number(form.taxRate),
          trackInventory: true, minStockLevel: 5, reorderLevel: 10
        }
      });
      setForm({ code: '', name: '', categoryId: '', basePrice: '', cost: '', taxRate: '0.07' });
      reload();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible crear el producto.');
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">Productos y precios</h1>

      <Card>
        <CardHeader><CardTitle>Nuevo producto</CardTitle></CardHeader>
        <CardBody>
          <form onSubmit={onSubmit} className="grid grid-cols-2 gap-4 sm:grid-cols-6" noValidate>
            <Input label="Código" required value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
            <Input label="Nombre" required className="sm:col-span-2" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Categoría</label>
              <select className="rounded-md border border-slate-300 px-3 py-2 text-sm dark:bg-slate-900 dark:border-slate-600" required
                value={form.categoryId} onChange={(e) => setForm({ ...form, categoryId: e.target.value })}>
                <option value="">Seleccione…</option>
                {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <Input label="Costo" type="number" step="0.01" value={form.cost} onChange={(e) => setForm({ ...form, cost: e.target.value })} />
            <Input label="Precio" type="number" step="0.01" required value={form.basePrice} onChange={(e) => setForm({ ...form, basePrice: e.target.value })} />
            {error && <p role="alert" className="sm:col-span-6 text-sm text-red-600">{error}</p>}
            <div className="sm:col-span-6 flex justify-end">
              <Button type="submit">Crear producto</Button>
            </div>
          </form>
        </CardBody>
      </Card>

      <div className="overflow-x-auto rounded-lg border border-slate-200 dark:border-slate-700">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
            <tr>
              <th className="px-4 py-3">Código</th><th className="px-4 py-3">Nombre</th><th className="px-4 py-3">Categoría</th>
              <th className="px-4 py-3">Precio</th><th className="px-4 py-3">Estado</th><th className="px-4 py-3">Stock</th>
            </tr>
          </thead>
          <tbody>
            {products?.items.map((p) => (
              <tr key={p.id} className="border-t border-slate-100 dark:border-slate-800">
                <td className="px-4 py-3">{p.code}</td>
                <td className="px-4 py-3">{p.name}</td>
                <td className="px-4 py-3">{p.categoryName}</td>
                <td className="px-4 py-3">{formatCurrency(p.basePrice)}</td>
                <td className="px-4 py-3"><Badge tone={p.status === 'Active' ? 'success' : 'neutral'}>{p.status}</Badge></td>
                <td className="px-4 py-3">
                  {p.stockOnHand != null ? (
                    <Badge tone={p.stockOnHand <= p.minStockLevel ? 'danger' : 'neutral'}>{p.stockOnHand}</Badge>
                  ) : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
