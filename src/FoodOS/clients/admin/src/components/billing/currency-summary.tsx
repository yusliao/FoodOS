import { useLocale } from "@/i18n/locale-provider";

/** Never add or average amounts across currencies; no exchange rates are available. */
export function CurrencySummary({ values, mode = "sum" }: {
  values: { currency: string; amount: number }[];
  mode?: "sum" | "average";
}) {
  const { culture } = useLocale();
  const groups = new Map<string, { total: number; count: number }>();
  for (const value of values) {
    const currency = value.currency.trim().toUpperCase();
    const group = groups.get(currency) ?? { total: 0, count: 0 };
    group.total += value.amount;
    group.count++;
    groups.set(currency, group);
  }
  if (!groups.size) return <>—</>;
  return <div className="space-y-1 text-lg break-words">
    {[...groups].sort(([a], [b]) => a.localeCompare(b)).map(([currency, group]) => {
      const amount = mode === "average" ? group.total / group.count : group.total;
      let text: string;
      try {
        text = new Intl.NumberFormat(culture, { style: "currency", currency, currencyDisplay: "code" }).format(amount);
      } catch {
        text = `${currency} ${amount.toFixed(2)}`;
      }
      return <div key={currency}>{text}</div>;
    })}
  </div>;
}
