import { appState } from "./app-state.svelte";

/** hash 路由:#/ 主页,#/t/dbo.Users 表详情,#/v/… 视图,#/p/… 存储过程。 */

export type Route =
  | { page: "home" }
  | { page: "detail"; key: string };

function parseHash(): Route {
  const hash = location.hash || "#/";
  const path = hash.startsWith("#/") ? hash.slice(2) : hash.slice(1);
  if (!path || path === "/") return { page: "home" };

  const m = path.match(/^([tTvVpP])\/(.+)$/);
  if (m) {
    const type = m[1]!.toUpperCase();
    const key = `${type}:${m[2]!}`;
    return { page: "detail", key };
  }
  return { page: "home" };
}

export const route = $state<{ value: Route }>({ value: parseHash() });

window.addEventListener("hashchange", () => {
  // 路由切换时清空全局搜索,避免搜索结果页残留
  appState.search = "";
  route.value = parseHash();
});

/** 按对象键导航到详情页,如 "T:dbo.Users" */
export function navigateTo(key: string): void {
  location.hash = "/" + key.replace(":", "/");
}