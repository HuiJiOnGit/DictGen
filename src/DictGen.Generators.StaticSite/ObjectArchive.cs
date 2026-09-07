using System.Text.Json.Serialization;
using DictGen.Abstractions;
using DictGen.Abstractions.Models;

namespace DictGen.Generators.StaticSite;

/// <summary>已就绪待写盘的完整分块。</summary>
public sealed record ChunkJob(string ChunkId, Dictionary<string, object> Objects);

/// <summary>
/// 流式对象归档器:逐对象接收 <see cref="SchemaObject"/>,
/// 实时转成紧凑产物结构、追加搜索索引、按首字母分块;
/// 分块写满即返回 <see cref="ChunkJob"/> 交由写盘消费者,内存有界。
///
/// 产物 JSON 形状(属性名刻意缩短以控制体积):
/// 搜索索引项: { k, n, s, t, d, c, ch }
/// 对象详情:   { s, n, t, d, rc/ct/mt, cols:[...], ix:[...], fk:[...], prm:[...], def }
/// </summary>
internal sealed class ObjectArchive
{
    /// <summary>每个分块最多容纳的对象数,超过则拆子块。</summary>
    public const int MaxObjectsPerChunk = 400;

    private const string TableKey = "T";
    private const string ViewKey = "V";
    private const string ProcKey = "P";

    // 待写分块缓冲区: letter -> 当前分块
    private readonly Dictionary<string, List<object>> _pending = [];
    private readonly Dictionary<string, int> _chunkSeq = [];
    private readonly List<object> _searchIndex = [];
    private readonly List<string> _chunkOrder = [];
    private readonly Dictionary<string, int> _counts = new()
    {
        ["tables"] = 0, ["views"] = 0, ["procs"] = 0, ["columns"] = 0, ["procsParams"] = 0,
    };
    private readonly Dictionary<string, List<string>> _byLetter = [];

    public int ObjectCount { get; private set; }

    /// <summary>全部分块的 id,按产出顺序(供外壳注入脚本标签与单文件模式内联枚举)。</summary>
    public IReadOnlyList<string> ChunkIds => _chunkOrder;

    public string? DatabaseName { get; }
    public string? ServerName { get; }
    public string? ServerVersion { get; }

    public ObjectArchive(SchemaSourceInfo? sourceInfo = null)
    {
        DatabaseName = sourceInfo?.DatabaseName;
        ServerName = sourceInfo?.ServerName;
        ServerVersion = sourceInfo?.ServerVersion;
    }

    /// <summary>逐字母(A-Z | '#')取分组键。</summary>
    public static string LetterOf(string name)
    {
        if (name.Length > 0)
        {
            var c = char.ToUpperInvariant(name[0]);
            if (c is >= 'A' and <= 'Z') return c.ToString();
        }
        return "#";
    }

    /// <summary>
    /// 分组键 → 分块基名。'#' 在 URL 里是片段起始符,不能作为脚本 src / 动态加载的文件名
    /// (data/#.js 会变成请求 data/ + 空 fragment),故用 '0' 落盘;前端只依赖索引里的 ch 字段,无感知。
    /// </summary>
    private static string ChunkBaseOf(string letter) => letter == "#" ? "0" : letter;

    /// <summary>接收一个对象;若某字母分块已写满,返回待写盘作业,否则 null。</summary>
    public ChunkJob? Add(SchemaObject obj)
    {
        object? wire = obj.Kind switch
        {
            SchemaObjectKind.Table when obj.Table is not null => MakeTableObject(obj.Table),
            SchemaObjectKind.View when obj.View is not null => MakeViewObject(obj.View),
            SchemaObjectKind.Procedure when obj.Procedure is not null => MakeProcObject(obj.Procedure),
            _ => null,
        };
        if (wire is null) return null;

        var letter = LetterOf(NameOf(wire));
        if (!_pending.TryGetValue(letter, out var list))
        {
            list = [];
            _pending[letter] = list;
            if (!_byLetter.ContainsKey(letter)) _byLetter[letter] = [];
        }

        // 该对象所属分块 id(当前打开的块)
        var seq = _chunkSeq.GetValueOrDefault(letter);
        var baseId = ChunkBaseOf(letter);
        var chunkId = seq == 0 ? baseId : $"{baseId}-{seq + 1}";
        list.Add(wire);
        ObjectCount++;

        // 搜索索引实时追加
        var type = TypeOf(wire);
        _searchIndex.Add(new Dictionary<string, object>
        {
            ["k"] = KeyOf(wire),
            ["n"] = NameOf(wire),
            ["s"] = SchemaOf(wire),
            ["t"] = type,
            ["d"] = ((IDictionary<string, object>)wire).TryGetValue("d", out var d) ? d : null,
            ["c"] = BuildSearchableColumns(wire),
            // 视图/存储过程正文,支持按内容搜索
            ["def"] = ((IDictionary<string, object>)wire).TryGetValue("def", out var def) ? def : null,
            ["ch"] = chunkId,
        });

        // 计数
        switch (type)
        {
            case TableKey:
                _counts["tables"]++;
                _counts["columns"] += ColCount(wire);
                break;
            case ViewKey:
                _counts["views"]++;
                _counts["columns"] += ColCount(wire);
                break;
            case ProcKey:
                _counts["procs"]++;
                _counts["procsParams"] += PrmCount(wire);
                break;
        }

        // 写满 → 关闭当前分块,作业交由调用方写盘(此处不留存,保持内存有界)
        if (list.Count >= MaxObjectsPerChunk)
        {
            _byLetter[letter].Add(chunkId);
            _chunkOrder.Add(chunkId);
            var job = new ChunkJob(chunkId, list.ToDictionary(o => KeyOf(o), o => o));
            _pending.Remove(letter);
            _chunkSeq[letter] = seq + 1;
            return job;
        }
        return null;
    }

    /// <summary>全部对象已接收:关闭所有未满分块并返回这些尾块作业(满块已在 <see cref="Add"/> 返回时交给调用方)。</summary>
    public IEnumerable<ChunkJob> Complete()
    {
        var closed = new List<ChunkJob>();
        foreach (var (letter, list) in _pending)
        {
            if (list.Count == 0) continue;
            var seq = _chunkSeq.GetValueOrDefault(letter);
            var baseId = ChunkBaseOf(letter);
            var chunkId = seq == 0 ? baseId : $"{baseId}-{seq + 1}";
            _byLetter[letter].Add(chunkId);
            _chunkOrder.Add(chunkId);
            closed.Add(new ChunkJob(chunkId, list.ToDictionary(o => KeyOf(o), o => o)));
        }
        _pending.Clear();
        return closed;
    }

    /// <summary>搜索索引 + 站点元数据,供前端启动即载。</summary>
    public Dictionary<string, object> BuildSearchIndex(string title, DateTime generatedAt)
    {
        return new Dictionary<string, object>
        {
            ["meta"] = new Dictionary<string, object>
            {
                ["title"] = title,
                ["db"] = DatabaseName ?? "",
                ["server"] = ServerName ?? "",
                ["serverVersion"] = ServerVersion,
                ["generatedAt"] = generatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ["counts"] = _counts.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["byLetter"] = _byLetter.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            },
            ["index"] = _searchIndex,
        };
    }

    // ---------- 内部工具 ----------

    private static int ColCount(object wire) =>
        ((IDictionary<string, object>)wire).TryGetValue("cols", out var c) && c is System.Collections.ICollection l ? l.Count : 0;

    private static int PrmCount(object wire) =>
        ((IDictionary<string, object>)wire).TryGetValue("prm", out var p) && p is System.Collections.ICollection l ? l.Count : 0;

    private static string BuildSearchableColumns(object wire)
    {
        var o = (IDictionary<string, object>)wire;
        var sb = new System.Text.StringBuilder();
        if (o.TryGetValue("cols", out var colsObj) && colsObj is System.Collections.IEnumerable cols)
        {
            foreach (var col in cols)
            {
                if (col is not IDictionary<string, object> cd) continue;
                sb.Append(cd["n"]);
                if (cd.TryGetValue("dn", out var dn) && dn is string dnStr && dnStr.Length > 0)
                    sb.Append('(').Append(dnStr).Append(')');
                sb.Append(", ");
            }
        }
        if (o.TryGetValue("prm", out var prmObj) && prmObj is System.Collections.IEnumerable prms)
        {
            foreach (var p in prms)
            {
                if (p is IDictionary<string, object> pd)
                    sb.Append(pd["n"]).Append(", ");
            }
        }
        return sb.Length > 2 ? sb.ToString(0, sb.Length - 2) : "";
    }

    private static string TypeOf(object o) => (string)((IDictionary<string, object>)o)["t"];
    private static string NameOf(object o) => (string)((IDictionary<string, object>)o)["n"];
    private static string SchemaOf(object o) => (string)((IDictionary<string, object>)o)["s"];
    private static string KeyOf(object o) => $"{TypeOf(o)}:{SchemaOf(o)}.{NameOf(o)}";

    // ---------- 对象归档 ----------

    private static Dictionary<string, object> MakeTableObject(TableInfo t) => new()
    {
        ["s"] = t.Schema,
        ["n"] = t.Name,
        ["t"] = TableKey,
        ["d"] = t.Description,
        ["rc"] = t.RowCount,
        ["ct"] = t.CreatedAt?.ToString("yyyy-MM-dd"),
        ["mt"] = t.ModifiedAt?.ToString("yyyy-MM-dd"),
        ["cols"] = t.Columns.Select(MakeColumn).ToList(),
        ["ix"] = t.Indexes.Select(MakeIndex).ToList(),
        ["fk"] = t.ForeignKeys.Select(MakeForeignKey).ToList(),
    };

    private static Dictionary<string, object> MakeViewObject(ViewInfo v) => new()
    {
        ["s"] = v.Schema,
        ["n"] = v.Name,
        ["t"] = ViewKey,
        ["d"] = v.Description,
        ["cols"] = v.Columns.Select(MakeColumn).ToList(),
        ["def"] = v.Definition,
    };

    private static Dictionary<string, object> MakeProcObject(ProcedureInfo p) => new()
    {
        ["s"] = p.Schema,
        ["n"] = p.Name,
        ["t"] = ProcKey,
        ["d"] = p.Description,
        ["prm"] = p.Parameters.Select(prm => (object)new Dictionary<string, object>
        {
            ["n"] = prm.Name,
            ["ty"] = prm.DataTypeFull,
            ["dir"] = prm.Direction,
            ["de"] = prm.DefaultValue,
        }).ToList(),
        ["def"] = p.Definition,
    };

    private static object MakeColumn(ColumnInfo c) => new Dictionary<string, object>
    {
        ["n"] = c.Name,
        ["ty"] = c.DataTypeFull,
        ["nu"] = c.IsNullable,
        ["id"] = c.IsIdentity,
        ["pk"] = c.IsPrimaryKey,
        ["cp"] = c.IsComputed,
        ["de"] = c.DefaultValue,
        ["dn"] = c.Description,
    };

    private static object MakeIndex(IndexInfo i) => new Dictionary<string, object>
    {
        ["n"] = i.Name,
        ["u"] = i.IsUnique,
        ["pk"] = i.IsPrimaryKey,
        ["tp"] = i.Type,
        ["cols"] = i.Columns,
        ["inc"] = i.IncludedColumns,
        ["f"] = i.Filter,
    };

    private static object MakeForeignKey(ForeignKeyInfo fk) => new Dictionary<string, object>
    {
        ["n"] = fk.Name,
        ["rt"] = fk.ReferencedTable,
        ["cols"] = fk.Columns.Select(c => c.Column).ToList(),
        ["rcols"] = fk.Columns.Select(c => c.ReferencedColumn).ToList(),
        ["del"] = fk.OnDeleteAction,
        ["upd"] = fk.OnUpdateAction,
    };
}
