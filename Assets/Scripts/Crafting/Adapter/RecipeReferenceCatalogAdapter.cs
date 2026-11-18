using System.Collections.Generic;
using System.Linq;

/// <summary>配置に使う設備Prefabを、Unityに依存しない参照カタログへ写す。</summary>
public static class RecipeReferenceCatalogAdapter
{
    public static RecipeReferenceCatalogApplication Create(IEnumerable<CellBaseAdapter> prefabs,
        ResourceDefinition resources, CurrencyPriceCatalogDomain prices)
    {
        var converted = new Dictionary<RecipeDataDefinition, RecipeReferenceCatalogApplication.RecipeInfo>();
        IEnumerable<RecipeReferenceCatalogApplication.RecipeInfo> Recipes(CrafterCellAdapter crafter)
        {
            foreach (var definition in crafter.ConfiguredRecipes)
            {
                if (definition == null) continue;
                if (!converted.TryGetValue(definition, out var info))
                {
                    if (!definition.TryReadRecipe(out var recipe)) continue;
                    info = new RecipeReferenceCatalogApplication.RecipeInfo(definition.RecipeName, definition.CraftSecond, recipe);
                    converted.Add(definition, info);
                }
                yield return info;
            }
        }

        var equipment = prefabs.OfType<CrafterCellAdapter>().Select(crafter => new RecipeReferenceCatalogApplication.Equipment(
            crafter.ProgressionType, crafter.Ports.Count(port => (port.Use & CellPortUseDomain.Input) != 0),
            crafter.BaseIngredientCapacity, crafter.BaseExporterCapacity, Recipes(crafter)));
        return new RecipeReferenceCatalogApplication(resources.GetAllResources().Select(info => info.ResourceTypeDomain), prices, equipment);
    }
}
