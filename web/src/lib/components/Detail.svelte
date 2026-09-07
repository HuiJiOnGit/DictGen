<script lang="ts">
  import { loadObject, getIndex } from "../data";
  import { hrefOf } from "../router.svelte";
  import { TYPE_LABEL, type DictObject } from "../types";
  import { copyText } from "../utils";
  import TypeBadge from "./ui/TypeBadge.svelte";
  import TableDetail from "./TableDetail.svelte";
  import ViewDetail from "./ViewDetail.svelte";
  import ProcDetail from "./ProcDetail.svelte";
  import SectionToc from "./ui/SectionToc.svelte";

  let { key }: { key: string } = $props();

  const state = $state<{ obj: DictObject | null; error: string | null; notFound: boolean }>({ obj: null, error: null, notFound: false });

  $effect(() => {
    const k = key;
    state.obj = null;
    state.error = null;
    state.notFound = false;
    loadObject(k)
      .then((o) => {
        if (key !== k) return; // 竞态保护:等待期间已切换到其他对象
        // null = 索引里没有该 key:明确提示"未找到",而不是永远停在加载态
        if (o === null) state.notFound = true;
        else state.obj = o;
      })
      .catch((e) => {
        if (key !== k) return;
        state.error = e instanceof Error ? e.message : String(e);
      });
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

  // 上一个 / 下一个:每次导航只计算一次(避免模板中重复 O(n) 扫描)
  const currentIndex = $derived(allEntries.findIndex((e) => e.k === key));
  const prevEntry = $derived(currentIndex > 0 ? allEntries[currentIndex - 1]! : null);
  const nextEntry = $derived(currentIndex >= 0 && currentIndex < allEntries.length - 1 ? allEntries[currentIndex + 1]! : null);

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

  function goHome() {
    location.hash = "#/";
  }

  function copyName() {
    const o = state.obj;
    if (o) copyText(o.s + "." + o.n);
  }
</script>

{#if state.error}
  <div class="loading">加载失败: {state.error}</div>
{:else if state.notFound}
  <div class="loading">
    <p>未找到对象:<b class="mono">{key}</b></p>
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
          <span class="sep" aria-hidden="true">›</span>
          <span>{o.s}</span>
          <span class="sep" aria-hidden="true">›</span>
          <span>{TYPE_LABEL[o.t]}</span>
          <span class="sep" aria-hidden="true">›</span>
          <span class="cur">{o.n}</span>
        </nav>
        <div class="head-main">
          <button class="back-btn" onclick={goHome} title="返回主页" aria-label="返回主页">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 8H3"/><path d="M8 3L3 8l5 5"/></svg>
          </button>
          <h1>
            <TypeBadge type={o.t} />
            <span class="nm">{o.n}</span>
            <button class="copy-name" onclick={copyName} title="复制完整名称" aria-label="复制完整名称">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="5" y="5" width="8" height="8" rx="1.5"/><path d="M10 5V3.5A1.5 1.5 0 0 0 8.5 2h-5A1.5 1.5 0 0 0 2 3.5v5A1.5 1.5 0 0 0 3.5 10H5"/></svg>
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

  <!-- 固定底部导航:左下 上一个 / 右下 下一个 -->
  <nav aria-label="上一篇 / 下一篇">
    {#if prevEntry}
      <a href={hrefOf(prevEntry.k)} class="nav-pill prev" title={prevEntry.n}>‹ 上一个</a>
    {:else}
      <span class="nav-pill prev disabled" aria-hidden="true">‹ 上一个</span>
    {/if}
    {#if nextEntry}
      <a href={hrefOf(nextEntry.k)} class="nav-pill next" title={nextEntry.n}>下一个 ›</a>
    {:else}
      <span class="nav-pill next disabled" aria-hidden="true">下一个 ›</span>
    {/if}
  </nav>
{/if}

<style>
  /* 外容器:内容列与右侧目录整体居中 */
  .detail-page {
    display: flex; justify-content: center; align-items: flex-start;
    gap: 24px;
  }
  .detail { flex: 0 1 1180px; min-width: 0; padding-bottom: 84px; }

  /* 粘性标题:面包屑 + 返回按钮 + 类型 + 名称 + 复制 */
  .detail-head {
    position: sticky; top: -24px; z-index: 20;
    margin: -24px -28px 16px; padding: 10px 28px 10px;
    background: var(--bg); border-bottom: 1px solid var(--border);
  }
  .crumb {
    display: flex; align-items: center; gap: 6px;
    font-size: 12px; color: var(--muted); flex-wrap: wrap;
    margin-bottom: 8px;
  }
  .crumb .sep { opacity: .55; }
  .crumb .cur {
    color: var(--text); font-weight: 600;
    max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }
  .head-main { display: flex; align-items: center; gap: 12px; }
  .head-main h1 {
    font-size: 20px; font-weight: 800; word-break: break-all;
    display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
  }
  .head-main h1 .nm { min-width: 0; }
  .back-btn {
    flex: none; display: grid; place-items: center;
    width: 34px; height: 34px; border-radius: 50%;
    border: 1px solid var(--border); background: var(--surface);
    color: var(--text); cursor: pointer;
    transition: color var(--dur-fast), border-color var(--dur-fast), background var(--dur-fast);
  }
  .back-btn:hover { color: var(--accent); border-color: var(--accent); background: var(--accent-soft); }
  .back-btn:active { transform: scale(.94); }
  .copy-name {
    flex: none; display: inline-grid; place-items: center;
    width: 24px; height: 24px; border-radius: 6px;
    border: 1px solid var(--border); background: var(--surface);
    color: var(--muted); cursor: pointer;
    transition: color var(--dur-fast), border-color var(--dur-fast);
  }
  .copy-name:hover { color: var(--accent); border-color: var(--accent); }
  .copy-name:active { transform: scale(.92); }

  .desc-box {
    background: var(--accent-soft); color: var(--accent-text);
    border-radius: var(--radius-sm); padding: 12px 16px; font-size: 14px;
    margin-bottom: 16px;
  }

  /* 固定底部 上一个/下一个 */
  .nav-pill {
    position: fixed; bottom: 22px; z-index: 50;
    padding: 9px 22px; border-radius: 999px;
    border: 1px solid var(--border); background: var(--surface);
    color: var(--accent-text); font-size: 13px; font-weight: 600;
    box-shadow: var(--shadow-pop); text-decoration: none;
    transition: border-color var(--dur-fast), background var(--dur-fast);
  }
  .nav-pill:hover { border-color: var(--accent); background: var(--accent-soft); text-decoration: none; }
  .nav-pill:active { transform: scale(.96); }
  .nav-pill.prev { left: 338px; }
  :global(.app.collapsed) .nav-pill.prev { left: 16px; }
  .nav-pill.next { right: 16px; }
  .nav-pill.disabled { color: var(--muted); opacity: .45; cursor: default; }
  .nav-pill.disabled:hover { border-color: var(--border); background: var(--surface); }

  @media (max-width: 900px) {
    .detail-head { margin: -16px -16px 10px; padding: 8px 16px 8px; top: -16px; }
    .nav-pill.prev { left: 16px; }
  }
</style>
