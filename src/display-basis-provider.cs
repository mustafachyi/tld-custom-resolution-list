using System;
using UnityEngine;

namespace CustomResolutionList;

internal static class DisplayBasisProvider
{
    public static DisplayBasis GetForPanel(Il2Cpp.Panel_OptionsMenu panel)
    {
        Display[] displays = Display.displays ?? Array.Empty<Display>();

        if (displays.Length == 0)
        {
            return GetPrimary();
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

        return GetPrimary();
    }

    public static DisplayBasis GetPrimary()
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
}