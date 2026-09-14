import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PackagePlus } from "lucide-react";
import { toast } from "sonner";
import { searchProducts } from "@/api/catalog";
import {
  createPurchaseOrder,
  createSupplier,
  PROCUREMENT_PERMISSIONS,
  searchPurchaseOrders,
  searchSuppliers,
  sendPurchaseOrder,
  type PurchaseOrderDto,
} from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Combobox, EntityEmpty, EntityPageHeader, EntityStatusBadge, ErrorBand, Field } from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { JobCard, newIdempotencyKey, todayIsoDate, useOpsWarehouse, WarehousePicker } from "./ops-helpers";

const ZONES = ["Ambient", "Chilled", "Frozen"];

function SendDraftButton({ order }: { order: PurchaseOrderDto }) {
  const t = useT();
  const queryClient = useQueryClient();
  const send = useMutation({
    mutationFn: (purchaseOrderId: string) => sendPurchaseOrder(purchaseOrderId, newIdempotencyKey()),
    onSuccess: async () => {
      toast.success(t("ops.poSent", "PO sent"));
      await queryClient.invalidateQueries({ queryKey: ["procurement", "purchase-orders"] });
    },
    onError: (error) => toast.error(describe(error)),
  });

  return (
    <Button
      data-testid={`po-send-${order.id}`}
      variant="outline"
      disabled={send.isPending}
      onClick={() => send.mutate(order.id)}
    >
      {t("ops.sendPo", "Send")}
    </Button>
  );
}

export function PurchaseDeskPage() {
  const t = useT();
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const queryClient = useQueryClient();
  const { warehouses, warehouseId, setWarehouseId, isLoading, isError, error } = useOpsWarehouse();

  const [supplierCode, setSupplierCode] = useState("");
  const [supplierName, setSupplierName] = useState("");
  const [supplierId, setSupplierId] = useState<string | null>(null);
  const [expectedDate, setExpectedDate] = useState(todayIsoDate);
  const [productId, setProductId] = useState<string | null>(null);
  const [zone, setZone] = useState("Ambient");
  const [quantity, setQuantity] = useState("10");
  const [productSearch, setProductSearch] = useState("");

  const suppliersQuery = useQuery({
    queryKey: ["procurement", "suppliers"],
    queryFn: () => searchSuppliers(),
  });
  const posQuery = useQuery({
    queryKey: ["procurement", "purchase-orders"],
    queryFn: () => searchPurchaseOrders(),
  });
  const productsQuery = useQuery({
    queryKey: ["catalog", "products", productSearch],
    queryFn: () => searchProducts({ search: productSearch || undefined, pageNumber: 1, pageSize: 30, isActive: true }),
  });

  useEffect(() => {
    if (!supplierId && suppliersQuery.data?.[0]) setSupplierId(suppliersQuery.data[0].id);
  }, [supplierId, suppliersQuery.data]);
  useEffect(() => {
    if (!productId && productsQuery.data?.items[0]) {
      const first = productsQuery.data.items[0];
      setProductId(first.id);
      if (first.temperatureZone) setZone(first.temperatureZone);
    }
  }, [productId, productsQuery.data]);

  const canCreateSupplier = perms.includes(PROCUREMENT_PERMISSIONS.suppliersCreate);
  const canCreatePo = perms.includes(PROCUREMENT_PERMISSIONS.purchaseCreate);

  const createVendor = useMutation({
    mutationFn: (input: { code: string; name: string }) => createSupplier(input, newIdempotencyKey()),
    onSuccess: async (id) => {
      toast.success(t("ops.supplierCreated", "Supplier created"));
      setSupplierId(id);
      setSupplierCode("");
      setSupplierName("");
      await queryClient.invalidateQueries({ queryKey: ["procurement", "suppliers"] });
    },
    onError: (err) => toast.error(describe(err)),
  });

  const createPo = useMutation({
    mutationFn: (input: {
      supplierId: string;
      warehouseId: string;
      expectedAt: string;
      lines: Array<{ productId: string; zone: string; quantity: number }>;
    }) => createPurchaseOrder(input, newIdempotencyKey()),
    onSuccess: async () => {
      toast.success(t("ops.poCreated", "Draft PO created"));
      await queryClient.invalidateQueries({ queryKey: ["procurement", "purchase-orders"] });
    },
    onError: (err) => toast.error(describe(err)),
  });

  const products = productsQuery.data?.items ?? [];
  const selectedProduct = products.find((p) => p.id === productId);

  const orders = useMemo(() => posQuery.data ?? [], [posQuery.data]);

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={PackagePlus}
        title={t("ops.purchaseTitle", "Purchasing")}
        description={t("ops.purchaseDescription", "Register a supplier, raise a draft PO, then send it so QC can receive.")}
      />
      <WarehousePicker
        warehouses={warehouses}
        warehouseId={warehouseId}
        onChange={setWarehouseId}
        loading={isLoading}
      />
      {isError ? <ErrorBand message={describe(error)} /> : null}
      {suppliersQuery.isError ? <ErrorBand message={describe(suppliersQuery.error)} /> : null}
      {posQuery.isError ? <ErrorBand message={describe(posQuery.error)} /> : null}

      {canCreateSupplier ? (
        <JobCard>
          <p className="mb-3 text-[13px] font-medium">{t("ops.newSupplier", "New supplier")}</p>
          <div className="grid gap-3 sm:grid-cols-3">
            <Field id="supplier-code" label={t("ops.supplierCode", "Code")} required>
              <Input id="supplier-code" data-testid="supplier-code" value={supplierCode} onChange={(e) => setSupplierCode(e.target.value)} />
            </Field>
            <Field id="supplier-name" label={t("ops.supplierName", "Name")} required>
              <Input id="supplier-name" data-testid="supplier-name" value={supplierName} onChange={(e) => setSupplierName(e.target.value)} />
            </Field>
            <div className="flex items-end">
              <Button
                data-testid="supplier-create"
                disabled={createVendor.isPending || !supplierCode.trim() || !supplierName.trim()}
                onClick={() => createVendor.mutate({ code: supplierCode.trim(), name: supplierName.trim() })}
              >
                {t("ops.createSupplier", "Create supplier")}
              </Button>
            </div>
          </div>
        </JobCard>
      ) : null}

      {canCreatePo ? (
        <JobCard>
          <p className="mb-3 text-[13px] font-medium">{t("ops.newPo", "New purchase order")}</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field id="po-supplier" label={t("ops.supplier", "Supplier")} required>
              <Combobox
                label={t("ops.supplier", "Supplier")}
                searchable
                value={supplierId}
                onChange={setSupplierId}
                placeholder={t("ops.chooseSupplier", "Choose a supplier")}
                options={(suppliersQuery.data ?? []).map((s) => ({
                  value: s.id,
                  label: s.name,
                  hint: s.code,
                }))}
              />
            </Field>
            <Field id="po-expected" label={t("ops.expected", "Expected")} required>
              <Input id="po-expected" type="date" value={expectedDate} onChange={(e) => setExpectedDate(e.target.value)} />
            </Field>
            <Field id="po-product-search" label={t("ops.productSearch", "Search product")}>
              <Input
                id="po-product-search"
                value={productSearch}
                onChange={(e) => setProductSearch(e.target.value)}
                placeholder={t("ops.searchSku", "SKU or name")}
              />
            </Field>
            <Field id="po-product" label={t("ops.product", "Product")} required>
              <Combobox
                label={t("ops.product", "Product")}
                searchable
                value={productId}
                onChange={(id) => {
                  setProductId(id);
                  const next = products.find((p) => p.id === id);
                  if (next?.temperatureZone) setZone(next.temperatureZone);
                }}
                placeholder={t("ops.chooseProduct", "Choose a product")}
                options={products.map((p) => ({
                  value: p.id,
                  label: p.name,
                  hint: p.sku,
                }))}
              />
            </Field>
            <Field id="po-zone" label={t("ops.zone", "Zone")} required>
              <Combobox
                label={t("ops.zone", "Zone")}
                value={zone}
                onChange={(v) => setZone(v ?? "Ambient")}
                options={ZONES.map((z) => ({ value: z, label: z }))}
              />
            </Field>
            <Field id="po-qty" label={t("ops.qty", "Quantity")} required>
              <Input id="po-qty" data-testid="po-qty" type="number" min="0.0001" step="any" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
            </Field>
            <div className="flex items-end">
              <Button
                data-testid="po-create"
                disabled={
                  createPo.isPending ||
                  !warehouseId ||
                  !supplierId ||
                  !productId ||
                  Number(quantity) <= 0
                }
                onClick={() =>
                  createPo.mutate({
                    supplierId: supplierId!,
                    warehouseId: warehouseId!,
                    expectedAt: new Date(`${expectedDate}T12:00:00Z`).toISOString(),
                    lines: [{ productId: productId!, zone, quantity: Number(quantity) }],
                  })
                }
              >
                {t("ops.createPo", "Create draft PO")}
              </Button>
            </div>
          </div>
          {selectedProduct ? (
            <p className="mt-2 text-[12px] text-[var(--color-muted-foreground)]">
              {selectedProduct.sku} · {selectedProduct.temperatureZone ?? zone}
            </p>
          ) : null}
        </JobCard>
      ) : null}

      {orders.length === 0 && !posQuery.isLoading ? (
        <EntityEmpty
          icon={PackagePlus}
          title={t("ops.noPosYet", "No purchase orders")}
          body={t("ops.noPosYetBody", "Create a supplier and a draft PO, then send it for QC.")}
        />
      ) : (
        <div className="space-y-3">
          {orders.map((order) => (
            <JobCard key={order.id}>
              <div className="flex items-start justify-between gap-3">
                <div>
                  <code className="font-mono text-[13px] font-medium">{order.number}</code>
                  <p className="text-[12px] text-[var(--color-muted-foreground)]">
                    {order.lines.length} {t("ops.lines", "lines")}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <EntityStatusBadge tone={order.status === "Draft" ? "warning" : "info"}>{order.status}</EntityStatusBadge>
                  {order.status === "Draft" && canCreatePo ? <SendDraftButton order={order} /> : null}
                </div>
              </div>
            </JobCard>
          ))}
        </div>
      )}
    </div>
  );
}
