import { mount } from "svelte";
import "./lib/styles/global.css";
import App from "./App.svelte";

/** 挂载点可能在 head 中的同步脚本里不存在,等待 DOMContentLoaded */
function bootstrap() {
  const target = document.getElementById("app");
  if (!target) {
    // 壳损坏时给出可见提示,而非静默白屏
    document.body.textContent = "应用挂载失败:页面缺少 #app 容器。";
    return;
  }
  mount(App, { target });
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", bootstrap);
} else {
  bootstrap();
}