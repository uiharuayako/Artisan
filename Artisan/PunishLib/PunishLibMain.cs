using Dalamud.Plugin;

using PunishLib.Sponsor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PunishLib
{
    public class PunishLibMain
    {
        internal static string PluginName = "";
        internal static DalamudPluginInterface PluginInterface;

        public static void Init(DalamudPluginInterface pluginInterface, IDalamudPlugin instance)
        {
            PluginName = instance.Name;
            PluginInterface = pluginInterface;
        }

        public static void Dispose() { }
    }
}
