using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ExportConveyorCell : ConveyorCell
{
    private IExportable _backwardCell;
    private CellBase _backwardCellBase;
    private bool _takeLoopStarted;
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
        OnLostConnectedCell += OnConnectionLost;
        base.InitializeSystem();
    }

    private void OnConnectionUpdated(Vector3Int dir, CellBase cell)
    {
        var back = DirectionEnumToVector(Directions.Back);
        if (dir == back && cell is IExportable exportable && _backwardCell == null)
        {
            _backwardCell = exportable;
            _backwardCellBase = cell;
            if (!_takeLoopStarted) TakeResourceAsync(_cts.Token).Forget();
        }
    }

    private void OnConnectionLost(CellBase cell)
    {
        if (cell != _backwardCellBase) return;
        _backwardCell = null;
        _backwardCellBase = null;
    }

    /// <summary>
    /// 後方のセルから状態を監視しつつリソースを取得する
    /// </summary>
    /// <param name="token">トークン</param>
    private async UniTask TakeResourceAsync(CancellationToken token)
    {
        _takeLoopStarted = true;
        try
        {
            while (!token.IsCancellationRequested)
            {
                _exportStatus = ExportStatus.CheckForTake;
                await UniTask.WaitUntil(() => _backwardCellBase != null && !HasResource && ResourceId == 0,
                    cancellationToken: token);

                var amount = 0;
                var type = ResourceType.None;
                _exportStatus = ExportStatus.WaitingForTake;

                await UniTask.WaitUntil(() => _backwardCellBase != null &&
                    _backwardCell.TryExport(transform.position, TransferAmount, out amount, out type),
                    cancellationToken: token);

                ResourceId = ResourceItemObjectPool.Instance.CreateIdFromResourceData(type, amount);
                HasResource = true;
                _exportStatus = ExportStatus.Taking;

                var padding = Vector3.up * 1.1f;
                var startPos = _backwardCell.GetPosition() + padding;
                var endPos = transform.position + padding;

                while (true)
                {
                    var result = await ResourceItemObjectPool.Instance.Transfer(token, startPos, endPos, ResourceId);
                    token.ThrowIfCancellationRequested();
                    if (result == ResourceAnimationResult.Completed) break;
                    // 表示を失っても搬出済みの資源は保持し、演出の正常完了まで送り出さない。
                    await UniTask.Delay(100, cancellationToken: token);
                }
                _exportStatus = ExportStatus.Idle;
                // 後方からの演出が終わるまで前方の搬送を開始しない。
                MarkResourceReady();
            }
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            // セル削除による終了。資源IDは基底クラスが返却する。
        }
        finally
        {
            _takeLoopStarted = false;
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
