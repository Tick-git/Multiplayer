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

    // THESE ARE WORLD CMDS
    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    [HarmonyPriority(5)]
    static class UpdateLoop
    {
        static string str = "CALLED FROM UPDATE LOOP";

        static void Prefix()
        {
            if(Input.GetKeyDown(KeyCode.LeftControl))
            {
                SyncTestClass.SyncFromTestClass(str);
            }

            if(Input.GetKeyDown(KeyCode.X))
            {
                Precept_Ritual_ShowRitualBeginWindow_Patch.SyncFromHarmonyTest(str);
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                SyncTestClass.SyncLongEvent(str);
            }
        }
    }

    public static class SyncTestClass
    {
        [SyncMethod]
        public static void SyncFromTestClass(string str)
        {
            Log.Message($"[{GetCurrentWorldTick()}] SyncFromTestClass value: {str} called on this client: {IsIssuer()}");
        }

        [SyncMethod]
        public static void SyncLongEvent(string str)
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                HostSleepFor(3000);
                Log.Message(str);

            }, "SyncLongEvent", false, null);
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

            if (Input.GetKey(KeyCode.A))
            {
                SyncFromHarmonyTest("SYNC FROM LAUNCH BUTTON");
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

        [SyncMethod]
        public static void SyncFromHarmonyTest(string str)
        {
            Log.Message($"[{GetCurrentWorldTick()}] SyncFromHarmonyTest value: {str} called on this client: {IsIssuer()}");
        }
    }

    // That is the OK Button in Launchwindow
    // THESE IS MAP CMD
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
                SyncTestClass.SyncFromTestClass("SYNC WITH TESTCLASS FROM OK BUTTON");
                return false;
            }

            if (Input.GetKey(KeyCode.Q))
            {
                SyncTestClass.SyncLongEvent("SYNC LONGEVENT FROM OK BUTTON");
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

        public static bool IsIssuer() => TickPatch.currentExecutingCmdIssuedBySelf;

        public static bool IsHost() => Multiplayer.session.playerId == 0;

        public static int GetCurrentWorldTick() => Multiplayer.game.asyncWorldTimeComp.worldTicks;
    }

}
