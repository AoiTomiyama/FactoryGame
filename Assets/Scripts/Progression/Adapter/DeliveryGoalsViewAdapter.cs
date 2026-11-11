using System;
using System.Linq;

/// <summary>納品の確定数を既存HUD向けに整形する。予約数は表示しない。</summary>
public sealed class DeliveryGoalsViewAdapter
{
    private readonly DeliveryGoalCatalogDomain _catalog;
    private readonly ResourceDefinition _resources;
    public DeliveryGoalsViewAdapter(DeliveryGoalCatalogDomain catalog, ResourceDefinition resources)
    { _catalog = catalog; _resources = resources; }
    private DeliveryGoalCatalogDomain.Stage Stage(DeliveryProgressApplication.Snapshot snapshot)
        => _catalog.Stages[Math.Min(snapshot.CompletedStages, _catalog.Stages.Count - 1)];
    public string Heading(DeliveryProgressApplication.Snapshot snapshot) => snapshot.IsComplete ? "目標達成" :
        $"段階 {snapshot.CompletedStages + 1}/{_catalog.Stages.Count}　{Stage(snapshot).Label}";
    private string Requirement(DeliveryGoalCatalogDomain.Requirement requirement, DeliveryProgressApplication.Snapshot snapshot)
    {
        var name = _resources.GetResourceByType(requirement.Resource).ResourceName;
        return $"{name}　{snapshot.Count(requirement.Resource)}/{requirement.Amount}";
    }
    public string[] Summary(DeliveryProgressApplication.Snapshot snapshot) => snapshot.IsComplete
        ? new[] { Heading(snapshot) }
        : new[] { Heading(snapshot) }.Concat(Stage(snapshot).Requirements.Select(r => Requirement(r, snapshot))).ToArray();
    public void RenderDetails(EquipmentDetailsViewAdapter view, DeliveryProgressApplication.Snapshot snapshot, bool active)
    {
        view.Heading.gameObject.SetActive(true);
        view.Heading.text = "特異点セル" + (active ? "" : "\n停止中");
        view.UpgradeRoot.gameObject.SetActive(true);
        view.UpgradeButton.gameObject.SetActive(false);
        view.UpgradeButton.interactable = false;
        view.Feedback = "";
        view.EquipmentText = Heading(snapshot) + "\n\n" + string.Join("\n",
            Stage(snapshot).Requirements.Select(r => Requirement(r, snapshot)));
        view.UpdateLayout();
    }
}
