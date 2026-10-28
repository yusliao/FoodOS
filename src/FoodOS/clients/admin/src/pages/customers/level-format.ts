/** Share of the standard price the customer pays, e.g. 10% off → 90. */
export function payPercent(discountPercent: number) {
  return Math.round((100 - discountPercent) * 100) / 100;
}

/** "9 折" / "95 折" style label for a level; 0% off shows "不打折". */
export function discountLabel(t: (key: string) => string, discountPercent: number) {
  if (discountPercent <= 0) return t("levels.noDiscount");
  const pay = payPercent(discountPercent);
  // 90 → "9 折"，95 → "95 折"，87.5 → "87.5 折"
  const zhe = pay % 10 === 0 ? String(pay / 10) : String(pay);
  return t("levels.discountLabel").replace("{zhe}", zhe).replace("{pct}", String(pay));
}
