import type { CustomerOrgDto } from "@/api/customers";
import { SearchableSelect } from "@/components/list/searchable-select";
import { useT } from "@/i18n/locale-provider";

/** Customer dropdown with type-to-filter on code and name. An empty value means "all" when allowAll is set. */
export function CustomerPicker({ id, label, value, onChange, customers, allowAll, disabled }: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  customers: readonly CustomerOrgDto[];
  allowAll?: boolean;
  disabled?: boolean;
}) {
  const t = useT();
  const options = [
    ...(allowAll ? [{ value: "", label: t("partners.allCustomers") }] : []),
    ...customers.map(customer => ({ value: customer.id, label: `${customer.code} · ${customer.name}` })),
  ];
  return <SearchableSelect id={id} label={label} value={value} onChange={onChange} options={options}
    placeholder={allowAll ? t("partners.allCustomers") : t("partners.chooseCustomer")}
    selectedLabel={value === "" && allowAll ? t("partners.allCustomers") : undefined}
    searchLabel={t("partners.searchCustomerHint")} disabled={disabled} />;
}
