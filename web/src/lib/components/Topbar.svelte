<script lang="ts">
  import { onMount } from "svelte";
  import { appState, toggleSidebar, toggleTheme } from "../app-state.svelte";
  import { getMeta } from "../data";

  const meta = getMeta();
  const title = meta?.title ?? "数据字典";

  // 快捷键提示按平台显示(Mac 实际支持 ⌘K)
  const isApple = /Mac|iPhone|iPad/i.test(navigator.platform || navigator.userAgent);
  const kbdHint = isApple ? "⌘K" : "Ctrl+K";

  let innerW = $state(0);
  const isMobile = $derived(innerW > 0 && innerW <= 900);

  let searchBox: HTMLInputElement;

  /** 桌面端折叠侧栏,小屏开合抽屉 */
  function onToggleNav() {
    if (isMobile) appState.mobileOpen = !appState.mobileOpen;
    else toggleSidebar();
  }

  const navHidden = $derived(isMobile ? !appState.mobileOpen : appState.collapsed);

  function onSearchKeydown(e: KeyboardEvent) {
    if (e.key === "Escape") {
      appState.search = "";
      searchBox.blur();
    }
  }

  // 全局 Ctrl/⌘+K 聚焦搜索框
  onMount(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        searchBox.focus();
        searchBox.select();
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  });
</script>

<svelte:window bind:innerWidth={innerW} />

<header class="topbar">
  <div class="topbar-left">
    <button class="iconbtn" onclick={onToggleNav} title={navHidden ? "打开侧栏" : "收起侧栏"} aria-label={navHidden ? "打开侧栏" : "收起侧栏"} aria-expanded={!navHidden}>
      {#if navHidden}
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><line x1="4" y1="6" x2="20" y2="6"/><line x1="4" y1="12" x2="20" y2="12"/><line x1="4" y1="18" x2="20" y2="18"/></svg>
      {:else}
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><line x1="6" y1="6" x2="18" y2="18"/><line x1="18" y1="6" x2="6" y2="18"/></svg>
      {/if}
    </button>
    <a class="brand" href="#/" title="返回主页">
      <span class="logo" aria-hidden="true">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/></svg>
      </span>
      {title}
    </a>
  </div>

  <div class="searchbox">
    <span class="icon" aria-hidden="true">
      <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="7" cy="7" r="5"/><line x1="11" y1="11" x2="14" y2="14"/></svg>
    </span>
    <input
      bind:this={searchBox}
      bind:value={appState.search}
      type="search"
      aria-label="搜索对象名、说明、字段、过程内容"
      placeholder="搜索对象名、说明、字段、过程内容…"
      autocomplete="off"
      onkeydown={onSearchKeydown}
    >
    <span class="kbd">{kbdHint}</span>
  </div>

  <div class="topbar-right">
    <button class="iconbtn" onclick={(e) => toggleTheme(e)} title="切换主题" aria-label="切换亮色/暗色主题">
      {#if appState.theme === "dark"}
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>
      {:else}
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>
      {/if}
    </button>
  </div>
</header>

<style>
  .topbar {
    display: flex; align-items: center; gap: 12px;
    padding: 0 16px; height: 56px; flex: none;
    background: var(--surface);
    border-bottom: 1px solid var(--border);
    z-index: 100;
  }
  .topbar-left { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 0; }
  .topbar-right { display: flex; align-items: center; justify-content: flex-end; flex: 1; min-width: 0; }

  .brand { display: flex; align-items: center; gap: 10px; font-weight: 700; font-size: 16px; white-space: nowrap; color: var(--text); overflow: hidden; }
  .brand:hover { text-decoration: none; color: var(--text); }
  .logo {
    flex: none; width: 30px; height: 30px; border-radius: 8px;
    background: linear-gradient(135deg, var(--accent), var(--accent-2));
    display: grid; place-items: center; color: #fff;
  }

  .searchbox { flex: 1; max-width: 620px; margin: 0 auto; position: relative; }
  .searchbox input {
    width: 100%; padding: 8px 64px 8px 38px;
    border: 1px solid var(--border); border-radius: 999px;
    background: var(--surface-2); color: var(--text);
    font-size: 13px; outline: none;
    transition: border-color var(--dur-fast), box-shadow var(--dur-fast);
  }
  .searchbox input::-webkit-search-cancel-button { -webkit-appearance: none; }
  .searchbox input:focus { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }
  .searchbox .icon { position: absolute; left: 13px; top: 50%; transform: translateY(-50%); color: var(--muted); display: grid; }
  .searchbox .kbd {
    position: absolute; right: 12px; top: 50%; transform: translateY(-50%);
    font-size: 11px; color: var(--muted); border: 1px solid var(--border);
    border-radius: 4px; padding: 1px 6px; background: var(--surface);
    font-family: var(--font-mono);
  }

  .iconbtn {
    display: grid; place-items: center; width: 34px; height: 34px;
    border: 1px solid var(--border); border-radius: 8px;
    background: var(--surface); color: var(--muted); cursor: pointer;
    transition: color var(--dur-fast), border-color var(--dur-fast), background var(--dur-fast);
  }
  .iconbtn:hover { color: var(--accent); border-color: var(--accent); }
  .iconbtn:active { transform: scale(.94); }

  @media (max-width: 900px) {
    .searchbox { max-width: none; }
    .searchbox .kbd { display: none; }
    .searchbox input { padding-right: 38px; }
  }
</style>
