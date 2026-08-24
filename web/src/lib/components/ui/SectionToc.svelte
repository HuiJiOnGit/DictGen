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
    scroller?.addEventListener("scroll", updateActive, { passive: true });
    updateActive();
    return () => scroller?.removeEventListener("scroll", updateActive);
  });
</script>

<aside class="toc">
  <div class="toc-title">本章目录</div>
  <nav class="toc-nav">
    {#each sections as s}
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
