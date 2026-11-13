using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>一回の同期取得で切り離す保存データ。Unity参照と未確定予約を含めない。</summary>
[Serializable]
public sealed class SaveGameApplication
{
    public int formatVersion;
    public long revision;
    public string savedUtc;
    public int size, seed;
    public long balance;
    public Stock[] construction, deliveries;
    public string[] skills;
    public string[] skillRules;
    public Definition[] definitions;
    public Cell[] cells;

    [Serializable] public sealed class Stock { public string resource; public long amount; }
    [Serializable] public sealed class Definition { public string kind, layout; }
    [Serializable] public sealed class Slot
    {
        public int port;
        public string resource;
        public int amount;
    }
    [Serializable] public sealed class Job
    {
        public float seconds, elapsed;
        public int amount;
        public string result;
        public Slot[] inputs;
    }
    [Serializable] public sealed class Cell
    {
        public string kind;
        public int x, z, rotation;
        public bool exporter;
        public Slot[] inventory, batches;
        // JsonUtilityのインラインクラスはnullを空の実体へ変えるため、任意の周期を0～1件の配列で表す。
        public Job[] jobs = Array.Empty<Job>();
        public Job job { get => jobs == null || jobs.Length == 0 ? null : jobs[0]; set => jobs = value == null ? Array.Empty<Job>() : new[] {value}; }
    }

    public void ValidateShape()
    {
        if (formatVersion != 1) throw new InvalidOperationException("Unsupported save format.");
        if (revision <= 0 || !DateTime.TryParse(savedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out _) ||
            size <= 0 || size > 512 || balance < 0 || cells == null || cells.Length == 0 ||
            cells.Length > checked(size * size) || construction == null || deliveries == null || skills == null ||
            skillRules == null || skillRules.Length != skills.Length || definitions == null)
            throw new InvalidOperationException("Invalid save header or missing data.");
        ValidateStocks(construction); ValidateStocks(deliveries);
        if (skills.Any(string.IsNullOrWhiteSpace) || skills.Distinct(StringComparer.Ordinal).Count() != skills.Length)
            throw new InvalidOperationException("Invalid saved skills.");
        var anchors = new HashSet<(int,int)>();
        foreach (var c in cells)
        {
            if (c == null || c.rotation < 0 || c.rotation > 3 || c.x < 0 || c.x >= size || c.z < 0 || c.z >= size ||
                !anchors.Add((c.x,c.z)) || c.inventory == null || c.batches == null || c.jobs == null || c.jobs.Length > 1 || c.jobs.Any(j => j == null))
                throw new InvalidOperationException("Invalid saved cell.");
            SaveKeysApplication.Cell(c.kind);
            ValidateSlots(c.inventory); ValidateSlots(c.batches);
            if (c.job == null) continue;
            var j = c.job;
            if (float.IsNaN(j.seconds) || float.IsInfinity(j.seconds) || j.seconds < 0 ||
                float.IsNaN(j.elapsed) || float.IsInfinity(j.elapsed) || j.elapsed < 0 || j.elapsed > j.seconds ||
                j.amount <= 0 || j.inputs == null)
                throw new InvalidOperationException("Invalid saved production job.");
            SaveKeysApplication.Resource(j.result); ValidateSlots(j.inputs);
        }
    }
    private static void ValidateStocks(Stock[] values)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in values)
        {
            if (s == null || s.amount <= 0 || !keys.Add(s.resource)) throw new InvalidOperationException("Invalid saved stock.");
            SaveKeysApplication.Resource(s.resource);
        }
    }
    private static void ValidateSlots(Slot[] values)
    {
        var ports = new HashSet<int>();
        foreach (var s in values)
        {
            if (s == null || s.port < 0 || s.amount <= 0 || !ports.Add(s.port)) throw new InvalidOperationException("Invalid saved slot.");
            SaveKeysApplication.Resource(s.resource);
        }
    }
    public static Stock[] Stocks(IEnumerable<KeyValuePair<ResourceTypeDomain,long>> values) => values
        .Where(p => p.Value != 0).OrderBy(p => p.Key).Select(p => new Stock {resource = SaveKeysApplication.Key(p.Key), amount = p.Value}).ToArray();
    public static Dictionary<ResourceTypeDomain,long> StockValues(Stock[] values) => values.ToDictionary(s => SaveKeysApplication.Resource(s.resource), s => s.amount);
    public static Slot SlotValue(int port, ResourceTypeDomain type, int amount) => new() {port = port, resource = SaveKeysApplication.Key(type), amount = amount};
}

/// <summary>保存形式のキーを固定する。Enumの順番や表示名をファイル契約にしない。</summary>
public static class SaveKeysApplication
{
    private static readonly (CellTypeDomain type,string key)[] Cells =
    {
        (CellTypeDomain.Empty,"empty"),(CellTypeDomain.ResourceWood,"wood-source"),(CellTypeDomain.ResourceStone,"stone-source"),
        (CellTypeDomain.ResourceIron,"iron-source"),(CellTypeDomain.ResourceCopper,"copper-source"),(CellTypeDomain.ResourceCoal,"coal-source"),
        (CellTypeDomain.ExtractorWood,"wood-extractor"),(CellTypeDomain.ExtractorStone,"stone-extractor"),(CellTypeDomain.ExtractorIron,"iron-extractor"),
        (CellTypeDomain.ExtractorCopper,"copper-extractor"),(CellTypeDomain.ExtractorCoal,"coal-extractor"),
        (CellTypeDomain.Storage,"storage"),(CellTypeDomain.Crafter,"crafter"),(CellTypeDomain.AdvancedCrafter,"advanced-crafter"),
        (CellTypeDomain.Conveyor,"conveyor"),(CellTypeDomain.ExportConveyor,"exporter"),(CellTypeDomain.Crossing,"crossing"),
        (CellTypeDomain.Singularity,"singularity"),(CellTypeDomain.Shipping,"shipping"),(CellTypeDomain.ConstructionDepot,"construction-depot")
    };
    private static readonly (ResourceTypeDomain type,string key)[] Resources =
    {
        (ResourceTypeDomain.Wood,"wood"),(ResourceTypeDomain.Stone,"stone"),(ResourceTypeDomain.Iron,"iron"),(ResourceTypeDomain.Gold,"gold"),
        (ResourceTypeDomain.Plank,"plank"),(ResourceTypeDomain.StoneBrick,"stone-brick"),(ResourceTypeDomain.BuildingParts,"building-parts"),
        (ResourceTypeDomain.Copper,"copper"),(ResourceTypeDomain.Coal,"coal"),(ResourceTypeDomain.IronIngot,"iron-ingot"),
        (ResourceTypeDomain.CopperIngot,"copper-ingot"),(ResourceTypeDomain.CopperWire,"copper-wire"),(ResourceTypeDomain.Steel,"steel"),
        (ResourceTypeDomain.MachineParts,"machine-parts")
    };
    public static string Key(CellTypeDomain type) => Cells.FirstOrDefault(p => p.type == type).key ?? throw new InvalidOperationException($"Unknown cell: {type}");
    public static string Key(ResourceTypeDomain type) => Resources.FirstOrDefault(p => p.type == type).key ?? throw new InvalidOperationException($"Unknown resource: {type}");
    public static CellTypeDomain Cell(string key) => Cells.FirstOrDefault(p => p.key == key && key != null).type is var t && t != CellTypeDomain.None ? t : throw new InvalidOperationException($"Unknown saved cell: {key}");
    public static ResourceTypeDomain Resource(string key) => Resources.FirstOrDefault(p => p.key == key && key != null).type is var t && t != ResourceTypeDomain.None ? t : throw new InvalidOperationException($"Unknown saved resource: {key}");
}
