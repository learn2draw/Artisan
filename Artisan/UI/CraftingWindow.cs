﻿using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic.ExpertSolver;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Linq;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan.UI
{
    internal class CraftingWindow : Window
    {
        public bool repeatTrial = false;

        public CraftingWindow() : base("Artisan Crafting Window###MainCraftWindow", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            this.SizeConstraints = new()
            {
                MinimumSize = new System.Numerics.Vector2(150f, 0f),
                MaximumSize = new System.Numerics.Vector2(310f, 500f)
            };
        }

        public override bool DrawConditions()
        {
            return P.PluginUi.CraftingVisible;
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
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

        public static TimeSpan MacroTime = new();
        public override void Draw()
        {
            if (!P.Config.DisableHighlightedAction)
                Hotbars.MakeButtonsGlow(CurrentRecommendation);

            if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
            {
                P.PluginUi.IsOpen = true;
            }

            if (CurrentRecipe is not null && CurrentRecipe.IsExpert && !P.Config.IRM.ContainsKey(CurrentRecipe.RowId))
            {
                ImGui.Dummy(new System.Numerics.Vector2(12f));
                if (!P.Config.ExpertSolverConfig.Enabled)
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, "This is an expert recipe. It is strongly recommended to use an Artisan macro or manually solve this.", this.SizeConstraints?.MaximumSize.X ?? 0);
                else
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, "This is an expert recipe. You are using the experimental solver currently. Your success rate may vary.", this.SizeConstraints?.MaximumSize.X ?? 0);
            }

            if (CurrentRecipe is not null && CurrentRecipe.SecretRecipeBook.Row > 0 && CurrentRecipe.RecipeLevelTable.Value.ClassJobLevel == CharacterInfo.CharacterLevel && !P.Config.IRM.ContainsKey(CurrentRecipe.RowId))
            {
                if (!CurrentRecipe.IsExpert)
                {
                    ImGui.Dummy(new System.Numerics.Vector2(12f));
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, "This is a current level master recipe. Your success rate may vary so it is recommended to use an Artisan macro or manually solve this.", this.SizeConstraints?.MaximumSize.X ?? 0);
                }
            }

            bool autoMode = P.Config.AutoMode;

            if (ImGui.Checkbox("Auto Action Mode", ref autoMode))
            {
                if (!autoMode)
                    ActionWatching.BlockAction = false;

                P.Config.AutoMode = autoMode;
                P.Config.Save();
            }

            if (autoMode)
            {
                var delay = P.Config.AutoDelay;
                ImGui.PushItemWidth(200);
                if (ImGui.SliderInt("Set delay (ms)", ref delay, 0, 1000))
                {
                    if (delay < 0) delay = 0;
                    if (delay > 1000) delay = 1000;

                    P.Config.AutoDelay = delay;
                    P.Config.Save();
                }
            }


            if (Endurance.RecipeID != 0 && !CraftingListUI.Processing && Endurance.Enable)
            {
                if (ImGui.Button("Disable Endurance"))
                {
                    Endurance.Enable = false;
                }
            }

            if (!Endurance.Enable && DoingTrial)
                ImGui.Checkbox("Trial Craft Repeat", ref repeatTrial);

            if (P.Config.IRM.ContainsKey((uint)Endurance.RecipeID))
            {
                var macro = P.Config.UserMacros.FirstOrDefault(x => x.ID == P.Config.IRM[(uint)Endurance.RecipeID]);
                ImGui.TextWrapped($"Using Macro: {macro.Name} ({(MacroStep >= macro.MacroActions.Count ? macro.MacroActions.Count : MacroStep + 1)}/{macro.MacroActions.Count})");

                if (MacroStep >= macro.MacroActions.Count)
                {
                    ImGui.TextWrapped($"Macro has completed. {(!P.Config.DisableMacroArtisanRecommendation ? "Now continuing with solver." : "Please continue to manually craft.")}");
                }
            }
            else
            {
                ImGui.TextColored(ImGuiColors.DalamudYellow, "No macro set");
            }

            if (P.Config.CraftingX && Endurance.Enable)
                ImGui.Text($"Remaining Crafts: {P.Config.CraftX}");

            if (P.Config.AutoMode)
            {
                if (P.Config.IRM.TryGetValue((uint)Endurance.RecipeID, out var prevMacro))
                {
                    Macro? macro = P.Config.UserMacros.First(x => x.ID == prevMacro);
                    if (macro != null)
                    {
                        string duration = string.Format("{0:D2}h {1:D2}m {2:D2}s", MacroTime.Hours, MacroTime.Minutes, MacroTime.Seconds);

                        ImGui.Text($"Approximate Remaining Duration: {duration}");
                    }
                }
            }

            if (!P.Config.AutoMode)
            {
                ImGui.Text("Semi-Manual Mode");

                if (ImGui.Button("Execute recommended action"))
                {
                    Hotbars.ExecuteRecommended(CurrentRecommendation);
                }
                if (ImGui.Button("Fetch Recommendation"))
                {
                    Artisan.Tasks.Clear();
                    FetchRecommendation(CurrentStep);
                }
            }
        }
    }
}
