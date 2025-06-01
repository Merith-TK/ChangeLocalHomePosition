using System;
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
        public static ModConfigurationKey<bool> ENABLE = new(
            "enable",
            "If true, LocalHome.bin will be used instead of Local.bin.",
            () => true
        );

        public static ModConfiguration config;

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            new Harmony("xyz.merith.ChangeLocalHomePosition").PatchAll();
        }

        [HarmonyPatch(typeof(WorldPresets), nameof(WorldPresets.LocalWorld))]
        public static class LocalWorldPatch
        {
            public static bool Prefix(World w)
            {
                if (!config.GetValue(ENABLE))
                    return true;

                string localHomePath = Path.Combine(w.Engine.DataPath, "LocalHome.bin");

                if (!File.Exists(localHomePath))
                {
                    string fallback = Path.Combine(w.Engine.AppPath, "RuntimeData", "Local.bin");
                    if (File.Exists(fallback))
                    {
                        File.Copy(fallback, localHomePath);
                        Msg("Created LocalHome.bin from fallback Local.bin.");
                    }
                    else
                    {
                        Msg("No LocalHome.bin or fallback Local.bin found.");
                        return false;
                    }
                }

                try
                {
                    using var stream = File.OpenRead(localHomePath);
                    var node = DataTreeConverter.LoadAuto(stream);
                    var loadControl = new LoadControl(
                        w,
                        new ReferenceTranslator(),
                        Engine.Version,
                        null
                    );
                    loadControl.SetLoadRoot(w);
                    w.Load(node, loadControl);
                    Msg("Loaded world from LocalHome.bin.");
                }
                catch (Exception ex)
                {
                    UniLog.Error($"[LocalHomeMod] Failed to load: {ex}");
                    return false;
                }

                return false; // Skip original method
            }
        }

        [HarmonyPatch(typeof(Userspace), nameof(Userspace.OpenLocalHomeAsync))]
        public static class OpenLocalHomePatch
        {
            public static bool Prefix(ref Task __result)
            {
                if (!config.GetValue(ENABLE))
                    return true;

                __result = Task.Run(() =>
                {
                    var world = Userspace.StartUtilityWorld(WorldPresets.LocalHome());
                    world.AssignNewRecord("M-" + world.Engine.LocalDB.MachineID, "R-Home");
                    world.CorrespondingRecord.Name = "Local";
                    world.Name = "Local";
                });

                return false;
            }
        }

        // Taken from the original LocalWorld method, plan is to replace the
        // `WorldPresets.LocalWorld()` call with this method once I figure out how the fuck thsi works
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
