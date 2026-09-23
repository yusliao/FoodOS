import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { toast } from "sonner";
import {
  CheckCircle2,
  CircleSlash2,
  Clock,
  Globe,
  Mail,
  MonitorSmartphone,
  Phone,
  Power,
  PowerOff,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  Trash2,
  User as UserIcon,
  UserCog,
  XCircle,
} from "lucide-react";
import {
  adminRevokeAllUserSessions,
  adminRevokeUserSession,
  assignUserRoles,
  confirmUserEmail,
  deleteUser,
  getUserById,
  getUserRoles,
  getUserSessionsAdmin,
  IDENTITY_PERMISSIONS,
  resendUserConfirmationEmail,
  toggleUserStatus,
  type AdminUserSessionDto,
  type UserRoleDto,
} from "@/api/identity";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  EntityDetailAvatar,
  EntityDetailBack,
  EntityDetailHero,
  EntityDetailMeta,
  EntityDetailSection,
  EntityDetailStat,
  ErrorBand,
} from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { cn } from "@/lib/cn";
import { useLocale, useT } from "@/i18n/locale-provider";

type DialogState =
  | { mode: "closed" }
  | { mode: "delete" }
  | { mode: "toggle-status" }
  | { mode: "impersonate" }
  | { mode: "revoke-all-sessions" };

function fullName(
  u: { firstName?: string; lastName?: string; userName?: string; email?: string },
  unnamed: string,
): string {
  const parts = [u.firstName, u.lastName].filter(Boolean);
  if (parts.length > 0) return parts.join(" ");
  return u.userName ?? u.email ?? unnamed;
}

// ───────────────────────────────────────────────────────────────────────
//  Page
// ───────────────────────────────────────────────────────────────────────

export function UserDetailPage() {
  const t = useT();
  const { userId = "" } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user: actor, beginImpersonation } = useAuth();
  const [dialog, setDialog] = useState<DialogState>({ mode: "closed" });
  const [impersonationReason, setImpersonationReason] = useState("");
  const [pending, setPending] = useState<Map<string, boolean>>(new Map());

  const canUpdate = (actor?.permissions ?? []).includes(IDENTITY_PERMISSIONS.users.update);
  const canDelete = (actor?.permissions ?? []).includes(IDENTITY_PERMISSIONS.users.delete);
  const canManageRoles = (actor?.permissions ?? []).includes(IDENTITY_PERMISSIONS.users.manageRoles);
  const canImpersonate = (actor?.permissions ?? []).includes("Permissions.Users.Impersonate");
  const canViewSessions = (actor?.permissions ?? []).includes(IDENTITY_PERMISSIONS.sessions.viewAll);
  const canRevokeSessions = (actor?.permissions ?? []).includes(IDENTITY_PERMISSIONS.sessions.revokeAll);
  const canConfirmEmail = (actor?.permissions ?? []).includes(IDENTITY_PERMISSIONS.users.confirmEmail);

  const userQuery = useQuery({
    queryKey: ["identity", "users", userId],
    queryFn: () => getUserById(userId),
    enabled: !!userId,
  });

  const rolesQuery = useQuery({
    queryKey: ["identity", "users", userId, "roles"],
    queryFn: () => getUserRoles(userId),
    enabled: !!userId,
  });

  const user = userQuery.data;
  const roles = useMemo(() => rolesQuery.data ?? [], [rolesQuery.data]);

  // Clear staged toggles only when navigating to a different user — NOT on
  // every `roles` array identity change. `roles` is `rolesQuery.data ?? []`,
  // so an incidental background refetch would otherwise wipe unsaved edits.
  // The deterministic post-save clear lives in saveRoles.onSuccess.
  useEffect(() => {
    setPending(new Map());
  }, [userId]);

  const effective = (role: UserRoleDto) => {
    if (!role.roleId) return role.enabled;
    return pending.has(role.roleId) ? !!pending.get(role.roleId) : role.enabled;
  };

  const dirtyIds = useMemo(() => {
    const out: string[] = [];
    for (const r of roles) {
      if (!r.roleId) continue;
      if (pending.has(r.roleId) && pending.get(r.roleId) !== r.enabled) {
        out.push(r.roleId);
      }
    }
    return out;
  }, [roles, pending]);

  const isDirty = dirtyIds.length > 0;

  const toggle = (role: UserRoleDto) => {
    if (!role.roleId) return;
    setPending((prev) => {
      const next = new Map(prev);
      const current = next.has(role.roleId!) ? !!next.get(role.roleId!) : role.enabled;
      next.set(role.roleId!, !current);
      return next;
    });
  };

  const saveRoles = useMutation({
    mutationFn: () => {
      const payload: UserRoleDto[] = roles.map((r) => ({
        roleId: r.roleId,
        roleName: r.roleName,
        description: r.description,
        enabled: effective(r),
      }));
      return assignUserRoles(userId, payload);
    },
    onSuccess: () => {
      toast.success(t("identity.users.rolesUpdated"), {
        description:
          dirtyIds.length === 1
            ? t("identity.users.rolesChangedOne")
            : t("identity.users.rolesChanged").replace("{n}", String(dirtyIds.length)),
      });
      setPending(new Map());
      void queryClient.invalidateQueries({
        queryKey: ["identity", "users", userId, "roles"],
      });
    },
    onError: (err) => toast.error(t("identity.updateFailed"), { description: describe(err) }),
  });

  const toggleStatus = useMutation({
    mutationFn: () => {
      if (!user?.id) throw new Error("Missing user id");
      return toggleUserStatus(user.id, !user.isActive);
    },
    onSuccess: () => {
      toast.success(user?.isActive ? t("identity.users.deactivated") : t("identity.users.reactivated"));
      void queryClient.invalidateQueries({ queryKey: ["identity", "users", userId] });
      void queryClient.invalidateQueries({ queryKey: ["identity", "users"] });
      setDialog({ mode: "closed" });
    },
    onError: (err) => toast.error(t("identity.users.statusFailed"), { description: describe(err) }),
  });

  const confirmEmail = useMutation({
    mutationFn: () => {
      if (!user?.id) throw new Error("Missing user id");
      return confirmUserEmail(user.id);
    },
    onSuccess: () => {
      toast.success(t("identity.users.emailConfirmedToast"));
      void queryClient.invalidateQueries({ queryKey: ["identity", "users", userId] });
      void queryClient.invalidateQueries({ queryKey: ["identity", "users"] });
    },
    onError: (err) => toast.error(t("identity.users.confirmEmailFailed"), { description: describe(err) }),
  });

  const resendConfirmation = useMutation({
    mutationFn: () => {
      if (!user?.id) throw new Error("Missing user id");
      return resendUserConfirmationEmail(user.id);
    },
    onSuccess: () => toast.success(t("identity.users.confirmationSent")),
    onError: (err) => toast.error(t("identity.users.confirmationSendFailed"), { description: describe(err) }),
  });

  const removeUser = useMutation({
    mutationFn: () => {
      if (!user?.id) throw new Error("Missing user id");
      return deleteUser(user.id);
    },
    onSuccess: () => {
      toast.success(t("identity.users.deleted"));
      void queryClient.invalidateQueries({ queryKey: ["identity", "users"] });
      navigate("/identity/users");
    },
    onError: (err) => {
      toast.error(t("identity.deleteFailed"), { description: describe(err) });
      setDialog({ mode: "closed" });
    },
  });

  // Admin sessions
  const sessionsQuery = useQuery({
    queryKey: ["identity", "users", userId, "sessions"],
    queryFn: () => getUserSessionsAdmin(userId),
    enabled: !!userId && canViewSessions,
    staleTime: 15_000,
  });

  const revokeOne = useMutation({
    mutationFn: (sessionId: string) => adminRevokeUserSession(userId, sessionId),
    onSuccess: () => {
      toast.success(t("identity.users.sessionRevoked"));
      void queryClient.invalidateQueries({
        queryKey: ["identity", "users", userId, "sessions"],
      });
    },
    onError: (err) => toast.error(t("identity.users.revokeFailed"), { description: describe(err) }),
  });

  const revokeAll = useMutation({
    mutationFn: () => adminRevokeAllUserSessions(userId),
    onSuccess: (data) => {
      toast.success(
        data.revokedCount === 1
          ? t("identity.users.revokedCountOne")
          : t("identity.users.revokedCount").replace("{n}", String(data.revokedCount)),
      );
      void queryClient.invalidateQueries({
        queryKey: ["identity", "users", userId, "sessions"],
      });
      setDialog({ mode: "closed" });
    },
    onError: (err) => toast.error(t("identity.users.revokeAllFailed"), { description: describe(err) }),
  });

  // Impersonation
  const impersonate = useMutation({
    mutationFn: () => {
      if (!user?.id) throw new Error("Missing user id");
      if (!actor?.tenant) throw new Error("No tenant on current session");
      return beginImpersonation({
        targetUserId: user.id,
        targetTenantId: actor.tenant,
        reason: impersonationReason.trim() || undefined,
      });
    },
    onSuccess: () => {
      toast.success(t("identity.users.impersonationStarted"), {
        description: t("identity.users.impersonationStartedBody"),
      });
      setDialog({ mode: "closed" });
      setImpersonationReason("");
      navigate("/", { replace: true });
    },
    onError: (err) => {
      toast.error(t("identity.users.impersonationFailed"), { description: describe(err) });
    },
  });

  if (userQuery.isLoading) {
    return (
      <div className="space-y-6">
        <EntityDetailBack to="/identity/users" label={t("identity.users.back")} />
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    );
  }

  if (userQuery.isError || !user) {
    return (
      <div className="space-y-4">
        <EntityDetailBack to="/identity/users" label={t("identity.users.back")} />
        <ErrorBand
          message={
            userQuery.error
              ? describe(userQuery.error)
              : t("identity.users.notFound")
          }
        />
      </div>
    );
  }

  const display = fullName(user, t("identity.unnamedUser"));
  const activeRolesCount = roles.filter((r) => effective(r)).length;
  const sessions = sessionsQuery.data ?? [];
  const activeSessionsCount = sessions.filter((s) => s.isActive).length;
  const subtitleParts: string[] = [];
  if (user.userName) subtitleParts.push(`@${user.userName}`);
  if (user.email) subtitleParts.push(user.email);
  if (user.phoneNumber) subtitleParts.push(user.phoneNumber);

  return (
    <div className="space-y-5 pb-12">
      <EntityDetailBack to="/identity/users" label={t("identity.users.back")} />

      <EntityDetailHero
        avatar={
          <EntityDetailAvatar
            name={display}
            src={user.imageUrl ?? undefined}
          />
        }
        title={display}
        badges={
          <>
            {user.isActive ? (
              <Badge variant="success">
                <ShieldCheck className="h-3 w-3" /> {t("identity.active")}
              </Badge>
            ) : (
              <Badge variant="outline">
                <CircleSlash2 className="h-3 w-3" /> {t("identity.inactive")}
              </Badge>
            )}
            {user.emailConfirmed ? (
              <Badge variant="brand">
                <CheckCircle2 className="h-3 w-3" /> {t("identity.users.emailConfirmed")}
              </Badge>
            ) : (
              <Badge variant="warning">
                <Mail className="h-3 w-3" /> {t("identity.users.emailPending")}
              </Badge>
            )}
          </>
        }
        subtitle={subtitleParts.join(" · ") || t("identity.users.member")}
        actions={
          <>
            {canImpersonate && user.id !== actor?.id && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setDialog({ mode: "impersonate" })}
                disabled={!user.isActive}
                title={!user.isActive ? t("identity.users.cannotImpersonateInactive") : undefined}
                className="gap-1.5"
              >
                <UserCog className="h-3.5 w-3.5" /> {t("identity.users.impersonate")}
              </Button>
            )}
            {canConfirmEmail && !user.emailConfirmed && (
              <>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => resendConfirmation.mutate()}
                  disabled={resendConfirmation.isPending}
                  className="gap-1.5"
                >
                  <Mail className="h-3.5 w-3.5" />
                  {resendConfirmation.isPending ? t("identity.users.sending") : t("identity.users.resendConfirmation")}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => confirmEmail.mutate()}
                  disabled={confirmEmail.isPending}
                  className="gap-1.5"
                >
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  {confirmEmail.isPending ? t("identity.users.confirming") : t("identity.users.confirmEmail")}
                </Button>
              </>
            )}
            {canUpdate && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setDialog({ mode: "toggle-status" })}
              >
                {user.isActive ? (
                  <>
                    <PowerOff className="mr-1 h-3.5 w-3.5" /> {t("identity.users.deactivate")}
                  </>
                ) : (
                  <>
                    <Power className="mr-1 h-3.5 w-3.5" /> {t("identity.users.reactivate")}
                  </>
                )}
              </Button>
            )}
            {canDelete && (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setDialog({ mode: "delete" })}
              >
                <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("identity.users.delete")}
              </Button>
            )}
          </>
        }
        stats={
          <>
            <EntityDetailStat
              icon={ShieldCheck}
              value={activeRolesCount}
              label={activeRolesCount === 1 ? t("identity.users.roleUnit") : t("identity.users.rolesUnit")}
              tone="primary"
            />
            {canViewSessions && (
              <EntityDetailStat
                icon={MonitorSmartphone}
                value={activeSessionsCount}
                label={activeSessionsCount === 1 ? t("identity.users.sessionUnit") : t("identity.users.sessionsUnit")}
                tone={activeSessionsCount > 0 ? "success" : "default"}
              />
            )}
          </>
        }
        meta={
          <>
            {user.email && (
              <EntityDetailMeta icon={Mail} hideOnMobile>
                {user.email}
              </EntityDetailMeta>
            )}
            {user.phoneNumber && (
              <EntityDetailMeta icon={Phone} hideOnMobile>
                {user.phoneNumber}
              </EntityDetailMeta>
            )}
          </>
        }
      />

      <div className="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        {/* Profile */}
        <EntityDetailSection
          title={t("identity.users.identityCard")}
          icon={UserIcon}
          description={t("identity.users.identityCardDesc")}
        >
          <div className="space-y-3">
            <ProfileRow label={t("identity.users.username")} value={user.userName ?? "—"} />
            <ProfileRow label={t("identity.users.email")} value={user.email ?? "—"} />
            <ProfileRow label={t("identity.users.firstName")} value={user.firstName ?? "—"} />
            <ProfileRow label={t("identity.users.lastName")} value={user.lastName ?? "—"} />
            <ProfileRow label={t("identity.users.phone")} value={user.phoneNumber ?? "—"} />
            <ProfileRow
              label={t("identity.users.id")}
              value={<span className="font-mono text-[11px]">{user.id}</span>}
            />
          </div>
        </EntityDetailSection>

        {/* Roles */}
        <EntityDetailSection
          title={t("identity.users.roleAssignment")}
          icon={ShieldCheck}
          description={t("identity.users.roleAssignmentDesc")}
          action={
            isDirty ? (
              <Badge variant="warning">
                {t("identity.users.pendingCount").replace("{n}", String(dirtyIds.length))}
              </Badge>
            ) : undefined
          }
          padded={false}
          footer={
            canManageRoles && roles.length > 0 ? (
              <div className="flex items-center justify-end gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPending(new Map())}
                  disabled={!isDirty || saveRoles.isPending}
                >
                  {t("identity.discard")}
                </Button>
                <Button
                  size="sm"
                  onClick={() => saveRoles.mutate()}
                  disabled={!isDirty || saveRoles.isPending}
                >
                  {saveRoles.isPending ? t("identity.saving") : t("identity.saveChanges")}
                </Button>
              </div>
            ) : undefined
          }
        >
          {rolesQuery.isLoading ? (
            <div className="space-y-3 p-5">
              <Skeleton className="h-12 w-full rounded-md" />
              <Skeleton className="h-12 w-full rounded-md" />
              <Skeleton className="h-12 w-full rounded-md" />
            </div>
          ) : rolesQuery.isError ? (
            <div className="p-5">
              <ErrorBand message={describe(rolesQuery.error)} />
            </div>
          ) : roles.length === 0 ? (
            <div className="p-5 text-sm text-[var(--color-muted-foreground)]">
              {t("identity.users.noRoles")}{" "}
              <Link to="/identity/roles" className="underline hover:text-[var(--color-foreground)]">
                {t("identity.users.createOne")}
              </Link>{" "}
              {t("identity.users.toAssign")}
            </div>
          ) : (
            <ul>
              {roles.map((role) => {
                const isOn = effective(role);
                const dirty =
                  role.roleId !== undefined &&
                  pending.has(role.roleId) &&
                  pending.get(role.roleId) !== role.enabled;
                return (
                  <li
                    key={role.roleId}
                    className="flex items-center justify-between gap-3 border-b border-[var(--color-border)] px-5 py-3.5 last:border-b-0 transition-colors hover:bg-[var(--color-accent)]"
                  >
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium tracking-tight">
                          {role.roleName ?? t("identity.users.untitledRole")}
                        </span>
                        {dirty && (
                          <span
                            className="inline-block h-1.5 w-1.5 rounded-full bg-[var(--color-warning)]"
                            aria-label={t("identity.users.modified")}
                          />
                        )}
                      </div>
                      {role.description && (
                        <div className="mt-0.5 line-clamp-1 text-[12.5px] text-[var(--color-muted-foreground)]">
                          {role.description}
                        </div>
                      )}
                    </div>
                    <Switch
                      checked={isOn}
                      onCheckedChange={() => toggle(role)}
                      disabled={!canManageRoles}
                      aria-label={t("identity.users.toggleRole").replace("{name}", role.roleName ?? t("identity.users.roleUnit"))}
                    />
                  </li>
                );
              })}
            </ul>
          )}
        </EntityDetailSection>
      </div>

      {/* Sessions */}
      {canViewSessions && (
        <SessionsCard
          sessions={sessionsQuery.data ?? []}
          isLoading={sessionsQuery.isLoading}
          isError={sessionsQuery.isError}
          error={sessionsQuery.error}
          canRevoke={canRevokeSessions}
          onRevoke={(id) => revokeOne.mutate(id)}
          onRevokeAll={() => setDialog({ mode: "revoke-all-sessions" })}
          revokingId={revokeOne.isPending ? revokeOne.variables : null}
        />
      )}

      {/* Delete confirmation */}
      {canDelete && <Dialog
        open={dialog.mode === "delete"}
        onOpenChange={(o) => (!o ? setDialog({ mode: "closed" }) : undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("identity.users.deleteTitle")}</DialogTitle>
            <DialogDescription>
              {t("identity.users.deleteBody").replace("{name}", display)}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" disabled={removeUser.isPending}>
                {t("chrome.cancel")}
              </Button>
            </DialogClose>
            <Button
              variant="destructive"
              onClick={() => removeUser.mutate()}
              disabled={removeUser.isPending}
            >
              {removeUser.isPending ? t("identity.deleting") : t("identity.users.deleteUser")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>}

      {/* Toggle status confirmation */}
      {canUpdate && <Dialog
        open={dialog.mode === "toggle-status"}
        onOpenChange={(o) => (!o ? setDialog({ mode: "closed" }) : undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{user.isActive ? t("identity.users.deactivateTitle") : t("identity.users.reactivateTitle")}</DialogTitle>
            <DialogDescription>
              {user.isActive
                ? t("identity.users.deactivateBody").replace("{name}", display)
                : t("identity.users.reactivateBody").replace("{name}", display)}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" disabled={toggleStatus.isPending}>
                {t("chrome.cancel")}
              </Button>
            </DialogClose>
            <Button onClick={() => toggleStatus.mutate()} disabled={toggleStatus.isPending}>
              {toggleStatus.isPending
                ? t("identity.working")
                : user.isActive
                  ? t("identity.users.deactivate")
                  : t("identity.users.reactivate")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>}

      {/* Impersonation confirmation */}
      <Dialog
        open={dialog.mode === "impersonate"}
        onOpenChange={(o) => (!o ? setDialog({ mode: "closed" }) : undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("identity.users.impersonateTitle").replace("{name}", display)}</DialogTitle>
            <DialogDescription>
              {t("identity.users.impersonateBody")}
            </DialogDescription>
          </DialogHeader>
          <div className="px-6 pb-2">
            <label className="block">
              <span className="text-[11.5px] font-medium text-[var(--color-muted-foreground)]">
                {t("identity.users.reasonLabel")}
              </span>
              <input
                type="text"
                value={impersonationReason}
                onChange={(e) => setImpersonationReason(e.target.value)}
                placeholder={t("identity.users.reasonPlaceholder")}
                maxLength={256}
                className={cn(
                  "mt-1.5 flex h-9 w-full rounded-md border border-[var(--color-input)]",
                  "bg-transparent px-3 py-1 text-sm shadow-sm",
                  "placeholder:text-[var(--color-muted-foreground)]",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]",
                )}
              />
            </label>
          </div>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" disabled={impersonate.isPending}>
                {t("chrome.cancel")}
              </Button>
            </DialogClose>
            <Button
              onClick={() => impersonate.mutate()}
              disabled={impersonate.isPending}
              className={cn(
                "gap-1.5",
                "bg-[var(--color-warning)] text-[var(--color-warning-foreground,white)] hover:opacity-90",
              )}
            >
              <ShieldAlert className="h-3.5 w-3.5" />
              {impersonate.isPending ? t("identity.users.starting") : t("identity.users.startImpersonation")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Revoke all sessions confirmation */}
      <Dialog
        open={dialog.mode === "revoke-all-sessions"}
        onOpenChange={(o) => (!o ? setDialog({ mode: "closed" }) : undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("identity.users.revokeAllTitle").replace("{name}", display)}</DialogTitle>
            <DialogDescription>
              {t("identity.users.revokeAllBody")}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" disabled={revokeAll.isPending}>
                {t("chrome.cancel")}
              </Button>
            </DialogClose>
            <Button
              variant="destructive"
              onClick={() => revokeAll.mutate()}
              disabled={revokeAll.isPending}
            >
              {revokeAll.isPending ? t("identity.users.revoking") : t("identity.users.revokeAll")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ProfileRow({
  label,
  value,
}: {
  label: string;
  value: React.ReactNode;
}) {
  return (
    <div className="flex items-baseline justify-between gap-3 border-b border-[oklch(from_var(--color-border)_l_c_h_/_0.5)] pb-2.5 last:border-b-0 last:pb-0">
      <div className="text-[11.5px] font-medium text-[var(--color-muted-foreground)]">
        {label}
      </div>
      <div className="min-w-0 truncate text-right text-[13px] text-[var(--color-foreground)]">
        {value}
      </div>
    </div>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Sessions card
// ───────────────────────────────────────────────────────────────────────

const sessionDateFmt = (culture: string) =>
  new Intl.DateTimeFormat(culture, {
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });

function describeDevice(s: AdminUserSessionDto, t: (key: string) => string): string {
  const browser = s.browser ?? t("settings.unknownBrowser");
  const version = s.browserVersion ? ` ${s.browserVersion}` : "";
  const os = s.operatingSystem ?? t("settings.unknownOs");
  return t("settings.deviceLine")
    .replace("{browser}", `${browser}${version}`)
    .replace("{os}", os);
}

function deviceIcon(s: AdminUserSessionDto) {
  const isMobile = (s.deviceType ?? "").toLowerCase().includes("mobile");
  return isMobile ? Smartphone : MonitorSmartphone;
}

function SessionsCard({
  sessions,
  isLoading,
  isError,
  error,
  canRevoke,
  onRevoke,
  onRevokeAll,
  revokingId,
}: {
  sessions: AdminUserSessionDto[];
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  canRevoke: boolean;
  onRevoke: (sessionId: string) => void;
  onRevokeAll: () => void;
  revokingId: string | null | undefined;
}) {
  const t = useT();
  const { culture } = useLocale();
  const dateFmt = sessionDateFmt(culture);
  const ordered = [...sessions].sort(
    (a, b) =>
      new Date(b.lastActivityAt).getTime() - new Date(a.lastActivityAt).getTime(),
  );
  const activeCount = ordered.filter((s) => s.isActive).length;

  return (
    <EntityDetailSection
      title={t("identity.users.activeSessions")}
      icon={MonitorSmartphone}
      description={
        isLoading
          ? t("identity.users.loadingSessions")
          : t("identity.users.sessionsSummary")
              .replace("{active}", String(activeCount))
              .replace("{total}", String(ordered.length))
      }
      action={
        canRevoke && activeCount > 0 ? (
          <Button variant="outline" size="sm" onClick={onRevokeAll} className="gap-1.5">
            <XCircle className="h-3.5 w-3.5" /> {t("identity.users.revokeAll")}
          </Button>
        ) : undefined
      }
      padded={false}
    >
      {isLoading ? (
        <div className="space-y-3 p-5">
          <Skeleton className="h-14 w-full rounded-md" />
          <Skeleton className="h-14 w-full rounded-md" />
        </div>
      ) : isError ? (
        <div className="p-5">
          <ErrorBand message={describe(error)} />
        </div>
      ) : ordered.length === 0 ? (
        <div className="p-5 text-sm text-[var(--color-muted-foreground)]">
          {t("identity.users.noSessions")}
        </div>
      ) : (
        <ul>
          {ordered.map((session) => {
            const DIcon = deviceIcon(session);
            const isRevoking = revokingId === session.id;
            return (
              <li
                key={session.id}
                className={cn(
                  "flex items-center gap-3 border-b border-[var(--color-border)] px-5 py-3.5 last:border-b-0",
                  "transition-colors hover:bg-[var(--color-accent)]",
                  !session.isActive && "opacity-60",
                )}
              >
                <span
                  aria-hidden
                  className={cn(
                    "grid h-9 w-9 shrink-0 place-items-center rounded-lg",
                    session.isActive
                      ? "bg-[var(--color-primary-soft)] text-[var(--color-primary)]"
                      : "bg-[var(--color-muted)] text-[var(--color-muted-foreground)]",
                  )}
                >
                  <DIcon className="h-4 w-4" />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="truncate text-sm font-medium tracking-tight">
                      {describeDevice(session, t)}
                    </span>
                    {session.isActive ? (
                      <Badge variant="success">{t("identity.active")}</Badge>
                    ) : (
                      <Badge variant="outline">{t("identity.ended")}</Badge>
                    )}
                  </div>
                  <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[12px] text-[var(--color-muted-foreground)]">
                    {session.ipAddress && (
                      <span className="inline-flex items-center gap-1 font-mono">
                        <Globe className="h-3 w-3" /> {session.ipAddress}
                      </span>
                    )}
                    <span className="inline-flex items-center gap-1">
                      <Clock className="h-3 w-3" />                       {t("identity.users.lastSeen").replace("{time}", dateFmt.format(new Date(session.lastActivityAt)))}
                    </span>
                    <span className="opacity-70">
                      {t("identity.users.started").replace("{time}", dateFmt.format(new Date(session.createdAt)))}
                    </span>
                  </div>
                </div>
                {canRevoke && session.isActive && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onRevoke(session.id)}
                    disabled={isRevoking}
                    className="shrink-0 text-[var(--color-muted-foreground)] hover:text-[var(--color-destructive)]"
                  >
                    <XCircle className="mr-1 h-3.5 w-3.5" />
                    {isRevoking ? t("identity.users.revoking") : t("identity.users.revoke")}
                  </Button>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </EntityDetailSection>
  );
}
