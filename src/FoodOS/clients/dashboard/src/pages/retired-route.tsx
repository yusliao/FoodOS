import { Link, useLocation } from "react-router-dom";
import { ArrowRight, Building2 } from "lucide-react";
import { EntityEmpty } from "@/components/list";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n/locale-provider";

export function RetiredRoutePage() {
  const t = useT();
  const location = useLocation();
  const requested = `${location.pathname}${location.search}`;

  return (
    <EntityEmpty
      icon={Building2}
      title={t("retiredRoute.title")}
      body={
        <>
          {t("retiredRoute.body")}
          <span className="mt-2 block font-mono text-[11px]" title={requested}>
            {t("retiredRoute.requested").replace("{path}", requested)}
          </span>
        </>
      }
      action={
        <Button asChild>
          <Link to="/shop/catalog">
            {t("retiredRoute.backToShop")}
            <ArrowRight className="size-4" />
          </Link>
        </Button>
      }
    />
  );
}
