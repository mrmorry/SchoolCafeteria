'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { apiFetch } from '@/lib/api-client';
import { formatCurrency } from '@/lib/format';
import { Badge } from '@/components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { StudentDto } from '@/lib/types';

export default function PortalHomePage() {
  const [students, setStudents] = useState<StudentDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // "me" resolves strictly from the caller's own JWT — a guardian can never browse another
    // guardian's students by guessing an id (see GuardiansController.GetMyStudents).
    apiFetch<StudentDto[]>('/api/v1/guardians/me/students')
      .then(setStudents)
      .catch(() => setError('No fue posible cargar sus estudiantes.'));
  }, []);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold">Mis estudiantes</h1>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {students?.map((s) => (
          <Link key={s.id} href={`/portal/students/${s.buyerId}`}>
            <Card className="transition hover:border-brand-500 hover:shadow-md">
              <CardHeader className="flex items-center justify-between">
                <CardTitle>{s.firstName} {s.lastName}</CardTitle>
                <Badge tone={s.status === 'Active' ? 'success' : 'warning'}>{s.status}</Badge>
              </CardHeader>
              <CardBody>
                <p className="text-sm text-slate-500">{s.schoolLevelName} {s.schoolSectionName ? `/ ${s.schoolSectionName}` : ''}</p>
                <p className="mt-2 text-2xl font-bold">{formatCurrency(s.walletBalance)}</p>
              </CardBody>
            </Card>
          </Link>
        ))}
        {students?.length === 0 && <p className="text-slate-500">No hay estudiantes asociados a su cuenta.</p>}
      </div>
    </div>
  );
}
