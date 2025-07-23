using HarmonyLib;
using Multiplayer.Client.Patches;
using Multiplayer.Client.Util;
using Multiplayer.Common;
using RimWorld.Planet;
using System;
using Verse;

namespace Multiplayer.Client.Patches
{
    [HarmonyPatch(typeof(GenTicks), nameof(GenTicks.GetCameraUpdateRate))]
    public static class VTRSyncPatch
    {
        static bool Prefix(Thing thing, ref int __result)
        {
            if (Multiplayer.Client == null)
                return true;

            // TODO: Put this back to the original value
            // Probably need to sync up all the animations before doing this
            __result = VTRSync.GetSynchronizedUpdateRate(thing);
            return false;
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.UpdateRateTicks), MethodType.Getter)]
    public static class VtrSyncProjectilePatch
    {
        static bool Prefix(ref int __result, Projectile __instance)
        {
            if (Multiplayer.Client == null)
                return true;

            __result = __instance.Spawned ? VTRSync.GetSynchronizedUpdateRate(__instance) : VTRSync.MaximumVtr;
            return false;
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.UpdateRateTicks), MethodType.Getter)]
    public static class VtrSyncWorldObjectPatch
    {
        static bool Prefix(ref int __result, WorldObject __instance)
        {
            if (Multiplayer.Client == null)
                return true;

            __result = VTRSync.MaximumVtr;
            return false;
        }
    }

    static class VTRSync
    {
        // Special identifier for world map (since it doesn't have a uniqueID like regular maps)
        public const int WorldMapId = -2;
        public static int lastMovedToMap = -1;
        public static int lastSentTick = -1;

        // Vtr rates
        public const int MaximumVtr = 15;
        public const int MinimumVtr = 1;

        public static int GetSynchronizedUpdateRate(Thing thing) => thing?.MapHeld?.AsyncTime()?.VTR ?? VTRSync.MaximumVtr;
    }

    [HarmonyPatch(typeof(Game), nameof(Game.CurrentMap), MethodType.Setter)]
    static class MapSwitchPatch
    {
        static void Prefix(Map value)
        {
            if (Multiplayer.Client == null || Client.Multiplayer.session == null) return;

            try
            {
                // Use the old map from the game's map list, if available
                int previousMap = -1;
                if (Find.Maps != null && Find.Maps.Count > 0)
                {
                    var currentMap = Find.CurrentMap;
                    previousMap = currentMap != null ? currentMap.uniqueID : -1;
                }
                int newMap = value?.uniqueID ?? -1;
                int currentTick = Find.TickManager?.TicksGame ?? 0;

                if (previousMap == newMap)
                    return;

                if (VTRSync.lastMovedToMap == newMap && currentTick == VTRSync.lastSentTick)
                    return;

                MpLog.Debug($"VTR MapSwitchPatch: Switching from map {previousMap} to {newMap} at tick {currentTick}");
                Multiplayer.Client.SendCommand(CommandType.PlayerCount, ScheduledCommand.Global, ByteWriter.GetBytes(previousMap, newMap));
                VTRSync.lastMovedToMap = newMap;
                VTRSync.lastSentTick = currentTick;
            }
            catch (Exception ex)
            {
                MpLog.Error($"VTR MapSwitchPatch error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(WorldRendererUtility), nameof(WorldRendererUtility.CurrentWorldRenderMode), MethodType.Getter)]
    static class WorldRenderModePatch
    {
        private static WorldRenderMode lastRenderMode = WorldRenderMode.None;

        static void Postfix(WorldRenderMode __result)
        {
            if (Multiplayer.Client == null) return;

            try
            {
                // Detect transition to world map (Planet mode)
                if (__result == WorldRenderMode.Planet && lastRenderMode != WorldRenderMode.Planet)
                {
                    if (VTRSync.lastMovedToMap != -1)
                    {
                        Multiplayer.Client.SendCommand(CommandType.PlayerCount, ScheduledCommand.Global, ByteWriter.GetBytes(VTRSync.lastMovedToMap, VTRSync.WorldMapId));
                    }
                }

                lastRenderMode = __result;
            }
            catch (Exception ex)
            {
                MpLog.Error($"WorldRenderModePatch error: {ex.Message}");
            }
        }
    }
}
