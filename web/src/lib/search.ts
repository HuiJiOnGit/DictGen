import { getIndex } from "./data";
import type { IndexEntry, ObjectType } from "./types";

/** 搜索结果条目:索引项 + 相关性分数(0 最佳) */
export interface SearchHit {
  e: IndexEntry;
  score: number;
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
    const name = e.n.toLowerCase();
    const desc = (e.d ?? "").toLowerCase();
    const cols = (e.c ?? "").toLowerCase();
    const def = (e.def ?? "").toLowerCase();

    const inName = name.includes(q);
    const inDesc = desc.includes(q);
    const inCols = cols.includes(q);
    const inDef = def.includes(q);
    if (!inName && !inDesc && !inCols && !inDef) continue;

    const score = inName && name.startsWith(q)
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