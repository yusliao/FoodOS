import { useEffect, useRef, useState, type ReactNode } from "react";
import { Check, ChevronDown } from "lucide-react";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";

type Option = { value: string; label: string; disabled?: boolean };

export function SearchableSelect({ id, label, value, onChange, options, placeholder, selectedLabel, search, onSearchChange, searchLabel, searchable = true, disabled, loading, children, onOpenChange }: {
  id?: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: Option[];
  placeholder?: string;
  selectedLabel?: string;
  search?: string;
  onSearchChange?: (search: string) => void;
  searchLabel: string;
  searchable?: boolean;
  disabled?: boolean;
  loading?: boolean;
  children?: ReactNode;
  onOpenChange?: (open: boolean) => void;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState("");
  const [selected, setSelected] = useState<Option>();
  const inputRef = useRef<HTMLInputElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const current = options.find(option => option.value === value);
  const currentValue = current?.value;
  const currentLabel = current?.label;
  useEffect(() => {
    if (currentValue !== undefined && currentLabel !== undefined) setSelected({ value: currentValue, label: currentLabel });
  }, [currentValue, currentLabel]);
  useEffect(() => {
    if (!open) return;
    const timer = setTimeout(() => inputRef.current?.focus(), 0);
    return () => clearTimeout(timer);
  }, [open]);
  const text = search ?? filter;
  const matches = onSearchChange ? options : options.filter(option => option.label.toLocaleLowerCase().includes(text.trim().toLocaleLowerCase()));
  const display = selectedLabel ?? current?.label ?? (selected?.value === value ? selected.label : value);
  const choices = value && !matches.some(option => option.value === value) ? [{ value, label: display }, ...matches] : matches;
  return <DropdownMenu open={open && !disabled} onOpenChange={next => { if (!disabled) { setOpen(next); onOpenChange?.(next); } }}>
    <DropdownMenuTrigger id={id} type="button" aria-label={label} disabled={disabled} className="flex h-10 w-full min-w-0 items-center justify-between gap-2 rounded-lg border bg-[var(--color-card)] px-3 text-left text-sm">
      <span className="truncate">{display || placeholder || searchLabel}</span><ChevronDown aria-hidden className="h-4 w-4 shrink-0" />
    </DropdownMenuTrigger>
    <DropdownMenuContent ref={menuRef} aria-label={label} align="start" className="w-[var(--radix-dropdown-menu-trigger-width)] min-w-0 space-y-2 p-2">
      {searchable && <Input ref={inputRef} type="search" aria-label={searchLabel} placeholder={searchLabel} value={text} onChange={event => onSearchChange ? onSearchChange(event.target.value) : setFilter(event.target.value)} onKeyDown={event => {
        if (event.key === "Escape") return;
        event.stopPropagation();
        if (event.key === "Tab") { setOpen(false); onOpenChange?.(false); }
        if (event.key === "Enter") event.preventDefault();
        if (event.key === "ArrowDown") {
          event.preventDefault();
          menuRef.current?.querySelector<HTMLElement>('[role="menuitemradio"]:not([data-disabled])')?.focus();
        }
      }} />}
      {!loading && !onSearchChange && matches.length === 0 && <p role="status">{t("common.emptyDefault")}</p>}
      <div className="max-h-52 overflow-y-auto">
        {choices.map(option => <DropdownMenuItem key={option.value} role="menuitemradio" aria-checked={value === option.value} disabled={disabled || loading || option.disabled} onSelect={() => {
          setSelected(option);
          onChange(option.value);
          setOpen(false);
          onOpenChange?.(false);
        }}>
          <span className="min-w-0 flex-1 break-words">{option.label}</span>{value === option.value && <Check aria-hidden className="h-4 w-4 shrink-0" />}
        </DropdownMenuItem>)}
      </div>
      {children}
    </DropdownMenuContent>
  </DropdownMenu>;
}
