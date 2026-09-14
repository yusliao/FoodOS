export const SUPPORTED_CULTURES = ["en-US", "zh-CN"] as const;

export type SupportedCulture = (typeof SUPPORTED_CULTURES)[number];

export const DEFAULT_CULTURE: SupportedCulture = "en-US";

export const STORAGE_KEY = "foodos.culture";
export const COOKIE_NAME = "foodos.culture";

const listeners = new Set<() => void>();

function isSupported(value: string | null | undefined): value is SupportedCulture {
  return value != null && (SUPPORTED_CULTURES as readonly string[]).includes(value);
}

export function resolveCulture(requested: string | null | undefined): SupportedCulture {
  if (!requested) return DEFAULT_CULTURE;
  const trimmed = requested.trim();
  const exact = SUPPORTED_CULTURES.find((c) => c.toLowerCase() === trimmed.toLowerCase());
  if (exact) return exact;
  const language = trimmed.split("-", 1)[0]?.toLowerCase();
  const languageMatch = SUPPORTED_CULTURES.find(
    (c) => c.toLowerCase() === language || c.toLowerCase().startsWith(`${language}-`),
  );
  return languageMatch ?? DEFAULT_CULTURE;
}

function readStored(): SupportedCulture | null {
  if (typeof window === "undefined") return null;
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    return isSupported(stored) ? stored : null;
  } catch {
    return null;
  }
}

function persistCookie(culture: SupportedCulture) {
  if (typeof document === "undefined") return;
  const value = encodeURIComponent(`c=${culture}|uic=${culture}`);
  document.cookie = `${COOKIE_NAME}=${value};path=/;max-age=31536000;samesite=lax`;
}

function applyDocumentLang(culture: SupportedCulture) {
  if (typeof document === "undefined") return;
  document.documentElement.lang = culture;
}

function detectInitial(): SupportedCulture {
  const stored = readStored();
  if (stored) return stored;
  if (typeof navigator === "undefined") return DEFAULT_CULTURE;
  return resolveCulture(navigator.language);
}

let current: SupportedCulture = detectInitial();
applyDocumentLang(current);
persistCookie(current);

export function getCulture(): SupportedCulture {
  return current;
}

export function setCulture(next: SupportedCulture) {
  if (current === next) return;
  current = next;
  try {
    window.localStorage.setItem(STORAGE_KEY, next);
  } catch {
    /* storage unavailable */
  }
  persistCookie(next);
  applyDocumentLang(next);
  for (const listener of listeners) listener();
}

export function subscribeCulture(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function messageKeyForNavItem(to: string): string {
  if (to === "/") return "nav.items.home";
  return `nav.items.${to.replace(/^\//, "").replaceAll("/", ".")}`;
}

export function lookupMessage(messages: unknown, key: string): string | undefined {
  const parts = key.split(".");
  let currentNode: unknown = messages;
  for (const part of parts) {
    if (typeof currentNode !== "object" || currentNode === null || !(part in currentNode)) {
      return undefined;
    }
    currentNode = (currentNode as Record<string, unknown>)[part];
  }
  return typeof currentNode === "string" ? currentNode : undefined;
}
