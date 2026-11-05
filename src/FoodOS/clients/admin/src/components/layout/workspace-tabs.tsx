import { useEffect, useMemo, useReducer, useRef, useState, type KeyboardEvent } from "react";
import { Link, NavigationType, useLocation, useNavigate, useNavigationType } from "react-router-dom";
import { ChevronDown, X } from "lucide-react";
import { cn } from "@/lib/cn";
import { useAuth } from "@/auth/use-auth";
import { useT } from "@/i18n/locale-provider";
import { messageKeyForNavItem } from "@/i18n/locale-store";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  HOME_TAB_KEY,
  canOpenTab,
  clearStoredTabs,
  loadTabsState,
  resolveTabRoute,
  saveTabs,
  tabsReducer,
  tabsStorageKey,
  type TabEntry,
  type TabsAction,
} from "@/components/layout/workspace-tabs-model";

/**
 * WorkspaceTabs — 内容区顶部的多页签栏。
 *
 * 任何导航（侧边栏、页内链接、浏览器前进后退）都会新增或激活对应页签；
 * 页面状态靠 TanStack Query 缓存保留，不做组件 keep-alive，路由守卫照常生效。
 * 由 AppShell 以 user.id 为 key 挂载，换用户时整体重建。
 */
export function WorkspaceTabs({ userId }: { userId: string }) {
  const t = useT();
  const location = useLocation();
  const navigationType = useNavigationType();
  const navigate = useNavigate();
  const { user } = useAuth();
  const storageKey = tabsStorageKey(userId);
  const [state, dispatch] = useReducer(tabsReducer, storageKey, loadTabsState);
  const [menuKey, setMenuKey] = useState<string | null>(null);
  const tabRefs = useRef(new Map<string, HTMLAnchorElement>());

  const grantedKey = (user?.permissions ?? []).join(",");
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const granted = useMemo(() => user?.permissions ?? [], [grantedKey]);
  const url = `${location.pathname}${location.search}${location.hash}`;
  const replace = navigationType === NavigationType.Replace;

  // 只在路由变化时登记页签；关闭页签不会触发这里，所以刚关掉的页签不会被加回来。
  useEffect(() => {
    const route = resolveTabRoute(location.pathname);
    const key = route && canOpenTab(location.pathname, granted) ? route.key : null;
    dispatch({ type: "visit", key, url, replace });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [url]);

  // 权限变化（含刷新后从存储恢复）时剔除已无权访问的页签。
  useEffect(() => {
    dispatch({ type: "retain", keep: (tab) => canOpenTab(tab.url, granted) });
  }, [granted]);

  useEffect(() => saveTabs(storageKey, state.tabs), [storageKey, state.tabs]);
  // 只保留当前用户的页签存储，切换用户后上一个人的页签不残留。
  useEffect(() => clearStoredTabs(storageKey), [storageKey]);

  const { tabs } = state;
  const currentKey = resolveTabRoute(location.pathname)?.key;
  const activeKey = tabs.some((tab) => tab.key === currentKey) ? currentKey : null;

  useEffect(() => {
    if (activeKey) tabRefs.current.get(activeKey)?.scrollIntoView?.({ block: "nearest", inline: "nearest" });
  }, [activeKey]);

  /** 执行关闭类动作；当前页签被关掉时跳到 fallback。 */
  const run = (action: TabsAction, fallback?: TabEntry) => {
    const next = tabsReducer(state, action).tabs;
    dispatch(action);
    if (activeKey && !next.some((tab) => tab.key === activeKey) && fallback) navigate(fallback.url);
  };

  const closeTab = (key: string) => {
    const idx = tabs.findIndex((tab) => tab.key === key);
    if (idx <= 0) return;
    run({ type: "close", key }, tabs[idx + 1] ?? tabs[idx - 1]);
  };

  const titleOf = (tab: TabEntry) => {
    const route = resolveTabRoute(tab.url);
    if (!route) return tab.url;
    const label = t(messageKeyForNavItem(route.nav.to), route.nav.label);
    if (!route.detail) return label;
    const short = route.detail.length > 8 ? `${route.detail.slice(0, 8)}…` : route.detail;
    return `${label} · ${short}`;
  };

  const onTabKeyDown = (event: KeyboardEvent<HTMLAnchorElement>, idx: number) => {
    if (event.key === "Delete") {
      event.preventDefault();
      closeTab(tabs[idx].key);
      return;
    }
    const step = event.key === "ArrowRight" ? 1 : event.key === "ArrowLeft" ? -1 : 0;
    if (step === 0) return;
    event.preventDefault();
    const target = tabs[(idx + step + tabs.length) % tabs.length];
    tabRefs.current.get(target.key)?.focus();
  };

  const menuItems = (key: string) => {
    const idx = tabs.findIndex((tab) => tab.key === key);
    const target = tabs[idx];
    if (!target) return null;
    return (
      <>
        <DropdownMenuItem disabled={key === HOME_TAB_KEY} onSelect={() => closeTab(key)}>
          {t("tabs.close")}
        </DropdownMenuItem>
        <DropdownMenuItem
          disabled={tabs.every((tab) => tab.key === HOME_TAB_KEY || tab.key === key)}
          onSelect={() => run({ type: "closeOthers", key }, target)}
        >
          {t("tabs.closeOthers")}
        </DropdownMenuItem>
        <DropdownMenuItem
          disabled={idx === tabs.length - 1}
          onSelect={() => run({ type: "closeRight", key }, target)}
        >
          {t("tabs.closeRight")}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          disabled={tabs.length <= 1}
          onSelect={() => run({ type: "closeAll" }, tabs[0])}
        >
          {t("tabs.closeAll")}
        </DropdownMenuItem>
      </>
    );
  };

  return (
    <div
      data-testid="workspace-tabs"
      className="flex h-10 shrink-0 items-center gap-1 border-b border-[var(--color-border)] bg-[var(--color-background)] pl-2 pr-1 md:pl-4 md:pr-3"
    >
      <div
        role="tablist"
        aria-label={t("tabs.label")}
        className="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
      >
        {tabs.map((tab, idx) => {
          const isActive = tab.key === activeKey;
          const pinned = tab.key === HOME_TAB_KEY;
          const route = resolveTabRoute(tab.url);
          const Icon = route?.nav.icon;
          const title = titleOf(tab);
          return (
            <div
              key={tab.key}
              role="presentation"
              className={cn(
                "group/tab relative flex h-8 shrink-0 items-center rounded-md border text-[13px] font-medium",
                "transition-colors duration-[var(--duration-fast)] ease-[var(--ease-out-cubic)]",
                isActive
                  ? "border-[oklch(from_var(--color-primary)_l_c_h_/_0.25)] bg-[var(--color-primary-soft)] text-[var(--color-primary)]"
                  : "border-transparent text-[var(--color-muted-foreground)] hover:bg-[var(--color-accent)] hover:text-[var(--color-foreground)]",
              )}
            >
              <Link
                ref={(el) => {
                  if (el) tabRefs.current.set(tab.key, el);
                  else tabRefs.current.delete(tab.key);
                }}
                to={tab.url}
                role="tab"
                aria-selected={isActive}
                tabIndex={isActive || (!activeKey && pinned) ? 0 : -1}
                title={title}
                data-tab-key={tab.key}
                onKeyDown={(event) => onTabKeyDown(event, idx)}
                onContextMenu={(event) => {
                  event.preventDefault();
                  setMenuKey(tab.key);
                }}
                className={cn(
                  "flex h-full items-center gap-1.5 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]",
                  pinned ? "px-3" : "pl-3 pr-1",
                )}
              >
                {Icon && <Icon className="h-3.5 w-3.5 shrink-0" aria-hidden />}
                <span className="max-w-[11rem] truncate">{title}</span>
              </Link>
              {!pinned && (
                <button
                  type="button"
                  tabIndex={-1}
                  aria-label={t("tabs.closeTab").replace("{title}", title)}
                  onClick={() => closeTab(tab.key)}
                  className="mr-1 grid h-5 w-5 cursor-pointer place-items-center rounded opacity-60 hover:bg-[var(--color-accent)] hover:opacity-100 group-hover/tab:opacity-100"
                >
                  <X className="h-3 w-3" aria-hidden />
                </button>
              )}
              {/* 右键菜单：触发器铺满页签但不吃鼠标事件，只让菜单定位到页签下方。 */}
              <DropdownMenu
                open={menuKey === tab.key}
                onOpenChange={(open) => {
                  if (!open) setMenuKey(null);
                }}
              >
                <DropdownMenuTrigger asChild>
                  <span aria-hidden className="pointer-events-none absolute inset-0" />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start">{menuItems(tab.key)}</DropdownMenuContent>
              </DropdownMenu>
            </div>
          );
        })}
      </div>

      <DropdownMenu>
        <DropdownMenuTrigger
          aria-label={t("tabs.actions")}
          title={t("tabs.actions")}
          className="grid h-7 w-7 shrink-0 cursor-pointer place-items-center rounded-md text-[var(--color-muted-foreground)] hover:bg-[var(--color-accent)] hover:text-[var(--color-foreground)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]"
        >
          <ChevronDown className="h-4 w-4" aria-hidden />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">{menuItems(activeKey ?? HOME_TAB_KEY)}</DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
