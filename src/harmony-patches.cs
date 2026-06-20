using HarmonyLib;
using UnityEngine;

namespace CustomResolutionList;

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "RefreshResolutionsStandalone", typeof(Il2CppTLD.SaveState.GraphicsMode))]
internal static class RefreshResolutionsStandalonePatch
{
    private static void Prefix(Il2Cpp.Panel_OptionsMenu __instance, out SelectionBasis __state)
    {
        __state = CustomResolutionController.CaptureSelection(__instance);
    }

    private static void Postfix(Il2Cpp.Panel_OptionsMenu __instance, SelectionBasis __state)
    {
        CustomResolutionController.ApplyToPanel(__instance, "RefreshResolutionsStandalone", __state);
    }
}

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "RefreshResolutions", typeof(Il2CppTLD.SaveState.GraphicsMode))]
internal static class RefreshResolutionsPatch
{
    private static void Prefix(Il2Cpp.Panel_OptionsMenu __instance, out SelectionBasis __state)
    {
        __state = CustomResolutionController.CaptureSelection(__instance);
    }

    private static void Postfix(Il2Cpp.Panel_OptionsMenu __instance, SelectionBasis __state)
    {
        CustomResolutionController.ApplyToPanel(__instance, "RefreshResolutions", __state);
    }
}

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "ApplyGraphicsModeAndResolution", typeof(bool))]
internal static class ApplyGraphicsModeAndResolutionPatch
{
    private static bool Prefix(Il2Cpp.Panel_OptionsMenu __instance)
    {
        return !CustomResolutionController.TryApplySelectedCustomResolution(__instance);
    }
}

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "GetScreenResolutionIndexFromString", typeof(string))]
internal static class GetScreenResolutionIndexFromStringPatch
{
    private static bool Prefix(Il2Cpp.Panel_OptionsMenu __instance, string text, ref int __result)
    {
        if (!CustomResolutionController.TryGetCustomResolutionIndex(__instance, text, ref __result))
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "IsResolutionSupported", typeof(Il2CppTLD.SaveState.GraphicsMode), typeof(int), typeof(int))]
internal static class IsResolutionSupportedPatch
{
    private static bool Prefix(int width, int height, ref bool __result)
    {
        if (!CustomResolutionController.IsAllowedCustomResolution(width, height))
        {
            return true;
        }

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "ResolutionCompatibleWithGraphicsMode", typeof(Il2CppTLD.SaveState.GraphicsMode), typeof(int), typeof(int))]
internal static class ResolutionCompatibleWithGraphicsModePatch
{
    private static bool Prefix(int width, int height, ref bool __result)
    {
        if (!CustomResolutionController.IsAllowedCustomResolution(width, height))
        {
            return true;
        }

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "GetClosestSupportedResolution", typeof(Il2CppTLD.SaveState.GraphicsMode), typeof(int), typeof(int))]
internal static class GetClosestSupportedResolutionPatch
{
    private static bool Prefix(int width, int height, ref Resolution __result)
    {
        if (!CustomResolutionController.TryCreateAllowedResolution(width, height, out __result))
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(Il2Cpp.Panel_OptionsMenu), "GetHighestCompatibleResolutionForGraphicsMode", typeof(Il2CppTLD.SaveState.GraphicsMode))]
internal static class GetHighestCompatibleResolutionForGraphicsModePatch
{
    private static bool Prefix(Il2Cpp.Panel_OptionsMenu __instance, ref Resolution __result)
    {
        if (!CustomResolutionController.TryGetSelectedCustomResolution(__instance, out __result))
        {
            return true;
        }

        return false;
    }
}