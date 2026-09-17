import { env } from "@/env";
import { ApiRequestError } from "@/lib/api-client";

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy" | string;

export type HealthEntry = {
  name: string;
  status: HealthStatus;
  description?: string | null;
  durationMs: number;
  details?: Record<string, unknown> | null;
};

export type HealthResult = {
  status: HealthStatus;
  results: HealthEntry[];
};

/**
 * Health probes are anonymous — bypass the apiClient so we don't drag the
 * tenant header / auth token into a public endpoint, and so we can read
 * the body on a 503 (apiClient would throw before parsing).
 */
async function fetchHealth(path: string, signal?: AbortSignal): Promise<HealthResult> {
  const url = `${env.apiBase}${path}`;
  const response = await fetch(url, {
    method: "GET",
    signal: AbortSignal.any([AbortSignal.timeout(8_000), ...(signal ? [signal] : [])]),
    credentials: "omit",
    headers: { Accept: "application/json" },
  });

  // /health/live returns 200; /health/ready returns 200 OR 503 with body.
  if (!response.ok && response.status !== 503) {
    throw new ApiRequestError(response.status, "Health probe request failed");
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) {
    throw new Error("Invalid health response");
  }

  const result: unknown = await response.json();
  const statusValid = (value: unknown) => value === "Healthy" || value === "Degraded" || value === "Unhealthy";
  if (!result || typeof result !== "object" || !("status" in result) || !statusValid(result.status)
    || !("results" in result) || !Array.isArray(result.results)
    || !result.results.every(entry => entry && typeof entry.name === "string" && statusValid(entry.status)
      && typeof entry.durationMs === "number" && Number.isFinite(entry.durationMs) && entry.durationMs >= 0
      && (entry.description == null || typeof entry.description === "string")
      && (entry.details == null || (typeof entry.details === "object" && !Array.isArray(entry.details))))) {
    throw new Error("Invalid health response");
  }
  return result as HealthResult;
}

export function getLiveness(signal?: AbortSignal): Promise<HealthResult> {
  return fetchHealth("/health/live", signal);
}

export function getReadiness(signal?: AbortSignal): Promise<HealthResult> {
  return fetchHealth("/health/ready", signal);
}
