import { mount } from "svelte";
import App from "./App.svelte";

/** 挂载点可能在 head 中的同步脚本里不存在,等待 DOMContentLoaded */
function bootstrap() {
  const target = document.getElementById("app");
  if (!target) return;
  mount(App, { target });
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", bootstrap);
} else {
  bootstrap();
}