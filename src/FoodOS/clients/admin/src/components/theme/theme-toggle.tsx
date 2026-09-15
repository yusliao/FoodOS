import { Moon, Sun } from "lucide-react";
import { useTheme } from "@/components/theme/theme-provider";
import { useT } from "@/i18n/locale-provider";
import { cn } from "@/lib/cn";

/**
 * ThemeToggle — switches between light and dark. Renders the *destination*
 * icon (i.e. if you're in dark, you see Sun, because clicking will send you
 * to light) so it reads as a directional affordance rather than a status.
 */
export function ThemeToggle({ className }: { className?: string }) {
  const t = useT();
  const { theme, toggle } = useTheme();
  const Icon = theme === "dark" ? Sun : Moon;
  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={theme === "dark" ? t("common.switchToLight") : t("common.switchToDark")}
      className={cn(
        "group relative inline-flex h-8 w-8 items-center justify-center rounded-md border border-transparent text-[var(--color-muted-foreground)] transition-colors hover:border-[var(--color-border)] hover:text-[var(--color-foreground)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]",
        className,
      )}
    >
      <Icon className="h-4 w-4" />
    </button>
  );
}
