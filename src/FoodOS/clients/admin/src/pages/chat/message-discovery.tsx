import { useState, type FormEvent, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { listPinnedChatMessages, searchChatMessages, type ChatMessage } from "@/api/chat";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ErrorBand, Field, LoadingRow } from "@/components/list";
import { useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

export function MessageDiscovery({
  channelId,
  userId,
  renderMessage,
}: {
  channelId: string;
  userId?: string;
  renderMessage: (message: ChatMessage) => ReactNode;
}) {
  const t = useT();
  const [input, setInput] = useState("");
  const [queryText, setQueryText] = useState("");
  const [showPinned, setShowPinned] = useState(false);
  const search = useQuery({
    queryKey: ["chat", userId, "search", channelId, queryText],
    queryFn: ({ signal }) => searchChatMessages({ query: queryText, channelId }, signal),
    enabled: queryText.length > 0,
  });
  const pinned = useQuery({
    queryKey: ["chat", userId, "pinned", channelId],
    queryFn: ({ signal }) => listPinnedChatMessages(channelId, signal),
    enabled: showPinned,
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    setQueryText(input.trim());
  }

  return <section aria-label={t("chatDiscovery.title")} className="space-y-4 rounded-xl border p-4">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <h2 className="font-semibold">{t("chatDiscovery.title")}</h2>
      <Button variant="outline" aria-expanded={showPinned} onClick={() => setShowPinned(value => !value)}>
        {t(showPinned ? "chatDiscovery.hidePinned" : "chatDiscovery.showPinned")}
      </Button>
    </div>
    <form className="flex min-w-0 flex-col gap-2 sm:flex-row sm:items-end" onSubmit={submit}>
      <Field id={`chat-search-${channelId}`} label={t("chatDiscovery.searchLabel")} className="min-w-0 flex-1">
        <Input
          id={`chat-search-${channelId}`}
          type="search"
          maxLength={500}
          value={input}
          onChange={event => setInput(event.target.value)}
        />
      </Field>
      <Button type="submit" variant="outline" disabled={!input.trim()}>{t("chatDiscovery.searchAction")}</Button>
    </form>
    {queryText && <section className="space-y-3" aria-label={t("chatDiscovery.searchResults")}>
      {search.isPending && <LoadingRow label={t("common.loading")} />}
      {search.isError && <><ErrorBand message={describe(search.error, t("chatDiscovery.searchFailed"))} /><Button variant="outline" onClick={() => void search.refetch()}>{t("workbench.retry")}</Button></>}
      {search.isSuccess && (search.data.length === 0 ? <p role="status">{t("chatDiscovery.noSearchResults")}</p> : search.data.map(renderMessage))}
    </section>}
    {showPinned && <section className="space-y-3" aria-label={t("chatDiscovery.pinnedMessages")}>
      {pinned.isPending && <LoadingRow label={t("common.loading")} />}
      {pinned.isError && <><ErrorBand message={describe(pinned.error, t("chatDiscovery.pinnedFailed"))} /><Button variant="outline" onClick={() => void pinned.refetch()}>{t("workbench.retry")}</Button></>}
      {pinned.isSuccess && (pinned.data.length === 0 ? <p role="status">{t("chatDiscovery.noPinned")}</p> : pinned.data.map(renderMessage))}
    </section>}
  </section>;
}
