import { useState } from "react";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteFile, getFileDownloadUrl, listOwnerFiles, type FileAssetDto } from "@/api/files";
import { useAuth } from "@/auth/use-auth";
import { FilesPermissions, TicketsPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { formatBytes } from "@/hooks/use-file-upload";
import { describe } from "@/pages/customers/request-error";
import { TicketAttachmentUpload } from "./attachment-upload";

export function TicketAttachments({ ticketId }: { ticketId: string }) {
  const t = useT();
  const { user } = useAuth();
  const cache = useQueryClient();
  const allowed = user?.tenant === "root" && user.permissions.includes(TicketsPermissions.View);
  const [page, setPage] = useState(1);
  const [target, setTarget] = useState<FileAssetDto | null>(null);
  const query = useQuery({ queryKey: ["tickets", "attachments", ticketId, page], queryFn: ({ signal }) => listOwnerFiles("Ticket", ticketId, page, signal), enabled: allowed });
  const canDelete = (file: FileAssetDto) => allowed && user.permissions.includes(FilesPermissions.DeleteOwn) && file.createdByUserId === user.id;
  const mutation = useMutation({ mutationKey: ["tickets", "write", ticketId], mutationFn: deleteFile, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["tickets", "attachments", ticketId] }); setTarget(null); } });
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticketId] }) > 0;
  return <section className="space-y-3" aria-label={t("tickets.attachments")}>
    <h2 className="font-semibold">{t("tickets.attachments")}</h2>
    <p className="text-sm">{t("tickets.attachmentsHint")}</p>
    <TicketAttachmentUpload ticketId={ticketId} />
    {query.isPending && <LoadingRow label={t("common.loading")} />}
    {query.isError && <div><ErrorBand message={describe(query.error, t("tickets.failed"))} /><Button variant="outline" disabled={query.isFetching} onClick={() => { if (allowed) void query.refetch(); }}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && <>
      {query.data.items.length === 0 && <p role="status">{t("tickets.noAttachments")}</p>}
      <div className="grid gap-3 md:grid-cols-2">{query.data.items.map(file => <article className="min-w-0 space-y-2 rounded-xl border p-4" key={file.id}>
        <h3 className="break-all font-medium">{file.originalFileName}</h3><p>{formatBytes(file.sizeBytes)}</p>
        <AttachmentDownload fileId={file.id} allowed={allowed} />
        {canDelete(file) && <Button variant="destructive" disabled={busy} onClick={() => { mutation.reset(); setTarget(file); }}>{t("tickets.deleteAttachment")}</Button>}
      </article>)}</div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching || busy} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
    </>}
    <ConfirmDialog open={!!target && canDelete(target)} title={t("tickets.deleteAttachment")} description={<>{target?.originalFileName}{mutation.isError && <span role="alert" className="mt-3 block">{describe(mutation.error, t("tickets.failed"))}</span>}</>} confirmLabel={t("tickets.deleteAttachment")} destructive pending={busy} onOpenChange={open => { if (!open && !busy) { setTarget(null); mutation.reset(); } }} onConfirm={() => { if (target && canDelete(target) && !busy) mutation.mutate(target.id); }} />
  </section>;
}

class InvalidDownloadLink extends Error {}

function AttachmentDownload({ fileId, allowed }: { fileId: string; allowed: boolean }) {
  const t = useT();
  const mutation = useMutation({ mutationFn: async (id: string) => {
    const result = await getFileDownloadUrl(id);
    try {
      const url = new URL(result.url);
      if (!["https:", "http:"].includes(url.protocol) || url.username || url.password || !(Date.parse(result.expiresAt) > Date.now())) throw new InvalidDownloadLink();
    } catch { throw new InvalidDownloadLink(); }
    return result;
  } });
  return <div className="space-y-2">
    <Button variant="outline" disabled={!allowed || mutation.isPending} onClick={() => { if (allowed && !mutation.isPending) mutation.mutate(fileId); }}>{t(mutation.isPending ? "common.working" : "tickets.prepareDownload")}</Button>
    {mutation.isError && <ErrorBand message={mutation.error instanceof InvalidDownloadLink ? t("tickets.downloadInvalid") : describe(mutation.error, t("tickets.failed"))} />}
    {allowed && mutation.isSuccess && <a className="block underline" href={mutation.data.url} target="_blank" rel="noopener noreferrer" referrerPolicy="no-referrer" onClick={event => { if (Date.parse(mutation.data.expiresAt) <= Date.now()) { event.preventDefault(); mutation.reset(); } }}>{t("tickets.downloadAttachment")}</a>}
  </div>;
}
