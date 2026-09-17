import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { recordQuality } from "@/api/procurement";
import { ErrorBand, Field } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

export function QualityDialog({ purchaseOrderId, lineId, result, onClose }: { purchaseOrderId: string; lineId: string; result: "pass" | "fail"; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [quantity, setQuantity] = useState("");
  const [sampleQty, setSampleQty] = useState("0");
  const [lotNo, setLotNo] = useState("");
  const [expiryDate, setExpiryDate] = useState("");
  const [manufacturedOn, setManufacturedOn] = useState("");
  const [note, setNote] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: recordQuality, onSuccess: async () => {
    await Promise.all([cache.invalidateQueries({ queryKey: ["procurement", "purchase-orders"] }), cache.invalidateQueries({ queryKey: ["inventory"] }), cache.invalidateQueries({ queryKey: ["warehouse"] })]); onClose();
  } });
  const valid = Number(quantity) > 0 && sampleQty !== "" && Number(sampleQty) >= 0 && !!lotNo.trim() && !!expiryDate;
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending) return;
    const body = { purchaseOrderId, lineId, quantity: Number(quantity), sampleQty: Number(sampleQty), lotNo: lotNo.trim(), expiryDate, manufacturedOn: manufacturedOn || null, note: note.trim() || null };
    const serialized = JSON.stringify(body);
    if (attempt.current?.body !== serialized) attempt.current = { body: serialized, key: crypto.randomUUID() };
    mutation.mutate({ body, result, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent>
    <DialogHeader><DialogTitle>{t(`quality.${result}`)}</DialogTitle><DialogDescription>{t(`quality.${result}Hint`)}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2"><Field id="qc-quantity" label={t("quality.quantity")} required><Input id="qc-quantity" type="number" min="0.001" step="0.001" required value={quantity} onChange={event => setQuantity(event.target.value)} /></Field><Field id="qc-sample" label={t("quality.sample")} required><Input id="qc-sample" type="number" min="0" step="0.001" required value={sampleQty} onChange={event => setSampleQty(event.target.value)} /></Field></div>
      <Field id="qc-lot" label={t("quality.lot")} required><Input id="qc-lot" maxLength={64} required value={lotNo} onChange={event => setLotNo(event.target.value)} /></Field>
      <div className="grid gap-3 sm:grid-cols-2"><Field id="qc-expiry" label={t("quality.expiry")} required><Input id="qc-expiry" type="date" required value={expiryDate} onChange={event => setExpiryDate(event.target.value)} /></Field><Field id="qc-manufactured" label={t("quality.manufactured")}><Input id="qc-manufactured" type="date" value={manufacturedOn} onChange={event => setManufacturedOn(event.target.value)} /></Field></div>
      <Field id="qc-note" label={t("quality.note")}><Input id="qc-note" maxLength={512} value={note} onChange={event => setNote(event.target.value)} /></Field>
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("purchase.failed"))} />}</DialogBody><DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t(`quality.${result}`)}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
