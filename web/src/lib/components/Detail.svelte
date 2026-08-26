<script lang="ts">
  import { loadObject, getIndex } from "../data";
  import { hrefOf } from "../router.svelte";
  import type { DictObject, IndexEntry, ObjectType } from "../types";
  import { copyText, esc } from "../utils";
  import TypeBadge from "./ui/TypeBadge.svelte";
  import TableDetail from "./TableDetail.svelte";
  import ViewDetail from "./ViewDetail.svelte";
  import ProcDetail from "./ProcDetail.svelte";
  import SectionToc from "./ui/SectionToc.svelte";

  let { key }: { key: string } = $props();

  const TYPE_LABEL: Record<ObjectType, string> = { T: "表", V: "视图", P: "过程" };

  const state = $state<{ obj: DictObject | null; error: string | null; notFound: boolean }>({ obj: null, error: null, notFound: false });

  $effect(() => {
    state.obj = null;
    state.error = null;
    state.notFound = false;
    loadObject(key)
      .then((o) => {
        // null = 索引里没有该 key:明确提示"未找到",而不是永远停在加载态
        if (o === null) state.notFound = true;
        else state.obj = o;
      })
      .catch((e) => { state.error = e instanceof Error ? e.message : String(e); });
  });

  // 导航顺序与左侧目录树一致:schema → [表, 视图, 过程] → 按名称排序
  const allEntries = $derived(
    [...getIndex()].sort((a, b) => {
      const order: Record<string, number> = { T: 0, V: 1, P: 2 };
      return a.s.localeCompare(b.s, "zh-CN")
        || (order[a.t] ?? 9) - (order[b.t] ?? 9)
        || a.n.localeCompare(b.n, "zh-CN");
    }),
  );

  // 右侧章节目录:与各详情组件的 section id 对应
  const sections = $derived.by(() => {
    const o = state.obj;
    if (!o) return [];
    if (o.t === "T") {
      return [
        { id: "sec-cols", label: "字段", count: o.cols?.length ?? 0 },
        { id: "sec-ix", label: "索引", count: o.ix?.length ?? 0 },
        { id: "sec-fk", label: "外键", count: o.fk?.length ?? 0 },
        { id: "sec-ddl", label: "CREATE 脚本" },
      ];
    }
    if (o.t === "V") {
      return [
        { id: "sec-cols", label: "字段", count: o.cols?.length ?? 0 },
        ...(o.def ? [{ id: "sec-def", label: "视图定义" }] : []),
      ];
    }
    return [
      { id: "sec-prm", label: "参数", count: o.prm?.length ?? 0 },
      ...(o.def ? [{ id: "sec-def", label: "过程定义" }] : []),
    ];
  });

  function neighbor(delta: -1 | 1): IndexEntry | null {
    const obj = state.obj;
    if (!obj) return null;
    const idx = allEntries.findIndex((e) => e.k === key);
    return allEntries[idx + delta] ?? null;
  }

  function goHome() {
    location.hash = "#/";
  }

  function copyName() {
    const o = state.obj;
    if (o) copyText(`${o.s}.${o.n}`);
  }
</script>

{#if state.error}
  <div class="loading">加载失败: {esc(state.error)}</div>
{:else if state.notFound}
  <div class="loading">
    <p>未找到对象:<b class="mono">{esc(key)}</b></p>
    <p>它可能已被删除、重命名,或链接不完整。</p>
    <p><a href="#/">← 返回主页</a></p>
  </div>
{:else if state.obj === null}
  <div class="loading"><div class="spinner"></div>加载中…</div>
{:else}
  {@const o = state.obj!}
  <div class="detail-page">
    <div class="detail">
      <!-- 粘性标题:面包屑 + 返回按钮 + 类型 + 名称 + 复制 -->
      <div class="detail-head">
        <nav class="crumb" aria-label="面包屑">
          <span>数据字典</span>
          <span class="sep">›</span>
          <span>{o.s}</span>
          <span class="sep">›</span>
          <span>{TYPE_LABEL[o.t]}</span>
          <span class="sep">›</span>
          <span class="cur">{o.n}</span>
        </nav>
        <div class="head-main">
          <button class="back-btn" onclick={goHome} title="返回主页" aria-label="返回主页">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 8H3"/><path d="M8 3L3 8l5 5"/></svg>
          </button>
          <h1>
            <TypeBadge type={o.t} />
            <span class="nm">{o.n}</span>
            <button class="copy-name" onclick={copyName} title="复制完整名称" aria-label="复制完整名称">
              <svg width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="5" y="5" width="8" height="8" rx="1.5"/><path d="M10 5V3.5A1.5 1.5 0 0 0 8.5 2h-5A1.5 1.5 0 0 0 2 3.5v5A1.5 1.5 0 0 0 3.5 10H5"/></svg>
            </button>
          </h1>
        </div>
      </div>

      {#if o.d}<div class="desc-box">{o.d}</div>{/if}

      {#if o.t === "T"}
        <TableDetail obj={o} />
      {:else if o.t === "V"}
        <ViewDetail obj={o} />
      {:else}
        <ProcDetail obj={o} />
      {/if}
    </div>

    {#if sections.length}
      <SectionToc {sections} />
    {/if}
  </div>

  <!-- 固定底部导航:左上 上一个 / 右上 下一个 -->
  <nav class="fixed-nav">
    {#if neighbor(-1)}
      <a href={hrefOf(neighbor(-1)!.k)} class="nav-pill prev" title={neighbor(-1)!.n}>‹ 上一个</a>
    {:else}
      <span class="nav-pill prev disabled">‹ 上一个</span>
    {/if}
    {#if neighbor(1)}
      <a href={hrefOf(neighbor(1)!.k)} class="nav-pill next" title={neighbor(1)!.n}>下一个 ›</a>
    {:else}
      <span class="nav-pill next disabled">下一个 ›</span>
    {/if}
  </nav>
{/if}
