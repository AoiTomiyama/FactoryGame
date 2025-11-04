using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ExportConveyorCell : ConveyorCell
{
    private IExportable _backwardCell;
    private ExportStatus _exportStatus;

    /// <summary>
    /// 現在の輸出ステータス
    /// デバッグ用に視覚的な非同期処理を確認するために設けている。
    /// </summary>
    private enum ExportStatus
    {
        // 待機中、または何もしていない
        Idle,
        
        // リソースを搬入中
        Taking,
        
        // リソース搬入待機中
        WaitingForTake,
        
        // リソース搬入可能か確認中
        CheckForTake
    }
    
    public override void InitializeSystem()
    {
        OnGetConnectedCell += OnConnectionUpdated;
        base.InitializeSystem();
    }

    private void OnConnectionUpdated(Vector3Int dir, CellBase cell)
    {
        var back = DirectionEnumToVector(Directions.Back);
        if (dir == back && cell is IExportable exportable && _backwardCell == null)
        {
            _backwardCell = exportable;
            TakeResourceAsync(_cts.Token).Forget();
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
            await UniTask.WaitUntil(() => _backwardCell != null && ResourceId == 0, cancellationToken: token);

            var amount = 0;
            var type = ResourceType.None;
            _exportStatus = ExportStatus.WaitingForTake;

            // リソースが取れるまで待機
            await UniTask.WaitUntil(
                () => _backwardCell.TryExport(transform.position, TransferAmount, out amount, out type),
                cancellationToken: token);

            ResourceId = ResourceItemObjectPool.Instance.CreateId(type, amount);
            HasResource = true;
            _exportStatus = ExportStatus.Taking;

            // 移動アニメーション
            var padding = Vector3.up * 1.1f;
            var startPos = _backwardCell.GetPosition() + padding;
            var endPos = transform.position + padding;

            await ResourceItemObjectPool.Instance.Transfer(token, startPos, endPos, ResourceId);
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
