import { defineConfig, type ConfigEnv, type Plugin, type UserConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import { fileURLToPath } from "node:url";
import { readFileSync, writeFileSync, existsSync } from "node:fs";

const OUT_DIR = fileURLToPath(
  new URL("../output/", import.meta.url),
);

/**
 * 构建后修正 index.html:
 * 1. 剥离 `type="module"` 与 crossorigin —— file:// 下 module 脚本会被 CORS 拦截
 * 2. 把 app.js 脚本移到 </body> 前 —— 必须在 data/search-index.js 与分块脚本之后执行,
 *    否则 app.js 启动时 window.__DICT 尚未加载,页面渲染为空
 */
function patchIndexHtml(): Plugin {
  return {
    name: "patch-index-html",
    apply: "build",
    closeBundle() {
      const htmlPath = OUT_DIR + "/index.html";
      try {
        let source = readFileSync(htmlPath, "utf-8");
        source = source
          .replace('<script type="module" crossorigin', "<script")
          .replace('<script type="module"', "<script")
          .replace('<link rel="stylesheet" crossorigin', '<link rel="stylesheet"');

        // 把 app.js 从 head 移到 body 末尾(数据脚本之后)
        const appScript = source.match(/\s*<script src="\.\/app\.js"><\/script>/)?.[0];
        if (appScript) {
          source = source.replace(appScript, "").replace("</body>", `${appScript}\n</body>`);
        }

        if (source !== readFileSync(htmlPath, "utf-8")) {
          writeFileSync(htmlPath, source, "utf-8");
          console.log("  ✓ index.html: module scripts stripped, app.js moved to end of body");
        }
      } catch (e) {
        this.warn(`patch-index-html: ${htmlPath}: ${(e as Error).message}`);
      }
    },
  };
}

/** 开发模式下把 /data/* 请求映射到后端上次生成的产物(outDir/data/) */
function dataProxy(): Plugin {
  return {
    name: "data-proxy",
    apply: "serve",
    configureServer(server) {
      // 必须在 configureServer 内直接注册(pre 中间件),
      // 返回函数注册的是 post 中间件,会排在 Vite HTML fallback 之后被抢先响应
      server.middlewares.use((req, res, next) => {
        const url = req.url ?? "";
        if (!url.startsWith("/data/")) return next();
        const rel = decodeURIComponent(url.slice("/data/".length));
        const file = OUT_DIR + "/data/" + rel;
        if (!existsSync(file)) {
          res.statusCode = 404;
          res.end(`data/${rel} 不存在，请先运行后端生成：dotnet run --project ../src/DictGen.Cli`);
          return;
        }
        const ext = file.split(".").pop() ?? "";
        const type = ext === "js" ? "application/javascript" : "text/plain";
        res.setHeader("Content-Type", type);
        res.end(readFileSync(file));
      });
    },
  };
}

export default defineConfig((_env: ConfigEnv): UserConfig => ({
  plugins: [
    svelte(),
    patchIndexHtml(),
    dataProxy(),
  ],
  base: "./",
  server: {
    allowedHosts: true,
  },
  build: {
    outDir: OUT_DIR,
    emptyOutDir: false, // 不清空保留后端生成的 data/
    cssCodeSplit: false,
    rolldownOptions: {
      input: fileURLToPath(new URL("./index.html", import.meta.url)),
      output: {
        format: "iife", // Rolldown 对 iife 强制单分块(等价旧版 inlineDynamicImports)
        entryFileNames: "app.js",
        assetFileNames: "style.css",
      },
    },
  },
}));
