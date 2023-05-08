using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.FCWorkshops;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using PunishLib.ImGuiMethods;
using System;
using System.IO;
using System.Numerics;
using static Artisan.CraftingLogic.CurrentCraft;
using ThreadLoadImageHandler = ECommons.ImGuiMethods.ThreadLoadImageHandler;

namespace Artisan.UI
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    unsafe internal class PluginUI : Window
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public OpenWindow OpenWindow { get; private set; } = OpenWindow.None;

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

        public PluginUI() : base($"{P.Name} {P.GetType().Assembly.GetName().Version}###Artisan")
        {
            this.RespectCloseHotkey = false;
            this.SizeConstraints = new()
            {
                MinimumSize = new(250, 100),
                MaximumSize = new(9999, 9999)
            };
            P.ws.AddWindow(this);
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
            {
                P.Style.Push();
                ImGui.PushFont(P.CustomFont);
                P.StylePushed = true;
            }

        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                ImGui.PopFont();
                P.StylePushed = false;
            }
        }

        public void Dispose()
        {

        }

        public override void Draw()
        {
            var region = ImGui.GetContentRegionAvail();
            var itemSpacing = ImGui.GetStyle().ItemSpacing;

            var topLeftSideHeight = region.Y;

            if (ImGui.BeginTable($"ArtisanTableContainer", 2, ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetWindowWidth() / 2);

                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();
                
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
                if (ImGui.BeginChild($"###ArtisanLeftSide", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                {
                    var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "artisan-icon.png");

                    if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
                    {
                        ImGui.Image(logo.ImGuiHandle, new(125f.Scale(), 125f.Scale()));
                    }
                    ImGui.Spacing();
                    ImGui.Separator();
                    if (ImGui.Selectable("设置", OpenWindow == OpenWindow.Main))
                    {
                        OpenWindow = OpenWindow.Main;
                    }
                    ImGui.Spacing();
                    if (ImGui.Selectable("自动重复制作", OpenWindow == OpenWindow.Endurance))
                    {
                        OpenWindow = OpenWindow.Endurance;
                    }
                    ImGui.Spacing();
                    if (ImGui.Selectable("宏", OpenWindow == OpenWindow.Macro))
                    {
                        OpenWindow = OpenWindow.Macro;
                    }
                    ImGui.Spacing();
                    if (ImGui.Selectable("制作列表", OpenWindow == OpenWindow.Lists))
                    {
                        OpenWindow = OpenWindow.Lists;
                    }
                    ImGui.Spacing();
                    if (ImGui.Selectable("列表生成", OpenWindow == OpenWindow.SpecialList))
                    {
                        OpenWindow = OpenWindow.SpecialList;
                    }
                    ImGui.Spacing();
                    if (ImGui.Selectable("部队工坊", OpenWindow == OpenWindow.FCWorkshop))
                    {
                        OpenWindow = OpenWindow.FCWorkshop;
                    }
                    ImGui.Spacing();
                    if (ImGui.Selectable("关于", OpenWindow == OpenWindow.About))
                    {
                        OpenWindow = OpenWindow.About;
                    }

#if DEBUG
                    ImGui.Spacing();
                    if (ImGui.Selectable("调试", OpenWindow == OpenWindow.Debug))
                    {
                        OpenWindow = OpenWindow.Debug;
                    }
                    ImGui.Spacing();
#endif

                }
                ImGui.EndChild();
                ImGui.PopStyleVar();
                ImGui.TableNextColumn();
                if (ImGui.BeginChild($"###ArtisanRightSide", Vector2.Zero, false, (false ? ImGuiWindowFlags.AlwaysVerticalScrollbar : ImGuiWindowFlags.None) | ImGuiWindowFlags.NoDecoration))
                {

                    if (OpenWindow == OpenWindow.Main)
                    {
                        DrawMainWindow();
                    }

                    if (OpenWindow == OpenWindow.Endurance)
                    {
                        Handler.Draw();
                    }

                    if (OpenWindow == OpenWindow.Lists)
                    {
                        CraftingListUI.Draw();
                    }

                    if (OpenWindow == OpenWindow.About)
                    {
                        PunishLib.ImGuiMethods.AboutTab.Draw(P);
                    }

                    if (OpenWindow == OpenWindow.Debug)
                    {
                        AutocraftDebugTab.Draw();
                    }

                    if (OpenWindow == OpenWindow.Macro)
                    {
                        MacroUI.Draw();
                    }

                    if (OpenWindow == OpenWindow.FCWorkshop)
                    {
                        FCWorkshopUI.Draw();
                    }

                    if (OpenWindow == OpenWindow.SpecialList)
                    {
                        SpecialLists.Draw();
                    }
                   
                }
                ImGui.EndChild();
                ImGui.EndTable();
            }

        }

        public static void DrawMainWindow()
        {
            ImGui.TextWrapped($"在这里你可以调整Artisan的设置. 其中的一些选项能够在制作期间调整.");
            ImGui.TextWrapped($"若要使用Artisan的手动模式高亮推荐操作, 请将你已解锁的所有制作技能放到可见的热键栏上.");
            bool autoEnabled = Service.Configuration.AutoMode;
            bool delayRec = Service.Configuration.DelayRecommendation;
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
            bool disableMini = Service.Configuration.DisableMiniMenu;
            bool useAlternative = Service.Configuration.UseAlternativeRotation;

            ImGui.Separator();
            if (ImGui.CollapsingHeader("模式选择"))
            {
                if (ImGui.Checkbox("启用自动制作模式", ref autoEnabled))
                {
                    Service.Configuration.AutoMode = autoEnabled;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker($"自动执行推荐的操作.");
                if (autoEnabled)
                {
                    var delay = Service.Configuration.AutoDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("执行间隔 (ms)###ActionDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.AutoDelay = delay;
                        Service.Configuration.Save();
                    }
                }

                if (Service.Configuration.UserMacros.Count > 0)
                {
                    if (ImGui.Checkbox("启用宏模式", ref useMacroMode))
                    {
                        Service.Configuration.UseMacroMode = useMacroMode;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker($"使用一个固定的宏来进行制作而不是让Artisan决定下一步操作.\r\n" +
                        $"优先级为单配方的宏, 在这之后是你所选择的宏.\r\n" +
                        $"如果你希望只使用单个配方宏, 请保持下方的设置为空.\r\n" +
                        $"如果宏在制作完成之前结束了, Artisan会自行决定下一步操作直到制作完成.");

                    if (useMacroMode)
                    {
                        string preview = Service.Configuration.SetMacro == null ? "" : Service.Configuration.SetMacro.Name!;
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
            }

            if (ImGui.CollapsingHeader("执行设置"))
            {
                if (ImGui.Checkbox("延迟下一步建议的获取", ref delayRec))
                {
                    Service.Configuration.DelayRecommendation = delayRec;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("如果你遇到 '最终确认' 技能没有正常触发的问题, 可以启用该选项.");

                if (delayRec)
                {
                    var delay = Service.Configuration.RecommendationDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("建议延时 (ms)###RecommendationDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.RecommendationDelay = delay;
                        Service.Configuration.Save();
                    }
                }

                if (ImGui.Checkbox($"使用 {LuminaSheets.CraftActions[Skills.Tricks].Name} - {LuminaSheets.AddonSheet[227].Text.RawString}", ref useTricksGood))
                {
                    Service.Configuration.UseTricksGood = useTricksGood;
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"使用 {LuminaSheets.CraftActions[Skills.Tricks].Name} - {LuminaSheets.AddonSheet[228].Text.RawString}", ref useTricksExcellent))
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
                if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"%d%%"))
                {
                    Service.Configuration.MaxPercentage = maxQuality;
                    Service.Configuration.Save();
                }

                bool requestStop = Service.Configuration.RequestToStopDuty;
                bool requestResume = Service.Configuration.RequestToResumeDuty;
                int resumeDelay = Service.Configuration.RequestToResumeDelay;

                if (ImGui.Checkbox("当任务准备窗口弹出后让Artisan停止重复制作模式/暂停列表制作", ref requestStop))
                {
                    Service.Configuration.RequestToStopDuty = requestStop;
                    Service.Configuration.Save();
                }

                if (requestStop)
                {
                    if (ImGui.Checkbox("当离开副本后让Artisan继续重复制作模式/继续列表制作", ref requestResume))
                    {
                        Service.Configuration.RequestToResumeDuty = requestResume;
                        Service.Configuration.Save();
                    }

                    if (requestResume)
                    {
                        if (ImGui.SliderInt("继续前的等待延时 (秒)", ref resumeDelay, 5, 60))
                        {
                            Service.Configuration.RequestToResumeDelay = resumeDelay;
                        }
                    }
                }

                if (ImGui.Checkbox("使用备用提升品质的循环 (需等级达到 84+)", ref useAlternative))
                {
                    Service.Configuration.UseAlternativeRotation = useAlternative;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("切换至 加工 -> 中级加工 -> 上级加工 而不是直接使用最高等级的加工.");

                if (ImGui.Checkbox("禁用自动装备制作所需的装备", ref Service.Configuration.DontEquipItems))
                    Service.Configuration.Save();
            }

            if (ImGui.CollapsingHeader("UI 设置"))
            {
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

                if (ImGui.Checkbox("禁用迷你菜单", ref disableMini))
                {
                    Service.Configuration.DisableMiniMenu = disableMini;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("在配方列表内隐藏迷你菜单中的配置. 仍旧会显示宏菜单.");

                bool lockMini = Service.Configuration.LockMiniMenu;
                if (ImGui.Checkbox("保持迷你菜单吸附至游戏配方窗口.", ref lockMini))
                {
                    Service.Configuration.LockMiniMenu = lockMini;
                    Service.Configuration.Save();
                }
                if (ImGui.Button("重设迷你菜单位置"))
                {
                    AtkResNodeFunctions.ResetPosition = true;
                }

                bool hideQuestHelper = Service.Configuration.HideQuestHelper;
                if (ImGui.Checkbox($"隐藏任务辅助", ref hideQuestHelper))
                {
                    Service.Configuration.HideQuestHelper = hideQuestHelper;
                    Service.Configuration.Save();
                }

                bool hideTheme = Service.Configuration.DisableTheme;
                if (ImGui.Checkbox("禁用自定义主题", ref hideTheme))
                {
                    Service.Configuration.DisableTheme = hideTheme;
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (IconButtons.IconTextButton(FontAwesomeIcon.Clipboard, "复制该主题"))
                {
                    ImGui.SetClipboardText("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA");
                    Notify.Success("主题配置已复制到剪切板");
                }
            }
            if (ImGui.CollapsingHeader("列表生成配置"))
            {
                ImGui.TextWrapped($"当创建制作列表时, 这些设置将会自动应用.");

                if (ImGui.Checkbox("跳过你已经拥有了足够数量的物品", ref Service.Configuration.DefaultListSkip))
                {
                    Service.Configuration.Save();
                }

                if (ImGui.Checkbox("自动精炼魔晶石", ref Service.Configuration.DefaultListMateria))
                {
                    Service.Configuration.Save();
                }

                if (ImGui.Checkbox("自动维修", ref Service.Configuration.DefaultListRepair))
                {
                    Service.Configuration.Save();
                }

                if (Service.Configuration.DefaultListRepair)
                {
                    ImGui.TextWrapped($"低于多少进行维修");
                    ImGui.SameLine();
                    if (ImGui.SliderInt("###SliderRepairDefault", ref Service.Configuration.DefaultListRepairPercent, 0, 100, $"%d%%"))
                    {
                        Service.Configuration.Save();
                    }
                }

                if (ImGui.Checkbox("设置新添加的物品制作模式为快速制作", ref Service.Configuration.DefaultListQuickSynth))
                {
                    Service.Configuration.Save();
                }

                if (ImGui.Checkbox($@"当添加到列表后重置 ""要添加的次数"".", ref Service.Configuration.ResetTimesToAdd))
                    Service.Configuration.Save();
            }
        }
    }

    public enum OpenWindow
    {
        None = 0,
        Main = 1,
        Endurance = 2,
        Macro = 3,
        Lists = 4,
        About = 5,
        Debug = 6,
        FCWorkshop = 7,
        SpecialList = 8,
    }
}
