# Asset Name Case Fixes

A SMAPI mod for Stardew Valley that resolves content asset names to the real capitalization of files in the game's `Content` folder.

Some mods request assets such as `data/quests` while the file is stored as `Content/Data/Quests.xnb`. On case-sensitive file systems (Linux, macOS) such requests fail. This mod rewrites the requested name to the real one before it reaches the content pipeline.

## Platform

Linux and macOS. At startup the mod checks whether the file system is case-sensitive by testing a real file. On case-insensitive file systems (typically Windows) it applies no patches.

## Requirements

- Stardew Valley 1.6.14 or newer
- SMAPI 4.x (Harmony is included with SMAPI)

## Installation

1. Copy the `AssetNameCaseFixes` folder (containing `AssetNameCaseFixes.dll` and `manifest.json`) into `Stardew Valley/Mods`.
2. Launch the game through SMAPI.

## Building from source

### Prerequisites

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or newer
- A Stardew Valley installation with SMAPI installed. The build references these files from the game folder:
  - `StardewModdingAPI.dll`
  - `Stardew Valley.dll`
  - `MonoGame.Framework.dll`
  - `smapi-internal/0Harmony.dll`

### Build

From the repository folder, pass the game folder as `GamePath`:

```bash
dotnet build -c Release -p:GamePath="/path/to/Stardew Valley"
```

Alternatively, set the `STARDEW_VALLEY_PATH` environment variable once and build without the parameter.

Linux / macOS:

```bash
export STARDEW_VALLEY_PATH="/path/to/Stardew Valley"
dotnet build -c Release
```

Windows (PowerShell):

```powershell
setx STARDEW_VALLEY_PATH "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley"
dotnet build -c Release
```

If the game folder is not found, the build stops with an error message.

### Output

The build creates an `AssetNameCaseFixes` folder inside the repository folder:

```
AssetNameCaseFixes/
├── AssetNameCaseFixes.dll
└── manifest.json
```

This folder is the ready-to-install mod. Copy it into `Stardew Valley/Mods` or archive it for distribution. The `obj` folder is an intermediate build directory and can be deleted.

Game libraries are referenced but not copied into the output.

## How it works

The mod indexes the `Content` folder once at startup and builds a case-insensitive map of asset names to their real names. Harmony prefixes then rewrite requested names:

| Patched method | Covers |
| --- | --- |
| `StardewModdingAPI.Framework.Content.AssetName.Parse(string, ...)` | `helper.GameContent.Load<T>(string)`, `ParseAssetName`, Content Patcher, and other string-based asset requests |
| `ContentManager.OpenStream(string)` | Direct `Game1.content.Load<T>(string)` calls |
| `LocalizedContentManager.OpenStream(string)` | Direct loads through the localized content manager |

Names that already match a real file exactly are left unchanged. Locale variants such as `data/quests.ru-RU` are resolved the same way.

Patches are applied inside `try/catch`. If a patch target cannot be found or patching fails, a warning is written to the SMAPI log and the game continues.

## Limitations

- Missing assets are not created or substituted.
- Asset type mismatches are not corrected (for example, requesting `Texture2D` for a data dictionary).
- Paths inside `Mods/` are not affected.
- Errors in mod data (invalid JSON/XNB, wrong keys) are not corrected.
- The `AssetName.Parse` patch relies on SMAPI internals and may need updating after SMAPI changes.

## Console commands

Available in the SMAPI console on case-sensitive file systems.

| Command | Description |
| --- | --- |
| `casefix_resolve <name>` | Show the real name that will be used for an asset. |
| `casefix_test [name]` | Load a data asset with deliberately wrong capitalization (default: `data/quests`). |
| `casefix_test_tex [name]` | Load a texture with deliberately wrong capitalization (default: `characters/abigail`). |
| `casefix_toggle` | Enable or disable the fixes for the current session. |
| `casefix_rescan` | Rebuild the file index after the `Content` folder changes. |
| `casefix_stats` | Show how many names were checked and rewritten this session. |
