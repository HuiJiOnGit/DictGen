<script lang="ts">
  import { appState } from "../app-state.svelte";
  import { getIndex, getMeta } from "../data";
  import { navigateTo, route } from "../router.svelte";
  import type { IndexEntry, ObjectType } from "../types";
  import { copyText } from "../utils";
  import TypeBadge from "./ui/TypeBadge.svelte";

  const meta = getMeta();
  const counts = meta?.counts ?? { tables: 0, views: 0, procs: 0 };

  const TYPE_LABEL: Record<ObjectType, string> = { T: "表", V: "视图", P: "过程" };
  const TYPE_ORDER: ObjectType[] = ["T", "V", "P"];

  let listEl: HTMLDivElement;
  let filterBox: HTMLInputElement;

  // 当前详情页对象键(用于侧栏选中高亮)
  const currentKey = $derived(route.value.page === "detail" ? route.value.key : "");

  // 折叠状态:schema 级 + 类型级(schema\u0000type),持久化
  let collapsedSchemas = $state<Set<string>>(new Set());
  let collapsedTypes = $state<Set<string>>(new Set());
  function loadCollapsed() {
    try {
      const raw = localStorage.getItem("dict-sidebar-cols");
      if (!raw) return;
      const d = JSON.parse(raw);
      if (Array.isArray(d)) {
        collapsedSchemas = new Set(d as string[]);
      } else {
        collapsedSchemas = new Set((d as { schemas?: string[] }).schemas ?? []);
        collapsedTypes = new Set((d as { types?: string[] }).types ?? []);
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

  const hasFilter = $derived(appState.nameFilter.trim().length > 0);

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
    const q = appState.nameFilter.trim().toLowerCase();
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
        `.side-item[data-key="${CSS.escape(currentKey)}"]`,
      ) as HTMLElement | null;
      if (el) el.scrollIntoView({ block: "center", behavior: "smooth" });
    }, 50);
  });

  function onFilterInput(e: Event) {
    appState.nameFilter = (e.target as HTMLInputElement).value;
  }

  function onFilterKeydown(e: KeyboardEvent) {
    if (e.key === "Escape") {
      appState.nameFilter = "";
      filterBox.value = "";
      filterBox.blur();
    }
  }

  function onItemClick(e: IndexEntry) {
    navigateTo(e.k);
  }

  // ---------- 全名 Tooltip(仅名称溢出时出现,带复制按钮) ----------

  const tooltip = $state<{ x: number; y: number; text: string; show: boolean }>({ x: 0, y: 0, text: "", show: false });
  let hideTimer: ReturnType<typeof setTimeout> | undefined;

  function maybeShowTooltip(ev: MouseEvent | FocusEvent, entry: IndexEntry) {
    const el = ev.currentTarget as HTMLElement;
    const nm = el.querySelector<HTMLElement>(".nm");
    if (!nm || nm.scrollWidth <= nm.clientWidth) { hideTooltip(); return; }
    const r = el.getBoundingClientRect();
    clearTimeout(hideTimer);
    tooltip.x = r.right + 10;
    tooltip.y = r.top + r.height / 2;
    tooltip.text = `${entry.s}.${entry.n}`;
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
    <span class="icon">
      <svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="6" cy="6" r="4.5"/><line x1="10" y1="10" x2="13" y2="13"/></svg>
    </span>
    <input
      bind:this={filterBox}
      type="text"
      class="filter-input"
      placeholder="输入名称二次筛选…"
      autocomplete="off"
      value={appState.nameFilter}
      oninput={onFilterInput}
      onkeydown={onFilterKeydown}
    >
  </div>

  <div class="sidelist-wrap">
    <div class="sidelist" bind:this={listEl} onscroll={onListScroll}>
      {#if tree.length === 0}
        <div class="empty">
          {appState.nameFilter.trim()
            ? `没有匹配 “${appState.nameFilter.trim()}” 的对象`
            : "暂无对象"}
        </div>
      {:else}
        {#each tree as sch (sch.s)}
          <div class="side-schema" data-schema={sch.s}>
            <div
              class="side-schema-h"
              class:collapsed={!isOpen(sch.s)}
              role="button"
              tabindex="0"
              title={sch.s}
              onclick={() => toggleSchema(sch.s)}
              onkeydown={(ev) => { if (ev.key === "Enter" || ev.key === " ") { ev.preventDefault(); toggleSchema(sch.s); } }}
            >
              <span class="chev">{isOpen(sch.s) ? "▾" : "▸"}</span>
              <span class="sc-nm">{sch.s}</span>
              <span class="sc-cnt">{sch.total}</span>
            </div>
            {#if isOpen(sch.s)}
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
                    <span class="ty-chev">{isTypeOpen(sch.s, g.t) ? "▾" : "▸"}</span>
                    <span class="ty-nm">{g.label}</span>
                    <span class="ty-cnt">{g.items.length}</span>
                  </div>
                  {#if isTypeOpen(sch.s, g.t)}
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
                  {/if}
                </div>
              {/each}
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
