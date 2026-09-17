import { useState } from "react";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Fingerprint, ShieldCheck, UserRound } from "lucide-react";
import { toast } from "sonner";
import { getMyProfile, setProfileImage, updateMyProfile, type UpdateMyProfileInput, type UserDto } from "@/api/users";
import { useAuth } from "@/auth/use-auth";
import { FilesPermissions } from "@/lib/permissions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { ErrorBand, LoadingRow, SettingsSection, SettingsField } from "@/components/list";
import { ImageInput } from "@/components/file/image-input";
import { useT } from "@/i18n/locale-provider";
import { ApiRequestError } from "@/lib/api-client";

/**
 * Self-service text fields and avatar use separate current-user endpoints.
 * Email and username are not persisted by the text update handler.
 */
export function ProfileSettings() {
  const t = useT();
  const profile = useQuery({ queryKey: ["identity", "profile"], queryFn: getMyProfile });

  if (profile.isLoading) return <LoadingRow label={t("settings.loadProfile")} />;
  if (profile.isError) {
    return (
      <div className="space-y-3"><ErrorBand
        message={
          profile.error instanceof ApiRequestError
            ? (profile.error.problem?.detail ?? profile.error.message)
            : t("settings.loadProfileFailed")
        }
      /><Button variant="outline" disabled={profile.isFetching} onClick={() => void profile.refetch()}>{t("workbench.retry")}</Button></div>
    );
  }

  const user = profile.data!;
  const displayName =
    [user.firstName, user.lastName].filter(Boolean).join(" ").trim() ||
    user.userName ||
    user.email ||
    t("settings.accountFallback");

  return (
    <div className="space-y-5 fsh-enter">
      {/* Avatar — presigned upload via ImageInput, no base64 data: URLs */}
      <SettingsSection
        title={t("settings.avatar")}
        icon={UserRound}
        description={t("settings.avatarDesc")}
      >
        <AvatarEditor key={user.id} profile={user} />
      </SettingsSection>

      {/* Account identifiers remain read-only. */}
      <SettingsSection
        title={t("settings.identity")}
        icon={Fingerprint}
        description={t("settings.identityDesc")}
      >
        <div className="grid gap-5 sm:grid-cols-2">
          <SettingsField id="profile-username" label={t("settings.username")}>
            <Input
              id="profile-username"
              value={user.userName ?? ""}
              readOnly
              className="font-mono bg-[var(--color-muted)] cursor-not-allowed"
            />
          </SettingsField>
          <SettingsField id="profile-display" label={t("settings.displayName")}>
            <Input
              id="profile-display"
              value={displayName}
              readOnly
              className="bg-[var(--color-muted)] cursor-not-allowed"
            />
          </SettingsField>
          <SettingsField id="profile-email" label={t("auth.email")}>
            <Input
              id="profile-email"
              type="email"
              value={user.email ?? ""}
              readOnly
              className="font-mono bg-[var(--color-muted)] cursor-not-allowed"
            />
            {user.emailConfirmed !== undefined && (
              <p className="mt-1 text-[11px] text-[var(--color-muted-foreground)]">
                {user.emailConfirmed ? t("settings.emailVerified") : t("settings.emailUnverified")}
              </p>
            )}
          </SettingsField>
        </div>
        <IdentityEditor key={user.id} profile={user} />
      </SettingsSection>

      {/* Status badges */}
      <SettingsSection
        title={t("settings.accountStatus")}
        icon={ShieldCheck}
        description={t("settings.accountStatusDesc")}
      >
        <div className="flex flex-wrap items-center gap-2">
          <Badge
            variant={user.isActive ? "success" : "muted"}
            className="font-mono uppercase tracking-[0.14em]"
          >
            {user.isActive ? t("chrome.active") : t("settings.disabled")}
          </Badge>
          <Badge
            variant={user.emailConfirmed ? "info" : "warning"}
            className="font-mono uppercase tracking-[0.14em]"
          >
            {user.emailConfirmed ? t("settings.emailConfirmed") : t("settings.emailPending")}
          </Badge>
          <Badge
            variant={user.twoFactorEnabled ? "success" : "outline"}
            className="font-mono uppercase tracking-[0.14em]"
          >
            {user.twoFactorEnabled ? t("settings.twoFaOn") : t("settings.twoFaOff")}
          </Badge>
        </div>
      </SettingsSection>
    </div>
  );
}

function AvatarEditor({ profile }: { profile: UserDto }) {
  const t = useT();
  const { user, permissionsHydrated } = useAuth();
  const queryClient = useQueryClient();
  const writing = useIsMutating({ mutationKey: ["identity", "profile", "write"] }) > 0;
  const canUpload = permissionsHydrated && !!user?.permissions.includes(FilesPermissions.Upload);
  const [draft, setDraft] = useState<string | undefined>();
  const [uploading, setUploading] = useState(false);
  const value = draft ?? profile.imageUrl ?? "";
  const dirty = value !== (profile.imageUrl ?? "");
  const imageMutation = useMutation({
    mutationKey: ["identity", "profile", "write"],
    mutationFn: (url: string | null) => setProfileImage(url),
    onSuccess: async (_result, imageUrl) => {
      queryClient.setQueryData<UserDto>(["identity", "profile"], old => old ? { ...old, imageUrl } : old);
      setDraft(undefined);
      toast.success(t("settings.imageUpdated"));
      await queryClient.invalidateQueries({ queryKey: ["identity", "profile"] });
    },
    onError: (err: unknown) => toast.error(err instanceof ApiRequestError
      ? (err.problem?.detail ?? err.problem?.title ?? err.message)
      : t("settings.imageUpdateFailed")),
  });
  return <div className="space-y-3">
    <fieldset disabled={writing} className="min-w-0">
      <ImageInput value={value} onChange={setDraft} ownerType="User" ownerId={profile.id}
        shape="circle" canUpload={canUpload} onBusyChange={setUploading} />
    </fieldset>
    <p className="text-xs text-[var(--color-muted-foreground)]">{t("settings.avatarSaveHint")}</p>
    <Button disabled={!dirty || uploading || writing} onClick={() => {
      if (user && dirty && !uploading && !writing) imageMutation.mutate(value || null);
    }}>{t(imageMutation.isPending ? "settings.avatarSaving" : "settings.saveAvatar")}</Button>
  </div>;
}

function IdentityEditor({ profile }: { profile: UserDto }) {
  const t = useT();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const writing = useIsMutating({ mutationKey: ["identity", "profile", "write"] }) > 0;
  const [draft, setDraft] = useState<UpdateMyProfileInput>();
  const saved = {
    firstName: profile.firstName ?? "",
    lastName: profile.lastName ?? "",
    phoneNumber: profile.phoneNumber ?? "",
  };
  const value = draft ?? saved;
  const dirty = Object.keys(saved).some(key => value[key as keyof UpdateMyProfileInput] !== saved[key as keyof UpdateMyProfileInput]);
  const mutation = useMutation({
    mutationKey: ["identity", "profile", "write"],
    mutationFn: updateMyProfile,
    onSuccess: async (_result, input) => {
      queryClient.setQueryData<UserDto>(["identity", "profile"], old => old ? { ...old, ...input } : old);
      setDraft(undefined);
      toast.success(t("settings.profileUpdated"));
      await queryClient.invalidateQueries({ queryKey: ["identity", "profile"] });
    },
    onError: (err: unknown) => toast.error(err instanceof ApiRequestError
      ? (err.problem?.detail ?? err.problem?.title ?? err.message)
      : t("settings.profileUpdateFailed")),
  });

  return (
    <form className="mt-5" onSubmit={event => {
      event.preventDefault();
      if (user && dirty && !writing) mutation.mutate({ ...value });
    }}>
      <fieldset disabled={writing} className="min-w-0 space-y-4">
        <div className="grid gap-5 sm:grid-cols-2">
          {(["firstName", "lastName", "phoneNumber"] as const).map(field => (
            <SettingsField key={field} id={`profile-${field}`} label={t(`settings.${field}`)}>
              <Input
                id={`profile-${field}`}
                type={field === "phoneNumber" ? "tel" : "text"}
                autoComplete={field === "firstName" ? "given-name" : field === "lastName" ? "family-name" : "tel"}
                maxLength={field === "phoneNumber" ? 15 : 50}
                value={value[field]}
                onChange={event => setDraft({ ...value, [field]: event.target.value })}
              />
            </SettingsField>
          ))}
        </div>
        <Button type="submit" disabled={!dirty || writing}>
          {t(mutation.isPending ? "settings.profileSaving" : "settings.saveProfile")}
        </Button>
      </fieldset>
    </form>
  );
}

