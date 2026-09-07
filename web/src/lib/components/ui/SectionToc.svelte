<script lang="ts">
  import { onMount } from "svelte";

  export interface TocSection {
    id: string;
    label: string;
    count?: number;
  }

  let { sections }: { sections: TocSection[] } = $props();

  let activeId = $state("");
  let scroller: HTMLElement | null = null;

  function updateActive() {
    if (!scroller || sections.length === 0) return;
    const scRect = scroller.getBoundingClientRect();
    const line = 90; // 与 scroll-margin-top 对齐(含亚像素容差)
    let current = "";
    for (const s of sections) {
      const el = document.getElementById(s.id);
      if (!el) continue;
      if (el.getBoundingClientRect().top - scRect.top <= line) current = s.id;
      else break;
    }
    activeId = current;
  }

  function jump(id: string) {
    activeId = id; // 点击即标记,滚动后由 spy 接管
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  onMount(() => {
    scroller = document.querySelector<HTMLElement>(".main");
    // rAF 节流:scroll 事件高频触发,避免每帧多次 getBoundingClientRect
    let ticking = false;
    const onScroll = () => {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(() => { ticking = false; updateActive(); });
    };
    scroller?.addEventListener("scroll", onScroll, { passive: true });
    updateActive();
    return () => scroller?.removeEventListener("scroll", onScroll);
  });
</script>

<aside class="toc">
  <div class="toc-title">本章目录</div>
  <nav class="toc-nav" aria-label="章节目录">
    {#each sections as s (s.id)}
      <a
        class="toc-item"
        class:active={activeId === s.id}
        href="#{s.id}"
        title={s.label}
        onclick={(ev) => { ev.preventDefault(); jump(s.id); }}
      >
        <span class="toc-nm">{s.label}</span>
        {#if s.count != null}<span class="toc-cnt">{s.count}</span>{/if}
      </a>
    {/each}
  </nav>
</aside>

<style>
  .toc {
    position: sticky; top: 120px; flex: none;
    width: 200px; max-height: calc(100vh - 160px); overflow-y: auto;
    padding: 12px 14px; border: 1px solid var(--border); border-radius: var(--radius);
    background: var(--surface); box-shadow: var(--shadow);
  }
  .toc .toc-title {
    font-size: 12px; font-weight: 700; color: var(--muted);
    letter-spacing: .4px; margin-bottom: 8px; text-transform: uppercase;
  }
  .toc .toc-nav { display: flex; flex-direction: column; gap: 2px; }
  .toc .toc-item {
    display: flex; align-items: center; gap: 8px;
    padding: 5px 8px; border-radius: 6px; font-size: 13px; color: var(--muted);
    border-left: 2px solid transparent; text-decoration: none;
  }
  .toc .toc-item:hover { background: var(--surface-2); color: var(--text); text-decoration: none; }
  .toc .toc-item.active { color: var(--accent-text); background: var(--accent-soft); border-left-color: var(--accent); font-weight: 600; }
  .toc .toc-nm { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .toc .toc-cnt { flex: none; font-size: 11px; color: var(--muted); background: var(--badge-bg); border-radius: 8px; padding: 0 6px; }
  .toc .toc-item.active .toc-cnt { background: var(--accent-soft); }

  @media (max-width: 1439px) {
    /* 屏幕不够宽时隐藏右侧目录,内容区回到全宽 */
    .toc { display: none; }
  }
</style>
