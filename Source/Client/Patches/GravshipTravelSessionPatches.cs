using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Client.Persistent;
using RimWorld;
using System.Threading;
using UnityEngine;
using Verse;
using static Multiplayer.Client.Patches.Helperino;


namespace Multiplayer.Client.Patches
{
    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    [HarmonyPriority(5)]
    static class UpdateLoop
    {
        static void Prefix()
        {
            if(Input.GetKeyDown(KeyCode.LeftControl))
            {
                SyncTestClass.SyncFromTestClass("CALLED FROM UPDATE LOOP");
            }
        }
    }

    public static class SyncTestClass
    {
        [SyncMethod]
        public static void SyncFromTestClass(string str)
        {
            Log.Message($"[{GetCurrentWorldTick()}] SyncFromTestClass value: {str} called on this client: {IsHost()}");
        }
    }


    // This is Method gets called by Launch button
    // 
    // Host == client in this text
    //
    // SyncMethod => SyncMethod.Register(typeof(Precept_Ritual), nameof(Precept_Ritual.ShowRitualBeginWindow));
    // This way the method is synced on all clients
    // Prefix
    //      return FALSE breaks the sync
    //      return TRUE keeps the sync
    // Transpiling
    //      breaks the sync
    // Postfix
    //      keeps the sync

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.ShowRitualBeginWindow))]
    static class Precept_Ritual_ShowRitualBeginWindow_Patch
    {
        static bool Prefix(ref bool __state)
        {
            __state = true;

            if(Input.GetKey(KeyCode.LeftShift))
            {
                Log.Message($"Launch PRESSED but return FALSE in prefix, therefore not synced");
                __state = false;
                return false;
            }

            return true;
        }

        static void Postfix(bool __state)
        {
            if(!__state)
            {
                Log.Message($"ShowRitualBeginWindow was skipped via Prefix but Postfix still executes");
                return;
            }
        }
    }

    // That is the OK Button in Launchwindow 
    [HarmonyPatch(typeof(RitualSession), nameof(RitualSession.Start))]
    static class Patch_Dialog_BeginRitual_Start
    {
        static bool Prefix(Dialog_BeginRitual __instance)
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                Log.Message($"OK PRESSED but return FALSE in prefix, therefore not synced");
                return false;
            }

            if(Input.GetKey(KeyCode.LeftAlt))
            {
                SyncTestClass.SyncFromTestClass("SYNC FROM OK BUTTON");
                return false;
            }

            return true;
        }
    }

    public class Helperino
    {
        public static void HostSleepFor(int mili)
        {
            if (IsHost())
            {
                Thread.Sleep(mili);
            }
        }

        public static string GetUsername() => Multiplayer.session.GetPlayerInfo(Multiplayer.session.playerId).Username;

        public static bool HasPressed() => TickPatch.currentExecutingCmdIssuedBySelf;

        public static bool IsHost() => Multiplayer.session.playerId == 0;

        public static int GetCurrentWorldTick() => Multiplayer.game.asyncWorldTimeComp.worldTicks;
    }

}
