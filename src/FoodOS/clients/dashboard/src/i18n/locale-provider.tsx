import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from "react";
import {
  DEFAULT_CULTURE,
  getCulture,
  lookupMessage,
  setCulture as writeCulture,
  subscribeCulture,
  type SupportedCulture,
} from "@/i18n/locale-store";
import enUS from "@/i18n/locales/en-US.json";
import zhCN from "@/i18n/locales/zh-CN.json";

const catalogs: Record<SupportedCulture, typeof enUS> = {
  "en-US": enUS,
  "zh-CN": zhCN,
};

type LocaleContextValue = {
  culture: SupportedCulture;
  setCulture: (next: SupportedCulture) => void;
  t: (key: string, fallback?: string) => string;
};

const LocaleContext = createContext<LocaleContextValue | null>(null);

export function LocaleProvider({ children }: { children: ReactNode }) {
  const culture = useSyncExternalStore(subscribeCulture, getCulture, () => DEFAULT_CULTURE);
  const messages = catalogs[culture] ?? catalogs[DEFAULT_CULTURE];

  const t = useCallback(
    (key: string, fallback?: string) => lookupMessage(messages, key) ?? fallback ?? key,
    [messages],
  );

  const value = useMemo<LocaleContextValue>(
    () => ({ culture, setCulture: writeCulture, t }),
    [culture, t],
  );

  return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>;
}

export function useLocale() {
  const ctx = useContext(LocaleContext);
  if (!ctx) throw new Error("useLocale must be used within LocaleProvider");
  return ctx;
}

export function useT() {
  return useLocale().t;
}
