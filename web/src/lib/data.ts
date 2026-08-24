import type { DictData, DictMeta, DictObject, IndexEntry, ObjectType } from "./types";

export type { DictMeta, IndexEntry, ObjectType };

/**
 * 数据层:读取 .NET 生成的 window.__DICT 全局数据。
 *
 * 分块加载策略:
 * 1. file:// 双击打开:分块脚本由 index.html 静态标签预载(生成器注入),
 *    启动时经 __DICT_CHUNK_QUEUE 队列进入内存 Map —— 静态加载,可靠。
 * 2. http 环境(dev server / IIS / Nginx):若分块未预载,
 *    动态注入 <script> 标签按需加载 —— 同样可用。
 */

const chunkStore = new Map<string, Record<string, DictObject>>();
const loading = new Map<string, Promise<void>>();

// 消费分块脚本执行时入队的 (chunkId, data) 队列
function drainQueue() {
  const queue = (window as unknown as { __DICT_CHUNK_QUEUE?: [string, Record<string, DictObject>][] }).__DICT_CHUNK_QUEUE;
  if (queue) {
    for (const [id, data] of queue) chunkStore.set(id, data);
    queue.length = 0;
  }
  // 后续(动态注入的分块)直接注册
  (window as unknown as { __DICT_CHUNK: (id: string, d: Record<string, DictObject>) => void }).__DICT_CHUNK =
    (id, d) => chunkStore.set(id, d);
}

drainQueue();

/** 元数据与搜索索引(启动即载,由 index.html 中 data/search-index.js 写入) */
export const dictData: DictData | null = (window as unknown as { __DICT?: DictData }).__DICT ?? null;

export function getMeta() {
  return dictData?.meta ?? null;
}

export function getIndex() {
  return dictData?.index ?? [];
}

/** 动态注入分块脚本(http 环境下按需加载的兜底) */
function loadChunkDynamic(chunkId: string): Promise<void> {
  const existing = loading.get(chunkId);
  if (existing) return existing;

  const p = new Promise<void>((resolve, reject) => {
    const s = document.createElement("script");
    s.src = `data/${chunkId}.js`;
    s.onload = () => resolve();
    s.onerror = () => {
      loading.delete(chunkId);
      reject(new Error(`分块 ${chunkId} 加载失败`));
    };
    document.head.appendChild(s);
  });
  loading.set(chunkId, p);
  return p;
}

/** 按对象键加载详情:优先取预载分块,未预载则动态加载 */
export async function loadObject(key: string): Promise<DictObject | null> {
  const entry = getIndex().find((e) => e.k === key);
  if (!entry) return null;

  if (!chunkStore.has(entry.ch)) {
    try {
      await loadChunkDynamic(entry.ch);
    } catch {
      return null;
    }
  }
  return chunkStore.get(entry.ch)?.[key] ?? null;
}
