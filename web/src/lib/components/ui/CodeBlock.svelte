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

  // 复制成功态:按钮短暂显示"已复制"
  let copied = $state(false);
  let copiedTimer: ReturnType<typeof setTimeout> | undefined;

  async function onCopy() {
    await copyText(text);
    copied = true;
    clearTimeout(copiedTimer);
    copiedTimer = setTimeout(() => { copied = false; }, 1500);
  }
</script>

<div class="code-wrap">
  <span class="code-lang" aria-hidden="true">{lang.toUpperCase()}</span>
  <button class="copy-btn" class:done={copied} onclick={onCopy}>
    {copied ? "✓ 已复制" : label}
  </button>
  <pre class="code hljs">{@html html}</pre>
</div>

<style>
  .code-wrap { position: relative; }
  .code-lang {
    position: absolute; top: 10px; left: 12px; z-index: 2;
    font-size: 10px; font-weight: 700; letter-spacing: .8px;
    color: #94a3b8; background: rgba(148, 163, 184, .12);
    border: 1px solid rgba(148, 163, 184, .25);
    border-radius: 4px; padding: 2px 7px;
    font-family: var(--font-mono);
    pointer-events: none;
  }
  .copy-btn {
    position: absolute; top: 10px; right: 10px; z-index: 2;
    font-size: 12px; padding: 4px 10px; border-radius: 6px;
    border: 1px solid #334155; background: #1e293b; color: #cbd5e1; cursor: pointer;
    opacity: .85; transition: opacity var(--dur-fast), color var(--dur-fast), border-color var(--dur-fast);
  }
  .copy-btn:hover { opacity: 1; }
  .copy-btn.done { color: #6ee7b7; border-color: rgba(110, 231, 183, .45); opacity: 1; }
  pre.code {
    background: var(--code-bg); color: var(--code-text);
    padding: 40px 16px 16px; overflow: auto; max-height: 460px;
    font-size: 13px; line-height: 1.65;
  }
  /* highlight.js token 配色(代码块恒为深底);token 由 @html 注入,需 :global */
  .code :global(.hljs-keyword), .code :global(.hljs-built_in) { color: #93c5fd; }
  .code :global(.hljs-string) { color: #a5b4fc; }
  .code :global(.hljs-comment) { color: #94a3b8; font-style: italic; }
  .code :global(.hljs-number) { color: #fbbf24; }
  .code :global(.hljs-type) { color: #6ee7b7; }
  .code :global(.hljs-title), .code :global(.hljs-title.function_) { color: #67e8f9; }
  .code :global(.hljs-operator), .code :global(.hljs-punctuation) { color: #cbd5e1; }
  .code :global(.hljs-variable), .code :global(.hljs-params) { color: #e2e8f0; }
  .code :global(.hljs-meta) { color: #f59e0b; }
</style>
