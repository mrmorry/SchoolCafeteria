'use client';

// Staff-only Microsoft Entra ID login (see docs/06-runbook.md). Tutors and students never use
// this — they keep the local email/password flow. No real tenant is configured out of the box;
// these env vars are empty placeholders until an administrator connects one, and isEntraConfigured()
// is what the login screen checks before showing the "Iniciar sesión con Microsoft" button at all.
//
// @azure/msal-browser is loaded lazily (dynamic import inside loginWithEntraId) rather than at
// module scope, so pages that only ever check isEntraConfigured() — which every page behind
// AdminShell does, via auth-context — never pay for the ~60kB library in their bundle. Only the
// login screen, when the button is actually clicked, loads it.
const CLIENT_ID = process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID ?? '';
const TENANT_ID = process.env.NEXT_PUBLIC_ENTRA_TENANT_ID ?? '';

export function isEntraConfigured(): boolean {
  return CLIENT_ID.length > 0 && TENANT_ID.length > 0;
}

let msalInstancePromise: Promise<import('@azure/msal-browser').PublicClientApplication> | null = null;

async function getMsalInstance() {
  if (!msalInstancePromise) {
    msalInstancePromise = import('@azure/msal-browser').then(async ({ PublicClientApplication }) => {
      const instance = new PublicClientApplication({
        auth: {
          clientId: CLIENT_ID,
          authority: `https://login.microsoftonline.com/${TENANT_ID}`,
          redirectUri: typeof window !== 'undefined' ? window.location.origin : undefined
        },
        cache: {
          cacheLocation: 'sessionStorage'
        }
      });
      await instance.initialize();
      return instance;
    });
  }
  return msalInstancePromise;
}

/// Returns the Entra ID token to hand to POST /api/v1/auth/entra-login. The backend validates it
/// against Entra's own OIDC discovery document — this function never talks to our API directly.
export async function loginWithEntraId(): Promise<string> {
  const instance = await getMsalInstance();
  const result = await instance.loginPopup({ scopes: ['openid', 'profile', 'email'] });
  if (!result.idToken) throw new Error('Microsoft no devolvió un token de identidad.');
  return result.idToken;
}
