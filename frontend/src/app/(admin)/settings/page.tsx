'use client';

import { useEffect, useState } from 'react';
import { apiFetch, ApiError } from '@/lib/api-client';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';

interface SystemSetting { key: string; value: string; valueType: string; description?: string }

export default function SettingsPage() {
  const [settings, setSettings] = useState<SystemSetting[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [savingKey, setSavingKey] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<SystemSetting[]>('/api/v1/settings').then(setSettings).catch(() => setError('No fue posible cargar la configuración.'));
  }, []);

  async function save(setting: SystemSetting, newValue: string) {
    setSavingKey(setting.key);
    try {
      await apiFetch(`/api/v1/settings/${setting.key}`, {
        method: 'PUT',
        body: { value: newValue, valueType: setting.valueType, description: setting.description }
      });
      setSettings((prev) => prev.map((s) => (s.key === setting.key ? { ...s, value: newValue } : s)));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible guardar el cambio.');
    } finally {
      setSavingKey(null);
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-semibold">Configuración del colegio</h1>
      <p className="text-sm text-slate-500">
        Moneda, políticas de saldo e inventario. Ningún valor está fijo en el código: todo se lee de esta tabla.
      </p>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

      <div className="flex flex-col gap-3">
        {settings.map((s) => (
          <Card key={s.key}>
            <CardHeader><CardTitle className="font-mono text-xs">{s.key}</CardTitle></CardHeader>
            <CardBody className="flex items-end gap-2">
              <Input
                label={s.description ?? 'Valor'}
                defaultValue={s.value}
                onBlur={(e) => e.target.value !== s.value && save(s, e.target.value)}
              />
              {savingKey === s.key && <span className="text-xs text-slate-500">Guardando…</span>}
            </CardBody>
          </Card>
        ))}
      </div>
    </div>
  );
}
