'use client';

import { FormEvent, useState } from 'react';
import { apiFetch, ApiError } from '@/lib/api-client';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { GuardianDto } from '@/lib/types';

export default function GuardiansPage() {
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [created, setCreated] = useState<GuardianDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const guardian = await apiFetch<GuardianDto>('/api/v1/guardians', {
        method: 'POST',
        body: { fullName, email, phone: phone || null }
      });
      setCreated(guardian);
      setFullName('');
      setEmail('');
      setPhone('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible crear el tutor.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-semibold">Tutores</h1>
      <p className="text-sm text-slate-500">
        Los tutores se vinculan a estudiantes específicos desde el formulario de alta de estudiante,
        o mediante la vinculación manual a continuación. Un tutor solo puede ver a los estudiantes vinculados.
      </p>

      <Card>
        <CardHeader><CardTitle>Nuevo tutor</CardTitle></CardHeader>
        <CardBody>
          <form onSubmit={onSubmit} className="grid grid-cols-1 gap-4 sm:grid-cols-2" noValidate>
            <Input label="Nombre completo" required value={fullName} onChange={(e) => setFullName(e.target.value)} />
            <Input label="Correo electrónico" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
            <Input label="Teléfono (opcional)" value={phone} onChange={(e) => setPhone(e.target.value)} />
            {error && <p role="alert" className="sm:col-span-2 text-sm text-red-600">{error}</p>}
            <div className="sm:col-span-2 flex justify-end">
              <Button type="submit" disabled={isSubmitting}>Crear tutor</Button>
            </div>
          </form>
        </CardBody>
      </Card>

      {created && (
        <Card>
          <CardHeader><CardTitle>Tutor creado</CardTitle></CardHeader>
          <CardBody>
            <p className="text-sm">{created.fullName} — {created.email}</p>
            <p className="mt-1 text-xs text-slate-500">
              Vincúlelo a un estudiante desde POST /api/v1/guardians/link-student o desde la ficha del estudiante.
            </p>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
