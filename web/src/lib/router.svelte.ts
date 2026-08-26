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
    // 浏览器会把 hash 里的非 ASCII 字符百分号编码(location.hash 返回编码形态),
    // 必须先还原再与索引 key 比对,否则含中文的对象名永远匹配不上。
    let name = m[2]!;
    try {
      name = decodeURIComponent(name);
    } catch {
      /* 名称本身含 "%" 等非法编码序列时按原样匹配 */
    }
    return { page: "detail", key: `${type}:${name}` };
  }
  return { page: "home" };
}

export const route = $state<{ value: Route }>({ value: parseHash() });

window.addEventListener("hashchange", () => {
  // 路由切换时清空全局搜索,避免搜索结果页残留
  appState.search = "";
  route.value = parseHash();
});

/** 对象键转详情页链接。名称段统一 encodeURIComponent,与 parseHash 的解码严格互逆
 *  (直接拼原始名字时,名称里含 "%" 会被浏览器误当编码序列,round-trip 后对不上)。 */
export function hrefOf(key: string): string {
  const i = key.indexOf(":");
  return `#/${key.slice(0, i)}/${encodeURIComponent(key.slice(i + 1))}`;
}

/** 按对象键导航到详情页,如 "T:dbo.Users" */
export function navigateTo(key: string): void {
  location.hash = hrefOf(key);
}