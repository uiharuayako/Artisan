using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using PunishLib.Sponsor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PunishLib.ImGuiMethods
{
    public static class AboutTab
    {
        public static Version version = null;

        static MiniManifest PluginManifest = null;
        static string GetImageURL()
        {
            if(PluginManifest == null)
            {
                PluginManifest = new();
                GenericHelpers.Safe(delegate
                {
                    var path = Path.Combine(PunishLibMain.PluginInterface.AssemblyLocation.DirectoryName, $"{Path.GetFileNameWithoutExtension(PunishLibMain.PluginInterface.AssemblyLocation.FullName)}.json");
                    PluginLog.Debug($"Path: {path}");
                    PluginManifest = JsonConvert.DeserializeObject<MiniManifest>(File.ReadAllText(path));
                });
                PluginLog.Debug($"Icon URL: {PluginManifest.IconUrl}");
            }
            return PluginManifest.IconUrl ?? "";
        }

        class MiniManifest
        {
            public string IconUrl = "";
            public string RepoUrl = "";
        }

        public static void Draw(IDalamudPlugin P)
        {
            version ??= P.GetType().Assembly.GetName().Version;
            ImGuiEx.ImGuiLineCentered("About1", delegate
            {
                ImGuiEx.Text($"{P.Name} - {version}");
            });
            ImGuiEx.ImGuiLineCentered("AboutHeader", delegate
            {
                ImGuiEx.Text($"Published and developed with ");
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.SameLine(0, 0);
                ImGuiEx.Text(ImGuiColors.DalamudRed, FontAwesomeIcon.Heart.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine(0, 0);
                ImGuiEx.Text($" by Puni.sh");
            });
            ImGuiHelpers.ScaledDummy(10f);
            ImGuiEx.ImGuiLineCentered("About2", delegate
            {
                if (ThreadLoadImageHandler.TryGetTextureWrap(GetImageURL(), out var texture))
                {
                    ImGui.Image(texture.ImGuiHandle, new(200f, 200f));
                }
            });
            ImGuiHelpers.ScaledDummy(10f);
            ImGuiEx.ImGuiLineCentered("About3", delegate
            {
                /*if (ImGuiEx.IconButton((FontAwesomeIcon)0xf392))
                {
                    GenericHelpers.ShellStart("https://discord.gg/Zzrcc8kmvy");
                }
                ImGui.SameLine();*/
                ImGui.TextWrapped("Join our Discord community for project announcements, updates, and support.");
            });
            ImGuiEx.ImGuiLineCentered("About4", delegate
            {
                if (ImGui.Button("Discord"))
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "https://discord.gg/Zzrcc8kmvy",
                        UseShellExecute = true
                    }); 
                }
                ImGui.SameLine();
                if (ImGui.Button("Repository"))
                {
                    ImGui.SetClipboardText("https://love.puni.sh/ment.json");
                    PunishLibMain.PluginInterface.UiBuilder.AddNotification("Link copied to clipboard", PunishLibMain.PluginName, NotificationType.Success);
                }
                if (SponsorManager.SponsorInfo != null)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Sponsor"))
                    {
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = SponsorManager.SponsorInfo.WebsiteURL,
                            UseShellExecute = true
                        });
                    }
                }
            });
        }
    }
}
