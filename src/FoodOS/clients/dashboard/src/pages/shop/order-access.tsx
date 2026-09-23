import type { ReactNode } from "react";
import { ShieldOff } from "lucide-react";
import { SHOP_PERMISSIONS } from "@/api/shop";
import { useAuth } from "@/auth/use-auth";
import { EntityEmpty } from "@/components/list";
import { Skeleton } from "@/components/ui/skeleton";
import { useT } from "@/i18n/locale-provider";

export function ShopOrderAccess({ children }: { children: ReactNode }) {
  const t = useT();
  const { user, permissionsHydrated } = useAuth();

  if (!permissionsHydrated) {
    return <Skeleton className="h-40 w-full rounded-xl" />;
  }

  if (!user?.permissions.includes(SHOP_PERMISSIONS.order)) {
    return (
      <EntityEmpty
        icon={ShieldOff}
        title={t("shop.orderAccessTitle", "Ordering access required")}
        body={t(
          "shop.orderAccessBody",
          "Your account can browse the customer catalog but cannot change carts or file after-sales claims.",
        )}
      />
    );
  }

  return children;
}
