import { useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { KeyRound, User as UserIcon, Users } from "lucide-react";
import { toast } from "sonner";
import { registerUser } from "@/api/users";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/list";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogBody,
  DialogFooter,
} from "@/components/ui/dialog";
import { ApiRequestError } from "@/lib/api-client";
import { useT } from "@/i18n/locale-provider";

// ─── Schema (identical to the old create page) ───────────────────────────────

const USERNAME_RE = /^[a-zA-Z][a-zA-Z0-9._-]{2,31}$/;

function makeSchema(t: (key: string, fallback?: string) => string) {
  return z
    .object({
      firstName: z.string().trim().min(1, t("users.required")).max(64),
      lastName: z.string().trim().min(1, t("users.required")).max(64),
      userName: z
        .string()
        .trim()
        .regex(USERNAME_RE, t("users.usernamePattern")),
      email: z.string().trim().email(t("users.validEmail")),
      phoneNumber: z.string().trim().max(32).optional(),
      password: z.string().min(8, t("users.min8")),
      confirmPassword: z.string().min(8),
    })
    .refine((d) => d.password === d.confirmPassword, {
      path: ["confirmPassword"],
      message: t("users.passwordsMismatch"),
    });
}

type FormValues = z.infer<ReturnType<typeof makeSchema>>;

// ─── Section label ────────────────────────────────────────────────────────────

function SectionLabel({
  icon: Icon,
  title,
  description,
}: {
  icon: React.ComponentType<{ className?: string; "aria-hidden"?: boolean | "true" }>;
  title: string;
  description: string;
}) {
  return (
    <div className="flex items-start gap-2.5 pb-1">
      <span
        aria-hidden
        className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-md bg-[var(--color-accent)] text-[var(--color-muted-foreground)]"
      >
        <Icon className="h-3.5 w-3.5" aria-hidden />
      </span>
      <div className="min-w-0">
        <p className="text-[12.5px] font-semibold text-[var(--color-foreground)]">{title}</p>
        <p className="text-[11.5px] leading-relaxed text-[var(--color-muted-foreground)]">
          {description}
        </p>
      </div>
    </div>
  );
}

// ─── Dialog ───────────────────────────────────────────────────────────────────

export function CreateUserDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const schema = useMemo(() => makeSchema(t), [t]);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: "",
      lastName: "",
      userName: "",
      email: "",
      phoneNumber: "",
      password: "",
      confirmPassword: "",
    },
  });

  const mutation = useMutation({
    // Pass values via mutate(arg) — no closed-over state captured at submit time.
    mutationFn: (values: FormValues) =>
      registerUser({
        firstName: values.firstName,
        lastName: values.lastName,
        userName: values.userName,
        email: values.email,
        password: values.password,
        confirmPassword: values.confirmPassword,
        phoneNumber: values.phoneNumber?.trim() || undefined,
      }),
    onSuccess: (result) => {
      toast.success(t("users.created"), {
        description: result.message ?? t("users.createdFallback"),
      });
      queryClient.invalidateQueries({ queryKey: ["users"] });
      handleClose();
      navigate(result.userId ? `/users/${result.userId}` : "/users");
    },
    onError: (err) => {
      const detail =
        err instanceof ApiRequestError
          ? err.problem?.detail ?? err.problem?.title ?? err.message
          : (err as Error).message;
      toast.error(t("users.createFailed"), { description: detail });
    },
  });

  function handleClose() {
    reset();
    onOpenChange(false);
  }

  const onSubmit = handleSubmit((values) => mutation.mutate(values));
  const submitting = isSubmitting || mutation.isPending;

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        if (!o) handleClose();
        else onOpenChange(true);
      }}
    >
      <DialogContent size="lg">
        {/* ── Header ── */}
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span
              aria-hidden
              className="relative grid h-9 w-9 shrink-0 place-items-center overflow-hidden rounded-xl
                bg-[oklch(from_var(--color-primary)_l_c_h_/_0.12)]
                text-[var(--color-primary)]
                ring-1 ring-inset ring-[oklch(from_var(--color-primary)_l_c_h_/_0.18)]"
            >
              <Users className="h-[18px] w-[18px]" />
            </span>
            <div className="min-w-0">
              <DialogTitle className="text-[16px]">{t("users.newAccount")}</DialogTitle>
            </div>
          </div>
          <DialogDescription className="mt-1">
            {t("users.createDesc")}
          </DialogDescription>
        </DialogHeader>

        {/* ── Form ── */}
        <form onSubmit={onSubmit}>
          <DialogBody className="space-y-6">
            {/* ── Identity section ── */}
            <div className="space-y-3">
              <SectionLabel
                icon={UserIcon}
                title={t("users.identity")}
                description={t("users.identityDesc")}
              />
              <div className="h-px bg-[var(--color-border)] opacity-60" />
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field
                    id="cu-firstName"
                    label={t("users.firstName")}
                    required
                    error={errors.firstName?.message}
                  >
                    <Input
                      id="cu-firstName"
                      autoComplete="given-name"
                      aria-invalid={errors.firstName ? true : undefined}
                      {...register("firstName")}
                    />
                  </Field>
                  <Field
                    id="cu-lastName"
                    label={t("users.lastName")}
                    required
                    error={errors.lastName?.message}
                  >
                    <Input
                      id="cu-lastName"
                      autoComplete="family-name"
                      aria-invalid={errors.lastName ? true : undefined}
                      {...register("lastName")}
                    />
                  </Field>
                </div>

                <Field
                  id="cu-userName"
                  label={t("users.username")}
                  required
                  hint={t("users.usernameHint")}
                  error={errors.userName?.message}
                >
                  <Input
                    id="cu-userName"
                    placeholder="m.chen"
                    autoComplete="off"
                    className="font-mono"
                    aria-invalid={errors.userName ? true : undefined}
                    {...register("userName")}
                  />
                </Field>

                <Field
                  id="cu-email"
                  label={t("users.email")}
                  required
                  error={errors.email?.message}
                >
                  <Input
                    id="cu-email"
                    type="email"
                    placeholder="user@example.com"
                    autoComplete="email"
                    className="font-mono"
                    aria-invalid={errors.email ? true : undefined}
                    {...register("email")}
                  />
                </Field>

                <Field
                  id="cu-phoneNumber"
                  label={t("users.phoneOptional")}
                  error={errors.phoneNumber?.message}
                >
                  <Input
                    id="cu-phoneNumber"
                    type="tel"
                    placeholder="+1 555 0100"
                    autoComplete="tel"
                    className="font-mono"
                    {...register("phoneNumber")}
                  />
                </Field>
              </div>
            </div>

            {/* ── Credentials section ── */}
            <div className="space-y-3">
              <SectionLabel
                icon={KeyRound}
                title={t("users.credentials")}
                description={t("users.credentialsDesc")}
              />
              <div className="h-px bg-[var(--color-border)] opacity-60" />
              <div className="grid gap-4 sm:grid-cols-2">
                <Field
                  id="cu-password"
                  label={t("users.password")}
                  required
                  error={errors.password?.message}
                >
                  <Input
                    id="cu-password"
                    type="password"
                    autoComplete="new-password"
                    className="font-mono"
                    aria-invalid={errors.password ? true : undefined}
                    {...register("password")}
                  />
                </Field>
                <Field
                  id="cu-confirmPassword"
                  label={t("users.confirmPassword")}
                  required
                  error={errors.confirmPassword?.message}
                >
                  <Input
                    id="cu-confirmPassword"
                    type="password"
                    autoComplete="new-password"
                    className="font-mono"
                    aria-invalid={errors.confirmPassword ? true : undefined}
                    {...register("confirmPassword")}
                  />
                </Field>
              </div>
            </div>
          </DialogBody>

          {/* ── Footer ── */}
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={handleClose}
              disabled={submitting}
            >
              {t("chrome.cancel")}
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? t("users.creating") : t("users.createAccount")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
