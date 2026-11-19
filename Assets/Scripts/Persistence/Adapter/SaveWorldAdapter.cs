using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>セルの所有状態と保存データを接続する。取得と検証ではプレイ状態を変更しない。</summary>
public sealed class SaveWorldAdapter
{
    private const int CloneBatchSize = 64;
    private const int MaxPendingCloneBatches = 3;
    private readonly GridFieldDatabaseAdapter _field;
    private readonly CellPlacerAdapter _definitions;
    private readonly Queue<GameObject> _retiredTerrain = new();
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
                    data.routeOutputCursor = junction.OutputCursor;
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
            if (hasRoute || cell.routeOutputCursor != 0)
                throw new InvalidOperationException("Saved route does not belong to this cell kind.");
            return;
        }
        var connectable = (ConnectableCellBaseAdapter)prefab;
        var inputs = connectable.Ports.Where(port => hasRoute ? (route.Inputs & port.Facing) != 0 :
            (port.Use & CellPortUseDomain.Input) != 0).Select(port => port.Id).ToHashSet();
        var outputCount = connectable.Ports.Count(port => (route.Outputs & port.Facing) != 0);
        var valid = prefab is CrossingCellAdapter
            ? (!hasRoute || route.Type == ConveyorRouteRulesDomain.Kind.Crossing) &&
                cell.routeOutputCursor == 0
            : route.Type == ConveyorRouteRulesDomain.Kind.Split &&
                cell.routeOutputCursor >= 0 && cell.routeOutputCursor < outputCount;
        if (!valid || cell.batches.Any(batch => !inputs.Contains(batch.port)))
            throw new InvalidOperationException("Invalid saved conveyor route or input.");
    }

    internal async UniTask BuildInitialTerrainAsync(StartSiteRulesDomain.Terrain[,] terrain, int seed, CancellationToken cancellation)
    {
        var (data, owners) = await Task.Run(() => PrepareInitialTerrain(terrain, seed, cancellation), cancellation);
        await BuildCellsAsync(data, owners, cancellation);
    }

    private static (SaveGameApplication data, int[] owners) PrepareInitialTerrain(
        StartSiteRulesDomain.Terrain[,] terrain, int seed, CancellationToken cancellation)
    {
        var size = terrain.GetLength(0);
        var cells = new SaveGameApplication.Cell[terrain.Length];
        var owners = new int[terrain.Length];
        for (var x = 0; x < size; x++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var z = 0; z < size; z++)
            {
                var kind = terrain[x, z].Empty ? CellTypeDomain.Empty : ResourceCellKind(terrain[x, z].Resource);
                var i = x * size + z;
                cells[i] = new SaveGameApplication.Cell { x = x, z = z, kind = SaveKeysApplication.Key(kind) };
                owners[i] = i;
            }
        }
        return (new SaveGameApplication { size = size, seed = seed, cells = cells }, owners);
    }

    public async UniTask<CellBaseAdapter[]> BuildCellsAsync(SaveGameApplication data, int[] owners, CancellationToken cancellation)
    {
        var previous = _field.GetComponentsInChildren<CellBaseAdapter>();
        var terrain = new (string Kind, int X, int Z, int Rotation)[previous.Length];
        var budget = new SaveFrameBudgetAdapter(cancellation);
        for (var i = 0; i < previous.Length; i++)
        {
            var cell = previous[i];
            if (!IsTerrain(cell)) continue;
            var anchor = Vector3Int.RoundToInt(cell.transform.position);
            terrain[i] = (SaveKeysApplication.Key(Kind(cell)), anchor.x, anchor.z, cell.QuarterTurns % 4);
            await budget.YieldIfNeeded();
        }

        // 座標と種類を写してから、差分と生成バッチを作る処理をワーカーへ渡す。
        var rotations = new[] { Quaternion.identity, Quaternion.Euler(0, 90, 0),
            Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 270, 0) };
        var (targets, batches) = await Task.Run(
            () => PrepareReplacement(data, owners, terrain, rotations, cancellation), cancellation);
        cancellation.ThrowIfCancellationRequested();
        var created = new CellBaseAdapter[data.cells.Length];
        budget = new SaveFrameBudgetAdapter(cancellation, maxOperations: 256, milliseconds: 8);
        for (var i = 0; i < previous.Length; i++)
        {
            var cell = previous[i];
            if (targets[i] >= 0)
            {
                created[targets[i]] = cell;
                continue;
            }
            if (cell is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
            var old = cell.gameObject;
            old.SetActive(false);
            if (IsTerrain(cell)) _retiredTerrain.Enqueue(old);
            else Object.Destroy(old);
            await budget.YieldIfNeeded();
        }
        // 搬送や生産を持つ旧設備の Destroy を済ませ、非アクティブな地形だけを破棄待ちに残す。
        await UniTask.NextFrame(cancellationToken: cancellation);
        if (_restoredRoot == null)
        {
            _restoredRoot = new GameObject("RestoredCells").transform;
            _restoredRoot.SetParent(_field.transform, false);
        }
        await CloneCellsAsync(batches, created, cancellation);
        budget = new SaveFrameBudgetAdapter(cancellation);
        for (var i = 0; i < data.cells.Length; i++)
        {
            var c = data.cells[i];
            var cell = created[i];
            if (cell is ConveyorCellAdapter belt) belt.RestoreExportRole(c.exporter);
            if (cell is CrossingCellAdapter crossing && c.routeInputs != 0) crossing.ConfigureRoute(SavedRoute(c));
            if (cell is ConveyorJunctionCellAdapter junction) junction.ConfigureRoute(SavedRoute(c), c.routeOutputCursor);
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

    private static bool IsTerrain(CellBaseAdapter cell) => cell is EmptyCellAdapter or ResourceCellAdapter;

    private sealed class CloneBatch
    {
        public CellTypeDomain Kind;
        public int[] Indices;
        public Vector3[] Positions;
        public Quaternion[] Rotations;
    }

    private static (int[] targets, CloneBatch[] batches) PrepareReplacement(
        SaveGameApplication data, int[] owners, (string Kind, int X, int Z, int Rotation)[] terrain,
        Quaternion[] rotations, CancellationToken cancellation)
    {
        var kept = new bool[data.cells.Length];
        var targets = new int[terrain.Length];
        Array.Fill(targets, -1);
        for (var i = 0; i < terrain.Length; i++)
        {
            var old = terrain[i];
            if (old.Kind == null || old.X < 0 || old.Z < 0 || old.X >= data.size || old.Z >= data.size) continue;
            var index = owners[old.X * data.size + old.Z];
            var next = data.cells[index];
            if (kept[index] || next.kind != old.Kind || next.x != old.X || next.z != old.Z || next.rotation != old.Rotation) continue;
            kept[index] = true;
            targets[i] = index;
        }

        var batches = new List<CloneBatch>();
        foreach (var group in Enumerable.Range(0, data.cells.Length).Where(i => !kept[i]).GroupBy(i => data.cells[i].kind))
        {
            cancellation.ThrowIfCancellationRequested();
            var kind = SaveKeysApplication.Cell(group.Key);
            var indices = group.ToArray();
            for (var start = 0; start < indices.Length; start += CloneBatchSize)
            {
                var count = Math.Min(CloneBatchSize, indices.Length - start);
                var batch = new CloneBatch
                {
                    Kind = kind, Indices = new int[count], Positions = new Vector3[count], Rotations = new Quaternion[count]
                };
                for (var i = 0; i < count; i++)
                {
                    var index = indices[start + i];
                    var cell = data.cells[index];
                    batch.Indices[i] = index;
                    batch.Positions[i] = new Vector3(cell.x, 0, cell.z);
                    batch.Rotations[i] = rotations[cell.rotation];
                }
                batches.Add(batch);
            }
        }
        return (targets, batches.ToArray());
    }

    private async UniTask CloneCellsAsync(CloneBatch[] batches, CellBaseAdapter[] created, CancellationToken cancellation)
    {
        var pending = new List<(CloneBatch Batch, AsyncInstantiateOperation<CellBaseAdapter> Operation)>();
        var cleanupTime = new System.Diagnostics.Stopwatch();
        var next = 0;
        try
        {
            while (next < batches.Length || pending.Count != 0 || _retiredTerrain.Count != 0)
            {
                cancellation.ThrowIfCancellationRequested();
                for (var i = pending.Count - 1; i >= 0; i--)
                {
                    var (batch, operation) = pending[i];
                    if (!operation.isDone) continue;
                    var results = operation.Result;
                    if (results == null || results.Length != batch.Indices.Length)
                        throw new InvalidOperationException("Cell instantiation did not return the requested objects.");
                    for (var j = 0; j < results.Length; j++) created[batch.Indices[j]] = results[j];
                    pending.RemoveAt(i);
                }
                // Awake がまとまって実行される場合も、一度に統合するセル数を小さく保つ。
                while (next < batches.Length && pending.Count < MaxPendingCloneBatches)
                {
                    var batch = batches[next++];
                    var prefab = _definitions.FieldPrefab(batch.Kind);
                    var operation = Object.InstantiateAsync(prefab, batch.Indices.Length, _restoredRoot,
                        batch.Positions, batch.Rotations, cancellation);
                    pending.Add((batch, operation));
                }
                cleanupTime.Restart();
                // Unity が生成を進める間に、停止済みの旧地形を同じフレームへ詰め込みすぎず破棄する。
                for (var retired = 0; retired < 160 && _retiredTerrain.Count != 0 && cleanupTime.ElapsedMilliseconds < 2; retired++)
                    Object.Destroy(_retiredTerrain.Dequeue());
                if (next < batches.Length || pending.Count != 0 || _retiredTerrain.Count != 0)
                    await UniTask.NextFrame(cancellationToken: cancellation);
            }
        }
        finally
        {
            foreach (var (_, operation) in pending)
                if (!operation.isDone) operation.Cancel();
        }
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
    private readonly int _maxOperations, _milliseconds;
    private int _operations;
    public SaveFrameBudgetAdapter(CancellationToken cancellation, int maxOperations = int.MaxValue, int milliseconds = 4)
    { _cancellation = cancellation; _maxOperations = maxOperations; _milliseconds = milliseconds; }
    public UniTask YieldIfNeeded()
    {
        return ++_operations < _maxOperations && _clock.ElapsedMilliseconds < _milliseconds ? UniTask.CompletedTask : NextFrame();
    }

    private async UniTask NextFrame()
    {
        await UniTask.NextFrame(cancellationToken: _cancellation);
        _operations = 0;
        _clock.Restart();
    }
}
