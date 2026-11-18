using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>保存された配置を展開し、検証済みの占有マスからセル番号を引く。</summary>
internal sealed class SaveOccupancyJobsAdapter : IDisposable
{
    private struct Placement
    {
        public int X, Z, Rotation;
        public CellFootprintDomain Footprint;
    }

    private struct Bounds
    {
        public int MinX, MinZ, MaxX, MaxZ;
    }

    [BurstCompile]
    private struct ExpandFootprintsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Placement> Placements;
        [WriteOnly] public NativeArray<Bounds> BoundsByCell;

        public void Execute(int index)
        {
            var cell = Placements[index];
            var footprint = cell.Footprint;
            var start = CellFootprintDomain.Rotate(footprint.OffsetX, footprint.OffsetZ, cell.Rotation);
            var end = CellFootprintDomain.Rotate(footprint.OffsetX + footprint.Width - 1,
                footprint.OffsetZ + footprint.Depth - 1, cell.Rotation);
            BoundsByCell[index] = new Bounds
            {
                MinX = cell.X + Math.Min(start.x, end.x), MaxX = cell.X + Math.Max(start.x, end.x),
                MinZ = cell.Z + Math.Min(start.z, end.z), MaxZ = cell.Z + Math.Max(start.z, end.z)
            };
        }
    }

    [BurstCompile]
    private struct MarkOccupancyJob : IJob
    {
        public int Size;
        [ReadOnly] public NativeArray<Bounds> BoundsByCell;
        public NativeArray<int> Owners;
        [WriteOnly] public NativeArray<byte> Error;

        public void Execute()
        {
            for (var i = 0; i < Owners.Length; i++) Owners[i] = -1;
            var filled = 0;
            // 重なりは元のセル順に判定し、各マスの書き込み先を一つのJobで所有する。
            for (var i = 0; i < BoundsByCell.Length; i++)
            {
                var bounds = BoundsByCell[i];
                if (bounds.MinX < 0 || bounds.MinZ < 0 || bounds.MaxX >= Size || bounds.MaxZ >= Size)
                {
                    Error[0] = 1;
                    return;
                }
                for (var x = bounds.MinX; x <= bounds.MaxX; x++)
                for (var z = bounds.MinZ; z <= bounds.MaxZ; z++)
                {
                    var point = x * Size + z;
                    if (Owners[point] != -1)
                    {
                        Error[0] = 1;
                        return;
                    }
                    Owners[point] = i;
                    filled++;
                }
            }
            Error[0] = filled == Owners.Length ? (byte)0 : (byte)2;
        }
    }

    private readonly int _size;
    private NativeArray<Placement> _placements;
    private NativeArray<Bounds> _bounds;
    private NativeArray<int> _owners;
    private NativeArray<byte> _error;
    private JobHandle _handle;

    public SaveOccupancyJobsAdapter(int size, int cells)
    {
        _size = size;
        _placements = new NativeArray<Placement>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _bounds = new NativeArray<Bounds>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _owners = new NativeArray<int>(checked(size * size), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _error = new NativeArray<byte>(1, Allocator.Persistent);
    }

    public void SetPlacement(int index, SaveGameApplication.Cell cell, CellFootprintDomain footprint)
        => _placements[index] = new Placement { X = cell.x, Z = cell.z, Rotation = cell.rotation, Footprint = footprint };

    public void Schedule()
    {
        _handle = new ExpandFootprintsJob { Placements = _placements, BoundsByCell = _bounds }.Schedule(_placements.Length, 64);
        _handle = new MarkOccupancyJob { Size = _size, BoundsByCell = _bounds, Owners = _owners, Error = _error }.Schedule(_handle);
        JobHandle.ScheduleBatchedJobs();
    }

    public async UniTask CompleteAsync(CancellationToken cancellation)
    {
        while (!_handle.IsCompleted)
            await UniTask.NextFrame(cancellationToken: cancellation);
        Complete();
    }

    public void Complete()
    {
        _handle.Complete();
        if (_error[0] == 1) throw new InvalidOperationException("Saved cell occupancy is invalid.");
        if (_error[0] == 2) throw new InvalidOperationException("Saved world is incomplete.");
    }

    public int OwnerIndex(int x, int z)
        => x < 0 || x >= _size || z < 0 || z >= _size ? -1 : _owners[x * _size + z];

    public int[] CopyOwners() => _owners.ToArray();

    public void Dispose()
    {
        // シーンが閉じられても投入済みのJobを先に完了させてから解放する。
        _handle.Complete();
        _placements.Dispose();
        _bounds.Dispose();
        _owners.Dispose();
        _error.Dispose();
    }
}
