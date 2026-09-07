<script lang="ts">
  import { appState } from "./lib/app-state.svelte";
  import { route } from "./lib/router.svelte";
  import { getMeta } from "./lib/data";
  import Topbar from "./lib/components/Topbar.svelte";
  import Sidebar from "./lib/components/Sidebar.svelte";
  import Dash from "./lib/components/Dash.svelte";
  import SearchResults from "./lib/components/SearchResults.svelte";
  import Detail from "./lib/components/Detail.svelte";

  const meta = getMeta();

  let mainEl: HTMLElement;

  // 同步主题到 <html>
  $effect(() => {
    document.documentElement.dataset.theme = appState.theme;
  });

  // 视图切换(路由 / 搜索词变化)时把主区滚回顶部,避免新视图从中部开始显示
  $effect(() => {
    void route.value;
    void appState.search;
    mainEl?.scrollTo({ top: 0 });
  });

  // 路由切换时收起小屏抽屉侧栏
  $effect(() => {
    void route.value;
    appState.mobileOpen = false;
  });
</script>

<div class="app" class:collapsed={appState.collapsed} class:mobile-open={appState.mobileOpen}>
  <Topbar />
  <div class="layout">
    <Sidebar />
    <main class="main" bind:this={mainEl}>
      {#if appState.search.trim()}
        <SearchResults />
      {:else if route.value.page === "detail"}
        <Detail key={route.value.key} />
      {:else}
        <Dash />
      {/if}
    </main>
  </div>
</div>

{#if appState.mobileOpen}
  <button class="scrim" tabindex="-1" aria-label="关闭侧栏" onclick={() => (appState.mobileOpen = false)}></button>
{/if}

<div class="toast" role="status" aria-live="polite"></div>

<svelte:head>
  <title>{meta?.title ?? "数据字典"}</title>
</svelte:head>

<style>
  /* 应用骨架:顶栏 / 侧栏 / 主区。组件私有样式在各组件内。 */
  .app { display: flex; flex-direction: column; height: 100%; }
  .layout { display: flex; flex: 1; min-height: 0; overflow: hidden; }
  .main { flex: 1; overflow-y: auto; padding: 24px 28px; min-width: 0; }

  /* 小屏抽屉遮罩 */
  .scrim {
    position: fixed; inset: 0; top: 56px; z-index: 240;
    background: rgba(0, 0, 0, .45);
    border: 0; padding: 0; cursor: pointer;
  }
  @media (min-width: 901px) {
    .scrim { display: none; }
  }

  @media (max-width: 900px) {
    .main { padding: 16px; }
  }
</style>
