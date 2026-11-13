using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>セルの所有状態と保存データを接続する。取得と検証ではプレイ状態を変更しない。</summary>
public sealed class SaveWorldAdapter
{
    private readonly GridFieldDatabaseAdapter _field;
    private readonly CellPlacerAdapter _definitions;
    public SaveWorldAdapter(GridFieldDatabaseAdapter field, CellPlacerAdapter definitions)
    { _field = field; _definitions = definitions; }

    public SaveGameApplication Capture(GameInitializerAdapter game, long revision)
    {
        if (game.Session.CurrentStage != GameStartApplication.Stage.Running || _definitions.IsPlacementTransactionActive)
            throw new InvalidOperationException("Save requires a running game outside a placement transaction.");
        var ids = new HashSet<int>();
        void Register(int id) { if (!ids.Add(id)) throw new InvalidOperationException("Resource batch has more than one saved owner."); }
        var layouts = new Dictionary<string,string>(StringComparer.Ordinal);
        var cells = new List<SaveGameApplication.Cell>();
        foreach (var cell in _field.Owners.OrderBy(c => c.XIndex).ThenBy(c => c.ZIndex))
        {
            var kind = SaveKeysApplication.Key(Kind(cell));
            var layout = Layout(cell);
            if (layouts.TryGetValue(kind,out var previous) && previous != layout) throw new InvalidOperationException("Cells of the same kind have different port layouts.");
            layouts[kind] = layout;
            var data = new SaveGameApplication.Cell {kind = kind, x = cell.XIndex, z = cell.ZIndex, rotation = (cell.QuarterTurns % 4 + 4) % 4,
                inventory = Array.Empty<SaveGameApplication.Slot>(), batches = Array.Empty<SaveGameApplication.Slot>()};
            switch (cell)
            {
                case StorageCellAdapter storage: data.inventory = storage.CaptureInventory(); break;
                case ExtractorCellAdapter extractor: data.inventory = extractor.CaptureInventory(); data.job = extractor.CaptureJob(); break;
                case CrafterCellAdapter crafter: data.inventory = crafter.CaptureInventory(); data.job = crafter.CaptureJob(); break;
                case ConveyorCellAdapter belt: data.exporter = belt.IsExportConveyor; data.batches = belt.CaptureBatches(Register); break;
                case CrossingCellAdapter crossing: data.batches = crossing.CaptureBatches(Register); break;
            }
            cells.Add(data);
        }
        if (ResourceItemObjectPoolAdapter.Instance.Resources.Count != ids.Count) throw new InvalidOperationException("A transit resource has no saved owner.");
        var skills = game.Skills.CaptureOwned();
        var result = new SaveGameApplication {formatVersion = 1, revision = revision, savedUtc = DateTime.UtcNow.ToString("O"),
            size = _field.GeneratedSize, seed = _field.GenerationSeed, balance = game.Wallet.Current.Balance,
            construction = SaveGameApplication.Stocks(game.ConstructionStock.CaptureStored()),
            deliveries = SaveGameApplication.Stocks(game.Deliveries.Current.Delivered), skills = skills,
            skillRules = skills.Select(id => { game.Skills.Catalog.TryGet(id,out var node); return SkillRule(node); }).ToArray(),
            definitions = layouts.Select(p => new SaveGameApplication.Definition {kind = p.Key,layout = p.Value}).ToArray(), cells = cells.ToArray()};
        result.ValidateShape(); return result;
    }

    public void Validate(SaveGameApplication data, SkillTreeCatalogDomain skills, IEnumerable<ResourceTypeDomain> registered)
    {
        data.ValidateShape();
        if (data.size != _field.GeneratedSize) throw new InvalidOperationException("Saved field dimensions differ from the scene grid.");
        var accepted = registered.ToHashSet();
        foreach (var stock in data.construction.Concat(data.deliveries)) CheckResource(stock.resource,accepted);
        for (var i = 0; i < data.skills.Length; i++)
            if (!skills.TryGet(data.skills[i],out var node) || data.skillRules[i] != SkillRule(node)) throw new InvalidOperationException("A saved skill definition has changed.");
        var effects = new SharedStatEffectsApplication();
        new SkillTreeApplication(skills,new CurrencyWalletApplication(data.balance),effects).RestoreOwned(data.skills);
        var layouts = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var definition in data.definitions)
            if (definition == null || string.IsNullOrEmpty(definition.layout) || !layouts.TryAdd(definition.kind,definition.layout))
                throw new InvalidOperationException("Invalid saved cell definitions.");
        var occupied = new Dictionary<(int,int),SaveGameApplication.Cell>();
        var kinds = new HashSet<string>(); var singularities = 0; var shippings = 0;
        foreach (var c in data.cells)
        {
            var kind = SaveKeysApplication.Cell(c.kind); var prefab = _definitions.FieldPrefab(kind);
            kinds.Add(c.kind);
            if (Kind(prefab) != kind || !layouts.TryGetValue(c.kind,out var layout) || layout != Layout(prefab))
                throw new InvalidOperationException($"Saved footprint or ports changed: {c.kind}");
            if (prefab is ConnectableCellBaseAdapter connectable) connectable.ValidatePorts();
            foreach (var p in prefab.Footprint.Coordinates(c.x,c.z,c.rotation))
                if (p.x < 0 || p.z < 0 || p.x >= data.size || p.z >= data.size || !occupied.TryAdd(p,c))
                    throw new InvalidOperationException("Saved cell occupancy is invalid.");
            foreach (var slot in c.inventory.Concat(c.batches).Concat(c.job?.inputs ?? Array.Empty<SaveGameApplication.Slot>())) CheckResource(slot.resource,accepted);
            if (c.job != null) CheckResource(c.job.result,accepted);
            if (prefab is not ConveyorCellAdapter && c.exporter || kind == CellTypeDomain.ExportConveyor && !c.exporter)
                throw new InvalidOperationException("Invalid saved export role.");
            if (prefab is not (ExtractorCellAdapter or CrafterCellAdapter) && c.job != null ||
                prefab is not (StorageCellAdapter or ExtractorCellAdapter or CrafterCellAdapter) && c.inventory.Length != 0 ||
                prefab is not (ConveyorCellAdapter or CrossingCellAdapter) && c.batches.Length != 0)
                throw new InvalidOperationException("Saved state does not belong to this cell kind.");
            switch (prefab)
            {
                case StorageCellAdapter s: s.ValidateSavedInventory(c.inventory,effects); break;
                case ExtractorCellAdapter e: e.ValidateSavedState(c,effects); break;
                case CrafterCellAdapter cr: cr.ValidateSavedState(c,effects); break;
                case ConveyorCellAdapter belt:
                    belt.ValidateSavedRole(c.exporter,effects);
                    if (c.batches.Length > 1 || c.batches.Length == 1 && c.batches[0].port != 0) throw new InvalidOperationException("Invalid saved conveyor slot.");
                    break;
                case CrossingCellAdapter crossing:
                    if (c.batches.Any(b => !crossing.HasPort(b.port,CellPortUseDomain.Input))) throw new InvalidOperationException("Invalid saved crossing port.");
                    break;
                case SingularityCellAdapter: singularities++; break;
                case ShippingCellAdapter: shippings++; break;
            }
        }
        if (occupied.Count != checked(data.size * data.size) || singularities != 1 || shippings < 1 || !kinds.SetEquals(layouts.Keys))
            throw new InvalidOperationException("Saved world is incomplete.");
        foreach (var c in data.cells.Where(c => c.job != null && ResourceCellRulesDomain.IsExtractor(SaveKeysApplication.Cell(c.kind))))
        {
            var direction = CellFootprintDomain.Rotate(0,1,c.rotation);
            if (!occupied.TryGetValue((c.x + direction.x,c.z + direction.z),out var source) ||
                _definitions.FieldPrefab(SaveKeysApplication.Cell(source.kind)) is not ResourceCellAdapter raw || SaveKeysApplication.Resource(c.job.result) != raw.ResourceTypeDomain)
                throw new InvalidOperationException("Saved extraction job has no matching resource cell.");
        }
    }
    private static void CheckResource(string key, HashSet<ResourceTypeDomain> accepted)
    { if (!accepted.Contains(SaveKeysApplication.Resource(key))) throw new InvalidOperationException($"Resource definition is missing: {key}"); }

    public CellBaseAdapter[] BuildCells(SaveGameApplication data)
    {
        // ロードは開始前だけ。元の地形を退役させ、保存された所有セルを一件ずつ構築する。
        foreach (var cell in _field.GetComponentsInChildren<CellBaseAdapter>()) { cell.gameObject.SetActive(false); Object.Destroy(cell.gameObject); }
        var root = new GameObject("RestoredCells"); root.transform.SetParent(_field.transform,false);
        var created = new CellBaseAdapter[data.cells.Length];
        for (var i = 0; i < data.cells.Length; i++)
        {
            var c = data.cells[i];
            var cell = Object.Instantiate(_definitions.FieldPrefab(SaveKeysApplication.Cell(c.kind)),new Vector3(c.x,0,c.z),Quaternion.Euler(0,c.rotation * 90,0),root.transform);
            if (cell is ConveyorCellAdapter belt) belt.RestoreExportRole(c.exporter);
            created[i] = cell;
        }
        _field.RecordGeneratedField(data.size,data.seed);
        _field.InitializeCells(data.size);
        return created;
    }
    public void RestoreCellStates(SaveGameApplication data, CellBaseAdapter[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
            switch (cells[i])
            {
                case StorageCellAdapter s: s.RestoreInventory(data.cells[i].inventory); break;
                case ExtractorCellAdapter e: e.RestoreState(data.cells[i]); break;
                case CrafterCellAdapter c: c.RestoreState(data.cells[i]); break;
                case ConveyorCellAdapter b: b.RestoreBatches(data.cells[i].batches); break;
                case CrossingCellAdapter cr: cr.RestoreBatches(data.cells[i].batches); break;
            }
    }
    public static CellTypeDomain Kind(CellBaseAdapter cell) => cell switch
    {
        EmptyCellAdapter => CellTypeDomain.Empty,
        ResourceCellAdapter r => r.ResourceTypeDomain switch
        {
            ResourceTypeDomain.Wood => CellTypeDomain.ResourceWood, ResourceTypeDomain.Stone => CellTypeDomain.ResourceStone,
            ResourceTypeDomain.Iron => CellTypeDomain.ResourceIron, ResourceTypeDomain.Copper => CellTypeDomain.ResourceCopper,
            ResourceTypeDomain.Coal => CellTypeDomain.ResourceCoal, _ => throw new InvalidOperationException("Unknown natural resource cell.")
        },
        ExportConveyorCellAdapter => CellTypeDomain.ExportConveyor, ConveyorCellAdapter => CellTypeDomain.Conveyor,
        CrossingCellAdapter => CellTypeDomain.Crossing, SingularityCellAdapter => CellTypeDomain.Singularity,
        ShippingCellAdapter => CellTypeDomain.Shipping, ConstructionDepotCellAdapter => CellTypeDomain.ConstructionDepot,
        _ when cell.SupportsProgression => cell.ProgressionType,
        _ => throw new InvalidOperationException($"Cell has no save definition: {cell.GetType().Name}")
    };
    private static string Layout(CellBaseAdapter cell)
    {
        var f = cell.Footprint;
        return $"{f.Width},{f.Depth},{f.OffsetX},{f.OffsetZ}|" + (cell is ConnectableCellBaseAdapter c ?
            string.Join(";",c.Ports.OrderBy(p => p.Id).Select(p => $"{p.Id},{p.Cell.x},{p.Cell.y},{p.Facing},{p.Use}")) : "");
    }
    private static string SkillRule(SkillNodeDomain node) => $"{node.Prerequisite}|{node.CellType}|{node.Stat}|{node.Modifier.Multiplier.ToString(CultureInfo.InvariantCulture)}|{node.Modifier.Addition}";
}
