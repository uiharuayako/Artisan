﻿using Artisan.Autocraft;
using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Artisan.CraftingLists
{
    internal class CraftingListUI
    {
        internal static Recipe? SelectedRecipe = null;
        internal static string Search = "";
        public unsafe static InventoryManager* invManager = InventoryManager.Instance();
        public static Dictionary<Recipe, bool> CraftableItems = new();
        internal static List<int> SelectedRecipeRawIngredients = new();
        internal static Dictionary<int, int> SelectedListMateralsNew = new();
        internal static Dictionary<int, bool> SelectedRecipesCraftable = new();
        internal static bool keyboardFocus = true;
        internal static string newListName = String.Empty;
        internal static CraftingList selectedList = new();
        public static Dictionary<uint, Recipe> FilteredList = LuminaSheets.RecipeSheet.Values
                    .DistinctBy(x => x.RowId)
                    .OrderBy(x => x.RecipeLevelTable.Value.ClassJobLevel)
                    .ThenBy(x => x.ItemResult.Value.Name.RawString)
                    .ToDictionary(x => x.RowId, x => x);

        internal static List<uint> jobs = new();
        internal static List<int> rawIngredientsList = new();
        internal static List<int> subtableList = new();
        internal static List<int> listMaterials = new();
        internal static Dictionary<int, int> listMaterialsNew = new();
        internal static uint selectedListItem;
        public static bool Processing = false;
        public static uint CurrentProcessedItem;
        private static bool renameMode = false;
        private static string? renameList;
        public static bool Minimized = false;
        private static int timesToAdd = 1;

        internal static void Draw()
        {
            string hoverText = "!! HOVER FOR INFORMATION !!";
            var hoverLength = ImGui.CalcTextSize(hoverText);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X / 2) - (hoverLength.Length() / 2));
            ImGui.TextColored(ImGuiColors.DalamudYellow, hoverText);
            if (ImGui.IsItemHovered())
            {
                ImGui.TextWrapped($"You can use this tab to see what items you can craft with the items in your inventory. You can also use it to create a quick crafting list that Artisan will try and work through.");
                ImGui.TextWrapped($"Please note that due to heavy computational requirements, filtering the recipe list to show only recipes you have ingredients for will not take into account raw ingredients for any crafted items. This may be addressed in the future. For now, it will only look at final ingredients *only* for a given recipe.");
                ImGui.TextWrapped($"Crafting lists process from top to bottom, so ensure any pre-requsite crafts come first.");
                ImGui.TextWrapped("Please ensure that you have gearsets saved for each job you have items to craft. Also if you require the usage of food and/or potions, please do so before starting the crafting list as this will not be automated.");
            }
            ImGui.Separator();

            if (Minimized)
            {
                if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowRight, "Maximize", new Vector2(80f, 0)))
                {
                    Minimized = false;
                }
                ImGui.Spacing();
            }
            
            DrawListOptions();
            ImGui.Spacing();
        }

        private static void DrawListOptions()
        {
            if (Handler.Enable)
            {
                Processing = false;
                ImGui.Text("Endurance mode enabled...");
                return;
            }
            if (Processing)
            {
                ImGui.Text("Currently processing list...");
                return;
            }

            if (!Minimized)
            {
                if (ImGui.Button("New List"))
                {
                    keyboardFocus = true;
                    ImGui.OpenPopup("NewCraftingList");
                }

                DrawNewListPopup();

                if (Service.Configuration.CraftingLists.Count > 0)
                {
                    ImGui.BeginGroup();
                    float longestName = 0;
                    foreach (var list in Service.Configuration.CraftingLists)
                    {
                        if (ImGui.CalcTextSize($"{list.Name}").Length() > longestName)
                            longestName = ImGui.CalcTextSize($"{list.Name}").Length();
                    }

                    longestName = Math.Max(150, longestName);
                    if (ImGui.BeginChild("###craftListSelector", new Vector2(longestName + 40, 0), true))
                    {
                        if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowLeft, "MinimizeButton", new Vector2(longestName + 20, 0)))
                        {
                            Minimized = true;
                        }

                        ImGui.Separator();
                        foreach (CraftingList l in Service.Configuration.CraftingLists)
                        {
                            var selected = ImGui.Selectable($"{l.Name}###list{l.ID}", l.ID == selectedList.ID);

                            if (selected)
                            {
                                selectedList = l;
                                SelectedListMateralsNew.Clear();
                                listMaterialsNew.Clear();
                            }
                        }

                        Teamcraft.DrawTeamCraftListButtons();

                        ImGui.EndChild();

                    }
                }
                else
                {
                    Teamcraft.DrawTeamCraftListButtons();
                }
            }

            if (selectedList.ID != 0)
            {
                if (!Minimized)
                ImGui.SameLine();
                if (ImGui.BeginChild("###selectedList", new Vector2(0, ImGui.GetContentRegionAvail().Y), false))
                {
                    if (!renameMode)
                    {
                        ImGui.Text($"Selected List: {selectedList.Name}");
                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                        {
                            renameMode = true;
                        }
                    }
                    else
                    {
                        renameList = selectedList.Name;
                        if (ImGui.InputText("", ref renameList, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            selectedList.Name = renameList;
                            Service.Configuration.Save();

                            renameMode = false;
                            renameList = String.Empty;
                        }
                    }
                    if (ImGui.Button("Delete List (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                    {
                        Service.Configuration.CraftingLists.Remove(selectedList);

                        Service.Configuration.Save();
                        selectedList = new();

                        SelectedListMateralsNew.Clear();
                        listMaterialsNew.Clear();
                    }

                    bool skipIfEnough = selectedList.SkipIfEnough;
                    if (ImGui.Checkbox("Skip items you already have enough of", ref skipIfEnough))
                    {
                        selectedList.SkipIfEnough = skipIfEnough;
                        Service.Configuration.Save();
                    }

                    bool materia = selectedList.Materia;
                    if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
                    {
                        selectedList.Materia = materia;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

                    bool repair = selectedList.Repair;
                    if (ImGui.Checkbox("Automatic Repairs", ref repair))
                    {
                        selectedList.Repair = repair;
                        Service.Configuration.Save();
                    }

                    ImGuiComponents.HelpMarker("If enabled, Artisan will automatically repair your gear using Dark Matter when any piece reaches the configured repair threshold.");
                    if (selectedList.Repair)
                    {
                        ImGui.PushItemWidth(200);
                        if (ImGui.SliderInt("##repairp", ref selectedList.RepairPercent, 10, 100, $"{selectedList.RepairPercent}%%"))
                        {
                            Service.Configuration.Save();
                        }
                    }

                    if (selectedList.Items.Count > 0)
                    {
                        if (ImGui.CollapsingHeader("List Items"))
                        {

                            ImGui.Columns(2, null, false);
                            ImGui.Text("Current Items");
                            ImGui.Indent();
                            var loop = 1;
                            foreach (var item in CollectionsMarshal.AsSpan(selectedList.Items.Distinct().ToList()))
                            {
                                var selected = ImGui.Selectable($"{loop}. {FilteredList[item].ItemResult.Value.Name.RawString} x{selectedList.Items.Count(x => x == item)} {(FilteredList[item].AmountResult > 1 ? $"({FilteredList[item].AmountResult * selectedList.Items.Count(x => x == item)} total)" : $"")}", selectedListItem == item);

                                if (selected)
                                {
                                    selectedListItem = item;
                                }

                                loop++;
                            }
                            ImGui.Unindent();
                            ImGui.NextColumn();
                            if (selectedListItem != 0)
                            {
                                ImGui.Text("Options");
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                                {
                                    selectedList.Items.RemoveAll(x => x == selectedListItem);
                                    selectedListItem = 0;
                                    Service.Configuration.Save();

                                    SelectedListMateralsNew.Clear();
                                    listMaterialsNew.Clear();
                                }
                                ImGui.SameLine();
                                var count = selectedList.Items.Count(x => x == selectedListItem);

                                ImGui.PushItemWidth(150);
                                if (ImGui.InputInt("Adjust quantity", ref count))
                                {
                                    if (count > 0)
                                    {
                                        var oldCount = selectedList.Items.Count(x => x == selectedListItem);
                                        if (oldCount < count)
                                        {
                                            var diff = count - oldCount;
                                            for (int i = 1; i <= diff; i++)
                                            {
                                                selectedList.Items.Insert(selectedList.Items.IndexOf(selectedListItem), selectedListItem);
                                            }
                                            Service.Configuration.Save();

                                            SelectedListMateralsNew.Clear();
                                            listMaterialsNew.Clear();
                                        }
                                        if (count < oldCount)
                                        {
                                            var diff = oldCount - count;
                                            for (int i = 1; i <= diff; i++)
                                            {
                                                selectedList.Items.Remove(selectedListItem);
                                            }
                                            Service.Configuration.Save();

                                            SelectedListMateralsNew.Clear();
                                            listMaterialsNew.Clear();
                                        }
                                    }
                                }


                                if (!selectedList.ListItemOptions.ContainsKey(selectedListItem))
                                {
                                    selectedList.ListItemOptions.TryAdd(selectedListItem, new ListItemOptions());
                                }
                                selectedList.ListItemOptions.TryGetValue(selectedListItem, out var options);
                                bool NQOnly = options.NQOnly;
                                if (ImGui.Checkbox("Quick Synthesis this item", ref NQOnly))
                                {
                                    options.NQOnly = NQOnly;
                                    Service.Configuration.Save();
                                }

                                string? preview = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var prevMacro) && prevMacro != null ? Service.Configuration.IndividualMacros[(uint)selectedListItem].Name : "";
                                if (prevMacro is not null && !Service.Configuration.UserMacros.Where(x => x.ID == prevMacro.ID).Any())
                                {
                                    preview = "";
                                    Service.Configuration.IndividualMacros[(uint)selectedListItem] = null;
                                    Service.Configuration.Save();
                                }

                                if (true)
                                {
                                    ImGui.Spacing();
                                    ImGui.TextWrapped($"Use a food item for this recipe");
                                    if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == options.Food, out var item) ? $"{(options.FoodHQ ? " " : "")}{item.Name}" : $"{(options.Food == 0 ? "Disabled" : $"{(options.FoodHQ ? " " : "")}{options.Food}")}"))
                                    {
                                        if (ImGui.Selectable("Disable"))
                                        {
                                            options.Food = 0;
                                        }
                                        foreach (var x in ConsumableChecker.GetFood(true))
                                        {
                                            if (ImGui.Selectable($"{x.Name}"))
                                            {
                                                options.Food = x.Id;
                                                options.FoodHQ = false;
                                            }
                                        }
                                        foreach (var x in ConsumableChecker.GetFood(true, true))
                                        {
                                            if (ImGui.Selectable($" {x.Name}"))
                                            {
                                                options.Food = x.Id;
                                                options.FoodHQ = true;
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }

                                if (true)
                                {
                                    ImGui.Spacing();
                                    ImGuiEx.SetNextItemFullWidth();
                                    ImGui.TextWrapped($"Use a potion item for this recipe");
                                    if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == options.Potion, out var item) ? $"{(options.PotHQ ? " " : "")}{item.Name}" : $"{(options.Potion == 0 ? "Disabled" : $"{(options.PotHQ ? " " : "")}{options.Potion}")}"))
                                    {
                                        if (ImGui.Selectable("Disable"))
                                        {
                                            options.Potion = 0;
                                        }
                                        foreach (var x in ConsumableChecker.GetPots(true))
                                        {
                                            if (ImGui.Selectable($"{x.Name}"))
                                            {
                                                options.Potion = x.Id;
                                                options.PotHQ = false;
                                            }
                                        }
                                        foreach (var x in ConsumableChecker.GetPots(true, true))
                                        {
                                            if (ImGui.Selectable($" {x.Name}"))
                                            {
                                                options.Potion = x.Id;
                                                options.PotHQ = true;
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }


                                if (Service.Configuration.UserMacros.Count > 0)
                                {
                                    ImGui.Spacing();
                                    ImGui.TextWrapped($"Use a macro for this recipe (only when Macro mode is enabled)");
                                    if (ImGui.BeginCombo("", preview))
                                    {
                                        if (ImGui.Selectable(""))
                                        {
                                            Service.Configuration.IndividualMacros[selectedListItem] = null;
                                            Service.Configuration.Save();
                                        }
                                        foreach (var macro in Service.Configuration.UserMacros)
                                        {
                                            bool selected = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var selectedMacro) && selectedMacro != null;
                                            if (ImGui.Selectable(macro.Name, selected))
                                            {
                                                Service.Configuration.IndividualMacros[(uint)selectedListItem] = macro;
                                                Service.Configuration.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }
                                ImGui.Spacing();


                                if (selectedList.Items.Distinct().Count() > 1)
                                {
                                    ImGui.Text("Re-order list");
                                    ImGui.SameLine();

                                    bool isFirstItem = selectedList.Items.IndexOf(selectedListItem) == 0;
                                    bool isLastItem = selectedList.Items.LastIndexOf(selectedListItem) == selectedList.Items.Count - 1;

                                    if (!isFirstItem)
                                    {
                                        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                                        {
                                            var loops = selectedList.Items.Count(x => x == selectedListItem);
                                            var previousNum = selectedList.Items[selectedList.Items.IndexOf(selectedListItem) - 1];
                                            var insertionIndex = selectedList.Items.IndexOf(previousNum);

                                            selectedList.Items.RemoveAll(x => x == selectedListItem);
                                            for (int i = 1; i <= loops; i++)
                                            {
                                                selectedList.Items.Insert(insertionIndex, selectedListItem);
                                            }

                                        }
                                        if (!isLastItem) ImGui.SameLine();
                                    }

                                    if (!isLastItem)
                                    {
                                        if (isFirstItem)
                                        {
                                            ImGui.Dummy(new Vector2(22));
                                            ImGui.SameLine();
                                        }

                                        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                                        {
                                            var nextNum = selectedList.Items[selectedList.Items.LastIndexOf(selectedListItem) + 1];
                                            var loops = selectedList.Items.Count(x => x == nextNum);
                                            var insertionIndex = selectedList.Items.IndexOf(selectedListItem);

                                            selectedList.Items.RemoveAll(x => x == nextNum);
                                            for (int i = 1; i <= loops; i++)
                                            {
                                                selectedList.Items.Insert(insertionIndex, nextNum);
                                            }

                                        }
                                    }
                                }

                            }
                        }
                        ImGui.Columns(1, null, false);
                        if (ImGui.CollapsingHeader("Total Ingredients"))
                        {
                            DrawTotalIngredientsTable();
                        }
                        if (ImGui.Button("Start Crafting List", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                        {
                            CraftingListFunctions.CurrentIndex = 0;
                            if (CraftingListFunctions.RecipeWindowOpen())
                                CraftingListFunctions.CloseCraftingMenu();

                            Processing = true;
                            Handler.Enable = false;
                        }
                    }
                    ImGui.Spacing();
                    ImGui.Separator();

                    DrawRecipeData();

                    ImGui.EndChild();
                }
            }


        }



        private static void DrawTotalIngredientsTable()
        {
            if (ImGui.BeginTable("###ListMaterialTableRaw", 3, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (SelectedListMateralsNew.Count == 0)
                {
                    foreach (var item in selectedList.Items.Distinct())
                    {
                        Recipe r = FilteredList[item];
                        AddRecipeIngredientsToList(r, ref SelectedListMateralsNew, false, selectedList);
                    }
                }

                if (listMaterialsNew.Count == 0)
                    listMaterialsNew = SelectedListMateralsNew;

                try
                {
                    foreach (var item in listMaterialsNew.OrderByDescending(x => x.Key))
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item.Key, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item.Key]) continue;
                            ImGui.PushID(item.Key);
                            var name = sheetItem.Name.RawString;
                            var count = item.Value;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{name}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{count}");
                            ImGui.TableNextColumn();
                            if (NumberOfIngredient((uint)item.Key) >= count)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.Text($"{NumberOfIngredient((uint)item.Key)}");
                            ImGui.PopID();
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }

            if (ImGui.BeginTable("###ListMaterialTableSub", 3, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (SelectedListMateralsNew.Count == 0)
                {
                    foreach (var item in selectedList.Items)
                    {
                        Recipe r = FilteredList[item];
                        AddRecipeIngredientsToList(r, ref SelectedListMateralsNew, false, selectedList);
                    }
                }

                if (listMaterialsNew.Count == 0)
                    listMaterialsNew = SelectedListMateralsNew;

                try
                {
                    foreach (var item in listMaterialsNew)
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item.Key, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item.Key])
                            {
                                ImGui.PushID(item.Key);
                                var name = sheetItem.Name.RawString;
                                var count = item.Value;
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text($"{name}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{count}");
                                ImGui.TableNextColumn();
                                if (NumberOfIngredient((uint)item.Key) >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                                ImGui.Text($"{NumberOfIngredient((uint)item.Key)}");
                                ImGui.PopID();
                            }
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }
        }

        private static void DrawNewListPopup()
        {
            if (ImGui.BeginPopup("NewCraftingList"))
            {
                if (keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    keyboardFocus = false;
                }

                if (ImGui.InputText("List Name###listName", ref newListName, 100, ImGuiInputTextFlags.EnterReturnsTrue) && newListName.Any())
                {
                    CraftingList newList = new();
                    newList.Name = newListName;
                    newList.SetID();
                    newList.Save(true);

                    newListName = String.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawRecipeData()
        {
            bool showOnlyCraftable = Service.Configuration.ShowOnlyCraftable;

            if (ImGui.Checkbox("Show only recipes you have materials for (toggle to refresh)", ref showOnlyCraftable))
            {
                Service.Configuration.ShowOnlyCraftable = showOnlyCraftable;
                Service.Configuration.Save();
                CraftableItems.Clear();
            }

            string preview = SelectedRecipe is null ? "" : $"{SelectedRecipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[SelectedRecipe.CraftType.Row + 8].Abbreviation.RawString})";
            if (ImGui.BeginCombo("Select Recipe", preview))
            {
                DrawRecipes();

                ImGui.EndCombo();
            }

            if (SelectedRecipe != null)
            {
                if (ImGui.CollapsingHeader("Recipe Information"))
                {
                    DrawRecipeOptions();
                }
                if (SelectedRecipeRawIngredients.Count == 0)
                    AddRecipeIngredientsToList(SelectedRecipe, ref SelectedRecipeRawIngredients);

                if (ImGui.CollapsingHeader("Raw Ingredients"))
                {
                    ImGui.Text($"Raw Ingredients Required");
                    DrawRecipeSubTable();

                }

                ImGui.Spacing();
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().Length() / 2f);
                ImGui.TextWrapped("Number of times to add");
                ImGui.SameLine();
                ImGui.InputInt("###TimesToAdd", ref timesToAdd, 1, 5);
                ImGui.PushItemWidth(-1f);

                if (ImGui.Button("Add to List", new Vector2(ImGui.GetContentRegionAvail().X / 2, 30)))
                {
                    SelectedListMateralsNew.Clear();
                    listMaterialsNew.Clear();

                    for (int i = 0; i < timesToAdd; i++)
                    {
                        if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(SelectedRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                        }
                    }
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Add to List (with all subcrafts)", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    SelectedListMateralsNew.Clear();
                    listMaterialsNew.Clear();

                    for (int p = 0; p < timesToAdd; p++) {
                        foreach (var subItem in SelectedRecipe.UnkData5)
                        {
                            var subRecipe = GetIngredientRecipe(subItem.ItemIngredient);
                            if (subRecipe.RowId != 0)
                            {
                                foreach (var subsubItem in subRecipe.UnkData5)
                                {
                                    var subsubRecipe = GetIngredientRecipe(subsubItem.ItemIngredient);
                                    if (subsubRecipe.RowId != 0)
                                    {
                                        for (int i = 1; i <= Math.Ceiling((double)subsubItem.AmountIngredient / (double)subsubRecipe.AmountResult); i++)
                                        {
                                            if (selectedList.Items.IndexOf(subsubRecipe.RowId) == -1)
                                            {
                                                selectedList.Items.Add(subsubRecipe.RowId);
                                            }
                                            else
                                            {
                                                var indexOfLast = selectedList.Items.IndexOf(subsubRecipe.RowId);
                                                selectedList.Items.Insert(indexOfLast, subsubRecipe.RowId);
                                            }

                                        }
                                    }
                                }
                                for (int i = 1; i <= Math.Ceiling((double)subItem.AmountIngredient / (double)subRecipe.AmountResult); i++)
                                {
                                    if (selectedList.Items.IndexOf(subRecipe.RowId) == -1)
                                    {
                                        selectedList.Items.Add(subRecipe.RowId);
                                    }
                                    else
                                    {
                                        var indexOfLast = selectedList.Items.IndexOf(subRecipe.RowId);
                                        selectedList.Items.Insert(indexOfLast, subRecipe.RowId);
                                    }
                                }
                            }
                        }

                        if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(SelectedRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                        }
                    }
                    Service.Configuration.Save();
                }
            }
        }

        private static void DrawRecipeSubTable()
        {
            if (ImGui.BeginTable("###SubTable", 3, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (subtableList.Count == 0)
                    subtableList = SelectedRecipeRawIngredients.Distinct().OrderByDescending(x => x).ToList();

                try
                {
                    foreach (var item in CollectionsMarshal.AsSpan(subtableList))
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item]) continue;
                            ImGui.PushID(item);
                            var name = sheetItem.Name.RawString;
                            var count = SelectedRecipeRawIngredients.Count(x => x == item);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{name}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{count}");
                            ImGui.TableNextColumn();
                            if (NumberOfIngredient((uint)item) >= count)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.Text($"{NumberOfIngredient((uint)item)}");
                            ImGui.PopID();
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }
        }

        private static void AddRecipeIngredientsToList(Recipe? recipe, ref Dictionary<int, int> ingredientList, bool addSublist = true, CraftingList selectedList = null)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    if (ingredientList.ContainsKey(ing.ItemIngredient))
                    {
                        ingredientList[ing.ItemIngredient] += ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId);
                    }
                    else
                    {
                        ingredientList.TryAdd(ing.ItemIngredient, ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId));
                    }

                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    if (GetIngredientRecipe(ing.ItemIngredient).RowId != 0 && addSublist)
                    {
                        AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                    }

                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "ERROR");
            }
        }
        private static void AddRecipeIngredientsToList(Recipe? recipe, ref List<int> ingredientList, bool addSubList = true, CraftingList selectedList = null)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    for (int i = 1; i <= ing.AmountIngredient; i++)
                    {
                        ingredientList.Add(ing.ItemIngredient);
                        if (GetIngredientRecipe(ing.ItemIngredient).RowId != 0 && addSubList)
                        {
                            AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "ERROR");
            }
        }

        private static void DrawRecipes()
        {
            ImGui.Text("Search");
            ImGui.SameLine();
            ImGui.InputText("###RecipeSearch", ref Search, 100);
            if (ImGui.Selectable("", SelectedRecipe == null))
            {
                SelectedRecipe = null;
            }

            if (Service.Configuration.ShowOnlyCraftable && CraftableItems.Count > 0)
            {
                foreach (var recipe in CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => x.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                {
                    ImGui.PushID((int)recipe.RowId);
                    var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                    if (selected)
                    {
                        subtableList.Clear();
                        SelectedRecipeRawIngredients.Clear();
                        SelectedRecipe = recipe;
                    }
                    ImGui.PopID();
                }
            }
            else
            {
                foreach (var recipe in CollectionsMarshal.AsSpan(FilteredList.Values.ToList()))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(recipe.ItemResult.Value.Name.RawString)) continue;
                        if (!recipe.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)) continue;
                        CheckForIngredients(recipe);
                        rawIngredientsList.Clear();
                        var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                        if (selected)
                        {
                            subtableList.Clear();
                            SelectedRecipeRawIngredients.Clear();
                            SelectedRecipe = recipe;
                        }

                    }
                    catch (Exception ex)
                    {
                        Dalamud.Logging.PluginLog.Error(ex, "DrawRecipeList");
                    }
                }
            }
        }

        public unsafe static bool CheckForIngredients(Recipe recipe, bool fetchFromCache = true)
        {
            if (fetchFromCache)
                if (CraftableItems.TryGetValue(recipe, out bool canCraft)) return canCraft;

            foreach (var value in recipe.UnkData5.Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0))
            {
                try
                {
                    int? invNumberNQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient);
                    int? invNumberHQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient, true);

                    if (value.AmountIngredient > (invNumberNQ + invNumberHQ))
                    {
                        invNumberHQ = null;
                        invNumberNQ = null;

                        CraftableItems[recipe] = false;
                        return false;
                    }

                    invNumberHQ = null;
                    invNumberNQ = null;
                }
                catch
                {

                }

            }

            CraftableItems[recipe] = true;
            return true;
        }

        private static bool HasRawIngredients(int itemIngredient, byte amountIngredient)
        {
            if (GetIngredientRecipe(itemIngredient).RowId == 0) return false;

            return CheckForIngredients(GetIngredientRecipe(itemIngredient));

        }

        public unsafe static int NumberOfIngredient(uint ingredient)
        {
            try
            {
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true);

                return invNumberHQ + invNumberNQ;
            }
            catch
            {
                return 0;
            }
        }
        private static void DrawRecipeOptions()
        {
            {
                List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == SelectedRecipe.ItemResult.Value.Name.RawString).Select(x => x.CraftType.Value.RowId + 8).ToList();
                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                ImGui.Text($"Crafted by: {String.Join(", ", jobstrings)}");
            }
            var ItemsRequired = SelectedRecipe.UnkData5;

            if (ImGui.BeginTable("###RecipeTable", 5, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Method", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();
                try
                {
                    foreach (var value in ItemsRequired.Where(x => x.AmountIngredient > 0))
                    {
                        jobs.Clear();
                        string ingredient = LuminaSheets.ItemSheet[(uint)value.ItemIngredient].Name.RawString;
                        Recipe? ingredientRecipe = GetIngredientRecipe(ingredient);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{ingredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{value.AmountIngredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{NumberOfIngredient((uint)value.ItemIngredient)}");
                        if (NumberOfIngredient((uint)value.ItemIngredient) >= value.AmountIngredient)
                        {
                            var color = ImGuiColors.HealerGreen;
                            color.W -= 0.3f;
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                        }
                        ImGui.TableNextColumn();
                        if (ingredientRecipe is not null)
                        {
                            if (ImGui.Button($"Crafted###search{ingredientRecipe.RowId}"))
                            {
                                SelectedRecipe = ingredientRecipe;
                            }
                        }
                        else
                        {
                            ImGui.Text("Gathered");
                        }
                        ImGui.TableNextColumn();
                        if (ingredientRecipe is not null)
                        {
                            try
                            {
                                jobs.AddRange(FilteredList.Values.Where(x => x.ItemResult.Value.Name.RawString == ingredient).Select(x => x.CraftType.Value.RowId + 8));
                                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => jobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                                ImGui.Text(String.Join(", ", jobstrings));
                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "JobStrings");
                            }

                        }
                        else
                        {
                            try
                            {
                                var gatheringItem = LuminaSheets.GatheringItemSheet?.Where(x => x.Value.Item == value.ItemIngredient).FirstOrDefault().Value;
                                if (gatheringItem != null)
                                {
                                    var jobs = LuminaSheets.GatheringPointBaseSheet?.Values.Where(x => x.Item.Any(y => y == gatheringItem.RowId)).Select(x => x.GatheringType).ToList();
                                    List<string> tempArray = new();
                                    if (jobs!.Any(x => x.Value.RowId is 0 or 1)) tempArray.Add(LuminaSheets.ClassJobSheet[16].Abbreviation.RawString);
                                    if (jobs!.Any(x => x.Value.RowId is 2 or 3)) tempArray.Add(LuminaSheets.ClassJobSheet[17].Abbreviation.RawString);
                                    if (jobs!.Any(x => x.Value.RowId is 4 or 5)) tempArray.Add(LuminaSheets.ClassJobSheet[18].Abbreviation.RawString);
                                    ImGui.Text($"{string.Join(", ", tempArray)}");
                                    continue;
                                }

                                var spearfish = LuminaSheets.SpearfishingItemSheet?.Where(x => x.Value.Item.Value.RowId == value.ItemIngredient).FirstOrDefault().Value;
                                if (spearfish != null && spearfish.Item.Value.Name.RawString == ingredient)
                                {
                                    ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    continue;
                                }

                                var fishSpot = LuminaSheets.FishParameterSheet?.Where(x => x.Value.Item == value.ItemIngredient).FirstOrDefault().Value;
                                if (fishSpot != null)
                                {
                                    ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    continue;
                                }


                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "JobStrings");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dalamud.Logging.PluginLog.Error(ex, "RecipeIngreds");
                }
                ImGui.EndTable();
            }

        }

        public unsafe static void DrawProcessingWindow()
        {
            if (Processing)
            {
                Service.Framework.RunOnFrameworkThread(() => CraftingListFunctions.ProcessList(selectedList));
                //ImGui.SetNextWindowSize(new Vector2(1000, 1000));
                ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("Processing List", ref Processing, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
                    {
                        Artisan.PluginUi.Visible = true;
                    }

                    ImGui.Text($"Now Processing: {selectedList.Name}");
                    ImGui.Separator();
                    ImGui.Spacing();
                    if (CurrentProcessedItem != 0)
                    {
                        ImGuiEx.TextV($"Trying to craft: {FilteredList[CurrentProcessedItem].ItemResult.Value.Name.RawString}");
                        ImGuiEx.TextV($"Overall Progress: {CraftingListFunctions.CurrentIndex + 1} / {selectedList.Items.Count}");
                    }

                    if (!CraftingListFunctions.Paused)
                    {
                        if (ImGui.Button("Pause"))
                        {
                            CraftingListFunctions.Paused = true;
                        }
                    }
                    else
                    {
                        if (ImGui.Button("Resume"))
                        {
                            CraftingListFunctions.OpenCraftingMenu();
                            CraftingListFunctions.Paused = false;
                        }
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        Processing = false;
                    }


                }
            }
        }

        public static Recipe? GetIngredientRecipe(string ingredient)
        {
            return FilteredList.Values.Any(x => x.ItemResult.Value.Name.RawString == ingredient) ? FilteredList.Values.First(x => x.ItemResult.Value.Name.RawString == ingredient) : null;
        }

        public static Recipe GetIngredientRecipe(int ingredient)
        {
            if (FilteredList.Values.Any(x => x.ItemResult.Value.RowId == ingredient))
                return FilteredList.Values.First(x => x.ItemResult.Value.RowId == ingredient);
            else
                return new Recipe() { RowId = 0 };
        }
    }
}
