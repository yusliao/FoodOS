import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ShieldOff } from "lucide-react";
import { toast } from "sonner";
import {
  revokeImpersonationGrant,
  type ImpersonationGrantDto,
} from "@/api/impersonation-grants";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { IdentityPermissions } from "@/lib/permissions";

type Props = {
  grant: ImpersonationGrantDto | null;
  onOpenChange: (open: boolean) => void;
  /** Optional callback after a successful revoke — receives the updated grant. */
  onRevoked?: (updated: ImpersonationGrantDto) => void;
};

/**
 * RevokeGrantDialog — confirmation modal for revoking an active impersonation
 * grant. Asks for an optional reason (recorded on the grant + in the security
 * audit trail) and surfaces the impersonated user / actor / tenant trio so the
 * operator knows exactly what they're killing.
 */
export function RevokeGrantDialog({ grant, onOpenChange, onRevoked }: Props) {
  const t = useT();
  const { user } = useAuth();
  const canRevoke = !!user?.permissions.includes(IdentityPermissions.Impersonation.Revoke);
  const queryClient = useQueryClient();
  const [reason, setReason] = useState("");
  const open = grant !== null;

  // Reset reason whenever a new grant is targeted (or the dialog closes).
  useEffect(() => {
    if (open) setReason("");
  }, [open, grant?.id]);

  const mutation = useMutation<ImpersonationGrantDto, Error, { id: string; reason?: string }>({
    mutationFn: ({ id, reason }) => revokeImpersonationGrant(id, reason),
    onSuccess: async (updated) => {
      toast.success(t("impersonation.revokedToast"), {
        description: t("impersonation.revokedToastBody").replace(
          "{name}",
          updated.impersonatedUserName ?? updated.impersonatedUserId,
        ),
      });
      await queryClient.invalidateQueries({ queryKey: ["impersonation-grants"] });
      onRevoked?.(updated);
      onOpenChange(false);
    },
    onError: (err) => {
      const detail =
        err instanceof ApiRequestError
          ? err.problem?.detail ?? err.problem?.title ?? err.message
          : err.message;
      toast.error(t("settings.revokeFailed"), { description: detail });
    },
  });

  return (
    <Dialog open={open && canRevoke} onOpenChange={(next) => { if (!mutation.isPending) onOpenChange(next); }}>
      <DialogContent size="md">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <span
              aria-hidden
              className="grid h-7 w-7 place-items-center rounded-md bg-[var(--color-destructive)]/15 text-[var(--color-destructive)]"
            >
              <ShieldOff className="h-4 w-4" />
            </span>
            <DialogTitle>{t("impersonation.revokeTitle")}</DialogTitle>
          </div>
          <DialogDescription>
            {t("impersonation.revokeDesc")}
          </DialogDescription>
        </DialogHeader>

        <DialogBody className="space-y-4">
          {grant && <GrantSummary grant={grant} />}

          <div className="space-y-1.5">
            <label
              htmlFor="revoke-reason"
              className="meta text-[var(--color-muted-foreground)]"
            >
              {t("impersonation.reasonOptional")}
            </label>
            <textarea
              id="revoke-reason"
              value={reason}
              disabled={mutation.isPending}
              onChange={(e) => setReason(e.target.value)}
              placeholder={t("impersonation.revokePlaceholder")}
              rows={3}
              maxLength={500}
              className={cn(
                "w-full resize-y rounded-md border border-[var(--color-input)] bg-transparent px-3 py-2 text-sm",
                "transition-[border-color,background-color,box-shadow] duration-[var(--duration-fast)]",
                "hover:border-[var(--color-border-strong)]",
                "focus-visible:outline-none focus-visible:border-[var(--color-accent-signal)] focus-visible:ring-2 focus-visible:ring-[oklch(from_var(--color-accent-signal)_l_c_h_/_0.25)] focus-visible:bg-[var(--color-surface-2)]",
              )}
            />
            <p className="text-[11px] text-[var(--color-muted-foreground)]">
              {t("impersonation.revokeHint")}
            </p>
          </div>
        </DialogBody>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
            {t("chrome.cancel")}
          </Button>
          <Button
            variant="destructive"
            onClick={() => { if (canRevoke && grant?.status === "Active" && !mutation.isPending) mutation.mutate({ id: grant.id, reason: reason.trim() || undefined }); }}
            disabled={mutation.isPending || grant?.status !== "Active"}
          >
            <ShieldOff className="mr-1 h-3.5 w-3.5" />
            {mutation.isPending ? t("settings.revoking") : t("impersonation.revokeNow")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function GrantSummary({ grant }: { grant: ImpersonationGrantDto }) {
  const t = useT();
  return (
    <div className="space-y-2 rounded-md border border-[var(--color-border)] bg-[var(--color-surface-2)] px-3 py-3">
      <Row label={t("impersonation.impersonating")}>
        <span className="font-medium">
          {grant.impersonatedUserName ?? grant.impersonatedUserId}
        </span>{" "}
        <Badge variant="muted" className="ml-1 font-mono uppercase tracking-[0.14em]">
          {grant.impersonatedTenantId}
        </Badge>
      </Row>
      <Row label={t("impersonation.startedBy")}>
        <span>{grant.actorUserName ?? grant.actorUserId}</span>{" "}
        <Badge variant="muted" className="ml-1 font-mono uppercase tracking-[0.14em]">
          {grant.actorTenantId}
        </Badge>
      </Row>
      <Row label={t("impersonation.reason")}>
        <span className="text-[var(--color-muted-foreground)]">{grant.reason || "—"}</span>
      </Row>
      <Row label={t("impersonation.expires")}>
        <code className="code-chip">{new Date(grant.expiresAtUtc).toLocaleString()}</code>
      </Row>
    </div>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[7rem_1fr] items-baseline gap-3">
      <span className="meta text-[var(--color-muted-foreground)]">{label}</span>
      <span className="min-w-0 text-sm">{children}</span>
    </div>
  );
}
