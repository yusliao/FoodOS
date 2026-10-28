import { Plus, Trash2 } from "lucide-react";
import { digits, MAX_CONTACTS, newContactKey, type ContactDraft } from "./store-contacts-model";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";

/** Caller contacts (name + phone) the customer service order desk uses to recognise a store. */
export function ContactRows({ drafts, onChange, disabled }: {
  drafts: ContactDraft[];
  onChange: (drafts: ContactDraft[]) => void;
  disabled?: boolean;
}) {
  const t = useT();
  const update = (key: number, change: Partial<ContactDraft>) =>
    onChange(drafts.map(draft => draft.key === key ? { ...draft, ...change } : draft));
  const duplicate = new Set(drafts.map(draft => digits(draft.phone)).filter((number, index, all) => number && all.indexOf(number) !== index));
  return <fieldset className="space-y-2" disabled={disabled}>
    <legend className="text-sm font-medium">{t("partners.contacts")}</legend>
    <p className="text-xs text-[var(--color-muted-foreground)]">{t("partners.contactsHint")}</p>
    {drafts.length === 0 && <p className="text-sm text-[var(--color-muted-foreground)]">{t("partners.noContacts")}</p>}
    {drafts.map((draft, index) => <div key={draft.key} className="flex flex-wrap items-center gap-2">
      <Input className="min-w-0 flex-1" aria-label={`${t("partners.contactName")} ${index + 1}`} placeholder={t("partners.contactName")}
        value={draft.name} maxLength={100} onChange={event => update(draft.key, { name: event.target.value })} />
      <Input className="min-w-0 flex-1" type="tel" aria-label={`${t("partners.contactPhone")} ${index + 1}`} placeholder={t("partners.contactPhone")}
        aria-invalid={duplicate.has(digits(draft.phone)) || undefined}
        value={draft.phone} maxLength={40} onChange={event => update(draft.key, { phone: event.target.value })} />
      <Button type="button" size="icon-xs" variant="ghost" aria-label={`${t("partners.removeContact")} ${index + 1}`}
        onClick={() => onChange(drafts.filter(item => item.key !== draft.key))}><Trash2 /></Button>
    </div>)}
    {duplicate.size > 0 && <p role="alert" className="text-xs text-[var(--color-destructive)]">{t("partners.duplicatePhone")}</p>}
    <Button type="button" size="sm" variant="outline" disabled={drafts.length >= MAX_CONTACTS}
      onClick={() => onChange([...drafts, { key: newContactKey(), name: "", phone: "" }])}><Plus />{t("partners.addContact")}</Button>
  </fieldset>;
}
