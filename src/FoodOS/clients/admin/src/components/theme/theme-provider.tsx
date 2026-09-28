import { createContext, useContext, useEffect, useMemo, useState } from "react";

type Theme = "light" | "dark";

export const ACCENTS = [
  { id: "rose", swatch: "oklch(0.575 0.232 13)" },
  { id: "indigo", swatch: "oklch(0.555 0.220 268)" },
  { id: "violet", swatch: "oklch(0.555 0.220 305)" },
  { id: "sky", swatch: "oklch(0.555 0.180 232)" },
  { id: "emerald", swatch: "oklch(0.545 0.170 152)" },
  { id: "amber", swatch: "oklch(0.620 0.165 76)" },
] as const;

type Accent = (typeof ACCENTS)[number]["id"];

type ThemeContextValue = {
  theme: Theme;
  setTheme: (t: Theme) => void;
  toggle: () => void;
  accent: Accent;
  setAccent: (accent: Accent) => void;
};

const STORAGE_KEY = "fsh.admin.theme";
const ACCENT_STORAGE_KEY = "fsh.admin.accent";

function resolveInitialAccent(): Accent {
  if (typeof window === "undefined") return "rose";
  const stored = window.localStorage.getItem(ACCENT_STORAGE_KEY);
  return ACCENTS.find(({ id }) => id === stored)?.id ?? "rose";
}

// Console is dark-first. We try storage → system pref → dark, in that order.
function resolveInitialTheme(): Theme {
  if (typeof window === "undefined") return "dark";
  const stored = window.localStorage.getItem(STORAGE_KEY);
  if (stored === "light" || stored === "dark") return stored;
  if (window.matchMedia?.("(prefers-color-scheme: light)").matches) return "light";
  return "dark";
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(resolveInitialTheme);
  const [accent, setAccentState] = useState<Accent>(resolveInitialAccent);

  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle("dark", theme === "dark");
    root.style.colorScheme = theme;
    window.localStorage.setItem(STORAGE_KEY, theme);
  }, [theme]);

  useEffect(() => {
    const root = document.documentElement;
    for (const { id } of ACCENTS) root.classList.remove(`accent-${id}`);
    if (accent !== "rose") root.classList.add(`accent-${accent}`);
    window.localStorage.setItem(ACCENT_STORAGE_KEY, accent);
  }, [accent]);

  const value = useMemo<ThemeContextValue>(
    () => ({
      theme,
      setTheme: setThemeState,
      toggle: () => setThemeState((t) => (t === "dark" ? "light" : "dark")),
      accent,
      setAccent: setAccentState,
    }),
    [theme, accent],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used inside <ThemeProvider>");
  return ctx;
}
