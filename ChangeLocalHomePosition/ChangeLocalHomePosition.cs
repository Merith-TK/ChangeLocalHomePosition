using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.Store;
using HarmonyLib;
using ResoniteModLoader;

namespace ChangeLocalHomePosition
{
    public class ChangeLocalHomePosition : ResoniteMod
    {
        public override string Name => "ChangeLocalHomePosition";
        public override string Author => "merith.tk";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/Merith-TK/ChangeLocalHomePosition/";

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> KEY_ENABLE = new(
            "enable",
            "If true local home will be loaded from a custom file.",
            () => true
        );

        public static ModConfiguration config;

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            new Harmony("xyz.merith.ChangeLocalHomePosition").PatchAll();
        }

        public static void Msg(string message) =>
            UniLog.Log("[ChangeLocalHomePosition] " + message);

        [HarmonyPatch(typeof(WorldPresets), nameof(WorldPresets.LocalWorld))]
        public static class LocalWorldOverridePatch
        {
            public static bool Prefix(World w)
            {
                if (!config.GetValue(KEY_ENABLE))
                    return true; // Run original method

                string customPath = Path.Combine(w.Engine.DataPath, "LocalHome.bin");

                if (!File.Exists(customPath))
                {
                    string originalPath = Path.Combine(
                        w.Engine.AppPath,
                        "RuntimeData",
                        "Local.bin"
                    );
                    if (File.Exists(originalPath))
                    {
                        File.Copy(originalPath, customPath);
                        Msg("Copied default Local.bin to LocalHome.bin");
                    }
                    else
                    {
                        Msg("Default Local.bin not found. Cannot load LocalHome.");
                        return false;
                    }
                }

                try
                {
                    using FileStream stream = File.OpenRead(customPath);
                    DataTreeDictionary node = DataTreeConverter.LoadAuto(stream);
                    LoadControl loadControl = new LoadControl(
                        w,
                        new ReferenceTranslator(),
                        Engine.Version,
                        null
                    );
                    loadControl.SetLoadRoot(w);
                    w.Load(node, loadControl);
                    Msg("Successfully loaded LocalHome.bin.");
                }
                catch (Exception ex)
                {
                    UniLog.Error(
                        $"[ChangeLocalHomePosition] Failed to load custom LocalHome: {ex}"
                    );
                }

                return false; // Skip original method
            }
        }

        [HarmonyPatch(typeof(Userspace), nameof(Userspace.OpenLocalHomeAsync))]
        public static class ChangeLocalHomePositionPatch
        {
            public static bool Prefix(ref Task __result)
            {
                if (!config.GetValue(KEY_ENABLE))
                    return true;

                __result = Task.Run(() =>
                {
                    // TODO: Potentially create a custom "preset" for the local home?
                    // the reasoning is that the preset here just direct loads from Local.bin
                    // so in theory?
                    var world = Userspace.StartUtilityWorld(CustomLocalWorld());
                    world.AssignNewRecord("M-" + world.Engine.LocalDB.MachineID, "R-Home");
                    world.CorrespondingRecord.Name = "Local";
                    world.Name = world.CorrespondingRecord.Name;
                    return;
                });

                return false;
            }
        }

        // Taken from the original LocalWorld method
        [HarmonyPatch(typeof(WorldPresets), nameof(WorldPresets.LocalWorld))]
        public static void CustomLocalWorld(World w)
        {
            using FileStream stream = File.OpenRead(Path.Combine(w.Engine.DataPath, "LocalHome.bin"));
            DataTreeDictionary node = DataTreeConverter.LoadAuto(stream);
            LoadControl loadControl = new LoadControl(w, new ReferenceTranslator(), Engine.Version, null);
            loadControl.SetLoadRoot(w);
            w.Load(node, loadControl);
        }
    }
}
