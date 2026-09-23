import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { useIsMutating, useMutation, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { sendChatMessage, type SendChatAttachment } from "@/api/chat";
import { getFileDownloadUrl, Visibility } from "@/api/files";
import { useAuth } from "@/auth/use-auth";
import { useFileUpload, formatBytes } from "@/hooks/use-file-upload";
import { ChatPermissions, FilesPermissions } from "@/lib/permissions";
import { useLocale } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
  const [attachment, setAttachment] = useState<SendChatAttachment | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const canUpload = user?.permissions.includes(FilesPermissions.Upload) ?? false;
  const fileUpload = useFileUpload({ ownerType: "ChatChannel", ownerId: channelId, visibility: Visibility.Private,
    category: file => file.type.startsWith("image/") ? "Image" : "Document", maxBytes: 50 * 1024 * 1024, resumeOnRetry: true });
  const attempt = useRef<{ signature: string; key: string } | null>(null);
  const mounted = useRef(true);
  const submitting = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => fileUpload.cancel, [fileUpload.cancel]);
  const busy = useIsMutating({ mutationKey: ["chat", "write", channelId] }) > 0;
  const uploadMutation = useMutation({
    mutationKey: ["chat", "write", channelId],
    mutationFn: async (file: File) => {
      const asset = await fileUpload.upload(file);
      if (asset.status !== "Available") throw new Error("Attachment unavailable");
      const download = await getFileDownloadUrl(asset.id, { inline: true });
      return { fileAssetId: asset.id, url: download.url, contentType: asset.contentType, fileName: asset.originalFileName, sizeBytes: asset.sizeBytes } satisfies SendChatAttachment;
    },
    onSuccess: value => { setAttachment(value); attempt.current = null; },
  });
  const mutation = useMutation({
    mutationKey: ["chat", "write", channelId], mutationFn: sendChatMessage,
    onSuccess: async message => {
      cache.setQueryData(["chat", user?.id, "message", channelId, message.id], message);
      await cache.invalidateQueries({ queryKey: ["chat", user?.id] });
      if (!mounted.current) return;
      setBody("");
      setAttachment(null);
      setSelectedFile(null);
      if (fileInput.current) fileInput.current.value = "";
      fileUpload.reset();
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
      || busy || submitting.current || (!text && !attachment) || text.length > 32768) return;
    const signature = JSON.stringify({ text, fileAssetId: attachment?.fileAssetId ?? null });
    if (attempt.current?.signature !== signature) attempt.current = { signature, key: crypto.randomUUID() };
    submitting.current = true;
    mutation.mutate({ channelId, parentMessageId, body: text, key: attempt.current.key, attachments: attachment ? [attachment] : [] });
  }
  function pickFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0] ?? null;
    setSelectedFile(file);
    setAttachment(null);
    attempt.current = null;
    uploadMutation.reset();
    fileUpload.reset();
    if (file && canUpload) uploadMutation.mutate(file);
  }
  const label = t(parentMessageId ? "chatCompose.reply" : "chatCompose.message");
  const id = `chat-compose-${parentMessageId ?? "channel"}`;
  return <form aria-label={label} onSubmit={submit} className="space-y-3">
    <Field id={id} label={label} required={!attachment}><textarea id={id} value={body} onChange={event => { setBody(event.target.value); attempt.current = null; }} required={!attachment} maxLength={32768} disabled={busy} className="min-h-28 w-full rounded-lg border bg-transparent p-3" /></Field>
    {canUpload && <div className="space-y-2 rounded-lg border p-3">
      <Field id={`${id}-attachment`} label={t("chatCompose.attachment")}><Input ref={fileInput} id={`${id}-attachment`} type="file" disabled={busy || !!attachment} onChange={pickFile} /></Field>
      <p className="text-sm text-[var(--color-muted-foreground)]">{t("chatCompose.attachmentHint")}</p>
      {busy && uploadMutation.isPending && <p role="status">{t("common.working")} {fileUpload.progress?.percent ?? 0}%</p>}
      {uploadMutation.isError && <div className="space-y-2"><ErrorBand message={describe(uploadMutation.error, t("chatCompose.uploadFailed"))} /><Button type="button" variant="outline" disabled={busy || !selectedFile} onClick={() => { if (selectedFile && canUpload) uploadMutation.mutate(selectedFile); }}>{t("chatCompose.retryUpload")}</Button></div>}
      {attachment && <div className="flex flex-wrap items-center justify-between gap-2"><p className="min-w-0 break-all" role="status">{attachment.fileName} · {formatBytes(attachment.sizeBytes)}</p><Button type="button" variant="outline" disabled={busy} onClick={() => { setAttachment(null); setSelectedFile(null); if (fileInput.current) fileInput.current.value = ""; fileUpload.reset(); attempt.current = null; }}>{t("chatCompose.removeAttachment")}</Button></div>}
    </div>}
    <p className="text-sm text-[var(--color-muted-foreground)]">{t("chatCompose.hint")}</p>
    {mutation.isError && <ErrorBand message={describe(mutation.error, t("chatCompose.failed"))} />}
    <Button type="submit" disabled={busy || (!body.trim() && !attachment)}>{t(busy ? "common.working" : "chatCompose.send")}</Button>
  </form>;
}
