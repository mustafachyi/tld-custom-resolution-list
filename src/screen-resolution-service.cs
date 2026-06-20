using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomResolutionList;

internal static class ScreenResolutionService
{
    private static bool HasPendingReapply;
    private static int PendingReapplyWidth;
    private static int PendingReapplyHeight;
    private static int ReapplyRequestId;
    private static GraphicsModeSelection PendingReapplyGraphicsModeSelection;

    public static void ApplyScreenResolution(int width, int height, GraphicsModeSelection graphicsModeSelection)
    {
        if (graphicsModeSelection == GraphicsModeSelection.Windowed)
        {
            Screen.SetResolution(width, height, false);
            return;
        }

        Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
    }

    public static void QueueScreenResolutionReapply(int width, int height, GraphicsModeSelection graphicsModeSelection)
    {
        if (HasPendingReapply && PendingReapplyWidth == width && PendingReapplyHeight == height && PendingReapplyGraphicsModeSelection == graphicsModeSelection)
        {
            return;
        }

        HasPendingReapply = true;
        PendingReapplyWidth = width;
        PendingReapplyHeight = height;
        PendingReapplyGraphicsModeSelection = graphicsModeSelection;
        ReapplyRequestId += 1;

        MelonCoroutines.Start(ReapplyScreenResolutionAfterFrames(width, height, graphicsModeSelection, ReapplyRequestId));
    }

    public static void QueueResolutionSynchronization(int width, int height)
    {
        MelonCoroutines.Start(SynchronizeDynamicResolutionHelperAfterFrames(width, height));
    }

    public static bool ScreenMatchesResolution(int width, int height, GraphicsModeSelection graphicsModeSelection)
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

    public static bool IsStockScreenResolution(int width, int height)
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

    public static bool IsGeneratedCustomResolutionForDisplay(int width, int height, DisplayBasis displayBasis)
    {
        if (IsStockScreenResolution(width, height))
        {
            return false;
        }

        if (!displayBasis.IsValid)
        {
            return false;
        }

        List<Resolution> customResolutions = CustomResolutionGenerator.Generate(displayBasis.Width, displayBasis.Height);
        return CustomResolutionGenerator.FindExactResolutionIndex(customResolutions, width, height) >= 0;
    }

    private static IEnumerator ReapplyScreenResolutionAfterFrames(int width, int height, GraphicsModeSelection graphicsModeSelection, int requestId)
    {
        yield return null;
        ApplyScreenResolution(width, height, graphicsModeSelection);
        yield return null;
        ApplyScreenResolution(width, height, graphicsModeSelection);

        if (ReapplyRequestId == requestId)
        {
            HasPendingReapply = false;
        }

        QueueResolutionSynchronization(width, height);
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
            CustomResolutionLog.Warning("Failed to synchronize internal render resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }
}