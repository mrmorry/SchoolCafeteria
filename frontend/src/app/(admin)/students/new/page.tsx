'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { apiFetch, ApiError } from '@/lib/api-client';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';

const schema = z.object({
  studentCode: z.string().min(1, 'El código es obligatorio.'),
  firstName: z.string().min(1, 'El nombre es obligatorio.'),
  lastName: z.string().min(1, 'El apellido es obligatorio.'),
  studentEmail: z.string().email('Correo inválido.').optional().or(z.literal('')),
  guardianFullName: z.string().min(1, 'El nombre del tutor es obligatorio.'),
  guardianEmail: z.string().email('Correo del tutor inválido.'),
  guardianPhone: z.string().optional()
});

type FormValues = z.infer<typeof schema>;

export default function NewStudentPage() {
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting }
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    setServerError(null);
    try {
      const student = await apiFetch<{ id: string }>('/api/v1/students', {
        method: 'POST',
        body: {
          studentCode: values.studentCode,
          firstName: values.firstName,
          lastName: values.lastName,
          studentEmail: values.studentEmail || null,
          guardianFullName: values.guardianFullName,
          guardianEmail: values.guardianEmail,
          guardianPhone: values.guardianPhone || null
        }
      });
      router.push(`/students`);
      void student;
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'No fue posible crear el estudiante.');
    }
  }

  return (
    <div className="max-w-2xl">
      <h1 className="mb-4 text-2xl font-semibold">Nuevo estudiante</h1>
      <Card>
        <CardHeader>
          <CardTitle>Datos del estudiante y tutor responsable</CardTitle>
        </CardHeader>
        <CardBody>
          <form onSubmit={handleSubmit(onSubmit)} className="grid grid-cols-1 gap-4 sm:grid-cols-2" noValidate>
            <Input label="Código de estudiante" {...register('studentCode')} error={errors.studentCode?.message} />
            <Input label="Correo del estudiante (opcional)" {...register('studentEmail')} error={errors.studentEmail?.message} />
            <Input label="Nombre" {...register('firstName')} error={errors.firstName?.message} />
            <Input label="Apellido" {...register('lastName')} error={errors.lastName?.message} />
            <Input label="Nombre completo del tutor" {...register('guardianFullName')} error={errors.guardianFullName?.message} />
            <Input label="Correo del tutor" {...register('guardianEmail')} error={errors.guardianEmail?.message} />
            <Input label="Teléfono del tutor (opcional)" {...register('guardianPhone')} error={errors.guardianPhone?.message} />

            {serverError && <p role="alert" className="sm:col-span-2 text-sm text-red-600">{serverError}</p>}

            <div className="sm:col-span-2 flex justify-end gap-2">
              <Button type="button" variant="secondary" onClick={() => router.back()}>Cancelar</Button>
              <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Guardando…' : 'Crear estudiante'}</Button>
            </div>
          </form>
        </CardBody>
      </Card>
    </div>
  );
}
