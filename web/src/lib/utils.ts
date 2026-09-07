/** 通用工具:转义、高亮、提示等。 */

function esc(s: unknown): string {
  return String(s ?? "").replace(/[&<>"']/g, (c) => (
    { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" } as Record<string, string>
  )[c]!);
}

/** 高亮关键字,返回 HTML(词已转义) */
export function hl(text: unknown, q: string): string {
  if (!q) return esc(text);
  const s = String(text ?? "");
  const lower = s.toLowerCase();
  const needle = q.toLowerCase();
  if (!lower.includes(needle)) return esc(s);

  let out = "";
  let i = 0;
  let idx = lower.indexOf(needle);
  while (idx >= 0) {
    out += esc(s.slice(i, idx)) + `<mark>${esc(s.slice(idx, idx + q.length))}</mark>`;
    i = idx + q.length;
    idx = lower.indexOf(needle, i);
  }
  return out + esc(s.slice(i));
}

export function truncate(s: string, n: number): string {
  return s.length > n ? s.slice(0, n) + "…" : s;
}

/** 千分位格式化数字 */
export function fmtNum(n: number | null | undefined): string {
  return n == null ? "-" : n.toLocaleString("zh-CN");
}

/** 轻提示(App 内有静态 .toast 元素,这里仅兜底创建) */
export function toast(msg: string): void {
  let el = document.querySelector<HTMLDivElement>(".toast");
  if (!el) {
    el = document.createElement("div");
    el.className = "toast";
    el.setAttribute("role", "status");
    el.setAttribute("aria-live", "polite");
    document.body.appendChild(el);
  }
  el.textContent = msg;
  el.classList.add("show");
  const owner = el as HTMLDivElement & { _t?: number };
  clearTimeout(owner._t);
  owner._t = window.setTimeout(() => el.classList.remove("show"), 1800);
}

/** 复制文本(clipboard API 失败时回退 execCommand,覆盖 file:// 场景) */
export async function copyText(text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
    toast("已复制");
  } catch {
    const ta = document.createElement("textarea");
    ta.value = text;
    document.body.appendChild(ta);
    ta.select();
    document.execCommand("copy");
    ta.remove();
    toast("已复制");
  }
}

/** 取 SQL 定义中命中词附近的片段,便于搜索结果预览 */
export function defSnippet(def: string, q: string, pad = 40): string {
  const i = def.toLowerCase().indexOf(q.toLowerCase());
  if (i < 0) return truncate(def, pad * 3);
  const start = Math.max(0, i - pad);
  const end = Math.min(def.length, i + q.length + pad);
  return (start > 0 ? "…" : "") + hl(def.slice(start, end), q) + (end < def.length ? "…" : "");
}
