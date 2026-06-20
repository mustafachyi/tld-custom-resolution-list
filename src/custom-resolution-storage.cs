using System;
using System.Globalization;
using System.IO;

namespace CustomResolutionList;

internal static class CustomResolutionStorage
{
    private static string StorageDirectoryPath = string.Empty;
    private static string StoredResolutionFilePath = string.Empty;

    public static PersistedResolution StoredResolution { get; private set; }

    public static void Initialize()
    {
        StorageDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "CustomResolutionList");
        StoredResolutionFilePath = Path.Combine(StorageDirectoryPath, "custom-resolution-list.cfg");

        try
        {
            Directory.CreateDirectory(StorageDirectoryPath);
        }
        catch (Exception exception)
        {
            CustomResolutionLog.Warning("Failed to initialize custom resolution storage: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static void Load()
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

            if (width < CustomResolutionLimits.MinimumWidth || height < CustomResolutionLimits.MinimumHeight)
            {
                return;
            }

            StoredResolution = new PersistedResolution(width, height, graphicsModeSelection, true);
            CustomResolutionLog.Message("Loaded stored custom resolution: " + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture) + " using " + graphicsModeSelection.ToString() + ".");
        }
        catch (Exception exception)
        {
            CustomResolutionLog.Warning("Failed to load stored custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static void Store(int width, int height, GraphicsModeSelection graphicsModeSelection)
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
            CustomResolutionLog.Warning("Failed to store custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static void Clear()
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
                CustomResolutionLog.Message("Cleared stored custom resolution.");
            }
        }
        catch (Exception exception)
        {
            CustomResolutionLog.Warning("Failed to clear stored custom resolution: " + exception.GetType().Name + ": " + exception.Message);
        }
    }
}