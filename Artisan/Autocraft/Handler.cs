using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.RawInformation;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Components;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;
using PluginLog = Dalamud.Logging.PluginLog;

namespace Artisan.Autocraft
{
    internal unsafe class Handler
    {
        private static bool enable = false;
        internal static List<int>? HQData = null;
        internal static int RecipeID = 0;
        internal static string RecipeName { get => recipeName; set { if (value != recipeName) PluginLog.Verbose($"{value}"); recipeName = value; } }

        internal static bool Enable
        {
            get => enable; 
            set
            {
                Tasks.Clear();
                enable = value;
            }
        }

        internal static CircularBuffer<long> Errors = new(5);
        private static string recipeName = "";
        public static List<Task> Tasks = new();


        internal static void Init()
        {
            SignatureHelper.Initialise(new Handler());
            Svc.Framework.Update += Framework_Update;
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
        }

        private static void Toasts_ErrorToast(ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
        {
            if (Enable)
            {
                Errors.PushBack(Environment.TickCount64);
                if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000))
                {
                    //Svc.Chat.Print($"{Errors.Select(x => x.ToString()).Join(",")}");
                    DuoLog.Error("Endurance has been disabled due to too many errors in succession.");
                    Enable = false;
                }
            }
        }

        internal static void Dispose()
        {
            //BeginSynthesisHook?.Disable();
            //BeginSynthesisHook?.Dispose();
            Svc.Framework.Update -= Framework_Update;
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
        }

        private static void Framework_Update(Dalamud.Game.Framework framework)
        {
            if (Enable)
            {
                var isCrafting = Service.Condition[ConditionFlag.Crafting];
                var preparing = Service.Condition[ConditionFlag.PreparingToCraft];

                if (!Throttler.Throttle(0))
                {
                    return;
                }
                if (Service.Configuration.CraftingX && Service.Configuration.CraftX == 0)
                {
                    Enable = false;
                    Service.Configuration.CraftingX = false;
                    DuoLog.Information("Craft X has completed.");
                    return;
                }
                if (Svc.Condition[ConditionFlag.Occupied39])
                {
                    Throttler.Rethrottle(1000);
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Throttle success");
                if (HQData == null)
                {
                    DuoLog.Error("HQ data is null");
                    Enable = false;
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ not null");
                if (Service.Configuration.Materia && Spiritbond.IsSpiritbondReadyAny())
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose("Entered materia extraction");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    if (!Spiritbond.IsMateriaMenuOpen() && !isCrafting && !preparing)
                    {
                        Spiritbond.OpenMateriaMenu();
                        return;
                    }
                    if (Spiritbond.IsMateriaMenuOpen() && !isCrafting && !preparing)
                    {
                        Spiritbond.ExtractFirstMateria();
                        return;
                    }

                    return;
                }
                else
                {
                    if (Spiritbond.IsMateriaMenuOpen())
                    {
                        Spiritbond.CloseMateriaMenu();
                        return;
                    }
                }

                if (Service.Configuration.Repair && !RepairManager.ProcessRepair(false) && ((Service.Configuration.Materia && !Spiritbond.IsSpiritbondReadyAny()) || (!Service.Configuration.Materia)))
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose("Entered repair check");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Not crafting");
                        if (!Svc.Condition[ConditionFlag.Crafting]) RepairManager.ProcessRepair(true);
                    }
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Repair ok");
                if (Service.Configuration.AbortIfNoFoodPot && !ConsumableChecker.CheckConsumables(false))
                {
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting] && Enable) ConsumableChecker.CheckConsumables(true);
                    }
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Consumables success");
                {
                    if (CraftingListFunctions.RecipeWindowOpen())
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Addon visible");

                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Error text not visible");
                        if (!HQManager.RestoreHQData(HQData, out var fin) || !fin)
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ data finalised");
                            return;
                        }
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ data restored");

                        if (Tasks.Count == 0)
                        {
                            Tasks.Add(Service.Framework.RunOnTick(CurrentCraft.RepeatActualCraft, TimeSpan.FromMilliseconds(300)));
                        }

                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting])
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Addon invisible");
                            if (Tasks.Count == 0 && !Svc.Condition[ConditionFlag.Crafting40])
                            {
                                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Opening crafting log");
                                if (RecipeID == 0)
                                {
                                    CommandProcessor.ExecuteThrottled("/clog");
                                }
                                else
                                {
                                    if (AutocraftDebugTab.Debug) PluginLog.Debug($"Opening recipe {RecipeID}");
                                    AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal((uint)RecipeID);
                                }
                            }
                        }
                    }

                }
            }
        }

        internal static void Draw()
        {
            if (CraftingListUI.Processing)
            {
                ImGui.TextWrapped("正在处理制作列表...");
                return;
            }

            ImGui.TextWrapped("该模式用于自动重复自动制作物品, 直到到达指定次数或剩余材料不足以继续制作. 该模式能够在自动制作时自动修复装备, 使用食物/指南/药水以及自动精制魔晶石. 请注意这里的设置是独立于制作列表的设置的, 且仅用于自动重复制作单个物品.");
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Checkbox("启用自动重复制作模式", ref enable))
            {
                Enable = enable;
            }
            ImGuiComponents.HelpMarker("要使用该模式, 你需要先在制作笔记界面选择配方然后选择HQ/NQ材料.\n该模式将会自动重复制作选择的配方, 类似自动制作, 但是会在制作之前自动使用选择的消耗品.");
            ImGuiEx.Text($"配方: {RecipeName} {(RecipeID != 0 ? $"({LuminaSheets.RecipeSheet[(uint)RecipeID].CraftType.Value.Name.RawString})" : "")}\nHQ 材料: {HQData?.Select(x => x.ToString()).Join(", ")}");
            bool requireFoodPot = Service.Configuration.AbortIfNoFoodPot;
            if (ImGui.Checkbox("使用食物, 指南并且/或者药水", ref requireFoodPot))
            {
                Service.Configuration.AbortIfNoFoodPot = requireFoodPot;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Artisan 将会寻找指定的食物、指南或药水, 若其中之一没有找到将会终止制作.");
            if (requireFoodPot)
            {

                {
                    ImGuiEx.TextV("使用食物:");
                    ImGui.SameLine(300f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == Service.Configuration.Food, out var item) ? $"{(Service.Configuration.FoodHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Food == 0 ? "无" : $"{(Service.Configuration.FoodHQ ? " " : "")}{Service.Configuration.Food}")}"))
                    {
                        if (ImGui.Selectable("禁用"))
                        {
                            Service.Configuration.Food = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetFood(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Food = x.Id;
                                Service.Configuration.FoodHQ = false;
                                Service.Configuration.Save();
                            }
                        }
                        foreach (var x in ConsumableChecker.GetFood(true, true))
                        {
                            if (ImGui.Selectable($" {x.Name}"))
                            {
                                Service.Configuration.Food = x.Id;
                                Service.Configuration.FoodHQ = true;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("使用药水:");
                    ImGui.SameLine(300f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == Service.Configuration.Potion, out var item) ? $"{(Service.Configuration.PotHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Potion == 0 ? "无" : $"{(Service.Configuration.PotHQ ? " " : "")}{Service.Configuration.Potion}")}"))
                    {
                        if (ImGui.Selectable("禁用"))
                        {
                            Service.Configuration.Potion = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetPots(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Potion = x.Id;
                                Service.Configuration.PotHQ = false;
                                Service.Configuration.Save();
                            }
                        }
                        foreach (var x in ConsumableChecker.GetPots(true, true))
                        {
                            if (ImGui.Selectable($" {x.Name}"))
                            {
                                Service.Configuration.Potion = x.Id;
                                Service.Configuration.PotHQ = true;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("使用指南:");
                    ImGui.SameLine(300f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##manualBuff", ConsumableChecker.Manuals.TryGetFirst(x => x.Id == Service.Configuration.Manual, out var item) ? $"{item.Name}" : $"{(Service.Configuration.Manual == 0 ? "无" : $"{Service.Configuration.Manual}")}"))
                    {
                        if (ImGui.Selectable("禁用"))
                        {
                            Service.Configuration.Manual = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetManuals(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Manual = x.Id;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("使用大国防联军指南:");
                    ImGui.SameLine(300f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##squadronManualBuff", ConsumableChecker.SquadronManuals.TryGetFirst(x => x.Id == Service.Configuration.SquadronManual, out var item) ? $"{item.Name}" : $"{(Service.Configuration.SquadronManual == 0 ? "无" : $"{Service.Configuration.SquadronManual}")}"))
                    {
                        if (ImGui.Selectable("禁用"))
                        {
                            Service.Configuration.SquadronManual = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetSquadronManuals(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.SquadronManual = x.Id;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

            }

            bool repairs = Service.Configuration.Repair;
            if (ImGui.Checkbox("自动维修", ref repairs))
            {
                Service.Configuration.Repair = repairs;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("若启用, Artisan将会在你的装备耐久低于设置的阈值时进行装备维修.");
            if (Service.Configuration.Repair)
            {
                //ImGui.SameLine();
                ImGui.PushItemWidth(200);
                int percent = Service.Configuration.RepairPercent;
                if (ImGui.SliderInt("##repairp", ref percent, 10, 100, $"%d%%"))
                {
                    Service.Configuration.RepairPercent = percent;
                    Service.Configuration.Save();
                }
            }

            bool materia = Service.Configuration.Materia;
            if (ImGui.Checkbox("自动精制魔晶石", ref materia))
            {
                Service.Configuration.Materia = materia;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("当身上的装备有其中一件的精炼值达到100%之后自动进行魔晶石精制");

            ImGui.Checkbox("仅制作N次", ref Service.Configuration.CraftingX);
            if (Service.Configuration.CraftingX)
            {
                ImGui.Text("制作次数:");
                ImGui.SameLine();
                ImGui.PushItemWidth(200);
                if (ImGui.InputInt("###TimesRepeat", ref Service.Configuration.CraftX))
                {
                    if (Service.Configuration.CraftX < 0)
                        Service.Configuration.CraftX = 0;

                }
            }

            bool stopIfFail = Service.Configuration.EnduranceStopFail;
            if (ImGui.Checkbox("当制作失败时自动禁用该模式", ref stopIfFail))
            {
                Service.Configuration.EnduranceStopFail = stopIfFail;
                Service.Configuration.Save();
            }

            bool stopIfNQ = Service.Configuration.EnduranceStopNQ;
            if (ImGui.Checkbox("当制作NQ物品时自动禁用该模式", ref stopIfNQ))
            {
                Service.Configuration.EnduranceStopNQ = stopIfNQ;
                Service.Configuration.Save();
            }
        }

        internal static void DrawRecipeData()
        {
            if (HQManager.TryGetCurrent(out var d))
            {
                HQData = d;
            }
            var addonPtr = Service.GameGui.GetAddonByName("RecipeNote", 1);
            if (addonPtr == IntPtr.Zero)
            {
                return;
            }

            var addon = (AtkUnitBase*)addonPtr;
            if (addon == null)
            {
                return;
            }

            if (addon->IsVisible && addon->UldManager.NodeListCount >= 49)
            {
                try
                {
                    if (addon->UldManager.NodeList[88]->IsVisible)
                    {
                        RecipeID = 0;
                        RecipeName = "";
                        return;
                    }

                    if (addon->UldManager.NodeList[49]->IsVisible)
                    {
                        var text = addon->UldManager.NodeList[49]->GetAsAtkTextNode()->NodeText;
                        var jobText = addon->UldManager.NodeList[101]->GetAsAtkTextNode()->NodeText.ExtractText();
                        uint jobTab = GetSelectedJobTab(addon);
                        var firstCrystal = GetCrystal(addon, 1);
                        var secondCrystal = GetCrystal(addon, 2);
                        var str = MemoryHelper.ReadSeString(&text);
                        var rName = "";

                        /*
                         *  0	3	2	Woodworking
                            1	1	5	Smithing
                            2	3	1	Armorcraft
                            3	2	4	Goldsmithing
                            4	3	4	Leatherworking
                            5	2	5	Clothcraft
                            6	4	6	Alchemy
                            7	5	6	Cooking

                            8	carpenter
                            9	blacksmith
                            10	armorer
                            11	goldsmith
                            12	leatherworker
                            13	weaver
                            14	alchemist
                            15	culinarian
                            (ClassJob - 8)
                         * 
                         * */

                        if (str.ExtractText().Length == 0) return;

                        if (str.ExtractText()[^1] == '')
                        {
                            rName += str.ExtractText().Remove(str.ExtractText().Length - 1, 1).Trim();
                        }
                        else
                        {

                            rName += str.ExtractText().Trim();
                        }

                        if (firstCrystal > 0)
                        {
                            if (RecipeName != rName)
                            {
                                if (LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.Value?.Name!.ExtractText() == rName && x.UnkData5[8].ItemIngredient == firstCrystal && x.UnkData5[9].ItemIngredient == secondCrystal, out var id))
                                {
                                    RecipeID = (int)id.RowId;
                                    RecipeName = id.ItemResult.Value.Name.ExtractText();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Setting Recipe ID");
                    RecipeID = 0;
                    RecipeName = "";
                }
            }
        }

        private static uint GetSelectedJobTab(AtkUnitBase* addon)
        {
            for (int i = 91; i <= 98; i++)
            {
                if (addon->UldManager.NodeList[i]->GetComponent()->UldManager.NodeList[5]->IsVisible)
                {
                    return i switch
                    {
                        91 => 15,
                        92 => 14,
                        93 => 13,
                        94 => 12,
                        95 => 11,
                        96 => 10,
                        97 => 9,
                        98 => 8,
                        _ => throw new NotImplementedException()
                    };
                }
            }

            return 0;
        }

        private static int GetCrystal(AtkUnitBase* addon, int slot)
        {
            try
            {
                var node = slot == 1 ? addon->UldManager.NodeList[29]->GetComponent()->UldManager.NodeList[1]->GetAsAtkImageNode() : addon->UldManager.NodeList[28]->GetComponent()->UldManager.NodeList[1]->GetAsAtkImageNode();
                if (slot == 2 && !node->AtkResNode.IsVisible)
                    return -1;

                var texturePath = node->PartsList->Parts[node->PartId].UldAsset;

                var texFileNameStdString = &texturePath->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
                var texString = texFileNameStdString->Length < 16
                        ? Marshal.PtrToStringAnsi((IntPtr)texFileNameStdString->Buffer)
                        : Marshal.PtrToStringAnsi((IntPtr)texFileNameStdString->BufferPtr);

                if (texString.Contains("020001")) return 2;     //Fire shard
                if (texString.Contains("020002")) return 7;     //Water shard
                if (texString.Contains("020003")) return 3;     //Ice shard
                if (texString.Contains("020004")) return 4;     //Wind shard
                if (texString.Contains("020005")) return 6;     //Lightning shard
                if (texString.Contains("020006")) return 5;     //Earth shard

                if (texString.Contains("020007")) return 8;     //Fire crystal
                if (texString.Contains("020008")) return 13;    //Water crystal
                if (texString.Contains("020009")) return 9;     //Ice crystal
                if (texString.Contains("020010")) return 10;    //Wind crystal
                if (texString.Contains("020011")) return 12;    //Lightning crystal
                if (texString.Contains("020012")) return 11;    //Earth crystal

                if (texString.Contains("020013")) return 14;    //Fire cluster
                if (texString.Contains("020014")) return 19;    //Water cluster
                if (texString.Contains("020015")) return 15;    //Ice cluster
                if (texString.Contains("020016")) return 16;    //Wind cluster
                if (texString.Contains("020017")) return 18;    //Lightning cluster
                if (texString.Contains("020018")) return 17;    //Earth cluster

            }
            catch
            {

            }

            return -1;

        }
    }
}
