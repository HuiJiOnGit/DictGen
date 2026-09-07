import type { DictObject } from "./types";

/**
 * 客户端生成表的 CREATE 脚本(便于迁移参考)。
 * 基于分块详情数据重建:字段、自增、默认值、主键、索引、外键。
 */

/** T-SQL 括号标识符:内部 ] 需翻倍转义,否则名字含 ] 时产出坏 SQL */
function id(name: string): string {
  return `[${name.replace(/]/g, "]]")}]`;
}

/** "schema.table" → "[schema].[table]";无 schema 时整体按表名处理 */
function twoPart(ref: string): string {
  const i = ref.indexOf(".");
  return i < 0 ? id(ref) : `${id(ref.slice(0, i))}.${id(ref.slice(i + 1))}`;
}

export function buildTableDdl(o: DictObject): string {
  const sch = id(o.s);
  const nm = id(o.n);

  const lines: string[] = [];
  lines.push(`CREATE TABLE ${sch}.${nm} (`);

  const colLines = (o.cols ?? []).map((c) => {
    let s = `    ${id(c.n)} ${c.ty}`;
    // 计算列的表达式未被数据收录:省略 AS 子句并以注释标注,保证产出仍是合法 SQL
    if (c.cp) return `${s} /* 计算列: 定义表达式未收录 */`;
    if (c.id) s += " IDENTITY(1,1)";
    s += c.nu ? " NULL" : " NOT NULL";
    if (c.de) s += ` DEFAULT ${c.de}`;
    return s;
  });

  const pks = (o.ix ?? []).filter((i) => i.pk);
  if (pks.length) {
    const pkCols = pks.flatMap((p) => p.cols).map(id).join(", ");
    colLines.push(`    CONSTRAINT ${id(pks[0]!.n)} PRIMARY KEY (${pkCols})`);
  }
  lines.push(colLines.join(",\n"));
  lines.push(");");
  lines.push("");

  for (const i of o.ix ?? []) {
    if (i.pk) continue;
    let s = `CREATE ${i.u ? "UNIQUE " : ""}INDEX ${id(i.n)} ON ${sch}.${nm} (${i.cols.map(id).join(", ")})`;
    if (i.inc.length) s += ` INCLUDE (${i.inc.map(id).join(", ")})`;
    if (i.f) s += ` WHERE ${i.f}`;
    lines.push(s + ";");
  }
  lines.push("");

  for (const f of o.fk ?? []) {
    let s = `ALTER TABLE ${sch}.${nm} ADD CONSTRAINT ${id(f.n)} FOREIGN KEY (${f.cols.map(id).join(", ")}) REFERENCES ${twoPart(f.rt)} (${f.rcols.map(id).join(", ")})`;
    if (f.del) s += ` ON DELETE ${f.del}`;
    if (f.upd) s += ` ON UPDATE ${f.upd}`;
    lines.push(s + ";");
  }

  return lines.join("\n");
}
