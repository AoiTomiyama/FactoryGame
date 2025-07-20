using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ExportPipeCell : ItemPipeCell, IExportable
{
    [SerializeField] private ExporterModule exportableModule;
    public ExporterModule ExportableModule => exportableModule;
    private StorageCell[] _storages = { };
    private bool _isActivate;
    private CancellationTokenSource _cts;

    public override void InitializeSystem()
    {
        ExportableModule.OnFilterPath += path => !_storages.Contains(path.Last());
        OnConnectionChanged += () => _storages = AdjacentCells.OfType<StorageCell>().ToArray();
        base.InitializeSystem();
        _isActivate = true;
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ExportableModule)}がnullです。");
#endif
        }

        _cts = new();
        TakeResourcesFromAdjacentStorageAsync(_cts.Token).Forget();
    }
    
    private void OnDestroy()
    {
        _isActivate = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async UniTask TakeResourcesFromAdjacentStorageAsync(CancellationToken token)
    {
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"{nameof(ExportableModule)}がnullです。エクスポート処理を中断します。");
#endif
            return;
        }

        while (_isActivate && !token.IsCancellationRequested)
        {
            // 周囲にストレージセルがあるかどうか
            await UniTask.WaitUntil(() => _storages.Length > 0 && ExportableModule.ExportPaths is { Count: > 0 },
                cancellationToken: token);

            var takenAmount = 0;
            StorageCell storageCell = null;

            if (_storages != null)
            {
                foreach (var cell in _storages)
                {
                    // 各ストレージからリソースの取得予約をする
                    takenAmount = cell.ReserveResource(ExportableModule.ExporterCapacity, out var type);

                    // 取得に失敗した場合、次のストレージへ
                    if (takenAmount <= 0) continue;

                    // 成功した場合、リソースタイプとストレージの座標を保存
                    ExportableModule.ExportResourceType = type;
                    ExportableModule.ExportBeginPos = cell.transform.position;
                    storageCell = cell;
                    break;
                }
            }

            if (takenAmount > 0 && storageCell != null)
            {
                // リソースの輸出
                await UniTask.WaitUntil(() => ExportableModule.TryStackToExporter(takenAmount),
                    cancellationToken: token);
                await UniTask.WaitUntil(() => ExportableModule.ExportResourceAmount == 0, cancellationToken: token);
                storageCell.TakeResource(takenAmount);
            }
            else
            {
                await UniTask.Yield(cancellationToken: token);
            }
        }
    }
}