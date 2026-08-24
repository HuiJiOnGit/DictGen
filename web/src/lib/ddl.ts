import type { DictObject } from "./types";

/**
 * 客户端生成表的 CREATE 脚本(便于迁移参考)。
 * 基于分块详情数据重建:字段、自增、默认值、主键、索引、外键。
 */
export function buildTableDdl(o: DictObject): string {
  const lines: string[] = [];
  const sch = o.s;
  const nm = o.n;

  lines.push(`CREATE TABLE [${sch}].[${nm}] (`);
  const colLines = (o.cols ?? []).map((c) => {
    let s = `    [${c.n}] ${c.ty}`;
    if (c.id) s += " IDENTITY(1,1)";
    if (c.cp) s += " AS (计算列)";
    if (c.nu) s += " NULL";
    else s += " NOT NULL";
    if (c.de) s += ` DEFAULT ${c.de}`;
    return s;
  });

  const pks = (o.ix ?? []).filter((i) => i.pk);
  if (pks.length) {
    const pkCols = pks.flatMap((p) => p.cols).map((c) => `[${c}]`).join(", ");
    colLines.push(`    CONSTRAINT [${pks[0]!.n}] PRIMARY KEY (${pkCols})`);
  }
  lines.push(colLines.join(",\n"));
  lines.push(");");
  lines.push("");

  for (const i of o.ix ?? []) {
    if (i.pk) continue;
    let s = `CREATE ${i.u ? "UNIQUE " : ""}INDEX [${i.n}] ON [${sch}].[${nm}] (${i.cols.map((c) => `[${c}]`).join(", ")})`;
    if (i.inc.length) s += ` INCLUDE (${i.inc.map((c) => `[${c}]`).join(", ")})`;
    if (i.f) s += ` WHERE ${i.f}`;
    lines.push(s + ";");
  }
  lines.push("");

  for (const f of o.fk ?? []) {
    let s = `ALTER TABLE [${sch}].[${nm}] ADD CONSTRAINT [${f.n}] FOREIGN KEY (${f.cols.map((c) => `[${c}]`).join(", ")}) REFERENCES [${f.rt.replace(".", "].[")}] (${f.rcols.map((c) => `[${c}]`).join(", ")})`;
    if (f.del) s += ` ON DELETE ${f.del}`;
    if (f.upd) s += ` ON UPDATE ${f.upd}`;
    lines.push(s + ";");
  }

  return lines.join("\n");
}