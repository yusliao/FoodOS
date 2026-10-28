import { useId, useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { toast } from "sonner";
import { Headset, History, Minus, Plus, Search, Star, Trash2 } from "lucide-react";
import {
  getMyCallers, getStoreHistory, placeOrderDeskOrder, quoteLines, searchCallers, suggestProducts,
  type OrderDeskCaller, type OrderDeskOrder, type OrderDeskProduct, type OrderDeskStoreHistory,
} from "@/api/order-desk";
import { useAuth } from "@/auth/use-auth";
import { useDebouncedSearch } from "@/hooks/use-debounced-search";
import { useLocale } from "@/i18n/locale-provider";
import { OrderingPermissions } from "@/lib/permissions";
import { newUuid } from "@/lib/uuid";
import { EntityPageHeader, ErrorBand, LoadingRow } from "@/components/list";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { describe } from "@/pages/customers/request-error";
import { discountLabel } from "@/pages/customers/level-format";

type DeskLine = {
  productId: string; sku: string; name: string; baseUom: string; catchWeight: boolean;
  quantity: number; unitPrice: number | null; currency: string;
};

const historyKey = (storeId: string) => ["order-desk", "history", storeId] as const;

export function OrderDeskPage() {
  const { t } = useLocale();
  const { user } = useAuth();
  const canPlace = user?.permissions.includes(OrderingPermissions.OrderDesk.Place) ?? false;
  const [caller, setCaller] = useState<OrderDeskCaller | null>(null);

  return <div className="space-y-6">
    <EntityPageHeader icon={Headset} title={t("orderDesk.title")} description={t("orderDesk.description")} />
    {caller
      ? <SelectedCaller caller={caller} onChange={() => setCaller(null)} />
      : <CallerLookup onSelect={setCaller} />}
    {caller && <OrderWorkspace key={caller.storeId} caller={caller} canPlace={canPlace} />}
  </div>;
}

function CallerLookup({ onSelect }: { onSelect: (caller: OrderDeskCaller) => void }) {
  const { t } = useLocale();
  const [input, setInput] = useState("");
  const [term, debouncing] = useDebouncedSearch(input);
  const callers = useQuery({
    queryKey: ["order-desk", "callers", term],
    queryFn: ({ signal }) => searchCallers(term, signal),
    enabled: term.length >= 2,
  });
  return <section className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5" aria-labelledby="caller-lookup">
    <h2 id="caller-lookup" className="font-semibold">{t("orderDesk.callerTitle")}</h2>
    <label className="relative block">
      <span className="sr-only">{t("orderDesk.callerSearch")}</span>
      <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-[var(--color-muted-foreground)]" aria-hidden />
      <Input autoFocus className="pl-9" value={input} maxLength={100} inputMode="search"
        placeholder={t("orderDesk.callerPlaceholder")} onChange={event => setInput(event.target.value)} />
    </label>
    {term.length < 2 && <p className="text-sm text-[var(--color-muted-foreground)]">{t("orderDesk.callerHint")}</p>}
    {term.length < 2 && <MyCallers onSelect={onSelect} />}
    {term.length >= 2 && (callers.isPending || debouncing) && <LoadingRow label={t("orderDesk.searching")} />}
    {callers.isError && <ErrorBand message={describe(callers.error, t("orderDesk.requestFailed"))} />}
    {callers.isSuccess && !debouncing && callers.data.length === 0 && <p role="status">{t("orderDesk.noCallers")}</p>}
    {callers.isSuccess && callers.data.length > 0 && <ul className="grid gap-3 md:grid-cols-2">
      {callers.data.map(item => <li key={item.storeId}><CallerCard item={item} onSelect={onSelect} /></li>)}
    </ul>}
  </section>;
}

/** The agent's own frequent customers, so a repeat caller is one click away. */
function MyCallers({ onSelect }: { onSelect: (caller: OrderDeskCaller) => void }) {
  const { t } = useLocale();
  const mine = useQuery({ queryKey: ["order-desk", "callers", "mine"], queryFn: ({ signal }) => getMyCallers(signal) });
  return <section aria-labelledby="my-callers" className="space-y-2 pt-2">
    <div>
      <h3 id="my-callers" className="text-sm font-semibold">{t("orderDesk.myCustomersTitle")}</h3>
      <p className="text-xs text-[var(--color-muted-foreground)]">{t("orderDesk.myCustomersHint")}</p>
    </div>
    {mine.isPending && <LoadingRow label={t("orderDesk.loading")} />}
    {mine.isError && <ErrorBand message={describe(mine.error, t("orderDesk.requestFailed"))} />}
    {mine.isSuccess && mine.data.length === 0 && <p role="status" className="text-sm text-[var(--color-muted-foreground)]">{t("orderDesk.noMyCustomers")}</p>}
    {mine.isSuccess && mine.data.length > 0 && <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {mine.data.map(({ caller, orderCount }) => <li key={caller.storeId}>
        <CallerCard item={caller} onSelect={onSelect} extra={<Badge variant="muted">{t("orderDesk.orderCount").replace("{n}", String(orderCount))}</Badge>} />
      </li>)}
    </ul>}
  </section>;
}

function CallerCard({ item, onSelect, extra }: { item: OrderDeskCaller; onSelect: (caller: OrderDeskCaller) => void; extra?: ReactNode }) {
  const { t } = useLocale();
  return <button type="button" onClick={() => onSelect(item)}
    className="w-full min-w-0 space-y-1 rounded-lg border border-[var(--color-border)] p-4 text-left text-sm transition-colors hover:bg-[var(--color-accent)] focus-visible:outline-2">
    <span className="flex flex-wrap items-center gap-2 font-semibold"><span className="break-words">{item.customerOrgName}</span><Badge variant="muted">{item.customerOrgCode}</Badge>
      {item.levelName && <Badge variant="info">{levelText(t, item)}</Badge>}
      {extra}
      {!item.canPlaceOrders && <Badge variant="warning">{t(`orderDesk.blocking.${item.blockingReason}`)}</Badge>}</span>
    <span className="block break-words">{t("orderDesk.store")}：{item.storeName} · {item.storeCode}</span>
    <span className="block break-words text-[var(--color-muted-foreground)]">{item.address || t("orderDesk.addressMissing")}</span>
    {item.contacts.length > 0 && <span className="block break-words">{item.contacts.map(contact => `${contact.name} ${contact.phone}`).join(" / ")}</span>}
  </button>;
}

/** "VIP · 9 折" for a level-priced customer. */
function levelText(t: (key: string) => string, caller: OrderDeskCaller) {
  return `${caller.levelName} · ${discountLabel(t, caller.levelDiscountPercent ?? 0)}`;
}

function SelectedCaller({ caller, onChange }: { caller: OrderDeskCaller; onChange: () => void }) {
  const { t, culture } = useLocale();
  return <section className="flex flex-wrap items-start justify-between gap-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5" aria-label={t("orderDesk.callerTitle")}>
    <dl className="grid min-w-0 flex-1 gap-3 text-sm sm:grid-cols-2 lg:grid-cols-5">
      <div className="min-w-0"><dt className="text-[var(--color-muted-foreground)]">{t("orderDesk.store")}</dt><dd className="break-words font-semibold">{caller.storeName} · {caller.storeCode}</dd></div>
      <div className="min-w-0"><dt className="text-[var(--color-muted-foreground)]">{t("orderDesk.customer")}</dt><dd className="break-words">{caller.customerOrgName}</dd></div>
      <div className="min-w-0"><dt className="text-[var(--color-muted-foreground)]">{t("orderDesk.level")}</dt><dd className="break-words font-semibold">{caller.levelName ? levelText(t, caller) : t("partners.noLevel")}</dd></div>
      <div className="min-w-0"><dt className="text-[var(--color-muted-foreground)]">{t("orderDesk.address")}</dt><dd className="break-words">{caller.address || t("orderDesk.addressMissing")}{caller.deliveryWindow ? ` · ${caller.deliveryWindow}` : ""}</dd></div>
      <div className="min-w-0"><dt className="text-[var(--color-muted-foreground)]">{t("orderDesk.lastOrder")}</dt><dd>{caller.lastOrderAt ? new Intl.DateTimeFormat(culture, { dateStyle: "medium", timeStyle: "short" }).format(new Date(caller.lastOrderAt)) : t("orderDesk.never")}</dd></div>
    </dl>
    <div className="flex flex-col items-end gap-2">
      {!caller.canPlaceOrders && <Badge variant="warning">{t(`orderDesk.blocking.${caller.blockingReason}`)}</Badge>}
      <Button variant="outline" onClick={onChange}>{t("orderDesk.changeCaller")}</Button>
    </div>
  </section>;
}

function OrderWorkspace({ caller, canPlace }: { caller: OrderDeskCaller; canPlace: boolean }) {
  const { t, culture } = useLocale();
  const queryClient = useQueryClient();
  const [lines, setLines] = useState<DeskLine[]>([]);
  const [placed, setPlaced] = useState<string | null>(null);
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const history = useQuery({
    queryKey: historyKey(caller.storeId),
    queryFn: ({ signal }) => getStoreHistory(caller.storeId, signal),
  });

  const requestLines = useMemo(() => lines.map(line => ({ productId: line.productId, quantity: line.quantity })), [lines]);
  const [quoteBody] = useDebouncedSearch(JSON.stringify(requestLines));
  const quote = useQuery({
    queryKey: ["order-desk", "quote", caller.storeId, quoteBody],
    queryFn: ({ signal }) => quoteLines(caller.storeId, JSON.parse(quoteBody) as typeof requestLines, signal),
    enabled: requestLines.length > 0 && quoteBody === JSON.stringify(requestLines),
  });
  const priced = lines.map(line => {
    const quoted = quote.data?.find(item => item.productId === line.productId && item.quantity === line.quantity);
    const unitPrice = quoted?.unitPrice ?? line.unitPrice;
    return { ...line, unitPrice, currency: quoted?.currency ?? line.currency, lineTotal: unitPrice === null ? null : unitPrice * line.quantity };
  });
  const currency = priced[0]?.currency ?? "USD";
  const total = priced.reduce((sum, line) => sum + (line.lineTotal ?? 0), 0);
  const money = (amount: number, code: string) => new Intl.NumberFormat(culture, { style: "currency", currency: code }).format(amount);

  const place = useMutation({
    mutationFn: placeOrderDeskOrder,
    onSuccess: async orderId => {
      setPlaced(orderId);
      setLines([]);
      attempt.current = null;
      toast.success(t("orderDesk.placed"));
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: historyKey(caller.storeId) }),
        queryClient.invalidateQueries({ queryKey: ["orders"] }),
      ]);
    },
  });

  const addLine = (product: Pick<DeskLine, "productId" | "sku" | "name" | "baseUom" | "catchWeight" | "unitPrice" | "currency">, quantity: number) => {
    setPlaced(null);
    setLines(current => current.some(line => line.productId === product.productId)
      ? current.map(line => line.productId === product.productId ? { ...line, quantity: line.quantity + quantity } : line)
      : [...current, { ...product, quantity }]);
  };
  const setQuantity = (productId: string, quantity: number) =>
    setLines(current => current.map(line => line.productId === productId ? { ...line, quantity } : line));
  const copyOrder = (order: OrderDeskOrder) => {
    for (const line of order.lines) {
      addLine({ productId: line.productId, sku: line.sku, name: line.name || line.productId, baseUom: line.baseUom, catchWeight: false, unitPrice: null, currency: line.currency }, line.orderedQty);
    }
  };
  const validLines = lines.length > 0 && lines.every(line => line.quantity > 0);
  const submit = () => {
    if (!validLines || place.isPending) return;
    const body = JSON.stringify(requestLines);
    if (attempt.current?.body !== body) attempt.current = { body, key: newUuid() };
    place.mutate({ storeId: caller.storeId, lines: requestLines, key: attempt.current.key });
  };

  return <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_380px]">
    <section className="min-w-0 space-y-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5" aria-labelledby="order-lines">
      <h2 id="order-lines" className="font-semibold">{t("orderDesk.linesTitle")}</h2>
      <ProductPicker storeId={caller.storeId} onPick={product => addLine({ ...product, unitPrice: product.unitPrice }, 1)} />
      {lines.length === 0 && <p className="text-sm text-[var(--color-muted-foreground)]">{t("orderDesk.noLines")}</p>}
      {lines.length > 0 && <ul className="divide-y divide-[var(--color-border)]">
        {priced.map(line => <li key={line.productId} className="flex flex-wrap items-center gap-3 py-3 text-sm">
          <div className="min-w-0 flex-1">
            <p className="break-words font-medium">{line.name}</p>
            <p className="text-[var(--color-muted-foreground)]">{line.sku} · {line.baseUom}{line.unitPrice !== null ? ` · ${money(line.unitPrice, line.currency)}` : ""}</p>
          </div>
          <div className="flex items-center gap-1">
            <Button size="icon-xs" variant="outline" aria-label={t("orderDesk.decrease")} disabled={line.quantity <= 1} onClick={() => setQuantity(line.productId, Math.max(1, line.quantity - 1))}><Minus /></Button>
            <Input className="h-7 w-20 text-center" type="number" inputMode="decimal" min={0} step={line.catchWeight ? 0.01 : 1}
              aria-label={`${t("orderDesk.quantity")} ${line.name}`} value={Number.isFinite(line.quantity) ? line.quantity : ""}
              onChange={event => setQuantity(line.productId, Number(event.target.value))} />
            <Button size="icon-xs" variant="outline" aria-label={t("orderDesk.increase")} onClick={() => setQuantity(line.productId, line.quantity + 1)}><Plus /></Button>
          </div>
          <span className="w-24 text-right tabular-nums">{line.lineTotal === null ? "—" : money(line.lineTotal, line.currency)}</span>
          <Button size="icon-xs" variant="ghost" aria-label={`${t("orderDesk.remove")} ${line.name}`} onClick={() => setLines(current => current.filter(item => item.productId !== line.productId))}><Trash2 /></Button>
        </li>)}
      </ul>}
      {quote.isError && <ErrorBand message={describe(quote.error, t("orderDesk.quoteFailed"))} />}
      {lines.length > 0 && <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--color-border)] pt-4">
        <p className="text-sm">{t("orderDesk.total")} <strong className="text-base tabular-nums">{money(total, currency)}</strong>
          <span className="block text-xs text-[var(--color-muted-foreground)]">{t("orderDesk.priceHint")}</span></p>
        {canPlace
          ? <Button disabled={!validLines || !caller.canPlaceOrders || place.isPending} onClick={submit}>{place.isPending ? t("orderDesk.placing") : t("orderDesk.place")}</Button>
          : <p role="status" className="text-sm">{t("orderDesk.readOnly")}</p>}
      </div>}
      {place.isError && <ErrorBand message={describe(place.error, t("orderDesk.placeFailed"))} />}
      {placed && <p role="status" className="rounded-lg bg-[var(--color-muted)] p-3 text-sm">{t("orderDesk.placedHint")} <Link className="underline" to={`/orders/${placed}`}>{t("orderDesk.viewOrder")}</Link></p>}
    </section>
    <StoreHistoryPanel history={history} onAdd={addLine} onCopy={copyOrder} />
  </div>;
}

function ProductPicker({ storeId, onPick }: { storeId: string; onPick: (product: OrderDeskProduct) => void }) {
  const { t, culture } = useLocale();
  const listId = useId();
  const [input, setInput] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const [term, debouncing] = useDebouncedSearch(input);
  const suggestions = useQuery({
    queryKey: ["order-desk", "products", storeId, term],
    queryFn: ({ signal }) => suggestProducts(storeId, term, signal),
    enabled: term.length >= 1,
  });
  const items = term.length >= 1 ? suggestions.data ?? [] : [];
  const pick = (product: OrderDeskProduct) => {
    onPick(product);
    setInput("");
    setActive(0);
  };
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown") { event.preventDefault(); setOpen(true); setActive(index => Math.min(index + 1, items.length - 1)); }
    else if (event.key === "ArrowUp") { event.preventDefault(); setActive(index => Math.max(index - 1, 0)); }
    else if (event.key === "Enter" && items[active]) { event.preventDefault(); pick(items[active]); }
    else if (event.key === "Escape") setOpen(false);
  };
  const showList = open && term.length >= 1;
  return <div className="relative">
    <label className="relative block">
      <span className="sr-only">{t("orderDesk.productSearch")}</span>
      <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-[var(--color-muted-foreground)]" aria-hidden />
      <Input role="combobox" aria-expanded={showList} aria-controls={listId} aria-autocomplete="list"
        aria-activedescendant={showList && items[active] ? `${listId}-${active}` : undefined}
        className="pl-9" value={input} maxLength={100} placeholder={t("orderDesk.productPlaceholder")}
        onChange={event => { setInput(event.target.value); setOpen(true); setActive(0); }}
        onFocus={() => setOpen(true)} onBlur={() => setOpen(false)} onKeyDown={onKeyDown} />
    </label>
    {showList && <ul id={listId} role="listbox" aria-label={t("orderDesk.productSearch")}
      className="absolute z-20 mt-1 max-h-80 w-full overflow-auto rounded-lg border border-[var(--color-border)] bg-[var(--color-popover,var(--color-card))] p-1 shadow-lg">
      {(suggestions.isPending || debouncing) && <li className="px-3 py-2 text-sm text-[var(--color-muted-foreground)]">{t("orderDesk.searching")}</li>}
      {suggestions.isError && <li className="px-3 py-2 text-sm" role="alert">{describe(suggestions.error, t("orderDesk.requestFailed"))}</li>}
      {suggestions.isSuccess && !debouncing && items.length === 0 && <li className="px-3 py-2 text-sm">{t("orderDesk.noProducts")}</li>}
      {items.map((product, index) => <li key={product.productId} id={`${listId}-${index}`} role="option" aria-selected={index === active}
        className={`flex cursor-pointer flex-wrap items-center gap-2 rounded-md px-3 py-2 text-sm ${index === active ? "bg-[var(--color-accent)]" : ""}`}
        onMouseDown={event => { event.preventDefault(); pick(product); }} onMouseEnter={() => setActive(index)}>
        <span className="min-w-0 flex-1"><span className="block break-words font-medium">{product.name}</span>
          <span className="text-[var(--color-muted-foreground)]">{product.sku} · {product.baseUom}</span></span>
        <AvailabilityBadge status={product.availabilityStatus} />
        <span className="tabular-nums">{new Intl.NumberFormat(culture, { style: "currency", currency: product.currency }).format(product.unitPrice)}</span>
      </li>)}
    </ul>}
  </div>;
}

function AvailabilityBadge({ status }: { status: string }) {
  const { t } = useLocale();
  if (status === "available") return <Badge variant="success">{t("orderDesk.stock.available")}</Badge>;
  if (status === "insufficient") return <Badge variant="warning">{t("orderDesk.stock.insufficient")}</Badge>;
  return <Badge variant="muted">{t("orderDesk.stock.pending")}</Badge>;
}

function StoreHistoryPanel({ history, onAdd, onCopy }: {
  history: UseQueryResult<OrderDeskStoreHistory>;
  onAdd: (product: Pick<DeskLine, "productId" | "sku" | "name" | "baseUom" | "catchWeight" | "unitPrice" | "currency">, quantity: number) => void;
  onCopy: (order: OrderDeskOrder) => void;
}) {
  const { t, culture } = useLocale();
  const [expanded, setExpanded] = useState<string | null>(null);
  const money = (amount: number, code: string) => new Intl.NumberFormat(culture, { style: "currency", currency: code }).format(amount);
  return <aside className="min-w-0 space-y-6">
    <section className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5" aria-labelledby="frequent-products">
      <h2 id="frequent-products" className="flex items-center gap-2 font-semibold"><Star className="size-4" aria-hidden />{t("orderDesk.frequentTitle")}</h2>
      {history.isPending && <LoadingRow label={t("orderDesk.loading")} />}
      {history.isError && <div className="space-y-2"><ErrorBand message={describe(history.error, t("orderDesk.requestFailed"))} /><Button variant="outline" onClick={() => void history.refetch()}>{t("workbench.retry")}</Button></div>}
      {history.isSuccess && history.data.frequentProducts.length === 0 && <p className="text-sm text-[var(--color-muted-foreground)]">{t("orderDesk.noFrequent")}</p>}
      {history.isSuccess && history.data.frequentProducts.length > 0 && <ul className="flex flex-wrap gap-2">
        {history.data.frequentProducts.map(product => <li key={product.productId}>
          <Button size="sm" variant="outline" disabled={!product.isActive}
            title={product.isActive ? `${product.sku} · ${t("orderDesk.orderedTimes")} ${product.orderCount}` : t("orderDesk.inactive")}
            onClick={() => onAdd({ productId: product.productId, sku: product.sku, name: product.name, baseUom: product.baseUom, catchWeight: false, unitPrice: null, currency: "USD" }, product.lastQty)}>
            <Plus />{product.name} × {product.lastQty}
          </Button>
        </li>)}
      </ul>}
    </section>
    <section className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5" aria-labelledby="recent-orders">
      <h2 id="recent-orders" className="flex items-center gap-2 font-semibold"><History className="size-4" aria-hidden />{t("orderDesk.recentTitle")}</h2>
      {history.isSuccess && history.data.recentOrders.length === 0 && <p className="text-sm text-[var(--color-muted-foreground)]">{t("orderDesk.noRecent")}</p>}
      {history.isSuccess && <ul className="space-y-3">
        {history.data.recentOrders.map(order => <li key={order.id} className="space-y-2 rounded-lg border border-[var(--color-border)] p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <Link className="font-medium underline" to={`/orders/${order.id}`}>{order.number}</Link>
            <span className="flex flex-wrap gap-1">
              <Badge variant="muted">{t(`orders.states.${order.status}`, order.status)}</Badge>
              {order.placementChannel === "OrderDesk" && <Badge variant="info">{t("orderDesk.byPhone")}</Badge>}
            </span>
          </div>
          <p className="text-[var(--color-muted-foreground)]">{order.businessDate} · {order.lines.length} {t("orderDesk.linesUnit")} · {money(order.totalAmount, order.currency)}</p>
          <div className="flex flex-wrap gap-2">
            <Button size="xs" variant="ghost" aria-expanded={expanded === order.id} onClick={() => setExpanded(current => current === order.id ? null : order.id)}>
              {expanded === order.id ? t("orderDesk.hideLines") : t("orderDesk.showLines")}
            </Button>
            <Button size="xs" variant="outline" onClick={() => onCopy(order)}>{t("orderDesk.copyOrder")}</Button>
          </div>
          {expanded === order.id && <ul className="space-y-1 border-t border-[var(--color-border)] pt-2">
            {order.lines.map(line => <li key={line.lineId} className="flex justify-between gap-2">
              <span className="min-w-0 break-words">{line.name || line.productId} <span className="text-[var(--color-muted-foreground)]">{line.sku}</span></span>
              <span className="shrink-0 tabular-nums">{line.orderedQty} {line.baseUom}{line.shortageQty > 0 ? ` (${t("orderDesk.short")} ${line.shortageQty})` : ""}</span>
            </li>)}
          </ul>}
        </li>)}
      </ul>}
    </section>
  </aside>;
}
