import { apiFetch } from "@/lib/api-client";

export const CHAT_PAGE_SIZE = 20;
export type ChatChannel = {
  id: string; type: "DirectMessage" | "GroupMessage" | "Channel";
  name?: string | null; description?: string | null; isPrivate: boolean; unreadCount: number;
  members: { id: string; userId: string; role: "Member" | "Admin"; joinedAtUtc: string; lastReadMessageId?: string | null }[];
};
export type ChatMessage = {
  id: string; channelId: string; authorUserId: string; body?: string | null;
  parentMessageId?: string | null; replyCount: number; createdAtUtc: string;
  editedAtUtc?: string | null; deletedAtUtc?: string | null; isPinned?: boolean;
  attachments: { id: string; fileAssetId?: string | null; url: string; originalFileName: string; contentType: string; sizeBytes: number }[];
  reactions: { id: string; userId: string; emoji: string }[];
};
const base = "/api/v1/chat";
export function editChatMessage(input: { messageId: string; body: string }) {
  return apiFetch<void>(`${base}/messages/${encodeURIComponent(input.messageId)}`, { method: "PUT", body: JSON.stringify({ body: input.body }) });
}
export function deleteChatMessage(messageId: string) {
  return apiFetch<void>(`${base}/messages/${encodeURIComponent(messageId)}`, { method: "DELETE" });
}
export function markChatRead(input: { channelId: string; messageId: string }) {
  return apiFetch<void>(`${base}/channels/${encodeURIComponent(input.channelId)}/read`, {
    method: "POST", body: JSON.stringify({ messageId: input.messageId }),
  });
}
export function sendChatMessage(input: { channelId: string; parentMessageId: string | null; body: string; key: string }) {
  return apiFetch<ChatMessage>(`${base}/channels/${encodeURIComponent(input.channelId)}/messages`, {
    method: "POST", headers: { "Idempotency-Key": input.key },
    body: JSON.stringify({ body: input.body, parentMessageId: input.parentMessageId, attachments: [] }),
  });
}
export function listChatChannels(page: number, signal?: AbortSignal) {
  return apiFetch<ChatChannel[]>(`${base}/channels?page=${page}&pageSize=${CHAT_PAGE_SIZE}`, { signal });
}
export function getChatChannel(id: string, signal?: AbortSignal) {
  return apiFetch<ChatChannel>(`${base}/channels/${encodeURIComponent(id)}`, { signal });
}
export function createChatChannel(input: { name: string; description: string | null; isPrivate: boolean }) {
  return apiFetch<string>(`${base}/channels`, { method: "POST", body: JSON.stringify(input) });
}
export function updateChatChannel(input: { channelId: string; name: string; description: string | null; isPrivate: boolean }) {
  return apiFetch<void>(`${base}/channels/${encodeURIComponent(input.channelId)}`, { method: "PUT", body: JSON.stringify(input) });
}
export function archiveChatChannel(channelId: string) {
  return apiFetch<void>(`${base}/channels/${encodeURIComponent(channelId)}`, { method: "DELETE" });
}
export function addChatMembers(input: { channelId: string; userIds: string[] }) {
  return apiFetch<void>(`${base}/channels/${encodeURIComponent(input.channelId)}/members`, {
    method: "POST", body: JSON.stringify(input),
  });
}
export function removeChatMember(input: { channelId: string; userId: string }) {
  return apiFetch<void>(`${base}/channels/${encodeURIComponent(input.channelId)}/members/${encodeURIComponent(input.userId)}`, { method: "DELETE" });
}
export function getChatMessage(channelId: string, messageId: string, signal?: AbortSignal) {
  return apiFetch<ChatMessage>(`${base}/channels/${encodeURIComponent(channelId)}/messages/${encodeURIComponent(messageId)}`, { signal });
}
export function listChatMessages(channelId: string, before: string | undefined, signal?: AbortSignal) {
  const query = new URLSearchParams({ pageSize: String(CHAT_PAGE_SIZE) });
  if (before) query.set("before", before);
  return apiFetch<ChatMessage[]>(`${base}/channels/${encodeURIComponent(channelId)}/messages?${query}`, { signal });
}
export function listChatReplies(messageId: string, before: string | undefined, signal?: AbortSignal) {
  const query = new URLSearchParams({ pageSize: String(CHAT_PAGE_SIZE) });
  if (before) query.set("before", before);
  return apiFetch<ChatMessage[]>(`${base}/messages/${encodeURIComponent(messageId)}/replies?${query}`, { signal });
}
