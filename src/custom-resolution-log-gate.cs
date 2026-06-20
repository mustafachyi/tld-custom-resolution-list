using System;

namespace CustomResolutionList;

internal static class CustomResolutionLogGate
{
    private static int LastLoggedNativeWidth;
    private static int LastLoggedNativeHeight;
    private static int LastLoggedSelectedIndex = -1;
    private static int LastLoggedCount;
    private static string LastLoggedSourceMethod = string.Empty;

    public static bool ShouldWriteApplyMessage(string sourceMethod, DisplayBasis displayBasis, int count, int selectedIndex)
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
}