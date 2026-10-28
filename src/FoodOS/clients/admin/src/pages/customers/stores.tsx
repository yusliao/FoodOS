import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { Search, Store } from "lucide-react";
import { listStores, searchCustomers, updateStore, type StoreDto } from "@/api/customers";
import { replaceStoreContacts } from "@/api/order-desk";
import { useAuth } from "@/auth/use-auth";
import { OrderingPermissions, InventoryPermissions } from "@/lib/permissions";
import { EntityPageHeader, Field, LoadingRow, ErrorBand } from "@/components/list";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { describe } from "./request-error";
import { StoreCreate } from "./store-create";
import { CustomerPicker } from "./customer-picker";
import { ContactRows } from "./store-contacts";
import { contactDraftsValid, toContactDrafts, toContactInputs, type ContactDraft } from "./store-contacts-model";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export function StoresPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const canViewCustomers = granted.includes(OrderingPermissions.Customers.View);
  const canCreate = granted.includes(OrderingPermissions.Stores.Create);
  const canEdit = granted.includes(OrderingPermissions.Stores.Update);
  const canChoose = canViewCustomers && granted.includes(InventoryPermissions.Warehouses.View);
  const [params, setParams] = useSearchParams();
  const customer = params.get("customerOrgId") ?? "";
  const validFilter = customer === "" || (GUID.test(customer) && customer !== EMPTY_GUID);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<StoreDto | null>(null);
  const [filter, setFilter] = useState("");
  const customers = useQuery({ queryKey: ["customers", ""], queryFn: ({ signal }) => searchCustomers("", signal), enabled: canViewCustomers });
  const stores = useQuery({ queryKey: ["stores", customer], queryFn: ({ signal }) => listStores(customer, signal), enabled: validFilter });
  const customerName = (id: string) => (canViewCustomers && customers.isSuccess ? customers.data.find(c => c.id === id)?.name : null) ?? id;
  const term = filter.trim().toLocaleLowerCase();
  const termDigits = term.replace(/\D/g, "");
  const visible = (stores.data ?? []).filter(store => !term
    || `${store.code} ${store.name} ${store.address}`.toLocaleLowerCase().includes(term)
    || (termDigits.length >= 3 && (store.contacts ?? []).some(contact => contact.phone.replace(/\D/g, "").includes(termDigits))));

  return <div className="space-y-6">
    <EntityPageHeader icon={Store} title={t("partners.stores")} description={t("partners.storesDescription")}>
      {canCreate && <Button disabled={!canChoose || !validFilter} onClick={() => setCreating(true)}>{t("partners.newStore")}</Button>}
    </EntityPageHeader>
    {canCreate && !canChoose && <p role="status" className="text-sm">{t("partners.lookupPermission")}</p>}
    <div className="grid gap-4 md:grid-cols-2">
      {canViewCustomers && customers.isSuccess && <Field id="store-filter" label={t("partners.customer")}>
        <CustomerPicker id="store-filter" label={t("partners.customer")} value={customer} allowAll
          customers={customers.data} onChange={value => setParams(value ? { customerOrgId: value } : {})} />
      </Field>}
      {stores.isSuccess && <Field id="store-search" label={t("partners.searchStores")}>
        <div className="relative">
          <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-[var(--color-muted-foreground)]" />
          <Input id="store-search" type="search" className="pl-9" value={filter} placeholder={t("partners.searchStoresHint")} onChange={event => setFilter(event.target.value)} />
        </div>
      </Field>}
    </div>
    {canViewCustomers && customers.isError && <div className="space-y-2"><ErrorBand message={describe(customers.error, t("partners.requestFailed"))} /><Button variant="outline" onClick={() => void customers.refetch()}>{t("workbench.retry")}</Button></div>}
    {customer && <div className="flex flex-wrap items-center gap-2 text-sm break-all"><span>{t("partners.filtered")}: {customerName(customer)}</span><Button variant="outline" onClick={() => setParams({})}>{t("partners.clearFilter")}</Button></div>}
    {!validFilter && <p role="alert">{t("partners.invalidFilter")}</p>}
    {validFilter && stores.isPending && <LoadingRow label={t("partners.loading")} />}
    {validFilter && stores.isError && <div className="space-y-2"><ErrorBand message={describe(stores.error, t("partners.requestFailed"))} /><Button variant="outline" onClick={() => void stores.refetch()}>{t("workbench.retry")}</Button></div>}
    {validFilter && stores.isSuccess && stores.data.length === 0 && <p role="status">{t("partners.emptyStores")}</p>}
    {validFilter && stores.isSuccess && stores.data.length > 0 && visible.length === 0 && <p role="status">{t("partners.noStoreMatches")}</p>}
    {validFilter && visible.length > 0 && <div className="overflow-x-auto rounded-xl border border-[var(--color-border)] bg-[var(--color-card)]">
      <table className="w-full min-w-[720px] text-left text-sm">
        <thead className="border-b border-[var(--color-border)] text-[var(--color-muted-foreground)]">
          <tr>{["store", "customer", "address", "contacts"].map(column => <th key={column} scope="col" className="px-4 py-3 font-medium">{t(`partners.column.${column}`)}</th>)}
            {canEdit && <th scope="col" className="px-4 py-3"><span className="sr-only">{t("partners.actions")}</span></th>}</tr>
        </thead>
        <tbody className="divide-y divide-[var(--color-border)]">
          {visible.map(store => <tr key={store.id} className="align-top">
            <th scope="row" className="px-4 py-3 font-normal">
              <span className="block break-words font-semibold">{store.name}</span>
              <span className="text-[var(--color-muted-foreground)]">{store.code}</span>
              {!store.customerTenantId && <Badge variant="warning" className="ml-2">{t("partners.unlinked")}</Badge>}
            </th>
            <td className="px-4 py-3 break-words">{customerName(store.customerOrgId)}</td>
            <td className="px-4 py-3">
              <span className="block break-words">{store.address || t("partners.addressPending")}</span>
              {store.deliveryWindow && <span className="text-[var(--color-muted-foreground)]">{t("partners.deliveryWindow")} {store.deliveryWindow}</span>}
            </td>
            <td className="px-4 py-3">
              {(store.contacts ?? []).length === 0
                ? <span className="text-[var(--color-muted-foreground)]">{t("partners.noContacts")}</span>
                : <ul className="space-y-0.5">{(store.contacts ?? []).map(contact => <li key={contact.id} className="break-words">{contact.name} · {contact.phone}</li>)}</ul>}
            </td>
            {canEdit && <td className="px-4 py-3 text-right"><Button size="sm" variant="outline" aria-label={`${t("partners.editStore")} ${store.name}`} onClick={() => setEditing(store)}>{t("partners.editStore")}</Button></td>}
          </tr>)}
        </tbody>
      </table>
    </div>}
    {creating && canCreate && canChoose && <StoreCreate customerOrgId={customer} onClose={() => setCreating(false)} />}
    {editing && <StoreEdit store={editing} onClose={() => setEditing(null)} />}
  </div>;
}

/** Edits a store's name, delivery address and caller contacts. */
function StoreEdit({ store, onClose }: { store: StoreDto; onClose: () => void }) {
  const t = useT();
  const queryClient = useQueryClient();
  const [name, setName] = useState(store.name);
  const [address, setAddress] = useState(store.address);
  const [contacts, setContacts] = useState<ContactDraft[]>(() => toContactDrafts(store.contacts));
  const save = useMutation({
    mutationFn: async () => {
      if (name.trim() !== store.name || address.trim() !== store.address) {
        await updateStore({ id: store.id, name: name.trim(), address: address.trim() });
      }
      await replaceStoreContacts({ storeId: store.id, contacts: toContactInputs(contacts) });
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["stores"] }),
        queryClient.invalidateQueries({ queryKey: ["order-desk", "callers"] }),
      ]);
      toast.success(t("partners.storeSaved"));
      onClose();
    },
  });
  const valid = Boolean(name.trim() && address.trim()) && contactDraftsValid(contacts);
  return <Dialog open onOpenChange={open => { if (!open && !save.isPending) onClose(); }}>
    <DialogContent size="lg">
      <DialogHeader><DialogTitle>{t("partners.editStore")} · {store.code}</DialogTitle>
        <DialogDescription>{t("partners.editStoreHint")}</DialogDescription></DialogHeader>
      <form onSubmit={event => { event.preventDefault(); if (valid && !save.isPending) save.mutate(); }}>
        <DialogBody className="space-y-4">
          <fieldset disabled={save.isPending} className="space-y-4">
            <Field id="edit-store-name" label={t("partners.name")} required><Input id="edit-store-name" value={name} maxLength={128} required onChange={event => setName(event.target.value)} /></Field>
            <Field id="edit-store-address" label={t("partners.address")} required><Input id="edit-store-address" value={address} maxLength={256} required onChange={event => setAddress(event.target.value)} /></Field>
          </fieldset>
          <ContactRows drafts={contacts} onChange={setContacts} disabled={save.isPending} />
          {save.isError && <ErrorBand message={describe(save.error, t("partners.requestFailed"))} />}
        </DialogBody>
        <DialogFooter>
          <Button type="button" variant="outline" disabled={save.isPending} onClick={onClose}>{t("partners.cancel")}</Button>
          <Button type="submit" disabled={!valid || save.isPending}>{t("partners.saveChanges")}</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>;
}
