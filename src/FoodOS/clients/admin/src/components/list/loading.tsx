import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";

type LoadingRowProps = {
  className?: string;
  label?: string;
};

/**
 * LoadingRow — small inline mono-caps placeholder used at the top of
 * a list while the first page resolves. Subtle, no spinner — the
 * caret-style ellipsis is enough.
 */
export function LoadingRow({ className, label }: LoadingRowProps) {
  const t = useT();
  return (
    <div
      role="status"
      className={cn(
        "px-1 py-12 text-center text-sm font-mono uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]",
        className,
      )}
    >
      {label ?? t("common.loading")}
      <span className="caret text-[var(--color-accent-signal)]" aria-hidden />
    </div>
  );
}
