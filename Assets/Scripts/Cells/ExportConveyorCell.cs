using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ExportConveyorCell : ConveyorCell
{
    private IExportable _backwardCell;
    private ExportStatus _exportStatus;

    private enum ExportStatus
    {
        // 待機中
        Idle,
        // リソースを搬入中
        Taking,
        // リソース搬出待機中
        WaitingForTake,
        // リソース搬出可能か確認中
        CheckForTake
    }
    
    public override void InitializeSystem()
    {
        OnConnectionChanged += UpdateTransferTarget;
        base.InitializeSystem();
    }
    
    private void UpdateTransferTarget()
    {
        for (var i = 0; i < AdjacentCount; i++)
        {
            var cell = AdjacentCells[i];

            if (cell == null) continue;

            var dir = (cell.transform.position - transform.position).ToCardinalDirection();

            var back = DirectionEnumToVector(Directions.Back);

            if (dir == back && cell is IExportable exportable && _backwardCell == null)
            {
                _backwardCell = exportable;
                TakeResourceAsync(_cts.Token).Forget();
            }
        }
    }

    /// <summary>
    /// 後方のセルから状態を監視しつつリソースを取得する
    /// </summary>
    /// <param name="token">トークン</param>
    private async UniTask TakeResourceAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _exportStatus = ExportStatus.CheckForTake;
            // 後方のセルが存在しない場合、またはリソースを既に持っている場合は待機
            await UniTask.WaitUntil(() => _backwardCell != null && ResourcePrefab == null, cancellationToken: token);

            var amount = 0;
            var type = ResourceType.None;
            _exportStatus = ExportStatus.WaitingForTake;

            // リソースが取れるまで待機
            await UniTask.WaitUntil(
                () => _backwardCell.TryExport(transform.position, TransferAmount, out amount, out type),
                cancellationToken: token);
            HasResource = true;
            _exportStatus = ExportStatus.Taking;

            // 移動アニメーション
            var padding = Vector3.up * 1.1f;
            var startPos = _backwardCell.GetPosition() + padding;
            var endPos = transform.position + padding;

            // 取得したリソースを保存
            var prefab = ResourceItemObjectPool.Instance.GetPrefab(type);

            var textMesh = prefab.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null)
            {
                // Textが存在する場合、予約量を表示
                textMesh.text = amount.ToString();
            }

            await Transfer(token, startPos, endPos, prefab);
            UpdateResourceData(prefab, amount, type);
            _exportStatus = ExportStatus.Idle;

            // リソースの保存が完了したら、次のセルにリソースを送る
            StoreResourceAsync(token).Forget();
        }
    }

    protected override void OnDrawGizmos()
    {
        // ステータスに応じて色を変更
        Gizmos.color = _exportStatus switch
        {
            ExportStatus.Idle => Color.white,
            ExportStatus.Taking => Color.blue,
            ExportStatus.WaitingForTake => Color.magenta,
            ExportStatus.CheckForTake => Color.red,
            _ => Color.black
        };

        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, Vector3.one * 1f);
        
        if (_backwardCell != null)
        {
            Gizmos.color = Color.blue;
            var start = transform.position + Vector3.up * 1.5f;
            var end = start - transform.forward * 0.5f;
            Gizmos.DrawLine(start, end);
        }
    }
}
