using System;
using System.Collections.Generic;

/// <summary>シーン・時間・UI に依存せず、レシピの対応付けと全量の素材消費を計算する。</summary>
public static class RecipeRulesDomain
{
    public readonly struct Ingredient
    {
        public ResourceTypeDomain Type { get; }
        public int Amount { get; }
        public Ingredient(ResourceTypeDomain type, int amount) { Type = type; Amount = amount; }
    }

    public sealed class Recipe
    {
        public IReadOnlyList<Ingredient> Ingredients { get; }
        public ResourceTypeDomain ResultType { get; }
        public int ResultAmount { get; }

        public Recipe(Ingredient[] ingredients, ResourceTypeDomain resultType, int resultAmount)
        {
            Ingredients = Array.AsReadOnly((Ingredient[])(ingredients ?? Array.Empty<Ingredient>()).Clone());
            ResultType = resultType;
            ResultAmount = resultAmount;
        }
    }

    public readonly struct Input
    {
        public int Id { get; }
        public ResourceTypeDomain Type { get; }
        public int Amount { get; }
        public int Reserved { get; }
        public Input(int id, ResourceTypeDomain type, int amount, int reserved = 0)
        { Id = id; Type = type; Amount = amount; Reserved = reserved; }
    }

    public readonly struct Output
    {
        public ResourceTypeDomain Type { get; }
        public int Amount { get; }
        public Output(ResourceTypeDomain type, int amount) { Type = type; Amount = amount; }
    }

    public sealed class Commit
    {
        public IReadOnlyList<Input> Inputs { get; }
        public Output Result { get; }
        internal Commit(Input[] inputs, Output result)
        { Inputs = Array.AsReadOnly(inputs); Result = result; }
    }

    public static bool TryCreatePlan(Recipe recipe, IReadOnlyList<Input> inputs, Output output,
        int outputCapacity, out RecipePlanDomain plan)
    {
        plan = null;
        if (recipe == null || recipe.Ingredients.Count == 0 || !ValidInputs(inputs) ||
            !CanProduce(output, outputCapacity, recipe.ResultType, recipe.ResultAmount)) return false;
        var order = new int[recipe.Ingredients.Count];
        for (var i = 0; i < order.Length; i++)
        {
            var ingredient = recipe.Ingredients[i];
            if (!ValidType(ingredient.Type) || ingredient.Amount <= 0) return false;
            order[i] = i;
        }
        if (order.Length > inputs.Count) return false;

        // 同種の入力は数量が大きい材料条件から割り当てる。最小の適合入力を使うことで、
        // 小さい条件が大きい入力を先取りして後の条件を不成立にすることを防ぐ。
        Array.Sort(order, (a, b) =>
        {
            var amountOrder = recipe.Ingredients[b].Amount.CompareTo(recipe.Ingredients[a].Amount);
            return amountOrder != 0 ? amountOrder : a.CompareTo(b);
        });
        var used = new bool[inputs.Count];
        var consumptions = new RecipePlanDomain.Consumption[order.Length];
        foreach (var ingredientIndex in order)
        {
            var ingredient = recipe.Ingredients[ingredientIndex];
            var selected = -1;
            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                if (used[i] || input.Type != ingredient.Type || input.Amount < ingredient.Amount) continue;
                if (selected < 0 || input.Amount < inputs[selected].Amount ||
                    (input.Amount == inputs[selected].Amount && input.Id < inputs[selected].Id)) selected = i;
            }
            if (selected < 0) return false;
            used[selected] = true;
            consumptions[ingredientIndex] = new RecipePlanDomain.Consumption(
                inputs[selected].Id, ingredient.Type, ingredient.Amount);
        }
        plan = new RecipePlanDomain(consumptions, recipe.ResultType, recipe.ResultAmount);
        return true;
    }

    /// <summary>同じ入力 ID を再確認し、全条件が成立した場合だけ反映用の数量を返す。</summary>
    public static bool TryApply(RecipePlanDomain plan, IReadOnlyList<Input> inputs, Output output,
        int outputCapacity, out Commit commit)
    {
        commit = null;
        if (plan == null || !ValidInputs(inputs) ||
            !CanProduce(output, outputCapacity, plan.ResultType, plan.ResultAmount)) return false;
        var next = new Input[inputs.Count];
        var indices = new Dictionary<int, int>();
        for (var i = 0; i < inputs.Count; i++) { next[i] = inputs[i]; indices.Add(inputs[i].Id, i); }
        foreach (var consumption in plan.Consumptions)
        {
            if (!indices.TryGetValue(consumption.InputId, out var i)) return false;
            var input = next[i];
            if (input.Type != consumption.Type || input.Amount < consumption.Amount) return false;
            // 搬入予約は保持する。元の入力は書き換えず、成功後に Adapter が一括反映する。
            next[i] = new Input(input.Id, input.Type, input.Amount - consumption.Amount, input.Reserved);
        }
        commit = new Commit(next, new Output(plan.ResultType, output.Amount + plan.ResultAmount));
        return true;
    }

    private static bool CanProduce(Output output, int capacity, ResourceTypeDomain type, int amount)
        => ValidType(type) && amount > 0 && capacity >= 0 && output.Amount >= 0 &&
           output.Amount <= capacity && amount <= capacity - output.Amount &&
           (output.Amount == 0 || (ValidType(output.Type) && output.Type == type));

    private static bool ValidType(ResourceTypeDomain type)
        => type != ResourceTypeDomain.None && Enum.IsDefined(typeof(ResourceTypeDomain), type);

    private static bool ValidInputs(IReadOnlyList<Input> inputs)
    {
        if (inputs == null) return false;
        var ids = new HashSet<int>();
        foreach (var input in inputs)
            if (!ids.Add(input.Id) || input.Amount < 0 || input.Reserved < 0 ||
                (!ValidType(input.Type) && !(input.Type == ResourceTypeDomain.None &&
                                            input.Amount == 0 && input.Reserved == 0))) return false;
        return true;
    }
}
