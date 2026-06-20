# Custom Resolution List

A MelonLoader mod for The Long Dark that dynamically populates the display options menu with same-aspect custom resolutions derived from the active monitor. It is designed to allow sub-native resolution scaling for performance optimization on low-end hardware or precise custom window layouts.

## Features

* **Custom Aspect Generation:** Computes exact-scale and approximate sub-native resolutions natively matched to the active display's aspect ratio.
* **Aspect Ratio Preservation:** Automatically scales resolutions down from maximum native limits while maintaining strict aspect ratio constraints.
* **Resolution Persistence:** Saves selected custom resolutions to a config file and applies them seamlessly upon game initialization.

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) (v0.7.2+ targeting .NET 6).
2. Place `CustomResolutionList.dll` into the `Mods` folder of your game directory.
