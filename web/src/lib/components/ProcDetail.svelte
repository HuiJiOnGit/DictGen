<script lang="ts">
  import type { DictObject } from "../types";
  import CodeBlock from "./ui/CodeBlock.svelte";

  let { obj }: { obj: DictObject } = $props();
  const prm = $derived(obj.prm ?? []);
</script>

<div class="stat-strip">
  <div class="stat"><b>{prm.length}</b>参数</div>
</div>

<div class="sec" id="sec-prm">
  <div class="sec-h">参数清单 <span class="hint">{prm.length} 个参数</span></div>
  <div class="sec-b">
    <table class="grid">
      <thead>
        <tr><th style="width:44px">#</th><th>参数名</th><th>类型</th><th style="width:80px">方向</th><th>默认值</th></tr>
      </thead>
      <tbody>
        {#each prm as p, i (p.n)}
          <tr>
            <td class="num dim">{i + 1}</td>
            <td class="mono"><b>{p.n}</b></td>
            <td class="mono">{p.ty}</td>
            <td>{p.dir ?? ""}</td>
            <td class="mono dim">{p.de ?? "-"}</td>
          </tr>
        {/each}
        {#if prm.length === 0}
          <tr class="empty-row"><td colspan="5">无参数</td></tr>
        {/if}
      </tbody>
    </table>
  </div>
</div>

{#if obj.def}
  <div class="sec" id="sec-def">
    <div class="sec-h">过程定义</div>
    <div class="sec-b">
      <CodeBlock text={obj.def} />
    </div>
  </div>
{/if}
