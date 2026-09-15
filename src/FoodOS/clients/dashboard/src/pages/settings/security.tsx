import { useEffect, useMemo, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  AlertCircle,
  Check,
  ClipboardCheck,
  Copy,
  Eye,
  EyeOff,
  KeyRound,
  LogOut,
  MonitorSmartphone,
  ShieldCheck,
  ShieldOff,
  Smartphone,
} from "lucide-react";
import QRCode from "qrcode";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  changePassword,
  disableTwoFactor,
  enrollTwoFactor,
  getMyProfile,
  verifyEnrollTwoFactor,
  type TwoFactorEnrollmentResponse,
} from "@/api/identity";
import {
  getMySessions,
  revokeAllOtherSessions,
  revokeSession,
  type UserSessionDto,
} from "@/api/sessions";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { useLocale, useT } from "@/i18n/locale-provider";

const PROFILE_KEY = ["identity", "me"] as const;

type Translate = (key: string, fallback?: string) => string;

function formatTimestamp(iso: string | null | undefined, culture: string) {
  if (!iso) return "—";
  return new Intl.DateTimeFormat(culture, {
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(iso));
}

function describeDevice(s: UserSessionDto, t: Translate): string {
  const browser = s.browser ?? t("settings.unknownBrowser");
  const version = s.browserVersion ? ` ${s.browserVersion}` : "";
  const os = s.operatingSystem ?? t("settings.unknownOs");
  return t("settings.deviceLine")
    .replace("{browser}", `${browser}${version}`)
    .replace("{os}", os);
}

function deviceIcon(s: UserSessionDto) {
  const isMobile = (s.deviceType ?? "").toLowerCase().includes("mobile");
  return isMobile ? Smartphone : MonitorSmartphone;
}

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof ApiRequestError) {
    return err.problem?.detail ?? err.problem?.title ?? err.message;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

// ─────────────────────────────────────────────────────────────────────────
// Page
// ─────────────────────────────────────────────────────────────────────────

export function SecuritySettings() {
  const t = useT();
  const { culture } = useLocale();
  const queryClient = useQueryClient();

  const profileQuery = useQuery({ queryKey: PROFILE_KEY, queryFn: getMyProfile });
  const twoFactorEnabled = profileQuery.data?.twoFactorEnabled ?? false;

  const sessionsQuery = useQuery({
    queryKey: ["identity", "sessions", "me"],
    queryFn: getMySessions,
    staleTime: 30_000,
  });

  const sessions = useMemo(() => {
    const data = sessionsQuery.data ?? [];
    return [...data].sort((a, b) => {
      if (a.isCurrentSession && !b.isCurrentSession) return -1;
      if (!a.isCurrentSession && b.isCurrentSession) return 1;
      if (a.isActive && !b.isActive) return -1;
      if (!a.isActive && b.isActive) return 1;
      return new Date(b.lastActivityAt).getTime() - new Date(a.lastActivityAt).getTime();
    });
  }, [sessionsQuery.data]);

  const revokeOne = useMutation({
    mutationFn: (id: string) => revokeSession(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["identity", "sessions", "me"] });
      toast.success(t("settings.sessionRevoked"));
    },
    onError: (err) =>
      toast.error(apiErrorMessage(err, t("settings.revokeFailed"))),
  });

  const revokeAll = useMutation({
    mutationFn: () => revokeAllOtherSessions(),
    onSuccess: (data) => {
      void queryClient.invalidateQueries({ queryKey: ["identity", "sessions", "me"] });
      toast.success(
        (data.revokedCount === 1 ? t("settings.revokedCountOne") : t("settings.revokedCountMany")).replace(
          "{n}",
          String(data.revokedCount),
        ),
      );
    },
    onError: (err) =>
      toast.error(apiErrorMessage(err, t("settings.revokeAllFailed"))),
  });

  const otherActiveCount = useMemo(
    () => sessions.filter((s) => s.isActive && !s.isCurrentSession).length,
    [sessions],
  );

  const sessionsError =
    sessionsQuery.error instanceof ApiRequestError
      ? sessionsQuery.error.problem?.detail ?? sessionsQuery.error.message
      : sessionsQuery.error
        ? t("settings.loadSessionsFailed")
        : null;

  return (
    <div className="space-y-6 fsh-enter">
      <PasswordCard />
      <TwoFactorCard enabled={twoFactorEnabled} loading={profileQuery.isLoading} />

      {/* Active sessions — already wired to the backend. */}
      <Card>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle className="flex items-center gap-2">
                {t("settings.activeSessions")}
                {!sessionsQuery.isLoading && (
                  <Badge variant="default">
                    {t("settings.activeCount").replace("{n}", String(sessions.filter((s) => s.isActive).length))}
                  </Badge>
                )}
              </CardTitle>
              <CardDescription>
                {t("settings.activeSessionsDesc")}
              </CardDescription>
            </div>
            <Button
              variant="outline"
              size="sm"
              disabled={otherActiveCount === 0 || revokeAll.isPending}
              onClick={() => revokeAll.mutate()}
            >
              <LogOut className="mr-1.5 h-3.5 w-3.5" />
              {t("settings.signOutEverywhere")}
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {sessionsError && (
            <div className="border-t border-[var(--color-border)] px-6 py-4 text-sm text-[var(--color-destructive)] flex items-start gap-2">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{sessionsError}</span>
            </div>
          )}

          {sessionsQuery.isLoading ? (
            <SessionsSkeleton />
          ) : sessions.length === 0 ? (
            <div className="px-6 py-12 text-center">
              <p className="text-sm font-medium tracking-tight">{t("settings.noSessions")}</p>
              <p className="mt-1 text-xs text-[var(--color-muted-foreground)]">
                {t("settings.noSessionsBody")}
              </p>
            </div>
          ) : (
            <ul>
              {sessions.map((s, idx) => {
                const Icon = deviceIcon(s);
                const isRevoking =
                  revokeOne.isPending && revokeOne.variables === s.id;
                return (
                  <li
                    key={s.id}
                    className={cn(
                      "fsh-enter group/row flex items-center justify-between gap-4",
                      "border-t border-[var(--color-border)] px-6 py-4 first:border-t-0",
                      "transition-colors",
                      !s.isActive && "opacity-60",
                    )}
                    style={{ animationDelay: `${50 * idx}ms` }}
                  >
                    <div className="flex items-center gap-3">
                      <span
                        aria-hidden
                        className={cn(
                          "grid h-9 w-9 shrink-0 place-items-center rounded-full ring-1 ring-inset",
                          s.isCurrentSession
                            ? "bg-[var(--color-primary-soft)] text-[var(--color-primary)] ring-[var(--color-primary)]/30"
                            : "bg-[var(--color-muted)] text-[var(--color-muted-foreground)] ring-[var(--color-border)]",
                        )}
                      >
                        <Icon className="h-4 w-4" />
                      </span>
                      <div className="space-y-0.5">
                        <div className="flex flex-wrap items-center gap-2 text-sm font-medium tracking-tight">
                          {describeDevice(s, t)}
                          {s.isCurrentSession && <Badge variant="brand">{t("settings.thisDevice")}</Badge>}
                          {!s.isActive && <Badge variant="outline">{t("settings.revoked")}</Badge>}
                        </div>
                        <div className="font-mono text-[11px] text-[var(--color-muted-foreground)]">
                          {s.ipAddress ?? t("settings.unknownIp")} · {t("settings.lastActivity").replace("{time}", formatTimestamp(s.lastActivityAt, culture))}
                          {" · "}{t("settings.expires").replace("{time}", formatTimestamp(s.expiresAt, culture))}
                        </div>
                      </div>
                    </div>
                    {s.isActive && !s.isCurrentSession && (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={isRevoking}
                        onClick={() => revokeOne.mutate(s.id)}
                      >
                        <LogOut className="mr-1.5 h-3.5 w-3.5" />
                        {isRevoking ? t("settings.revoking") : t("settings.revoke")}
                      </Button>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────
// Password card — Dialog-driven change-password flow
// ─────────────────────────────────────────────────────────────────────────

function PasswordCard() {
  const t = useT();
  const [open, setOpen] = useState(false);

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>{t("settings.passwordTitle")}</CardTitle>
          <CardDescription>
            {t("settings.passwordDesc")}
          </CardDescription>
        </CardHeader>
        <CardContent className="flex items-center justify-between gap-4 px-6 pb-5 pt-1">
          <div className="text-sm text-[var(--color-muted-foreground)]">
            {t("settings.passwordHint")}
          </div>
          <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
            {t("settings.changePassword")}
          </Button>
        </CardContent>
      </Card>

      <ChangePasswordDialog open={open} onOpenChange={setOpen} />
    </>
  );
}

// Labelled password field with an inline show/hide toggle. Reusable across the
// security dialogs so every secret entry behaves identically.
function PasswordField({
  id,
  label,
  value,
  onChange,
  autoComplete,
  autoFocus,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  autoComplete: string;
  autoFocus?: boolean;
}) {
  const [show, setShow] = useState(false);
  const t = useT();
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <div className="relative">
        <Input
          id={id}
          type={show ? "text" : "password"}
          autoComplete={autoComplete}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          autoFocus={autoFocus}
          required
          className="pr-10"
        />
        <button
          type="button"
          tabIndex={-1}
          aria-label={show ? t("auth.hidePassword") : t("auth.showPassword")}
          onClick={() => setShow((s) => !s)}
          className="absolute right-1.5 top-1/2 grid size-6 -translate-y-1/2 place-items-center rounded-md text-[var(--color-muted-foreground)] outline-none transition-colors hover:text-[var(--color-foreground)] focus-visible:ring-2 focus-visible:ring-[oklch(from_var(--color-ring)_l_c_h_/_0.5)]"
        >
          {show ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
        </button>
      </div>
    </div>
  );
}

// 0–4 heuristic: length tiers + mixed case + digit/symbol. Drives the meter only;
// the server enforces the real policy.
function passwordStrength(pw: string, t: Translate): { score: number; label: string } {
  if (!pw) return { score: 0, label: "" };
  let s = 0;
  if (pw.length >= 8) s++;
  if (pw.length >= 12) s++;
  if (/[a-z]/.test(pw) && /[A-Z]/.test(pw)) s++;
  if (/\d/.test(pw) && /[^A-Za-z0-9]/.test(pw)) s++;
  s = Math.max(1, Math.min(4, s));
  return {
    score: s,
    label: ["", t("settings.weak"), t("settings.fair"), t("settings.good"), t("settings.strong")][s],
  };
}

function StrengthMeter({ password }: { password: string }) {
  const t = useT();
  if (!password) return null;
  const { score, label } = passwordStrength(password, t);
  const tone =
    score <= 1
      ? "var(--color-destructive)"
      : score < 4
        ? "var(--color-warning)"
        : "var(--color-success)";
  return (
    <div className="mt-2 flex items-center gap-2.5">
      <div className="flex flex-1 gap-1" aria-hidden>
        {[1, 2, 3, 4].map((i) => (
          <span
            key={i}
            className="h-1 flex-1 rounded-full transition-colors duration-[var(--duration-fast)]"
            style={{ backgroundColor: i <= score ? tone : "var(--color-muted)" }}
          />
        ))}
      </div>
      <span
        className="shrink-0 text-[11px] font-semibold tabular-nums"
        style={{ color: tone }}
      >
        {label}
      </span>
    </div>
  );
}

function ChangePasswordDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (next: boolean) => void;
}) {
  const t = useT();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [localError, setLocalError] = useState<string | null>(null);

  // Reset state every time the dialog opens so stale values don't bleed.
  useEffect(() => {
    if (open) {
      setCurrent("");
      setNext("");
      setConfirm("");
      setLocalError(null);
    }
  }, [open]);

  const mutation = useMutation({
    mutationFn: () =>
      changePassword({
        password: current,
        newPassword: next,
        confirmNewPassword: confirm,
      }),
    onSuccess: () => {
      toast.success(t("settings.passwordChanged"), {
        description: t("settings.passwordChangedBody"),
      });
      onOpenChange(false);
    },
    onError: (err) => setLocalError(apiErrorMessage(err, t("settings.changeFailed"))),
  });

  const onSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setLocalError(null);

    if (next.length < 8) {
      setLocalError(t("settings.useAtLeast8"));
      return;
    }
    if (next !== confirm) {
      setLocalError(t("settings.passwordsDontMatch"));
      return;
    }
    if (next === current) {
      setLocalError(t("settings.mustDiffer"));
      return;
    }
    mutation.mutate();
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-[oklch(from_var(--color-primary)_l_c_h_/_0.10)] ring-1 ring-inset ring-[oklch(from_var(--color-primary)_l_c_h_/_0.20)]">
              <KeyRound className="size-4 text-[var(--color-primary)]" />
            </span>
            <DialogTitle>{t("settings.changePassword")}</DialogTitle>
          </div>
          <DialogDescription>
            {t("settings.changePasswordDesc")}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} className="contents" noValidate>
          <DialogBody className="space-y-4">
            <PasswordField
              id="cp-current"
              label={t("settings.currentPassword")}
              value={current}
              onChange={setCurrent}
              autoComplete="current-password"
              autoFocus
            />

            <div>
              <PasswordField
                id="cp-next"
                label={t("settings.newPassword")}
                value={next}
                onChange={setNext}
                autoComplete="new-password"
              />
              <StrengthMeter password={next} />
            </div>

            <div>
              <PasswordField
                id="cp-confirm"
                label={t("settings.confirmNewPassword")}
                value={confirm}
                onChange={setConfirm}
                autoComplete="new-password"
              />
              {confirm.length > 0 &&
                (confirm === next ? (
                  <p className="mt-1.5 flex items-center gap-1 text-[11px] font-medium text-[var(--color-success)]">
                    <Check className="size-3.5" /> {t("settings.passwordsMatch")}
                  </p>
                ) : (
                  <p className="mt-1.5 text-[11px] text-[var(--color-muted-foreground)]">
                    {t("settings.doesntMatchYet")}
                  </p>
                ))}
            </div>

            {localError && (
              <div
                role="alert"
                className="flex items-start gap-2 rounded-lg border border-[oklch(from_var(--color-destructive)_l_c_h_/_0.40)] bg-[oklch(from_var(--color-destructive)_l_c_h_/_0.08)] px-3 py-2 text-sm text-[var(--color-destructive)]"
              >
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                <span className="leading-snug">{localError}</span>
              </div>
            )}
          </DialogBody>

          <DialogFooter>
            <Button
              type="button"
              variant="ghost"
              onClick={() => onOpenChange(false)}
              disabled={mutation.isPending}
            >
              {t("chrome.cancel")}
            </Button>
            <Button
              type="submit"
              disabled={mutation.isPending || !current || !next || !confirm}
            >
              <KeyRound className="mr-1 h-3.5 w-3.5" />
              {mutation.isPending ? t("settings.updating") : t("settings.updatePassword")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────────────────────
// Two-factor card — real enroll/verify/disable flow
// ─────────────────────────────────────────────────────────────────────────

function TwoFactorCard({ enabled, loading }: { enabled: boolean; loading: boolean }) {
  const t = useT();
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          {t("settings.twoFa")}
          {loading ? (
            <Skeleton className="h-5 w-16 rounded-full" />
          ) : enabled ? (
            <Badge variant="success">{t("settings.enabled")}</Badge>
          ) : (
            <Badge variant="warning">{t("settings.disabled")}</Badge>
          )}
        </CardTitle>
        <CardDescription>
          {t("settings.twoFaDesc")}
        </CardDescription>
      </CardHeader>
      <CardContent className="px-6 pb-5 pt-1">
        {loading ? (
          <Skeleton className="h-9 w-40" />
        ) : enabled ? (
          <TwoFactorDisable />
        ) : (
          <TwoFactorEnroll />
        )}
      </CardContent>
    </Card>
  );
}

function TwoFactorEnroll() {
  const t = useT();
  const queryClient = useQueryClient();
  const [enrollment, setEnrollment] = useState<TwoFactorEnrollmentResponse | null>(null);
  const [code, setCode] = useState("");
  const [qrSvg, setQrSvg] = useState<string | null>(null);
  const [copiedKey, setCopiedKey] = useState(false);

  const beginMutation = useMutation({
    mutationFn: enrollTwoFactor,
    onSuccess: (data) => setEnrollment(data),
    onError: (err) =>
      toast.error(t("settings.enrollmentFailed"), {
        description: apiErrorMessage(err, t("settings.enrollFailedBody")),
      }),
  });

  const verifyMutation = useMutation({
    mutationFn: (otp: string) => verifyEnrollTwoFactor(otp),
    onSuccess: (data) => {
      if (data.success) {
        toast.success(t("settings.twoFaEnabledToast"), {
          description: t("settings.twoFaEnabledBody"),
        });
        setEnrollment(null);
        setCode("");
        setQrSvg(null);
        void queryClient.invalidateQueries({ queryKey: PROFILE_KEY });
      } else {
        toast.error(t("settings.verifyFailed"), {
          description: t("settings.codeMismatch"),
        });
      }
    },
    onError: (err) =>
      toast.error(t("settings.verifyFailed"), {
        description: apiErrorMessage(err, t("settings.verifyFailedBody")),
      }),
  });

  // Render the QR as inline SVG when the otpauth URI changes. Keeps the
  // image source-of-truth in JS without an extra <canvas>, and lets us
  // theme it via currentColor so it tracks dark/light mode.
  useEffect(() => {
    if (!enrollment) {
      setQrSvg(null);
      return;
    }
    let cancelled = false;
    void QRCode.toString(enrollment.authenticatorUri, {
      type: "svg",
      margin: 1,
      width: 200,
      errorCorrectionLevel: "M",
    }).then((svg) => {
      if (cancelled) return;
      const themed = svg
        .replace(/fill="#ffffff"/gi, 'fill="transparent"')
        .replace(/fill="#FFFFFF"/gi, 'fill="transparent"')
        .replace(/fill="#000000"/gi, 'fill="currentColor"')
        .replace(/fill="#000"/gi, 'fill="currentColor"');
      setQrSvg(themed);
    });
    return () => {
      cancelled = true;
    };
  }, [enrollment]);

  const copyKey = async () => {
    if (!enrollment) return;
    try {
      await navigator.clipboard.writeText(enrollment.sharedKey);
      setCopiedKey(true);
      window.setTimeout(() => setCopiedKey(false), 1500);
    } catch {
      /* clipboard unavailable — silently noop */
    }
  };

  if (!enrollment) {
    return (
      <div className="flex flex-wrap items-center gap-3">
        <Button
          onClick={() => beginMutation.mutate()}
          disabled={beginMutation.isPending}
        >
          <ShieldCheck className="mr-1.5 h-3.5 w-3.5" />
          {beginMutation.isPending ? t("settings.generating") : t("settings.enableTwoFa")}
        </Button>
        <span className="text-xs text-[var(--color-muted-foreground)]">
          {t("settings.enrollHint")}
        </span>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <div className="grid gap-5 sm:grid-cols-[14rem_1fr] sm:items-start">
        <div className="grid h-52 w-52 place-items-center rounded-md border border-[var(--color-border)] bg-[var(--color-card)] p-2 text-[var(--color-foreground)]">
          {qrSvg ? (
            <div
              aria-label={t("settings.qrLabel")}
              role="img"
              className="h-full w-full [&_svg]:h-full [&_svg]:w-full"
              dangerouslySetInnerHTML={{ __html: qrSvg }}
            />
          ) : (
            <span className="text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
              {t("settings.rendering")}
            </span>
          )}
        </div>
        <div className="space-y-3">
          <div>
            <div className="text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
              {t("settings.cantScan")}
            </div>
            <div className="mt-1 flex items-center gap-2">
              <code className="break-all rounded-md border border-[var(--color-border)] bg-[var(--color-muted)] px-2 py-1 font-mono text-[11px]">
                {enrollment.sharedKey}
              </code>
              <button
                type="button"
                onClick={copyKey}
                className="inline-flex h-7 items-center gap-1 rounded-md px-2 text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)] transition-colors hover:bg-[var(--color-muted)] hover:text-[var(--color-foreground)]"
              >
                {copiedKey ? (
                  <>
                    <ClipboardCheck className="h-3 w-3" /> {t("settings.copied")}
                  </>
                ) : (
                  <>
                    <Copy className="h-3 w-3" /> {t("settings.copy")}
                  </>
                )}
              </button>
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="totp-code">{t("settings.totpLabel")}</Label>
            <Input
              id="totp-code"
              inputMode="numeric"
              autoComplete="one-time-code"
              placeholder="123 456"
              maxLength={8}
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\s/g, ""))}
              className={cn(
                "font-mono text-lg tracking-[0.4em]",
                code.length >= 6 && "border-[var(--color-primary)]",
              )}
            />
          </div>

          <div className="flex flex-wrap items-center gap-2 pt-1">
            <Button
              onClick={() => verifyMutation.mutate(code)}
              disabled={code.length < 6 || verifyMutation.isPending}
            >
              {verifyMutation.isPending ? t("settings.verifying") : t("settings.confirmEnable")}
            </Button>
            <Button
              variant="ghost"
              onClick={() => {
                setEnrollment(null);
                setCode("");
              }}
              disabled={verifyMutation.isPending}
            >
              {t("chrome.cancel")}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function TwoFactorDisable() {
  const t = useT();
  const queryClient = useQueryClient();
  const [password, setPassword] = useState("");

  const mutation = useMutation({
    mutationFn: (pw: string) => disableTwoFactor(pw),
    onSuccess: (data) => {
      if (data.success) {
        toast.success(t("settings.twoFaDisabled"));
        setPassword("");
        void queryClient.invalidateQueries({ queryKey: PROFILE_KEY });
      } else {
        toast.error(t("settings.disableFailed"), {
          description: t("settings.passwordVerifyFailed"),
        });
      }
    },
    onError: (err) =>
      toast.error(t("settings.disableFailed"), {
        description: apiErrorMessage(err, t("settings.disableFailedBody")),
      }),
  });

  return (
    <div className="space-y-3">
      <p className="text-sm text-[var(--color-muted-foreground)]">
        {t("settings.twoFaDisableDesc")}
      </p>
      <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
        <PasswordField
          id="disable-pw"
          label={t("settings.currentPassword")}
          value={password}
          onChange={setPassword}
          autoComplete="current-password"
        />
        <Button
          type="button"
          variant="destructive"
          onClick={() => mutation.mutate(password)}
          disabled={password.length === 0 || mutation.isPending}
        >
          <ShieldOff className="mr-1 h-3.5 w-3.5" />
          {mutation.isPending ? t("settings.disabling") : t("settings.disableTwoFa")}
        </Button>
      </div>
    </div>
  );
}

function SessionsSkeleton() {
  return (
    <ul>
      {[0, 1].map((i) => (
        <li
          key={i}
          className="flex items-center justify-between gap-4 border-t border-[var(--color-border)] px-6 py-4 first:border-t-0"
        >
          <div className="flex items-center gap-3">
            <Skeleton className="h-9 w-9 rounded-full" />
            <div className="space-y-1.5">
              <Skeleton className="h-4 w-48" />
              <Skeleton className="h-3 w-64" />
            </div>
          </div>
          <Skeleton className="h-8 w-20" />
        </li>
      ))}
    </ul>
  );
}
