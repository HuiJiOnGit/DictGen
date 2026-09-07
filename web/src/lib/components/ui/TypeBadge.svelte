<script lang="ts">
  import type { ObjectType } from "../../types";
  import { TYPE_NAME } from "../../types";

  let { type }: { type: ObjectType } = $props();
</script>

<span class="badge-type {type.toLowerCase()}">{TYPE_NAME[type]}</span>

<style>
  /* 类型徽章:亮色用加深底色保证白字对比度 ≥4.5;暗色改深色文字 */
  .badge-type {
    flex: none; font-size: 11px; font-weight: 700; padding: 1px 7px; border-radius: 5px;
    letter-spacing: .5px; color: #fff;
  }
  .badge-type.t { background: var(--t-table); }
  .badge-type.v { background: #047857; }
  .badge-type.p { background: #7e22ce; }
  :global(html[data-theme="dark"]) .badge-type { color: var(--bg); }
</style>
