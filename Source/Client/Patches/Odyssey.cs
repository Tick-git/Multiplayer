using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Multiplayer.Client.Patches
{
    [HarmonyPatch(typeof(Window), "PostClose")]
    public static class Patch_Window_PostClose
    {
        static void Postfix(Window __instance)
        {
            if (__instance is Dialog_MessageBox msgBox &&
                msgBox.text.RawText.StartsWith("ConfirmGravEngineLaunch".Translate().RawText))
            {
                SyncCloseGravshipDialog();
            }
        }

        [SyncMethod]
        public static void SyncCloseGravshipDialog()
        {
            IList<Window> windows = Find.WindowStack.Windows;
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (windows[i] is Dialog_MessageBox msgBox && msgBox.text.RawText.StartsWith("ConfirmGravEngineLaunch".Translate().RawText))
                {
                    msgBox.Close();
                }
            }
        }
    }
}
