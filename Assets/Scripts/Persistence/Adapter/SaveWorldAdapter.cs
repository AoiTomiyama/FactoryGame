using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>セルの所有状態と保存データを接続する。取得と検証ではプレイ状態を変更しない。</summary>
public sealed class SaveWorldAdapter
{
    private readonly GridFieldDatabaseAdapter _field;
    private readonly CellPlacerAdapter _definitions;
    private Transform _restoredRoot;
    public SaveWorldAdapter(GridFieldDatabaseAdapter field, CellPlacerAdapter definitions)
    { _field = field; _definitions = definitions; }

    public SaveGameApplication Capture(GameInitializerAdapter game, long revision)
    {
        if (game.Session.CurrentStage != GameStartApplication.Stage.Running || _definitions.IsPlacementTransactionActive)
            throw new InvalidOperationException("Save requires a running game outside a placement transaction.");
        var ids = new HashSet<int>();
        void Register(int id) { if (!ids.Add(id)) throw new InvalidOperationException("Resource batch has more than one saved owner."); }
        var layouts = new Dictionary<string,(CellFootprintDomain Footprint,string Signature)>(StringComparer.Ordinal);
        var cells = new List<SaveGameApplication.Cell>();
        foreach (var cell in _field.Owners.OrderBy(c => c.XIndex).ThenBy(c => c.ZIndex))
        {
            var kind = SaveKeysApplication.Key(Kind(cell));
            var footprint = cell.Footprint;
            if (!layouts.TryGetValue(kind, out var previous))
                layouts.Add(kind, (footprint, Layout(cell)));
            else if (footprint.Width != previous.Footprint.Width || footprint.Depth != previous.Footprint.Depth ||
                     footprint.OffsetX != previous.Footprint.OffsetX || footprint.OffsetZ != previous.Footprint.OffsetZ ||
                     (cell is ConnectableCellBaseAdapter && Layout(cell) != previous.Signature))
                throw new InvalidOperationException("Cells of the same kind have different port layouts.");
            var data = new SaveGameApplication.Cell {kind = kind, x = cell.XIndex, z = cell.ZIndex, rotation = (cell.QuarterTurns % 4 + 4) % 4,
                inventory = Array.Empty<SaveGameApplication.Slot>(), batches = Array.Empty<SaveGameApplication.Slot>()};
            switch (cell)
            {
                case StorageCellAdapter storage: data.inventory = storage.CaptureInventory(); break;
                case ExtractorCellAdapter extractor: data.inventory = extractor.CaptureInventory(); data.job = extractor.CaptureJob(); break;
                case CrafterCellAdapter crafter: data.inventory = crafter.CaptureInventory(); data.job = crafter.CaptureJob(); break;
                case ConveyorCellAdapter belt: data.exporter = belt.IsExportConveyor; data.batches = belt.CaptureBatches(Register); break;
                case CrossingCellAdapter crossing:
                    data.routeInputs = crossing.LocalRoute.Inputs; data.routeOutputs = crossing.LocalRoute.Outputs;
                    data.batches = crossing.CaptureBatches(Register); break;
                case ConveyorJunctionCellAdapter junction:
                    data.routeInputs = junction.LocalRoute.Inputs; data.routeOutputs = junction.LocalRoute.Outputs;
                    data.routeInputCursor = junction.InputCursor; data.routeOutputCursor = junction.OutputCursor;
                    data.batches = junction.CaptureBatches(Register); break;
            }
            cells.Add(data);
        }
        if (ResourceItemObjectPoolAdapter.Instance.Resources.Count != ids.Count) throw new InvalidOperationException("A transit resource has no saved owner.");
        var skills = game.Skills.CaptureOwned();
        var result = new SaveGameApplication {formatVersion = SaveGameApplication.CurrentFormat, revision = revision, savedUtc = DateTime.UtcNow.ToString("O"),
            size = _field.GeneratedSize, seed = _field.GenerationSeed, balance = game.Wallet.Current.Balance,
            construction = SaveGameApplication.Stocks(game.ConstructionStock.CaptureStored()),
            deliveries = SaveGameApplication.Stocks(game.Deliveries.Current.Delivered), skills = skills,
            skillRules = skills.Select(id => { game.Skills.Catalog.TryGet(id,out var node); return SkillRule(node); }).ToArray(),
            definitions = layouts.Select(p => new SaveGameApplication.Definition {kind = p.Key,layout = p.Value.Signature}).ToArray(), cells = cells.ToArray()};
        return result;
    }

    public async UniTask<int[]> ValidateAsync(SaveGameApplication data, SkillTreeCatalogDomain skills,
        IEnumerable<ResourceTypeDomain> registered, DeliveryGoalCatalogDomain deliveryGoals, CancellationToken cancellation,
        bool includeOccupancy = false)
    {
        var budget = new SaveFrameBudgetAdapter(cancellation);
        if (data.size != _field.GeneratedSize) throw new InvalidOperationException("Saved field dimensions differ from the scene grid.");
        var accepted = registered.ToHashSet();
        foreach (var stock in data.construction.Concat(data.deliveries)) CheckResource(stock.resource,accepted);
        for (var i = 0; i < data.skills.Length; i++)
            if (!skills.TryGet(data.skills[i],out var node) || data.skillRules[i] != SkillRule(node)) throw new InvalidOperationException("A saved skill definition has changed.");
        var effects = new SharedStatEffectsApplication();
        var progress = new DeliveryProgressApplication(deliveryGoals);
        progress.RestoreDelivered(SaveGameApplication.StockValues(data.deliveries));
        new SkillTreeApplication(skills,new CurrencyWalletApplication(data.balance),effects,progress).RestoreOwned(data.skills);
        var prefabs = ValidateDefinitions(data.definitions);
        var kinds = new HashSet<string>(); var singularities = 0; var shippings = 0;
        using var occupied = new SaveOccupancyJobsAdapter(data.size, data.cells.Length);
        for (var i = 0; i < data.cells.Length; i++)
        {
            var cell = data.cells[i];
            if (!prefabs.TryGetValue(cell.kind, out var definition))
                throw new InvalidOperationException($"Missing saved cell definition: {cell.kind}");
            occupied.SetPlacement(i, cell, definition.Footprint);
            kinds.Add(cell.kind);
            await budget.YieldIfNeeded();
        }
        occupied.Schedule();
        foreach (var c in data.cells)
        {
            var prefab = prefabs[c.kind].Prefab;
            foreach (var slot in c.inventory) CheckResource(slot.resource, accepted);
            foreach (var slot in c.batches) CheckResource(slot.resource, accepted);
            if (c.job != null)
            {
                foreach (var slot in c.job.inputs) CheckResource(slot.resource, accepted);
                CheckResource(c.job.result, accepted);
            }
            if (prefab is not ConveyorCellAdapter && c.exporter)
                throw new InvalidOperationException("Invalid saved export role.");
            if (prefab is not (ExtractorCellAdapter or CrafterCellAdapter) && c.job != null ||
                prefab is not (StorageCellAdapter or ExtractorCellAdapter or CrafterCellAdapter) && c.inventory.Length != 0 ||
                prefab is not (ConveyorCellAdapter or CrossingCellAdapter or ConveyorJunctionCellAdapter) && c.batches.Length != 0)
                throw new InvalidOperationException("Saved state does not belong to this cell kind.");
            ValidateRoute(c, prefab);
            switch (prefab)
            {
                case StorageCellAdapter s: s.ValidateSavedInventory(c.inventory,effects); break;
                case ExtractorCellAdapter e: e.ValidateSavedState(c,effects); break;
                case CrafterCellAdapter cr: cr.ValidateSavedState(c,effects); break;
                case ConveyorCellAdapter belt:
                    belt.ValidateSavedRole(c.exporter,effects);
                    if (c.batches.Length > 1 || c.batches.Length == 1 && c.batches[0].port != 0) throw new InvalidOperationException("Invalid saved conveyor slot.");
                    break;
                case SingularityCellAdapter: singularities++; break;
                case ShippingCellAdapter: shippings++; break;
            }
            await budget.YieldIfNeeded();
        }
        await occupied.CompleteAsync(cancellation);
        if (singularities != 1 || shippings < 1 || kinds.Count != prefabs.Count)
            throw new InvalidOperationException("Saved world is incomplete.");
        foreach (var c in data.cells.Where(c => c.job != null && ResourceCellRulesDomain.IsExtractor(SaveKeysApplication.Cell(c.kind))))
        {
            var direction = CellFootprintDomain.Rotate(0,1,c.rotation);
            var source = occupied.OwnerIndex(c.x + direction.x, c.z + direction.z);
            if (source < 0 || prefabs[data.cells[source].kind].Prefab is not ResourceCellAdapter raw ||
                SaveKeysApplication.Resource(c.job.result) != raw.ResourceTypeDomain)
                throw new InvalidOperationException("Saved extraction job has no matching resource cell.");
        }
        return includeOccupancy ? occupied.CopyOwners() : null;
    }

    private Dictionary<string,(CellBaseAdapter Prefab, CellFootprintDomain Footprint)> ValidateDefinitions(
        IEnumerable<SaveGameApplication.Definition> definitions)
    {
        var prefabs = new Dictionary<string,(CellBaseAdapter,CellFootprintDomain)>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.kind) || string.IsNullOrEmpty(definition.layout) ||
                prefabs.ContainsKey(definition.kind))
                throw new InvalidOperationException("Invalid saved cell definitions.");
            var kind = SaveKeysApplication.Cell(definition.kind);
            var prefab = _definitions.FieldPrefab(kind);
            if (Kind(prefab) != kind || definition.layout != Layout(prefab))
                throw new InvalidOperationException($"Saved footprint or ports changed: {definition.kind}");
            if (prefab is ConnectableCellBaseAdapter connectable) connectable.ValidatePorts();
            prefabs.Add(definition.kind, (prefab, prefab.Footprint));
        }
        return prefabs;
    }
    private static void CheckResource(string key, HashSet<ResourceTypeDomain> accepted)
    { if (!accepted.Contains(SaveKeysApplication.Resource(key))) throw new InvalidOperationException($"Resource definition is missing: {key}"); }

    private static ConveyorRouteRulesDomain.Route SavedRoute(SaveGameApplication.Cell cell) => new(cell.routeInputs, cell.routeOutputs);

    private static void ValidateRoute(SaveGameApplication.Cell cell, CellBaseAdapter prefab)
    {
        var route = SavedRoute(cell);
        var hasRoute = (route.Inputs | route.Outputs) != 0;
        if (prefab is not (CrossingCellAdapter or ConveyorJunctionCellAdapter))
        {
            if (hasRoute || cell.routeInputCursor != 0 || cell.routeOutputCursor != 0)
                throw new InvalidOperationException("Saved route does not belong to this cell kind.");
            return;
        }
        var connectable = (ConnectableCellBaseAdapter)prefab;
        var inputs = connectable.Ports.Where(port => hasRoute ? (route.Inputs & port.Facing) != 0 :
            (port.Use & CellPortUseDomain.Input) != 0).Select(port => port.Id).ToHashSet();
        var outputCount = connectable.Ports.Count(port => (route.Outputs & port.Facing) != 0);
        var valid = prefab is CrossingCellAdapter
            ? (!hasRoute || route.Type == ConveyorRouteRulesDomain.Kind.Crossing) &&
                cell.routeInputCursor == 0 && cell.routeOutputCursor == 0
            : route.Type is ConveyorRouteRulesDomain.Kind.Split or ConveyorRouteRulesDomain.Kind.Merge &&
                cell.routeInputCursor >= 0 && cell.routeInputCursor < inputs.Count &&
                cell.routeOutputCursor >= 0 && cell.routeOutputCursor < outputCount;
        if (!valid || cell.batches.Any(batch => !inputs.Contains(batch.port)))
            throw new InvalidOperationException("Invalid saved conveyor route or input.");
    }

    internal async UniTask BuildInitialTerrainAsync(StartSiteRulesDomain.Terrain[,] terrain, int seed, CancellationToken cancellation)
    {
        var size = terrain.GetLength(0);
        var budget = new SaveFrameBudgetAdapter(cancellation);
        var cells = new SaveGameApplication.Cell[terrain.Length];
        var owners = new int[terrain.Length];
        for (var x = 0; x < size; x++)
        for (var z = 0; z < size; z++)
        {
            var kind = terrain[x, z].Empty ? CellTypeDomain.Empty : ResourceCellKind(terrain[x, z].Resource);
            var i = x * size + z;
            cells[i] = new SaveGameApplication.Cell { x = x, z = z, kind = SaveKeysApplication.Key(kind) };
            owners[i] = i;
            await budget.YieldIfNeeded();
        }
        await BuildCellsAsync(new SaveGameApplication { size = size, seed = seed, cells = cells }, owners, cancellation);
    }

    public async UniTask<CellBaseAdapter[]> BuildCellsAsync(SaveGameApplication data, int[] owners, CancellationToken cancellation)
    {
        var budget = new SaveFrameBudgetAdapter(cancellation, maxOperations: 64);
        var created = new CellBaseAdapter[data.cells.Length];
        // 同じ自然地形を使い回し、設備と変わったマスだけを入れ替える。
        foreach (var cell in _field.GetComponentsInChildren<CellBaseAdapter>())
        {
            var anchor = Vector3Int.RoundToInt(cell.transform.position);
            if (cell is (EmptyCellAdapter or ResourceCellAdapter) && anchor.x >= 0 && anchor.z >= 0 &&
                anchor.x < data.size && anchor.z < data.size)
            {
                var index = owners[anchor.x * data.size + anchor.z];
                var target = data.cells[index];
                if (created[index] == null && target.x == anchor.x && target.z == anchor.z &&
                    target.rotation == cell.QuarterTurns && target.kind == SaveKeysApplication.Key(Kind(cell)))
                {
                    created[index] = cell;
                    await budget.YieldIfNeeded(countOperation: false);
                    continue;
                }
            }
            if (cell is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
            cell.gameObject.SetActive(false);
            Object.Destroy(cell.gameObject);
            await budget.YieldIfNeeded();
        }
        // Destroy はフレーム末尾に実行されるため、旧設備の終了を先に済ませる。
        await UniTask.NextFrame(cancellationToken: cancellation);
        budget = new SaveFrameBudgetAdapter(cancellation, maxOperations: 64);
        if (_restoredRoot == null)
        {
            _restoredRoot = new GameObject("RestoredCells").transform;
            _restoredRoot.SetParent(_field.transform, false);
        }
        for (var i = 0; i < data.cells.Length; i++)
        {
            if (created[i] != null) continue;
            var c = data.cells[i];
            var cell = Object.Instantiate(_definitions.FieldPrefab(SaveKeysApplication.Cell(c.kind)),new Vector3(c.x,0,c.z),Quaternion.Euler(0,c.rotation * 90,0),_restoredRoot);
            if (cell is ConveyorCellAdapter belt) belt.RestoreExportRole(c.exporter);
            if (cell is CrossingCellAdapter crossing && c.routeInputs != 0) crossing.ConfigureRoute(SavedRoute(c));
            if (cell is ConveyorJunctionCellAdapter junction) junction.ConfigureRoute(SavedRoute(c), c.routeInputCursor, c.routeOutputCursor);
            created[i] = cell;
            await budget.YieldIfNeeded();
        }
        _field.RecordGeneratedField(data.size,data.seed);
        _field.InitializeRestoredOccupancy(data.size, owners, created);
        budget = new SaveFrameBudgetAdapter(cancellation);
        for (var i = 0; i < created.Length; i++)
        {
            var cell = created[i];
            cell.XIndex = data.cells[i].x;
            cell.ZIndex = data.cells[i].z;
            cell.ConfigureProgression(_field.StatEffects);
            await budget.YieldIfNeeded();
        }
        return created;
    }
    public async UniTask RestoreCellStatesAsync(SaveGameApplication data, CellBaseAdapter[] cells, CancellationToken cancellation)
    {
        var budget = new SaveFrameBudgetAdapter(cancellation);
        for (var i = 0; i < cells.Length; i++)
        {
            switch (cells[i])
            {
                case StorageCellAdapter s: s.RestoreInventory(data.cells[i].inventory); break;
                case ExtractorCellAdapter e: e.RestoreState(data.cells[i]); break;
                case CrafterCellAdapter c: c.RestoreState(data.cells[i]); break;
                case ConveyorCellAdapter b: b.RestoreBatches(data.cells[i].batches); break;
                case CrossingCellAdapter cr: cr.RestoreBatches(data.cells[i].batches); break;
                case ConveyorJunctionCellAdapter junction: junction.RestoreBatches(data.cells[i].batches); break;
            }
            await budget.YieldIfNeeded();
        }
    }
    public static CellTypeDomain Kind(CellBaseAdapter cell) => cell switch
    {
        EmptyCellAdapter => CellTypeDomain.Empty,
        ResourceCellAdapter r => ResourceCellKind(r.ResourceTypeDomain),
        ExportConveyorCellAdapter => CellTypeDomain.ExportConveyor, ConveyorCellAdapter => CellTypeDomain.Conveyor,
        CrossingCellAdapter => CellTypeDomain.Crossing, ConveyorJunctionCellAdapter => CellTypeDomain.Junction, SingularityCellAdapter => CellTypeDomain.Singularity,
        ShippingCellAdapter => CellTypeDomain.Shipping, ConstructionDepotCellAdapter => CellTypeDomain.ConstructionDepot,
        _ when cell.SupportsProgression => cell.ProgressionType,
        _ => throw new InvalidOperationException($"Cell has no save definition: {cell.GetType().Name}")
    };
    private static CellTypeDomain ResourceCellKind(ResourceTypeDomain resource) => resource switch
    {
        ResourceTypeDomain.Wood => CellTypeDomain.ResourceWood, ResourceTypeDomain.Stone => CellTypeDomain.ResourceStone,
        ResourceTypeDomain.Iron => CellTypeDomain.ResourceIron, ResourceTypeDomain.Copper => CellTypeDomain.ResourceCopper,
        ResourceTypeDomain.Coal => CellTypeDomain.ResourceCoal, _ => throw new InvalidOperationException("Unknown natural resource cell.")
    };
    internal static string Layout(CellBaseAdapter cell)
    {
        var f = cell.Footprint;
        return $"{f.Width},{f.Depth},{f.OffsetX},{f.OffsetZ}|" + (cell is ConnectableCellBaseAdapter c ?
            string.Join(";",c.Ports.OrderBy(p => p.Id).Select(p => $"{p.Id},{p.Cell.x},{p.Cell.y},{p.Facing},{p.Use}")) : "");
    }
    private static string SkillRule(SkillNodeDomain node) => $"{node.Prerequisite}|{string.Join(",",node.TargetCellTypes.OrderBy(t => t))}|{node.Stat}|{node.Modifier.Multiplier.ToString(CultureInfo.InvariantCulture)}|{node.Modifier.Addition}" +
        (node.RequiredDeliveryStage == null ? "" : $"|delivery:{node.RequiredDeliveryStage}");
}

internal sealed class SaveFrameBudgetAdapter
{
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly CancellationToken _cancellation;
    private readonly int _maxOperations;
    private int _operations;
    public SaveFrameBudgetAdapter(CancellationToken cancellation, int maxOperations = int.MaxValue)
    { _cancellation = cancellation; _maxOperations = maxOperations; }
    public UniTask YieldIfNeeded(bool countOperation = true)
    {
        if (countOperation) _operations++;
        return _operations < _maxOperations && _clock.ElapsedMilliseconds < 4 ? UniTask.CompletedTask : NextFrame();
    }

    private async UniTask NextFrame()
    {
        await UniTask.NextFrame(cancellationToken: _cancellation);
        _operations = 0;
        _clock.Restart();
    }
}
