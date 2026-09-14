import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Fingerprint, ShieldCheck, UserRound } from "lucide-react";
import { toast } from "sonner";
import { getMyProfile, setProfileImage } from "@/api/users";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { ErrorBand, LoadingRow, SettingsSection, SettingsField } from "@/components/list";
import { ImageInput } from "@/components/file/image-input";
import { useT } from "@/i18n/locale-provider";
import { ApiRequestError } from "@/lib/api-client";

/**
 * ProfileSettings — read-only view of identity fields (server doesn't expose
 * an /update-me endpoint for these yet) plus avatar upload via the presigned
 * ImageInput flow. Username, email, and name are intentionally not editable
 * from here — they require admin involvement, which is correct for a
 * multi-tenant operator console.
 *
 * Avatar fix: uses ImageInput + presigned upload (durable URL via Files module)
 * instead of the old base64 data: URL approach that hit the 2048-char limit.
 */
export function ProfileSettings() {
  const t = useT();
  const queryClient = useQueryClient();
  const profile = useQuery({ queryKey: ["identity", "profile"], queryFn: getMyProfile });

  const imageMutation = useMutation({
    mutationFn: (url: string | null) => setProfileImage(url),
    onSuccess: () => {
      toast.success(t("settings.imageUpdated"));
      void queryClient.invalidateQueries({ queryKey: ["identity", "profile"] });
    },
    onError: (err: unknown) => {
      const message =
        err instanceof ApiRequestError
          ? (err.problem?.detail ?? err.problem?.title ?? err.message)
          : t("settings.imageUpdateFailed");
      toast.error(message);
    },
  });

  if (profile.isLoading) return <LoadingRow label={t("settings.loadProfile")} />;
  if (profile.isError) {
    return (
      <ErrorBand
        message={
          profile.error instanceof ApiRequestError
            ? (profile.error.problem?.detail ?? profile.error.message)
            : t("settings.loadProfileFailed")
        }
      />
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
        <ImageInput
          value={user.imageUrl ?? ""}
          onChange={(next) => imageMutation.mutate(next.length > 0 ? next : null)}
          ownerType="User"
          ownerId={user.id ?? null}
          shape="circle"
        />
      </SettingsSection>

      {/* Identity — read-only; admin must update these server-side */}
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
          <SettingsField id="profile-phone" label={t("settings.phone")}>
            <Input
              id="profile-phone"
              value={user.phoneNumber ?? "—"}
              readOnly
              className="font-mono bg-[var(--color-muted)] cursor-not-allowed"
            />
          </SettingsField>
        </div>
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

