using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomResolutionList;

internal static class CustomResolutionGenerator
{
    private static readonly List<Resolution> CachedResolutions = new(CustomResolutionLimits.MaximumEntries);
    private static int CachedNativeWidth;
    private static int CachedNativeHeight;

    public static List<Resolution> Generate(int nativeWidth, int nativeHeight)
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

        if (nativeWidth < CustomResolutionLimits.MinimumWidth || nativeHeight < CustomResolutionLimits.MinimumHeight)
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

    public static int FindBestSelectionIndex(List<Resolution> customResolutions, SelectionBasis selectionBasis)
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

    public static int FindExactResolutionIndex(List<Resolution> resolutions, int width, int height)
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

    private static void GenerateExactScaleResolutions(List<Resolution> resolutions, int nativeWidth, int nativeHeight, int aspectWidth, int aspectHeight)
    {
        int maxScale = Math.Min(nativeWidth / aspectWidth, nativeHeight / aspectHeight);
        int minScaleFromWidth = DivideRoundUp(CustomResolutionLimits.MinimumWidth, aspectWidth);
        int minScaleFromHeight = DivideRoundUp(CustomResolutionLimits.MinimumHeight, aspectHeight);
        int minScale = Math.Max(minScaleFromWidth, minScaleFromHeight);

        for (int scale = minScale; scale <= maxScale; scale += CustomResolutionLimits.ExactScaleStep)
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

        if (minimumWidth >= CustomResolutionLimits.MinimumWidth && minimumHeight >= CustomResolutionLimits.MinimumHeight)
        {
            AddResolution(resolutions, minimumWidth, minimumHeight);
        }
    }

    private static void GenerateApproximateResolutions(List<Resolution> resolutions, int nativeWidth, int nativeHeight)
    {
        for (int width = CustomResolutionLimits.MinimumWidth; width <= nativeWidth; width += CustomResolutionLimits.ApproximateWidthStep)
        {
            int height = RoundToEven((int)Math.Round(width * (nativeHeight / (double)nativeWidth)));

            if (height < CustomResolutionLimits.MinimumHeight || height > nativeHeight)
            {
                continue;
            }

            AddResolution(resolutions, width, height);
        }

        int minimumHeightBasedWidth = RoundToEven((int)Math.Round(CustomResolutionLimits.MinimumHeight * (nativeWidth / (double)nativeHeight)));

        if (minimumHeightBasedWidth >= CustomResolutionLimits.MinimumWidth && minimumHeightBasedWidth <= nativeWidth)
        {
            AddResolution(resolutions, minimumHeightBasedWidth, CustomResolutionLimits.MinimumHeight);
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
        if (resolutions.Count <= CustomResolutionLimits.MaximumEntries)
        {
            return;
        }

        List<Resolution> sampledResolutions = new(CustomResolutionLimits.MaximumEntries);
        int sourceCount = resolutions.Count;

        for (int index = 0; index < CustomResolutionLimits.MaximumEntries; index++)
        {
            int sourceIndex = (int)Math.Round(index * ((sourceCount - 1) / (double)(CustomResolutionLimits.MaximumEntries - 1)));
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
}