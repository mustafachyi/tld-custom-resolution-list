namespace CustomResolutionList;

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