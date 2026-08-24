<script lang="ts">
  import { appState } from "../app-state.svelte";
  import { navigateTo } from "../router.svelte";
  import { searchIndex, type SearchHit } from "../search";
  import type { ObjectType } from "../types";
  import { defSnippet, esc, hl, truncate } from "../utils";
  import TypeBadge from "./ui/TypeBadge.svelte";

  const TYPE_LABEL: Record<ObjectType, string> = { T: "表", V: "视图", P: "存储过程" };
  const ORDER: ObjectType[] = ["T", "V", "P"];

  const q = $derived(appState.search.trim());

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
    <h1>搜索 “{esc(q)}” — 共 {hits.length} 条结果</h1>
  </div>

  {#each ORDER as type}
    {@const list = perType(type)}
    {#if list.length}
      <div class="group-title" style="padding-left:0">{TYPE_LABEL[type]} ({list.length})</div>
      {#each list.slice(0, appState.searchMore[type] ?? 50) as h, i (h.e.k + i)}
        <div class="result" role="link" tabindex="0" onclick={() => openObject(h)} onkeydown={(ev) => { if (ev.key === "Enter") openObject(h); }}>
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
    <div class="loading">没有匹配的结果</div>
  {/if}
</div>