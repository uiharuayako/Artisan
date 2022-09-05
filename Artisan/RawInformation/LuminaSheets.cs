using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Artisan.RawInformation
{
    public class LuminaSheets
    {

        public static Dictionary<uint, Recipe>? RecipeSheet = Service.DataManager?.GetExcelSheet<Recipe>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Action>? ActionSheet = Service.DataManager?.GetExcelSheet<Action>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftAction>? CraftActions = Service.DataManager?.GetExcelSheet<CraftAction>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftLevelDifference>? CraftLevelDifference = Service.DataManager?.GetExcelSheet<CraftLevelDifference>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, RecipeLevelTable>? RecipeLevelTableSheet = Service.DataManager?.GetExcelSheet<RecipeLevelTable>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ItemFood>? ItemFoodSheet = Service.DataManager?.GetExcelSheet<ItemFood>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Item>? ItemSheet = Service.DataManager?.GetExcelSheet<Item>()?
           .ToDictionary(i => i.RowId, i => i);
    }
}
