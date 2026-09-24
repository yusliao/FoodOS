import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { createHmac, randomUUID } from "node:crypto";

const apiBase = process.env.PLAYWRIGHT_REAL_API_URL ?? "http://localhost:5030";
const tenant = "root";
const email = "superadmin@root.com";
const password = "Password123!";

async function signIn(page: Page, userEmail = email) {
  await page.goto("/login");
  await expect(page.getByLabel("Tenant")).toHaveValue(tenant);
  await page.getByLabel("Email").fill(userEmail);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/$/);
}

function createTotp(sharedKey: string) {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  const normalized = sharedKey.replace(/=+$/u, "").replace(/\s/gu, "").toUpperCase();
  let bits = "";
  for (const character of normalized) {
    const value = alphabet.indexOf(character);
    if (value < 0) throw new Error("The authenticator key is not valid Base32.");
    bits += value.toString(2).padStart(5, "0");
  }

  const key = Buffer.from(
    Array.from({ length: Math.floor(bits.length / 8) }, (_, index) =>
      Number.parseInt(bits.slice(index * 8, index * 8 + 8), 2)),
  );
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000)));
  const digest = createHmac("sha1", key).update(counter).digest();
  const offset = digest[digest.length - 1] & 0x0f;
  const binary = (
    ((digest[offset] & 0x7f) << 24)
    | ((digest[offset + 1] & 0xff) << 16)
    | ((digest[offset + 2] & 0xff) << 8)
    | (digest[offset + 3] & 0xff)
  ) >>> 0;
  return (binary % 1_000_000).toString().padStart(6, "0");
}

test("operator signs in and loads the workbench with real permissions", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByLabel("Tenant")).toHaveValue(tenant);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);

  const tokenResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/identity/token/issue")
    && response.request().method() === "POST");
  const permissionsResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/identity/permissions")
    && response.request().method() === "GET");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();

  expect((await tokenResponse).status()).toBe(200);
  const permissions = await permissionsResponse;
  expect(permissions.status()).toBe(200);
  expect((await permissions.json() as string[]).length).toBeGreaterThan(0);
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("main").getByRole("heading", { name: "Operator workbench" })).toBeVisible();
});

test("operator completes a real authenticator challenge and can disable it again", async ({ page, request }) => {
  let sharedKey = "";
  let enabled = false;

  try {
    await page.goto("/login");
    await expect(page.getByLabel("Tenant")).toHaveValue(tenant);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(password);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/$/);

    await page.goto("/settings/security");
    const enrollResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/2fa/enroll")
      && response.request().method() === "POST");
    await page.getByRole("button", { name: "Enable two-factor", exact: true }).click();
    expect((await enrollResponse).status()).toBe(200);
    sharedKey = (await page.locator("code").first().textContent())?.trim() ?? "";
    expect(sharedKey).not.toBe("");

    const verifyResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/2fa/verify")
      && response.request().method() === "POST");
    await page.getByLabel("6-digit code from your app").fill(createTotp(sharedKey));
    await page.getByRole("button", { name: "Confirm & enable", exact: true }).click();
    expect((await verifyResponse).status()).toBe(200);
    enabled = true;
    await expect(page.getByRole("button", { name: "Disable two-factor", exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Open profile menu", exact: true }).click();
    await page.getByRole("menuitem", { name: "Sign out", exact: true }).click();
    await page.getByRole("dialog", { name: "Sign out?" })
      .getByRole("button", { name: "Sign out", exact: true })
      .click();
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByLabel("Tenant")).toHaveValue(tenant);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(password);

    const challengeResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/token/issue")
      && response.request().method() === "POST");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    expect((await challengeResponse).status()).toBe(401);
    await expect(page.getByLabel("Authenticator code")).toBeVisible();

    const loginResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/token/issue")
      && response.request().method() === "POST");
    await page.getByLabel("Authenticator code").fill(createTotp(sharedKey));
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    expect((await loginResponse).status()).toBe(200);
    await expect(page).toHaveURL(/\/settings\/security$/);

    await page.getByLabel("Current password").fill(password);
    const disableResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/2fa/disable")
      && response.request().method() === "POST");
    await page.getByRole("button", { name: "Disable two-factor", exact: true }).click();
    expect((await disableResponse).status()).toBe(200);
    enabled = false;
    await expect(page.getByRole("button", { name: "Enable two-factor", exact: true })).toBeVisible();
  } finally {
    if (enabled && sharedKey) {
      const tokenResponse = await request.post(`${apiBase}/api/v1/identity/token/issue`, {
        data: { email, password, twoFactorCode: createTotp(sharedKey) },
        headers: { tenant, "X-FSH-App": "admin" },
      });
      if (tokenResponse.ok()) {
        const token = await tokenResponse.json() as { accessToken: string };
        await request.post(`${apiBase}/api/v1/identity/2fa/disable`, {
          data: { currentPassword: password },
          headers: { tenant, Authorization: `Bearer ${token.accessToken}` },
        });
      }
    }
  }
});

test("operator receives a real chat event without refreshing the page", async ({ page, request }) => {
  let accessToken = "";
  let channelId = "";

  try {
    const hubSocketPromise = page.waitForEvent("websocket", {
      predicate: socket => socket.url().includes("/api/v1/realtime/hub"),
      timeout: 15_000,
    });

    await page.goto("/login");
    await expect(page.getByLabel("Tenant")).toHaveValue(tenant);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(password);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/$/);

    const hubSocket = await hubSocketPromise;
    expect(hubSocket.isClosed()).toBe(false);
    accessToken = await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken") ?? "");
    expect(accessToken).not.toBe("");

    const createResponse = await request.post(`${apiBase}/api/v1/chat/channels`, {
      data: {
        name: `real-realtime-${randomUUID().slice(0, 8)}`,
        description: "Disposable browser-to-backend realtime verification",
        isPrivate: true,
      },
      headers: { tenant, Authorization: `Bearer ${accessToken}` },
    });
    expect(createResponse.status()).toBe(200);
    channelId = await createResponse.json() as string;

    const initialMessages = page.waitForResponse(response =>
      response.url().includes(`/api/v1/chat/channels/${channelId}/messages`)
      && response.request().method() === "GET");
    await page.goto(`/chat/${channelId}`);
    expect((await initialMessages).status()).toBe(200);

    const messageBody = `Realtime ${randomUUID()}`;
    const refreshedMessages = page.waitForResponse(response =>
      response.url().includes(`/api/v1/chat/channels/${channelId}/messages`)
      && response.request().method() === "GET");
    const sendResponse = await request.post(`${apiBase}/api/v1/chat/channels/${channelId}/messages`, {
      data: { body: messageBody, parentMessageId: null, attachments: [] },
      headers: {
        tenant,
        Authorization: `Bearer ${accessToken}`,
        "Idempotency-Key": randomUUID(),
      },
    });
    expect(sendResponse.status()).toBe(200);
    expect((await refreshedMessages).status()).toBe(200);
    await expect(page.getByText(messageBody, { exact: true })).toBeVisible();
  } finally {
    if (accessToken && channelId) {
      await request.delete(`${apiBase}/api/v1/chat/channels/${channelId}`, {
        headers: { tenant, Authorization: `Bearer ${accessToken}` },
      });
    }
  }
});

test("chat catches up from authoritative state after the realtime connection recovers", async ({ page, request }) => {
  let accessToken = "";
  let channelId = "";
  let connectionCount = 0;
  let closeActive: (() => Promise<void>) | undefined;

  try {
    await page.routeWebSocket(/\/api\/v1\/realtime\/hub/u, socket => {
      connectionCount += 1;
      closeActive = () => socket.close({ code: 1012, reason: "Real reconnect verification" });
      socket.connectToServer();
    });
    await signIn(page);
    await expect.poll(() => connectionCount).toBe(1);
    accessToken = await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken") ?? "");
    expect(accessToken).not.toBe("");
    const headers = { tenant, Authorization: `Bearer ${accessToken}` };

    const createResponse = await request.post(`${apiBase}/api/v1/chat/channels`, {
      data: {
        name: `real-reconnect-${randomUUID().slice(0, 8)}`,
        description: "Disposable realtime reconnect verification",
        isPrivate: true,
      },
      headers,
    });
    expect(createResponse.status()).toBe(200);
    channelId = await createResponse.json() as string;
    await page.goto(`/chat/${channelId}`);

    await closeActive?.();

    const messageBody = `Missed while offline ${randomUUID()}`;
    const sendResponse = await request.post(`${apiBase}/api/v1/chat/channels/${channelId}/messages`, {
      data: { body: messageBody, parentMessageId: null, attachments: [] },
      headers: { ...headers, "Idempotency-Key": randomUUID() },
    });
    expect(sendResponse.status()).toBe(200);

    const catchUpResponse = page.waitForResponse(response =>
      response.url().includes(`/api/v1/chat/channels/${channelId}/messages`)
      && response.request().method() === "GET", { timeout: 20_000 });
    await expect.poll(() => connectionCount, { timeout: 20_000 }).toBe(2);
    expect((await catchUpResponse).status()).toBe(200);
    await expect(page.getByText(messageBody, { exact: true })).toBeVisible();
  } finally {
    if (accessToken && channelId) {
      await request.delete(`${apiBase}/api/v1/chat/channels/${channelId}`, {
        headers: { tenant, Authorization: `Bearer ${accessToken}` },
      });
    }
  }
});

test("operator uploads, sends, and downloads a chat attachment through real object storage", async ({ page, request }) => {
  let accessToken = "";
  let channelId = "";
  let fileAssetId = "";
  const contents = Buffer.from(`FoodOS real attachment ${randomUUID()}\n`, "utf8");
  const fileName = `real-attachment-${randomUUID().slice(0, 8)}.txt`;

  try {
    await signIn(page);
    accessToken = await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken") ?? "");
    expect(accessToken).not.toBe("");
    const headers = { tenant, Authorization: `Bearer ${accessToken}` };

    const createResponse = await request.post(`${apiBase}/api/v1/chat/channels`, {
      data: {
        name: `real-attachment-${randomUUID().slice(0, 8)}`,
        description: "Disposable browser-to-object-storage verification",
        isPrivate: true,
      },
      headers,
    });
    expect(createResponse.status()).toBe(200);
    channelId = await createResponse.json() as string;

    await page.goto(`/chat/${channelId}`);
    const form = page.getByRole("form", { name: "Message" });
    const uploadUrlResponse = page.waitForResponse(response =>
      response.url().endsWith("/api/v1/files/upload-url")
      && response.request().method() === "POST");
    const storagePutResponse = page.waitForResponse(response =>
      response.request().method() === "PUT"
      && decodeURIComponent(new URL(response.url()).pathname).endsWith(`/${fileName}`));
    const finalizeResponse = page.waitForResponse(response =>
      response.url().includes("/api/v1/files/")
      && response.url().endsWith("/finalize")
      && response.request().method() === "POST");

    await form.getByLabel("Attachment").setInputFiles({
      name: fileName,
      mimeType: "text/plain",
      buffer: contents,
    });

    const uploadUrl = await uploadUrlResponse;
    expect(uploadUrl.status()).toBe(200);
    fileAssetId = (await uploadUrl.json() as { fileAssetId: string }).fileAssetId;
    expect((await storagePutResponse).status()).toBe(200);
    expect((await finalizeResponse).status()).toBe(200);
    await expect(form.getByText(new RegExp(fileName))).toBeVisible();

    const sendResponse = page.waitForResponse(response =>
      response.url().endsWith(`/api/v1/chat/channels/${channelId}/messages`)
      && response.request().method() === "POST");
    await form.getByRole("button", { name: "Send message", exact: true }).click();
    expect((await sendResponse).status()).toBe(200);
    await expect(page.getByText(fileName, { exact: true })).toBeVisible();

    const downloadUrlResponse = page.waitForResponse(response =>
      response.url().endsWith(`/api/v1/files/${fileAssetId}/url`)
      && response.request().method() === "GET");
    await page.getByRole("button", { name: "Prepare download", exact: true }).click();
    expect((await downloadUrlResponse).status()).toBe(200);
    const downloadLink = page.getByRole("link", { name: "Download attachment", exact: true });
    const href = await downloadLink.getAttribute("href");
    const downloadUrl = new URL(href!);
    expect(["http:", "https:"]).toContain(downloadUrl.protocol);
    expect(decodeURIComponent(downloadUrl.pathname).endsWith(`/${fileName}`)).toBe(true);

    const downloadResponse = await request.get(href!);
    expect(downloadResponse.status()).toBe(200);
    expect(await downloadResponse.body()).toEqual(contents);
  } finally {
    if (accessToken && fileAssetId) {
      await request.delete(`${apiBase}/api/v1/files/${fileAssetId}`, {
        headers: { tenant, Authorization: `Bearer ${accessToken}` },
      });
    }
    if (accessToken && channelId) {
      await request.delete(`${apiBase}/api/v1/chat/channels/${channelId}`, {
        headers: { tenant, Authorization: `Bearer ${accessToken}` },
      });
    }
  }
});

test("removed channel member loses the open page through realtime", async ({ page, request, browser }) => {
  let accessToken = "";
  let channelId = "";
  let memberUserId = "";
  let memberContext: BrowserContext | undefined;

  try {
    await signIn(page);
    accessToken = await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken") ?? "");
    expect(accessToken).not.toBe("");
    const adminHeaders = { tenant, Authorization: `Bearer ${accessToken}` };

    const suffix = randomUUID().slice(0, 8);
    const memberEmail = `realtime-member-${suffix}@root.test`;
    const registerResponse = await request.post(`${apiBase}/api/v1/identity/register`, {
      data: {
        firstName: "Realtime",
        lastName: "Member",
        email: memberEmail,
        userName: `realtime.member.${suffix}`,
        password,
        confirmPassword: password,
        phoneNumber: "",
      },
      headers: adminHeaders,
    });
    expect(registerResponse.status()).toBe(201);
    memberUserId = (await registerResponse.json() as { userId: string }).userId;
    const confirmResponse = await request.post(
      `${apiBase}/api/v1/identity/users/${memberUserId}/confirm-email`,
      { headers: adminHeaders },
    );
    expect(confirmResponse.status()).toBe(204);

    const createResponse = await request.post(`${apiBase}/api/v1/chat/channels`, {
      data: {
        name: `real-removal-${randomUUID().slice(0, 8)}`,
        description: "Disposable realtime revocation verification",
        isPrivate: true,
      },
      headers: adminHeaders,
    });
    expect(createResponse.status()).toBe(200);
    channelId = await createResponse.json() as string;

    const addResponse = await request.post(`${apiBase}/api/v1/chat/channels/${channelId}/members`, {
      data: { userIds: [memberUserId] },
      headers: adminHeaders,
    });
    expect(addResponse.status()).toBe(204);

    memberContext = await browser.newContext({ baseURL: "http://localhost:5273", locale: "en-US" });
    const memberPage = await memberContext.newPage();
    const memberSocketPromise = memberPage.waitForEvent("websocket", {
      predicate: socket => socket.url().includes("/api/v1/realtime/hub"),
      timeout: 15_000,
    });
    await signIn(memberPage, memberEmail);
    const memberSocket = await memberSocketPromise;
    expect(memberSocket.isClosed()).toBe(false);

    const initialChannel = memberPage.waitForResponse(response =>
      new URL(response.url()).pathname.endsWith(`/api/v1/chat/channels/${channelId}`)
      && response.request().method() === "GET");
    await memberPage.goto(`/chat/${channelId}`);
    expect((await initialChannel).status()).toBe(200);

    const revokedChannel = memberPage.waitForResponse(response =>
      new URL(response.url()).pathname.endsWith(`/api/v1/chat/channels/${channelId}`)
      && response.request().method() === "GET");
    const removeResponse = await request.delete(
      `${apiBase}/api/v1/chat/channels/${channelId}/members/${memberUserId}`,
      { headers: adminHeaders },
    );
    expect(removeResponse.status()).toBe(204);
    expect((await revokedChannel).status()).toBe(404);
    await expect(memberPage.getByRole("alert")).toBeVisible();
  } finally {
    await memberContext?.close();
    if (accessToken && channelId) {
      await request.delete(`${apiBase}/api/v1/chat/channels/${channelId}`, {
        headers: { tenant, Authorization: `Bearer ${accessToken}` },
      });
    }
    if (accessToken && memberUserId) {
      await request.delete(`${apiBase}/api/v1/identity/users/${memberUserId}`, {
        headers: { tenant, Authorization: `Bearer ${accessToken}` },
      });
    }
  }
});
