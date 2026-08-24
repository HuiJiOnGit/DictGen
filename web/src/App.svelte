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

  // 同步主题到 <html>
  $effect(() => {
    document.documentElement.dataset.theme = appState.theme;
  });
</script>

<div class="app" class:collapsed={appState.collapsed}>
  <Topbar />
  <div class="layout">
    <Sidebar />
    <main class="main">
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

<div class="toast"></div>

<svelte:head>
  <title>{meta?.title ?? "数据字典"}</title>
</svelte:head>

<style>
  /* ==========================================================================
     数据字典 — 现代主题
     亮色/暗色双主题,通过 html[data-theme] 切换,无任何外部依赖
     ========================================================================== */
  :global(:root) {
    --bg: #f5f6fa;
    --surface: #ffffff;
    --surface-2: #f0f2f7;
    --border: #e3e6ee;
    --text: #1f2430;
    --muted: #6b7385;
    --accent: #4f46e5;
    --accent-soft: #eef0ff;
    --accent-text: #4338ca;
    --t-table: #2563eb;
    --t-view: #059669;
    --t-proc: #9333ea;
    --badge-bg: #e5e7f2;
    --code-bg: #0f172a;
    --code-text: #e2e8f0;
    --shadow: 0 1px 2px rgba(16, 24, 40, .05), 0 1px 3px rgba(16, 24, 40, .07);
    --radius: 10px;
    --radius-sm: 6px;
  }

  :global(html[data-theme="dark"]) {
    --bg: #0f1117;
    --surface: #171a23;
    --surface-2: #1e2230;
    --border: #2a2f40;
    --text: #e5e9f2;
    --muted: #929bb0;
    --accent: #818cf8;
    --accent-soft: #22284a;
    --accent-text: #a5b0fc;
    --t-table: #60a5fa;
    --t-view: #34d399;
    --t-proc: #c084fc;
    --badge-bg: #262c3d;
    --shadow: 0 1px 2px rgba(0, 0, 0, .3);
  }

  :global(*), :global(*::before), :global(*::after) { box-sizing: border-box; margin: 0; padding: 0; }

  :global(html), :global(body) { height: 100%; }

  :global(body) {
    font-family: "PingFang SC", "Microsoft YaHei", "Segoe UI", "Helvetica Neue", Arial, sans-serif;
    font-size: 14px;
    color: var(--text);
    background: var(--bg);
    line-height: 1.6;
  }

  :global(::selection) { background: var(--accent); color: #fff; }
  :global(a) { color: var(--accent); text-decoration: none; }
  :global(a:hover) { text-decoration: underline; }

  :global(code), :global(pre), :global(.mono) {
    font-family: "Cascadia Code", Consolas, "JetBrains Mono", "Courier New", monospace;
    font-size: 12.5px;
  }

  /* ---------- 布局 ---------- */
  :global(html), :global(body) { height: 100%; overflow: hidden; }
  :global(#app) { height: 100%; } /* mount 目标,补齐 height:100% 链 */
  :global(.app) { display: flex; flex-direction: column; height: 100%; }

  :global(.topbar) {
    display: flex; align-items: center; gap: 12px;
    padding: 0 16px; height: 56px; flex: none;
    background: var(--surface);
    border-bottom: 1px solid var(--border);
    z-index: 100;
    position: sticky; top: 0;
  }
  :global(.topbar-left) { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 0; }
  :global(.topbar-right) { display: flex; align-items: center; justify-content: flex-end; flex: 1; min-width: 0; }

  :global(.brand) { display: flex; align-items: center; gap: 10px; font-weight: 700; font-size: 16px; white-space: nowrap; color: var(--text); }
  :global(.brand:hover) { text-decoration: none; color: var(--text); }
  :global(.brand .logo) {
    width: 30px; height: 30px; border-radius: 8px;
    background: linear-gradient(135deg, var(--accent), #7c3aed);
    display: grid; place-items: center; color: #fff; font-size: 16px;
  }

  :global(.searchbox) { flex: 1; max-width: 620px; margin: 0 auto; position: relative; }
  :global(.searchbox input) {
    width: 100%; padding: 8px 40px 8px 38px;
    border: 1px solid var(--border); border-radius: 999px;
    background: var(--surface-2); color: var(--text);
    font-size: 13.5px; outline: none; transition: border-color .15s, box-shadow .15s;
  }
  :global(.searchbox input:focus) { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }
  :global(.searchbox .icon) { position: absolute; left: 13px; top: 50%; transform: translateY(-50%); color: var(--muted); display: grid; }
  :global(.searchbox .kbd) {
    position: absolute; right: 12px; top: 50%; transform: translateY(-50%);
    font-size: 11px; color: var(--muted); border: 1px solid var(--border);
    border-radius: 4px; padding: 1px 6px; background: var(--surface);
  }

  :global(.iconbtn) {
    display: grid; place-items: center; width: 34px; height: 34px;
    border: 1px solid var(--border); border-radius: 8px;
    background: var(--surface); color: var(--muted); cursor: pointer;
    transition: color .15s, border-color .15s;
  }
  :global(.iconbtn:hover) { color: var(--accent); border-color: var(--accent); }

  :global(.layout) { display: flex; flex: 1; min-height: 0; overflow: hidden; }

  :global(.sidebar) {
    width: 320px; flex: none; display: flex; flex-direction: column;
    background: var(--surface); border-right: 1px solid var(--border);
    min-height: 0;
    transition: margin-left .2s ease;
  }
  :global(.app.collapsed .sidebar) { margin-left: -320px; }

  /* 树型分组: schema 分组头粘顶, 类型分组头粘其下 */
  :global(.side-schema-h) {
    position: sticky; top: 0; z-index: 3;
    height: 34px; display: flex; align-items: center; gap: 6px;
    padding: 0 10px 0 14px; cursor: pointer; user-select: none;
    background: var(--surface); border-bottom: 1px solid var(--border);
    transition: background .12s;
  }
  :global(.side-schema-h:hover) { background: var(--surface-2); }
  :global(.side-schema-h .chev) {
    flex: none; width: 14px; font-size: 10px; color: var(--muted);
    display: grid; place-items: center; transition: transform .12s;
  }
  :global(.side-schema-h.collapsed .chev) { transform: rotate(-90deg); }
  :global(.side-schema-h .sc-nm) {
    flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    font-size: 13px; font-weight: 700; letter-spacing: .3px;
  }
  :global(.side-schema-h .sc-cnt) {
    flex: none; font-size: 11px; color: var(--muted);
    background: var(--badge-bg); border-radius: 8px; padding: 0 7px;
  }
  :global(.side-type-h) {
    position: sticky; top: 34px; z-index: 2;
    height: 27px; display: flex; align-items: center; gap: 6px;
    padding: 0 10px 0 28px; cursor: pointer; user-select: none;
    background: var(--surface-2); border-bottom: 1px solid var(--border);
    color: var(--muted); transition: background .12s;
  }
  :global(.side-type-h:hover) { background: var(--surface); }
  :global(.side-type-h .ty-chev) {
    flex: none; width: 10px; font-size: 9px; color: var(--muted);
    display: grid; place-items: center; transition: transform .12s;
  }
  :global(.side-type-h.collapsed .ty-chev) { transform: rotate(-90deg); }
  :global(.side-type-h .ty-nm) { flex: 1; font-size: 11.5px; font-weight: 700; letter-spacing: .4px; }
  :global(.side-type-h .ty-cnt) { flex: none; font-size: 10.5px; color: var(--muted); }
  :global(.side-type .side-item) { padding-left: 34px; }

  /* 名称二次筛选 */
  :global(.filter-wrap) { padding: 10px 12px; border-bottom: 1px solid var(--border); position: relative; }
  :global(.filter-wrap .icon) { position: absolute; left: 22px; top: 50%; transform: translateY(-50%); color: var(--muted); display: grid; }
  :global(.filter-wrap input) {
    width: 100%; padding: 7px 12px 7px 32px;
    border: 1px solid var(--border); border-radius: 8px;
    background: var(--surface-2); color: var(--text);
    font-size: 12.5px; outline: none; transition: border-color .15s, box-shadow .15s;
  }
  :global(.filter-wrap input:focus) { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }

  /* 对象列表 */
  :global(.sidelist-wrap) { flex: 1; min-height: 0; display: flex; position: relative; }
  :global(.sidelist) { flex: 1; overflow-y: auto; padding: 0 0 8px; position: relative; scroll-behavior: smooth; }
  :global(.sidelist .empty) { padding: 30px 16px; text-align: center; color: var(--muted); font-size: 13px; }

  :global(.side-item) { padding: 4px 10px 4px 14px; cursor: pointer; transition: background .1s; border-left: 3px solid transparent; }
  :global(.side-item:hover), :global(.side-item:focus-visible) { background: var(--surface-2); outline: none; }
  :global(.side-item.active) { background: var(--accent-soft); border-left-color: var(--accent); }
  :global(.side-item.active .nm) { color: var(--accent-text); font-weight: 600; }
  :global(.side-item-row1) { display: flex; align-items: center; gap: 6px; min-width: 0; }
  :global(.side-item .nm) { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; flex: 1; min-width: 0; }
  :global(.side-item .desc) {
    display: block; font-size: 11px; color: var(--muted);
    overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    margin-top: 1px; padding-left: 0;
  }

  :global(.side-stats) { padding: 10px 14px; border-top: 1px solid var(--border); font-size: 11.5px; color: var(--muted); display: flex; gap: 14px; }

  /* ---------- 主区域 ---------- */
  :global(.main) { flex: 1; overflow-y: auto; padding: 24px 28px; min-width: 0; }

  /* 固定底部 上一个/下一个 */
  :global(.nav-pill) {
    position: fixed; bottom: 22px; z-index: 50;
    padding: 9px 22px; border-radius: 999px;
    border: 1px solid var(--border); background: var(--surface);
    color: var(--accent-text); font-size: 13px; font-weight: 600;
    box-shadow: 0 4px 14px rgba(0, 0, 0, .12); text-decoration: none;
    transition: border-color .12s, background .12s;
  }
  :global(.nav-pill:hover) { border-color: var(--accent); background: var(--accent-soft); text-decoration: none; }
  :global(.nav-pill.prev) { left: 338px; }
  :global(.app.collapsed .nav-pill.prev) { left: 16px; }
  :global(.nav-pill.next) { right: 16px; }
  :global(.nav-pill.disabled) { color: var(--muted); opacity: .45; cursor: default; }
  :global(.nav-pill.disabled:hover) { border-color: var(--border); background: var(--surface); }

  /* 全名 Tooltip */
  :global(.tt) {
    position: fixed; z-index: 300; transform: translateY(-50%);
    display: flex; align-items: center; gap: 10px;
    max-width: 440px; padding: 8px 8px 8px 13px;
    background: #1f2430; color: #e5e9f2; border-radius: 8px;
    box-shadow: 0 6px 20px rgba(0, 0, 0, .28);
    font-size: 12px;
  }
  :global(html[data-theme="dark"] .tt) { background: #2a2f40; }
  :global(.tt .tt-nm) { word-break: break-all; line-height: 1.45; max-height: 130px; overflow-y: auto; }
  :global(.tt .tt-copy) {
    flex: none; border: 1px solid rgba(255, 255, 255, .28); background: transparent; color: inherit;
    font-size: 11px; padding: 3px 11px; border-radius: 6px; cursor: pointer; transition: background .12s;
  }
  :global(.tt .tt-copy:hover) { background: rgba(255, 255, 255, .14); }

  /* 仪表盘 */
  :global(.dash) { max-width: 1000px; margin: 0 auto; }
  :global(.dash .hero) { margin-bottom: 26px; }
  :global(.dash .hero h1) { font-size: 24px; font-weight: 800; letter-spacing: .3px; }
  :global(.dash .hero p) { color: var(--muted); margin-top: 6px; }
  :global(.cards) { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 14px; margin-bottom: 26px; }
  :global(.card) {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 18px 20px; box-shadow: var(--shadow);
  }
  :global(.card .num) { font-size: 28px; font-weight: 800; }
  :global(.card .lbl) { font-size: 12.5px; color: var(--muted); margin-top: 2px; }
  :global(.card .num.t) { color: var(--t-table); }
  :global(.card .num.v) { color: var(--t-view); }
  :global(.card .num.p) { color: var(--t-proc); }
  :global(.card .num.a) { color: var(--accent); }

  :global(.panel) {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 18px 20px; box-shadow: var(--shadow); margin-bottom: 14px;
  }
  :global(.panel h3) { font-size: 15px; margin-bottom: 10px; }
  :global(.panel ul) { list-style: none; }
  :global(.panel li) { padding: 5px 0; font-size: 13.5px; color: var(--muted); }
  :global(.panel li b) { color: var(--text); }

  /* 主页对象总览 */
  :global(.ov-nav) { display: flex; gap: 10px; margin-bottom: 16px; }
  :global(.ov-nav a) {
    padding: 7px 16px; border: 1px solid var(--border); border-radius: 999px;
    background: var(--surface); color: var(--muted); font-size: 13px; box-shadow: var(--shadow);
    transition: color .15s, border-color .15s;
  }
  :global(.ov-nav a:hover) { color: var(--accent-text); border-color: var(--accent); text-decoration: none; }
  :global(.ov-sec) { margin-bottom: 18px; }
  :global(.ov-title) { display: flex; align-items: baseline; gap: 8px; font-size: 14px; font-weight: 700; margin-bottom: 8px; }
  :global(.ov-title span) { font-size: 11.5px; color: var(--muted); font-weight: 400; }
  :global(.ov-list) {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-sm);
    max-height: 380px; overflow-y: auto; box-shadow: var(--shadow);
  }
  :global(.ov-item) {
    display: flex; align-items: baseline; gap: 12px;
    padding: 7px 14px; border-bottom: 1px solid var(--border);
    font-size: 13px;
  }
  :global(.ov-item:last-child) { border-bottom: none; }
  :global(.ov-item:hover) { background: var(--surface-2); text-decoration: none; }
  :global(.ov-item .ov-desc) {
    flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    color: var(--muted); font-size: 12px;
  }

  /* ---------- 详情页 ---------- */
  /* 外容器:内容列与右侧目录整体居中 */
  :global(.detail-page) {
    display: flex; justify-content: center; align-items: flex-start;
    gap: 24px;
  }
  /* 内容区:与旧 detail 等宽(最大 1180),目录不再挤占内容 */
  :global(.detail) { flex: 0 1 1180px; min-width: 0; padding-bottom: 84px; }

  /* 粘性标题:面包屑 + 返回按钮 + 类型 + 名称 + 复制 */
  :global(.detail-head) {
    position: sticky; top: -24px; z-index: 20;
    margin: -24px -28px 16px; padding: 10px 28px 10px;
    background: var(--bg); border-bottom: 1px solid var(--border);
  }
  :global(.crumb) {
    display: flex; align-items: center; gap: 6px;
    font-size: 12px; color: var(--muted); flex-wrap: wrap;
    margin-bottom: 8px;
  }
  :global(.crumb .sep) { opacity: .55; }
  :global(.crumb .cur) {
    color: var(--text); font-weight: 600;
    max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }
  :global(.head-main) {
    display: flex; align-items: center; gap: 12px;
  }
  :global(.head-main h1) {
    font-size: 21px; font-weight: 800; word-break: break-all;
    display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
  }
  :global(.head-main h1 .nm) { min-width: 0; }
  :global(.back-btn) {
    flex: none; display: grid; place-items: center;
    width: 34px; height: 34px; border-radius: 50%;
    border: 1px solid var(--border); background: var(--surface);
    color: var(--text); cursor: pointer;
    transition: color .15s, border-color .15s, background .15s;
  }
  :global(.back-btn:hover) { color: var(--accent); border-color: var(--accent); background: var(--accent-soft); }
  :global(.copy-name) {
    flex: none; display: inline-grid; place-items: center;
    width: 24px; height: 24px; border-radius: 6px;
    border: 1px solid var(--border); background: var(--surface);
    color: var(--muted); cursor: pointer;
    transition: color .15s, border-color .15s;
  }
  :global(.copy-name:hover) { color: var(--accent); border-color: var(--accent); }

  /* 右侧章节目录 */
  :global(.toc) {
    position: sticky; top: 120px; flex: none;
    width: 200px; max-height: calc(100vh - 160px); overflow-y: auto;
    padding: 12px 14px; border: 1px solid var(--border); border-radius: var(--radius);
    background: var(--surface); box-shadow: var(--shadow);
  }
  :global(.toc .toc-title) {
    font-size: 11.5px; font-weight: 700; color: var(--muted);
    letter-spacing: .4px; margin-bottom: 8px; text-transform: uppercase;
  }
  :global(.toc .toc-nav) { display: flex; flex-direction: column; gap: 2px; }
  :global(.toc .toc-item) {
    display: flex; align-items: center; gap: 8px;
    padding: 5px 8px; border-radius: 6px; font-size: 12.5px; color: var(--muted);
    border-left: 2px solid transparent; text-decoration: none;
  }
  :global(.toc .toc-item:hover) { background: var(--surface-2); color: var(--text); text-decoration: none; }
  :global(.toc .toc-item.active) { color: var(--accent-text); background: var(--accent-soft); border-left-color: var(--accent); font-weight: 600; }
  :global(.toc .toc-nm) { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  :global(.toc .toc-cnt) { flex: none; font-size: 11px; color: var(--muted); background: var(--badge-bg); border-radius: 8px; padding: 0 6px; }
  :global(.toc .toc-item.active .toc-cnt) { background: var(--accent-soft); }

  :global(.stat-strip) { display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 18px; }
  :global(.stat) {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-sm);
    padding: 8px 16px; font-size: 12.5px; color: var(--muted);
  }
  :global(.stat b) { color: var(--text); font-size: 15px; margin-right: 6px; }

  :global(.sec) {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    box-shadow: var(--shadow); margin-bottom: 16px; overflow: hidden;
    scroll-margin-top: 88px; /* 目录跳转时避开粘性标题 */
  }
  :global(.sec .sec-h) {
    display: flex; align-items: center; gap: 10px;
    padding: 12px 18px; border-bottom: 1px solid var(--border); font-weight: 700; font-size: 14px;
  }
  :global(.sec .sec-h .hint) { font-size: 11.5px; color: var(--muted); font-weight: 400; margin-left: auto; }
  :global(.sec .sec-b) { padding: 8px 18px 16px; }
  :global(.desc-box) {
    background: var(--accent-soft); color: var(--accent-text);
    border-radius: var(--radius-sm); padding: 12px 16px; font-size: 13.5px;
    margin-bottom: 16px;
  }

  :global(.grid) { width: 100%; border-collapse: collapse; font-size: 13px; }
  :global(.grid th) {
    text-align: left; padding: 8px 12px; font-size: 12px; color: var(--muted);
    font-weight: 600; border-bottom: 1px solid var(--border); white-space: nowrap;
    position: sticky; top: 0; background: var(--surface);
  }
  :global(.grid td) { padding: 7px 12px; border-bottom: 1px solid var(--border); vertical-align: top; }
  :global(.grid tbody tr:hover) { background: var(--surface-2); }
  :global(.grid td.mono), :global(.grid th.mono) { font-family: "Cascadia Code", Consolas, monospace; font-size: 12.5px; }
  :global(.grid .num) { text-align: right; }
  :global(.grid .dim) { color: var(--muted); font-size: 12.5px; }
  :global(.grid .empty-row td) { text-align: center; color: var(--muted); padding: 20px; }

  /* 徽标 */
  :global(.badge) {
    display: inline-block; font-size: 10.5px; font-weight: 700; border-radius: 4px;
    padding: 1px 6px; margin-right: 4px;
  }
  :global(.badge.pk) { background: #fde68a; color: #78350f; }
  :global(.badge.id) { background: #99f6e4; color: #134e4a; }
  :global(.badge.null) { background: var(--badge-bg); color: var(--muted); }
  :global(.badge.cp) { background: #e9d5ff; color: #6b21a8; }
  :global(html[data-theme="dark"] .badge.pk) { background: #78350f; color: #fde68a; }
  :global(html[data-theme="dark"] .badge.id) { background: #134e4a; color: #99f6e4; }
  :global(html[data-theme="dark"] .badge.cp) { background: #581c87; color: #e9d5ff; }

  /* 类型徽章 */
  :global(.badge-type) {
    flex: none; font-size: 10.5px; font-weight: 700; padding: 1px 7px; border-radius: 5px;
    letter-spacing: .5px; color: #fff;
  }
  :global(.badge-type.t) { background: var(--t-table); }
  :global(.badge-type.v) { background: var(--t-view); }
  :global(.badge-type.p) { background: var(--t-proc); }

  /* 代码块 */
  :global(.code-wrap) { position: relative; }
  :global(.copy-btn) {
    position: absolute; top: 10px; right: 10px; z-index: 2;
    font-size: 11.5px; padding: 4px 10px; border-radius: 6px;
    border: 1px solid #334155; background: #1e293b; color: #cbd5e1; cursor: pointer;
    opacity: .85; transition: opacity .15s;
  }
  :global(.copy-btn:hover) { opacity: 1; }
  :global(pre.code) {
    background: var(--code-bg); color: var(--code-text);
    padding: 14px 16px; overflow: auto; max-height: 460px;
    font-size: 12.5px; line-height: 1.65;
  }
  /* highlight.js token 配色(代码块恒为深底) */
  :global(.code .hljs-keyword), :global(.code .hljs-built_in) { color: #93c5fd; }
  :global(.code .hljs-string) { color: #a5b4fc; }
  :global(.code .hljs-comment) { color: #94a3b8; font-style: italic; }
  :global(.code .hljs-number) { color: #fbbf24; }
  :global(.code .hljs-type) { color: #6ee7b7; }
  :global(.code .hljs-title), :global(.code .hljs-title.function_) { color: #67e8f9; }
  :global(.code .hljs-operator), :global(.code .hljs-punctuation) { color: #cbd5e1; }
  :global(.code .hljs-variable), :global(.code .hljs-params) { color: #e2e8f0; }
  :global(.code .hljs-meta) { color: #f59e0b; }

  /* 搜索页 */
  :global(.search-page) { max-width: 1100px; margin: 0 auto; }
  :global(.search-page .head) { margin-bottom: 16px; }
  :global(.search-page .head h1) { font-size: 19px; }
  :global(h1 mark), :global(.result mark) { background: #fde68a; color: #78350f; border-radius: 3px; padding: 0 2px; }
  :global(html[data-theme="dark"] h1 mark), :global(html[data-theme="dark"] .result mark) { background: #78350f; color: #fde68a; }

  :global(.group-title) {
    padding: 10px 0 4px; font-size: 11.5px; font-weight: 700;
    color: var(--muted); text-transform: uppercase; letter-spacing: .4px;
  }
  :global(.result) {
    display: flex; align-items: baseline; gap: 12px;
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-sm);
    padding: 12px 16px; margin-bottom: 8px; cursor: pointer;
    transition: border-color .12s, box-shadow .12s;
  }
  :global(.result:hover), :global(.result:focus-visible) { border-color: var(--accent); box-shadow: var(--shadow); outline: none; }
  :global(.result .nm) { font-weight: 700; font-size: 14.5px; min-width: 200px; }
  :global(.result .sc) { color: var(--muted); font-size: 12px; }
  :global(.result .nm-desc) { color: var(--muted); font-size: 12.5px; flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  :global(.result .def-snip) { display: block; }

  :global(.search-more) { text-align: center; padding: 10px; color: var(--muted); font-size: 12.5px; }
  :global(.more-btn) {
    border: 1px solid var(--border); background: var(--surface); color: var(--accent);
    font-size: 12.5px; padding: 6px 16px; border-radius: 999px; cursor: pointer;
    transition: border-color .12s, background .12s;
  }
  :global(.more-btn:hover) { border-color: var(--accent); background: var(--accent-soft); }

  /* 加载/提示 */
  :global(.loading) { text-align: center; padding: 50px; color: var(--muted); }
  :global(.spinner) {
    width: 28px; height: 28px; margin: 0 auto 12px; border-radius: 50%;
    border: 3px solid var(--border); border-top-color: var(--accent);
    animation: spin .8s linear infinite;
  }

  /* Svelte 会自动把 @keyframes 提升为全局 */
  @keyframes spin { to { transform: rotate(360deg); } }
  :global(.toast) {
    position: fixed; bottom: 24px; left: 50%; transform: translateX(-50%);
    background: #1f2430; color: #fff; padding: 10px 18px; border-radius: 8px;
    font-size: 13px; z-index: 100; opacity: 0; pointer-events: none;
    transition: opacity .2s; box-shadow: 0 4px 16px rgba(0,0,0,.25);
  }
  :global(.toast.show) { opacity: 1; }

  :global(::-webkit-scrollbar) { width: 10px; height: 10px; }
  :global(::-webkit-scrollbar-thumb) { background: var(--border); border-radius: 5px; }
  :global(::-webkit-scrollbar-thumb:hover) { background: var(--muted); }
  :global(::-webkit-scrollbar-track) { background: transparent; }

  /* 响应式 */
  @media (max-width: 1439px) {
    /* 屏幕不够宽时隐藏右侧目录,内容区回到全宽 */
    :global(.toc) { display: none; }
  }
  @media (max-width: 900px) {
    :global(.sidebar) { display: none; }
    :global(.layout) { flex-direction: column; }
    :global(.main) { padding: 16px; }
    :global(.searchbox) { max-width: none; }
    :global(.detail-head) { margin: -16px -16px 10px; padding: 8px 16px 8px; top: -16px; }
    :global(.nav-pill.prev) { left: 16px; }
  }
</style>