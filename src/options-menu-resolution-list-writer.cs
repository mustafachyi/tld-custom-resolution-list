using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomResolutionList;

internal static class OptionsMenuResolutionListWriter
{
    public static void ReplaceResolutionList(Il2Cpp.Panel_OptionsMenu panel, List<Resolution> customResolutions)
    {
        Il2CppSystem.Collections.Generic.List<Resolution> targetList = panel.m_Resolutions;

        if (targetList == null)
        {
            CustomResolutionLog.Warning("Panel resolution list was null.");
            return;
        }

        targetList.Clear();

        for (int index = 0; index < customResolutions.Count; index++)
        {
            targetList.Add(customResolutions[index]);
        }
    }

    public static void ReplaceResolutionComboBox(Il2Cpp.Panel_OptionsMenu panel, List<Resolution> customResolutions, int selectedIndex)
    {
        Il2Cpp.ConsoleComboBox comboBox = panel.m_GraphicsResolutionPopupList;

        if (comboBox == null)
        {
            CustomResolutionLog.Warning("Graphics resolution combo box was null.");
            return;
        }

        Il2CppSystem.Collections.Generic.List<string> items = comboBox.items;

        if (items == null)
        {
            CustomResolutionLog.Warning("Graphics resolution combo box item list was null.");
            return;
        }

        items.Clear();

        for (int index = 0; index < customResolutions.Count; index++)
        {
            items.Add(ResolutionText.Format(customResolutions[index]));
        }

        if (selectedIndex < 0 || selectedIndex >= customResolutions.Count)
        {
            selectedIndex = customResolutions.Count - 1;
        }

        string selectedText = ResolutionText.Format(customResolutions[selectedIndex]);

        comboBox.value = selectedText;
        comboBox.m_CurrentIndex = selectedIndex;
        comboBox.m_SelectedItem = selectedText;
        comboBox.Refresh();
        comboBox.m_CurrentIndex = selectedIndex;
        comboBox.m_SelectedItem = selectedText;
        comboBox.value = selectedText;
    }

    public static void RestoreSelectedResolution(Il2Cpp.Panel_OptionsMenu panel, List<Resolution> customResolutions, int selectedIndex)
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
            CustomResolutionLog.Warning("Failed to restore selected custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }
}