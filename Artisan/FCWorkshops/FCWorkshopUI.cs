using Artisan.CraftingLists;
using Artisan.RawInformation;
using Dalamud.Logging;
using ECommons;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Numerics;

namespace Artisan.FCWorkshops
{
    internal static class FCWorkshopUI
    {
        internal static uint SelectedProject = 0;
        internal static CompanyCraftSequence CurrentProject => LuminaSheets.WorkshopSequenceSheet[SelectedProject];
        private static string Search = string.Empty;


        internal static void Draw()
        {
            ImGui.TextWrapped($"在此选项卡内, 你可以浏览游戏内所有的部队工房制作项目. " +
                $"此选项卡内被分为了3个部分. 第一部分用于总览制作项目的全貌. " +
                $"第二部分分解展示了制作项目内的每个部件. " +
                $"第三部分展示了每个阶段. " +
                $"在每个部分, 你可以点击里面的按钮创建一个附带了你制作该项目 " +
                $"所需要的所有材料的制作列表.");


            ImGui.Separator();
            string preview = SelectedProject != 0 ? LuminaSheets.ItemSheet[LuminaSheets.WorkshopSequenceSheet[SelectedProject].ResultItem.Row].Name.ExtractText() : "";
            if (ImGui.BeginCombo("###Workshop Project", preview))
            {
                ImGui.Text("搜索");
                ImGui.SameLine();
                ImGui.InputText("###ProjectSearch", ref Search, 100);

                if (ImGui.Selectable("", SelectedProject == 0))
                {
                    SelectedProject = 0;
                }

                foreach (var project in LuminaSheets.WorkshopSequenceSheet.Values.Where(x => x.RowId > 0).Where(x => x.ResultItem.Value.Name.ExtractText().Contains(Search, System.StringComparison.CurrentCultureIgnoreCase)))
                {
                    bool selected = ImGui.Selectable($"{project.ResultItem.Value.Name.ExtractText()}", project.RowId == SelectedProject);

                    if (selected)
                    {
                        SelectedProject = project.RowId;
                    }
                }

                ImGui.EndCombo();
            }

            if (SelectedProject != 0)
            {
                var project = LuminaSheets.WorkshopSequenceSheet[SelectedProject];

                if (ImGui.CollapsingHeader("项目信息"))
                {
                    if (ImGui.BeginTable($"FCWorkshopProjectContainer", 2, ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableSetupColumn($"###Description", ImGuiTableColumnFlags.WidthFixed);

                        ImGui.TableNextColumn();

                        ImGuiEx.Text($"选中的项目:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.ResultItem.Value.Name.ExtractText()}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"部件数量:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.CompanyCraftPart.Where(x => x.Row > 0).Count()}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"总阶段数量:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.CompanyCraftPart.Where(x => x.Row > 0).SelectMany(x => x.Value.CompanyCraftProcess).Where(x => x.Row > 0).Count()}");

                        ImGui.EndTable();
                    }
                    if (ImGui.BeginTable($"###FCWorkshopProjectItemsContainer", 2, ImGuiTableFlags.Borders))
                    {
                        ImGui.TableSetupColumn($"物品", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn($"总数量", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableHeadersRow();

                        foreach (var item in project.CompanyCraftPart.Where(x => x.Row > 0).SelectMany(x => x.Value.CompanyCraftProcess).Where(x => x.Row > 0).SelectMany(x => x.Value.UnkData0).Where(x => x.SupplyItem > 0).GroupBy(x => x.SupplyItem))
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Value.Name.ExtractText()}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{item.Sum(x => x.SetQuantity * x.SetsRequired)}");

                        }

                        ImGui.EndTable();
                    }
                    if (ImGui.Button($"创建此项目的制作列表", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                    {
                        CreateProjectList(project, false);
                        Notify.Success("部队工房列表已创建");
                    }

                    if (ImGui.Button($"创建此项目的制作列表 (包括前置配方)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                    {
                        CreateProjectList(project, true);
                        Notify.Success("部队工房列表已创建");
                    }
                }
                if (ImGui.CollapsingHeader("项目部件"))
                {
                    ImGui.Indent();
                    string partNum = "";
                    foreach (var part in project.CompanyCraftPart.Where(x => x.Row > 0).Select(x => x.Value))
                    {
                        partNum = part.CompanyCraftType.Value.Name.ExtractText();
                        if (ImGui.CollapsingHeader($"{partNum}"))
                        {
                            if (ImGui.BeginTable($"FCWorkshopPartsContainer###{part.RowId}", 2, ImGuiTableFlags.None))
                            {
                                ImGui.TableSetupColumn($"###PartType{part.RowId}", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"###Phases{part.RowId}", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableNextColumn();

                                ImGuiEx.Text($"部件类型:");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{part.CompanyCraftType.Value.Name.ExtractText()}");
                                ImGui.TableNextColumn();
                                ImGuiEx.Text($"阶段数量:");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{part.CompanyCraftProcess.Where(x => x.Row > 0).Count()}");
                                ImGui.TableNextColumn();

                                ImGui.EndTable();
                            }
                            if (ImGui.BeginTable($"###FCWorkshopPartItemsContainer{part.RowId}", 2, ImGuiTableFlags.Borders))
                            {
                                ImGui.TableSetupColumn($"物品", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"总数量", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableHeadersRow();

                                foreach (var item in part.CompanyCraftProcess.Where(x => x.Row > 0).SelectMany(x => x.Value.UnkData0).Where(x => x.SupplyItem > 0).GroupBy(x => x.SupplyItem))
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Value.Name.ExtractText()}");
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{item.Sum(x => x.SetQuantity * x.SetsRequired)}");

                                }

                                ImGui.EndTable();
                            }
                            if (ImGui.Button($"创建此部件的制作列表", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                            {
                                CreatePartList(part, partNum, false);
                                Notify.Success("部队工房列表已创建");
                            }

                            if (ImGui.Button($"创建此部件的制作列表 (包括前置配方)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                            {
                                CreatePartList(part, partNum, true);
                                Notify.Success("部队工房列表已创建");
                            }
                        }
                    }
                    ImGui.Unindent();
                }

                if (ImGui.CollapsingHeader("项目阶段"))
                {
                    string pNum = "";
                    foreach (var part in project.CompanyCraftPart.Where(x => x.Row > 0).Select(x => x.Value))
                    {
                        ImGui.Indent();
                        int phaseNum = 1;
                        pNum = part.CompanyCraftType.Value.Name.ExtractText();
                        foreach (var phase in part.CompanyCraftProcess.Where(x => x.Row > 0))
                        {
                            if (ImGui.CollapsingHeader($"{pNum} - 阶段 {phaseNum}"))
                            {
                                if (ImGui.BeginTable($"###FCWorkshopPhaseContainer{phase.Row}", 4, ImGuiTableFlags.Borders))
                                {
                                    ImGui.TableSetupColumn($"物品", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"每合集所需物品数量", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"合集数量", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"总数量", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableHeadersRow();

                                    foreach (var item in phase.Value.UnkData0.Where(x => x.SupplyItem > 0))
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Value.Name.ExtractText()}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetQuantity}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetsRequired}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetsRequired * item.SetQuantity}");

                                    }

                                    ImGui.EndTable();
                                }
                                if (ImGui.Button($"创建此阶段的制作列表###PhaseButton{phaseNum}", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                                {
                                    CreatePhaseList(phase.Value!, pNum, phaseNum, false);
                                    Notify.Success("部队工房列表已创建");
                                }

                                if (ImGui.Button($"创建此阶段的制作列表 (包括前置配方)###PhaseButtonPC{phaseNum}", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                                {
                                    CreatePhaseList(phase.Value!, pNum, phaseNum, true);
                                    Notify.Success("部队工房列表已创建");
                                }

                            }
                            phaseNum++;
                        }
                        ImGui.Unindent();
                    }

                }
            }
        }

        private static void CreatePartList(CompanyCraftPart value, string partNum, bool includePrecraft, CraftingList existingList = null)
        {
            if (existingList == null)
            {
                existingList = new CraftingList();
                existingList.Name = $"{CurrentProject.ResultItem.Value.Name.ExtractText()} - 部件 {partNum}";
                existingList.SetID();
                existingList.Save(true);
            }

            var phaseNum = 1;
            foreach (var phase in value.CompanyCraftProcess.Where(x => x.Row > 0))
            {
                CreatePhaseList(phase.Value!, partNum, phaseNum, includePrecraft, existingList);
                phaseNum++;
            }
        }

        private static void CreateProjectList(CompanyCraftSequence value, bool includePrecraft)
        {
            CraftingList existingList = new CraftingList();
            existingList.Name = $"{CurrentProject.ResultItem.Value.Name.ExtractText()}";
            existingList.SetID();
            existingList.Save(true);

            foreach (var part in value.CompanyCraftPart.Where(x => x.Row > 0))
            {
                string partNum = part.Value.CompanyCraftType.Value.Name.ExtractText();
                var phaseNum = 1;
                foreach (var phase in part.Value.CompanyCraftProcess.Where(x => x.Row > 0))
                {
                    CreatePhaseList(phase.Value!, partNum, phaseNum, includePrecraft, existingList);
                    phaseNum++;
                }
               
            }
            

        }

        public static void CreatePhaseList(CompanyCraftProcess value, string partNum, int phaseNum, bool includePrecraft, CraftingList existingList = null, CompanyCraftSequence projectOverride = null)
        {
            if (existingList == null)
            {
                existingList = new CraftingList();
                if (projectOverride != null)
                {
                    existingList.Name = $"{projectOverride.ResultItem.Value.Name.ExtractText()} - {partNum}, 阶段 {phaseNum}";
                }
                else
                {
                    existingList.Name = $"{CurrentProject.ResultItem.Value.Name.ExtractText()} - {partNum}, 阶段 {phaseNum}";
                }
                existingList.SetID();
                existingList.Save(true);
            }

            foreach (var item in value.UnkData0.Where(x => x.SupplyItem > 0))
            {
                var timesToAdd = item.SetsRequired * item.SetQuantity;
                var supplyItemID = LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Value.RowId;
                if (LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == supplyItemID))
                {
                    var recipeID = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == supplyItemID);
                    if (includePrecraft)
                    {
                        PluginLog.Debug($"I want to add {recipeID.ItemResult.Value.Name.RawString} {timesToAdd} times");
                        CraftingListUI.AddAllSubcrafts(recipeID, existingList, timesToAdd);
                    }

                    for (int i = 1; i <= timesToAdd / recipeID.AmountResult; i++)
                    {
                        if (existingList.Items.IndexOf(recipeID.RowId) == -1)
                        {
                            existingList.Items.Add(recipeID.RowId);
                        }
                        else
                        {
                            var indexOfLast = existingList.Items.IndexOf(recipeID.RowId);
                            existingList.Items.Insert(indexOfLast, recipeID.RowId);
                        }
                    }

                }
            }
            Service.Configuration.Save();

        }
    }
}
