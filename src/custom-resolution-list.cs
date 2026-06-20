using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace CustomResolutionList;

public sealed class CustomResolutionListMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        CustomResolutionController.Configure(
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));

        new HarmonyLib.Harmony("custom-resolution-list").PatchAll();

        CustomResolutionController.OnModInitialized();
        CustomResolutionController.WriteMessage("Custom Resolution List initialized.");
    }

    public override void OnUpdate()
    {
        CustomResolutionController.OnUpdate();
    }
}

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

internal static class CustomResolutionController
{
    private const int MinimumWidth = 640;
    private const int MinimumHeight = 360;
    private const int MaximumEntries = 32;
    private const int ExactScaleStep = 4;
    private const int ApproximateWidthStep = 64;

    private static readonly List<Resolution> CachedResolutions = new(MaximumEntries);
    private static Action<string> MessageWriter = _ => { };
    private static Action<string> WarningWriter = _ => { };
    private static int CachedNativeWidth;
    private static int CachedNativeHeight;
    private static int LastNativeWidth;
    private static int LastNativeHeight;
    private static int LastLoggedNativeWidth;
    private static int LastLoggedNativeHeight;
    private static int LastLoggedSelectedIndex = -1;
    private static int LastLoggedCount;
    private static string LastLoggedSourceMethod = string.Empty;
    private static string StorageDirectoryPath = string.Empty;
    private static string StoredResolutionFilePath = string.Empty;
    private static PersistedResolution StoredResolution;

    public static void Configure(Action<string> messageWriter, Action<string> warningWriter)
    {
        MessageWriter = messageWriter ?? MessageWriter;
        WarningWriter = warningWriter ?? WarningWriter;
    }

    public static void OnModInitialized()
    {
        InitializeStorage();
        LoadStoredResolution();
        ApplyStoredResolutionIfNeeded();
    }

    public static void OnUpdate()
    {
        ApplyStoredResolutionIfNeeded();
    }

    public static SelectionBasis CaptureSelection(Il2Cpp.Panel_OptionsMenu panel)
    {
        if (panel == null)
        {
            return default;
        }

        if (TryCaptureExplicitComboSelection(panel, out SelectionBasis comboSelection))
        {
            return comboSelection;
        }

        if (TryCaptureStartupSelection(panel, out SelectionBasis startupSelection))
        {
            return startupSelection;
        }

        if (TryCaptureCurrentScreenSelection(out SelectionBasis screenSelection))
        {
            return screenSelection;
        }

        return default;
    }

    public static void ApplyToPanel(Il2Cpp.Panel_OptionsMenu panel, string sourceMethod, SelectionBasis selectionBeforeRefresh)
    {
        if (panel == null)
        {
            return;
        }

        try
        {
            DisplayBasis displayBasis = GetDisplayBasis(panel);

            if (!displayBasis.IsValid)
            {
                WriteWarning("No valid display basis was available.");
                return;
            }

            List<Resolution> customResolutions = GenerateResolutions(displayBasis.Width, displayBasis.Height);

            if (customResolutions.Count == 0)
            {
                WriteWarning("No custom resolutions were generated.");
                return;
            }

            SelectionBasis effectiveSelection = selectionBeforeRefresh;

            if (!effectiveSelection.IsExplicit && StoredResolution.IsValid)
            {
                int storedIndex = FindExactResolutionIndex(customResolutions, StoredResolution.Width, StoredResolution.Height);

                if (storedIndex >= 0)
                {
                    effectiveSelection = new SelectionBasis(StoredResolution.Width, StoredResolution.Height, true, false);
                }
            }

            if (!effectiveSelection.IsValid && !TryCaptureCurrentScreenSelection(out effectiveSelection))
            {
                effectiveSelection = new SelectionBasis(displayBasis.Width, displayBasis.Height, true, false);
            }

            int selectedIndex = FindBestSelectionIndex(customResolutions, effectiveSelection);
            ReplaceResolutionList(panel, customResolutions);
            ReplaceResolutionComboBox(panel, customResolutions, selectedIndex);

            LastNativeWidth = displayBasis.Width;
            LastNativeHeight = displayBasis.Height;

            Resolution selectedResolution = customResolutions[selectedIndex];

            if (StoredResolution.IsValid && selectedResolution.width == StoredResolution.Width && selectedResolution.height == StoredResolution.Height)
            {
                ApplyStoredResolutionIfNeeded();
            }

            if (ShouldWriteApplyMessage(sourceMethod, displayBasis, customResolutions.Count, selectedIndex))
            {
                WriteMessage("Applied " + customResolutions.Count.ToString(CultureInfo.InvariantCulture) + " same-aspect resolutions from " + displayBasis.Width.ToString(CultureInfo.InvariantCulture) + "x" + displayBasis.Height.ToString(CultureInfo.InvariantCulture) + " after " + sourceMethod + ". Selected " + FormatResolutionText(selectedResolution) + ".");
            }
        }
        catch (Exception exception)
        {
            WriteWarning("Failed to apply custom resolution list: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static bool TryApplySelectedCustomResolution(Il2Cpp.Panel_OptionsMenu panel)
    {
        if (panel == null)
        {
            return false;
        }

        if (!TryCaptureExplicitComboSelection(panel, out SelectionBasis selectedResolution))
        {
            return false;
        }

        if (IsStockScreenResolution(selectedResolution.Width, selectedResolution.Height))
        {
            ClearStoredResolution();
            QueueResolutionSynchronization(selectedResolution.Width, selectedResolution.Height);
            return false;
        }

        DisplayBasis displayBasis = GetDisplayBasis(panel);

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = GenerateResolutions(displayBasis.Width, displayBasis.Height);
        int selectedIndex = FindExactResolutionIndex(customResolutions, selectedResolution.Width, selectedResolution.Height);

        if (selectedIndex < 0)
        {
            return false;
        }

        GraphicsModeSelection graphicsModeSelection = GetGraphicsModeSelection(panel);

        StoreResolution(selectedResolution.Width, selectedResolution.Height, graphicsModeSelection);
        ApplyScreenResolution(selectedResolution.Width, selectedResolution.Height, graphicsModeSelection);
        RestoreSelectedResolution(panel, customResolutions, selectedIndex);
        QueueScreenResolutionReapply(selectedResolution.Width, selectedResolution.Height, graphicsModeSelection);

        WriteMessage("Applied custom resolution directly: " + selectedResolution.Width.ToString(CultureInfo.InvariantCulture) + "x" + selectedResolution.Height.ToString(CultureInfo.InvariantCulture) + " using " + graphicsModeSelection.ToString() + ".");

        return true;
    }

    public static bool TryGetCustomResolutionIndex(Il2Cpp.Panel_OptionsMenu panel, string text, ref int result)
    {
        SelectionBasis parsedSelection = ParseSelectionText(text);

        if (!parsedSelection.IsValid)
        {
            return false;
        }

        DisplayBasis displayBasis = GetDisplayBasis(panel);

        if (!displayBasis.IsValid)
        {
            displayBasis = GetPrimaryDisplayBasis();
        }

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = GenerateResolutions(displayBasis.Width, displayBasis.Height);
        int index = FindExactResolutionIndex(customResolutions, parsedSelection.Width, parsedSelection.Height);

        if (index < 0)
        {
            return false;
        }

        result = index;
        return true;
    }

    public static bool IsAllowedCustomResolution(int width, int height)
    {
        return TryCreateAllowedResolution(width, height, out _);
    }

    public static bool TryCreateAllowedResolution(int width, int height, out Resolution result)
    {
        result = default;

        if (width < MinimumWidth || height < MinimumHeight)
        {
            return false;
        }

        DisplayBasis displayBasis = GetPrimaryDisplayBasis();

        if (!displayBasis.IsValid && LastNativeWidth > 0 && LastNativeHeight > 0)
        {
            displayBasis = new DisplayBasis(LastNativeWidth, LastNativeHeight);
        }

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = GenerateResolutions(displayBasis.Width, displayBasis.Height);
        int index = FindExactResolutionIndex(customResolutions, width, height);

        if (index < 0)
        {
            return false;
        }

        result = customResolutions[index];
        return true;
    }

    public static bool TryGetSelectedCustomResolution(Il2Cpp.Panel_OptionsMenu panel, out Resolution result)
    {
        result = default;

        if (panel == null)
        {
            return false;
        }

        if (!TryCaptureExplicitComboSelection(panel, out SelectionBasis selection))
        {
            return false;
        }

        DisplayBasis displayBasis = GetDisplayBasis(panel);

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = GenerateResolutions(displayBasis.Width, displayBasis.Height);
        int index = FindExactResolutionIndex(customResolutions, selection.Width, selection.Height);

        if (index < 0)
        {
            return false;
        }

        result = customResolutions[index];
        return true;
    }

    private static bool TryCaptureExplicitComboSelection(Il2Cpp.Panel_OptionsMenu panel, out SelectionBasis selection)
    {
        selection = default;

        try
        {
            Il2Cpp.ConsoleComboBox comboBox = panel.m_GraphicsResolutionPopupList;

            if (comboBox == null)
            {
                return false;
            }

            selection = ParseSelectionText(comboBox.value);

            if (selection.IsValid)
            {
                selection = new SelectionBasis(selection.Width, selection.Height, true, true);
                return true;
            }

            selection = ParseSelectionText(comboBox.m_SelectedItem);

            if (selection.IsValid)
            {
                selection = new SelectionBasis(selection.Width, selection.Height, true, true);
                return true;
            }

            return false;
        }
        catch
        {
            selection = default;
            return false;
        }
    }

    private static bool TryCaptureStartupSelection(Il2Cpp.Panel_OptionsMenu panel, out SelectionBasis selection)
    {
        selection = default;

        try
        {
            Resolution startupResolution = panel.m_ResolutionAtStartup;

            if (startupResolution.width <= 0 || startupResolution.height <= 0)
            {
                return false;
            }

            selection = new SelectionBasis(startupResolution.width, startupResolution.height, true, false);
            return true;
        }
        catch
        {
            selection = default;
            return false;
        }
    }

    private static bool TryCaptureCurrentScreenSelection(out SelectionBasis selection)
    {
        Resolution currentResolution = Screen.currentResolution;

        if (currentResolution.width > 0 && currentResolution.height > 0)
        {
            selection = new SelectionBasis(currentResolution.width, currentResolution.height, true, false);
            return true;
        }

        if (Screen.width > 0 && Screen.height > 0)
        {
            selection = new SelectionBasis(Screen.width, Screen.height, true, false);
            return true;
        }

        selection = default;
        return false;
    }

    private static GraphicsModeSelection GetGraphicsModeSelection(Il2Cpp.Panel_OptionsMenu panel)
    {
        try
        {
            Il2Cpp.ConsoleComboBox comboBox = panel.m_GraphicsModePopupList;

            if (comboBox == null)
            {
                return GraphicsModeSelection.Borderless;
            }

            string text = comboBox.value;

            if (string.IsNullOrWhiteSpace(text))
            {
                text = comboBox.m_SelectedItem;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return GraphicsModeSelection.Borderless;
            }

            if (text.Contains("Window", StringComparison.OrdinalIgnoreCase))
            {
                return GraphicsModeSelection.Windowed;
            }

            return GraphicsModeSelection.Borderless;
        }
        catch
        {
            return GraphicsModeSelection.Borderless;
        }
    }

    private static void ApplyStoredResolutionIfNeeded()
    {
        if (!StoredResolution.IsValid)
        {
            return;
        }

        if (ScreenMatchesResolution(StoredResolution.Width, StoredResolution.Height, StoredResolution.GraphicsModeSelection))
        {
            return;
        }

        if (!IsGeneratedCustomResolutionForCurrentDisplay(StoredResolution.Width, StoredResolution.Height))
        {
            return;
        }

        QueueScreenResolutionReapply(StoredResolution.Width, StoredResolution.Height, StoredResolution.GraphicsModeSelection);
    }

    private static void ApplyScreenResolution(int width, int height, GraphicsModeSelection graphicsModeSelection)
    {
        if (graphicsModeSelection == GraphicsModeSelection.Windowed)
        {
            Screen.SetResolution(width, height, false);
            return;
        }

        Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
    }

    private static void QueueScreenResolutionReapply(int width, int height, GraphicsModeSelection graphicsModeSelection)
    {
        MelonCoroutines.Start(ReapplyScreenResolutionAfterFrames(width, height, graphicsModeSelection));
    }

    private static IEnumerator ReapplyScreenResolutionAfterFrames(int width, int height, GraphicsModeSelection graphicsModeSelection)
    {
        yield return null;
        ApplyScreenResolution(width, height, graphicsModeSelection);
        yield return null;
        ApplyScreenResolution(width, height, graphicsModeSelection);
        
        QueueResolutionSynchronization(width, height);
    }

    private static void QueueResolutionSynchronization(int width, int height)
    {
        MelonCoroutines.Start(SynchronizeDynamicResolutionHelperAfterFrames(width, height));
    }

    private static IEnumerator SynchronizeDynamicResolutionHelperAfterFrames(int width, int height)
    {
        yield return null;
        yield return null;
        yield return null;

        try
        {
            Il2CppTLD.Rendering.DynamicResolutionHelper.MaxWidth = width;
            Il2CppTLD.Rendering.DynamicResolutionHelper.MaxHeight = height;
            Il2CppTLD.Rendering.DynamicResolutionHelper.RenderWidth = width;
            Il2CppTLD.Rendering.DynamicResolutionHelper.RenderHeight = height;

            Il2CppTLD.Rendering.DynamicResolutionHelper.UpdateDynamicResolution();
            Il2CppTLD.Rendering.DynamicResolutionHelper.UpdateResolutionScale();
        }
        catch (Exception exception)
        {
            WriteWarning("Failed to synchronize internal render resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static bool ScreenMatchesResolution(int width, int height, GraphicsModeSelection graphicsModeSelection)
    {
        if (Screen.width != width || Screen.height != height)
        {
            return false;
        }

        if (graphicsModeSelection == GraphicsModeSelection.Windowed)
        {
            return !Screen.fullScreen;
        }

        return Screen.fullScreen && Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
    }

    private static bool IsGeneratedCustomResolutionForCurrentDisplay(int width, int height)
    {
        if (IsStockScreenResolution(width, height))
        {
            return false;
        }

        DisplayBasis displayBasis = GetPrimaryDisplayBasis();

        if (!displayBasis.IsValid && LastNativeWidth > 0 && LastNativeHeight > 0)
        {
            displayBasis = new DisplayBasis(LastNativeWidth, LastNativeHeight);
        }

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = GenerateResolutions(displayBasis.Width, displayBasis.Height);
        return FindExactResolutionIndex(customResolutions, width, height) >= 0;
    }

    private static void RestoreSelectedResolution(Il2Cpp.Panel_OptionsMenu panel, List<Resolution> customResolutions, int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= customResolutions.Count)
        {
            return;
        }

        try
        {
            ReplaceResolutionList(panel, customResolutions);
            ReplaceResolutionComboBox(panel, customResolutions, selectedIndex);
        }
        catch (Exception exception)
        {
            WriteWarning("Failed to restore selected custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static bool IsStockScreenResolution(int width, int height)
    {
        Resolution[] resolutions = Screen.resolutions ?? Array.Empty<Resolution>();

        for (int index = 0; index < resolutions.Length; index++)
        {
            Resolution resolution = resolutions[index];

            if (resolution.width == width && resolution.height == height)
            {
                return true;
            }
        }

        return false;
    }

    private static void ReplaceResolutionList(Il2Cpp.Panel_OptionsMenu panel, List<Resolution> customResolutions)
    {
        Il2CppSystem.Collections.Generic.List<Resolution> targetList = panel.m_Resolutions;

        if (targetList == null)
        {
            WriteWarning("Panel resolution list was null.");
            return;
        }

        targetList.Clear();

        for (int index = 0; index < customResolutions.Count; index++)
        {
            targetList.Add(customResolutions[index]);
        }
    }

    private static void ReplaceResolutionComboBox(Il2Cpp.Panel_OptionsMenu panel, List<Resolution> customResolutions, int selectedIndex)
    {
        Il2Cpp.ConsoleComboBox comboBox = panel.m_GraphicsResolutionPopupList;

        if (comboBox == null)
        {
            WriteWarning("Graphics resolution combo box was null.");
            return;
        }

        Il2CppSystem.Collections.Generic.List<string> items = comboBox.items;

        if (items == null)
        {
            WriteWarning("Graphics resolution combo box item list was null.");
            return;
        }

        items.Clear();

        for (int index = 0; index < customResolutions.Count; index++)
        {
            items.Add(FormatResolutionText(customResolutions[index]));
        }

        if (selectedIndex < 0 || selectedIndex >= customResolutions.Count)
        {
            selectedIndex = customResolutions.Count - 1;
        }

        string selectedText = FormatResolutionText(customResolutions[selectedIndex]);

        comboBox.value = selectedText;
        comboBox.m_CurrentIndex = selectedIndex;
        comboBox.m_SelectedItem = selectedText;
        comboBox.Refresh();
        comboBox.m_CurrentIndex = selectedIndex;
        comboBox.m_SelectedItem = selectedText;
        comboBox.value = selectedText;
    }

    private static int FindBestSelectionIndex(List<Resolution> customResolutions, SelectionBasis selectionBasis)
    {
        if (customResolutions.Count == 0)
        {
            return 0;
        }

        if (!selectionBasis.IsValid)
        {
            return customResolutions.Count - 1;
        }

        int exactIndex = FindExactResolutionIndex(customResolutions, selectionBasis.Width, selectionBasis.Height);

        if (exactIndex >= 0)
        {
            return exactIndex;
        }

        long bestScore = long.MaxValue;
        int bestIndex = customResolutions.Count - 1;

        for (int index = 0; index < customResolutions.Count; index++)
        {
            Resolution resolution = customResolutions[index];
            long widthDelta = resolution.width - selectionBasis.Width;
            long heightDelta = resolution.height - selectionBasis.Height;
            long score = (widthDelta * widthDelta) + (heightDelta * heightDelta);

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static int FindExactResolutionIndex(List<Resolution> resolutions, int width, int height)
    {
        for (int index = 0; index < resolutions.Count; index++)
        {
            Resolution resolution = resolutions[index];

            if (resolution.width == width && resolution.height == height)
            {
                return index;
            }
        }

        return -1;
    }

    private static SelectionBasis ParseSelectionText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        string normalizedText = text.Replace(" ", string.Empty);
        string[] pieces = normalizedText.Split('x', 'X');

        if (pieces.Length != 2)
        {
            return default;
        }

        if (!int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width))
        {
            return default;
        }

        if (!int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
        {
            return default;
        }

        if (width <= 0 || height <= 0)
        {
            return default;
        }

        return new SelectionBasis(width, height, true, false);
    }

    private static DisplayBasis GetDisplayBasis(Il2Cpp.Panel_OptionsMenu panel)
    {
        Display[] displays = Display.displays ?? Array.Empty<Display>();

        if (displays.Length == 0)
        {
            return GetPrimaryDisplayBasis();
        }

        int displayIndex = 0;

        try
        {
            Il2Cpp.ConsoleComboBox displayComboBox = panel != null ? panel.m_DisplayNumberPopupList : null;

            if (displayComboBox != null && displayComboBox.m_CurrentIndex >= 0)
            {
                displayIndex = displayComboBox.m_CurrentIndex;
            }
        }
        catch
        {
            displayIndex = 0;
        }

        if (displayIndex < 0 || displayIndex >= displays.Length)
        {
            displayIndex = 0;
        }

        Display display = displays[displayIndex];

        if (display.systemWidth > 0 && display.systemHeight > 0)
        {
            return new DisplayBasis(display.systemWidth, display.systemHeight);
        }

        return GetPrimaryDisplayBasis();
    }

    private static DisplayBasis GetPrimaryDisplayBasis()
    {
        Display[] displays = Display.displays ?? Array.Empty<Display>();

        if (displays.Length > 0)
        {
            Display display = displays[0];

            if (display.systemWidth > 0 && display.systemHeight > 0)
            {
                return new DisplayBasis(display.systemWidth, display.systemHeight);
            }
        }

        Resolution currentResolution = Screen.currentResolution;

        if (currentResolution.width > 0 && currentResolution.height > 0)
        {
            return new DisplayBasis(currentResolution.width, currentResolution.height);
        }

        if (Screen.width > 0 && Screen.height > 0)
        {
            return new DisplayBasis(Screen.width, Screen.height);
        }

        return default;
    }

    private static List<Resolution> GenerateResolutions(int nativeWidth, int nativeHeight)
    {
        if (CachedNativeWidth == nativeWidth && CachedNativeHeight == nativeHeight && CachedResolutions.Count > 0)
        {
            return CachedResolutions;
        }

        CachedNativeWidth = nativeWidth;
        CachedNativeHeight = nativeHeight;
        CachedResolutions.Clear();

        if (nativeWidth <= 0 || nativeHeight <= 0)
        {
            return CachedResolutions;
        }

        if (nativeWidth < MinimumWidth || nativeHeight < MinimumHeight)
        {
            AddResolution(CachedResolutions, nativeWidth, nativeHeight);
            return CachedResolutions;
        }

        int divisor = GreatestCommonDivisor(nativeWidth, nativeHeight);
        int aspectWidth = nativeWidth / divisor;
        int aspectHeight = nativeHeight / divisor;

        if (aspectWidth <= 256 && aspectHeight <= 256)
        {
            GenerateExactScaleResolutions(CachedResolutions, nativeWidth, nativeHeight, aspectWidth, aspectHeight);
        }
        else
        {
            GenerateApproximateResolutions(CachedResolutions, nativeWidth, nativeHeight);
        }

        AddResolution(CachedResolutions, nativeWidth, nativeHeight);
        CachedResolutions.Sort(CompareResolutionAreaAscending);
        TrimResolutionList(CachedResolutions);

        return CachedResolutions;
    }

    private static void GenerateExactScaleResolutions(List<Resolution> resolutions, int nativeWidth, int nativeHeight, int aspectWidth, int aspectHeight)
    {
        int maxScale = Math.Min(nativeWidth / aspectWidth, nativeHeight / aspectHeight);
        int minScaleFromWidth = DivideRoundUp(MinimumWidth, aspectWidth);
        int minScaleFromHeight = DivideRoundUp(MinimumHeight, aspectHeight);
        int minScale = Math.Max(minScaleFromWidth, minScaleFromHeight);

        for (int scale = minScale; scale <= maxScale; scale += ExactScaleStep)
        {
            int width = aspectWidth * scale;
            int height = aspectHeight * scale;

            if ((width % 2) != 0 || (height % 2) != 0)
            {
                continue;
            }

            AddResolution(resolutions, width, height);
        }

        int minimumWidth = aspectWidth * minScale;
        int minimumHeight = aspectHeight * minScale;

        if (minimumWidth >= MinimumWidth && minimumHeight >= MinimumHeight)
        {
            AddResolution(resolutions, minimumWidth, minimumHeight);
        }
    }

    private static void GenerateApproximateResolutions(List<Resolution> resolutions, int nativeWidth, int nativeHeight)
    {
        for (int width = MinimumWidth; width <= nativeWidth; width += ApproximateWidthStep)
        {
            int height = RoundToEven((int)Math.Round(width * (nativeHeight / (double)nativeWidth)));

            if (height < MinimumHeight || height > nativeHeight)
            {
                continue;
            }

            AddResolution(resolutions, width, height);
        }

        int minimumHeightBasedWidth = RoundToEven((int)Math.Round(MinimumHeight * (nativeWidth / (double)nativeHeight)));

        if (minimumHeightBasedWidth >= MinimumWidth && minimumHeightBasedWidth <= nativeWidth)
        {
            AddResolution(resolutions, minimumHeightBasedWidth, MinimumHeight);
        }
    }

    private static void AddResolution(List<Resolution> resolutions, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        for (int index = 0; index < resolutions.Count; index++)
        {
            Resolution existingResolution = resolutions[index];

            if (existingResolution.width == width && existingResolution.height == height)
            {
                return;
            }
        }

        Resolution resolution = new()
        {
            width = width,
            height = height,
            refreshRate = 0
        };

        resolutions.Add(resolution);
    }

    private static int CompareResolutionAreaAscending(Resolution left, Resolution right)
    {
        long leftArea = (long)left.width * left.height;
        long rightArea = (long)right.width * right.height;
        int areaComparison = leftArea.CompareTo(rightArea);

        if (areaComparison != 0)
        {
            return areaComparison;
        }

        int widthComparison = left.width.CompareTo(right.width);

        if (widthComparison != 0)
        {
            return widthComparison;
        }

        return left.height.CompareTo(right.height);
    }

    private static void TrimResolutionList(List<Resolution> resolutions)
    {
        if (resolutions.Count <= MaximumEntries)
        {
            return;
        }

        List<Resolution> sampledResolutions = new(MaximumEntries);
        int sourceCount = resolutions.Count;

        for (int index = 0; index < MaximumEntries; index++)
        {
            int sourceIndex = (int)Math.Round(index * ((sourceCount - 1) / (double)(MaximumEntries - 1)));
            Resolution resolution = resolutions[sourceIndex];
            AddResolution(sampledResolutions, resolution.width, resolution.height);
        }

        resolutions.Clear();

        for (int index = 0; index < sampledResolutions.Count; index++)
        {
            resolutions.Add(sampledResolutions[index]);
        }
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);

        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }

        return Math.Max(left, 1);
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return (value + divisor - 1) / divisor;
    }

    private static int RoundToEven(int value)
    {
        if ((value % 2) == 0)
        {
            return value;
        }

        return value + 1;
    }

    private static void InitializeStorage()
    {
        StorageDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "CustomResolutionList");
        StoredResolutionFilePath = Path.Combine(StorageDirectoryPath, "custom-resolution-list.cfg");

        try
        {
            Directory.CreateDirectory(StorageDirectoryPath);
        }
        catch (Exception exception)
        {
            WriteWarning("Failed to initialize custom resolution storage: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static void LoadStoredResolution()
    {
        StoredResolution = default;

        if (string.IsNullOrWhiteSpace(StoredResolutionFilePath) || !File.Exists(StoredResolutionFilePath))
        {
            return;
        }

        try
        {
            int width = 0;
            int height = 0;
            GraphicsModeSelection graphicsModeSelection = GraphicsModeSelection.Borderless;
            string[] lines = File.ReadAllLines(StoredResolutionFilePath);

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');

                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();

                if (string.Equals(key, "width", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
                }
                else if (string.Equals(key, "height", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
                }
                else if (string.Equals(key, "mode", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(value, true, out GraphicsModeSelection parsedGraphicsModeSelection))
                {
                    graphicsModeSelection = parsedGraphicsModeSelection;
                }
            }

            if (width < MinimumWidth || height < MinimumHeight)
            {
                return;
            }

            StoredResolution = new PersistedResolution(width, height, graphicsModeSelection, true);
            WriteMessage("Loaded stored custom resolution: " + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture) + " using " + graphicsModeSelection.ToString() + ".");
        }
        catch (Exception exception)
        {
            WriteWarning("Failed to load stored custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static void StoreResolution(int width, int height, GraphicsModeSelection graphicsModeSelection)
    {
        StoredResolution = new PersistedResolution(width, height, graphicsModeSelection, true);

        if (string.IsNullOrWhiteSpace(StoredResolutionFilePath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(StorageDirectoryPath);

            string content =
                "width=" + width.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "height=" + height.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "mode=" + graphicsModeSelection.ToString() + Environment.NewLine;

            File.WriteAllText(StoredResolutionFilePath, content);
        }
        catch (Exception exception)
        {
            WriteWarning("Failed to store custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static void ClearStoredResolution()
    {
        bool hadStoredResolution = StoredResolution.IsValid;
        StoredResolution = default;

        if (string.IsNullOrWhiteSpace(StoredResolutionFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(StoredResolutionFilePath))
            {
                File.Delete(StoredResolutionFilePath);
                hadStoredResolution = true;
            }

            if (hadStoredResolution)
            {
                WriteMessage("Cleared stored custom resolution.");
            }
        }
        catch (Exception exception)
        {
            WriteWarning("Failed to clear stored custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static string FormatResolutionText(Resolution resolution)
    {
        return resolution.width.ToString(CultureInfo.InvariantCulture) + " x " + resolution.height.ToString(CultureInfo.InvariantCulture);
    }

    private static bool ShouldWriteApplyMessage(string sourceMethod, DisplayBasis displayBasis, int count, int selectedIndex)
    {
        if (!string.Equals(LastLoggedSourceMethod, sourceMethod, StringComparison.Ordinal))
        {
            StoreLastApplyMessage(sourceMethod, displayBasis, count, selectedIndex);
            return true;
        }

        if (LastLoggedNativeWidth != displayBasis.Width || LastLoggedNativeHeight != displayBasis.Height || LastLoggedCount != count || LastLoggedSelectedIndex != selectedIndex)
        {
            StoreLastApplyMessage(sourceMethod, displayBasis, count, selectedIndex);
            return true;
        }

        return false;
    }

    private static void StoreLastApplyMessage(string sourceMethod, DisplayBasis displayBasis, int count, int selectedIndex)
    {
        LastLoggedSourceMethod = sourceMethod;
        LastLoggedNativeWidth = displayBasis.Width;
        LastLoggedNativeHeight = displayBasis.Height;
        LastLoggedCount = count;
        LastLoggedSelectedIndex = selectedIndex;
    }

    public static void WriteMessage(string message)
    {
        MessageWriter(message);
    }

    private static void WriteWarning(string message)
    {
        WarningWriter(message);
    }
}

internal enum GraphicsModeSelection
{
    Windowed,
    Borderless
}

internal readonly struct DisplayBasis
{
    public readonly int Width;
    public readonly int Height;

    public DisplayBasis(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public bool IsValid => Width > 0 && Height > 0;
}

internal readonly struct SelectionBasis
{
    public readonly int Width;
    public readonly int Height;
    public readonly bool IsValid;
    public readonly bool IsExplicit;

    public SelectionBasis(int width, int height, bool isValid, bool isExplicit)
    {
        Width = width;
        Height = height;
        IsValid = isValid;
        IsExplicit = isExplicit;
    }
}

internal readonly struct PersistedResolution
{
    public readonly int Width;
    public readonly int Height;
    public readonly GraphicsModeSelection GraphicsModeSelection;
    public readonly bool IsValid;

    public PersistedResolution(int width, int height, GraphicsModeSelection graphicsModeSelection, bool isValid)
    {
        Width = width;
        Height = height;
        GraphicsModeSelection = graphicsModeSelection;
        IsValid = isValid;
    }
}