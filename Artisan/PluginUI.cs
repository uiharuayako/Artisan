using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using Dalamud.Plugin;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    public class PluginUI : IDisposable
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;

#if DEBUG
        public bool repeatTrial = false;
#endif
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        private bool craftingVisible = false;
        public bool CraftingVisible
        {
            get { return this.craftingVisible; }
            set { if (this.craftingVisible != value) CraftingWindowStateChanged?.Invoke(this, value); this.craftingVisible = value; }
        }

        private static string? CurrentSelectedCraft;

        private readonly IDalamudPlugin Plugin;
        public PluginUI(Artisan plugin)
        {
            Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            //if (!CheckIfCorrectRepo())
            //{
            //    ImGui.SetWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);
            //    if (ImGui.Begin("Fraudulant Repo Detected", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
            //    {
            //        ImGui.Text("[Artisan] Please uninstall and use the official repo:");
            //        if (ImGui.Button("Repository"))
            //        {
            //            ImGui.SetClipboardText("https://love.puni.sh/ment.json");
            //            Notify.Success("Link copied to clipboard");
            //        }
            //        return;
            //    }
            //}

            DrawCraftingWindow();
            CraftingListUI.DrawProcessingWindow();

            if (!Handler.Enable)
            Handler.DrawRecipeData();

            //if (Service.Configuration.ShowEHQ)
            //    MarkChanceOfSuccess();

            if (!Service.Configuration.DisableHighlightedAction)
            Hotbars.MakeButtonsGlow(CurrentRecommendation);

            if (!Visible)
            {
                return;
            }

            ImGui.SetWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan", ref visible, ImGuiWindowFlags.AlwaysUseWindowPadding))
            {
                if (ImGui.BeginTabBar("TabBar"))
                {
                    if (ImGui.BeginTabItem("设置"))
                    {
                        DrawMainWindow();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("长效/自动重复模式"))
                    {
                        Handler.Draw();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("宏"))
                    {
                        MacroUI.Draw();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("制作列表 (BETA)"))
                    {
                        CraftingListUI.Draw();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("关于"))
                    {
                        PunishLib.ImGuiMethods.AboutTab.Draw(Plugin);
                        ImGui.EndTabItem();
                    }
#if DEBUG
                    if (ImGui.BeginTabItem("调试"))
                    {
                        AutocraftDebugTab.Draw();
                        ImGui.EndTabItem();
                    }
#endif
                    ImGui.EndTabBar();
                }
                if (!visible)
                {
                    Service.Configuration.Save();
                    PluginLog.Information("配置已保存");
                }
            }
        }

        private bool CheckIfCorrectRepo()
        {
#if DEBUG
            return true;
#endif
            FileInfo? m = ECommons.DalamudServices.Svc.PluginInterface.AssemblyLocation;
            var manifest = Path.Join(m.DirectoryName, "Artison.json");
            if (File.Exists(manifest))
            {
                DRM? drm = JsonConvert.DeserializeObject<DRM>(File.ReadAllText(manifest));
                if (drm is null)
                    return false;

                if (!drm.DownloadLinkInstall.Equals(@"https://love.puni.sh/plugins/Artisan/latest.zip")) return false;
                if (!drm.Name.Equals("Artisan")) return false;

                return true;

            }
            else
            {
                return false;
            }
        }


        public unsafe static void MarkChanceOfSuccess()
        {
            try
            {
                var recipeWindow = Service.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AtkUnitBase*)recipeWindow;
                if (addonPtr == null)
                    return;

                var baseX = addonPtr->X;
                var baseY = addonPtr->Y;

                var visCheck = (AtkComponentNode*)addonPtr->UldManager.NodeList[6];
                if (!visCheck->AtkResNode.IsVisible)
                    return;

                var selectedCraftNameNode = (AtkTextNode*)addonPtr->UldManager.NodeList[49];
                var selectedCraftWindowBox = (AtkTextNode*)addonPtr->UldManager.NodeList[31];
                var selectedCraftName = selectedCraftNameNode->NodeText.ToString()[14..];
                selectedCraftName = selectedCraftName.Remove(selectedCraftName.Length - 10, 10);
                if (!char.IsLetterOrDigit(selectedCraftName[^1]))
                {
                    selectedCraftName = selectedCraftName.Remove(selectedCraftName.Length - 1, 1).Trim();
                }
                CurrentSelectedCraft = selectedCraftName;
                string selectedCalculated = CalculateEstimate(selectedCraftName);
                AtkResNodeFunctions.DrawSuccessRate(&selectedCraftWindowBox->AtkResNode, selectedCalculated, selectedCraftName, true);
                AtkResNodeFunctions.DrawQualitySlider(&selectedCraftNameNode->AtkResNode, selectedCraftName);

                var craftCount = (AtkTextNode*)addonPtr->UldManager.NodeList[63];
                string count = craftCount->NodeText.ToString();
                int maxCrafts = Convert.ToInt32(count.Split("-")[^1]);

                var crafts = (AtkComponentNode*)addonPtr->UldManager.NodeList[67];
                if (crafts->AtkResNode.IsVisible)
                {
                    var currentShownNodes = 0;

                    for (int i = 1; i <= 13; i++)
                    {
                        var craft = (AtkComponentNode*)crafts->Component->UldManager.NodeList[i];
                        if (craft->AtkResNode.IsVisible && craft->AtkResNode.Y >= 0 && craft->AtkResNode.Y < 340 && currentShownNodes < 10 && currentShownNodes < maxCrafts)
                        {
                            currentShownNodes++;
                            var craftNameNode = (AtkTextNode*)craft->Component->UldManager.NodeList[14];
                            var ItemName = craftNameNode->NodeText.ToString()[14..];
                            ItemName = ItemName.Remove(ItemName.Length - 10, 10);
                            if (!char.IsLetterOrDigit(ItemName[^1]))
                            {
                                ItemName = ItemName.Remove(ItemName.Length - 1, 1).Trim();
                            }

                            string calculatedPercentage = CalculateEstimate(ItemName);

                            AtkResNodeFunctions.DrawSuccessRate(&craft->AtkResNode, $"{calculatedPercentage}", ItemName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Dalamud.Logging.PluginLog.Error(ex, "DrawRecipeChance");
            }
        }

        private static string CalculateEstimate(string itemName)
        {
            var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(itemName)).FirstOrDefault();
            if (sheetItem == null)
                return "未知物品 - 请确认选择的配方窗口";
            var recipeTable = sheetItem.RecipeLevelTable.Value;

            if (!sheetItem.ItemResult.Value.CanBeHq && !sheetItem.IsExpert && !sheetItem.ItemResult.Value.IsCollectable)
                return $"该物品无法制作成HQ物品.";

            if (CharacterInfo.Craftsmanship() < sheetItem.RequiredCraftsmanship || CharacterInfo.Control() < sheetItem.RequiredControl)
                return "当前三围无法制作.";

            if (CharacterInfo.CharacterLevel() >= 80 && CharacterInfo.CharacterLevel() >= sheetItem.RecipeLevelTable.Value.ClassJobLevel + 10 && !sheetItem.IsExpert)
                return "EHQ: 肯定能成.";

            var simulatedPercent = Service.Configuration.UseSimulatedStartingQuality && sheetItem.MaterialQualityFactor != 0 ? Math.Floor(((double)Service.Configuration.CurrentSimulated / ((double)sheetItem.RecipeLevelTable.Value.Quality * ((double)sheetItem.QualityFactor / 100))) * 100) : 0;
            simulatedPercent = CurrentSelectedCraft is null || CurrentSelectedCraft != sheetItem.ItemResult.Value.Name!.RawString ? 0 : simulatedPercent;
            var baseQual = BaseQuality(sheetItem);
            var dur = recipeTable.Durability;
            var baseSteps = baseQual * (dur / 10);
            var maxQual = (double)recipeTable.Quality;
            bool meetsRecCon = CharacterInfo.Control() >= recipeTable.SuggestedControl;
            bool meetsRecCraft = CharacterInfo.Craftsmanship() >= recipeTable.SuggestedCraftsmanship;
            var q1 = baseSteps / maxQual;
            var q2 = CharacterInfo.MaxCP / sheetItem.QualityFactor / 1.5;
            var q3 = CharacterInfo.IsManipulationUnlocked() ? 2 : 1;
            var q4 = sheetItem.RecipeLevelTable.Value.Stars * 6;
            var q5 = meetsRecCon && meetsRecCraft ? 3 : 1;
            var q6 = Math.Floor((q1 * 100) + (q2 * 3 * q3 * q5) - q4 + simulatedPercent);
            var chance = q6 > 100 ? 100 : q6;
            chance = chance < 0 ? 0 : chance;

            return chance switch
            {
                < 20 => "EHQ: 别试了.",
                < 40 => "EHQ: 较低几率.",
                < 60 => "EHQ: 中等几率.",
                < 80 => "EHQ: 很有几率.",
                < 90 => "EHQ: 十分有几率.",
                < 100 => "EHQ: 非常有几率.",
                _ => "EHQ: 肯定能成.",
            };
        }

        private void DrawCraftingWindow()
        {
            if (!CraftingVisible)
            {
                return;
            }

            CraftingVisible = craftingVisible;

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan Crafting Window", ref this.craftingVisible, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                bool autoMode = Service.Configuration.AutoMode;

                if (ImGui.Checkbox("自动模式", ref autoMode))
                {
                    Service.Configuration.AutoMode = autoMode;
                    Service.Configuration.Save();
                }

                if (autoMode)
                {
                    var delay = Service.Configuration.AutoDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("执行间隔 (ms)", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.AutoDelay = delay;
                        Service.Configuration.Save();
                    }
                }


                if (Handler.RecipeID != 0)
                ImGui.Checkbox("长效模式", ref Handler.Enable);

                if (Service.Configuration.CraftingX && Handler.Enable)
                {
                    ImGui.Text($"剩余制作次数: {Service.Configuration.CraftX}");
                }

#if DEBUG
                ImGui.Checkbox("重复模拟制作", ref repeatTrial);
#endif
                //bool failureCheck = Service.Configuration.DisableFailurePrediction;

                //if (ImGui.Checkbox($"Disable Failure Prediction", ref failureCheck))
                //{
                //    Service.Configuration.DisableFailurePrediction = failureCheck;
                //    Service.Configuration.Save();
                //}
                //ImGuiComponents.HelpMarker($"Disabling failure prediction may result in items failing to be crafted.\nUse at your own discretion.");

                ImGui.Text("半自动模式");

                if (ImGui.Button("执行推荐操作"))
                {
                    Hotbars.ExecuteRecommended(CurrentRecommendation);
                }
                if (ImGui.Button("获取推荐操作"))
                {
                    Artisan.FetchRecommendation(CurrentStep);
                }



            }
            ImGui.End();
        }

        public static void DrawMainWindow()
        {
            ImGui.TextWrapped($"在这里你可以调整Artisan的设置. 其中的一些选项能够在制作期间调整.");
            ImGui.TextWrapped($"若要使用Artisan的手动模式高亮推荐操作, 请将你已解锁的所有制作技能放到可见的热键栏上.");
            bool autoEnabled = Service.Configuration.AutoMode;
            //bool autoCraft = Service.Configuration.AutoCraft;
            bool failureCheck = Service.Configuration.DisableFailurePrediction;
            int maxQuality = Service.Configuration.MaxPercentage;
            bool useTricksGood = Service.Configuration.UseTricksGood;
            bool useTricksExcellent = Service.Configuration.UseTricksExcellent;
            bool useSpecialist = Service.Configuration.UseSpecialist;
            //bool showEHQ = Service.Configuration.ShowEHQ;
            //bool useSimulated = Service.Configuration.UseSimulatedStartingQuality;
            bool useMacroMode = Service.Configuration.UseMacroMode;
            bool disableGlow = Service.Configuration.DisableHighlightedAction;
            bool disableToasts = Service.Configuration.DisableToasts;

            ImGui.Separator();
            if (ImGui.Checkbox("启用自动模式", ref autoEnabled))
            {
                Service.Configuration.AutoMode = autoEnabled;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"自动执行推荐的操作.\n需要对应的技能在可见的热键栏上.");
            if (autoEnabled)
            {
                var delay = Service.Configuration.AutoDelay;
                ImGui.PushItemWidth(200);
                if (ImGui.SliderInt("执行间隔 (ms)", ref delay, 0, 1000))
                {
                    if (delay < 0) delay = 0;
                    if (delay > 1000) delay = 1000;

                    Service.Configuration.AutoDelay = delay;
                    Service.Configuration.Save();
                }
            }

            if (ImGui.Checkbox("禁用高亮推荐操作", ref disableGlow))
            {
                Service.Configuration.DisableHighlightedAction = disableGlow;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("如果你想手动来的话, 该矩形高亮会向你指示推荐的操作.");

            if (ImGui.Checkbox($"禁用右下角推荐操作通知", ref disableToasts))
            {
                Service.Configuration.DisableToasts = disableToasts;
                Service.Configuration.Save();
            }

            ImGuiComponents.HelpMarker("当有操作可推荐时弹出提示.");

            //if (ImGui.Checkbox($"Automatically Repeat Last Craft", ref autoCraft))
            //{
            //    Service.Configuration.AutoCraft = autoCraft;
            //    Service.Configuration.Save();
            //}
            //ImGuiComponents.HelpMarker($"Repeats the currently selected craft in your recipe list.\nWill only work whilst you have the items.\nThis will repeat using your set item quality settings.");

            //if (ImGui.Checkbox($"Disable Failure Prediction", ref failureCheck))
            //{
            //    Service.Configuration.DisableFailurePrediction = failureCheck;
            //    Service.Configuration.Save();
            //}
            //ImGuiComponents.HelpMarker($"Disabling failure prediction may result in items failing to be crafted.\nUse at your own discretion.");

            //if (ImGui.Checkbox("Show Estimated HQ on Recipe (EHQ)", ref showEHQ))
            //{
            //    Service.Configuration.ShowEHQ = showEHQ;
            //    Service.Configuration.Save();

            //}
            //ImGuiComponents.HelpMarker($"This will mark in the crafting list an estimated HQ chance based on your current stats.\nThis does not factor in any HQ items used as materials.\nIt is also only a rough estimate due to the nature of crafting.");

            //if (showEHQ)
            //{
            //    ImGui.Indent();
            //    if (ImGui.Checkbox("Use Simulated Starting Quality in Estimates", ref useSimulated))
            //    {
            //        Service.Configuration.UseSimulatedStartingQuality = useSimulated;
            //        Service.Configuration.Save();
            //    }
            //    ImGuiComponents.HelpMarker($"Set a starting quality as if you were using HQ items for calculating EHQ.");
            //    ImGui.Unindent();
            //}

            if (Service.Configuration.UserMacros.Count > 0)
            {
                if (ImGui.Checkbox("启用宏模式", ref useMacroMode))
                {
                    Service.Configuration.UseMacroMode = useMacroMode;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker(@"使用一个固定的宏来进行制作而不是让Artisan决定下一步操作. 
如果宏在制作完成之前结束了, Artisan会自行决定下一步操作直到制作完成.
如果宏无法执行一些操作, 你必须要进行手动干预.");

                if (useMacroMode)
                {
                    string preview = Service.Configuration.SetMacro == null ? "" : Service.Configuration.SetMacro.Name;
                    if (ImGui.BeginCombo("选择宏", preview))
                    {
                        if (ImGui.Selectable(""))
                        {
                            Service.Configuration.SetMacro = null;
                            Service.Configuration.Save();
                        }
                        foreach (var macro in Service.Configuration.UserMacros)
                        {
                            bool selected = Service.Configuration.SetMacro == null ? false : Service.Configuration.SetMacro.ID == macro.ID;
                            if (ImGui.Selectable(macro.Name, selected))
                            {
                                Service.Configuration.SetMacro = macro;
                                Service.Configuration.Save();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }
            }
            else
            {
                useMacroMode = false;
            }

            if (ImGui.Checkbox("使用 '秘诀' 技能 - 高品质", ref useTricksGood))
            {
                Service.Configuration.UseTricksGood = useTricksGood;
                Service.Configuration.Save();
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("使用 '秘诀' 技能 - 最高品质", ref useTricksExcellent))
            {
                Service.Configuration.UseTricksExcellent = useTricksExcellent;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"这2个选项允许你当前状态为高品质或最高品质时使用秘诀.\n其他依赖于该状态的技能不会被使用.");
            if (ImGui.Checkbox("使用专家技能", ref useSpecialist))
            {
                Service.Configuration.UseSpecialist = useSpecialist;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("若当前职业有专家认证, 使用消耗'能工巧匠图纸'道具的技能.\n'设计变动' 将会取代 '观察'.");
            ImGui.TextWrapped("最大品质%%");
            ImGuiComponents.HelpMarker($"当品质达到了设置的品质及以上, Artisan将会专注于推动进展.");
            if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"{maxQuality}%%"))
            {
                Service.Configuration.MaxPercentage = maxQuality;
                Service.Configuration.Save();
            }
        }
    }
}
