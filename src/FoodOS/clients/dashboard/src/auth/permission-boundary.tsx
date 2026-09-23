import type { ReactNode } from "react";
import { ShieldOff } from "lucide-react";
import { useAuth } from "@/auth/use-auth";
import { EntityEmpty } from "@/components/list";
import { Skeleton } from "@/components/ui/skeleton";
import { useT } from "@/i18n/locale-provider";

export function PermissionBoundary({
  permission,
  children,
}: {
  permission: string;
  children: ReactNode;
}) {
  const t = useT();
  const { user, permissionsHydrated } = useAuth();

  if (!permissionsHydrated) {
    return <Skeleton className="h-40 w-full rounded-xl" />;
  }

  if (!user?.permissions.includes(permission)) {
    return (
      <EntityEmpty
        icon={ShieldOff}
        title={t("identity.accessDeniedTitle")}
        body={t("identity.accessDeniedBody")}
      />
    );
  }

  return children;
}
