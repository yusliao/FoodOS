import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Lock,
  Search,
  ShieldCheck,
  Star,
  Trash2,
  UserMinus,
  UserPlus,
  Users as UsersIcon,
  X,
} from "lucide-react";
import {
  addUsersToGroup,
  deleteGroup,
  getGroupById,
  getGroupMembers,
  IDENTITY_PERMISSIONS,
  listRoles,
  removeUserFromGroup,
  searchUsers,
  updateGroup,
  type GroupMemberDto,
  type RoleDto,
  type UserDto,
} from "@/api/identity";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Avatar } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogBody,
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
  EntityDetailSection,
  EntityDetailStat,
  ErrorBand,
  Field,
} from "@/components/list";
import { describe, pad2 } from "@/lib/list-helpers";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";

function memberDisplay(m: GroupMemberDto, unnamed: string): string {
  const parts = [m.firstName, m.lastName].filter(Boolean);
  if (parts.length > 0) return parts.join(" ");
  return m.userName ?? m.email ?? unnamed;
}

function userDisplay(u: UserDto, unnamed: string): string {
  const parts = [u.firstName, u.lastName].filter(Boolean);
  if (parts.length > 0) return parts.join(" ");
  return u.userName ?? u.email ?? unnamed;
}

export function GroupDetailPage() {
  const t = useT();
  const { groupId = "" } = useParams<{ groupId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const canUpdate = permissions.includes(IDENTITY_PERMISSIONS.groups.update);
  const canDelete = permissions.includes(IDENTITY_PERMISSIONS.groups.delete);
  const canManageMembers = permissions.includes(IDENTITY_PERMISSIONS.groups.manageMembers);
  const canViewUsers = permissions.includes(IDENTITY_PERMISSIONS.users.view);
  const canViewRoles = permissions.includes(IDENTITY_PERMISSIONS.roles.view);

  const groupQuery = useQuery({
    queryKey: ["identity", "groups", groupId],
    queryFn: () => getGroupById(groupId),
    enabled: !!groupId,
  });

  const membersQuery = useQuery({
    queryKey: ["identity", "groups", groupId, "members"],
    queryFn: () => getGroupMembers(groupId),
    enabled: !!groupId,
  });

  const rolesQuery = useQuery({
    queryKey: ["identity", "roles"],
    queryFn: listRoles,
    staleTime: 60_000,
    enabled: canViewRoles,
  });

  const group = groupQuery.data;
  const members = membersQuery.data ?? [];
  const roles = rolesQuery.data ?? [];

  // Metadata + role edit state
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isDefault, setIsDefault] = useState(false);
  const [selectedRoleIds, setSelectedRoleIds] = useState<Set<string>>(new Set());
  const [initialRoleIds, setInitialRoleIds] = useState<Set<string>>(new Set());
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [addOpen, setAddOpen] = useState(false);

  useEffect(() => {
    if (!group) return;
    setName(group.name);
    setDescription(group.description ?? "");
    setIsDefault(group.isDefault);
    const next = new Set(group.roleIds ?? []);
    setSelectedRoleIds(next);
    setInitialRoleIds(new Set(next));
  }, [group]);

  const dirtyMeta = useMemo(() => {
    if (!group) return false;
    return (
      name.trim() !== group.name ||
      (description ?? "") !== (group.description ?? "") ||
      isDefault !== group.isDefault
    );
  }, [group, name, description, isDefault]);

  const dirtyRoles = useMemo(() => {
    if (selectedRoleIds.size !== initialRoleIds.size) return true;
    for (const id of selectedRoleIds) if (!initialRoleIds.has(id)) return true;
    return false;
  }, [selectedRoleIds, initialRoleIds]);

  const isDirty = dirtyMeta || dirtyRoles;

  const toggleRole = (roleId: string) => {
    setSelectedRoleIds((prev) => {
      const next = new Set(prev);
      if (next.has(roleId)) next.delete(roleId);
      else next.add(roleId);
      return next;
    });
  };

  const save = useMutation({
    mutationFn: () =>
      updateGroup(groupId, {
        name: name.trim(),
        description: description.trim() || undefined,
        isDefault,
        roleIds: Array.from(selectedRoleIds),
      }),
    onSuccess: () => {
      toast.success(t("identity.groups.updated"));
      void queryClient.invalidateQueries({ queryKey: ["identity", "groups"] });
      void queryClient.invalidateQueries({ queryKey: ["identity", "groups", groupId] });
    },
    onError: (err) => toast.error(t("identity.updateFailed"), { description: describe(err) }),
  });

  const remove = useMutation({
    mutationFn: () => deleteGroup(groupId),
    onSuccess: () => {
      toast.success(t("identity.groups.deleted"));
      void queryClient.invalidateQueries({ queryKey: ["identity", "groups"] });
      navigate("/identity/groups");
    },
    onError: (err) => {
      toast.error(t("identity.deleteFailed"), { description: describe(err) });
      setConfirmDelete(false);
    },
  });

  const removeMember = useMutation({
    mutationFn: (userId: string) => removeUserFromGroup(groupId, userId),
    onSuccess: () => {
      toast.success(t("identity.groups.memberRemoved"));
      void queryClient.invalidateQueries({
        queryKey: ["identity", "groups", groupId, "members"],
      });
      void queryClient.invalidateQueries({ queryKey: ["identity", "groups", groupId] });
    },
    onError: (err) => toast.error(t("identity.groups.removeFailed"), { description: describe(err) }),
  });

  const reset = () => {
    if (!group) return;
    setName(group.name);
    setDescription(group.description ?? "");
    setIsDefault(group.isDefault);
    setSelectedRoleIds(new Set(group.roleIds ?? []));
  };

  if (groupQuery.isLoading) {
    return (
      <div className="space-y-6">
        <EntityDetailBack to="/identity/groups" label={t("identity.groups.back")} />
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    );
  }

  if (groupQuery.isError || !group) {
    return (
      <div className="space-y-4">
        <EntityDetailBack to="/identity/groups" label={t("identity.groups.back")} />
        <ErrorBand message={groupQuery.error ? describe(groupQuery.error) : t("identity.groups.notFound")} />
      </div>
    );
  }

  return (
    <div className="space-y-5 pb-12">
      <EntityDetailBack to="/identity/groups" label={t("identity.groups.back")} />

      <EntityDetailHero
        avatar={<EntityDetailAvatar name={group.name} icon={UsersIcon} />}
        title={group.name}
        badges={
          <>
            {group.isDefault && (
              <Badge variant="brand">
                <Star className="h-3 w-3" /> {t("identity.default")}
              </Badge>
            )}
            {group.isSystemGroup && (
              <Badge variant="outline">
                <Lock className="h-3 w-3" /> {t("identity.system")}
              </Badge>
            )}
          </>
        }
        subtitle={group.description || t("identity.groups.cohort")}
        actions={
          canDelete && !group.isSystemGroup ? (
            <Button variant="destructive" size="sm" onClick={() => setConfirmDelete(true)}>
              <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("identity.groups.deleteGroup")}
            </Button>
          ) : undefined
        }
        stats={
          <>
            <EntityDetailStat
              icon={UsersIcon}
              value={group.memberCount}
              label={group.memberCount === 1 ? t("identity.groups.memberUnit") : t("identity.groups.membersUnit")}
              tone="primary"
            />
            <EntityDetailStat
              icon={ShieldCheck}
              value={group.roleNames?.length ?? 0}
              label={(group.roleNames?.length ?? 0) === 1 ? t("identity.groups.roleUnit") : t("identity.groups.rolesUnit")}
            />
          </>
        }
      />

      <div className="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        {/* Metadata + roles */}
        <EntityDetailSection
          title={t("identity.groups.details")}
          icon={UsersIcon}
          description={t("identity.groups.detailsDesc")}
          footer={canUpdate ? (
            <div className="flex items-center justify-end gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={reset}
                disabled={!isDirty || save.isPending}
              >
                {t("identity.discard")}
              </Button>
              <Button
                size="sm"
                onClick={() => save.mutate()}
                disabled={!isDirty || save.isPending}
              >
                {save.isPending ? t("identity.saving") : t("identity.saveChanges")}
              </Button>
            </div>
          ) : undefined}
        >
          <div className="space-y-4">
            <Field id="g-name" label={t("identity.groups.name")} required>
              <Input
                id="g-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={group.isSystemGroup || !canUpdate}
                maxLength={128}
              />
            </Field>
            <Field id="g-desc" label={t("identity.groups.descriptionLabel")}>
              <Input
                id="g-desc"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                disabled={!canUpdate}
                placeholder={t("identity.groups.descPlaceholder")}
                maxLength={512}
              />
            </Field>
            <div className="flex items-center justify-between gap-3 rounded-md border border-[var(--color-border)] bg-[var(--color-muted)] px-3 py-2.5">
              <div className="min-w-0">
                <span className="block text-sm font-medium tracking-tight">{t("identity.groups.defaultGroup")}</span>
                <span className="mt-0.5 block text-[12px] text-[var(--color-muted-foreground)]">
                  {t("identity.groups.defaultAssign")}
                </span>
              </div>
              <Switch
                checked={isDefault}
                onCheckedChange={setIsDefault}
                disabled={!canUpdate}
                aria-label={t("identity.groups.defaultGroup")}
              />
            </div>

            {/* Roles attached */}
            {canViewRoles && <div className="pt-2">
              <div className="mb-2 flex items-center justify-between">
                <span className="text-[11.5px] font-medium text-[var(--color-muted-foreground)]">
                  {t("identity.groups.rolesAttached")}
                </span>
                <span className="text-[11px] text-[var(--color-muted-foreground)]">
                  {pad2(selectedRoleIds.size)} / {pad2(roles.length)}
                </span>
              </div>
              {rolesQuery.isLoading ? (
                <Skeleton className="h-20 w-full rounded-md" />
              ) : roles.length === 0 ? (
                <p className="text-sm text-[var(--color-muted-foreground)]">
                  {t("identity.groups.noRoles")}{" "}
                  <Link
                    to="/identity/roles"
                    className="underline hover:text-[var(--color-foreground)]"
                  >
                    {t("identity.groups.createOne")}
                  </Link>{" "}
                  {t("identity.groups.first")}
                </p>
              ) : (
                <ul className="grid gap-1.5">
                  {roles.map((role) => (
                    <RoleToggleRow
                      key={role.id}
                      role={role}
                      selected={selectedRoleIds.has(role.id)}
                      onToggle={() => toggleRole(role.id)}
                      disabled={!canUpdate}
                    />
                  ))}
                </ul>
              )}
            </div>}
          </div>
        </EntityDetailSection>

        {/* Members */}
        <EntityDetailSection
          title={t("identity.groups.members")}
          icon={UsersIcon}
          description={t("identity.groups.membersDesc")}
          action={canManageMembers && canViewUsers ? (
            <Button size="sm" onClick={() => setAddOpen(true)} className="gap-1.5">
              <UserPlus className="h-3.5 w-3.5" /> {t("identity.groups.addMembers")}
            </Button>
          ) : undefined}
          padded={false}
        >
          {membersQuery.isLoading ? (
            <div className="space-y-2 p-5">
              <Skeleton className="h-12 w-full rounded-md" />
              <Skeleton className="h-12 w-full rounded-md" />
              <Skeleton className="h-12 w-full rounded-md" />
            </div>
          ) : membersQuery.isError ? (
            <div className="p-5">
              <ErrorBand message={describe(membersQuery.error)} />
            </div>
          ) : members.length === 0 ? (
            <div className="p-5 text-sm text-[var(--color-muted-foreground)]">
              {t("identity.groups.noMembers")}
            </div>
          ) : (
            <ul>
              {members.map((member) => {
                const summary = (
                  <>
                    <Avatar name={memberDisplay(member, t("identity.unknownUser"))} size="sm" />
                    <div className="min-w-0">
                      <div className="truncate text-sm font-medium tracking-tight">
                        {memberDisplay(member, t("identity.unknownUser"))}
                      </div>
                      {member.email && (
                        <div className="truncate text-[12px] text-[var(--color-muted-foreground)]">
                          {member.email}
                        </div>
                      )}
                    </div>
                  </>
                );
                return (
                  <li
                    key={member.userId}
                    className="flex items-center justify-between gap-3 border-b border-[var(--color-border)] px-5 py-3 last:border-b-0 transition-colors hover:bg-[var(--color-accent)]"
                  >
                    {canViewUsers ? (
                      <Link
                        to={`/identity/users/${member.userId}`}
                        className="flex min-w-0 flex-1 items-center gap-3"
                      >
                        {summary}
                      </Link>
                    ) : (
                      <div className="flex min-w-0 flex-1 items-center gap-3">{summary}</div>
                    )}
                    {canManageMembers && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => removeMember.mutate(member.userId)}
                        disabled={removeMember.isPending}
                        className="shrink-0 text-[var(--color-muted-foreground)] hover:text-[var(--color-destructive)]"
                      >
                        <UserMinus className="mr-1 h-3.5 w-3.5" /> {t("identity.groups.remove")}
                      </Button>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </EntityDetailSection>
      </div>

      {/* Delete dialog */}
      {canDelete && <Dialog open={confirmDelete} onOpenChange={(o) => (!o ? setConfirmDelete(false) : undefined)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("identity.groups.deleteTitle")}</DialogTitle>
            <DialogDescription>
              {t("identity.groups.deleteBody").replace("{name}", group.name)}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" disabled={remove.isPending}>
                {t("chrome.cancel")}
              </Button>
            </DialogClose>
            <Button
              variant="destructive"
              onClick={() => remove.mutate()}
              disabled={remove.isPending}
            >
              {remove.isPending ? t("identity.deleting") : t("identity.groups.deleteGroup")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>}

      {canManageMembers && canViewUsers && <AddMembersDialog
        open={addOpen}
        groupId={groupId}
        existingMemberIds={new Set(members.map((m) => m.userId))}
        onClose={() => setAddOpen(false)}
      />}
    </div>
  );
}

function RoleToggleRow({
  role,
  selected,
  onToggle,
  disabled,
}: {
  role: RoleDto;
  selected: boolean;
  onToggle: () => void;
  disabled: boolean;
}) {
  const t = useT();
  return (
    <li
      className={cn(
        "flex items-center justify-between gap-3 rounded-md border px-3 py-2",
        "transition-colors duration-[var(--duration-fast)]",
        selected
          ? "border-[oklch(from_var(--color-primary)_l_c_h_/_0.30)] bg-[var(--color-primary-soft)]"
          : "border-[var(--color-border)] bg-[var(--color-card)] hover:border-[var(--color-border-strong)]",
      )}
    >
      <div className="flex min-w-0 items-center gap-2.5">
        <ShieldCheck
          className={cn(
            "h-4 w-4 shrink-0",
            selected ? "text-[var(--color-primary)]" : "text-[var(--color-muted-foreground)]",
          )}
        />
        <div className="min-w-0">
          <div className="truncate text-sm font-medium tracking-tight">{role.name}</div>
          {role.description && (
            <div className="truncate text-[11.5px] text-[var(--color-muted-foreground)]">
              {role.description}
            </div>
          )}
        </div>
      </div>
      <Switch
        checked={selected}
        onCheckedChange={onToggle}
        disabled={disabled}
        aria-label={t("identity.groups.attachRole").replace("{name}", role.name)}
      />
    </li>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Add members dialog
// ───────────────────────────────────────────────────────────────────────

function AddMembersDialog({
  open,
  groupId,
  existingMemberIds,
  onClose,
}: {
  open: boolean;
  groupId: string;
  existingMemberIds: Set<string>;
  onClose: () => void;
}) {
  const t = useT();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debounced, setDebounced] = useState("");
  const [picked, setPicked] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!open) {
      setSearch("");
      setDebounced("");
      setPicked(new Set());
    }
  }, [open]);

  useEffect(() => {
    const t = setTimeout(() => setDebounced(search.trim()), 250);
    return () => clearTimeout(t);
  }, [search]);

  const usersQuery = useQuery({
    queryKey: ["identity", "users", "search", { search: debounced }],
    queryFn: () =>
      searchUsers({
        pageNumber: 1,
        pageSize: 20,
        search: debounced || undefined,
        isActive: true,
      }),
    enabled: open,
    placeholderData: keepPreviousData,
  });

  const candidates = (usersQuery.data?.items ?? []).filter(
    (u): u is UserDto & { id: string } => !!u.id,
  );

  const togglePick = (userId: string) => {
    setPicked((prev) => {
      const next = new Set(prev);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
  };

  const add = useMutation({
    mutationFn: () => addUsersToGroup(groupId, Array.from(picked)),
    onSuccess: (data) => {
      const dupes = data.alreadyMemberUserIds.length;
      const added = data.addedCount;
      toast.success(
        (added === 1
          ? t("identity.groups.addedMemberOne")
          : t("identity.groups.addedMembers").replace("{n}", String(added))) +
          (dupes > 0 ? t("identity.groups.alreadyPresent").replace("{n}", String(dupes)) : ""),
      );
      void queryClient.invalidateQueries({
        queryKey: ["identity", "groups", groupId, "members"],
      });
      void queryClient.invalidateQueries({ queryKey: ["identity", "groups", groupId] });
      onClose();
    },
    onError: (err) => toast.error(t("identity.groups.addFailed"), { description: describe(err) }),
  });

  return (
    <Dialog open={open} onOpenChange={(o) => (!o ? onClose() : undefined)}>
      <DialogContent className="!max-w-lg">
        <DialogHeader>
          <DialogTitle>{t("identity.groups.pickTitle")}</DialogTitle>
          <DialogDescription>
            {t("identity.groups.pickDesc")}
          </DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-3">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-[var(--color-muted-foreground)]" />
            <Input
              autoFocus
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("identity.groups.searchUsers")}
              className="pl-9"
            />
            {search && (
              <button
                type="button"
                onClick={() => setSearch("")}
                aria-label={t("identity.clearSearch")}
                className="absolute right-2 top-2 grid h-5 w-5 place-items-center rounded text-[var(--color-muted-foreground)] hover:bg-[var(--color-muted)]"
              >
                <X className="h-3 w-3" />
              </button>
            )}
          </div>
          <div className="max-h-[320px] overflow-y-auto rounded-md border border-[var(--color-border)]">
            {usersQuery.isLoading ? (
              <div className="space-y-2 p-3">
                <Skeleton className="h-10 w-full rounded-md" />
                <Skeleton className="h-10 w-full rounded-md" />
                <Skeleton className="h-10 w-full rounded-md" />
              </div>
            ) : candidates.length === 0 ? (
              <div className="p-6 text-center text-sm text-[var(--color-muted-foreground)]">
                {debounced ? t("identity.groups.noMatch").replace("{q}", debounced) : t("identity.groups.noUsers")}
              </div>
            ) : (
              <ul>
                {candidates.map((user) => {
                  const already = existingMemberIds.has(user.id);
                  const isPicked = picked.has(user.id);
                  return (
                    <li key={user.id}>
                      <button
                        type="button"
                        onClick={() => !already && togglePick(user.id)}
                        disabled={already}
                        className={cn(
                          "flex w-full items-center gap-3 border-b border-[var(--color-border)] px-3 py-2 text-left last:border-b-0",
                          "transition-colors",
                          already
                            ? "opacity-50"
                            : isPicked
                              ? "bg-[var(--color-primary-soft)]"
                              : "hover:bg-[var(--color-accent)]",
                        )}
                      >
                        <span
                          className={cn(
                            "grid h-4 w-4 shrink-0 place-items-center rounded border",
                            isPicked
                              ? "border-[var(--color-primary)] bg-[var(--color-primary)] text-[var(--color-primary-foreground)]"
                              : "border-[var(--color-input)]",
                          )}
                        >
                          {isPicked && <span className="text-[10px] leading-none">✓</span>}
                        </span>
                        <Avatar name={userDisplay(user, t("identity.unknownUser"))} size="sm" />
                        <div className="min-w-0 flex-1">
                          <div className="truncate text-sm font-medium">{userDisplay(user, t("identity.unknownUser"))}</div>
                          <div className="truncate text-[11.5px] text-[var(--color-muted-foreground)]">
                            {user.email ?? user.userName}
                          </div>
                        </div>
                        {already && (
                          <span className="text-[10px] uppercase tracking-[0.14em] text-[var(--color-muted-foreground)]">
                            {t("identity.groups.alreadyIn")}
                          </span>
                        )}
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
          <div className="text-[12px] text-[var(--color-muted-foreground)]">
            {t("identity.groups.selected").replace("{n}", String(picked.size))}
          </div>
        </DialogBody>
        <DialogFooter>
          <DialogClose asChild>
            <Button type="button" variant="outline" disabled={add.isPending}>
              {t("chrome.cancel")}
            </Button>
          </DialogClose>
          <Button
            onClick={() => add.mutate()}
            disabled={picked.size === 0 || add.isPending}
            className="gap-1.5"
          >
            <UserPlus className="h-4 w-4" />
            {add.isPending ? t("identity.groups.adding") : t("identity.groups.addCount").replace("{n}", String(picked.size))}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
