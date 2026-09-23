import { useMutation } from "@tanstack/react-query";
import { getFileDownloadUrl } from "@/api/files";
import type { ChatAttachment } from "@/api/chat";
import { useLocale } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { ErrorBand } from "@/components/list";
import { formatBytes } from "@/hooks/use-file-upload";
import { describe } from "@/pages/customers/request-error";

class InvalidDownloadLink extends Error {}

export function ChatAttachmentDownload({ attachment }: { attachment: ChatAttachment }) {
  const { t } = useLocale();
  const fileId = attachment.fileAssetId;
  const validId = typeof fileId === "string" && /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(fileId);
  const mutation = useMutation({ mutationFn: async (id: string) => {
    const result = await getFileDownloadUrl(id);
    try {
      const url = new URL(result.url);
      if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || !(Date.parse(result.expiresAt) > Date.now())) throw new InvalidDownloadLink();
    } catch { throw new InvalidDownloadLink(); }
    return result;
  } });
  return <li className="min-w-0 space-y-2 rounded-lg border p-3">
    <p className="break-all font-medium">{attachment.originalFileName}</p>
    <p>{formatBytes(attachment.sizeBytes)}</p>
    {validId ? <>
      <Button variant="outline" disabled={mutation.isPending} onClick={() => { if (!mutation.isPending) mutation.mutate(fileId); }}>{t(mutation.isPending ? "common.working" : "chat.prepareDownload")}</Button>
      {mutation.isError && <ErrorBand message={mutation.error instanceof InvalidDownloadLink ? t("chat.downloadInvalid") : describe(mutation.error, t("chat.downloadFailed"))} />}
      {mutation.isSuccess && <a className="block underline" href={mutation.data.url} target="_blank" rel="noopener noreferrer" referrerPolicy="no-referrer" onClick={event => { if (Date.parse(mutation.data.expiresAt) <= Date.now()) { event.preventDefault(); mutation.reset(); } }}>{t("chat.download")}</a>}
    </> : <p role="status">{t("chat.attachmentUnavailable")}</p>}
  </li>;
}
