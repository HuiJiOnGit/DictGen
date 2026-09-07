import { getIndex } from "./data";
import type { IndexEntry, ObjectType } from "./types";

/** 搜索结果条目:索引项 + 相关性分数(0 最佳) */
export interface SearchHit {
  e: IndexEntry;
  score: number;
}

/** 小写字段缓存:正文(数千条过程全文)只做一次 toLowerCase,搜索时复用 */
interface LoweredEntry {
  n: string;
  d: string;
  c: string;
  def: string;
}
const lcCache = new WeakMap<IndexEntry, LoweredEntry>();

function lowered(e: IndexEntry): LoweredEntry {
  let x = lcCache.get(e);
  if (!x) {
    x = {
      n: e.n.toLowerCase(),
      d: (e.d ?? "").toLowerCase(),
      c: (e.c ?? "").toLowerCase(),
      def: (e.def ?? "").toLowerCase(),
    };
    lcCache.set(e, x);
  }
  return x;
}

/**
 * 本地搜索:按对象名、说明、字段/参数、视图与过程正文匹配,
 * 返回按相关性排序的结果(名称前缀命中 > 名称包含 > 字段/参数 > 说明 > 正文)。
 */
export function searchIndex(query: string, typeFilter: "all" | ObjectType): SearchHit[] {
  const q = query.trim().toLowerCase();
  if (!q) return [];

  const hits: SearchHit[] = [];
  for (const e of getIndex()) {
    if (typeFilter !== "all" && e.t !== typeFilter) continue;
    const low = lowered(e);

    const inName = low.n.includes(q);
    const inDesc = low.d.includes(q);
    const inCols = low.c.includes(q);
    const inDef = low.def.includes(q);
    if (!inName && !inDesc && !inCols && !inDef) continue;

    const score = inName && low.n.startsWith(q)
      ? 0
      : inName ? 1
      : inCols ? 2
      : inDesc ? 3
      : 4;

    hits.push({ e, score });
  }

  hits.sort((a, b) => a.score - b.score || a.e.n.localeCompare(b.e.n, "zh-CN"));
  return hits;
}
