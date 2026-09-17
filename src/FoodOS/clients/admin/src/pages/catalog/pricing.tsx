import { useMemo, useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BadgeDollarSign } from "lucide-react";
import { toast } from "sonner";
import {
  createPriceList,
  getPriceLists,
  quoteProductPrice,
  searchProducts,
  upsertPriceListLine,
  upsertPriceLock,
  type PriceListDto,
} from "@/api/catalog";
import { searchCustomers, type CustomerOrgDto } from "@/api/customers";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import {
  Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter,
  DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { CatalogPermissions, OrderingPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

const priceKey = ["catalog", "price-lists"] as const;
import { CatalogChoice } from "./catalog-choice";

export function PricingPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const [customerFilter, setCustomerFilter] = useState("");
  const [creating, setCreating] = useState(false);
  const [editingLines, setEditingLines] = useState<PriceListDto | null>(null);
  const [locking, setLocking] = useState(false);
  const canCreate = granted.includes(CatalogPermissions.PriceLists.Create);
  const canUpdate = granted.includes(CatalogPermissions.PriceLists.Update);
  const canReadProducts = granted.includes(CatalogPermissions.Products.View);
  const canReadCustomers = granted.includes(OrderingPermissions.Customers.View);
  const lists = useQuery({
    queryKey: [...priceKey, customerFilter],
    queryFn: ({ signal }) => getPriceLists(customerFilter, signal),
  });
  const customers = useQuery({
    queryKey: ["customers", "pricing"],
    queryFn: ({ signal }) => searchCustomers("", signal),
    enabled: canReadCustomers,
  });
  const products = useQuery({
    queryKey: ["catalog", "products", "choice", "", 1],
    queryFn: ({ signal }) => searchProducts({ pageSize: 50 }, signal),
    enabled: canReadProducts,
  });
  const customerItems = useMemo(() => customers.isSuccess ? customers.data : [], [customers.data, customers.isSuccess]);
  const productItems = useMemo(() => products.isSuccess ? products.data.items : [], [products.data, products.isSuccess]);
  const customerNames = useMemo(() => new Map(customerItems.map(item => [item.id, item.name])), [customerItems]);
  const productNames = useMemo(() => new Map(productItems.map(item => [item.id, item.name])), [productItems]);
  const canCreateCustomerPrice = canCreate && canReadCustomers;
  const canEditTiers = canUpdate && canReadProducts;
  const canLock = canUpdate && canReadCustomers && canReadProducts;
  const canQuote = canReadCustomers && canReadProducts;

  return <div className="space-y-6">
    <EntityPageHeader icon={BadgeDollarSign} title={t("pricing.title")} description={t("pricing.description")}>
      <div className="flex flex-wrap gap-2">
        {canCreate && <Button disabled={canReadCustomers && !customers.isSuccess} onClick={() => setCreating(true)}>{t("pricing.newList")}</Button>}
        {canLock && <Button variant="outline" disabled={!customers.isSuccess} onClick={() => setLocking(true)}>{t("pricing.setLock")}</Button>}
      </div>
    </EntityPageHeader>

    {canReadCustomers && <label className="block max-w-md space-y-1 text-sm"><span>{t("pricing.customerFilter")}</span>
      <select className="h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-card)] px-3" value={customerFilter} onChange={event => setCustomerFilter(event.target.value)}>
        <option value="">{t("pricing.allLists")}</option>
        {customerItems.map(customer => <option key={customer.id} value={customer.id}>{customer.code} · {customer.name}</option>)}
      </select>
    </label>}
    {canReadCustomers && customers.isFetching && <LoadingRow label={t("common.loading")} />}
    {canReadCustomers && customers.isError && <section aria-label={t("pricing.customersFailed")} className="space-y-2"><ErrorBand message={describe(customers.error, t("pricing.customersFailed"))} /><Button disabled={customers.isFetching} variant="outline" onClick={() => { if (canReadCustomers) void customers.refetch(); }}>{t("workbench.retry")}</Button></section>}
    {canReadProducts && products.isError && <section aria-label={t("pricing.productsFailed")} className="space-y-2"><ErrorBand message={describe(products.error, t("pricing.productsFailed"))} /><Button disabled={products.isFetching} variant="outline" onClick={() => { if (canReadProducts) void products.refetch(); }}>{t("workbench.retry")}</Button></section>}

    {lists.isPending && <LoadingRow label={t("pricing.loading")} />}
    {lists.isError && <div className="space-y-2"><ErrorBand message={describe(lists.error, t("pricing.loadFailed"))} /><Button variant="outline" onClick={() => void lists.refetch()}>{t("workbench.retry")}</Button></div>}
    {lists.isSuccess && lists.data.length === 0 && <p role="status">{t("pricing.empty")}</p>}
    {lists.isSuccess && <div className="grid gap-4 xl:grid-cols-2">{lists.data.map(list =>
      <article key={list.id} className="space-y-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5">
        <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-start"><div><h2 className="font-semibold break-words">{list.name}</h2><p className="text-sm text-[var(--color-muted-foreground)]">{list.customerOrgId ? customerNames.get(list.customerOrgId) ?? list.customerOrgId : t("pricing.global")}</p></div><span className="rounded-full border px-2 py-1 text-xs">{t("pricing.priority")} {list.priority}</span></div>
        <p className="text-sm text-[var(--color-muted-foreground)]">{formatDate(list.validFrom)} – {list.validTo ? formatDate(list.validTo) : t("pricing.noEnd")}</p>
        {list.lines.length === 0 ? <p className="text-sm">{t("pricing.noLines")}</p> : <>
          <dl className="space-y-3 sm:hidden">{list.lines.map(line => <div key={line.id} className="grid grid-cols-2 gap-2 rounded-lg border border-[var(--color-border)] p-3 text-sm"><dt className="col-span-2 text-[var(--color-muted-foreground)]">{t("pricing.product")}</dt><dd className="col-span-2 break-all">{productNames.get(line.productId) ?? line.productId}</dd><dt className="text-[var(--color-muted-foreground)]">{t("pricing.minQty")}</dt><dt className="text-[var(--color-muted-foreground)]">{t("pricing.unitPrice")}</dt><dd>{line.minQty}</dd><dd>{formatMoney(line.unitPrice, line.currency)}</dd></div>)}</dl>
          <div className="hidden overflow-x-auto sm:block"><table className="w-full min-w-[28rem] text-left text-sm"><thead><tr className="border-b"><th className="py-2">{t("pricing.product")}</th><th>{t("pricing.minQty")}</th><th>{t("pricing.unitPrice")}</th></tr></thead><tbody>{list.lines.map(line => <tr key={line.id} className="border-b border-[var(--color-border)]"><td className="py-2">{productNames.get(line.productId) ?? line.productId}</td><td>{line.minQty}</td><td>{formatMoney(line.unitPrice, line.currency)}</td></tr>)}</tbody></table></div>
        </>}
        {canEditTiers && <Button size="sm" variant="outline" onClick={() => setEditingLines(list)}>{t("pricing.addOrReplaceTier")}</Button>}
      </article>)}</div>}

    {!canReadCustomers && (canCreate || canUpdate) && <p className="text-sm text-[var(--color-muted-foreground)]">{t("pricing.customerPermissionHint")}</p>}
    {!canReadProducts && canUpdate && <p className="text-sm text-[var(--color-muted-foreground)]">{t("pricing.productPermissionHint")}</p>}
    {canQuote && <QuotePanel customers={customerItems} />}
    <section className="rounded-xl border border-dashed border-[var(--color-border)] p-4 text-sm text-[var(--color-muted-foreground)]"><h2 className="font-semibold text-[var(--color-foreground)]">{t("pricing.boundariesTitle")}</h2><p>{t("pricing.boundaries")}</p></section>

    {creating && canCreate && <CreatePriceListDialog customers={canCreateCustomerPrice ? customerItems : []} onClose={() => setCreating(false)} />}
    {editingLines && canEditTiers && <PriceTierDialog list={editingLines} onClose={() => setEditingLines(null)} />}
    {locking && canLock && <PriceLockDialog customers={customerItems} onClose={() => setLocking(false)} />}
  </div>;
}

function CreatePriceListDialog({ customers, onClose }: { customers: CustomerOrgDto[]; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [name, setName] = useState("");
  const [customerOrgId, setCustomerOrgId] = useState("");
  const [priority, setPriority] = useState("0");
  const [validFrom, setValidFrom] = useState(toLocalInput(new Date()));
  const [validTo, setValidTo] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({
    mutationFn: ({ input, key }: { input: Parameters<typeof createPriceList>[0]; key: string }) => createPriceList(input, key),
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: priceKey }); toast.success(t("pricing.created")); onClose(); },
  });
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!name.trim() || !validFrom || mutation.isPending) return;
    const input = { name: name.trim(), customerOrgId: customerOrgId || null, priority: Number(priority), validFrom: new Date(validFrom).toISOString(), validTo: validTo ? new Date(validTo).toISOString() : null };
    const body = JSON.stringify(input);
    if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() };
    mutation.mutate({ input, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent><DialogHeader><DialogTitle>{t("pricing.newList")}</DialogTitle><DialogDescription>{t("pricing.createHint")}</DialogDescription></DialogHeader><form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4"><Field id="price-list-name" label={t("pricing.name")} required><Input id="price-list-name" required value={name} onChange={event => setName(event.target.value)} /></Field>{customers.length > 0 && <Field id="price-list-customer" label={t("pricing.customer")}><select id="price-list-customer" className="h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-card)] px-3" value={customerOrgId} onChange={event => setCustomerOrgId(event.target.value)}><option value="">{t("pricing.global")}</option>{customers.map(customer => <option key={customer.id} value={customer.id}>{customer.code} · {customer.name}</option>)}</select></Field>}<Field id="price-list-priority" label={t("pricing.priority")} required><Input id="price-list-priority" type="number" required value={priority} onChange={event => setPriority(event.target.value)} /></Field><div className="grid gap-4 sm:grid-cols-2"><Field id="price-list-from" label={t("pricing.validFrom")} required><Input id="price-list-from" type="datetime-local" required value={validFrom} onChange={event => setValidFrom(event.target.value)} /></Field><Field id="price-list-to" label={t("pricing.validTo")}><Input id="price-list-to" type="datetime-local" value={validTo} onChange={event => setValidTo(event.target.value)} /></Field></div></fieldset>{mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("pricing.requestFailed"))}</p>}</DialogBody><DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!name.trim() || !validFrom || mutation.isPending}>{t(mutation.isPending ? "pricing.saving" : "pricing.create")}</Button></DialogFooter></form></DialogContent></Dialog>;
}

function PriceTierDialog({ list, onClose }: { list: PriceListDto; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [productId, setProductId] = useState("");
  const [minQty, setMinQty] = useState("1");
  const [unitPrice, setUnitPrice] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: ({ input, key }: { input: Parameters<typeof upsertPriceListLine>[0]; key: string }) => upsertPriceListLine(input, key), onSuccess: async () => { await cache.invalidateQueries({ queryKey: priceKey }); toast.success(t("pricing.tierSaved")); onClose(); } });
  function submit(event: FormEvent) { event.preventDefault(); if (mutation.isPending || !unitPrice.trim() || !Number.isFinite(Number(unitPrice))) return; const input = { priceListId: list.id, productId, minQty: Number(minQty), unitPrice: Number(unitPrice), currency: "USD" }; const body = JSON.stringify(input); if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() }; if (productId && input.minQty > 0 && input.unitPrice >= 0) mutation.mutate({ input, key: attempt.current.key }); }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent><DialogHeader><DialogTitle>{t("pricing.addOrReplaceTier")}</DialogTitle><DialogDescription>{list.name} · {t("pricing.tierIdentity")}</DialogDescription></DialogHeader><form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4"><CatalogChoice kind="products" value={productId} onChange={setProductId} disabled={mutation.isPending} /><div className="grid gap-4 sm:grid-cols-2"><Field id="tier-quantity" label={t("pricing.minQty")} required><Input id="tier-quantity" type="number" min="0.001" step="0.001" required value={minQty} onChange={event => setMinQty(event.target.value)} /></Field><Field id="tier-price" label={t("pricing.unitPriceUsd")} required><Input id="tier-price" type="number" min="0" step="0.01" required value={unitPrice} onChange={event => setUnitPrice(event.target.value)} /></Field></div></fieldset>{mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("pricing.requestFailed"))}</p>}</DialogBody><DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!productId || Number(minQty) <= 0 || !unitPrice || Number(unitPrice) < 0 || mutation.isPending}>{t(mutation.isPending ? "pricing.saving" : "pricing.saveTier")}</Button></DialogFooter></form></DialogContent></Dialog>;
}

function PriceLockDialog({ customers, onClose }: { customers: CustomerOrgDto[]; onClose: () => void }) {
  const t = useT();
  const [customerOrgId, setCustomerOrgId] = useState("");
  const [productId, setProductId] = useState("");
  const [unitPrice, setUnitPrice] = useState("");
  const [until, setUntil] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: ({ input, key }: { input: Parameters<typeof upsertPriceLock>[0]; key: string }) => upsertPriceLock(input, key), onSuccess: () => { toast.success(t("pricing.lockSaved")); onClose(); } });
  function submit(event: FormEvent) { event.preventDefault(); if (mutation.isPending || !unitPrice.trim() || !Number.isFinite(Number(unitPrice))) return; if (!until) return; const input = { customerOrgId, productId, unitPrice: Number(unitPrice), currency: "USD", until: new Date(until).toISOString() }; const body = JSON.stringify(input); if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() }; if (customerOrgId && productId && input.unitPrice >= 0) mutation.mutate({ input, key: attempt.current.key }); }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent><DialogHeader><DialogTitle>{t("pricing.setLock")}</DialogTitle><DialogDescription>{t("pricing.lockHint")}</DialogDescription></DialogHeader><form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4"><Field id="lock-customer" label={t("pricing.customer")} required><select id="lock-customer" className="h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-card)] px-3" required value={customerOrgId} onChange={event => setCustomerOrgId(event.target.value)}><option value="">{t("pricing.chooseCustomer")}</option>{customers.map(customer => <option key={customer.id} value={customer.id}>{customer.code} · {customer.name}</option>)}</select></Field><CatalogChoice kind="products" value={productId} onChange={setProductId} disabled={mutation.isPending} /><div className="grid gap-4 sm:grid-cols-2"><Field id="lock-price" label={t("pricing.unitPriceUsd")} required><Input id="lock-price" type="number" min="0" step="0.01" required value={unitPrice} onChange={event => setUnitPrice(event.target.value)} /></Field><Field id="lock-until" label={t("pricing.lockUntil")} required><Input id="lock-until" type="datetime-local" required value={until} onChange={event => setUntil(event.target.value)} /></Field></div></fieldset>{mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("pricing.requestFailed"))}</p>}</DialogBody><DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!customerOrgId || !productId || !unitPrice || Number(unitPrice) < 0 || !until || mutation.isPending}>{t(mutation.isPending ? "pricing.saving" : "pricing.saveLock")}</Button></DialogFooter></form></DialogContent></Dialog>;
}

function QuotePanel({ customers }: { customers: CustomerOrgDto[] }) {
  const t = useT();
  const [customerOrgId, setCustomerOrgId] = useState("");
  const [productId, setProductId] = useState("");
  const [quantity, setQuantity] = useState("1");
  const quote = useMutation({ mutationFn: (input: Parameters<typeof quoteProductPrice>[0]) => quoteProductPrice(input) });
  function submit(event: FormEvent) { event.preventDefault(); if (!quote.isPending && Number.isFinite(Number(quantity)) && customerOrgId && productId && Number(quantity) > 0) quote.mutate({ customerOrgId, productId, quantity: Number(quantity) }); }
  return <section className="space-y-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5"><div><h2 className="font-semibold">{t("pricing.quoteTitle")}</h2><p className="text-sm text-[var(--color-muted-foreground)]">{t("pricing.quoteHint")}</p></div><form onSubmit={submit}><fieldset disabled={quote.isPending} className="grid gap-3 md:grid-cols-4"><label className="space-y-1 text-sm"><span>{t("pricing.customer")}</span><select className="h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-card)] px-3" value={customerOrgId} onChange={event => { setCustomerOrgId(event.target.value); quote.reset(); }}><option value="">{t("pricing.chooseCustomer")}</option>{customers.map(customer => <option key={customer.id} value={customer.id}>{customer.code} · {customer.name}</option>)}</select></label><CatalogChoice kind="products" value={productId} onChange={value => { setProductId(value); quote.reset(); }} disabled={quote.isPending} /><Field id="quote-quantity" label={t("pricing.quantity")}><Input id="quote-quantity" type="number" min="0.001" step="0.001" value={quantity} onChange={event => { setQuantity(event.target.value); quote.reset(); }} /></Field><Button className="self-end" type="submit" disabled={!customerOrgId || !productId || Number(quantity) <= 0 || quote.isPending}>{t(quote.isPending ? "pricing.quoting" : "pricing.quote")}</Button></fieldset></form>{quote.isError && <ErrorBand message={describe(quote.error, t("pricing.quoteFailed"))} />}{quote.isSuccess && quote.data && <p role="status" className="text-lg font-semibold">{formatMoney(quote.data.unitPrice, quote.data.currency)} <span className="text-sm font-normal text-[var(--color-muted-foreground)]">· {quote.data.source}</span></p>}</section>;
}

function toLocalInput(value: Date) {
  const local = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

function formatDate(value: string) { return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)); }
function formatMoney(amount: number, currency: string) { return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount); }
