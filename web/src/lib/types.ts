/**
 * 与 .NET 生成产物契约对齐的类型定义。
 * 数据来源: data/search-index.js (window.__DICT) 与 data/{chunk}.js (window.__DICT_CHUNK)。
 */

export type ObjectType = "T" | "V" | "P";

/** 徽章单字缩写(表/视/过) */
export const TYPE_NAME: Record<ObjectType, string> = { T: "表", V: "视", P: "过" };

/** 类型全称(表/视图/存储过程)—— 全站唯一来源,勿在组件内重复定义 */
export const TYPE_LABEL: Record<ObjectType, string> = { T: "表", V: "视图", P: "存储过程" };

// ---------- 搜索索引 ----------

export interface IndexEntry {
  /** 对象键 "T:dbo.Users" */
  k: string;
  /** 名称 */
  n: string;
  /** schema */
  s: string;
  /** 类型 T/V/P */
  t: ObjectType;
  /** 对象说明 */
  d: string | null;
  /** 列名+列说明 / 参数名 拼接串 */
  c: string | null;
  /** 视图/存储过程正文(可空) */
  def?: string | null;
  /** 所在分块 id */
  ch: string;
}

// ---------- 站点元数据 ----------

export interface DictMeta {
  title: string;
  db: string;
  server: string;
  serverVersion: string | null;
  generatedAt: string;
  counts: {
    tables: number;
    views: number;
    procs: number;
    columns: number;
    procsParams: number;
  };
  /** 字母 → 分块列表 */
  byLetter: Record<string, string[]>;
}

export interface DictData {
  meta: DictMeta;
  index: IndexEntry[];
}

// ---------- 对象详情(分块内) ----------

export interface ColumnObj {
  n: string;         // 列名
  ty: string;        // 显示类型
  nu: boolean;       // 可空
  id: boolean;       // 自增
  pk: boolean;       // 主键
  cp: boolean;       // 计算列
  de: string | null; // 默认值
  dn: string | null; // 列说明
}

export interface IndexObj {
  n: string;                 // 索引名
  u: boolean;                // 唯一
  pk: boolean;               // 主键
  tp: string | null;         // 类型
  cols: string[];            // 键列
  inc: string[];             // 包含列
  f: string | null;          // 筛选条件
}

export interface FkObj {
  n: string;                 // 约束名
  rt: string;                // 引用表 "dbo.Users"
  cols: string[];            // 本表列
  rcols: string[];           // 引用列
  del: string | null;        // 删除规则
  upd: string | null;        // 更新规则
}

export interface ParamObj {
  n: string;                 // 参数名
  ty: string;                // 类型
  dir: string | null;        // IN/OUT
  de: string | null;         // 默认值
}

export interface DictObject {
  s: string;                 // schema
  n: string;                 // 名称
  t: ObjectType;
  d: string | null;          // 说明
  rc?: number | null;        // 行数(仅表)
  ct?: string | null;        // 创建日期(仅表)
  mt?: string | null;        // 修改日期(仅表)
  cols?: ColumnObj[];        // 字段(表/视图)
  ix?: IndexObj[];           // 索引(仅表)
  fk?: FkObj[];              // 外键(仅表)
  prm?: ParamObj[];          // 参数(仅过程)
  def?: string | null;       // 定义(视图/过程)
}

// ---------- 全局挂载契约 ----------

declare global {
  interface Window {
    __DICT?: DictData;
    /** 分块队列 shim:生成器注入的分块脚本先入队,数据层启动时搬入内存 */
    __DICT_CHUNK_QUEUE?: [chunkId: string, data: Record<string, DictObject>][];
    __DICT_CHUNK?: (chunkId: string, data: Record<string, DictObject>) => void;
  }
}

export {};