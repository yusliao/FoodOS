import type { StoreContactDto, StoreContactInput } from "@/api/customers";

export type ContactDraft = { key: number; name: string; phone: string };

export const MAX_CONTACTS = 10;
let nextKey = 0;
export const newContactKey = () => nextKey++;

export function toContactDrafts(contacts: readonly StoreContactDto[] | null | undefined): ContactDraft[] {
  return (contacts ?? []).map(contact => ({ key: nextKey++, name: contact.name, phone: contact.phone }));
}

export function toContactInputs(drafts: readonly ContactDraft[]): StoreContactInput[] {
  return drafts.map(draft => ({ name: draft.name.trim(), phone: draft.phone.trim() }));
}

export const digits = (phone: string) => phone.replace(/\D/g, "");

/** Every contact needs a name and 5+ digits, and a number can only appear once. */
export function contactDraftsValid(drafts: readonly ContactDraft[]): boolean {
  const numbers = drafts.map(draft => digits(draft.phone));
  return drafts.every(draft => draft.name.trim().length > 0) && numbers.every(number => number.length >= 5)
    && new Set(numbers).size === numbers.length;
}
