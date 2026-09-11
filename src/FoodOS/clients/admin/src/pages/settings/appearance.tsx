import { Moon, Palette, Sun } from "lucide-react";
import { useTheme } from "@/components/theme/theme-provider";
import { Button } from "@/components/ui/button";
import { SettingsSection } from "@/components/list";
import { useLocale } from "@/i18n/locale-provider";
import { SUPPORTED_CULTURES } from "@/i18n/locale-store";
import { cn } from "@/lib/cn";

type Mode = "light" | "dark";

const MODES: {
  value: Mode;
  label: string;
  icon: typeof Sun;
  blurb: string;
}[] = [
  {
    value: "light",
    label: "Light",
    icon: Sun,
    blurb: "Paper-white surfaces, magazine-print mood.",
  },
  {
    value: "dark",
    label: "Dark",
    icon: Moon,
    blurb: "Console-default. Lower glare for long sessions.",
  },
];

/**
 * AppearanceSettings — theme picker. ThemeProvider carries a binary
 * light/dark today; a future "Follow system" mode would extend the provider
 * to a tri-state. Persistence is handled by the provider; we just call setTheme.
 */
export function AppearanceSettings() {
  const { theme, setTheme } = useTheme();
  const { culture, setCulture, t } = useLocale();

  return (
    <div className="space-y-5 fsh-enter">
      {/* Theme */}
      <SettingsSection
        title="Theme"
        icon={Palette}
        description="Console looks good in both modes — the editorial-terminal language is built around tone-neutral surfaces with a single chartreuse accent that reads identically on either."
      >
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {MODES.map(({ value, label, icon: Icon, blurb }) => {
            const active = theme === value;
            return (
              <button
                key={value}
                type="button"
                onClick={() => setTheme(value)}
                aria-pressed={active}
                className={cn(
                  "group/card relative overflow-hidden flex flex-col items-start gap-2 rounded-xl border p-4 text-left",
                  "transition-colors duration-[var(--duration-default)]",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)] focus-visible:ring-offset-2",
                  active
                    ? "border-[var(--color-accent-signal)] bg-[oklch(from_var(--color-accent-signal)_l_c_h_/_0.08)]"
                    : "border-[var(--color-border)] bg-[var(--color-card)] hover:bg-[var(--color-muted)]",
                )}
              >
                <div className="flex w-full items-center justify-between">
                  <span
                    className={cn(
                      "grid h-8 w-8 place-items-center rounded-md",
                      active
                        ? "bg-[oklch(from_var(--color-accent-signal)_l_c_h_/_0.12)] text-[var(--color-accent-signal)]"
                        : "bg-[var(--color-muted)] text-[var(--color-muted-foreground)]",
                    )}
                  >
                    <Icon className="h-4 w-4" />
                  </span>
                  {active && (
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-accent-signal)]">
                      Active
                    </span>
                  )}
                </div>
                <span
                  className={cn(
                    "text-sm font-semibold tracking-tight",
                    active && "text-[var(--color-accent-signal)]",
                  )}
                >
                  {label}
                </span>
                <span className="text-xs leading-relaxed text-[var(--color-muted-foreground)]">
                  {blurb}
                </span>
              </button>
            );
          })}
        </div>
      </SettingsSection>

      <SettingsSection
        title={t("settings.languageTitle")}
        icon={Palette}
        description={t("settings.languageDescription")}
      >
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
          {SUPPORTED_CULTURES.map((code) => {
            const active = culture === code;
            return (
              <button
                key={code}
                type="button"
                onClick={() => setCulture(code)}
                aria-pressed={active}
                className={cn(
                  "flex flex-col items-start gap-1 rounded-xl border p-4 text-left",
                  "transition-colors duration-[var(--duration-default)]",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]",
                  active
                    ? "border-[var(--color-accent-signal)] bg-[oklch(from_var(--color-accent-signal)_l_c_h_/_0.08)]"
                    : "border-[var(--color-border)] bg-[var(--color-card)] hover:bg-[var(--color-muted)]",
                )}
              >
                <span className="text-sm font-semibold tracking-tight">{t(`languages.${code}`)}</span>
                <span className="text-xs text-[var(--color-muted-foreground)]">{code}</span>
              </button>
            );
          })}
        </div>
      </SettingsSection>

      {/* Density — placeholder for a future compact toggle */}
      <SettingsSection
        title="Density"
        icon={Palette}
        description="Compact mode will reduce card padding and row height for data-dense screens — similar to the dashboard's density toggle."
      >
        <Button variant="outline" size="sm" disabled>
          Compact rows · coming soon
        </Button>
      </SettingsSection>
    </div>
  );
}
