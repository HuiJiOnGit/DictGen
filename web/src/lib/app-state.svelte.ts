/** 全局应用状态(Svelte 5 runes)。 */
import type { ObjectType } from "./types";

export const appState = $state({
  /** 桌面端侧栏是否折叠 */
  collapsed: false,
  /** 小屏(<900px)抽屉侧栏是否打开 */
  mobileOpen: false,
  /** 当前亮/暗主题 */
  theme: "light" as "light" | "dark",
  /** 顶栏全局搜索词(非空时主区显示搜索结果) */
  search: "",
  /** 各类型搜索结果展开条数 */
  searchMore: { T: 50, V: 50, P: 50 } as Record<ObjectType, number>,
  /** 侧栏名称二次筛选词 */
  nameFilter: "",
});

function loadPrefs() {
  try {
    const t = localStorage.getItem("dict-theme");
    if (t === "dark" || t === "light") appState.theme = t;
    else if (window.matchMedia?.("(prefers-color-scheme: dark)").matches) appState.theme = "dark";
    appState.collapsed = localStorage.getItem("dict-sidebar") === "1";
  } catch {
    /* file:// 可能禁用 localStorage */
  }
}

type RunningTransition = { skipTransition(): void };
let runningVT: RunningTransition | null = null;

/**
 * 切换亮/暗主题。传入点击事件时使用 View Transitions API 做"圆形扩散"过渡:
 * 亮→暗,暗色自点击处扩散铺满;暗→亮,暗色向点击处收起。
 * 圆形动画由 CSS keyframes 驱动(见 global.css),JS 只负责注入圆心/半径和方向类。
 * 不支持该 API 或用户偏好减少动效时,直接瞬时切换。
 */
export function toggleTheme(e?: MouseEvent): void {
  const next = appState.theme === "dark" ? "light" : "dark";
  const root = document.documentElement;
  try {
    localStorage.setItem("dict-theme", next);
  } catch {
    /* ignore */
  }

  const reduce = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
  if (!document.startViewTransition || reduce || !e) {
    appState.theme = next;
    root.dataset.theme = next;
    return;
  }

  // 圆心:键盘触发(clientX/Y 均为 0)时取按钮中心
  let x = e.clientX;
  let y = e.clientY;
  if (!x && !y) {
    const r = (e.currentTarget as HTMLElement | null)?.getBoundingClientRect();
    if (r) {
      x = r.left + r.width / 2;
      y = r.top + r.height / 2;
    }
  }
  const radius = Math.hypot(Math.max(x, window.innerWidth - x), Math.max(y, window.innerHeight - y));
  const expanding = next === "dark";

  // 圆心/半径写入 CSS 变量,keyframes 据此绘制圆形裁剪(变量继承进伪元素树)
  root.style.setProperty("--vt-x", `${x}px`);
  root.style.setProperty("--vt-y", `${y}px`);
  root.style.setProperty("--vt-r", `${radius}px`);
  // 方向类决定哪个伪元素在上层、播揭示还是收起动画;必须挂好后再开启过渡
  root.classList.remove("vt-expand", "vt-shrink");
  root.classList.add(expanding ? "vt-expand" : "vt-shrink");
  // 过渡期间禁用元素自身的颜色过渡,保证新旧快照都是"切换完成"的稳定画面(防闪动)
  root.classList.add("theme-switching");
  runningVT?.skipTransition();
  const vt = document.startViewTransition(() => {
    // 回调内同步落盘新旧状态,保证过渡快照正确;App 的 $effect 会再写一次相同值(幂等)
    root.dataset.theme = next;
    appState.theme = next;
  });
  runningVT = vt;
  // 方向类必须在过渡结束后清理,否则会污染下一次过渡的动画方向;
  // 若已被更新的一次过渡接管,类归新过渡管理,这里不能动
  vt.finished
    .finally(() => {
      if (runningVT !== vt) return;
      root.classList.remove("vt-expand", "vt-shrink");
      root.classList.remove("theme-switching");
      runningVT = null;
    })
    .catch(() => { /* ignore */ });
}

export function toggleSidebar(): void {
  appState.collapsed = !appState.collapsed;
  try {
    localStorage.setItem("dict-sidebar", appState.collapsed ? "1" : "0");
  } catch {
    /* ignore */
  }
}

loadPrefs();
