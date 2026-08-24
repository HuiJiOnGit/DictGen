<script lang="ts">
  import type { ObjectType } from "../../types";
  import { TYPE_NAME } from "../../types";

  let { type }: { type: ObjectType } = $props();
</script>

<span class="badge-type {type.toLowerCase()}">{TYPE_NAME[type]}</span>