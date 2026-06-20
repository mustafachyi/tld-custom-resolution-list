using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CustomResolutionList;

internal static class CustomResolutionController
{
    private static int LastNativeWidth;
    private static int LastNativeHeight;

    public static void OnModInitialized()
    {
        CustomResolutionStorage.Initialize();
        CustomResolutionStorage.Load();
        ApplyStoredResolutionIfNeeded();
    }

    public static void OnUpdate()
    {
        ApplyStoredResolutionIfNeeded();
    }

    public static SelectionBasis CaptureSelection(Il2Cpp.Panel_OptionsMenu panel)
    {
        return OptionsMenuSelectionService.CaptureSelection(panel);
    }

    public static void ApplyToPanel(Il2Cpp.Panel_OptionsMenu panel, string sourceMethod, SelectionBasis selectionBeforeRefresh)
    {
        if (panel == null)
        {
            return;
        }

        try
        {
            DisplayBasis displayBasis = DisplayBasisProvider.GetForPanel(panel);

            if (!displayBasis.IsValid)
            {
                CustomResolutionLog.Warning("No valid display basis was available.");
                return;
            }

            List<Resolution> customResolutions = CustomResolutionGenerator.Generate(displayBasis.Width, displayBasis.Height);

            if (customResolutions.Count == 0)
            {
                CustomResolutionLog.Warning("No custom resolutions were generated.");
                return;
            }

            SelectionBasis effectiveSelection = ResolveEffectiveSelection(selectionBeforeRefresh, displayBasis, customResolutions);
            int selectedIndex = CustomResolutionGenerator.FindBestSelectionIndex(customResolutions, effectiveSelection);

            OptionsMenuResolutionListWriter.ReplaceResolutionList(panel, customResolutions);
            OptionsMenuResolutionListWriter.ReplaceResolutionComboBox(panel, customResolutions, selectedIndex);

            LastNativeWidth = displayBasis.Width;
            LastNativeHeight = displayBasis.Height;

            Resolution selectedResolution = customResolutions[selectedIndex];
            PersistedResolution storedResolution = CustomResolutionStorage.StoredResolution;

            if (storedResolution.IsValid && selectedResolution.width == storedResolution.Width && selectedResolution.height == storedResolution.Height)
            {
                ApplyStoredResolutionIfNeeded();
            }

            if (CustomResolutionLogGate.ShouldWriteApplyMessage(sourceMethod, displayBasis, customResolutions.Count, selectedIndex))
            {
                CustomResolutionLog.Message("Applied " + customResolutions.Count.ToString(CultureInfo.InvariantCulture) + " same-aspect resolutions from " + displayBasis.Width.ToString(CultureInfo.InvariantCulture) + "x" + displayBasis.Height.ToString(CultureInfo.InvariantCulture) + " after " + sourceMethod + ". Selected " + ResolutionText.Format(selectedResolution) + ".");
            }
        }
        catch (Exception exception)
        {
            CustomResolutionLog.Warning("Failed to apply custom resolution list: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static bool TryApplySelectedCustomResolution(Il2Cpp.Panel_OptionsMenu panel)
    {
        if (panel == null)
        {
            return false;
        }

        if (!OptionsMenuSelectionService.TryCaptureExplicitComboSelection(panel, out SelectionBasis selectedResolution))
        {
            return false;
        }

        if (ScreenResolutionService.IsStockScreenResolution(selectedResolution.Width, selectedResolution.Height))
        {
            CustomResolutionStorage.Clear();
            ScreenResolutionService.QueueResolutionSynchronization(selectedResolution.Width, selectedResolution.Height);
            return false;
        }

        DisplayBasis displayBasis = DisplayBasisProvider.GetForPanel(panel);

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = CustomResolutionGenerator.Generate(displayBasis.Width, displayBasis.Height);
        int selectedIndex = CustomResolutionGenerator.FindExactResolutionIndex(customResolutions, selectedResolution.Width, selectedResolution.Height);

        if (selectedIndex < 0)
        {
            return false;
        }

        GraphicsModeSelection graphicsModeSelection = OptionsMenuSelectionService.GetGraphicsModeSelection(panel);

        CustomResolutionStorage.Store(selectedResolution.Width, selectedResolution.Height, graphicsModeSelection);
        ScreenResolutionService.ApplyScreenResolution(selectedResolution.Width, selectedResolution.Height, graphicsModeSelection);
        OptionsMenuResolutionListWriter.RestoreSelectedResolution(panel, customResolutions, selectedIndex);
        ScreenResolutionService.QueueScreenResolutionReapply(selectedResolution.Width, selectedResolution.Height, graphicsModeSelection);

        CustomResolutionLog.Message("Applied custom resolution directly: " + selectedResolution.Width.ToString(CultureInfo.InvariantCulture) + "x" + selectedResolution.Height.ToString(CultureInfo.InvariantCulture) + " using " + graphicsModeSelection.ToString() + ".");

        return true;
    }

    public static bool TryGetCustomResolutionIndex(Il2Cpp.Panel_OptionsMenu panel, string text, ref int result)
    {
        SelectionBasis parsedSelection = ResolutionText.ParseSelectionText(text);

        if (!parsedSelection.IsValid)
        {
            return false;
        }

        DisplayBasis displayBasis = DisplayBasisProvider.GetForPanel(panel);

        if (!displayBasis.IsValid)
        {
            displayBasis = DisplayBasisProvider.GetPrimary();
        }

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = CustomResolutionGenerator.Generate(displayBasis.Width, displayBasis.Height);
        int index = CustomResolutionGenerator.FindExactResolutionIndex(customResolutions, parsedSelection.Width, parsedSelection.Height);

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

        if (width < CustomResolutionLimits.MinimumWidth || height < CustomResolutionLimits.MinimumHeight)
        {
            return false;
        }

        DisplayBasis displayBasis = GetResolutionGenerationBasis();

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = CustomResolutionGenerator.Generate(displayBasis.Width, displayBasis.Height);
        int index = CustomResolutionGenerator.FindExactResolutionIndex(customResolutions, width, height);

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

        if (!OptionsMenuSelectionService.TryCaptureExplicitComboSelection(panel, out SelectionBasis selection))
        {
            return false;
        }

        DisplayBasis displayBasis = DisplayBasisProvider.GetForPanel(panel);

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = CustomResolutionGenerator.Generate(displayBasis.Width, displayBasis.Height);
        int index = CustomResolutionGenerator.FindExactResolutionIndex(customResolutions, selection.Width, selection.Height);

        if (index < 0)
        {
            return false;
        }

        result = customResolutions[index];
        return true;
    }

    private static SelectionBasis ResolveEffectiveSelection(SelectionBasis selectionBeforeRefresh, DisplayBasis displayBasis, List<Resolution> customResolutions)
    {
        SelectionBasis effectiveSelection = selectionBeforeRefresh;
        PersistedResolution storedResolution = CustomResolutionStorage.StoredResolution;

        if (!effectiveSelection.IsExplicit && storedResolution.IsValid)
        {
            int storedIndex = CustomResolutionGenerator.FindExactResolutionIndex(customResolutions, storedResolution.Width, storedResolution.Height);

            if (storedIndex >= 0)
            {
                effectiveSelection = new SelectionBasis(storedResolution.Width, storedResolution.Height, true, false);
            }
        }

        if (!effectiveSelection.IsValid && !OptionsMenuSelectionService.TryCaptureCurrentScreenSelection(out effectiveSelection))
        {
            effectiveSelection = new SelectionBasis(displayBasis.Width, displayBasis.Height, true, false);
        }

        return effectiveSelection;
    }

    private static void ApplyStoredResolutionIfNeeded()
    {
        PersistedResolution storedResolution = CustomResolutionStorage.StoredResolution;

        if (!storedResolution.IsValid)
        {
            return;
        }

        if (ScreenResolutionService.ScreenMatchesResolution(storedResolution.Width, storedResolution.Height, storedResolution.GraphicsModeSelection))
        {
            return;
        }

        DisplayBasis displayBasis = GetResolutionGenerationBasis();

        if (!ScreenResolutionService.IsGeneratedCustomResolutionForDisplay(storedResolution.Width, storedResolution.Height, displayBasis))
        {
            return;
        }

        ScreenResolutionService.QueueScreenResolutionReapply(storedResolution.Width, storedResolution.Height, storedResolution.GraphicsModeSelection);
    }

    private static DisplayBasis GetResolutionGenerationBasis()
    {
        DisplayBasis displayBasis = DisplayBasisProvider.GetPrimary();

        if (!displayBasis.IsValid && LastNativeWidth > 0 && LastNativeHeight > 0)
        {
            displayBasis = new DisplayBasis(LastNativeWidth, LastNativeHeight);
        }

        return displayBasis;
    }
}