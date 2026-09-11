import type { EntityStatusTone } from "@/components/list";

export function quoteSourceLabel(source: string): string {
  switch (source) {
    case "Locked":
      return "Locked";
    case "Contract":
      return "Contract";
    case "Catalog":
      return "Catalog";
    default:
      return source;
  }
}

export function zoneLabel(zone: string | null | undefined): string {
  switch ((zone ?? "").toLowerCase()) {
    case "ambient":
      return "Ambient";
    case "chilled":
      return "Chilled";
    case "frozen":
      return "Frozen";
    default:
      return zone || "—";
  }
}

export function orderStatusTone(status: string): EntityStatusTone {
  switch (status) {
    case "Reserved":
      return "info";
    case "Planned":
    case "Picking":
    case "Packed":
      return "warning";
    case "InTransit":
      return "info";
    case "Received":
    case "Reconciled":
      return "success";
    case "Cancelled":
      return "danger";
    default:
      return "default";
  }
}

export function isAmendable(status: string, cutoffAt: string, nowMs = Date.now()): boolean {
  return status === "Reserved" && Date.parse(cutoffAt) > nowMs;
}

export function formatCountdown(cutoffAt: string, nowMs = Date.now()): {
  open: boolean;
  label: string;
} {
  const remaining = Date.parse(cutoffAt) - nowMs;
  if (!Number.isFinite(remaining) || remaining <= 0) {
    return { open: false, label: "Cutoff passed" };
  }
  const totalMinutes = Math.floor(remaining / 60_000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours >= 24) {
    const days = Math.floor(hours / 24);
    return { open: true, label: `Cutoff in ${days}d ${hours % 24}h` };
  }
  if (hours > 0) {
    return { open: true, label: `Cutoff in ${hours}h ${minutes}m` };
  }
  return { open: true, label: `Cutoff in ${minutes}m` };
}
