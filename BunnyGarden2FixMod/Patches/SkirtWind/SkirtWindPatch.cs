using GB.Scene;
using HarmonyLib;

namespace BunnyGarden2FixMod.Patches.SkirtWind;

[HarmonyPatch(typeof(CharacterHandle), nameof(CharacterHandle.setup))]
public static class SkirtWindPatch
{
    private static void Postfix(CharacterHandle __instance)
    {
        SkirtWindApplier.Attach(__instance);
    }
}