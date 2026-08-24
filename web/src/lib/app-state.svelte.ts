/** 全局应用状态(Svelte 5 runes)。 */

export const appState = $state({
  /** 侧栏是否折叠 */
  collapsed: false,
  /** 当前亮/暗主题 */
  theme: "light" as "light" | "dark",
  /** 顶栏全局搜索词(非空时主区显示搜索结果) */
  search: "",
  /** 各类型搜索结果展开条数 */
  searchMore: { T: 50, V: 50, P: 50 } as Record<string, number>,
  /** 侧栏名称二次筛选词 */
  nameFilter: "",
});

function loadPrefs() {
  try {
    const t = localStorage.getItem("dict-theme");
    if (t === "dark" || t === "light") appState.theme = t;
    appState.collapsed = localStorage.getItem("dict-sidebar") === "1";
  } catch {
    /* file:// 可能禁用 localStorage */
  }
}

export function toggleTheme(): void {
  appState.theme = appState.theme === "dark" ? "light" : "dark";
  try {
    localStorage.setItem("dict-theme", appState.theme);
  } catch {
    /* ignore */
  }
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