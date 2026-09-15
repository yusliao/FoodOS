import { NavLink, Outlet } from "react-router-dom";
import { CreditCard } from "lucide-react";
import { cn } from "@/lib/cn";
import { EntityPageHeader } from "@/components/list";
import { useT } from "@/i18n/locale-provider";

/**
 * BillingLayout — page hero + horizontal tabbed sub-nav. Child routes render
 * inside `<Outlet />`.
 */
export function BillingLayout() {
  const t = useT();
  const tabs = [
    { to: "/billing/plans", label: t("billing.plans") },
    { to: "/billing/invoices", label: t("billing.invoices") },
  ];

  return (
    <div className="space-y-6">
      <EntityPageHeader
        icon={CreditCard}
        tone="saffron"
        title={t("billing.title")}
        description={t("billing.description")}
      />

      <nav
        className="flex items-center gap-1 border-b border-[var(--color-border)]"
        aria-label={t("billing.sectionsAria")}
      >
        {tabs.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            className={({ isActive }) =>
              cn(
                "relative -mb-px border-b-2 px-4 py-2.5 text-sm font-medium transition-colors",
                isActive
                  ? "border-[var(--color-foreground)] text-[var(--color-foreground)]"
                  : "border-transparent text-[var(--color-muted-foreground)] hover:text-[var(--color-foreground)]",
              )
            }
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>

      <div className="pt-1">
        <Outlet />
      </div>
    </div>
  );
}
