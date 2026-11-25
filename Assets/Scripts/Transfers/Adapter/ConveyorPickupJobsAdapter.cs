using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>搬出候補の数値を一度に処理し、確定をメインスレッドへ戻す。</summary>
internal sealed class ConveyorPickupJobsAdapter : IDisposable
{
    internal readonly struct Request
    {
        public readonly int Stock;
        public readonly int Limit;
        public Request(int stock, int limit) { Stock = stock; Limit = limit; }
    }

    [BurstCompile]
    private struct CalculatePickupAmounts : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Request> Requests;
        [WriteOnly] public NativeArray<int> Amounts;

        public void Execute(int index)
        {
            var request = Requests[index];
            Amounts[index] = ResourceStorageRulesDomain.GetExportableAmount(request.Stock, request.Limit);
        }
    }

    private readonly List<ConveyorLanePickupAdapter> _queued = new();
    private readonly List<ConveyorLanePickupAdapter> _active = new();
    private NativeArray<Request> _requests;
    private NativeArray<int> _amounts;
    private JobHandle _job;
    private bool _running, _disposed;

    internal void Enqueue(ConveyorLanePickupAdapter lane)
    {
        if (_disposed || lane.IsQueued) return;
        lane.IsQueued = true;
        _queued.Add(lane);
        if (!_running) RunAsync().Forget();
    }

    private async UniTask RunAsync()
    {
        _running = true;
        try
        {
            while (!_disposed && _queued.Count != 0)
            {
                await UniTask.Yield(PlayerLoopTiming.LastUpdate);
                if (_disposed) return;
                Collect();
                if (_active.Count == 0) continue;

                _job = new CalculatePickupAmounts { Requests = _requests, Amounts = _amounts }.Schedule(_active.Count, 32);
                JobHandle.ScheduleBatchedJobs();
                await UniTask.Yield(PlayerLoopTiming.LastPreLateUpdate);
                while (!_disposed && !_job.IsCompleted) await UniTask.NextFrame(PlayerLoopTiming.LastPreLateUpdate);
                if (_disposed) return;
                _job.Complete();

                // 確定は投入順。前の確定で在庫や接続が変わっても各レーンが再確認する。
                for (var i = 0; i < _active.Count; i++)
                {
                    var lane = _active[i];
                    lane.IsQueued = false;
                    try { lane.Apply(_requests[i], _amounts[i]); }
                    catch (Exception error) { Debug.LogException(error); }
                }
                _active.Clear();
            }
        }
        finally { _running = false; }
    }

    private void Collect()
    {
        if (_requests.Length < _queued.Count)
        {
            if (_requests.IsCreated) _requests.Dispose();
            if (_amounts.IsCreated) _amounts.Dispose();
            var capacity = Mathf.NextPowerOfTwo(_queued.Count);
            _requests = new NativeArray<Request>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _amounts = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
        foreach (var lane in _queued)
        {
            try
            {
                if (lane.TryCapture(out var request))
                {
                    _requests[_active.Count] = request;
                    _active.Add(lane);
                    continue;
                }
            }
            catch (Exception error) { Debug.LogException(error); }
            lane.IsQueued = false;
        }
        _queued.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _job.Complete();
        if (_requests.IsCreated) _requests.Dispose();
        if (_amounts.IsCreated) _amounts.Dispose();
        _queued.Clear();
        _active.Clear();
    }
}
