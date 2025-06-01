using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
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

        [HarmonyPatch(typeof(WorldPresets), nameof(WorldPresets.LocalWorld))]
        class LocalHomeAlternateFilePatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(
                IEnumerable<CodeInstruction> codes
            )
            {
                foreach (var code in codes)
                {
                    yield return code;
                    if (code.Is(OpCodes.Ldstr, "Local.bin"))
                    {
                        yield return new(
                            OpCodes.Call,
                            typeof(LocalHomeAlternateFilePatch).GetMethod(nameof(GetValidHomePath))
                        );
                    }
                }
            }

            public static string GetValidHomePath(string orig)
            {
                string dataPath = Path.Combine(Engine.Current.DataPath, "LocalHome.bin");

                if (!File.Exists(dataPath))
                {
                    string originalPath = Path.Combine(
                        Engine.Current.AppPath,
                        "RuntimeData",
                        "Local.bin"
                    );
                    if (File.Exists(originalPath))
                    {
                        File.Copy(originalPath, dataPath);
                        Msg("Copied default Local.bin to LocalHome.bin");
                    }
                    else
                    {
                        Msg("Default Local.bin not found. Using fallback.");
                    }
                }

                if (File.Exists(dataPath))
                    return dataPath;

                return orig;
            }
        }

        [HarmonyPatch(typeof(Userspace), nameof(Userspace.OpenLocalHomeAsync))]
        class ChangeLocalHomePositionPatch
        {
            public static bool Prefix(ref Task __result)
            {
                if (!config.GetValue(KEY_ENABLE))
                    return true;

                __result = Task.Run(() =>
                {
                    var world = Userspace.StartUtilityWorld(WorldPresets.LocalHome());
                    world.AssignNewRecord("M-" + world.Engine.LocalDB.MachineID, "R-Home");
                    world.CorrespondingRecord.Name = "Local";
                    world.Name = world.CorrespondingRecord.Name;
                    return;
                });

                return false;
            }
        }
    }
}