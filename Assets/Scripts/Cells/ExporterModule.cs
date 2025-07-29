using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ExporterModule : MonoBehaviour
{
    [SerializeField] private int exporterCapacity;
    [SerializeField] private float exportIntervalSecond;

    private bool _isActivate;
    private CancellationTokenSource _cts;
    
    [Tooltip("輸送先の経路リスト")]
    public HashSet<List<ConnectableCellBase>> ExportPaths { get; set; } = new();
    public Func<List<ConnectableCellBase>, bool> OnFilterPath { get; set; }
    public Vector3 ExportBeginPos { get; set; }
    public ResourceType ExportResourceType { get; set; }
    public int ExportResourceAmount { get; private set; }
    public Action OnExport { get; set; }
    public int ExporterCapacity => exporterCapacity;

    private void OnEnable()
    {
        ExportBeginPos = transform.position;
        _isActivate = true;
        _cts = new();
        // _ = ExportAsync(_cts.Token);
    }

    private void OnDestroy()
    {
        _isActivate = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public bool TryStackToExporter(int amount)
    {
        if (amount <= 0) return false;
        var available = ExporterCapacity - ExportResourceAmount;
        if (amount > available) return false;

        ExportResourceAmount += amount;
        return true;
    }

    private async UniTask ExportAsync(CancellationToken token)
    {
        while (_isActivate)
        {
            // 容量上限に達した場合はスペースが空くまで待機
            await UniTask.WaitUntil(() => ExportResourceAmount < ExporterCapacity, cancellationToken: token);

            // 毎フレーム検索かけないように遅延を加える
            while (!TryExportResource())
            {
                await UniTask.Delay(TimeSpan.FromSeconds(exportIntervalSecond), cancellationToken: token);
            }
        }
    }

    public bool TryExport(out int amount, out ResourceType resourceType)
    {
        amount = 0;
        resourceType = ResourceType.None;
        if (ExportResourceAmount <= 0) return false;
        amount = ExportResourceAmount;
        resourceType = ExportResourceType;
        ExportResourceAmount = 0;
        return true;
    }

    private bool TryExportResource()
    {
        // ネットワークを介してターゲットにリソースを送る
        var isAllowedToTransfer = PipelineNetworkManager.Instance.TryExport(
            exporter: this,
            exportAmount: ExportResourceAmount,
            exportBeginPos: ExportBeginPos,
            allocated: out var allocatedAmount);

        // 輸出が確立されたら現在のリソース値から予約量を減らす
        if (isAllowedToTransfer)
        {
            ExportResourceAmount -= allocatedAmount;
            OnExport?.Invoke();
        }

        return isAllowedToTransfer;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        var startPadding = Vector3.up * 5f;
        foreach (var path in ExportPaths.Where(path => path is { Count: > 0 }))
        {
            // パスの先頭から終点までの線を描画

            var firstCell = transform.position + startPadding;
            foreach (var cell in path)
            {
                var nextPos = cell.transform.position + startPadding;
                Gizmos.DrawLine(firstCell, nextPos);
                firstCell = nextPos;
            }

            startPadding += Vector3.up * 0.2f;
        }
    }
}