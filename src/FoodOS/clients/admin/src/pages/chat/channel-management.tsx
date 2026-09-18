import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { addChatMembers, archiveChatChannel, createChatChannel, listChatChannels, removeChatMember, updateChatChannel, type ChatChannel } from "@/api/chat";
import { searchUsers, type UserDto } from "@/api/users";
import { useAuth } from "@/auth/use-auth";
import { ErrorBand, Field, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { ChatPermissions, IdentityPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

const userLabel = (user: UserDto) => [user.firstName, user.lastName].filter(Boolean).join(" ") || user.userName || user.email || user.id;

export function CreateChannelButton() {
  const { user } = useAuth();
  const t = useT();
  const [open, setOpen] = useState(false);
  const allowed = user?.tenant === "root" && user.permissions.includes(ChatPermissions.View) && user.permissions.includes(ChatPermissions.Create);
  if (!allowed) return null;
  return <><Button onClick={() => setOpen(true)}>{t("chatManage.create")}</Button>{open && <ChannelForm mode="create" onClose={() => setOpen(false)} />}</>;
}

function ChannelForm({ mode, channel, onClose }: { mode: "create" | "edit"; channel?: ChatChannel; onClose: () => void }) {
  const t = useT();
  const navigate = useNavigate();
  const cache = useQueryClient();
  const [name, setName] = useState(channel?.name ?? "");
  const [description, setDescription] = useState(channel?.description ?? "");
  const [isPrivate, setPrivate] = useState(channel?.isPrivate ?? false);
  const mutation = useMutation({
    mutationKey: ["chat", "write", channel?.id ?? "channels"],
    mutationFn: async () => mode === "create"
      ? createChatChannel({ name: name.trim(), description: description.trim() || null, isPrivate })
      : updateChatChannel({ channelId: channel!.id, name: name.trim(), description: description.trim() || null, isPrivate }),
    onSuccess: async result => {
      await cache.invalidateQueries({ queryKey: ["chat"] });
      onClose();
      if (mode === "create") navigate(`/chat/${result as string}`);
    },
  });
  const valid = !!name.trim() && name.trim().length <= 200 && description.trim().length <= 2000;
  const submit = (event: FormEvent) => { event.preventDefault(); if (valid && !mutation.isPending) mutation.mutate(); };
  return <Dialog open onOpenChange={value => { if (!value && !mutation.isPending) onClose(); }}><DialogContent>
    <DialogHeader><DialogTitle>{t(mode === "create" ? "chatManage.create" : "chatManage.edit")}</DialogTitle><DialogDescription>{t("chatManage.channelHint")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4">
      <Field id={`chat-channel-name-${mode}`} label={t("chatManage.name")} required><Input id={`chat-channel-name-${mode}`} required maxLength={200} value={name} onChange={event => setName(event.target.value)} /></Field>
      <Field id={`chat-channel-description-${mode}`} label={t("chatManage.description")}><textarea id={`chat-channel-description-${mode}`} maxLength={2000} value={description} onChange={event => setDescription(event.target.value)} className="min-h-24 w-full rounded-lg border bg-transparent p-3" /></Field>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={isPrivate} onChange={event => setPrivate(event.target.checked)} />{t("chatManage.private")}</label>
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("chatManage.failed"))} />}</DialogBody>
    <DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t(mutation.isPending ? "common.working" : "chatManage.save")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}

export function ChannelManagement({ channel }: { channel: ChatChannel }) {
  const { user } = useAuth();
  const t = useT();
  const navigate = useNavigate();
  const cache = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [adding, setAdding] = useState(false);
  const [archiving, setArchiving] = useState(false);
  const [removing, setRemoving] = useState<string | null>(null);
  const me = channel.members.find(member => member.userId === user?.id);
  const admin = me?.role === "Admin";
  const named = channel.type === "Channel";
  const fixedMembers = channel.type === "DirectMessage";
  const canCreate = user?.permissions.includes(ChatPermissions.Create) ?? false;
  const canChoose = user?.permissions.includes(IdentityPermissions.Users.View) ?? false;
  const canEdit = named && admin && canCreate;
  const canAdd = !fixedMembers && (!channel.isPrivate || admin) && canChoose;
  const busy = useIsMutating({ mutationKey: ["chat", "write", channel.id] }) > 0;
  const archive = useMutation({ mutationKey: ["chat", "write", channel.id], mutationFn: archiveChatChannel,
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: ["chat", user?.id] });
      await cache.fetchQuery({ queryKey: ["chat", user?.id, "channels", 1], queryFn: ({ signal }) => listChatChannels(1, signal) });
      navigate("/chat");
    },
  });
  const remove = useMutation({ mutationKey: ["chat", "write", channel.id], mutationFn: removeChatMember,
    onSuccess: async (_, input) => {
      await cache.invalidateQueries({ queryKey: ["chat", user?.id] });
      if (input.userId === user?.id) {
        await cache.fetchQuery({ queryKey: ["chat", user?.id, "channels", 1], queryFn: ({ signal }) => listChatChannels(1, signal) });
        navigate("/chat");
      }
      setRemoving(null);
    },
  });
  const removeMember = channel.members.find(member => member.userId === removing);
  return <section aria-label={t("chatManage.members")} className="space-y-4 rounded-xl border p-4">
    <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{t("chatManage.members")}</h2><div className="flex flex-wrap gap-2">
      {canEdit && <Button variant="outline" disabled={busy} onClick={() => setEditing(true)}>{t("chatManage.edit")}</Button>}
      {canAdd && <Button variant="outline" disabled={busy} onClick={() => setAdding(true)}>{t("chatManage.add")}</Button>}
      {canEdit && <Button variant="destructive" disabled={busy} onClick={() => { archive.reset(); setArchiving(true); }}>{t("chatManage.archive")}</Button>}
    </div></div>
    {!fixedMembers && !canChoose && <p role="status" className="text-sm">{t("chatManage.lookupRequired")}</p>}
    <ul className="space-y-2">{channel.members.map(member => { const self = member.userId === user?.id; const canRemove = !fixedMembers && (self || admin); return <li key={member.id ?? member.userId} className="flex min-w-0 flex-wrap items-center justify-between gap-2 rounded-lg border p-3"><span className="break-all text-sm">{member.userId} · {t(`chatManage.roles.${member.role}`)}{self ? ` · ${t("chatManage.you")}` : ""}</span>{canRemove && <Button size="sm" variant="outline" disabled={busy} onClick={() => { remove.reset(); setRemoving(member.userId); }}>{t(self ? "chatManage.leave" : "chatManage.remove")}</Button>}</li>; })}</ul>
    {editing && canEdit && <ChannelForm mode="edit" channel={channel} onClose={() => setEditing(false)} />}
    {adding && canAdd && <AddMembersDialog channel={channel} onClose={() => setAdding(false)} />}
    <ConfirmDialog open={archiving && canEdit} title={t("chatManage.archive")} description={<>{t("chatManage.archiveHint")}{archive.isError && <span role="alert" className="mt-3 block">{describe(archive.error, t("chatManage.failed"))}</span>}</>} confirmLabel={t("chatManage.archive")} destructive pending={busy}
      onOpenChange={value => { if (!value && !busy) { setArchiving(false); archive.reset(); } }} onConfirm={() => { if (canEdit && !busy) archive.mutate(channel.id); }} />
    <ConfirmDialog open={!!removeMember} title={t(removing === user?.id ? "chatManage.leave" : "chatManage.remove")} description={<>{t("chatManage.removeHint")}{remove.isError && <span role="alert" className="mt-3 block">{describe(remove.error, t("chatManage.failed"))}</span>}</>} confirmLabel={t(removing === user?.id ? "chatManage.leave" : "chatManage.remove")} destructive pending={busy}
      onOpenChange={value => { if (!value && !busy) { setRemoving(null); remove.reset(); } }} onConfirm={() => { if (removing && !busy) remove.mutate({ channelId: channel.id, userId: removing }); }} />
  </section>;
}

function AddMembersDialog({ channel, onClose }: { channel: ChatChannel; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<UserDto[]>([]);
  const existing = new Set(channel.members.map(member => member.userId));
  const users = useQuery({ queryKey: ["chat", "member-users", search.trim(), page], queryFn: ({ signal }) => searchUsers({ search, pageNumber: page, pageSize: 10, isActive: true }, signal) });
  const mutation = useMutation({ mutationKey: ["chat", "write", channel.id], mutationFn: addChatMembers,
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["chat"] }); onClose(); },
  });
  const toggle = (candidate: UserDto) => setSelected(value => value.some(item => item.id === candidate.id) ? value.filter(item => item.id !== candidate.id) : [...value, candidate]);
  return <Dialog open onOpenChange={value => { if (!value && !mutation.isPending) onClose(); }}><DialogContent>
    <DialogHeader><DialogTitle>{t("chatManage.add")}</DialogTitle><DialogDescription>{t("chatManage.addHint")}</DialogDescription></DialogHeader>
    <DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="min-w-0 space-y-4">
      <Field id="chat-member-search" label={t("chatManage.search")}><Input id="chat-member-search" type="search" value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} /></Field>
      {selected.length > 0 && <p className="break-words text-sm">{t("chatManage.selected")}: {selected.map(userLabel).join(", ")}</p>}
      {users.isPending && <LoadingRow label={t("common.loading")} />}
      {users.isError && <><ErrorBand message={describe(users.error, t("chatManage.failed"))} /><Button type="button" variant="outline" onClick={() => void users.refetch()}>{t("workbench.retry")}</Button></>}
      {users.isSuccess && <>{users.data.items.filter(user => !existing.has(user.id)).length === 0 && <p role="status">{t("chatManage.noUsers")}</p>}<div className="max-h-48 space-y-2 overflow-y-auto">{users.data.items.filter(user => !existing.has(user.id)).map(candidate => <Button className="h-auto w-full justify-start whitespace-normal text-left break-words" type="button" variant="outline" key={candidate.id} aria-pressed={selected.some(user => user.id === candidate.id)} onClick={() => toggle(candidate)}>{userLabel(candidate)} · {candidate.id}</Button>)}</div><Pagination page={page} totalPages={users.data.totalPages} totalCount={users.data.totalCount} shown={users.data.items.length} hasPrev={users.data.hasPrevious} hasNext={users.data.hasNext} fetching={users.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} /></>}
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("chatManage.failed"))} />}</DialogBody>
    <DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button disabled={selected.length === 0 || mutation.isPending} onClick={() => mutation.mutate({ channelId: channel.id, userIds: selected.map(user => user.id) })}>{t(mutation.isPending ? "common.working" : "chatManage.add")}</Button></DialogFooter>
  </DialogContent></Dialog>;
}
