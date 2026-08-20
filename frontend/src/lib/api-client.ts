'use client';

import type { LoginResult, ProblemDetails } from './types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5000';

export class ApiError extends Error {
  status: number;
  code?: string;
  constructor(problem: ProblemDetails, status: number) {
    super(problem.detail ?? problem.title ?? 'Error desconocido');
    this.status = status;
    this.code = problem.code;
  }
}

// Access token is kept in memory only (never localStorage) to reduce XSS exposure; the refresh
// token is persisted so a page reload doesn't force a re-login — see docs/01-analisis.md /
// docs/06-runbook.md for the production hardening recommendation (httpOnly refresh cookie).
let accessToken: string | null = null;
let onUnauthorized: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

function getRefreshToken(): string | null {
  if (typeof window === 'undefined') return null;
  return window.localStorage.getItem('sc_refresh_token');
}

export function setRefreshToken(token: string | null) {
  if (typeof window === 'undefined') return;
  if (token) window.localStorage.setItem('sc_refresh_token', token);
  else window.localStorage.removeItem('sc_refresh_token');
}

async function refreshAccessToken(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return false;

  const response = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken })
  });
  if (!response.ok) return false;

  const result: LoginResult = await response.json();
  setAccessToken(result.accessToken);
  setRefreshToken(result.refreshToken);
  return true;
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined>;
  isRetry?: boolean;
}

function buildUrl(path: string, query?: RequestOptions['query']) {
  const url = new URL(`${API_BASE_URL}${path}`);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, String(value));
    }
  }
  return url.toString();
}

export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;

  const response = await fetch(buildUrl(path, options.query), {
    method: options.method ?? 'GET',
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined
  });

  if (response.status === 401 && !options.isRetry) {
    const refreshed = await refreshAccessToken();
    if (refreshed) return apiFetch<T>(path, { ...options, isRetry: true });
    onUnauthorized?.();
    throw new ApiError({ title: 'Sesión expirada', detail: 'Inicie sesión nuevamente.' }, 401);
  }

  if (!response.ok) {
    let problem: ProblemDetails = {};
    try {
      problem = await response.json();
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError(problem, response.status);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export async function apiDownload(path: string, query?: RequestOptions['query']): Promise<Blob> {
  const headers: Record<string, string> = {};
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  const response = await fetch(buildUrl(path, query), { headers });
  if (!response.ok) throw new ApiError({ title: 'Error al descargar el archivo' }, response.status);
  return response.blob();
}

export { API_BASE_URL };
