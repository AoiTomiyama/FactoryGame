using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public sealed class ExtractorCell : ConnectableCellBase, IExportable, IUIRenderable
{
    [Header("抽出設定")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private float extractionSecond;
    [SerializeField] private int extractionAmount;

    [Header("その他設定")]
    [SerializeField] private ResourceSO resourceDatabase;
    [SerializeField] private ExporterModule exportableModule;
    [SerializeField] private List<LabelName> labelNames;

    public ExporterModule ExportableModule => exportableModule;
    private CellBase _forwardCell;
    private float _elapsedTime;
    private bool _isActivate;
    private readonly Dictionary<Label, UIStatusRowBase> _renderedUI = new();
    private Dictionary<Label, UIElementDataBase> _uiElementDataBases;

    public bool IsUIActive { get; set; }
    
    private enum Label
    {
        CellName,
        Location,
        Amount,
        Progress,
    }

    [Serializable]
    private struct LabelName
    {
        public Label label;
        public string name;
    }

    protected override void Start()
    {
        base.Start();
        _isActivate = true;
        SetDefaultUIData();

        _forwardCell = AdjacentCells
            .OfType<ResourceCell>()
            .FirstOrDefault(cell =>
                cell.XIndex == XIndex + Mathf.RoundToInt(transform.forward.x) &&
                cell.ZIndex == ZIndex + Mathf.RoundToInt(transform.forward.z) &&
                cell.ResourceType == resourceType);

        if (_forwardCell == null) return;
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ExportableModule)}がnullです。");
#endif
            return;
        }

        ExportableModule.ExportResourceType = resourceType;
        ExportableModule.OnExport += InitUI;
        StartCoroutine(ExtractFromForwardResourceEnumerator());
    }

    private void SetDefaultUIData()
    {
        _uiElementDataBases = new()
        {
            { Label.CellName, new TextElementData("-", "Extractor") },
            { Label.Location, new TextElementData("-", $"({XIndex}, {ZIndex})") },
            { Label.Amount, new StorageElementData("-", ExportableModule.ExporterCapacity) },
            { Label.Progress, new GaugeElementData("-", 1) },
        };
        
        var usedLabels = new HashSet<Label>();
        foreach (var labelName in labelNames)
        {
            if (!usedLabels.Add(labelName.label)) continue; // 重複ラベルはスキップ
            if (_uiElementDataBases.TryGetValue(labelName.label, out var data))
            {
                data.StatusName = labelName.name + ":";
            }
        }
    }

    public void ResetUI() => _renderedUI.Clear();

    public void InitUI()
    {
        if (!IsUIActive) return;

        foreach (var (label, data) in _uiElementDataBases)
        {
            if (data is GaugeElementData gaugeData)
            {
                gaugeData.Current = label switch
                {
                    Label.Amount => ExportableModule.ExportResourceAmount,
                    Label.Progress => _elapsedTime / extractionSecond,
                    _ => 0
                };
                gaugeData.GaugeText = label switch
                {
                    Label.Amount => $"{ExportableModule.ExportResourceAmount}/{ExportableModule.ExporterCapacity}",
                    Label.Progress => $"{extractionSecond - _elapsedTime:F1} sec",
                    _ => ""
                };
            }
            if (data is StorageElementData storageData)
            {
                storageData.ResourceType = resourceType;
            }

            if (_renderedUI.TryGetValue(label, out var uiElement))
            {
                uiElement.RenderUIByData(data);
            }
            else
            {
                _renderedUI[label] = CellStatusView.Instance.CreateStatusRow(data);
            }
        }
    }

    private IEnumerator ExtractFromForwardResourceEnumerator()
    {
        while (_isActivate)
        {
            // 容量に空きが出るまで待機
            yield return new WaitUntil(() => ExportableModule.ExportResourceAmount < ExportableModule.ExporterCapacity);

            var tween = DOTween.To(
                    () => _elapsedTime,
                    x => _elapsedTime = x,
                    extractionSecond,
                    extractionSecond)
                .OnUpdate(InitUI)
                .SetEase(Ease.Linear);

            // 抽出が終わるまで待機
            yield return tween.WaitForCompletion();
            _elapsedTime = 0;

            // 輸出モジュールにリソースを転送するまで待機
            var available = ExportableModule.ExporterCapacity - ExportableModule.ExportResourceAmount;
            var gainAmount = Mathf.Min(available, extractionAmount);

            yield return new WaitUntil(() => ExportableModule.TryStackToExporter(gainAmount));
            InitUI();
        }
    }
}