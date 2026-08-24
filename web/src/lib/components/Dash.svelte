<script lang="ts">
  import { getIndex, getMeta } from "../data";
  import { navigateTo } from "../router.svelte";
  import type { IndexEntry, ObjectType } from "../types";
  import { esc } from "../utils";

  const meta = getMeta();
  const m = meta!;
  const counts = m.counts;

  const TYPE_LABEL: Record<ObjectType, string> = { T: "表", V: "视图", P: "存储过程" };
  const TYPE_KEY: Record<ObjectType, string> = { T: "t", V: "v", P: "p" };

  // 按类型分组,组内按名称排序
  const groups = $derived.by(() => {
    const map: Record<ObjectType, IndexEntry[]> = { T: [], V: [], P: [] };
    for (const e of getIndex()) {
      if (map[e.t]) map[e.t]!.push(e);
    }
    for (const k of Object.keys(map) as ObjectType[]) {
      map[k]!.sort((a, b) => a.n.localeCompare(b.n, "zh-CN"));
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

<div class="dash">
  <div class="hero">
    <h1>{m.title}</h1>
    <p>
      {#if m.db}数据库 <b>{m.db}</b>{/if}
      {#if m.server} · 服务器 {m.server}{/if}
      {#if m.serverVersion} · {m.serverVersion}{/if}
      {#if m.generatedAt} · 生成于 {m.generatedAt}{/if}
    </p>
  </div>

  <div class="cards">
    <div class="card"><div class="num a">{counts.tables}</div><div class="lbl">表</div></div>
    <div class="card"><div class="num v">{counts.views}</div><div class="lbl">视图</div></div>
    <div class="card"><div class="num p">{counts.procs}</div><div class="lbl">存储过程</div></div>
    <div class="card"><div class="num a">{counts.columns}</div><div class="lbl">字段(表+视图)</div></div>
  </div>

  <div class="ov-nav">
    {#if groups.T.length}<a href="#ov-t" onclick={(e) => scrollToAnchor("ov-t", e)}>表 ({groups.T.length})</a>{/if}
    {#if groups.V.length}<a href="#ov-v" onclick={(e) => scrollToAnchor("ov-v", e)}>视图 ({groups.V.length})</a>{/if}
    {#if groups.P.length}<a href="#ov-p" onclick={(e) => scrollToAnchor("ov-p", e)}>存储过程 ({groups.P.length})</a>{/if}
  </div>

  {#each Object.keys(TYPE_LABEL) as type (type)}
    {@const list = groups[type as ObjectType]}
    {#if list.length}
      <div class="ov-sec" id="ov-{TYPE_KEY[type as ObjectType]}">
        <div class="ov-title">{TYPE_LABEL[type as ObjectType]} <span>{list.length} 个</span></div>
        <div class="ov-list">
          {#each list as e (e.k)}
            <a
              class="ov-item"
              href="#/{TYPE_KEY[e.t]}/{e.s}.{e.n}"
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
      <li>· 左侧<b>页签</b>筛选<b>表 / 视图 / 存储过程</b>,下方输入框可按<b>名称二次筛选</b></li>
      <li>· 右侧<b>字母索引</b>为快速定位导航,点击跳转到对应分组,滚动自动高亮</li>
      <li>· 顶部<b>搜索框</b>支持按<b>对象名、说明、字段名、过程内容</b>本地搜索, <b>Ctrl+K</b> 聚焦, <b>Esc</b> 清除</li>
      <li>· 打开表详情可查看<b>字段、索引、外键</b>与可复制的一键 <b>CREATE 脚本</b></li>
      <li>· 本页面为纯静态文件, 可放入任意 Web 服务器或直接双击使用</li>
    </ul>
  </div>
</div>