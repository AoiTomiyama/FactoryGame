using System;
using System.Collections.Generic;

/// <summary>一件の加工計画を所有し、確定・取消を一度だけ受け付ける。</summary>
public sealed class CraftingOperationApplication
{
    public enum Stage { Processing, Completed, Cancelled }
    public RecipePlanDomain Plan { get; }
    public Stage CurrentStage { get; private set; } = Stage.Processing;

    public CraftingOperationApplication(RecipePlanDomain plan)
        => Plan = plan ?? throw new ArgumentNullException(nameof(plan));

    public bool TryComplete(IReadOnlyList<RecipeRulesDomain.Input> inputs, RecipeRulesDomain.Output output,
        int capacity, out RecipeRulesDomain.Commit commit)
    {
        commit = null;
        if (CurrentStage != Stage.Processing) return false;
        if (!RecipeRulesDomain.TryApply(Plan, inputs, output, capacity, out commit))
        {
            // 条件を失った計画を後から再確定しない。次の加工は新しい計画として選び直す。
            TryCancel();
            return false;
        }
        CurrentStage = Stage.Completed;
        return true;
    }

    public bool TryCancel()
    {
        if (CurrentStage != Stage.Processing) return false;
        CurrentStage = Stage.Cancelled;
        return true;
    }
}
