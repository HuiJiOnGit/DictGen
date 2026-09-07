<script lang="ts">
  import { slide } from "svelte/transition";
  import { cubicOut } from "svelte/easing";
  import { appState } from "../app-state.svelte";
  import { getIndex, getMeta } from "../data";
  import { navigateTo, route } from "../router.svelte";
  import { TYPE_LABEL, type IndexEntry, type ObjectType } from "../types";
  import { copyText } from "../utils";
  import TypeBadge from "./ui/TypeBadge.svelte";

  const meta = getMeta();
  const counts = meta?.counts ?? { tables: 0, views: 0, procs: 0 };

  const TYPE_ORDER: ObjectType[] = ["T", "V", "P"];

  // 展开/收起滑动时长(尊重系统的减少动效偏好)
  const treeSlide = {
    duration: typeof matchMedia !== "undefined" && matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 190,
    easing: cubicOut,
  };

  let listEl: HTMLDivElement;
  let filterBox: HTMLInputElement;

  // 当前详情页对象键(用于侧栏选中高亮)
  const currentKey = $derived(route.value.page === "detail" ? route.value.key : "");

  // 折叠状态:schema 级 + 类型级(schema\0type),持久化
  let collapsedSchemas = $state<Set<string>>(new Set());
  let collapsedTypes = $state<Set<string>>(new Set());
  function loadCollapsed() {
    try {
      const raw = localStorage.getItem("dict-sidebar-cols");
      if (!raw) return;
      const d = JSON.parse(raw);
      if (Array.isArray(d)) {
        collapsedSchemas = new Set(d.filter((x): x is string => typeof x === "string"));
      } else {
        const obj = d as { schemas?: unknown; types?: unknown };
        collapsedSchemas = new Set(Array.isArray(obj.schemas) ? obj.schemas.filter((x): x is string => typeof x === "string") : []);
        collapsedTypes = new Set(Array.isArray(obj.types) ? obj.types.filter((x): x is string => typeof x === "string") : []);
      }
    } catch { /* ignore */ }
  }
  loadCollapsed();
  $effect(() => {
    try {
      localStorage.setItem("dict-sidebar-cols", JSON.stringify({
        schemas: [...collapsedSchemas],
        types: [...collapsedTypes],
      }));
    } catch { /* ignore */ }
  });

  // 防抖镜像:筛选词停顿 120ms 后再重建整棵树,避免逐键全量排序分组
  let appliedFilter = $state("");
  $effect(() => {
    const v = appState.nameFilter;
    const t = setTimeout(() => { appliedFilter = v; }, 120);
    return () => clearTimeout(t);
  });

  const hasFilter = $derived(appliedFilter.trim().length > 0);

  function isOpen(schema: string): boolean {
    return hasFilter || !collapsedSchemas.has(schema);
  }
  function isTypeOpen(schema: string, type: ObjectType): boolean {
    return hasFilter || !collapsedTypes.has(typeKey(schema, type));
  }
  function typeKey(schema: string, type: ObjectType): string {
    return schema + "\u0000" + type;
  }

  function toggleSchema(schema: string) {
    if (hasFilter) return;
    const next = new Set(collapsedSchemas);
    if (next.has(schema)) next.delete(schema); else next.add(schema);
    collapsedSchemas = next;
  }
  function toggleType(schema: string, type: ObjectType) {
    if (hasFilter) return;
    const key = typeKey(schema, type);
    const next = new Set(collapsedTypes);
    if (next.has(key)) next.delete(key); else next.add(key);
    collapsedTypes = next;
  }

  // 树型分组:schema → [表, 视图, 过程] → 按名称排序
  const tree = $derived.by(() => {
    const q = appliedFilter.trim().toLowerCase();
    const map = new Map<string, Map<ObjectType, IndexEntry[]>>();
    for (const e of getIndex()) {
      if (q && !e.n.toLowerCase().includes(q)) continue;
      let m = map.get(e.s);
      if (!m) map.set(e.s, (m = new Map()));
      let list = m.get(e.t);
      if (!list) m.set(e.t, (list = []));
      list.push(e);
    }
    return [...map.entries()]
      .map(([s, m]) => ({
        s,
        total: [...m.values()].reduce((n, l) => n + l.length, 0),
        types: TYPE_ORDER
          .filter((t) => m.has(t))
          .map((t) => ({
            t,
            label: TYPE_LABEL[t],
            items: m.get(t)!.sort((a, b) => a.n.localeCompare(b.n, "zh-CN")),
          })),
      }))
      .sort((a, b) => a.s.localeCompare(b.s, "zh-CN"));
  });

  // 路由切换时:仅当选中项所在分组已展开时才滚动到它(只在 key 变化时执行一次,
  // 不自动展开、不打扰用户手动折叠,展开/折叠分组不会触发滚动)
  let lastScrolledKey = "";
  $effect(() => {
    if (!currentKey || currentKey === lastScrolledKey) return;
    lastScrolledKey = currentKey;
    setTimeout(() => {
      const el = listEl?.querySelector(
        '.side-item[data-key="' + CSS.escape(currentKey) + '"]',
      ) as HTMLElement | null;
      if (el) el.scrollIntoView({ block: "center", behavior: "smooth" });
    }, 50);
  });

  function onFilterKeydown(e: KeyboardEvent) {
    if (e.key === "Escape") {
      appState.nameFilter = "";
      filterBox.blur();
    }
  }

  function onItemClick(e: IndexEntry) {
    navigateTo(e.k);
  }

  // ---------- 全名 Tooltip(仅名称溢出时出现,带复制按钮) ----------

  const tooltip = $state<{ x: number; y: number; text: string; show: boolean }>({ x: 0, y: 0, text: "", show: false });
  let hideTimer: ReturnType<typeof setTimeout> | undefined;

  const TT_MAX_W = 440;
  const TT_MARGIN = 12;

  function maybeShowTooltip(ev: MouseEvent | FocusEvent, entry: IndexEntry) {
    const el = ev.currentTarget as HTMLElement;
    const nm = el.querySelector<HTMLElement>(".nm");
    if (!nm || nm.scrollWidth <= nm.clientWidth) { hideTooltip(); return; }
    const r = el.getBoundingClientRect();
    clearTimeout(hideTimer);
    // 视口钳制:避免右侧溢出或上下边缘裁剪
    tooltip.x = Math.min(r.right + 10, window.innerWidth - TT_MAX_W - TT_MARGIN);
    tooltip.y = Math.min(Math.max(r.top + r.height / 2, 90), window.innerHeight - 90);
    tooltip.text = entry.s + "." + entry.n;
    tooltip.show = true;
  }

  function hideTooltip() {
    clearTimeout(hideTimer);
    hideTimer = setTimeout(() => { tooltip.show = false; }, 200);
  }

  function keepTooltip() {
    clearTimeout(hideTimer);
  }

  function onListScroll() {
    tooltip.show = false;
  }
</script>

<aside class="sidebar">
  <div class="filter-wrap">
    <span class="icon" aria-hidden="true">
      <svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="6" cy="6" r="4.5"/><line x1="10" y1="10" x2="13" y2="13"/></svg>
    </span>
    <input
      bind:this={filterBox}
      bind:value={appState.nameFilter}
      type="search"
      aria-label="按名称二次筛选"
      placeholder="输入名称二次筛选…"
      autocomplete="off"
      onkeydown={onFilterKeydown}
    >
  </div>

  <div class="sidelist-wrap">
    <div class="sidelist" bind:this={listEl} onscroll={onListScroll}>
      {#if tree.length === 0}
        <div class="empty">
          {appliedFilter.trim()
            ? "没有匹配 “" + appliedFilter.trim() + "” 的对象"
            : "暂无对象"}
        </div>
      {:else}
        {#each tree as sch (sch.s)}
          <div data-schema={sch.s}>
            <div
              class="side-schema-h"
              class:collapsed={!isOpen(sch.s)}
              role="button"
              tabindex="0"
              aria-expanded={isOpen(sch.s)}
              title={sch.s}
              onclick={() => toggleSchema(sch.s)}
              onkeydown={(ev) => { if (ev.key === "Enter" || ev.key === " ") { ev.preventDefault(); toggleSchema(sch.s); } }}
            >
              <span class="chev" aria-hidden="true">
                <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg>
              </span>
              <span class="sc-nm">{sch.s}</span>
              <span class="sc-cnt">{sch.total}</span>
            </div>
            {#if isOpen(sch.s)}
              <div class="schema-body" transition:slide={treeSlide}>
                {#each sch.types as g (g.t)}
                  <div class="side-type">
                    <div
                      class="side-type-h"
                      class:collapsed={!isTypeOpen(sch.s, g.t)}
                      role="button"
                      tabindex="0"
                      aria-expanded={isTypeOpen(sch.s, g.t)}
                      onclick={() => toggleType(sch.s, g.t)}
                      onkeydown={(ev) => { if (ev.key === "Enter" || ev.key === " ") { ev.preventDefault(); toggleType(sch.s, g.t); } }}
                    >
                      <span class="ty-chev" aria-hidden="true">
                        <svg width="8" height="8" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg>
                      </span>
                      <span class="ty-nm">{g.label}</span>
                      <span class="ty-cnt">{g.items.length}</span>
                    </div>
                    {#if isTypeOpen(sch.s, g.t)}
                      <div class="type-items" transition:slide={treeSlide}>
                        {#each g.items as e (e.k)}
                          <div
                            class="side-item"
                            class:active={currentKey === e.k}
                            data-key={e.k}
                            role="link"
                            tabindex="0"
                            aria-current={currentKey === e.k ? "true" : undefined}
                            onclick={() => onItemClick(e)}
                            onkeydown={(ev) => { if (ev.key === "Enter") onItemClick(e); }}
                            onmouseenter={(ev) => maybeShowTooltip(ev, e)}
                            onmouseleave={hideTooltip}
                            onfocus={(ev) => maybeShowTooltip(ev, e)}
                            onblur={hideTooltip}
                          >
                            <span class="side-item-row1">
                              <TypeBadge type={e.t} />
                              <span class="nm">{e.n}</span>
                            </span>
                            {#if e.d}
                              <span class="desc" title={e.d}>{e.d}</span>
                            {/if}
                          </div>
                        {/each}
                      </div>
                    {/if}
                  </div>
                {/each}
              </div>
            {/if}
          </div>
        {/each}
      {/if}
    </div>
  </div>

  <div class="side-stats">
    {counts.tables} 表 · {counts.views} 视图 · {counts.procs} 过程
  </div>
</aside>

{#if tooltip.show}
  <div
    class="tt"
    role="tooltip"
    style="left:{tooltip.x}px; top:{tooltip.y}px"
    onmouseenter={keepTooltip}
    onmouseleave={hideTooltip}
  >
    <span class="tt-nm">{tooltip.text}</span>
    <button class="tt-copy" onclick={() => copyText(tooltip.text)}>复制</button>
  </div>
{/if}

<style>
  .sidebar {
    width: 320px; flex: none; display: flex; flex-direction: column;
    background: var(--surface); border-right: 1px solid var(--border);
    min-height: 0;
    transition: margin-left var(--dur) ease;
  }
  :global(.app.collapsed) .sidebar { margin-left: -320px; }

  /* 名称二次筛选 */
  .filter-wrap { padding: 10px 12px; border-bottom: 1px solid var(--border); position: relative; }
  .filter-wrap .icon { position: absolute; left: 22px; top: 50%; transform: translateY(-50%); color: var(--muted); display: grid; }
  .filter-wrap input {
    width: 100%; padding: 7px 12px 7px 32px;
    border: 1px solid var(--border); border-radius: 8px;
    background: var(--surface-2); color: var(--text);
    font-size: 13px; outline: none;
    transition: border-color var(--dur-fast), box-shadow var(--dur-fast);
  }
  .filter-wrap input::-webkit-search-cancel-button { -webkit-appearance: none; }
  .filter-wrap input:focus { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }

  /* 树型分组: schema 分组头粘顶, 类型分组头粘其下 */
  .side-schema-h {
    position: sticky; top: 0; z-index: 3;
    height: 34px; display: flex; align-items: center; gap: 6px;
    padding: 0 10px 0 14px; cursor: pointer; user-select: none;
    background: var(--surface); border-bottom: 1px solid var(--border);
    transition: background var(--dur-fast);
  }
  .side-schema-h:hover { background: var(--surface-2); }
  .side-schema-h .chev {
    flex: none; width: 14px; color: var(--muted);
    display: grid; place-items: center;
    transition: transform var(--dur-fast);
  }
  .side-schema-h.collapsed .chev { transform: rotate(-90deg); }
  .side-schema-h .sc-nm {
    flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    font-size: 13px; font-weight: 700; letter-spacing: .3px;
  }
  .side-schema-h .sc-cnt {
    flex: none; font-size: 11px; color: var(--muted);
    background: var(--badge-bg); border-radius: 8px; padding: 0 7px;
  }
  .side-type-h {
    position: sticky; top: 34px; z-index: 2;
    height: 27px; display: flex; align-items: center; gap: 6px;
    padding: 0 10px 0 28px; cursor: pointer; user-select: none;
    background: var(--surface-2); border-bottom: 1px solid var(--border);
    color: var(--muted); transition: background var(--dur-fast);
  }
  .side-type-h:hover { background: var(--surface); }
  .side-type-h .ty-chev {
    flex: none; width: 10px; color: var(--muted);
    display: grid; place-items: center;
    transition: transform var(--dur-fast);
  }
  .side-type-h.collapsed .ty-chev { transform: rotate(-90deg); }
  .side-type-h .ty-nm { flex: 1; font-size: 12px; font-weight: 700; letter-spacing: .4px; }
  .side-type-h .ty-cnt { flex: none; font-size: 11px; color: var(--muted); }
  .side-type .side-item { padding-left: 34px; }

  /* 对象列表 */
  .sidelist-wrap { flex: 1; min-height: 0; display: flex; position: relative; }
  .sidelist { flex: 1; overflow-y: auto; padding: 0 0 8px; position: relative; scroll-behavior: smooth; }
  .sidelist .empty { padding: 30px 16px; text-align: center; color: var(--muted); font-size: 13px; }

  .side-item {
    padding: 4px 10px 4px 14px; cursor: pointer;
    transition: background var(--dur-fast); border-left: 3px solid transparent;
  }
  .side-item:hover { background: var(--surface-2); }
  .side-item:focus-visible { background: var(--surface-2); outline: 2px solid var(--accent); outline-offset: -2px; }
  .side-item.active { background: var(--accent-soft); border-left-color: var(--accent); }
  .side-item.active .nm { color: var(--accent-text); font-weight: 600; }
  .side-item-row1 { display: flex; align-items: center; gap: 6px; min-width: 0; }
  .side-item .nm { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; flex: 1; min-width: 0; }
  .side-item .desc {
    display: block; font-size: 11px; color: var(--muted);
    overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    margin-top: 1px;
  }

  .side-stats {
    padding: 10px 14px; border-top: 1px solid var(--border);
    font-size: 12px; color: var(--muted); display: flex; gap: 14px;
  }

  /* 全名 Tooltip */
  .tt {
    position: fixed; z-index: 300; transform: translateY(-50%);
    display: flex; align-items: center; gap: 10px;
    max-width: 440px; padding: 8px 8px 8px 13px;
    background: var(--text); color: var(--bg); border-radius: 8px;
    box-shadow: var(--shadow-pop);
    font-size: 12px;
  }
  :global(html[data-theme="dark"]) .tt { background: var(--surface-2); color: var(--text); border: 1px solid var(--border); }
  .tt .tt-nm { word-break: break-all; line-height: 1.45; max-height: 130px; overflow-y: auto; }
  .tt .tt-copy {
    flex: none; border: 1px solid rgba(255, 255, 255, .28); background: transparent; color: inherit;
    font-size: 11px; padding: 3px 11px; border-radius: 6px; cursor: pointer;
    transition: background var(--dur-fast);
  }
  :global(html[data-theme="dark"]) .tt .tt-copy { border-color: var(--border); }
  .tt .tt-copy:hover { background: rgba(255, 255, 255, .14); }
  :global(html[data-theme="dark"]) .tt .tt-copy:hover { background: var(--badge-bg); }

  @media (max-width: 900px) {
    /* 小屏:抽屉式侧栏(汉堡开合 + 遮罩),不再是隐藏不可达 */
    .sidebar {
      position: fixed; top: 56px; bottom: 0; left: 0; z-index: 250;
      width: min(320px, 84vw);
      transform: translateX(-105%);
      transition: transform var(--dur) ease;
      box-shadow: var(--shadow-pop);
    }
    :global(.app.collapsed) .sidebar { margin-left: 0; }
    :global(.app.mobile-open) .sidebar { transform: none; }
  }
</style>
