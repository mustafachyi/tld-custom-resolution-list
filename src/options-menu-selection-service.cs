using System;
using UnityEngine;

namespace CustomResolutionList;

internal static class OptionsMenuSelectionService
{
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

    public static bool TryCaptureExplicitComboSelection(Il2Cpp.Panel_OptionsMenu panel, out SelectionBasis selection)
    {
        selection = default;

        try
        {
            Il2Cpp.ConsoleComboBox comboBox = panel.m_GraphicsResolutionPopupList;

            if (comboBox == null)
            {
                return false;
            }

            selection = ResolutionText.ParseSelectionText(comboBox.value);

            if (selection.IsValid)
            {
                selection = new SelectionBasis(selection.Width, selection.Height, true, true);
                return true;
            }

            selection = ResolutionText.ParseSelectionText(comboBox.m_SelectedItem);

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

    public static bool TryCaptureCurrentScreenSelection(out SelectionBasis selection)
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

    public static GraphicsModeSelection GetGraphicsModeSelection(Il2Cpp.Panel_OptionsMenu panel)
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
}