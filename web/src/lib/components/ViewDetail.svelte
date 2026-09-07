<script lang="ts">
  import type { DictObject } from "../types";
  import CodeBlock from "./ui/CodeBlock.svelte";

  let { obj }: { obj: DictObject } = $props();
  const cols = $derived(obj.cols ?? []);
</script>

<div class="stat-strip">
  <div class="stat"><b>{cols.length}</b>字段</div>
</div>

<div class="sec" id="sec-cols">
  <div class="sec-h">字段清单 <span class="hint">{cols.length} 个字段</span></div>
  <div class="sec-b">
    <table class="grid">
      <thead>
        <tr>
          <th class="w-idx" scope="col">#</th>
          <th scope="col">字段名</th>
          <th scope="col">类型</th>
          <th class="w-null" scope="col">可空</th>
          <th scope="col">说明</th>
        </tr>
      </thead>
      <tbody>
        {#each cols as c, i (c.n)}
          <tr>
            <td class="num dim">{i + 1}</td>
            <td class="mono"><b>{c.n}</b></td>
            <td class="mono">{c.ty}</td>
            <td class="dim">{c.nu ? "是" : "否"}</td>
            <td>{c.dn ?? ""}</td>
          </tr>
        {/each}
        {#if cols.length === 0}
          <tr class="empty-row"><td colspan="5">无字段</td></tr>
        {/if}
      </tbody>
    </table>
  </div>
</div>

{#if obj.def}
  <div class="sec" id="sec-def">
    <div class="sec-h">视图定义</div>
    <div class="sec-b">
      <CodeBlock text={obj.def} />
    </div>
  </div>
{/if}
