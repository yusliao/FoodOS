import { useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useIsMutating, useQuery } from "@tanstack/react-query";
import { MessageSquare } from "lucide-react";
import { CHAT_PAGE_SIZE, getChatChannel, getChatMessage, listChatChannels, listChatMessages, listChatReplies, type ChatChannel, type ChatMessage } from "@/api/chat";
import { useAuth } from "@/auth/use-auth";
import { ChatPermissions } from "@/lib/permissions";
import { useLocale } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { EntityPageHeader, ErrorBand, LoadingRow } from "@/components/list";
import { describe } from "@/pages/customers/request-error";
import { ChatComposer } from "./composer";
import { MessageActions } from "./message-actions";
import { ReadAction } from "./read-action";
import { ChannelManagement, CreateChannelButton } from "./channel-management";
import { ArchivedChannelsButton } from "./archived-channels";
import { ChatAttachmentDownload } from "./attachment-download";

const validId = (value: string) => /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value) && !/^0{8}(-0{4}){3}-0{12}$/.test(value);
function useChatAccess() {
  const { user } = useAuth();
  return { userId: user?.id, allowed: user?.tenant === "root" && user.permissions.includes(ChatPermissions.View) };
}
function Failure({ error, retry, pending }: { error: unknown; retry: () => void; pending: boolean }) {
  const { t } = useLocale();
  return <div className="space-y-2"><ErrorBand message={describe(error, t("chat.failed"))} /><Button variant="outline" onClick={retry} disabled={pending}>{t("workbench.retry")}</Button></div>;
}
function ChannelName({ channel }: { channel: ChatChannel }) {
  const { t } = useLocale();
  return <>{channel.name || `${t(`chat.types.${channel.type}`)} · ${channel.id}`}</>;
}
export function ChatPage() {
  const { t } = useLocale();
  const { allowed, userId } = useChatAccess();
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ["chat", userId, "channels", page], queryFn: ({ signal }) => listChatChannels(page, signal), enabled: allowed });
  return <div className="space-y-6">
    <EntityPageHeader icon={MessageSquare} title={t("chat.title")} description={t("chat.description")}><div className="flex flex-wrap gap-2"><ArchivedChannelsButton /><CreateChannelButton /></div></EntityPageHeader>
    {query.isPending && <LoadingRow label={t("common.loading")} />}
    {query.isError && <Failure error={query.error} retry={() => { if (allowed) void query.refetch(); }} pending={query.isFetching} />}
    {query.isSuccess && <>
      {query.data.length === 0 && <p role="status">{t("chat.noChannels")}</p>}
      <div className="grid gap-4 md:grid-cols-2">{query.data.map(channel => <article key={channel.id} className="min-w-0 space-y-2 rounded-xl border p-4">
        <h2 className="break-words font-semibold"><Link to={`/chat/${channel.id}`} className="underline"><ChannelName channel={channel} /></Link></h2>
        <p className="text-sm">{t(channel.isPrivate ? "chat.private" : "chat.public")} · {t("chat.unread")}: {channel.unreadCount}</p>
        {channel.description && <p className="whitespace-pre-wrap break-words">{channel.description}</p>}
      </article>)}</div>
    </>}
    <div className="flex flex-wrap items-center gap-3">
      <Button variant="outline" disabled={page === 1 || query.isFetching} onClick={() => setPage(value => value - 1)}>{t("common.previous")}</Button>
      <span>{page}</span>
      <Button variant="outline" disabled={!query.isSuccess || query.data.length < CHAT_PAGE_SIZE || query.isFetching} onClick={() => setPage(value => value + 1)}>{t("common.next")}</Button>
    </div>
  </div>;
}
export function ChatChannelPage() {
  const { channelId = "" } = useParams();
  const { t } = useLocale();
  const { allowed, userId } = useChatAccess();
  const valid = validId(channelId);
  const query = useQuery({ queryKey: ["chat", userId, "channel", channelId], queryFn: ({ signal }) => getChatChannel(channelId, signal), enabled: allowed && valid });
  const member = query.data?.members.some(value => value.userId === userId);
  return <div className="min-w-0 space-y-6">
    <Link to="/chat" className="underline">{t("chat.back")}</Link>
    {!valid ? <ErrorBand message={t("chat.invalidId")} /> : <>
      {query.isPending && <LoadingRow label={t("common.loading")} />}
      {query.isError && <Failure error={query.error} retry={() => { if (allowed) void query.refetch(); }} pending={query.isFetching} />}
      {query.isSuccess && (!member ? <ErrorBand message={t("chat.notMember")} /> : <ChannelContent key={`${userId}:${channelId}`} channel={query.data} />)}
    </>}
  </div>;
}
function ChannelContent({ channel }: { channel: ChatChannel }) {
  const { t } = useLocale();
  const busy = useIsMutating({ mutationKey: ["chat", "write", channel.id] }) > 0;
  const [search, setSearch] = useSearchParams();
  const target = search.get("messageId");
  return <>
    <h1 className="break-words text-2xl font-semibold"><ChannelName channel={channel} /></h1>
    <p className="whitespace-pre-wrap break-words">{channel.description}</p>
    <p className="text-sm">{t("chat.unread")}: {channel.unreadCount ?? 0}</p>
    <p className="text-sm text-[var(--color-muted-foreground)]">{t("chatCompose.browsingHint")}</p>
    <ChannelManagement channel={channel} />
    <ChatComposer channelId={channel.id} />
    {target !== null && <section aria-label={t("chat.target")} className="space-y-4 rounded-xl border p-4">
      <div className="flex flex-wrap items-center justify-between gap-2"><h2 className="font-semibold">{t("chat.target")}</h2><Button variant="outline" disabled={busy} onClick={() => setSearch(previous => { const next = new URLSearchParams(previous); next.delete("messageId"); return next; })}>{t("chat.closeTarget")}</Button></div>
      {!validId(target) ? <ErrorBand message={t("chat.invalidId")} /> : <TargetMessage key={target} channelId={channel.id} messageId={target} />}
    </section>}
    <MessagePages channelId={channel.id} />
  </>;
}
function TargetMessage({ channelId, messageId }: { channelId: string; messageId: string }) {
  const { t } = useLocale();
  const { allowed, userId } = useChatAccess();
  const query = useQuery({ queryKey: ["chat", userId, "message", channelId, messageId], queryFn: ({ signal }) => getChatMessage(channelId, messageId, signal), enabled: allowed });
  return <>
    {query.isPending && <LoadingRow label={t("common.loading")} />}
    {query.isError && <Failure error={query.error} retry={() => { if (allowed) void query.refetch(); }} pending={query.isFetching} />}
    {query.isSuccess && <>
      <MessageCard message={query.data} />
      {query.data.parentMessageId && <ParentMessage channelId={channelId} messageId={query.data.parentMessageId} />}
      <MessagePages key={query.data.parentMessageId ?? messageId} channelId={channelId} parentId={query.data.parentMessageId ?? messageId} />
      <ChatComposer channelId={channelId} parentMessageId={query.data.parentMessageId ?? messageId} />
    </>}
  </>;
}
function ParentMessage({ channelId, messageId }: { channelId: string; messageId: string }) {
  const { t } = useLocale();
  const { allowed, userId } = useChatAccess();
  const query = useQuery({ queryKey: ["chat", userId, "message", channelId, messageId], queryFn: ({ signal }) => getChatMessage(channelId, messageId, signal), enabled: allowed });
  return <section aria-label={t("chat.parent")} className="space-y-3">
    <h3 className="font-semibold">{t("chat.parent")}</h3>
    {query.isPending && <LoadingRow label={t("common.loading")} />}
    {query.isError && <Failure error={query.error} retry={() => { if (allowed) void query.refetch(); }} pending={query.isFetching} />}
    {query.isSuccess && <MessageCard message={query.data} />}
  </section>;
}
function MessagePages({ channelId, parentId }: { channelId: string; parentId?: string }) {
  const { t } = useLocale();
  const { allowed, userId } = useChatAccess();
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const before = cursors.at(-1);
  const query = useQuery({ queryKey: ["chat", userId, "messages", channelId, parentId, before], queryFn: ({ signal }) => parentId ? listChatReplies(parentId, before, signal) : listChatMessages(channelId, before, signal), enabled: allowed });
  const label = t(parentId ? "chat.replies" : "chat.messages");
  return <section aria-label={label} className="min-w-0 space-y-4">
    <h2 className="font-semibold">{label}</h2>
    {query.isPending && <LoadingRow label={t("common.loading")} />}
    {query.isError && <Failure error={query.error} retry={() => { if (allowed) void query.refetch(); }} pending={query.isFetching} />}
    {query.isSuccess && (query.data.length === 0 ? <p role="status">{t("chat.noMessages")}</p> : query.data.map(message => <MessageCard key={message.id} message={message} openThread={!parentId} />))}
    <div className="flex flex-wrap gap-3">
      <Button variant="outline" disabled={cursors.length === 1 || query.isFetching} onClick={() => setCursors(value => value.slice(0, -1))}>{t("chat.newer")}</Button>
      <Button variant="outline" disabled={!query.isSuccess || query.data.length < CHAT_PAGE_SIZE || query.isFetching} onClick={() => { const next = query.data?.at(-1)?.id; if (next && next !== before) setCursors(value => [...value, next]); }}>{t("chat.older")}</Button>
    </div>
  </section>;
}
function MessageCard({ message, openThread = false }: { message: ChatMessage; openThread?: boolean }) {
  const { t, culture } = useLocale();
  const [showDeletedThread, setShowDeletedThread] = useState(false);
  const busy = useIsMutating({ mutationKey: ["chat", "write", message.channelId] }) > 0;
  return <article className="min-w-0 space-y-2 rounded-lg border p-3">
    <p className="break-all text-sm text-[var(--color-muted-foreground)]">{message.authorUserId} · {new Intl.DateTimeFormat(culture, { dateStyle: "medium", timeStyle: "short" }).format(new Date(message.createdAtUtc))}</p>
    {message.deletedAtUtc ? <p>{t("chat.deleted")}</p> : <>
      <p className="whitespace-pre-wrap break-words [overflow-wrap:anywhere]">{message.body}</p>
      {message.editedAtUtc && <span className="text-sm">{t("chat.edited")}</span>}
      {message.isPinned && <span className="text-sm"> · {t("chat.pinned")}</span>}
      {message.attachments.length > 0 && <div className="space-y-2 text-sm"><p>{t("chat.attachmentHint")}</p><ul className="grid gap-2 sm:grid-cols-2">{message.attachments.map(file => <ChatAttachmentDownload key={file.id} attachment={file} />)}</ul></div>}
      {message.reactions.length > 0 && <p className="break-words">{message.reactions.map(reaction => reaction.emoji).join(" ")}</p>}
      <MessageActions message={message} />
    </>}
    <ReadAction message={message} />
    {openThread && !message.deletedAtUtc && <Link aria-disabled={busy} onClick={event => { if (busy) event.preventDefault(); }} className="inline-block underline" to={`/chat/${message.channelId}?messageId=${message.id}`}>{t("chat.openThread")} ({message.replyCount})</Link>}
    {openThread && message.deletedAtUtc && message.replyCount > 0 && <>
      <Button variant="outline" disabled={busy} aria-expanded={showDeletedThread} onClick={() => setShowDeletedThread(value => !value)}>{t("chat.openThread")} ({message.replyCount})</Button>
      {showDeletedThread && <MessagePages channelId={message.channelId} parentId={message.id} />}
    </>}
  </article>;
}
