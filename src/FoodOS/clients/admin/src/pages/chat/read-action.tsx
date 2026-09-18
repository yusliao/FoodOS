import { useMutation, useQueryClient, useIsMutating } from "@tanstack/react-query";
import { markChatRead, type ChatMessage } from "@/api/chat";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { ErrorBand } from "@/components/list";
import { useT } from "@/i18n/locale-provider";
import { ChatPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

export function ReadAction({ message }: { message: ChatMessage }) {
  const { user } = useAuth();
  const t = useT();
  const cache = useQueryClient();
  const allowed = user?.tenant === "root" && user.permissions.includes(ChatPermissions.View);
  const busy = useIsMutating({ mutationKey: ["chat", "write", message.channelId] }) > 0;
  const mutation = useMutation({
    mutationKey: ["chat", "write", message.channelId],
    mutationFn: markChatRead,
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: ["chat", user?.id] });
    },
  });

  if (!allowed) return null;
  const mark = () => mutation.mutate({ channelId: message.channelId, messageId: message.id });
  return <div className="space-y-2">
    <Button size="sm" variant="outline" disabled={busy} onClick={() => { mutation.reset(); mark(); }}>
      {t(busy ? "common.working" : "chatRead.action")}
    </Button>
    {mutation.isSuccess && <p role="status" className="text-sm">{t("chatRead.success")}</p>}
    {mutation.isError && <div className="space-y-2">
      <ErrorBand message={describe(mutation.error, t("chatRead.failed"))} />
      <Button size="sm" variant="outline" disabled={busy} onClick={mark}>{t("workbench.retry")}</Button>
    </div>}
  </div>;
}
