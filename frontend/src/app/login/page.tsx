'use client';

import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/auth-context';
import { ApiError } from '@/lib/api-client';
import { isEntraConfigured } from '@/lib/msal';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';

export default function LoginPage() {
  const { login, loginWithEntra } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [mfaCode, setMfaCode] = useState('');
  const [needsMfa, setNeedsMfa] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isEntraSubmitting, setIsEntraSubmitting] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await login(email, password, mfaCode || undefined);
      router.push('/');
    } catch (err) {
      if (err instanceof ApiError && err.code === 'auth.mfa_required') {
        setNeedsMfa(true);
        setError('Ingrese el código de verificación de su aplicación de autenticación.');
      } else if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('No fue posible iniciar sesión. Intente nuevamente.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  async function onEntraLogin() {
    setError(null);
    setIsEntraSubmitting(true);
    try {
      await loginWithEntra();
      router.push('/');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible iniciar sesión con Microsoft.');
    } finally {
      setIsEntraSubmitting(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-100 px-4 dark:bg-slate-950">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Iniciar sesión — SchoolCafeteria</CardTitle>
        </CardHeader>
        <CardBody>
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            <Input
              label="Correo electrónico"
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
            <Input
              label="Contraseña"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            {needsMfa && (
              <Input
                label="Código MFA"
                inputMode="numeric"
                autoComplete="one-time-code"
                value={mfaCode}
                onChange={(e) => setMfaCode(e.target.value)}
              />
            )}
            {error && (
              <p role="alert" className="text-sm text-red-600">
                {error}
              </p>
            )}
            <Button type="submit" disabled={isSubmitting} className="w-full">
              {isSubmitting ? 'Ingresando…' : 'Ingresar'}
            </Button>

            {isEntraConfigured() && (
              <>
                <div className="flex items-center gap-3 text-xs text-slate-400">
                  <span className="h-px flex-1 bg-slate-200 dark:bg-slate-700" />
                  o, si eres personal del colegio
                  <span className="h-px flex-1 bg-slate-200 dark:bg-slate-700" />
                </div>
                <Button type="button" variant="secondary" disabled={isEntraSubmitting} className="w-full" onClick={onEntraLogin}>
                  {isEntraSubmitting ? 'Conectando…' : 'Iniciar sesión con Microsoft'}
                </Button>
              </>
            )}

            <p className="text-center text-xs text-slate-500">
              Datos de demostración sintéticos — ver README para las credenciales de prueba.
            </p>
          </form>
        </CardBody>
      </Card>
    </main>
  );
}
