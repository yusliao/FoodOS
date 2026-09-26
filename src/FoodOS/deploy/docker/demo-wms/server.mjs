import { createHash, createHmac, randomUUID, timingSafeEqual } from "node:crypto";
import http from "node:http";

const port = Number.parseInt(process.env.PORT ?? "8080", 10);
const foodOsApiUrl = (process.env.FOODOS_API_URL ?? "http://api:8080").replace(/\/$/u, "");
const tenant = process.env.FOODOS_TENANT ?? "root";
const provider = process.env.WMS_PROVIDER ?? "demo-wms";
const connectionId = process.env.WMS_CONNECTION_ID ?? "customer-demo";
const signingSecret = process.env.WMS_SIGNING_SECRET ?? "";
const validModes = new Set(["accepted", "pending", "rejected", "shortage"]);
let mode = validModes.has(process.env.DEMO_WMS_MODE ?? "") ? process.env.DEMO_WMS_MODE : "accepted";
const operations = new Map();

if (signingSecret.length < 32) {
  throw new Error("WMS_SIGNING_SECRET must contain at least 32 characters.");
}

function json(response, status, value) {
  const body = JSON.stringify(value);
  response.writeHead(status, { "content-type": "application/json", "content-length": Buffer.byteLength(body) });
  response.end(body);
}

async function readBody(request) {
  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    size += chunk.length;
    if (size > 1024 * 1024) throw new Error("request_too_large");
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}

function signature(timestamp, body) {
  return `sha256=${createHmac("sha256", signingSecret).update(timestamp).update(".").update(body).digest("hex")}`;
}

function hasValidSignature(request, body) {
  const timestamp = request.headers["x-foodos-wms-timestamp"];
  const candidate = request.headers["x-foodos-wms-signature"];
  if (typeof timestamp !== "string" || typeof candidate !== "string") return false;
  if (Math.abs(Date.now() / 1000 - Number(timestamp)) > 300) return false;
  const expected = Buffer.from(signature(timestamp, body));
  const actual = Buffer.from(candidate);
  return expected.length === actual.length && timingSafeEqual(expected, actual);
}

async function sendFeedback(payload, eventType) {
  const now = new Date().toISOString();
  const revision = Math.max(1, Number(payload.revision ?? 1));
  const lines = (payload.lines ?? []).map((line, index) => ({
    lineId: line.lineId,
    sku: line.sku,
    uom: line.uom,
    quantity: eventType === "outbound.shortage" && index === 0 ? Math.min(1, Number(line.quantity)) : Number(line.quantity),
  }));
  const envelope = {
    messageId: randomUUID(),
    provider,
    connectionId,
    eventType,
    entityType: "outboundOrder",
    schemaVersion: "1.0",
    externalEventId: `demo:${eventType}:${payload.outboundOrderId}:r${revision}`,
    externalObjectId: String(payload.outboundOrderId),
    sequence: revision,
    occurredAt: now,
    sentAt: now,
    correlationId: `demo-${randomUUID()}`,
    causationId: null,
    idempotencyKey: `demo:${eventType}:${payload.outboundOrderId}:r${revision}`,
    payload: {
      outboundOrderId: String(payload.outboundOrderId),
      warehouseId: String(payload.warehouseId),
      ownerId: String(payload.ownerId),
      occurredAt: now,
      ...(eventType === "outbound.shortage" ? { reasonCode: "demo_shortage" } : {}),
      lines,
    },
  };
  const body = Buffer.from(JSON.stringify(envelope));
  for (let attempt = 1; attempt <= 5; attempt += 1) {
    const timestamp = Math.floor(Date.now() / 1000).toString();
    try {
      const response = await fetch(`${foodOsApiUrl}/api/v1/wms/inbound/events`, {
        method: "POST",
        headers: {
          "content-type": "application/json",
          tenant,
          "x-foodos-wms-timestamp": timestamp,
          "x-foodos-wms-signature": signature(timestamp, body),
        },
        body,
      });
      if (response.ok) return;
      console.error(`[demo-wms] callback ${eventType} failed with HTTP ${response.status}`);
    } catch (error) {
      console.error(`[demo-wms] callback ${eventType} attempt ${attempt} failed`, error);
    }
    await new Promise(resolve => setTimeout(resolve, attempt * 500));
  }
}

function acceptedResponse(key) {
  return {
    status: "accepted",
    externalOperationId: `demo-${createHash("sha256").update(key).digest("hex").slice(0, 16)}`,
    errorCode: null,
    detail: "Accepted by the FoodOS demo WMS adapter.",
    payload: null,
  };
}

const server = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url ?? "/", `http://${request.headers.host ?? "localhost"}`);
    if (request.method === "GET" && url.pathname === "/api/v1/health") {
      return json(response, 200, { status: "healthy", mode, provider, connectionId });
    }
    if (request.method === "GET" && url.pathname === "/demo/status") {
      return json(response, 200, { mode, operations: operations.size });
    }
    if (request.method === "POST" && url.pathname === "/demo/mode") {
      const value = JSON.parse((await readBody(request)).toString("utf8"));
      if (!validModes.has(value.mode)) return json(response, 400, { error: "invalid_mode", validModes: [...validModes] });
      mode = value.mode;
      return json(response, 200, { mode });
    }
    if (request.method !== "POST" || !url.pathname.startsWith("/api/v1/")) {
      return json(response, 404, { error: "not_found" });
    }

    const body = await readBody(request);
    if (!hasValidSignature(request, body)) return json(response, 401, { error: "invalid_signature" });
    const key = request.headers["idempotency-key"];
    if (typeof key !== "string" || key.length === 0) return json(response, 400, { error: "missing_idempotency_key" });
    const payloadHash = createHash("sha256").update(body).digest("hex");
    const prior = operations.get(key);
    if (prior && prior.payloadHash !== payloadHash) return json(response, 409, { error: "idempotency_conflict" });
    if (prior) return json(response, prior.statusCode, prior.response);

    if (mode === "pending") return json(response, 503, { error: "demo_wms_unavailable" });
    if (mode === "rejected") return json(response, 422, { error: "demo_wms_rejected" });

    const payload = JSON.parse(body.toString("utf8"));
    const value = acceptedResponse(key);
    operations.set(key, { payloadHash, statusCode: 200, response: value });
    json(response, 200, value);

    if (url.pathname === "/api/v1/outbound-orders") {
      const eventType = mode === "shortage" ? "outbound.shortage" : "outbound.allocated";
      setTimeout(() => void sendFeedback(payload, eventType), 1500);
    }
  } catch (error) {
    console.error("[demo-wms] request failed", error);
    if (!response.headersSent) json(response, 500, { error: "demo_wms_error" });
    else response.end();
  }
});

server.listen(port, "0.0.0.0", () => {
  console.log(`[demo-wms] listening on ${port}; mode=${mode}`);
});
