import { useEffect, useRef, useState, type FormEvent } from "react";
import { useIsMutating, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/auth/use-auth";
import { useFileUpload } from "@/hooks/use-file-upload";
import { Visibility } from "@/api/files";
import { FilesPermissions, TicketsPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ErrorBand, Field } from "@/components/list";

function category(file: File) {
  const extension = file.name.split(".").pop()?.toLowerCase();
  if (["jpg", "jpeg", "png", "webp", "gif", "ico"].includes(extension ?? "")) return "Image";
  return extension === "zip" ? "Archive" : "Document";
}

export function TicketAttachmentUpload({ ticketId }: { ticketId: string }) {
  const { user } = useAuth();
  const allowed = user?.tenant === "root" && user.permissions.includes(TicketsPermissions.View) && user.permissions.includes(FilesPermissions.Upload);
  return allowed ? <UploadForm key={`${user.id}:${ticketId}`} ticketId={ticketId} /> : null;
}

function UploadForm({ ticketId }: { ticketId: string }) {
  const t = useT();
  const { user } = useAuth();
  const cache = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [done, setDone] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const { upload, progress, cancel, reset } = useFileUpload({ ownerType: "Ticket", ownerId: ticketId, visibility: Visibility.Private, category, resumeOnRetry: true });
  useEffect(() => cancel, [cancel]);
  const mutation = useMutation({ mutationKey: ["tickets", "write", ticketId], mutationFn: async (selected: File) => {
    const asset = await upload(selected);
    if (asset.status !== "Available") throw new Error("Attachment unavailable");
    return asset;
  }, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["tickets", "attachments", ticketId] }); setFile(null); if (input.current) input.current.value = ""; setDone(true); reset(); } });
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticketId] }) > 0;
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!file || file.size === 0 || busy || user?.tenant !== "root" || !user.permissions.includes(TicketsPermissions.View) || !user.permissions.includes(FilesPermissions.Upload)) return;
    mutation.mutate(file);
  }
  return <form onSubmit={submit} className="space-y-3 rounded-xl border p-4">
    <Field id="ticket-attachment-upload" label={t("tickets.chooseAttachment")}><Input ref={input} id="ticket-attachment-upload" type="file" disabled={busy} onChange={event => { setFile(event.target.files?.[0] ?? null); setDone(false); mutation.reset(); reset(); }} /></Field>
    <p className="text-sm">{t("tickets.uploadHint")}</p>
    {mutation.isError && <ErrorBand message={t("tickets.uploadFailed")} />}
    {busy && <p role="status">{t("common.working")} {progress?.percent ?? 0}%</p>}
    {done && <p role="status">{t("tickets.uploadDone")}</p>}
    <Button type="submit" disabled={!file || file.size === 0 || busy}>{t(mutation.isError ? "tickets.retryUpload" : "tickets.uploadAttachment")}</Button>
  </form>;
}
