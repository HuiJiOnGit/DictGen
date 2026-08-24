<script lang="ts">
  import hljs from "highlight.js/lib/core";
  import sql from "highlight.js/lib/languages/sql";
  import { copyText } from "../../utils";

  hljs.registerLanguage("sql", sql);

  let { text, label = "复制", lang = "sql" }: {
    text: string;
    label?: string;
    lang?: string;
  } = $props();

  const html = $derived(
    hljs.getLanguage(lang) ? hljs.highlight(text, { language: lang }).value : text,
  );

  function onCopy() {
    copyText(text);
  }
</script>

<div class="code-wrap">
  <button class="copy-btn" onclick={onCopy}>{label}</button>
  <pre class="code hljs">{@html html}</pre>
</div>
