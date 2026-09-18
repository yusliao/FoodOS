import { useEffect, useRef, useState, type FormEvent } from "react";
import { useIsMutating, useMutation, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { sendChatMessage } from "@/api/chat";
import { useAuth } from "@/auth/use-auth";
import { ChatPermissions } from "@/lib/permissions";
import { useLocale } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { ErrorBand, Field } from "@/components/list";
import { describe } from "@/pages/customers/request-error";

// The parent only mounts this after the channel membership check succeeds.
export function ChatComposer({ channelId, parentMessageId = null }: { channelId: string; parentMessageId?: string | null }) {
  const { user } = useAuth();
  if (user?.tenant !== "root" || !user.permissions.includes(ChatPermissions.View) || !user.permissions.includes(ChatPermissions.Send)) return null;
  return <ComposerForm key={`${user.id}:${channelId}:${parentMessageId}`} channelId={channelId} parentMessageId={parentMessageId} />;
}
function ComposerForm({ channelId, parentMessageId }: { channelId: string; parentMessageId: string | null }) {
  const { user } = useAuth();
  const { t } = useLocale();
  const cache = useQueryClient();
  const [, setSearch] = useSearchParams();
  const [body, setBody] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mounted = useRef(true);
  const submitting = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const busy = useIsMutating({ mutationKey: ["chat", "write", channelId] }) > 0;
  const mutation = useMutation({
    mutationKey: ["chat", "write", channelId], mutationFn: sendChatMessage,
    onSuccess: async message => {
      cache.setQueryData(["chat", user?.id, "message", channelId, message.id], message);
      await cache.invalidateQueries({ queryKey: ["chat", user?.id] });
      if (!mounted.current) return;
      setBody("");
      attempt.current = null;
      // Also locates newly sent messages when the user was browsing an older page.
      setSearch(previous => { const next = new URLSearchParams(previous); next.set("messageId", message.id); return next; });
    },
    onSettled: () => { submitting.current = false; },
  });
  function submit(event: FormEvent) {
    event.preventDefault();
    const text = body.trim();
    if (user?.tenant !== "root" || !user.permissions.includes(ChatPermissions.View) || !user.permissions.includes(ChatPermissions.Send)
      || busy || submitting.current || !text || text.length > 32768) return;
    if (attempt.current?.body !== text) attempt.current = { body: text, key: crypto.randomUUID() };
    submitting.current = true;
    mutation.mutate({ channelId, parentMessageId, body: text, key: attempt.current.key });
  }
  const label = t(parentMessageId ? "chatCompose.reply" : "chatCompose.message");
  const id = `chat-compose-${parentMessageId ?? "channel"}`;
  return <form aria-label={label} onSubmit={submit} className="space-y-3">
    <Field id={id} label={label} required><textarea id={id} value={body} onChange={event => setBody(event.target.value)} required maxLength={32768} disabled={busy} className="min-h-28 w-full rounded-lg border bg-transparent p-3" /></Field>
    <p className="text-sm text-[var(--color-muted-foreground)]">{t("chatCompose.hint")}</p>
    {mutation.isError && <ErrorBand message={describe(mutation.error, t("chatCompose.failed"))} />}
    <Button type="submit" disabled={busy || !body.trim()}>{t(busy ? "common.working" : "chatCompose.send")}</Button>
  </form>;
}
