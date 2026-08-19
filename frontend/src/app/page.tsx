'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/auth-context';

export default function RootPage() {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (isLoading) return;
    if (!user) router.replace('/login');
    else if (user.roles.includes('Tutor')) router.replace('/portal');
    else router.replace('/dashboard');
  }, [user, isLoading, router]);

  return <div className="flex h-screen items-center justify-center text-slate-500">Cargando…</div>;
}
