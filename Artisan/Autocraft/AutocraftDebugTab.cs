using Artisan.CraftingLogic;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.Autocraft
{
    internal unsafe static class AutocraftDebugTab
    {
        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;
        internal static void Draw()
        {
            ImGui.Checkbox("Debug logging", ref Debug);
            if (ImGui.CollapsingHeader("所有能工巧匠食物"))
            {
                foreach (var x in ConsumableChecker.GetFood())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("背包内的HQ食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }
            if (ImGui.CollapsingHeader("所有能工巧匠药水"))
            {
                foreach (var x in ConsumableChecker.GetPots())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("背包内的药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("背包内的HQ药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }

            if (ImGui.CollapsingHeader("制作状态"))
            {
                ImGui.Text($"当前耐久: {CurrentCraft.CurrentDurability}");
                ImGui.Text($"最大耐久: {CurrentCraft.MaxDurability}");
                ImGui.Text($"当前进展: {CurrentCraft.CurrentProgress}");
                ImGui.Text($"最大进展: {CurrentCraft.MaxProgress}");
                ImGui.Text($"当前品质: {CurrentCraft.CurrentQuality}");
                ImGui.Text($"最大品质: {CurrentCraft.MaxQuality}");
                ImGui.Text($"物品名称: {CurrentCraft.ItemName}");
                ImGui.Text($"当前状态: {CurrentCraft.CurrentCondition}");
                ImGui.Text($"当前步骤: {CurrentCraft.CurrentStep}");
                ImGui.Text($"内静+贝尔格: {CurrentCraft.GreatStridesByregotCombo()}");
                ImGui.Text($"预期品质: {CurrentCraft.CalculateNewQuality(CurrentCraft.CurrentRecommendation)}");
            }
            ImGui.Separator();

            if (ImGui.Button("修复所有装备"))
            {
                RepairManager.ProcessRepair();
            }
            ImGuiEx.Text($"装备耐久: {RepairManager.GetMinEquippedPercent()}");
            ImGuiEx.Text($"选中的配方: {AgentRecipeNote.Instance()->SelectedRecipeIndex}");
            ImGuiEx.Text($"材料是否足够: {HQManager.InsufficientMaterials}");

            /*ImGui.InputInt("id", ref SelRecId);
            if (ImGui.Button("OpenRecipeByRecipeId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeId((uint)SelRecId);
            }
            if (ImGui.Button("OpenRecipeByItemId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByItemId((uint)SelRecId);
            }*/
            //ImGuiEx.Text($"Selected recipe id: {*(int*)(((IntPtr)AgentRecipeNote.Instance()) + 528)}");




        }
    }
}
