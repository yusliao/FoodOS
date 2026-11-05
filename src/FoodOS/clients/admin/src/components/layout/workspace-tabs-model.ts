import {
  canSeeNavItem,
  sections,
  topNavBottom,
  topNavTop,
  type NavSpec,
} from "@/components/layout/nav-items";
import { IdentityPermissions } from "@/lib/permissions";

/**
 * 工作区页签的纯逻辑：路由 → 页签、页签增删的 reducer、按用户的本地持久化。
 * 页签标题与权限都从侧边栏 nav-items 推导，菜单改了这里自动跟着变。
 */

export const HOME_TAB_KEY = "/";
export const MAX_TABS = 10;

const STORAGE_PREFIX = "foodos.admin.tabs.";

/** 自带子导航的布局页：下一级子路径（如 /settings/profile）归入同一个页签。 */
const GROUPED_SECTIONS = new Set(["/settings", "/billing"]);

/** 侧边栏之外还需额外权限的子页面，与 routes.tsx 里的 RouteGuard 保持一致。 */
const EXTRA_PERMS: Record<string, readonly string[]> = {
  "/settings/sessions": [IdentityPermissions.Sessions.View],
};

const NAV_SPECS: NavSpec[] = [...topNavTop, ...sections.flatMap((s) => s.items), ...topNavBottom];

export type TabEntry = { key: string; url: string };

export type TabRoute = {
  key: string;
  nav: NavSpec;
  /** 详情页的路由参数（如订单 id），用于区分同一菜单下的多个详情页签。 */
  detail?: string;
  pinned: boolean;
};

export function pathOf(url: string): string {
  const path = url.split(/[?#]/, 1)[0].replace(/\/+$/, "");
  return path === "" ? "/" : path;
}

/** 按最长前缀匹配菜单项；匹配不上的路由（重定向入口、未知路径）不进页签。 */
export function resolveTabRoute(pathname: string): TabRoute | null {
  const path = pathOf(pathname);
  let best: NavSpec | null = null;
  for (const item of NAV_SPECS) {
    const hit = item.to === "/" ? path === "/" : path === item.to || path.startsWith(`${item.to}/`);
    if (hit && (!best || item.to.length > best.to.length)) best = item;
  }
  if (!best) return null;
  if (best.to === "/") return { key: HOME_TAB_KEY, nav: best, pinned: true };

  const rest = path.slice(best.to.length + 1).split("/").filter(Boolean);
  if (rest.length === 0 || (GROUPED_SECTIONS.has(best.to) && rest.length === 1)) {
    return { key: best.to, nav: best, pinned: false };
  }
  return { key: path, nav: best, detail: decodeSegment(rest[rest.length - 1]), pinned: false };
}

function decodeSegment(segment: string): string {
  try {
    return decodeURIComponent(segment);
  } catch {
    return segment;
  }
}

/** 无权限的路由不能出现在页签里：与侧边栏同一套可见性规则，再叠加 EXTRA_PERMS。 */
export function canOpenTab(pathname: string, granted: readonly string[]): boolean {
  const route = resolveTabRoute(pathname);
  if (!route) return false;
  const extra = EXTRA_PERMS[pathOf(pathname)] ?? [];
  return canSeeNavItem(route.nav, granted) && extra.every((p) => granted.includes(p));
}

// ─── Reducer ─────────────────────────────────────────────────────────────────

export type TabsState = {
  tabs: TabEntry[];
  /** 上一次访问：用来识别 StrictMode 重放和「新开页签随即被 replace 重定向」。 */
  last: { key: string | null; url: string; created: boolean };
};

export type TabsAction =
  /** key 为 null 表示当前路由不进页签（无菜单项或无权限）。 */
  | { type: "visit"; key: string | null; url: string; replace: boolean }
  | { type: "close"; key: string }
  | { type: "closeOthers"; key: string }
  | { type: "closeRight"; key: string }
  | { type: "closeAll" }
  | { type: "retain"; keep: (tab: TabEntry) => boolean };

const isPinned = (tab: TabEntry) => tab.key === HOME_TAB_KEY;

/** 超出上限时关掉最早打开的非固定页签（刚访问的那个除外）。 */
function enforceLimit(tabs: TabEntry[], keepKey: string): TabEntry[] {
  const next = [...tabs];
  while (next.length > MAX_TABS) {
    const idx = next.findIndex((t) => !isPinned(t) && t.key !== keepKey);
    if (idx < 0) break;
    next.splice(idx, 1);
  }
  return next;
}

function visit(state: TabsState, key: string | null, url: string, replace: boolean): TabsState {
  if (state.last.key === key && state.last.url === url) return state;
  if (key === null) return { ...state, last: { key: null, url, created: false } };

  // 刚新开的页签被 <Navigate replace> 重定向走了（如 /tenants/new → /tenants），这个中间页签不该留下。
  const redirected = replace && state.last.created ? state.last.key : null;
  const existingIdx = state.tabs.findIndex((t) => t.key === key);

  if (existingIdx >= 0) {
    // 同一页签内的查询参数变化：记住最后的 URL。
    const tabs = state.tabs
      .map((t, i) => (i === existingIdx && t.url !== url ? { key, url } : t))
      .filter((t) => t.key === key || t.key !== redirected);
    return { tabs, last: { key, url, created: false } };
  }

  const entry = { key, url };
  const redirectedIdx = redirected ? state.tabs.findIndex((t) => t.key === redirected) : -1;
  const tabs =
    redirectedIdx >= 0
      ? state.tabs.map((t, i) => (i === redirectedIdx ? entry : t))
      : [...state.tabs, entry];
  return { tabs: enforceLimit(tabs, key), last: { key, url, created: true } };
}

export function tabsReducer(state: TabsState, action: TabsAction): TabsState {
  switch (action.type) {
    case "visit":
      return visit(state, action.key, action.url, action.replace);
    case "close":
      return withTabs(state, state.tabs.filter((t) => isPinned(t) || t.key !== action.key));
    case "closeOthers":
      return withTabs(state, state.tabs.filter((t) => isPinned(t) || t.key === action.key));
    case "closeRight": {
      const idx = state.tabs.findIndex((t) => t.key === action.key);
      return idx < 0 ? state : withTabs(state, state.tabs.slice(0, idx + 1));
    }
    case "closeAll":
      return withTabs(state, state.tabs.filter(isPinned));
    case "retain":
      return withTabs(state, state.tabs.filter((t) => isPinned(t) || action.keep(t)));
  }
}

/** 页签没变就返回原 state，避免无谓的重渲染和写存储。 */
function withTabs(state: TabsState, tabs: TabEntry[]): TabsState {
  return tabs.length === state.tabs.length ? state : { ...state, tabs };
}

// ─── 持久化（按用户存 localStorage，读写都兜底） ────────────────────────────

export function tabsStorageKey(userId: string): string {
  return `${STORAGE_PREFIX}${userId}`;
}

export function loadTabsState(storageKey: string): TabsState {
  const tabs: TabEntry[] = [{ key: HOME_TAB_KEY, url: "/" }];
  let stored: unknown = [];
  try {
    stored = JSON.parse(window.localStorage.getItem(storageKey) ?? "[]");
  } catch {
    stored = [];
  }
  if (Array.isArray(stored)) {
    for (const url of stored) {
      if (typeof url !== "string" || !url.startsWith("/")) continue;
      const route = resolveTabRoute(url);
      if (!route) continue;
      if (route.pinned) tabs[0] = { key: HOME_TAB_KEY, url };
      else if (!tabs.some((t) => t.key === route.key)) tabs.push({ key: route.key, url });
    }
  }
  return { tabs: tabs.slice(0, MAX_TABS), last: { key: null, url: "", created: false } };
}

export function saveTabs(storageKey: string, tabs: TabEntry[]) {
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(tabs.map((t) => t.url)));
  } catch {
    /* storage unavailable */
  }
}

/** 清掉页签存储；传 exceptKey 时保留当前用户的那份（切换用户时清掉别人的）。 */
export function clearStoredTabs(exceptKey?: string) {
  try {
    const storage = window.localStorage;
    for (let i = storage.length - 1; i >= 0; i--) {
      const key = storage.key(i);
      if (key?.startsWith(STORAGE_PREFIX) && key !== exceptKey) storage.removeItem(key);
    }
  } catch {
    /* storage unavailable */
  }
}
