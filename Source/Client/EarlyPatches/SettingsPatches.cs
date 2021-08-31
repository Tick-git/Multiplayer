using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using Multiplayer.Client.Patches;
using UnityEngine;
using Verse;

namespace Multiplayer.Client.EarlyPatches
{
    [EarlyPatch]
    [HarmonyPatch]
    static class PrefGettersInMultiplayer
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertyGetter(typeof(Prefs), nameof(Prefs.PauseOnError));
            yield return AccessTools.PropertyGetter(typeof(Prefs), nameof(Prefs.AutomaticPauseMode));
            yield return AccessTools.PropertyGetter(typeof(Prefs), nameof(Prefs.PauseOnLoad));
        }

        static bool Prefix() => Multiplayer.Client == null;
    }

    [EarlyPatch]
    [HarmonyPatch(typeof(Prefs), nameof(Prefs.PreferredNames), MethodType.Getter)]
    static class PreferredNamesPatch
    {
        static List<string> empty = new List<string>();

        static void Postfix(ref List<string> __result)
        {
            if (Multiplayer.Client != null)
                __result = empty;
        }
    }

    [EarlyPatch]
    [HarmonyPatch(typeof(Prefs), nameof(Prefs.MaxNumberOfPlayerSettlements), MethodType.Getter)]
    static class MaxColoniesPatch
    {
        static void Postfix(ref int __result)
        {
            if (Multiplayer.Client != null)
                __result = 5;
        }
    }

    [EarlyPatch]
    [HarmonyPatch(typeof(Prefs), nameof(Prefs.RunInBackground), MethodType.Getter)]
    static class RunInBackgroundPatch
    {
        static void Postfix(ref bool __result)
        {
            if (Multiplayer.Client != null)
                __result = true;
        }
    }

    [EarlyPatch]
    [HarmonyPatch]
    static class CancelDuringSimulating
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertyGetter(typeof(Prefs), nameof(Prefs.VolumeGame));
            yield return AccessTools.Method(typeof(Prefs), nameof(Prefs.Save));
        }

        static bool Prefix() => !TickPatch.Simulating;
    }

    [EarlyPatch]
    [HarmonyPatch(typeof(TutorSystem), nameof(TutorSystem.AdaptiveTrainingEnabled), MethodType.Getter)]
    static class DisableAdaptiveLearningPatch
    {
        static bool Prefix() => Multiplayer.Client == null;
    }

    // Affects both reading and writing
    [EarlyPatch]
    [HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.GetSettingsFilename))]
    static class OverrideConfigsPatch
    {
        static void Postfix(string modIdentifier, string modHandleName, ref string __result)
        {
            if (!Multiplayer.restartConfigs)
                return;

            var mod = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m =>
                    m.FolderName == modIdentifier
                    && m.assemblies.loadedAssemblies.Any(a => a.GetTypes().Any(t => t.Name == modHandleName))
                );

            if (mod == null)
                return;

            // Example: MultiplayerTempConfigs/rwmt.multiplayer-Multiplayer
            var newPath = Path.Combine(
                GenFilePaths.FolderUnderSaveData(JoinData.TempConfigsDir),
                GenText.SanitizeFilename(mod.PackageIdPlayerFacing.ToLowerInvariant() + "-" + modHandleName)
            );

            if (File.Exists(newPath))
            {
                __result = newPath;
            }
        }
    }

    [EarlyPatch]
    [HarmonyPatch]
    static class HugsLib_OverrideConfigsPatch
    {
        private static MethodInfo MethodToPatch = AccessTools.Method("HugsLib.Core.PersistentDataManager:GetSettingsFilePath");

        static bool Prepare() => MethodToPatch != null;

        static MethodInfo TargetMethod() => MethodToPatch;

        static void Prefix(object __instance)
        {
            if (!Multiplayer.restartConfigs)
                return;

            if (__instance.GetType().Name != "ModSettingsManager")
                return;

            var newPath = Path.Combine(
                GenFilePaths.FolderUnderSaveData(JoinData.TempConfigsDir),
                GenText.SanitizeFilename($"{JoinData.HugsLibId}-{JoinData.HugsLibSettingsFile}")
            );

            if (File.Exists(newPath))
            {
                __instance.SetPropertyOrField("OverrideFilePath", newPath);
            }
        }
    }
}
