using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zlib;
using Multiplayer.Client.EarlyPatches;
using Multiplayer.Common;
using RimWorld;
using Steamworks;
using UnityEngine;
using Verse;

namespace Multiplayer.Client
{
    [HotSwappable]
    public static class JoinData
    {
        public static byte[] WriteServerData()
        {
            var data = new ByteWriter();

            data.WriteInt32(activeModsSnapshot.Count());
            foreach (var m in activeModsSnapshot)
            {
                data.WriteString(m.PackageIdNonUnique);
                data.WriteString(m.Name);
                data.WriteULong((ulong)m.GetPublishedFileId());
                data.WriteByte((byte)m.Source);
            }

            data.WriteInt32(modFilesSnapshot.Count());
            foreach (var files in modFilesSnapshot)
            {
                data.WriteString(files.Key);
                data.WriteInt32(files.Value.Count);

                foreach (var file in files.Value.Values)
                {
                    data.WriteString(file.relPath);
                    data.WriteInt32(file.hash);
                }
            }

            var configs = GetSyncableConfigContents(activeModsSnapshot.Select(m => m.PackageIdNonUnique));

            data.WriteInt32(configs.Count);
            foreach (var config in configs)
            {
                data.WriteString(config.ModId);
                data.WriteString(config.FileName);
                data.WriteString(config.Contents);
            }

            return GZipStream.CompressBuffer(data.ToArray());
        }

        public static void ReadServerData(byte[] compressedData, RemoteData remoteInfo)
        {
            var data = new ByteReader(GZipStream.UncompressBuffer(compressedData));

            var modCount = data.ReadInt32();
            for (int i = 0; i < modCount; i++)
            {
                var packageId = data.ReadString();
                var name = data.ReadString();
                var steamId = data.ReadULong();
                var source = (ContentSource)data.ReadByte();

                remoteInfo.remoteMods.Add(new ModInfo() { packageId = packageId, name = name, steamId = steamId, source = source });
            }

            var rootCount = data.ReadInt32();
            for (int i = 0; i < rootCount; i++)
            {
                var modId = data.ReadString();
                var mod = GetInstalledMod(modId);
                var fileCount = data.ReadInt32();

                for (int j = 0; j < fileCount; j++)
                {
                    var relPath = data.ReadString();
                    var hash = data.ReadInt32();
                    //hash++;// todo for testing
                    string absPath = null;

                    if (mod != null)
                        absPath = Path.Combine(mod.RootDir.FullName, relPath);

                    remoteInfo.remoteFiles.Add(modId, new ModFile(absPath, relPath, hash));
                }
            }

            var configCount = data.ReadInt32();
            for (int i = 0; i < configCount; i++)
            {
                const int MaxConfigContentLen = 8388608; // 8 megabytes

                var modId = data.ReadString();
                var fileName = data.ReadString();
                var contents = data.ReadString(MaxConfigContentLen);

                remoteInfo.remoteModConfigs.Add(new ModConfig(modId, fileName, contents));
                //remoteInfo.remoteModConfigs[trimmedPath] = remoteInfo.remoteModConfigs[trimmedPath].Insert(0, "a"); // todo for testing
            }
        }

        public static ModMetaData GetInstalledMod(string id)
        {
            if (ModsConfig.IsActive(id + ModMetaData.SteamModPostfix))
                id += ModMetaData.SteamModPostfix;

            return ModLister.GetModWithIdentifier(id);
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        private static string[] ignoredConfigsModIds =
        {
            // The old mod management code also included TacticalGroupsMod.xml and GraphicSetter.xml but I couldn't find their ids
            // todo unhardcode it
            "rwmt.multiplayer",
            "hodlhodl.twitchtoolkit", // contains username
            "dubwise.dubsmintmenus",
            "dubwise.dubsmintminimap",
            "arandomkiwi.rimthemes",
            "brrainz.cameraplus",
            "giantspacehamster.moody",
            "fluffy.modmanager",
            "jelly.modswitch",
            "betterscenes.rimconnect", // contains secret key for streamer
            "jaxe.rimhud"
        };

        public const string TempConfigsDir = "MultiplayerTempConfigs";
        public const string HugsLibId = "unlimitedhugs.hugslib";
        public const string HugsLibSettingsFile = "ModSettings";

        public static List<ModConfig> GetSyncableConfigContents(IEnumerable<string> modIds)
        {
            var list = new List<ModConfig>();

            foreach (var modId in modIds)
            {
                if (ignoredConfigsModIds.Contains(modId)) continue;

                var mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageIdPlayerFacing.ToLowerInvariant() == modId);
                if (mod == null) continue;

                foreach (var modInstance in LoadedModManager.runningModClasses.Values)
                {
                    if (!mod.assemblies.loadedAssemblies.Contains(modInstance.GetType().Assembly)) continue;

                    var instanceName = modInstance.GetType().Name;
                    var file = LoadedModManager.GetSettingsFilename(mod.FolderName, instanceName);

                    if (File.Exists(file))
                        list.Add(new ModConfig(modId, instanceName, File.ReadAllText(file)));
                }
            }

            // Special case for HugsLib
            if (modIds.Contains("unlimitedhugs.hugslib") && GetInstalledMod("unlimitedhugs.hugslib") is { Active: true })
            {
                var hugsConfig = Path.Combine(GenFilePaths.SaveDataFolderPath, "HugsLib", "ModSettings.xml");
                if (File.Exists(hugsConfig))
                    list.Add(new ModConfig(HugsLibId, HugsLibSettingsFile, File.ReadAllText(hugsConfig)));
            }

            return list;
        }

        public static bool CompareToLocal(RemoteData remote)
        {
            var remoteMods = remote.remoteMods.Select(m => (m.packageId, m.source));
            var localMods = activeModsSnapshot.Select(m => (m.PackageIdNonUnique, m.Source));

            return
                remote.remoteRwVersion == VersionControl.CurrentVersionString &&
                remoteMods.SequenceEqual(localMods) &&
                remote.remoteFiles.DictsEqual(modFilesSnapshot) &&
                remote.remoteModConfigs.EqualAsSets(GetSyncableConfigContents(remote.RemoteModIds));
        }

        public static List<ModMetaData> activeModsSnapshot;
        public static ModFileDict modFilesSnapshot;

        internal static void TakeModDataSnapshot()
        {
            activeModsSnapshot = ModsConfig.ActiveModsInLoadOrder.ToList();
            modFilesSnapshot = GetModFiles(activeModsSnapshot.Select(m => m.PackageIdNonUnique));
        }

        internal static ModFileDict GetModFiles(IEnumerable<string> modIds)
        {
            var fileDict = new ModFileDict();

            foreach (var modId in modIds)
            {
                var mod = GetInstalledMod(modId);
                if (mod == null || !mod.RootDir.Exists) continue;

                // The constructor calls InitLoadFolders
                var contentPack = MultiplayerData.DummyContentPack(mod);

                foreach (var asm in MultiplayerData.GetModAssemblies(contentPack))
                {
                    var relPath = asm.FullName.IgnorePrefix(contentPack.RootDir).NormalizePath();
                    fileDict.Add(modId, new ModFile(asm.FullName, relPath, asm.CRC32()));
                }

                foreach (var xmlFile in GetModDefsAndPatches(contentPack))
                {
                    var relPath = xmlFile.FullName.IgnorePrefix(contentPack.RootDir).NormalizePath();
                    fileDict.Add(modId, new ModFile(xmlFile.FullName, relPath, xmlFile.CRC32()));
                }
            }

            return fileDict;
        }

        public static IEnumerable<FileInfo> GetModDefsAndPatches(ModContentPack mod)
        {
            foreach (var f in ModContentPack.GetAllFilesForModPreserveOrder(mod, "Defs/", f => f.ToLower() == ".xml"))
                yield return f.Item2;

            foreach (var f in ModContentPack.GetAllFilesForModPreserveOrder(mod, "Patches/", f => f.ToLower() == ".xml"))
                yield return f.Item2;
        }
    }

    public class RemoteData
    {
        public string remoteRwVersion;
        public string remoteMpVersion;

        public List<ModInfo> remoteMods = new();
        public ModFileDict remoteFiles = new();
        public List<ModConfig> remoteModConfigs = new();

        public IEnumerable<string> RemoteModIds => remoteMods.Select(m => m.packageId);

        public string remoteAddress;
        public int remotePort;
        public CSteamID? remoteSteamHost;
    }

    public record ModConfig(string ModId, string FileName, string Contents);

    public class ModFileDict : IEnumerable<KeyValuePair<string, Dictionary<string, ModFile>>>
    {
        // Mod id => (path => file)
        private Dictionary<string, Dictionary<string, ModFile>> files = new();

        public void Add(string mod, ModFile file){
            files.GetOrAddNew(mod)[file.relPath] = file;
        }

        public ModFile? GetOrDefault(string mod, string relPath){
            if (files.TryGetValue(mod, out var modFiles) && modFiles.TryGetValue(relPath, out var file))
                return file;
            return null;
        }

        public bool DictsEqual(ModFileDict other)
        {
            return files.Keys.EqualAsSets(other.files.Keys) &&
                files.All(kv => kv.Value.Values.EqualAsSets(kv.Value.Values));
        }

        public IEnumerator<KeyValuePair<string, Dictionary<string, ModFile>>> GetEnumerator()
        {
            return files.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return files.GetEnumerator();
        }

        public ModFileDict CopyWithMods(IEnumerable<string> modIds)
        {
            var dict = new ModFileDict();

            foreach (var modId in modIds)
                if (files.TryGetValue(modId, out var ofMod))
                    dict.files[modId] = ofMod;

            return dict;
        }
    }

    public struct ModInfo
    {
        public string packageId; // Mod package id with no _steam suffix
        public string name;
        public ulong steamId; // Zero means invalid
        public ContentSource source;

        public bool Installed =>
            JoinData.GetInstalledMod(packageId) is { } m
            && (!m.OnSteamWorkshop || m.SteamModInstalled());

        public bool CanSubscribe => steamId != 0;
    }

    public struct ModFile
    {
        public string absPath; // Can be null on the remote side
        public string relPath;
        public int hash;

        public ModFile(string absPath, string relPath, int hash)
        {
            this.absPath = absPath?.NormalizePath();
            this.relPath = relPath.NormalizePath();
            this.hash = hash;
        }
    }
}
