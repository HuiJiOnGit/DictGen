<script lang="ts">
  import { buildTableDdl } from "../ddl";
  import { hrefOf } from "../router.svelte";
  import type { DictObject } from "../types";
  import { fmtNum } from "../utils";
  import CodeBlock from "./ui/CodeBlock.svelte";

  let { obj }: { obj: DictObject } = $props();

  const cols = $derived(obj.cols ?? []);
  const ix = $derived(obj.ix ?? []);
  const fk = $derived(obj.fk ?? []);

  const ddl = $derived(buildTableDdl(obj));
</script>

<div class="stat-strip">
  <div class="stat"><b>{fmtNum(obj.rc)}</b>行数(估算)</div>
  <div class="stat"><b>{cols.length}</b>字段</div>
  <div class="stat"><b>{ix.length}</b>索引</div>
  <div class="stat"><b>{fk.length}</b>外键</div>
  <div class="stat"><b>{obj.ct ?? "-"}</b>创建</div>
  <div class="stat"><b>{obj.mt ?? "-"}</b>修改</div>
</div>

<div class="sec" id="sec-cols">
  <div class="sec-h">字段清单 <span class="hint">{cols.length} 个字段 · 主键以 PK 标记</span></div>
  <div class="sec-b">
    <table class="grid">
      <thead>
        <tr>
          <th class="w-idx" scope="col">#</th>
          <th scope="col">字段名</th>
          <th scope="col">类型</th>
          <th class="w-null" scope="col">可空</th>
          <th scope="col">默认值</th>
          <th scope="col">说明</th>
        </tr>
      </thead>
      <tbody>
        {#each cols as c, i (c.n)}
          <tr>
            <td class="num dim">{i + 1}</td>
            <td class="mono">
              <b>{c.n}</b>
              {#if c.pk}<span class="badge pk">PK</span>{/if}
              {#if c.id}<span class="badge id">IDENTITY</span>{/if}
              {#if c.cp}<span class="badge cp">计算列</span>{/if}
              {#if c.nu}<span class="badge null">可空</span>{/if}
            </td>
            <td class="mono">{c.ty}</td>
            <td class="dim">{c.nu ? "是" : "否"}</td>
            <td class="mono dim">{c.de ?? "-"}</td>
            <td>{c.dn ?? ""}</td>
          </tr>
        {/each}
        {#if cols.length === 0}
          <tr class="empty-row"><td colspan="6">无字段</td></tr>
        {/if}
      </tbody>
    </table>
  </div>
</div>

<div class="sec" id="sec-ix">
  <div class="sec-h">索引 <span class="hint">{ix.length} 个</span></div>
  <div class="sec-b">
    {#if ix.length === 0}
      <div class="empty-note">暂无索引</div>
    {:else}
      <table class="grid">
        <thead>
          <tr>
            <th scope="col">索引名</th>
            <th class="w-attr" scope="col">属性</th>
            <th scope="col">类型</th>
            <th scope="col">键列</th>
            <th scope="col">包含列</th>
            <th scope="col">筛选条件</th>
          </tr>
        </thead>
        <tbody>
          {#each ix as i (i.n)}
            <tr>
              <td class="mono"><b>{i.n}</b></td>
              <td>
                {#if i.pk}<span class="badge pk">PK</span>{/if}
                {#if i.u}<span class="badge id">UNIQUE</span>{/if}
              </td>
              <td class="mono">{i.tp ?? ""}</td>
              <td class="mono">{i.cols.join(", ")}</td>
              <td class="mono dim">{i.inc.length ? i.inc.join(", ") : "-"}</td>
              <td class="mono dim">{i.f ?? "-"}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    {/if}
  </div>
</div>

<div class="sec" id="sec-fk">
  <div class="sec-h">外键 <span class="hint">{fk.length} 个 · 点击引用表可跳转</span></div>
  <div class="sec-b">
    {#if fk.length === 0}
      <div class="empty-note">暂无外键</div>
    {:else}
      <table class="grid">
        <thead>
          <tr>
            <th scope="col">约束名</th>
            <th scope="col">本表列</th>
            <th scope="col">引用表</th>
            <th scope="col">引用列</th>
            <th scope="col">更新/删除</th>
          </tr>
        </thead>
        <tbody>
          {#each fk as f (f.n)}
            <tr>
              <td class="mono">{f.n}</td>
              <td class="mono">{f.cols.join(", ")}</td>
              <td class="mono"><a href={hrefOf("T:" + f.rt)}>{f.rt}</a></td>
              <td class="mono dim">{f.rcols.join(", ")}</td>
              <td class="dim">{f.del ?? ""}{f.upd && f.upd !== f.del ? ` / ${f.upd}` : ""}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    {/if}
  </div>
</div>

<div class="sec" id="sec-ddl">
  <div class="sec-h">CREATE 脚本 <span class="hint">客户端生成, 便于迁移参考</span></div>
  <div class="sec-b">
    <CodeBlock text={ddl} />
  </div>
</div>
