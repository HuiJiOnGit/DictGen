<script lang="ts">
  import { appState, toggleSidebar, toggleTheme } from "../app-state.svelte";
  import { getMeta } from "../data";
  import { esc } from "../utils";

  const meta = getMeta();
  const title = meta?.title ?? "数据字典";

  let searchBox: HTMLInputElement;

  let themeIcon = $derived(appState.theme === "dark" ? "☀️" : "🌙");

  function onSearchInput(e: Event) {
    appState.search = (e.target as HTMLInputElement).value;
  }

  function onKeydown(e: KeyboardEvent) {
    if (e.key === "Escape") {
      appState.search = "";
      searchBox.value = "";
      searchBox.blur();
    }
  }

  // 全局 Ctrl+K 聚焦搜索框
  import { onMount } from "svelte";
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

<header class="topbar">
  <div class="topbar-left">
    <button class="iconbtn" onclick={toggleSidebar} title={appState.collapsed ? "展开侧栏" : "折叠侧栏"}>
      {appState.collapsed ? "☰" : "✕"}
    </button>
    <a class="brand" href="#/" title="返回主页">
      <span class="logo">📖</span> {@html esc(title)}
    </a>
  </div>

  <div class="searchbox">
    <span class="icon">
      <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="7" cy="7" r="5"/><line x1="11" y1="11" x2="14" y2="14"/></svg>
    </span>
    <input
      bind:this={searchBox}
      type="text"
      placeholder="搜索对象名、说明、字段、过程内容…"
      autocomplete="off"
      value={appState.search}
      oninput={onSearchInput}
      onkeydown={onKeydown}
    >
    <span class="kbd">Ctrl+K</span>
  </div>

  <div class="topbar-right">
    <button class="iconbtn" onclick={toggleTheme} title="切换主题">{themeIcon}</button>
  </div>
</header>