import { ApiRequestError } from "@/lib/api-client";

export function describe(error: unknown, fallback: string): string {
  if (error instanceof ApiRequestError) {
    const validation = Object.values(error.problem?.errors ?? {}).flat().join(" ");
    return validation || error.problem?.detail || error.problem?.title || fallback;
  }
  return fallback;
}
