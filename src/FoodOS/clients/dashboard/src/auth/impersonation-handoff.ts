import { tokenStore } from "@/auth/token-store";
import { decodeJwt, isTokenExpired } from "@/auth/jwt";

/**
 * Cross-app impersonation handoff. The admin app issues an impersonation
 * access token server-side, then opens the dashboard with the token in the
 * URL hash:
 *
 *   https://dashboard.example.com/#impersonate?token=<jwt>&tenant=<id>&expiresAt=<iso>
 *
 * We use the hash (not query) for two reasons:
 *   1. Browsers never send the fragment in HTTP requests, so the token can't
 *      leak via referrer headers or server access logs.
 *   2. SPA hash routes are already a thing — the bootstrap can scrub the
 *      hash before any router runs, without touching the path.
 *
 * Call this synchronously in main.tsx BEFORE createRoot so the token is
 * installed before AuthProvider's first render — otherwise ProtectedRoute
 * would see an anonymous session, redirect to /login, and the user would
 * have to sign in even though we have a valid impersonation token.
 */
export function installImpersonationFromHash(): void {
  if (typeof window === "undefined") return;
  const hash = window.location.hash;
  if (!hash.startsWith("#impersonate?")) return;

  const params = new URLSearchParams(hash.slice("#impersonate?".length));
  const token = params.get("token");
  const tenant = params.get("tenant");
  // Scrub even rejected links, before installing a session or rendering React.
  stripHash();
  const claims = decodeJwt(token);
  const expiresAt = params.get("expiresAt");
  if (!token || !tenant || tenant === "root" || claims?.tenant !== tenant
    || claims.business_actor === "operator"
    || typeof claims.sub !== "string" || !claims.sub.trim()
    || typeof claims.act_sub !== "string" || !claims.act_sub.trim()
    || claims.act_tenant !== "root"
    || typeof claims.exp !== "number" || !Number.isFinite(claims.exp)
    || isTokenExpired(claims)
    || (expiresAt !== null && (!Number.isFinite(Date.parse(expiresAt)) || Date.parse(expiresAt) <= Date.now()))) return;

  // These are shape/expiry checks, NOT signature verification; the API remains
  // the authentication authority. Invalid links leave the existing session alone.
  // A cross-app handoff must never stash an unrelated customer (or old root)
  // session as the actor. Ending this session signs out of the customer portal.
  tokenStore.clear();
  tokenStore.beginImpersonation(token, tenant);
}

function stripHash(): void {
  try {
    const cleaned = `${window.location.pathname}${window.location.search}`;
    window.history.replaceState(null, "", cleaned);
  } catch {
    // history API unavailable (file://, sandboxed iframe); fall back to a
    // plain location.hash assignment which adds a history entry but at
    // least removes the secret from the URL bar.
    window.location.hash = "";
  }
}
