<script lang="ts">
  import { getIndex, getMeta } from "../data";
  import { hrefOf, navigateTo } from "../router.svelte";
  import type { IndexEntry, ObjectType } from "../types";

  const meta = getMeta();

  const TYPE_LABEL: Record<ObjectType, string> = { T: "表", V: "视图", P: "存储过程" };
  const TYPE_KEY: Record<ObjectType, string> = { T: "t", V: "v", P: "p" };
  const TYPE_ORDER: ObjectType[] = ["T", "V", "P"];

  // 按类型分组,组内按名称排序
  const groups = $derived.by(() => {
    const map: Record<ObjectType, IndexEntry[]> = { T: [], V: [], P: [] };
    for (const e of getIndex()) {
      if (map[e.t]) map[e.t]!.push(e);
    }
    for (const t of TYPE_ORDER) {
      map[t]!.sort((a, b) => a.n.localeCompare(b.n, "zh-CN"));
    }
    return map;
  });

  function scrollToAnchor(id: string, ev: Event) {
    ev.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function openObject(e: IndexEntry) {
    navigateTo(e.k);
  }
</script>

{#if !meta}
  <!-- 数据脚本缺失/加载失败的兜底视图:给出可操作的排查提示,而不是白屏报错 -->
  <div class="dash">
    <div class="hero">
      <h1>数据未加载</h1>
      <p>未能读取数据文件 <code class="mono">data/search-index.js</code></p>
    </div>
    <div class="panel">
      <ul>
        <li>· 若为 http 部署:请确认 <b>data/</b> 目录与 index.html 同级,且服务器可访问</li>
        <li>· 若数据为空:请先运行后端生成 <b>dotnet run --project src/DictGen.Cli</b></li>
      </ul>
    </div>
  </div>
{:else}
  <div class="dash">
    <div class="hero">
      <h1>{meta.title}</h1>
      <p>
        {#if meta.db}数据库 <b>{meta.db}</b>{/if}
        {#if meta.server} · 服务器 {meta.server}{/if}
        {#if meta.serverVersion} · {meta.serverVersion}{/if}
        {#if meta.generatedAt} · 生成于 {meta.generatedAt}{/if}
      </p>
    </div>

    <div class="cards">
      <div class="card"><div class="num t">{meta.counts.tables}</div><div class="lbl">表</div></div>
      <div class="card"><div class="num v">{meta.counts.views}</div><div class="lbl">视图</div></div>
      <div class="card"><div class="num p">{meta.counts.procs}</div><div class="lbl">存储过程</div></div>
      <div class="card"><div class="num a">{meta.counts.columns}</div><div class="lbl">字段(表+视图)</div></div>
    </div>

    <div class="ov-nav">
      {#if groups.T.length}<a href="#ov-t" onclick={(e) => scrollToAnchor("ov-t", e)}>表 ({groups.T.length})</a>{/if}
      {#if groups.V.length}<a href="#ov-v" onclick={(e) => scrollToAnchor("ov-v", e)}>视图 ({groups.V.length})</a>{/if}
      {#if groups.P.length}<a href="#ov-p" onclick={(e) => scrollToAnchor("ov-p", e)}>存储过程 ({groups.P.length})</a>{/if}
    </div>

    {#each TYPE_ORDER as type (type)}
      {@const list = groups[type]}
      {#if list.length}
        <div class="ov-sec" id="ov-{TYPE_KEY[type]}">
          <div class="ov-title">{TYPE_LABEL[type]} <span>{list.length} 个</span></div>
          <div class="ov-list">
            {#each list as e (e.k)}
              <a
                class="ov-item"
                href={hrefOf(e.k)}
                onclick={() => openObject(e)}
              >
                <span class="mono">{e.s}.{e.n}</span>
                <span class="ov-desc">{e.d ?? ""}</span>
              </a>
            {/each}
          </div>
        </div>
      {/if}
    {/each}

    <div class="panel">
      <h3>使用说明</h3>
      <ul>
        <li>· 左侧目录按 <b>schema 与类型</b>分组浏览,顶部输入框可按<b>名称二次筛选</b></li>
        <li>· 顶部<b>搜索框</b>支持按<b>对象名、说明、字段名、过程内容</b>本地搜索, <b>Ctrl+K</b> 聚焦, <b>Esc</b> 清除</li>
        <li>· 打开表详情可查看<b>字段、索引、外键</b>与可复制的一键 <b>CREATE 脚本</b></li>
        <li>· 本页面为纯静态文件, 可放入任意 Web 服务器或直接双击使用</li>
      </ul>
    </div>
  </div>
{/if}

<style>
  .dash { max-width: 1000px; margin: 0 auto; }
  .hero { margin-bottom: 26px; }
  .hero h1 { font-size: 24px; font-weight: 800; letter-spacing: .3px; }
  .hero p { color: var(--muted); margin-top: 6px; }

  .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 14px; margin-bottom: 26px; }
  .card {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 18px 20px; box-shadow: var(--shadow);
  }
  .card .num { font-size: 28px; font-weight: 800; }
  .card .lbl { font-size: 13px; color: var(--muted); margin-top: 2px; }
  .card .num.t { color: var(--t-table); }
  .card .num.v { color: var(--t-view); }
  .card .num.p { color: var(--t-proc); }
  .card .num.a { color: var(--accent); }

  .panel {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 18px 20px; box-shadow: var(--shadow); margin-bottom: 14px;
  }
  .panel h3 { font-size: 15px; margin-bottom: 10px; }
  .panel ul { list-style: none; }
  .panel li { padding: 5px 0; font-size: 14px; color: var(--muted); }
  .panel li b { color: var(--text); }

  /* 对象总览 */
  .ov-nav { display: flex; gap: 10px; margin-bottom: 16px; }
  .ov-nav a {
    padding: 7px 16px; border: 1px solid var(--border); border-radius: 999px;
    background: var(--surface); color: var(--muted); font-size: 13px; box-shadow: var(--shadow);
    transition: color var(--dur-fast), border-color var(--dur-fast);
  }
  .ov-nav a:hover { color: var(--accent-text); border-color: var(--accent); text-decoration: none; }
  .ov-sec { margin-bottom: 18px; }
  .ov-title { display: flex; align-items: baseline; gap: 8px; font-size: 14px; font-weight: 700; margin-bottom: 8px; }
  .ov-title span { font-size: 12px; color: var(--muted); font-weight: 400; }
  .ov-list {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-sm);
    max-height: 380px; overflow-y: auto; box-shadow: var(--shadow);
  }
  .ov-item {
    display: flex; align-items: baseline; gap: 12px;
    padding: 7px 14px; border-bottom: 1px solid var(--border);
    font-size: 13px;
  }
  .ov-item:last-child { border-bottom: none; }
  .ov-item:hover { background: var(--surface-2); text-decoration: none; }
  .ov-item .ov-desc {
    flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    color: var(--muted); font-size: 12px;
  }
</style>
