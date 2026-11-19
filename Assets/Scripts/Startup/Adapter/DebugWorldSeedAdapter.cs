using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>実際の設備定義を使い、検証用の配置を既存のロードへ渡す。</summary>
internal static class DebugWorldSeedAdapter
{
    internal const long StartingBalance = 10000;

    internal static SaveGameApplication Create(int size, CellPlacerAdapter definitions, ResourceTypeDomain deliverySample)
    {
        var prefabs = definitions.FieldPrefabs().ToDictionary(SaveWorldAdapter.Kind);
        var center = size / 2;
        var cells = new List<SaveGameApplication.Cell>();
        var occupied = new HashSet<(int x, int z)>();
        var used = new HashSet<CellTypeDomain>();
        var wood = ResourceTypeDomain.Wood;
        var stone = ResourceTypeDomain.Stone;

        SaveGameApplication.Cell Add(CellTypeDomain type, int x, int z, int turn = 0)
        {
            x += center;
            z += center;
            foreach (var point in prefabs[type].Footprint.Coordinates(x, z, turn))
            {
                if (point.x < 0 || point.x >= size || point.z < 0 || point.z >= size || !occupied.Add(point))
                    throw new InvalidOperationException($"Debug cell {type} does not fit at ({x}, {z}).");
            }
            var cell = new SaveGameApplication.Cell { kind = SaveKeysApplication.Key(type), x = x, z = z, rotation = turn,
                inventory = Array.Empty<SaveGameApplication.Slot>(), batches = Array.Empty<SaveGameApplication.Slot>(),
                exporter = type == CellTypeDomain.ExportConveyor };
            used.Add(type);
            cells.Add(cell);
            return cell;
        }

        void Storage(int x, int z, ResourceTypeDomain resource, int amount) =>
            Add(CellTypeDomain.Storage, x, z).inventory = new[] { SaveGameApplication.SlotValue(0, resource, amount) };

        void Route(CellTypeDomain type, int x, int z, DirectionsDomain inputs, DirectionsDomain outputs)
        {
            var cell = Add(type, x, z);
            cell.routeInputs = inputs;
            cell.routeOutputs = outputs;
        }

        // 特異点の東側から納品し、周囲の空きマスを閲覧と移動に使う。
        Add(CellTypeDomain.Singularity, 0, 0);
        Storage(6, 0, deliverySample, 9);
        for (var x = 5; x >= 3; x--) Add(CellTypeDomain.Conveyor, x, 0, 3);

        // 地形・採取設備・搬出路は品目ごとに一列で確認する。
        var natural = prefabs.Values.OfType<ResourceCellAdapter>().OrderBy(cell => (int)cell.ResourceTypeDomain).ToArray();
        for (var i = 0; i < natural.Length; i++)
        {
            var resource = natural[i].ResourceTypeDomain;
            var x = -12 + 3 * i;
            Add(SaveWorldAdapter.Kind(natural[i]), x, -13);
            var extractor = ResourceCellRulesDomain.ExtractorType(resource);
            if (extractor == CellTypeDomain.None) continue;
            Add(extractor, x, -14);
            Add(CellTypeDomain.ExportConveyor, x, -15, 2);
            Add(CellTypeDomain.Storage, x, -16);
        }

        void Craft(CellTypeDomain type, int x, int z, CellTypeDomain destination)
        {
            Add(type, x, z);
            var machine = (CrafterCellAdapter)prefabs[type];
            var inputs = machine.Ports.Where(port => (port.Use & CellPortUseDomain.Input) != 0).OrderBy(port => port.Id).ToArray();
            var recipe = machine.ConfiguredRecipes.Where(definition => definition != null)
                .Select(definition => definition.TryReadRecipe(out var value) ? value : null)
                .FirstOrDefault(value => value != null && value.Ingredients.Count == inputs.Length);
            if (recipe == null) throw new InvalidOperationException($"No debug recipe for {type}.");
            for (var i = 0; i < inputs.Length; i++)
            {
                var port = inputs[i];
                var ingredient = recipe.Ingredients[i];
                Storage(x + port.Cell.x, z + port.Cell.y - 2, ingredient.Type, ingredient.Amount);
                Add(CellTypeDomain.ExportConveyor, x + port.Cell.x, z + port.Cell.y - 1);
            }
            var output = machine.Ports.Single(port => (port.Use & CellPortUseDomain.Output) != 0);
            Add(CellTypeDomain.ExportConveyor, x + output.Cell.x, z + output.Cell.y + 1);
            Add(destination, x + output.Cell.x, z + output.Cell.y + 2);
        }

        Craft(CellTypeDomain.Crafter, -6, 2, CellTypeDomain.Shipping);
        Craft(CellTypeDomain.AdvancedCrafter, 0, 9, CellTypeDomain.Storage);

        // それぞれの入口に在庫を置き、ゲーム開始直後に各経路へ資源を流す。
        Route(CellTypeDomain.Crossing, -11, 9, DirectionsDomain.Back | DirectionsDomain.Left,
            DirectionsDomain.Forward | DirectionsDomain.Right);
        Storage(-11, 8, wood, 9);
        Storage(-12, 9, stone, 9);
        Add(CellTypeDomain.Storage, -11, 10);
        Add(CellTypeDomain.Storage, -10, 9);

        Storage(-11, 13, wood, 9);
        Add(CellTypeDomain.ExportConveyor, -11, 14);
        Add(CellTypeDomain.Crossing, -11, 15);
        Add(CellTypeDomain.Storage, -11, 16);

        Route(CellTypeDomain.Junction, 7, 8, DirectionsDomain.Back, DirectionsDomain.Forward | DirectionsDomain.Right);
        Storage(7, 7, wood, 9);
        Add(CellTypeDomain.Storage, 7, 9);
        Add(CellTypeDomain.Storage, 8, 8);

        Route(CellTypeDomain.Junction, 12, 8, DirectionsDomain.Back | DirectionsDomain.Left, DirectionsDomain.Forward);
        Storage(12, 7, wood, 4);
        Storage(11, 8, wood, 4);
        Add(CellTypeDomain.Storage, 12, 9);

        Route(CellTypeDomain.Junction, 7, 14, DirectionsDomain.Back,
            DirectionsDomain.Forward | DirectionsDomain.Left | DirectionsDomain.Right);
        Storage(7, 13, stone, 15);
        Add(CellTypeDomain.Storage, 6, 14);
        Add(CellTypeDomain.Storage, 8, 14);
        Add(CellTypeDomain.Storage, 7, 15);

        Storage(8, -6, ResourceTypeDomain.Plank, 6);
        Add(CellTypeDomain.ExportConveyor, 9, -6, 1);
        Add(CellTypeDomain.ConstructionDepot, 10, -6);

        // 未配置の定義もモデル閲覧に含める。既存の検証ラインからは間隔を空ける。
        var galleryIndex = 0;
        foreach (var type in prefabs.Keys.Where(type => type != CellTypeDomain.Empty && !used.Contains(type)).OrderBy(type => type).ToArray())
            Add(type, -12 + 4 * galleryIndex++, 20);

        for (var x = 0; x < size; x++)
            for (var z = 0; z < size; z++)
                if (!occupied.Contains((x, z))) Add(CellTypeDomain.Empty, x - center, z - center);

        var data = new SaveGameApplication
        {
            formatVersion = SaveGameApplication.CurrentFormat, revision = 1, savedUtc = "2000-01-01T00:00:00.0000000Z",
            size = size, seed = 0, balance = StartingBalance, cells = cells.ToArray(),
            construction = new[] { ResourceTypeDomain.Plank, ResourceTypeDomain.CopperWire, ResourceTypeDomain.IronIngot, ResourceTypeDomain.MachineParts }
                .Select(resource => new SaveGameApplication.Stock { resource = SaveKeysApplication.Key(resource), amount = 100 }).ToArray(),
            deliveries = Array.Empty<SaveGameApplication.Stock>(), skills = Array.Empty<string>(), skillRules = Array.Empty<string>(),
            definitions = used.OrderBy(type => type).Select(type => new SaveGameApplication.Definition
                { kind = SaveKeysApplication.Key(type), layout = SaveWorldAdapter.Layout(prefabs[type]) }).ToArray()
        };
        data.ValidateShape();
        return data;
    }
}
