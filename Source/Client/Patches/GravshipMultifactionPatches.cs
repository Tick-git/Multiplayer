using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Multiplayer.Client.Factions;
using Multiplayer.Client.Util;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Client.Patches;

// TODO: Maybe put into the other StartChoosingDestination patch
[HarmonyPatch(typeof(CompPilotConsole), nameof(CompPilotConsole.StartChoosingDestination_NewTemp))]
public static class PatchGravshipStartChoosingDestinationShouldDisplay
{
    static bool Prefix(CompPilotConsole __instance)
    {
        return Multiplayer.Client == null || __instance.engine.Faction.IsClientFaction();
    }
}

public static class GravshipMultifactionPatches
{
    public static void SkipLandingAnimation(WorldComponent_GravshipController gravshipController, Gravship gravship, IntVec3 landingPos, Map map)
    {
        gravshipController.PlaceGravship(gravship, landingPos, map);
        gravshipController.landingMap = null;
    }

    // TODO: Check if setting all values is even necessary
    public static void SkipTakeOffAnimation(WorldComponent_GravshipController __instance, Building_GravEngine engine, PlanetTile targetTile)
    {
        __instance.map = engine.Map;
        __instance.takeoffTile = __instance.map.Tile;
        __instance.landingTile = targetTile;
        __instance.mapHasGravAnchor = __instance.map.listerThings.AnyThingWithDef(ThingDefOf.GravAnchor);
        __instance.gravship = __instance.RemoveGravshipFromMap(engine);
        __instance.TakeoffEnded();
    }
}

[HarmonyPatch(typeof(GravshipUtility), nameof(GravshipUtility.AbandonMap))]
public static class PatchGravshipAbandonMapToRevertWantedModeChange
{
    static void Postfix(Map map)
    {
        if (Multiplayer.Client == null) return;
        if (!Multiplayer.MultifactionEnabled) return;

        if (!map.ParentFaction.IsClientFaction())
            Find.World.renderer.wantedMode = WorldRenderMode.None;
    }
}

[HarmonyPatch(typeof(WorldComponent_GravshipController), nameof(WorldComponent_GravshipController.WorldComponentOnGUI))]
public static class PatchGravshipOnGuiToCancelItForNonClientFactions
{
    static bool Prefix(WorldComponent_GravshipController __instance)
    {
        if (Multiplayer.Client == null) return true;
        if (__instance.landingMarker?.gravship?.Faction == null) return true;

        LazyLoadMoveDesignator(__instance);

        return __instance.landingMarker.gravship.Faction.IsClientFaction();
    }

    static void LazyLoadMoveDesignator(WorldComponent_GravshipController __instance)
    {
        __instance.MoveDesignator();
    }
}

[HarmonyPatch(typeof(GravshipUtility), nameof(GravshipUtility.ArriveNewMap))]
public static class PatchGravshipArriveNewMapHandleFactionContext
{
    static void Prefix(Gravship gravship, bool __state)
    {
        if (Multiplayer.Client == null) return;
        if (!Multiplayer.MultifactionEnabled) return;

        FactionContext.Push(gravship.engine.Faction);
        __state = true;
    }

    static void Finalizer(bool __state)
    {
        if (__state)
            FactionContext.Pop();
    }
}

public static class GravshipCameraMultifactionPatches
{
    static bool runCameraMethods = true;

    [HarmonyPatch]
    public static class PatchGravshipTryJumpMethods
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return MpMethodUtil.GetLambda(typeof(GravshipUtility), nameof(GravshipUtility.ArriveNewMap), lambdaOrdinal: 1);
            yield return MpMethodUtil.GetLambda(typeof(GravshipUtility), nameof(GravshipUtility.ArriveExistingMap), lambdaOrdinal: 0);
            yield return MpMethodUtil.GetLambda(typeof(GravshipLandingMarker), nameof(GravshipLandingMarker.SpawnSetup), lambdaOrdinal: 0);
            yield return MpMethodUtil.GetLambda(typeof(CompPilotConsole), nameof(CompPilotConsole.StartChoosingDestination_NewTemp), lambdaOrdinal: 4);
            yield return AccessTools.Method(typeof(GravshipLandingMarker), nameof(GravshipLandingMarker.BeginLanding));
            yield return AccessTools.Method(typeof(GravshipUtility), nameof(GravshipUtility.TravelTo));
        }

        static void Prefix() => runCameraMethods = false;

        static void Finalizer() => runCameraMethods = true;
    }

    [HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.TryJump), [typeof(GlobalTargetInfo), typeof(CameraJumper.MovementMode)])]
    public static class PatchTryJumpTargetInfoGravshipMultifaction
    {
        static bool Prefix(GlobalTargetInfo target, CameraJumper.MovementMode mode)
        {
            if (Multiplayer.Client == null) return true;
            if (!Multiplayer.MultifactionEnabled) return true;

            return runCameraMethods || target.Faction().IsClientFaction();
        }
    }

    // TODO: Test Arrive On Existing Map
    [HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.TryJump), [typeof(IntVec3), typeof(Map), typeof(CameraJumper.MovementMode)])]
    public static class PatchTryJumpMapGravshipMultifaction
    {
        static bool Prefix(IntVec3 cell, Map map, CameraJumper.MovementMode mode)
        {
            if (Multiplayer.Client == null) return true;
            if (!Multiplayer.MultifactionEnabled) return true;

            return runCameraMethods || map.ParentFaction.IsClientFaction();
        }
    }

    [HarmonyPatch(typeof(CameraShaker), nameof(CameraShaker.DoShake), [typeof(float), typeof(int)])]
    public static class PatchGravshipCameraShakeMultifaction
    {
        static bool Prefix()
        {
            if (Multiplayer.Client == null) return true;
            if (!Multiplayer.MultifactionEnabled) return true;

            return runCameraMethods;
        }
    }

    [HarmonyPatch(typeof(WorldComponent_GravshipController), nameof(WorldComponent_GravshipController.Notify_LandingAreaConfirmationStarted))]
    public static class PatchGravshipNotifyLandingAreaConfirmationStartedToSetFaction
    {
        static void Prefix(ref GravshipLandingMarker marker)
        {
            if (Multiplayer.Client == null) return;
            if (!Multiplayer.MultifactionEnabled) return;

            marker.factionInt = marker.gravship.Faction;
        }
    }
}
