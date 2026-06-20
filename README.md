# Custom Resolution List

Custom Resolution List is a MelonLoader mod for The Long Dark that replaces the display resolution list with same-aspect custom resolutions derived from the active monitor.

The mod is built for sub-native rendering, low-end hardware, and precise window sizing. It keeps the monitor aspect ratio intact, stores the selected custom resolution, and reapplies it when the game starts.

## Features

- Generates same-aspect resolutions from the active display
- Preserves the native aspect ratio across exact-scale and approximate entries
- Keeps the resolution list bounded for a clean options menu
- Applies custom selections directly when the game would reject them as unsupported
- Persists the selected custom resolution in `UserData/CustomResolutionList/custom-resolution-list.cfg`
- Synchronizes the internal dynamic resolution helper after applying a custom resolution

## Installation

1. Install MelonLoader v0.7.2 or newer for The Long Dark with .NET 6 support.
2. Copy `CustomResolutionList.dll` into the game `Mods` folder.
3. Start the game.
4. Open the display options menu.
5. Select a generated resolution that matches your performance target or window size.

## Build requirements

- Windows
- .NET 6 SDK
- The Long Dark installed through Steam
- MelonLoader installed in the game folder
- Generated Il2Cpp assemblies present in `MelonLoader/Il2CppAssemblies`

The project resolves the Steam install path automatically. You can also pass the game folder directly:

```powershell
dotnet build tld-custom-resolution-list.csproj -c Release /p:TheLongDarkGamePath="D:\Games\Steam\steamapps\common\TheLongDark"
```
