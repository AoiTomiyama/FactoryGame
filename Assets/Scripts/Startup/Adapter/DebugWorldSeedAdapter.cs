using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>実際の設備定義を使い、検証用の配置を既存のロードへ渡す。</summary>
internal static class DebugWorldSeedAdapter
{
    internal const long StartingBalance = 10000;

    internal static SaveGameApplication Create(int size, CellPlacerAdapter definitions)
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

        void Route(CellTypeDomain type, int x, int z, DirectionsDomain inputs, DirectionsDomain outputs)
        {
            var cell = Add(type, x, z);
            cell.routeInputs = inputs;
            cell.routeOutputs = outputs;
        }

        void BeltLine(int x, int z, int turn, int length)
        {
            var (dx, dz) = CellFootprintDomain.Rotate(0, 1, turn);
            for (var i = 0; i < length; i++) Add(CellTypeDomain.Conveyor, x + i * dx, z + i * dz, turn);
        }

        var natural = prefabs.Values.OfType<ResourceCellAdapter>().ToDictionary(cell => cell.ResourceTypeDomain);
        void Harvest(ResourceTypeDomain resource, int x, int z, int facing, int shoulder)
        {
            var terrain = SaveWorldAdapter.Kind(natural[resource]);
            var (dx, dz) = CellFootprintDomain.Rotate(0, 1, facing);
            var deepZ = z + (dz == 0 ? -1 : dz);
            Add(terrain, x, z);
            Add(terrain, x + shoulder, z);
            for (var i = 0; i < 3; i++) Add(terrain, x + i * shoulder, deepZ);
            var extractor = ResourceCellRulesDomain.ExtractorType(resource);
            if (extractor == CellTypeDomain.None) return;
            Add(extractor, x - dx, z - dz, facing);
            Add(CellTypeDomain.ExportConveyor, x - 2 * dx, z - 2 * dz, (facing + 2) % 4);
        }

        // 木材と石材を平行に加工し、上位加工から出荷と建設資材庫へ分ける。
        Add(CellTypeDomain.Singularity, 0, 0);
        Add(CellTypeDomain.Shipping, 0, -3);
        foreach (var (resource, x) in new[] { (wood, -1), (stone, 1) })
        {
            Harvest(resource, x, -17, 2, x);
            BeltLine(x, -14, 0, 1);
            Add(CellTypeDomain.Crafter, x, -13);
            Add(CellTypeDomain.ExportConveyor, x, -12);
            Add(CellTypeDomain.Storage, x, -11);
            Add(CellTypeDomain.ExportConveyor, x, -10);
        }
        Add(CellTypeDomain.AdvancedCrafter, -1, -9);
        Add(CellTypeDomain.ExportConveyor, 0, -7);
        BeltLine(0, -6, 0, 1);
        Route(CellTypeDomain.Junction, 0, -5, DirectionsDomain.Back, DirectionsDomain.Forward | DirectionsDomain.Right);
        BeltLine(0, -4, 0, 1);
        BeltLine(1, -5, 1, 1);
        Add(CellTypeDomain.ConstructionDepot, 2, -5);

        // 両側の採取が続くため、後から見に来ても北側で合流を確認できる。
        Harvest(wood, 0, 11, 0, 1);
        BeltLine(0, 8, 2, 2);
        Harvest(wood, 6, 6, 1, 1);
        BeltLine(3, 6, 3, 3);
        Route(CellTypeDomain.Junction, 0, 6, DirectionsDomain.Forward | DirectionsDomain.Right, DirectionsDomain.Back);
        BeltLine(0, 5, 2, 3);

        // 西側で採取ラインを交差させ、縦の流れは手動交差を経由して三方向へ分ける。
        Harvest(wood, -8, -8, 2, -1);
        BeltLine(-8, -5, 0, 4);
        Harvest(stone, -15, -1, 3, -1);
        BeltLine(-12, -1, 1, 4);
        Route(CellTypeDomain.Crossing, -8, -1, DirectionsDomain.Back | DirectionsDomain.Left,
            DirectionsDomain.Forward | DirectionsDomain.Right);
        BeltLine(-7, -1, 1, 2);
        Add(CellTypeDomain.Storage, -5, -1);
        BeltLine(-8, 0, 0, 3);
        Add(CellTypeDomain.Crossing, -8, 3);
        BeltLine(-8, 4, 0, 2);
        Route(CellTypeDomain.Junction, -8, 6, DirectionsDomain.Back,
            DirectionsDomain.Forward | DirectionsDomain.Left | DirectionsDomain.Right);
        BeltLine(-8, 7, 0, 2);
        BeltLine(-9, 6, 3, 2);
        BeltLine(-7, 6, 1, 2);
        Add(CellTypeDomain.Storage, -8, 9);
        Add(CellTypeDomain.Storage, -11, 6);
        Add(CellTypeDomain.Storage, -5, 6);

        // 東側には残りの採取ラインを揃え、中央と各区画の間を歩けるようにする。
        var otherResources = natural.Keys.Where(resource => resource != wood && resource != stone).OrderBy(resource => resource).ToArray();
        for (var i = 0; i < otherResources.Length; i++)
        {
            var x = 6 + 4 * i;
            Harvest(otherResources[i], x, -13, 2, 1);
            BeltLine(x, -10, 0, 2);
            Add(CellTypeDomain.Storage, x, -8);
        }

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
