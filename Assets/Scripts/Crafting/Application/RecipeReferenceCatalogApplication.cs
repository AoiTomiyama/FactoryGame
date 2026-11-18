using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>成果物から、実設備の製法と出荷に使う売値を参照する。</summary>
public sealed class RecipeReferenceCatalogApplication
{
    public sealed class RecipeInfo
    {
        public string Name { get; }
        public float Seconds { get; }
        public RecipeRulesDomain.Recipe Recipe { get; }
        public RecipeInfo(string name, float seconds, RecipeRulesDomain.Recipe recipe)
        { Name = name; Seconds = seconds; Recipe = recipe; }
    }

    public readonly struct Equipment
    {
        public readonly CellTypeDomain Type;
        public readonly int InputPorts, InputCapacity, OutputCapacity;
        public readonly IEnumerable<RecipeInfo> Recipes;
        public Equipment(CellTypeDomain type, int inputPorts, int inputCapacity, int outputCapacity, IEnumerable<RecipeInfo> recipes)
        { Type = type; InputPorts = inputPorts; InputCapacity = inputCapacity; OutputCapacity = outputCapacity; Recipes = recipes; }
    }

    public readonly struct Machine
    {
        public readonly CellTypeDomain Type;
        public readonly int InputCapacityNeeded, OutputCapacityNeeded;
        public Machine(CellTypeDomain type, int inputCapacityNeeded, int outputCapacityNeeded)
        { Type = type; InputCapacityNeeded = inputCapacityNeeded; OutputCapacityNeeded = outputCapacityNeeded; }
    }

    public sealed class Method
    {
        public RecipeInfo Definition { get; }
        public IReadOnlyList<Machine> Machines { get; }
        internal Method(RecipeInfo definition, IReadOnlyList<Machine> machines)
        { Definition = definition; Machines = machines; }
    }

    private readonly Dictionary<ResourceTypeDomain, IReadOnlyList<Method>> _methods;
    private readonly CurrencyPriceCatalogDomain _prices;
    private readonly HashSet<ResourceTypeDomain> _registered;
    public IReadOnlyList<ResourceTypeDomain> Resources { get; }

    public RecipeReferenceCatalogApplication(IEnumerable<ResourceTypeDomain> resources,
        CurrencyPriceCatalogDomain prices, IEnumerable<Equipment> equipment)
    {
        Resources = Array.AsReadOnly(resources.Where(type => type != ResourceTypeDomain.None).Distinct().ToArray());
        _registered = Resources.ToHashSet();
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));

        var machinesByRecipe = new Dictionary<RecipeInfo, List<Machine>>();
        var ordered = new List<RecipeInfo>();
        var seen = new HashSet<RecipeInfo>();
        foreach (var machine in equipment)
        foreach (var info in machine.Recipes)
        {
            if (seen.Add(info)) ordered.Add(info);
            var recipe = info.Recipe;
            if (!_registered.Contains(recipe.ResultType) || recipe.Ingredients.Any(row => !_registered.Contains(row.Type)) ||
                !RecipeRulesDomain.CanUseInputPorts(recipe, machine.InputPorts)) continue;

            var inputNeeded = recipe.Ingredients.Max(row => row.Amount);
            var outputNeeded = recipe.ResultAmount;
            if (!machinesByRecipe.TryGetValue(info, out var matches))
                machinesByRecipe.Add(info, matches = new List<Machine>());
            var match = new Machine(machine.Type, inputNeeded > machine.InputCapacity ? inputNeeded : 0,
                outputNeeded > machine.OutputCapacity ? outputNeeded : 0);
            if (!matches.Contains(match)) matches.Add(match);
        }

        // 同じ設備定義を参照する製法はまとめ、初出順を維持する。
        _methods = ordered.Where(machinesByRecipe.ContainsKey).GroupBy(info => info.Recipe.ResultType)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Method>)Array.AsReadOnly(group
                .Select(info => new Method(info, machinesByRecipe[info].AsReadOnly())).ToArray()));
    }

    public IReadOnlyList<Method> MethodsFor(ResourceTypeDomain type)
        => _methods.TryGetValue(type, out var methods) ? methods : Array.Empty<Method>();
    public bool HasRecipe(ResourceTypeDomain type) => _methods.ContainsKey(type);
    public bool IsRegistered(ResourceTypeDomain type) => _registered.Contains(type);
    public bool TryGetUnitSalePrice(ResourceTypeDomain type, out long price) => _prices.TryGetSaleAmount(type, 1, out price);
}
