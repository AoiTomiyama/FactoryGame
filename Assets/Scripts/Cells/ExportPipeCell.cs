using System.Collections;
using System.Linq;
using UnityEngine;

public class ExportPipeCell : ItemPipeCell, IExportable
{
    [SerializeField] private ExporterModule exportableModule;
    public ExporterModule ExportableModule => exportableModule;
    private StorageCell[] _storages = {};
    private bool _isActivate;

    protected override void Start()
    {
        ExportableModule.OnFilterPath += path => !_storages.Contains(path.Last());
        OnConnectionChanged += () => _storages = AdjacentCells.OfType<StorageCell>().ToArray();
        base.Start();
        _isActivate = true;
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ExportableModule)}がnullです。");
#endif
        }

        StartCoroutine(TakeResourcesFromAdjacentStorageEnumerator());
    }

    private IEnumerator TakeResourcesFromAdjacentStorageEnumerator()
    {
        if (ExportableModule == null)
        {
#if UNITY_EDITOR 
            Debug.LogWarning($"{nameof(ExportableModule)}がnullです。エクスポート処理を中断します。");
#endif
            yield break;
        }
        
        while (_isActivate)
        {
            // 周囲にストレージセルがあるかどうか
            yield return new WaitUntil(() => _storages.Length > 0 && ExportableModule.ExportPaths is { Count: > 0 });
        
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
                yield return new WaitUntil(() => ExportableModule.TryStackToExporter(takenAmount));
                yield return new WaitUntil(() => ExportableModule.ExportResourceAmount == 0);
                storageCell.TakeResource(takenAmount);
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }
        }
    }
}