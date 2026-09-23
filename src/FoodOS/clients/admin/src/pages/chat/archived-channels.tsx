import { useState, type FormEvent } from "react";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { listArchivedChatChannels, restoreChatChannel } from "@/api/chat";
import { useAuth } from "@/auth/use-auth";
import { ErrorBand, Field, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { ChatPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

export function ArchivedChannelsButton() {
  const { user } = useAuth();
  const t = useT();
  const [open, setOpen] = useState(false);
  const allowed = user?.tenant === "root" && user.permissions.includes(ChatPermissions.View) && user.permissions.includes(ChatPermissions.ManageAll);
  if (!allowed) return null;
  return <><Button variant="outline" onClick={() => setOpen(true)}>{t("chatManage.archived")}</Button>{open && <ArchivedChannelsDialog onClose={() => setOpen(false)} />}</>;
}

function ArchivedChannelsDialog({ onClose }: { onClose: () => void }) {
  const { user } = useAuth();
  const t = useT();
  const cache = useQueryClient();
  const [input, setInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [restoring, setRestoring] = useState<string | null>(null);
  const allowed = user?.tenant === "root" && user.permissions.includes(ChatPermissions.View) && user.permissions.includes(ChatPermissions.ManageAll);
  const query = useQuery({
    queryKey: ["chat", user?.id, "archived", search, page],
    queryFn: ({ signal }) => listArchivedChatChannels(search, page, signal),
    enabled: allowed,
  });
  const busy = useIsMutating({ mutationKey: ["chat", "write", "archived"] }) > 0;
  const restore = useMutation({
    mutationKey: ["chat", "write", "archived"],
    mutationFn: restoreChatChannel,
    onSuccess: async () => {
      await Promise.all([
        cache.invalidateQueries({ queryKey: ["chat", user?.id, "archived"] }),
        cache.invalidateQueries({ queryKey: ["chat", user?.id, "channels"] }),
      ]);
      setRestoring(null);
    },
  });
  const submitSearch = (event: FormEvent) => { event.preventDefault(); setPage(1); setSearch(input.trim()); };
  return <Dialog open={allowed} onOpenChange={value => { if (!value && !busy) onClose(); }}><DialogContent size="lg">
    <DialogHeader><DialogTitle>{t("chatManage.archived")}</DialogTitle><DialogDescription>{t("chatManage.archivedHint")}</DialogDescription></DialogHeader>
    <DialogBody className="space-y-4">
      <form className="flex min-w-0 flex-col gap-2 sm:flex-row sm:items-end" onSubmit={submitSearch}><Field id="chat-archived-search" label={t("chatManage.searchChannels")}><Input id="chat-archived-search" maxLength={200} value={input} disabled={busy} onChange={event => setInput(event.target.value)} /></Field><Button type="submit" variant="outline" disabled={busy}>{t("chatManage.searchAction")}</Button></form>
      {query.isPending && <LoadingRow label={t("common.loading")} />}
      {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("chatManage.failed"))} /><Button variant="outline" disabled={query.isFetching} onClick={() => { if (allowed) void query.refetch(); }}>{t("workbench.retry")}</Button></div>}
      {query.isSuccess && <>
        {query.data.items.length === 0 && <p role="status">{t("chatManage.noArchived")}</p>}
        <div className="space-y-2">{query.data.items.map(channel => <article key={channel.id} className="flex min-w-0 flex-wrap items-center justify-between gap-3 rounded-lg border p-3"><div className="min-w-0"><h3 className="break-words font-medium">{channel.name || channel.id}</h3><p className="break-all text-sm text-[var(--color-muted-foreground)]">{channel.id} · {t(channel.isPrivate ? "chat.private" : "chat.public")}</p></div><Button variant="outline" disabled={busy} onClick={() => { restore.reset(); setRestoring(channel.id); }}>{t("chatManage.restore")}</Button></article>)}</div>
        <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
      </>}
    </DialogBody><DialogFooter><Button variant="outline" disabled={busy} onClick={onClose}>{t("common.close")}</Button></DialogFooter>
    <ConfirmDialog open={!!restoring} title={t("chatManage.restore")} description={<>{t("chatManage.restoreHint")}{restore.isError && <span role="alert" className="mt-3 block">{describe(restore.error, t("chatManage.failed"))}</span>}</>} confirmLabel={t("chatManage.restore")} pending={busy}
      onOpenChange={value => { if (!value && !busy) { setRestoring(null); restore.reset(); } }} onConfirm={() => { if (restoring && allowed && !busy) restore.mutate(restoring); }} />
  </DialogContent></Dialog>;
}
