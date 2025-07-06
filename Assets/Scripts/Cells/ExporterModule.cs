using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExporterModule : MonoBehaviour
{
    [SerializeField] private int exporterCapacity;
    [SerializeField] private float exportIntervalSecond;

    [Tooltip("輸送先の経路リスト")]
    public HashSet<List<ConnectableCellBase>> ExportPaths { get; set; } = new();
    public Func<List<ConnectableCellBase>, bool> OnFilterPath { get; set; }
    public Vector3 ExportBeginPos { get; set; }
    public ResourceType ExportResourceType { get; set; }
    public int ExportResourceAmount { get; private set; }
    public Action OnExport { get; set; }
    private bool _isActivate;

    public int ExporterCapacity => exporterCapacity;

    private void Start()
    {
        ExportBeginPos = transform.position;
        _isActivate = true;
        StartCoroutine(ExportEnumerator());
    }

    public bool TryStackToExporter(int amount)
    {
        if (amount <= 0) return false;
        var available = ExporterCapacity - ExportResourceAmount;
        if (amount > available) return false;

        ExportResourceAmount += amount;
        return true;
    }

    private IEnumerator ExportEnumerator()
    {
        while (_isActivate)
        {
            // 容量上限に達した場合はスペースが空くまで待機
            yield return new WaitUntil(() => ExportResourceAmount < ExporterCapacity);

            // 毎フレーム検索かけないように遅延を加える
            while (!TryExportResource())
            {
                yield return new WaitForSeconds(exportIntervalSecond);
            }
        }
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
    
    private void OnDrawGizmos()
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