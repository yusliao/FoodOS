import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Plus, Trash2 } from "lucide-react";
import { getStoreContacts, replaceStoreContacts } from "@/api/order-desk";
import { ErrorBand, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { describe } from "./request-error";

type Draft = { key: number; name: string; phone: string };

/** Caller contacts the phone order desk uses to recognise a store. */
export function StoreContacts({ storeId, canEdit }: { storeId: string; canEdit: boolean }) {
  const t = useT();
  const queryClient = useQueryClient();
  const queryKey = ["stores", storeId, "contacts"] as const;
  const contacts = useQuery({ queryKey, queryFn: ({ signal }) => getStoreContacts(storeId, signal) });
  const [drafts, setDrafts] = useState<Draft[] | null>(null);
  const save = useMutation({
    mutationFn: replaceStoreContacts,
    onSuccess: async saved => {
      queryClient.setQueryData(queryKey, saved);
      await queryClient.invalidateQueries({ queryKey: ["order-desk", "callers"] });
      setDrafts(null);
      toast.success(t("partners.contactsSaved"));
    },
  });
  const startEditing = () => {
    save.reset();
    setDrafts((contacts.data ?? []).map((contact, index) => ({ key: index, name: contact.name, phone: contact.phone })));
  };
  const update = (key: number, change: Partial<Draft>) =>
    setDrafts(current => current?.map(draft => draft.key === key ? { ...draft, ...change } : draft) ?? null);
  const validDrafts = drafts?.every(draft => draft.name.trim() && draft.phone.replace(/\D/g, "").length >= 5) ?? false;

  return <div className="space-y-2 border-t border-[var(--color-border)] pt-3 text-sm">
    <p className="font-medium">{t("partners.contacts")}</p>
    {contacts.isPending && <LoadingRow label={t("partners.loading")} />}
    {contacts.isError && <ErrorBand message={describe(contacts.error, t("partners.requestFailed"))} />}
    {contacts.isSuccess && drafts === null && <>
      {contacts.data.length === 0
        ? <p className="text-[var(--color-muted-foreground)]">{t("partners.noContacts")}</p>
        : <ul className="space-y-1">{contacts.data.map(contact => <li key={contact.id} className="break-words">{contact.name} · {contact.phone}</li>)}</ul>}
      {canEdit && <Button size="sm" variant="outline" onClick={startEditing}>{t("partners.editContacts")}</Button>}
    </>}
    {drafts !== null && <form className="space-y-2" onSubmit={event => {
      event.preventDefault();
      if (!validDrafts) return;
      save.mutate({ storeId, contacts: drafts.map(draft => ({ name: draft.name.trim(), phone: draft.phone.trim() })) });
    }}>
      <p className="text-xs text-[var(--color-muted-foreground)]">{t("partners.contactsHint")}</p>
      {drafts.map((draft, index) => <div key={draft.key} className="flex flex-wrap items-center gap-2">
        <Input className="min-w-0 flex-1" aria-label={`${t("partners.contactName")} ${index + 1}`} placeholder={t("partners.contactName")}
          value={draft.name} maxLength={100} required onChange={event => update(draft.key, { name: event.target.value })} />
        <Input className="min-w-0 flex-1" type="tel" aria-label={`${t("partners.contactPhone")} ${index + 1}`} placeholder={t("partners.contactPhone")}
          value={draft.phone} maxLength={40} required onChange={event => update(draft.key, { phone: event.target.value })} />
        <Button type="button" size="icon-xs" variant="ghost" aria-label={`${t("partners.removeContact")} ${index + 1}`}
          onClick={() => setDrafts(current => current?.filter(item => item.key !== draft.key) ?? null)}><Trash2 /></Button>
      </div>)}
      {save.isError && <ErrorBand message={describe(save.error, t("partners.requestFailed"))} />}
      <div className="flex flex-wrap gap-2">
        <Button type="button" size="sm" variant="outline" disabled={drafts.length >= 10}
          onClick={() => setDrafts(current => [...(current ?? []), { key: Date.now(), name: "", phone: "" }])}><Plus />{t("partners.addContact")}</Button>
        <Button type="submit" size="sm" disabled={!validDrafts || save.isPending}>{t("partners.saveContacts")}</Button>
        <Button type="button" size="sm" variant="ghost" onClick={() => setDrafts(null)}>{t("partners.cancel")}</Button>
      </div>
    </form>}
  </div>;
}
