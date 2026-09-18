import { useId, useState, type FormEvent } from "react";
import { useIsMutating, useMutation, useQueryClient } from "@tanstack/react-query";
import { deleteChatMessage, editChatMessage, type ChatMessage } from "@/api/chat";
import { useAuth } from "@/auth/use-auth";
import { ChatPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { ErrorBand, Field } from "@/components/list";
import { describe } from "@/pages/customers/request-error";

export function MessageActions({ message }: { message: ChatMessage }) {
  const { user } = useAuth();
  const t = useT();
  const cache = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [body, setBody] = useState("");
  const fieldId = useId();
  const allowed = user?.tenant === "root" && user.permissions.includes(ChatPermissions.View) && !message.deletedAtUtc;
  const own = user?.id === message.authorUserId;
  const canEdit = allowed && own && user.permissions.includes(ChatPermissions.EditOwn);
  const canDelete = allowed && user.permissions.includes(ChatPermissions.DeleteOwn) && (own || user.permissions.includes(ChatPermissions.DeleteAny));
  const busy = useIsMutating({ mutationKey: ["chat", "write", message.channelId] }) > 0;
  const update = useMutation({ mutationKey: ["chat", "write", message.channelId], mutationFn: editChatMessage,
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["chat", user?.id] }); setEditing(false); },
  });
  const remove = useMutation({ mutationKey: ["chat", "write", message.channelId], mutationFn: deleteChatMessage,
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["chat", user?.id] }); setDeleting(false); },
  });
  function closeEdit() { if (!busy) { setEditing(false); update.reset(); } }
  function submit(event: FormEvent) {
    event.preventDefault();
    const text = body.trim();
    if (canEdit && !busy && text && text.length <= 32768) update.mutate({ messageId: message.id, body: text });
  }
  if (!canEdit && !canDelete) return null;
  return <div className="flex flex-wrap gap-2">
    {canEdit && <Button size="sm" variant="outline" disabled={busy} onClick={() => { setBody(message.body ?? ""); update.reset(); setEditing(true); }}>{t("chatActions.edit")}</Button>}
    {canDelete && <Button size="sm" variant="destructive" disabled={busy} onClick={() => { remove.reset(); setDeleting(true); }}>{t("chatActions.delete")}</Button>}
    <Dialog open={editing && !!canEdit} onOpenChange={value => { if (!value) closeEdit(); }}><DialogContent>
      <DialogHeader><DialogTitle>{t("chatActions.edit")}</DialogTitle><DialogDescription>{t("chatActions.editHint")}</DialogDescription></DialogHeader>
      <form onSubmit={submit}><DialogBody className="space-y-3">
        <Field id={fieldId} label={t("chatActions.body")} required><textarea id={fieldId} required maxLength={32768} value={body} disabled={busy} onChange={event => setBody(event.target.value)} className="min-h-28 w-full rounded-lg border bg-transparent p-3" /></Field>
        {update.isError && <ErrorBand message={describe(update.error, t("chatActions.failed"))} />}
      </DialogBody><DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={closeEdit}>{t("chrome.cancel")}</Button><Button type="submit" disabled={busy || !body.trim()}>{t(busy ? "common.working" : "chatActions.save")}</Button></DialogFooter></form>
    </DialogContent></Dialog>
    <ConfirmDialog open={deleting && !!canDelete} title={t("chatActions.delete")} description={<>{t("chatActions.deleteHint")}{remove.isError && <span role="alert" className="mt-3 block">{describe(remove.error, t("chatActions.failed"))}</span>}</>}
      confirmLabel={t("chatActions.delete")} destructive pending={busy}
      onOpenChange={value => { if (!value && !busy) { setDeleting(false); remove.reset(); } }}
      onConfirm={() => { if (canDelete && !busy) remove.mutate(message.id); }} />
  </div>;
}
