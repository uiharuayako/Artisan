using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Artisan.RawInformation
{
    internal unsafe class AtkResNodeFunctions
    {
        public unsafe static void DrawOutline(AtkResNode* node)
        {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRect(position, position + size, 0xFFFFFF00, 0, ImDrawFlags.RoundCornersAll, 8);
        }

        public unsafe static void DrawSuccessRate(AtkComponentNode* node, AtkTextNode* textNode, string str, string itemName, uint recipeID, bool isMainWindow = false)
        {
            AtkTextNode* clonedNode = null;

            for (var i = 1; i < node->Component->UldManager.NodeListCount; i++)
            {
                var n = node->Component->UldManager.NodeList[i];
                if (n->Type == NodeType.Text && n->NodeID == recipeID)
                {
                    clonedNode = (AtkTextNode*)n;
                    break;
                }
            }

            if (!Service.Configuration.ShowEHQ)
            {
                clonedNode->AtkResNode.ToggleVisibility(false);
                return;
            }

            if (clonedNode == null)
            {
                clonedNode = UiHelper.CloneNode(textNode);
                clonedNode->AtkResNode.NodeID = recipeID;
                var newStrPtr = UiHelper.Alloc(512);
                clonedNode->NodeText.StringPtr = (byte*)newStrPtr;
                clonedNode->NodeText.BufSize = 512;
                UiHelper.ExpandNodeList(node, 1);

                node->Component->UldManager.NodeList[node->Component->UldManager.NodeListCount++] = (AtkResNode*)clonedNode;

                clonedNode->AtkResNode.ParentNode = (AtkResNode*)node;
                clonedNode->AtkResNode.ChildNode = null;
                textNode->AtkResNode.PrevSiblingNode = (AtkResNode*)clonedNode;
                clonedNode->AtkResNode.NextSiblingNode = (AtkResNode*)textNode;

                clonedNode->AlignmentFontType = (byte)AlignmentType.BottomRight;
            }

            node->Component->UldManager.UpdateDrawNodeList();
            node->Component->UldManager.NodeList[6]->ToggleVisibility(false);
            node->Component->UldManager.NodeList[8]->ToggleVisibility(false);
            UiHelper.SetPosition(clonedNode,-50, -3);
            UiHelper.SetSize(clonedNode, node->AtkResNode.Width, node->AtkResNode.Height);
            clonedNode->AtkResNode.ToggleVisibility(true);
            clonedNode->SetText(str);
            clonedNode->TextColor.A = 255;
            clonedNode->TextColor.R = 255;
            clonedNode->TextColor.G = 255;
            clonedNode->TextColor.B = 255;
            clonedNode->TextFlags = 157;
            clonedNode->ResizeNodeForCurrentText();
            textNode->ResizeNodeForCurrentText();
            textNode->TextFlags2 = 0;

        }

        public static unsafe Vector2 GetNodePosition(AtkResNode* node)
        {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null)
            {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }

            return pos;
        }

        public static unsafe Vector2 GetNodeScale(AtkResNode* node)
        {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null)
            {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }

            return scale;
        }

        internal static unsafe void DrawQualitySlider(AtkResNode* node, string selectedCraftName)
        {
            if (!Service.Configuration.UseSimulatedStartingQuality) return;

            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(selectedCraftName)).FirstOrDefault();
            if (sheetItem == null)
                return;

            var currentSimulated = Service.Configuration.CurrentSimulated;
            if (sheetItem.MaterialQualityFactor == 0) return;
            var maxFactor = sheetItem.MaterialQualityFactor == 0 ? 0 : Math.Floor((double)sheetItem.RecipeLevelTable.Value.Quality * ((double)sheetItem.MaterialQualityFactor / 100) * ((double)sheetItem.QualityFactor / 100));
            if (currentSimulated > (int)maxFactor)
                currentSimulated = (int)maxFactor;


            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X - 50f, position.Y + node->Height));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin($"###SliderQuality", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize  | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar);
            var textSize = ImGui.CalcTextSize("Simulated Starting Quality");
            ImGui.TextUnformatted($"Simulated Starting Quality");
            ImGui.PushItemWidth(textSize.Length());
            if (ImGui.SliderInt("", ref currentSimulated, 0, (int)maxFactor))
            {
                Service.Configuration.CurrentSimulated = currentSimulated;
                Service.Configuration.Save();
            }
            ImGui.End();
            ImGui.PopStyleVar(2);
        }
    }
}