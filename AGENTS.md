# AGENTS.md — 给 AI 协作者的项目指南

本文件面向 AI 编码/测试代理,说明 DictGen 的结构、构建方式与**自动化验证方法**。重点:如何在不依赖真实数据库的情况下端到端测试,以及如何用浏览器验证前端。

---

## 1. 项目是什么

.NET 10 控制台应用 + Svelte 5 前端,生成"数据库数据字典"静态站点(表/视图/存储过程文档)。

- 后端(`src/`):读数据库 → 流式生成静态产物
- 前端(`web/`):Svelte 5 + Vite,构建产物直出 `output/`
- 产物:纯静态文件,支持 file:// 双击打开与 HTTP 部署

## 2. 关键技术决策(改动前必读)

| 决策 | 说明 |
|------|------|
| **流式管线** | `ISchemaProvider.EnumerateObjectsAsync` 返回 `IAsyncEnumerable<SchemaObject>`,内部 Channel 三路并行(表/视图/过程);生成器消费时按字母分块(≤400 对象/块)、3 路并行写盘。**不要**改回"全量读入内存再生成" |
| **file:// 兼容** | 产物必须支持双击打开:app.js 为 **IIFE**(非 ESM)、相对路径、分块以**静态 `<script>` 标签**注入 index.html(`<!--DICT_CHUNKS-->` 占位符),前端不做动态 script 注入 |
| **脚本顺序** | index.html 中:search-index.js → `__DICT_CHUNK_QUEUE` 桥接 → 分块脚本 → **app.js 必须在最后**(否则 `window.__DICT` 未加载,页面空白) |
| **前后端分离** | 前端 build 写 `index.html/app.js/style.css`(`emptyOutDir:false` 不清 data/);后端只写 `data/` + 注入分块引用,不覆盖外壳。两者可独立运行 |
| **输出目录锚定** | 相对路径 `OutputDirectory` 在 `Program.cs` 锚定到**解决方案根**(向上找 `DictGen.slnx`),任何 CWD 下产物都到 `output/` |
| **partial 拆分** | `SqlServerSchemaProvider` 按对象类型拆成 `SqlServerSchemaProvider.cs`(入口+通用)/`Tables.cs`/`Views.cs`/`Procedures.cs`,新查询放对应文件 |
| **键控 DI** | 提供器/生成器用 `AddKeyedSingleton` 注册,`appsettings.json` 的 `Provider`/`Generator` 键选择实现 |

## 3. 构建

```bash
# 后端(全部项目)
dotnet build DictGen.slnx

# 前端(需要 pnpm;改 UI 后必须重新 build 才会反映到产物)
cd web && pnpm install && pnpm build   # 产物 → ../output/
```

前端构建约束(vite.config.ts 已处理):IIFE 单文件、`base:'./'`、剥离 `type="module"`、app.js 移到 body 末尾。**若输出 index.html 中出现 `type="module"` 或脚本在 head 中,说明构建链路被破坏。**

## 4. 运行与生成

```bash
# 示例模式(无需数据库,3表/1视图/2过程,几秒完成)
dotnet run --project src/DictGen.Cli -- --sample

# 真实数据库(读 src/DictGen.Cli/appsettings.json 的 ConnectionString)
dotnet run --project src/DictGen.Cli
```

产物统一输出到 `DictGen/output/`(index.html + app.js + style.css + data/)。

## 5. 自动化测试方法(重点)

### 5.1 无数据库的端到端测试(首选)

**示例模式即可覆盖完整管线**(读库→流式归档→分块写盘→外壳注入),无需真实数据库:

```bash
dotnet build DictGen.slnx
dotnet run --project src/DictGen.Cli -- --sample
```

断言产物:
- `output/index.html` 存在,且**不含** `type="module"`,`app.js` 位于 `</body>` 前
- `output/index.html` 含 `data/search-index.js` 引用、`__DICT_CHUNK_QUEUE` 桥接、分块 `<script src="data/X.js">` 标签(示例模式应有 U/O/V/S.js)
- `output/data/` 下有 `search-index.js` + 分块文件
- `output/data/search-index.js` 中对象键格式 `"k":"T:dbo.Users"`,且过程/视图条目**含 `def` 字段**(正文搜索依赖)

### 5.2 用 LocalDB 测真实 SQL 路径(可选)

本机有 SQL Server LocalDB(MSSQLLocalDB)。可用环境变量覆盖连接串,**不要改动用户的 appsettings.json**:

```bash
# 启动 LocalDB 并建测试库(表+视图+过程+外键+注释)
sqllocaldb start MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE TestDict;"

# 用环境变量覆盖连接串运行(优先级高于 appsettings.json)
Database__ConnectionString="Server=(localdb)\MSSQLLocalDB;Database=TestDict;Integrated Security=True;TrustServerCertificate=True" \
  dotnet run --project src/DictGen.Cli
```

验证真实 SQL 路径:`📋 表清单` → `🔗 字段/索引/外键` → `👁️ 视图` → `⚙️ 过程` 日志齐全且无 ❌。

> ⚠️ 注意:`Program.cs` 用 `Configuration.Sources.Insert(0, ...)` 加载程序目录的 appsettings.json,**环境变量/命令行参数仍可覆盖**(插入在最前)。若改动了配置加载逻辑,务必保持此语义。

### 5.3 浏览器 UI 验证

产物是静态站,可用任意静态服务器(如 python)或 Vite dev server 预览:

```bash
# 方式 A:静态服务器(验证产物本身)
python -m http.server 8765 --directory output   # http://localhost:8765/index.html

# 方式 B:Vite dev(验证前端源码,自动代理 output/data/)
cd web && pnpm dev   # http://localhost:5173
```

用浏览器自动化(如 Playwright)验证的核心用例:

1. **主页**:标题为 `{库名}数据字典`;统计卡片(表/视图/过程/字段);按类型分组锚点列表;点击对象可进详情
2. **搜索**:输入 `DATEADD`(仅存在于示例过程定义中)→ 应命中 `SP_CancelExpiredOrders` 并显示定义片段;搜索框 Ctrl+K 聚焦、Esc 清除
3. **侧栏**:字母导航条一屏可见(固定高度,不随列表滚动);点击字母定位到对应分组;当前打开对象**高亮**且自动滚动到可见
4. **详情页**:字段表格(类型/可空/默认/说明)、索引、外键(可点击跳转)、DDL 可复制;底部「上一个/主页/下一个」按 表→视图→过程 全局顺序跳转
5. **布局**:body 不整体滚动(overflow:hidden);顶栏/侧栏固定;侧栏列表与主区各自滚动
6. **file:// 兼容**:直接打开 `output/index.html`(不经 HTTP),详情页应正常加载(依赖静态分块注入;若报"分块加载失败"或一直"加载中",检查 index.html 分块 script 标签)

### 5.4 常见回归点(改动后重点验证)

| 改动 | 回归风险 |
|------|----------|
| 改 `ObjectArchive.cs` | 搜索索引字段(k/n/s/t/d/c/**def**/ch)缺失 → 正文搜索失效;分块键(首字母)变化 → 前端加载 404 |
| 改生成器写盘逻辑 | `PrepareOutputDirectory` 只应清 `data/`,**不得删除前端外壳**(前后端分离) |
| 改 vite.config.ts | IIFE 丢失 → file:// 空白;`emptyOutDir` 误开 → 清掉 data/;app.js 位置错误 → 页面空白 |
| 改 index.html 模板 | 必须保留 `<!--DICT_CHUNKS-->` 占位符与 `__DICT_CHUNK_QUEUE` 桥接 |
| 改接口(ISchemaProvider 等) | `SampleSchemaProvider` 与 `SqlServerSchemaProvider` 必须同步实现 |
| 改 Program.cs 配置加载 | 保持"程序目录 appsettings.json 优先、环境变量/命令行仍可覆盖"语义 |

## 6. 代码规范约定

- **接口优先**:新数据库/新产物实现接口,不改主流程
- **流式优先**:批量读数据的方法不要改回逐对象 N+1 查询;定义文本用 `LoadDefinitionsAsync` 分批(每批 50)
- **属性注入**:构造函数注入的参数存入只读属性再使用,不直接引用构造参数
- **emoji 日志**:后端日志用 emoji 开头(📡📋🔗👁️⚙️🚀📦🎉✅❌),方便肉眼扫读
- **partial 拆分**:SqlServer 提供器新查询按对象类型放入对应 partial 文件
- **不碰用户配置**:测试用环境变量覆盖连接串,不要改写 `src/DictGen.Cli/appsettings.json` 中的真实连接串
