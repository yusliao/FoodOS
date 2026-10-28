/** "优惠 10%" style label for a level discount; 0 shows "不打折". */
export function discountLabel(t: (key: string) => string, discountPercent: number) {
  return discountPercent > 0
    ? t("levels.discountLabel").replace("{pct}", String(discountPercent))
    : t("levels.noDiscount");
}
