using System.Globalization;
using UnityEngine;

namespace CustomResolutionList;

internal static class ResolutionText
{
    public static string Format(Resolution resolution)
    {
        return resolution.width.ToString(CultureInfo.InvariantCulture) + " x " + resolution.height.ToString(CultureInfo.InvariantCulture);
    }

    public static SelectionBasis ParseSelectionText(string text)
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
}