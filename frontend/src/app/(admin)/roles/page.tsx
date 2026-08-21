'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import { apiFetch, ApiError } from '@/lib/api-client';
import { formatDateTime } from '@/lib/format';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Badge } from '@/components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import type { PagedResult, PermissionDto, RoleDto, UserSummaryDto } from '@/lib/types';

export default function RolesPage() {
  const [permissions, setPermissions] = useState<PermissionDto[]>([]);
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [users, setUsers] = useState<UserSummaryDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function reloadRoles() {
    setRoles(await apiFetch<RoleDto[]>('/api/v1/roles'));
  }
  async function reloadUsers() {
    const result = await apiFetch<PagedResult<UserSummaryDto>>('/api/v1/users', { query: { page: 1, pageSize: 100 } });
    setUsers(result.items);
  }

  useEffect(() => {
    apiFetch<PermissionDto[]>('/api/v1/roles/permissions').then(setPermissions);
    reloadRoles().catch(() => setError('No fue posible cargar los roles.'));
    reloadUsers().catch(() => setError('No fue posible cargar los usuarios.'));
  }, []);

  const permissionsByModule = useMemo(() => {
    const groups = new Map<string, PermissionDto[]>();
    for (const p of permissions) {
      const list = groups.get(p.module) ?? [];
      list.push(p);
      groups.set(p.module, list);
    }
    return Array.from(groups.entries());
  }, [permissions]);

  function notify(fn: () => Promise<void>) {
    setError(null);
    setMessage(null);
    fn()
      .then(() => setMessage('Cambios guardados.'))
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Ocurrió un error.'));
  }

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="text-2xl font-semibold">Roles y permisos</h1>
        <p className="text-sm text-slate-500">
          Los permisos son datos, no nombres de rol codificados: cualquier cambio aquí toma efecto de inmediato
          para los usuarios con ese rol (en su próximo inicio de sesión o renovación de token).
        </p>
      </div>

      {message && <p className="text-sm text-green-700">{message}</p>}
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

      <NewRoleForm
        onCreated={() => {
          setMessage('Rol creado.');
          reloadRoles();
        }}
        onError={(msg) => setError(msg)}
      />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {roles.map((role) => (
          <RoleCard
            key={role.id}
            role={role}
            permissionsByModule={permissionsByModule}
            onSave={(keys) =>
              notify(async () => {
                await apiFetch(`/api/v1/roles/${role.id}/permissions`, { method: 'PUT', body: { permissionKeys: keys } });
                await reloadRoles();
              })
            }
            onDelete={() =>
              notify(async () => {
                await apiFetch(`/api/v1/roles/${role.id}`, { method: 'DELETE' });
                await reloadRoles();
              })
            }
          />
        ))}
      </div>

      <UsersSection roles={roles} users={users} onChanged={() => reloadUsers().catch(() => setError('No fue posible recargar usuarios.'))} onError={setError} />
    </div>
  );
}

function NewRoleForm({ onCreated, onError }: { onCreated: () => void; onError: (msg: string) => void }) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await apiFetch('/api/v1/roles', { method: 'POST', body: { name, description: description || null } });
      setName('');
      setDescription('');
      onCreated();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'No fue posible crear el rol.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>Nuevo rol</CardTitle></CardHeader>
      <CardBody>
        <form onSubmit={onSubmit} className="flex flex-wrap items-end gap-3" noValidate>
          <Input label="Nombre" required value={name} onChange={(e) => setName(e.target.value)} />
          <Input label="Descripción (opcional)" value={description} onChange={(e) => setDescription(e.target.value)} className="min-w-[16rem]" />
          <Button type="submit" disabled={isSubmitting || !name}>Crear rol</Button>
        </form>
      </CardBody>
    </Card>
  );
}

function RoleCard({
  role,
  permissionsByModule,
  onSave,
  onDelete
}: {
  role: RoleDto;
  permissionsByModule: [string, PermissionDto[]][];
  onSave: (keys: string[]) => void;
  onDelete: () => void;
}) {
  const [checked, setChecked] = useState<Set<string>>(new Set(role.permissions));
  const isDirty = useMemo(
    () => checked.size !== role.permissions.length || role.permissions.some((k) => !checked.has(k)),
    [checked, role.permissions]
  );

  useEffect(() => setChecked(new Set(role.permissions)), [role.permissions]);

  function toggle(key: string) {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <Card>
      <CardHeader className="flex items-center justify-between">
        <div>
          <CardTitle>{role.name}</CardTitle>
          {role.description && <p className="mt-1 text-xs text-slate-500">{role.description}</p>}
        </div>
        <div className="flex items-center gap-2">
          {role.isSystemRole && <Badge tone="info">Predefinido</Badge>}
          <Badge tone="neutral">{role.userCount} usuario{role.userCount === 1 ? '' : 's'}</Badge>
        </div>
      </CardHeader>
      <CardBody className="flex flex-col gap-4">
        <div className="max-h-64 overflow-y-auto pr-1">
          {permissionsByModule.map(([module, perms]) => (
            <fieldset key={module} className="mb-3">
              <legend className="text-xs font-semibold uppercase text-slate-500">{module}</legend>
              <div className="mt-1 grid grid-cols-1 gap-1 sm:grid-cols-2">
                {perms.map((p) => (
                  <label key={p.key} className="flex items-center gap-2 text-sm">
                    <input type="checkbox" checked={checked.has(p.key)} onChange={() => toggle(p.key)} />
                    <span title={p.description}>{p.key}</span>
                  </label>
                ))}
              </div>
            </fieldset>
          ))}
        </div>
        <div className="flex items-center justify-between border-t border-slate-200 pt-3 dark:border-slate-700">
          <Button
            variant="danger"
            size="sm"
            disabled={role.isSystemRole || role.userCount > 0}
            onClick={() => {
              if (window.confirm(`¿Eliminar el rol "${role.name}"?`)) onDelete();
            }}
          >
            Eliminar
          </Button>
          <Button size="sm" disabled={!isDirty} onClick={() => onSave(Array.from(checked))}>
            Guardar permisos
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}

function UsersSection({
  roles,
  users,
  onChanged,
  onError
}: {
  roles: RoleDto[];
  users: UserSummaryDto[];
  onChanged: () => void;
  onError: (msg: string) => void;
}) {
  const [form, setForm] = useState({ email: '', fullName: '', temporaryPassword: '', roleId: '' });
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function createUser(e: FormEvent) {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await apiFetch('/api/v1/users', {
        method: 'POST',
        body: { email: form.email, fullName: form.fullName, temporaryPassword: form.temporaryPassword, roleIds: form.roleId ? [form.roleId] : [] }
      });
      setForm({ email: '', fullName: '', temporaryPassword: '', roleId: '' });
      onChanged();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'No fue posible crear el usuario.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function assignRole(userId: string, roleId: string) {
    if (!roleId) return;
    try {
      await apiFetch('/api/v1/users/roles', { method: 'POST', body: { userId, roleId, pointOfSaleId: null } });
      onChanged();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'No fue posible asignar el rol.');
    }
  }

  async function toggleActive(user: UserSummaryDto) {
    try {
      await apiFetch(`/api/v1/users/${user.id}/active`, { method: 'PUT', body: { isActive: !user.isActive } });
      onChanged();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'No fue posible actualizar el estado del usuario.');
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-xl font-semibold">Usuarios de personal (staff)</h2>
      <p className="text-sm text-slate-500">
        Solo cuentas de personal interno (Administrador, Finanzas, Supervisor, Operador, Auditor). Los
        tutores y estudiantes no se crean aquí — se asocian automáticamente al dar de alta un estudiante.
      </p>

      <Card>
        <CardHeader><CardTitle>Nuevo usuario</CardTitle></CardHeader>
        <CardBody>
          <form onSubmit={createUser} className="grid grid-cols-1 gap-4 sm:grid-cols-5" noValidate>
            <Input label="Correo" type="email" required value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            <Input label="Nombre completo" required value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
            <Input
              label="Contraseña temporal"
              type="password"
              required
              minLength={12}
              value={form.temporaryPassword}
              onChange={(e) => setForm({ ...form, temporaryPassword: e.target.value })}
            />
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Rol inicial</label>
              <select
                className="rounded-md border border-slate-300 px-3 py-2 text-sm dark:bg-slate-900 dark:border-slate-600"
                value={form.roleId}
                onChange={(e) => setForm({ ...form, roleId: e.target.value })}
              >
                <option value="">Sin rol (asignar después)</option>
                {roles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
              </select>
            </div>
            <div className="flex items-end">
              <Button type="submit" disabled={isSubmitting} className="w-full">Crear usuario</Button>
            </div>
          </form>
        </CardBody>
      </Card>

      <div className="overflow-x-auto rounded-lg border border-slate-200 dark:border-slate-700">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
            <tr>
              <th className="px-4 py-3">Correo</th>
              <th className="px-4 py-3">Nombre</th>
              <th className="px-4 py-3">Roles</th>
              <th className="px-4 py-3">Estado</th>
              <th className="px-4 py-3">Entra ID</th>
              <th className="px-4 py-3">Último acceso</th>
              <th className="px-4 py-3">Agregar rol</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id} className="border-t border-slate-100 dark:border-slate-800">
                <td className="px-4 py-3">{u.email}</td>
                <td className="px-4 py-3">{u.fullName}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1">
                    {u.roles.map((r) => <Badge key={r.id} tone="neutral">{r.name}</Badge>)}
                    {u.roles.length === 0 && <span className="text-xs text-slate-400">Sin roles</span>}
                  </div>
                </td>
                <td className="px-4 py-3">
                  <button onClick={() => toggleActive(u)} className="inline-flex">
                    <Badge tone={u.isActive ? 'success' : 'danger'}>{u.isActive ? 'Activo' : 'Inactivo'}</Badge>
                  </button>
                </td>
                <td className="px-4 py-3">{u.hasEntraLink ? <Badge tone="info">Vinculado</Badge> : <span className="text-xs text-slate-400">—</span>}</td>
                <td className="px-4 py-3">{u.lastLoginAtUtc ? formatDateTime(u.lastLoginAtUtc) : '—'}</td>
                <td className="px-4 py-3">
                  <select
                    className="rounded-md border border-slate-300 px-2 py-1 text-xs dark:bg-slate-900 dark:border-slate-600"
                    defaultValue=""
                    onChange={(e) => {
                      assignRole(u.id, e.target.value);
                      e.target.value = '';
                    }}
                  >
                    <option value="" disabled>Seleccionar…</option>
                    {roles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                  </select>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
