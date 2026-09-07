# DictGen — 数据库数据字典生成器

基于 **.NET 10 + 原生 ADO.NET(Microsoft.Data.SqlClient)** 的数据库结构文档生成工具。读取数据库元数据(表、视图、存储过程、字段、索引、外键、说明),生成**现代化、可离线双击打开**的静态数据字典站点。

前端基于 **Svelte 5 + TypeScript + Vite** 构建,产物为纯静态文件:可直接双击打开(`file://`),也可部署到任意 Web 服务器(IIS / Nginx / 内网共享)。4000+ 对象规模下也不会出现 vuepress 式 OOM——数据由 .NET **流式生成**(Channel 边读边写),前端分块加载,内存占用有界。

---

## ✨ 功能特性

- **三类对象全覆盖**:表(字段/索引/外键/行数/DDL 脚本)、视图(字段/定义)、存储过程(参数/定义),均含 `MS_Description` 注释
- **本地全文搜索**:对象名、说明、字段名+字段注释、过程/视图正文,即输即搜,命中词高亮 + 定义片段预览
- **通讯录式浏览**:侧栏按首字母分组,右侧竖排字母索引条——一屏可见、点击快速定位、滚动自动高亮(纯导航,非过滤)
- **当前选中高亮**:详情页与侧栏联动,选中项高亮显示 + 自动滚动到可视区域
- **名称二次筛选**:侧栏页签(全部/表/视图/过程)+ 名称模糊过滤
- **主页总览**:数据库名/服务器/版本/生成时间、统计卡片、按类型分组锚点列表
- **固定布局**:顶栏/侧栏固定,侧栏与主区各自独立滚动
- **详情页导航**:底部「‹ 上一个 / 主页 / 下一个 ›」,按表→视图→过程全局顺序跳转
- **细节拉满**:PK/自增/计算列徽标、可点击外键跳转、一键复制 CREATE 脚本(客户端生成)
- **亮/暗双主题**、侧栏折叠、hash 路由(`#/t/dbo.Users` 可分享)、Ctrl+K 聚焦搜索
- **两种产物模式**:分块 SPA(默认)与单文件 HTML(全部内联,便于拷贝分发)

---

## 🏗️ 架构

```
DictGen/
├── DictGen.slnx
├── Directory.Build.props          # 公共构建属性 (net10.0)
├── Directory.Packages.props       # 集中包版本管理 (Central Package Management)
├── output/                        # 产物目录(前后端汇合,双击 index.html 即看)
├── src/
│   ├── DictGen.Abstractions/          # 抽象层:接口 + 数据库无关模型
│   │   ├── ISchemaProvider.cs             # 流式接口 → IAsyncEnumerable<SchemaObject>
│   │   ├── ISchemaInfoProvider.cs         # 连接信息(库名/服务器/版本)
│   │   ├── IDataDictionaryGenerator.cs    # 产物生成器接口(流式)
│   │   ├── GenerationOptions.cs / DatabaseOptions.cs
│   │   └── Models/                        # Table/View/Procedure/Column/Index/FK/SchemaObject...
│   ├── DictGen.SqlServer/            # SQL Server 提供器(partial 多文件拆分,原生 T-SQL)
│   │   ├── SqlServerSchemaProvider.cs     # 入口:流式 Channel 三路并行 + 通用辅助
│   │   ├── Tables.cs                      # 表:清单/字段/索引/外键
│   │   ├── Views.cs                       # 视图:清单/字段/定义(分批)
│   │   └── Procedures.cs                  # 存储过程:清单/参数/定义(分批)
│   ├── DictGen.PostgreSql/           # 占位:后续按需实现 PostgreSQL 提供器
│   ├── DictGen.Generators.StaticSite/# 静态站点生成器
│   │   ├── StaticSiteGenerator.cs         # 流式生成:Channel 多消费者并行写盘
│   │   └── ObjectArchive.cs               # 流式归档:增量搜索索引 + 按字母分块
│   └── DictGen.Cli/                 # 控制台入口(Host + 键控 DI + BackgroundService)
│       ├── Program.cs                     # Host 构建 + 输出目录锚定解决方案根
│       ├── DictionaryGenerationService.cs # 主流程:读库 → 流式生成 编排
│       ├── SampleSchemaProvider.cs        # 内置示例库(--sample)
│       ├── appsettings.example.json       # 配置模板(复制为 appsettings.json 后填写连接串)
│       └── appsettings.json               # 本地真实配置(.gitignore 排除,不入库)
└── web/                            # 前端工程(Svelte 5 + TS + Vite + pnpm)
    ├── vite.config.ts              # IIFE 输出;build 直出 ../output/;dev 代理 data/
    └── src/lib/
        ├── data.ts / search.ts / ddl.ts / router.svelte.ts / types.ts
        └── components/             # Topbar / Sidebar / Dash / Detail / 详情页组件
```

**流式管线(核心设计)**:读库与生成重叠进行,内存有界——

```
读库(3路并行,独立连接)      归档(内存有界)          写盘(3路并行)
┌─────────────┐   SchemaObject   ┌──────────────┐   ChunkJob   ┌──────────────┐
│ 表生产者      │ ──────────────→ │ ObjectArchive │ ──────────→ │ Writer×3     │
│ 视图生产者    │  Channel(200)  │ 逐对象归档     │  Channel(20)│ 并发写.js     │
│ 过程生产者    │                 │ 满400即出块    │             │              │
└─────────────┘                 └──────────────┘             └──────────────┘
```

**面向接口设计**:新增数据库或产物形式只需新实现一个接口并注册,主程序零改动:

| 关注点 | 接口 | 实现 |
|--------|------|------|
| 数据库 | `ISchemaProvider` / `ISchemaInfoProvider` | `DictGen.SqlServer`(键 `"SqlServer"`)、`SampleSchemaProvider`(键 `"Sample"`) |
| 产物 | `IDataDictionaryGenerator` | `StaticSiteGenerator`(键 `"StaticSite"`) |

通过 `Microsoft.Extensions.DependencyInjection` **键控 DI** 注册,由 `appsettings.json` 的 `Database:Provider` / `Generation:Generator` 选择。

---

## 📋 环境要求

| 工具 | 版本 | 说明 |
|------|------|------|
| .NET SDK | 10.0+ | `dotnet --version` 确认 |
| pnpm | 9+ | 仅前端开发/构建时需要;只跑生成器不需要 |

---

## 🚀 快速开始

### 方式一:示例模式(无需数据库,立即体验)

```bash
cd DictGen
dotnet run --project src/DictGen.Cli -- --sample
```

### 方式二:生成真实数据库字典

1. 复制配置模板并填入连接串(仓库只含脱敏的 `appsettings.example.json`,需先另存为 `appsettings.json`——真实文件已被 `.gitignore` 排除,不会误提交):

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=你的服务器;Database=库名;User Id=账号;Password=密码;TrustServerCertificate=True;Encrypt=True"
  }
}
```

2. 运行:

```bash
dotnet run --project src/DictGen.Cli
```

3. 打开产物:

```bash
start output/index.html
```

> **部署提示**:产物是纯静态文件,可直接拷贝到 IIS 站点、Nginx、或内网共享目录。分块模式双击 `index.html` 即可用(`file://` 兼容);数据分块由生成器静态注入 `<script>` 标签,无需 HTTP 服务。
>
> **输出目录锚定**:无论从哪个工作目录启动(`dotnet run` / 直接运行 exe / bin 目录),相对路径 `OutputDirectory` 都会解析到**解决方案根**下的 `output/`,与前端 `pnpm build` 产物一致——只有一个预览入口。

### 方式三:本地 HTTP 预览(可选)

产物是纯静态文件,可让任意静态服务器**监听端口**预览(比 `file://` 更接近真实部署;部分浏览器对 `file://` 有限制):

```bash
python -m http.server 8765 --directory output   # 浏览器打开 http://localhost:8765
# Node 环境: npx --yes serve output -l 8765
```

监听地址/端口可自行修改;数据分块由生成器静态注入 `<script>` 标签,HTTP 预览与双击打开效果一致。

---

## ⚙️ 前后端分离构建

前端(UI 外壳)与后端(数据)可**独立生成**,产物汇入同一目录 `output/`:

```bash
# ① 前端:构建 UI 外壳(仅改界面时执行)
cd web
pnpm build          # → 输出 index.html / app.js / style.css 到 ../output/

# ② 后端:读取数据库生成数据(仅改数据/连接串时执行)
cd ..
dotnet run --project src/DictGen.Cli

# ③ 前端开发模式:Vite dev server,自动代理后端已生成的 data/ 目录
cd web
pnpm dev            # → http://localhost:5173 (数据来自上次后端生成的 data/)
```

- 前端构建**不会清空** `data/` 目录(`emptyOutDir: false`)
- 后端生成**不会覆盖**前端外壳文件,只写入 `data/` 并往 `index.html` 的 `<!--DICT_CHUNKS-->` 占位符注入分块脚本引用
- 先跑哪边都行:后端单独跑会生成 data/ + 往 index.html 注入分块引用(外壳不存在时只写数据);前端单独跑可预览 UI(数据未生成时搜索/详情不可用)

---

## ⚙️ 配置说明(`appsettings.json`)

仓库提供脱敏模板 `src/DictGen.Cli/appsettings.example.json`,复制为 `appsettings.json` 后填写真实值即可(该文件已加入 `.gitignore`,不会被提交)。

```jsonc
{
  "Database": {
    "Provider": "SqlServer",          // 提供器键名,对应键控 DI 注册
    "ConnectionString": "..."         // 连接串
  },
  "Generation": {
    "Generator": "StaticSite",        // 生成器键名
    "OutputDirectory": "output",      // 输出目录(相对解决方案根;锚定到含 DictGen.slnx 的目录)
    "SiteTitle": "",                  // 站点标题;留空/默认时自动为 "{库名}数据字典"
    "IncludeTables": true,            // 是否生成表
    "IncludeViews": true,             // 是否生成视图
    "IncludeProcedures": true,        // 是否生成存储过程
    "IncludeObjectDefinitions": true, // 是否读取视图/存储过程定义文本
    "EmbedSingleFile": false,         // true = 单文件 HTML(全部内联);false = 分块 SPA
    "CleanOutputDirectory": true,     // 生成前清空 data/ 子目录(不碰前端外壳)
    "SchemaFilter": []                // 只生成指定 schema,如 ["dbo"];空 = 全部
  }
}
```

命令行参数:

```bash
dotnet run --project src/DictGen.Cli -- --sample        # 使用内置示例库
dotnet run --project src/DictGen.Cli --                 # 使用 appsettings.json 配置
```

---

## 📄 许可证

[MIT](LICENSE) © 2026 HuiJiOnGit

---

## 🎨 前端开发(仅修改 UI 时需要)

前端工程在 `web/`,构建产物直接输出到 `../output/`(不再是嵌入资源):

```bash
cd web
pnpm install        # 首次
pnpm dev            # 开发模式:http://localhost:5173,自动代理 output/data/ 数据
pnpm build          # 生产构建 → 输出到 ../output/(index.html / app.js / style.css)
```

构建约束(`vite.config.ts` 已处理):

构建约束(`vite.config.ts` 已处理):

- **IIFE 单文件**输出(默认 ESM 会被 `file://` 的 CORS 拦截)
- `base: './'` 相对路径
- 构建后剥离 `type="module"`,`app.js` 移到 `<body>` 末尾(必须在数据脚本之后执行,否则 `window.__DICT` 未加载页面空白)
- `pnpm-workspace.yaml` 中已配置 `onlyBuiltDependencies: [esbuild]`(pnpm 11 安全策略)

> **注意**:`pnpm build` 不会清空 `data/`(`emptyOutDir: false`),请勿手动修改 `data/`;数据契约(`window.__DICT` / `__DICT_CHUNK`)由 .NET 生成器保证,前端 `src/lib/types.ts` 与之对齐。

---

## ❓ 常见问题

**Q: 重新生成后浏览器还是旧页面?**
产物已给 `app.js`/`style.css` 附加版本号(`?v=时间戳`),正常刷新即可;若仍异常请强制刷新(Ctrl+F5)。

**Q: 4000+ 对象会卡吗 / 会 OOM 吗?**
不会。数据由 .NET 流式生成(Channel 背压 + 分块满即写、写完全释放),前端仅加载轻量搜索索引 + 分块(每块 ≤400 对象)。

**Q: file:// 双击打开时详情页一直"加载中"?**
说明分块脚本未注入 index.html(前端 build 后未跑后端,或产物被手动修改)。重新执行 `dotnet run --project src/DictGen.Cli -- --sample` 生成即可——分块以静态 `<script>` 标签注入,file:// 下可靠。

**Q: `dotnet run` 报找不到 `appsettings.json` 的配置?**
配置显式从程序目录(`AppContext.BaseDirectory`)加载,与工作目录无关。克隆仓库后请先 `cp src/DictGen.Cli/appsettings.example.json src/DictGen.Cli/appsettings.json` 并填写连接串,确认编辑的是该文件且已重新构建。

**Q: 单文件模式输出哪些文件?**
仅一个自包含 `index.html`(样式/逻辑/全部数据内联),拷贝单个文件即可分发。

**Q: 存储过程/视图的定义文本哪里来?**
`IncludeObjectDefinitions: true` 时从 `sys.sql_modules` 分批读取(每批 500 个、并发 4 批;经 `COMPRESS` 以 GZIP 传输、客户端解压);搜索结果会索引并预览定义内容。全量字段/定义文本这类大结果集在远程库上均走压缩单值通道,目录查询带 HASH JOIN 提示避免逐行探测的随机 IO。**生成目标库需要 SQL Server 2016+**(`COMPRESS`/`FOR JSON`)。
