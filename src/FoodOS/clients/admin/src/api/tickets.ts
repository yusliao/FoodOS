import { apiFetch } from "@/lib/api-client";
import type { PagedResponse } from "@/api/catalog";

export type Ticket = {
  id: string; number: string; title: string; description?: string | null;
  status: "Open" | "InProgress" | "Resolved" | "Closed";
  priority: "Low" | "Medium" | "High" | "Critical";
  customerTenantId?: string | null; reporterUserId: string;
  assignedToUserId?: string | null; resolutionNote?: string | null;
  createdAtUtc: string; commentCount: number;
};
export type TicketComment = { id: string; ticketId: string; authorUserId: string; body: string; createdAtUtc: string };
export function getTicketTrash(page: number, signal?: AbortSignal) {
  return apiFetch<PagedResponse<Ticket>>(`/api/v1/tickets/trash?pageNumber=${page}&pageSize=20`, { signal });
}
export function deleteTicket(id: string) {
  return apiFetch<void>(`/api/v1/tickets/${encodeURIComponent(id)}`, { method: "DELETE" });
}
export function restoreTicket(input: { id: string; key: string }) {
  return apiFetch<string>(`/api/v1/tickets/${encodeURIComponent(input.id)}/restore`, { method: "POST", headers: { "Idempotency-Key": input.key } });
}
export function searchTickets(search: string, page: number, signal?: AbortSignal) {
  const query = new URLSearchParams({ search, pageNumber: String(page), pageSize: "20" });
  return apiFetch<PagedResponse<Ticket>>(`/api/v1/tickets?${query}`, { signal });
}
export function getTicket(id: string, signal?: AbortSignal) {
  return apiFetch<Ticket>(`/api/v1/tickets/${encodeURIComponent(id)}`, { signal });
}
export function getComments(id: string, signal?: AbortSignal) {
  return apiFetch<TicketComment[]>(`/api/v1/tickets/${encodeURIComponent(id)}/comments`, { signal });
}
export function addComment(input: { ticketId: string; body: string; key: string }) {
  return apiFetch<string>(`/api/v1/tickets/${encodeURIComponent(input.ticketId)}/comments`, {
    method: "POST", body: JSON.stringify({ body: input.body }), headers: { "Idempotency-Key": input.key },
  });
}
export type TicketAction = "assign" | "resolve" | "reopen" | "close";
export function saveTicket(input: { ticketId?: string; title: string; description: string | null; priority: Ticket["priority"]; key: string }) {
  return apiFetch<string>(input.ticketId ? `/api/v1/tickets/${encodeURIComponent(input.ticketId)}` : "/api/v1/tickets", {
    method: input.ticketId ? "PUT" : "POST",
    body: JSON.stringify({ title: input.title, description: input.description, priority: input.priority, ...(input.ticketId ? {} : { assignedToUserId: null }) }),
    headers: { "Idempotency-Key": input.key },
  });
}
export function actOnTicket(input: { ticketId: string; action: TicketAction; payload: { assigneeUserId?: string | null; resolutionNote?: string | null }; key: string }) {
  return apiFetch<string>(`/api/v1/tickets/${encodeURIComponent(input.ticketId)}/${input.action}`, {
    method: "POST", body: JSON.stringify(input.payload), headers: { "Idempotency-Key": input.key },
  });
}
