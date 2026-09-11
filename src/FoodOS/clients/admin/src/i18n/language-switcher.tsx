import { Check, Globe } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useLocale } from "@/i18n/locale-provider";
import { SUPPORTED_CULTURES } from "@/i18n/locale-store";
import { cn } from "@/lib/cn";

export function LanguageSwitcher({
  compact = true,
  className,
}: {
  compact?: boolean;
  className?: string;
}) {
  const { culture, setCulture, t } = useLocale();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={t("chrome.language")}
          title={t("chrome.language")}
          className={cn(
            "inline-flex h-8 items-center justify-center gap-1.5 rounded-md border border-transparent",
            "text-[var(--color-muted-foreground)] transition-colors",
            "hover:border-[var(--color-border)] hover:text-[var(--color-foreground)]",
            "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]",
            compact ? "w-8" : "px-2.5",
            className,
          )}
        >
          <Globe className="h-4 w-4" aria-hidden />
          {!compact && (
            <span className="text-[12px] font-medium">{t(`languages.${culture}`)}</span>
          )}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-[160px] p-1">
        {SUPPORTED_CULTURES.map((code) => (
          <DropdownMenuItem
            key={code}
            onSelect={() => setCulture(code)}
            className="flex cursor-pointer items-center justify-between gap-3"
          >
            <span className="text-[12.5px]">{t(`languages.${code}`)}</span>
            {culture === code && <Check className="size-3.5" aria-hidden />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
