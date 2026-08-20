'use client';

import { useEffect } from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import clsx from 'clsx';
import { useAuth } from '@/lib/auth-context';
import { Button } from '@/components/ui/Button';

const NAV = [
  { href: '/dashboard', label: 'Panel', permission: 'reports.read' },
  { href: '/students', label: 'Estudiantes', permission: 'students.read' },
  { href: '/guardians', label: 'Tutores', permission: 'guardians.read' },
  { href: '/employees', label: 'Empleados', permission: 'employees.read' },
  { href: '/pos', label: 'Punto de venta', permission: 'pos.sell' },
  { href: '/products', label: 'Productos y precios', permission: 'products.read' },
  { href: '/inventory', label: 'Inventario', permission: 'inventory.read' },
  { href: '/reports', label: 'Reportes', permission: 'reports.read' },
  { href: '/audit', label: 'Auditoría', permission: 'audit.read' },
  { href: '/settings', label: 'Configuración', permission: 'settings.write' }
];

export function AdminShell({ children }: { children: React.ReactNode }) {
  const { user, isLoading, hasPermission, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!isLoading && !user) router.replace('/login');
  }, [isLoading, user, router]);

  if (isLoading || !user) {
    return <div className="flex h-screen items-center justify-center text-slate-500">Cargando…</div>;
  }

  const visibleNav = NAV.filter((item) => hasPermission(item.permission));

  return (
    <div className="flex min-h-screen">
      <a href="#main-content" className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-white focus:p-2">
        Saltar al contenido principal
      </a>
      <nav aria-label="Navegación principal" className="hidden w-64 flex-col border-r border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900 md:flex">
        <div className="mb-6 px-2 text-lg font-bold text-brand-700 dark:text-brand-500">SchoolCafeteria</div>
        <ul className="flex flex-col gap-1">
          {visibleNav.map((item) => (
            <li key={item.href}>
              <Link
                href={item.href}
                className={clsx(
                  'block rounded-md px-3 py-2 text-sm font-medium',
                  pathname?.startsWith(item.href)
                    ? 'bg-brand-50 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300'
                    : 'text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800'
                )}
              >
                {item.label}
              </Link>
            </li>
          ))}
        </ul>
        <div className="mt-auto border-t border-slate-200 pt-4 dark:border-slate-700">
          <p className="px-2 text-sm font-medium">{user.fullName}</p>
          <p className="px-2 text-xs text-slate-500">{user.roles.join(', ')}</p>
          <Button variant="ghost" size="sm" className="mt-2 w-full justify-start" onClick={logout}>
            Cerrar sesión
          </Button>
        </div>
      </nav>
      <main id="main-content" className="flex-1 overflow-y-auto p-6">
        {children}
      </main>
    </div>
  );
}
