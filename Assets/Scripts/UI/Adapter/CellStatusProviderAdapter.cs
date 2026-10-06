using System;
using System.Collections.Generic;

/// <summary>セルの読み取り専用プロパティを表示値へ変換する。予約・搬入・搬出操作は呼ばない。</summary>
public static class CellStatusProviderAdapter
{
    public static IUIDataProviderApplication Create(CellBaseAdapter cell, ProviderBaseDefinition definition)
    {
        if (definition == null) return null;
        var labels = new List<KeyValuePair<LabelEnumApplication, string>>();
        foreach (var entry in definition.GetLabels()) labels.Add(new(entry.Label, entry.Name));
        return new CellStatusDataProviderApplication(labels, label => Read(cell, definition, label));
    }

    private static UIElementDataBaseApplication Read(CellBaseAdapter cell, ProviderBaseDefinition definition,
        LabelEnumApplication label)
    {
        if (label == LabelEnumApplication.Location)
            return new TextElementDataApplication("", $"({cell.XIndex}, {cell.ZIndex})");
        if (label == LabelEnumApplication.CellName)
            return new TextElementDataApplication("", cell switch
            {
                StorageCellAdapter => "Storage", ResourceCellAdapter => "Resource",
                ExtractorCellAdapter => "Extractor", CrafterCellAdapter => "Crafter",
                _ => throw new ArgumentException("Unsupported cell status source.")
            });

        switch (cell)
        {
            case StorageCellAdapter storage:
                return label switch
                {
                    LabelEnumApplication.Amount => Stock(storage.StoredResourceType, storage.CurrentLoad, storage.Capacity),
                    LabelEnumApplication.Allocated => Gauge(storage.AllocatedAmount, storage.Capacity,
                        $"{storage.AllocatedAmount}/{storage.Capacity}"),
                    _ => throw Unsupported(label)
                };
            case ResourceCellAdapter resource when label == LabelEnumApplication.ResourceName:
                var database = ((ResourceProviderDefinition)definition).ResourceDatabase;
                return new TextElementDataApplication("", database == null ? "" :
                    database.GetResourceByType(resource.ResourceTypeDomain).ResourceName);
            case ExtractorCellAdapter extractor:
                return label switch
                {
                    LabelEnumApplication.Amount => Stock(extractor.ResourceTypeDomain, extractor.CurrentLoad, extractor.StorageCapacity),
                    LabelEnumApplication.Progress => Progress(extractor.ElapsedTime, extractor.ExtractionSecond),
                    _ => throw Unsupported(label)
                };
            case CrafterCellAdapter crafter:
                if (label == LabelEnumApplication.Progress) return Progress(crafter.ElapsedProcessTime, crafter.ProcessTime);
                if (label == LabelEnumApplication.OutputStorage)
                    return Stock(crafter.ExportResourceType, crafter.ExportStorageAmount, crafter.ExporterCapacity);
                if (label == LabelEnumApplication.LeftStorage || label == LabelEnumApplication.RightStorage)
                {
                    var input = crafter.GetInput(label == LabelEnumApplication.LeftStorage ? DirectionsDomain.Left : DirectionsDomain.Right);
                    return Stock(input.Type, input.Amount, crafter.IngredientCapacity);
                }
                throw Unsupported(label);
            default: throw Unsupported(label);
        }
    }

    private static StorageElementDataApplication Stock(ResourceTypeDomain type, int amount, int max)
        => new("", max) { Current = amount, GaugeText = $"{amount}/{max}", ResourceTypeDomain = type };
    private static GaugeElementDataApplication Gauge(float current, float max, string text)
        => new("", 1) { Current = current, Max = max, GaugeText = text };
    private static GaugeElementDataApplication Progress(float elapsed, float seconds)
        => Gauge(seconds > 0 ? elapsed / seconds : 0, 1, $"{Math.Max(0, seconds - elapsed):F1} sec");
    private static Exception Unsupported(LabelEnumApplication label)
        => new ArgumentException($"Unsupported cell status label: {label}");
}
