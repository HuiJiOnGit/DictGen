<script lang="ts">
  import { appState } from "../app-state.svelte";
  import { navigateTo } from "../router.svelte";
  import { searchIndex, type SearchHit } from "../search";
  import { TYPE_LABEL, type ObjectType } from "../types";
  import { defSnippet, hl, truncate } from "../utils";
  import TypeBadge from "./ui/TypeBadge.svelte";

  const ORDER: ObjectType[] = ["T", "V", "P"];

  // 防抖镜像:索引含数千条过程全文,逐键同步扫描太重,停顿 150ms 再执行搜索
  let q = $state("");
  $effect(() => {
    const v = appState.search.trim();
    const t = setTimeout(() => { q = v; }, 150);
    return () => clearTimeout(t);
  });

  // 新查询时重置各类型的结果展开条数,避免上一次的"显示更多"影响下一次搜索
  $effect(() => {
    void q;
    appState.searchMore = { T: 50, V: 50, P: 50 };
  });

  const hits = $derived(q ? searchIndex(q, "all") : []);

  function perType(type: ObjectType): SearchHit[] {
    return hits.filter((h) => h.e.t === type);
  }

  function openObject(h: SearchHit) {
    navigateTo(h.e.k);
  }

  function more(type: ObjectType) {
    appState.searchMore[type] = (appState.searchMore[type] ?? 50) + 100;
  }
</script>

<div class="search-page">
  <div class="head">
    <h1>搜索 “{q}” — 共 {hits.length} 条结果</h1>
  </div>

  {#each ORDER as type (type)}
    {@const list = perType(type)}
    {#if list.length}
      <div class="group-title">{TYPE_LABEL[type]} ({list.length})</div>
      {#each list.slice(0, appState.searchMore[type] ?? 50) as h (h.e.k)}
        <div
          class="result"
          role="link"
          tabindex="0"
          onclick={() => openObject(h)}
          onkeydown={(ev) => { if (ev.key === "Enter") openObject(h); }}
        >
          <TypeBadge type={h.e.t} />
          <span class="nm">{@html hl(h.e.n, q)}</span>
          <span class="sc">{h.e.s}</span>
          <span class="nm-desc">{@html hl(h.e.d ?? "", q)}</span>
          {#if h.e.c && h.e.c.toLowerCase().includes(q.toLowerCase())}
            <span class="sc">字段/参数: {@html hl(truncate(h.e.c, 60), q)}</span>
          {/if}
          {#if h.e.def && h.e.def.toLowerCase().includes(q.toLowerCase())}
            <span class="sc def-snip">定义: {@html defSnippet(h.e.def, q)}</span>
          {/if}
        </div>
      {/each}
      {#if list.length > (appState.searchMore[type] ?? 50)}
        <div class="search-more"><button type="button" class="more-btn" onclick={() => more(type)}>显示更多 ({list.length - (appState.searchMore[type] ?? 50)})</button></div>
      {/if}
    {/if}
  {/each}

  {#if hits.length === 0}
    <div class="loading">
      <svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><circle cx="11" cy="11" r="7"/><line x1="16.5" y1="16.5" x2="21" y2="21"/><line x1="8.5" y1="11" x2="13.5" y2="11"/></svg>
      <p>没有匹配 “{q}” 的结果</p>
      <p class="tip">试试更短的关键词,或检查拼写</p>
    </div>
  {/if}
</div>

<style>
  .search-page { max-width: 1100px; margin: 0 auto; }
  .head { margin-bottom: 16px; }
  .head h1 { font-size: 20px; }

  /* mark 由 @html 高亮注入,选择器需 :global */
  .head :global(mark), .result :global(mark) {
    background: #fde68a; color: #78350f;
    border-radius: 3px; padding: 0 2px;
  }
  :global(html[data-theme="dark"]) .head :global(mark),
  :global(html[data-theme="dark"]) .result :global(mark) { background: #78350f; color: #fde68a; }

  .group-title {
    padding: 10px 0 4px; font-size: 12px; font-weight: 700;
    color: var(--muted); text-transform: uppercase; letter-spacing: .4px;
  }
  .result {
    display: flex; align-items: baseline; gap: 12px;
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-sm);
    padding: 12px 16px; margin-bottom: 8px; cursor: pointer;
    transition: border-color var(--dur-fast), box-shadow var(--dur-fast);
  }
  .result:hover { border-color: var(--accent); box-shadow: var(--shadow); }
  .result:focus-visible { border-color: var(--accent); box-shadow: var(--shadow); outline: 2px solid var(--accent); outline-offset: 1px; }
  .result .nm { font-weight: 700; font-size: 14px; min-width: 200px; }
  .result .sc { color: var(--muted); font-size: 12px; }
  .result .nm-desc { color: var(--muted); font-size: 13px; flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .result .def-snip { display: block; }

  .search-more { text-align: center; padding: 10px; color: var(--muted); font-size: 13px; }
  .more-btn {
    border: 1px solid var(--border); background: var(--surface); color: var(--accent);
    font-size: 13px; padding: 6px 16px; border-radius: 999px; cursor: pointer;
    transition: border-color var(--dur-fast), background var(--dur-fast);
  }
  .more-btn:hover { border-color: var(--accent); background: var(--accent-soft); }
  .more-btn:active { transform: scale(.96); }

  /* 空态 */
  .loading svg { color: var(--muted); opacity: .7; margin-bottom: 8px; }
  .loading .tip { font-size: 12px; opacity: .75; margin-top: 4px; }

  @media (max-width: 900px) {
    .result { flex-wrap: wrap; row-gap: 2px; }
    .result .nm { min-width: 0; flex-basis: auto; }
    .result .nm-desc { flex-basis: 100%; white-space: normal; }
  }
</style>
