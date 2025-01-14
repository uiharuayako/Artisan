﻿using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CustomDeliveries;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    public sealed class Artisan : IDalamudPlugin
    {
        public string Name => "Artisan";
        private const string commandName = "/artisan";
        public static PluginUI? PluginUi { get; set; }
        public static bool currentCraftFinished = false;
        public static readonly object _lockObj = new();
        public static List<Task> Tasks = new();
        public static bool warningMessage = false;

        public Artisan(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            pluginInterface.Create<Service>();
            Service.Plugin = this;

            Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);


            ECommons.ECommons.Init(pluginInterface, this);
            PluginUi = new PluginUI(this);

            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Artisan menu."
            });

            Service.Interface.UiBuilder.Draw += DrawUI;
            Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.Condition.ConditionChange += CheckForCraftedState;
            Service.Framework.Update += FireBot;
            Service.ClientState.Logout += DisableEndurance;
            Service.ClientState.Login += DisableEndurance;
            Service.Condition.ConditionChange += Condition_ConditionChange;
            Service.ChatGui.ChatMessage += ScanForHQItems;
            ActionWatching.Enable();
            StepChanged += ResetRecommendation;
            ConsumableChecker.Init();
            Handler.Init();
            //SatisfactionManagerHelper.Enable();
            CleanUpIndividualMacros();
        }

        private void ScanForHQItems(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (type == (XivChatType)2242 && Service.Condition[ConditionFlag.Crafting])
            {
                if (message.Payloads.Any(x => x.Type == PayloadType.Item))
                {
                    var item = (ItemPayload)message.Payloads.First(x => x.Type == PayloadType.Item);
                    if (item.Item.CanBeHq)
                        LastItemWasHQ = item.IsHQ;

                    LastCraftedItem = item.Item;
                }
            }
        }

        private void Condition_ConditionChange(ConditionFlag flag, bool value)
        {
            if (Service.Condition[ConditionFlag.PreparingToCraft])
            {
                State = CraftingState.PreparingToCraft;
                return;
            }
            if (Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft])
            {
                State = CraftingState.Crafting;
                return;
            }
            if (!Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft])
            {
                State = CraftingState.NotCrafting;
                return;
            }
        }

        private void DisableEndurance(object? sender, EventArgs e)
        {
            Handler.Enable = false;
            CraftingListUI.Processing = false;
        }

        public static void CleanUpIndividualMacros()
        {
            foreach (var item in Service.Configuration.IndividualMacros)
            {
                if (item.Value is null || !Service.Configuration.UserMacros.Any(x => x.ID == item.Value.ID))
                {
                    Service.Configuration.IndividualMacros.Remove(item.Key);
                    Service.Configuration.Save();
                }
            }
        }

        private void ResetRecommendation(object? sender, int e)
        {
            if (e == 0)
            {
                ManipulationUsed = false;
                JustUsedObserve = false;
                VenerationUsed = false;
                InnovationUsed = false;
                WasteNotUsed = false;
                JustUsedFinalAppraisal = false;
                BasicTouchUsed = false;
                StandardTouchUsed = false;
                AdvancedTouchUsed = false;
                ExpertCraftOpenerFinish = false;
                MacroStep = 0;
            }
            if (e > 0)
                Tasks.Clear();
        }

        public static bool CheckIfCraftFinished()
        {
            //if (QuickSynthMax > 0 && QuickSynthCurrent == QuickSynthMax) return true;
            if (MaxProgress == 0) return false;
            if (CurrentProgress == MaxProgress) return true;
            if (CurrentProgress < MaxProgress && CurrentDurability == 0) return true;
            currentCraftFinished = false;
            return false;
        }

        private void FireBot(Framework framework)
        {
            if (!Service.ClientState.IsLoggedIn)
            {
                Handler.Enable = false;
                CraftingListUI.Processing = false;
            }
            PluginUi.CraftingVisible = Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft];
            if (!PluginUi.CraftingVisible)
                ActionWatching.TryDisable();
            else
                ActionWatching.TryEnable();

            GetCraft();
            if (CanUse(Skills.BasicSynth) && CurrentRecommendation == 0 && Tasks.Count == 0 && CurrentStep >= 1)
            {
                if (Recipe is null && !warningMessage)
                {
                    DuoLog.Error("Warning: Your recipe cannot be parsed in Artisan. Please report this to the Discord with the recipe name and client language.");
                    warningMessage = true;
                }
                else
                {
                    warningMessage = false;
                }

                if (warningMessage)
                    return;

                var delay = Service.Configuration.DelayRecommendation ? Service.Configuration.RecommendationDelay : 0;
                Tasks.Add(Service.Framework.RunOnTick(() => FetchRecommendation(CurrentStep), TimeSpan.FromMilliseconds(delay)));
            }

            if (CheckIfCraftFinished() && !currentCraftFinished)
            {
                currentCraftFinished = true;

                if (CraftingListUI.Processing)
                {
                    Dalamud.Logging.PluginLog.Verbose("Advancing Crafting List");
                    CraftingListFunctions.CurrentIndex++;
                }


                if (Handler.Enable && Service.Configuration.CraftingX && Service.Configuration.CraftX > 0)
                {
                    Service.Configuration.CraftX -= 1;
                    if (Service.Configuration.CraftX == 0)
                    {
                        Service.Configuration.CraftingX = false;
                        Handler.Enable = false;
                        DuoLog.Information("Craft X has completed.");

                    }
                }

#if DEBUG
                if (PluginUi.repeatTrial && Service.Configuration.CraftingX && Service.Configuration.CraftX > 0)
                {
                    Service.Configuration.CraftX -= 1;
                    if (Service.Configuration.CraftX == 0)
                    {
                        Service.Configuration.CraftingX = false;
                        PluginUi.repeatTrial = false;
                    }
                }
#endif
            }


#if DEBUG
            if (PluginUi.repeatTrial)
            {
                RepeatTrialCraft();
            }
#endif

        }

        public static void FetchRecommendation(int e)
        {
            lock (_lockObj)
            {
                try
                {
                    CurrentRecommendation = Recipe.IsExpert ? GetExpertRecommendation() : GetRecommendation();

                    if (Service.Configuration.UseMacroMode && Service.Configuration.UserMacros.Count > 0)
                    {
                        if (Service.Configuration.IndividualMacros.TryGetValue(Recipe.RowId, out var macro))
                        {
                            macro = Service.Configuration.UserMacros.First(x => x.ID == macro.ID);
                            if (MacroStep < macro.MacroActions.Count)
                            {
                                if (macro.MacroOptions.SkipQualityIfMet)
                                {
                                    if (CurrentQuality >= MaxQuality)
                                    {
                                        while (ActionIsQuality(macro))
                                        {
                                            MacroStep++;
                                        }
                                    }
                                }

                                CurrentRecommendation = macro.MacroActions[MacroStep] == 0 ? CurrentRecommendation : macro.MacroActions[MacroStep];

                                try
                                {
                                    if (macro.MacroStepOptions.Count == 0 || !macro.MacroStepOptions[MacroStep].ExcludeFromUpgrade)
                                    {
                                        if (macro.MacroOptions.UpgradeQualityActions && ActionIsQuality(macro) && ActionUpgradable(macro, out uint newAction))
                                        {
                                            CurrentRecommendation = newAction;
                                        }
                                        if (macro.MacroOptions.UpgradeProgressActions && !ActionIsQuality(macro) && ActionUpgradable(macro, out newAction))
                                        {
                                            CurrentRecommendation = newAction;
                                        }
                                    }
                                }
                                catch {}
                            }
                        }
                        else
                        {
                            if (Service.Configuration.SetMacro != null && MacroStep < Service.Configuration.SetMacro.MacroActions.Count)
                            {
                                if (Service.Configuration.SetMacro.MacroOptions.SkipQualityIfMet)
                                {
                                    if (CurrentQuality >= MaxQuality)
                                    {
                                        while (ActionIsQuality(Service.Configuration.SetMacro))
                                        {
                                            MacroStep++;
                                        }
                                    }
                                }

                                CurrentRecommendation = Service.Configuration.SetMacro.MacroActions[MacroStep] == 0 ? CurrentRecommendation : Service.Configuration.SetMacro.MacroActions[MacroStep];

                                try
                                {
                                    if (Service.Configuration.SetMacro.MacroStepOptions.Count == 0 || !Service.Configuration.SetMacro.MacroStepOptions[MacroStep].ExcludeFromUpgrade)
                                    {
                                        if (Service.Configuration.SetMacro.MacroOptions.UpgradeQualityActions && ActionIsQuality(Service.Configuration.SetMacro) && ActionUpgradable(Service.Configuration.SetMacro, out uint newAction))
                                        {
                                            CurrentRecommendation = newAction;
                                        }
                                        if (Service.Configuration.SetMacro.MacroOptions.UpgradeProgressActions && !ActionIsQuality(Service.Configuration.SetMacro) && ActionUpgradable(Service.Configuration.SetMacro, out newAction))
                                        {
                                            CurrentRecommendation = newAction;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    RecommendationName = CurrentRecommendation.NameOfAction();

                    if (CurrentRecommendation != 0)
                    {
                        if (LuminaSheets.ActionSheet.TryGetValue(CurrentRecommendation, out var normalAct))
                        {
                            if (normalAct.ClassJob.Value.RowId != CharacterInfo.JobID())
                            {
                                var newAct = LuminaSheets.ActionSheet.Values.Where(x => x.Name.RawString == normalAct.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                                CurrentRecommendation = newAct.RowId;
                                if (!Service.Configuration.DisableToasts)
                                {
                                    QuestToastOptions options = new() { IconId = newAct.Icon };
                                    Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);
                                }

                            }
                            else
                            {
                                if (!Service.Configuration.DisableToasts)
                                {
                                    QuestToastOptions options = new() { IconId = normalAct.Icon };
                                    Service.ToastGui.ShowQuest($"Use {normalAct.Name}", options);
                                }
                            }
                        }

                        if (LuminaSheets.CraftActions.TryGetValue(CurrentRecommendation, out var craftAction))
                        {
                            if (craftAction.ClassJob.Row != CharacterInfo.JobID())
                            {
                                var newAct = LuminaSheets.CraftActions.Values.Where(x => x.Name.RawString == craftAction.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                                CurrentRecommendation = newAct.RowId;
                                if (!Service.Configuration.DisableToasts)
                                {
                                    QuestToastOptions options = new() { IconId = newAct.Icon };
                                    Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);
                                }
                            }
                            else
                            {
                                if (!Service.Configuration.DisableToasts)
                                {
                                    QuestToastOptions options = new() { IconId = craftAction.Icon };
                                    Service.ToastGui.ShowQuest($"Use {craftAction.Name}", options);
                                }
                            }
                        }

                        if (Service.Configuration.AutoMode)
                        {
                            Service.Framework.RunOnTick(() => Hotbars.ExecuteRecommended(CurrentRecommendation), TimeSpan.FromMilliseconds(Service.Configuration.AutoDelay));

                            //Service.Plugin.BotTask.Schedule(() => Hotbars.ExecuteRecommended(CurrentRecommendation), Service.Configuration.AutoDelay);
                        }

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Dalamud.Logging.PluginLog.Error(ex, "Crafting Step Change");
                }
            }

        }

        private static bool ActionUpgradable(Macro macro, out uint newAction)
        {
            newAction = macro.MacroActions[MacroStep];
            if (CurrentCondition is CraftingLogic.CurrentCraft.Condition.Good or CraftingLogic.CurrentCraft.Condition.Excellent)
            {
                switch (newAction)
                {
                    case Skills.FocusedSynthesis:
                    case Skills.Groundwork:
                    case Skills.PrudentSynthesis:
                    case Skills.CarefulSynthesis:
                    case Skills.BasicSynth:
                        newAction = Skills.IntensiveSynthesis;
                        break;
                    case Skills.HastyTouch:
                    case Skills.FocusedTouch:
                    case Skills.PreparatoryTouch:
                    case Skills.AdvancedTouch:
                    case Skills.StandardTouch:
                    case Skills.BasicTouch:
                        newAction = Skills.PreciseTouch;
                        break;
                }

                return CanUse(newAction);
            }

            return false;
        }

        public static bool ActionIsQuality(Macro macro)
        {
            var currentAction = macro.MacroActions[MacroStep];
            switch (currentAction)
            {
                case Skills.HastyTouch:
                case Skills.FocusedTouch:
                case Skills.PreparatoryTouch:
                case Skills.AdvancedTouch:
                case Skills.StandardTouch:
                case Skills.BasicTouch:
                case Skills.GreatStrides:
                case Skills.Innovation:
                case Skills.ByregotsBlessing:
                case Skills.TrainedFinesse:
                    return true;
                default:
                    return false;
            }
        }

        private void CheckForCraftedState(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.Crafting && value)
            {
                PluginUi.CraftingVisible = true;
            }
        }

        public void Dispose()
        {
            PluginUi.Dispose();
            Handler.Dispose();
            ECommons.ECommons.Dispose();

            Service.CommandManager.RemoveHandler(commandName);
            Service.Condition.ConditionChange -= Condition_ConditionChange;
            Service.ChatGui.ChatMessage -= ScanForHQItems;
            Service.Interface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            Service.Interface.UiBuilder.Draw -= DrawUI;
            Service.Framework.Update -= FireBot;

            ActionWatching.Dispose();
            SatisfactionManagerHelper.Dispose();
            Service.Plugin = null!;
        }

        private void OnCommand(string command, string args)
        {
            PluginUi.Visible = !PluginUi.Visible;
        }

        private void DrawUI()
        {
            PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            PluginUi.Visible = true;
        }
    }
}
