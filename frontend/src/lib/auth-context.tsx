'use client';

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiFetch, setAccessToken, setRefreshToken, setUnauthorizedHandler } from './api-client';
import type { LoginResult, UserProfile } from './types';

interface AuthContextValue {
  user: UserProfile | null;
  isLoading: boolean;
  login: (email: string, password: string, mfaCode?: string) => Promise<void>;
  logout: () => void;
  hasPermission: (key: string) => boolean;
  hasRole: (role: string) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

const STORAGE_KEY = 'sc_user_profile';

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();

  const logout = useCallback(() => {
    setAccessToken(null);
    setRefreshToken(null);
    if (typeof window !== 'undefined') window.localStorage.removeItem(STORAGE_KEY);
    setUser(null);
    router.push('/login');
  }, [router]);

  useEffect(() => {
    setUnauthorizedHandler(logout);
    // Best-effort session restore: we keep the profile (non-sensitive) in localStorage but the
    // access token itself only in memory, so a refresh always re-authenticates via refresh token
    // on the first API call (see api-client's 401 retry path).
    const stored = typeof window !== 'undefined' ? window.localStorage.getItem(STORAGE_KEY) : null;
    if (stored) setUser(JSON.parse(stored));
    setIsLoading(false);
    return () => setUnauthorizedHandler(null);
  }, [logout]);

  const login = useCallback(async (email: string, password: string, mfaCode?: string) => {
    const result = await apiFetch<LoginResult>('/api/v1/auth/login', {
      method: 'POST',
      body: { email, password, mfaCode: mfaCode || null }
    });
    setAccessToken(result.accessToken);
    setRefreshToken(result.refreshToken);
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(result.user));
    setUser(result.user);
  }, []);

  const hasPermission = useCallback((key: string) => user?.permissions.includes(key) ?? false, [user]);
  const hasRole = useCallback((role: string) => user?.roles.includes(role) ?? false, [user]);

  const value = useMemo(
    () => ({ user, isLoading, login, logout, hasPermission, hasRole }),
    [user, isLoading, login, logout, hasPermission, hasRole]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>');
  return ctx;
}
