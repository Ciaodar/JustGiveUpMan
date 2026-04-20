# Just Give Up Man! (JGUM)

`JGUM` is a Mount & Blade II: Bannerlord singleplayer mod that adds surrender mechanics for:
- besieged settlements,
- hostile lord encounters,
- patrol encounters.

The goal is to reduce repetitive battles when one side is clearly overwhelmed, while keeping player choice (accept/reject surrender) and trait consequences.

## Module Model

This repository uses a **single module folder** model:

- Module folder: `Modules/JGUM`
- Core assembly: `JGUM.dll` (required)
- Optional bridge assembly: `JGUM.MCMBridge.dll` (optional MCM integration)

At runtime:
- If MCM dependency chain is present, core activates bridge and uses MCM settings.
- If MCM is not present (or bridge is missing), core continues with JSON settings.

## Features

- Dynamic settlement surrender checks during siege/starvation flow
- Proactive siege negotiation + persuasion flow
- Lord encounter surrender interception (multi-party aware)
- Patrol encounter surrender behavior (`PatrolEncounterSurrenderBehavior`)
- EN/TR localization via `ModuleData/Languages/*/jgum_strings.xml`

## Installation

1. Place `JGUM` folder into Bannerlord `Modules` directory.
2. Enable `JGUM` in launcher.
3. Keep `JGUM` after native modules in load order.
4. Optional MCM UI: install/enable framework chain; bridge activates automatically.

Typical modules path:
`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules`

## Requirements

### Core (`JGUM`)
- Bannerlord base game modules (Native/Sandbox stack)

### Optional MCM UI (bridge mode)
- `Harmony`
- `ButterLib`
- `UIExtenderEx`
- `MCMv5` (`Bannerlord.MBOptionScreen`)

## Build From Source

Project target:
- `net472`, `x64`

Solution:
- `JustGiveUpMan.sln`

Projects:
- `JGUM/JGUM.csproj` (core)
- `JGUM.MCMBridge/JGUM.MCMBridge.csproj` (optional bridge DLL)

Build all:

```powershell
Set-Location "C:\Users\meteh\RiderProjects\JustGiveUpMan"
dotnet build .\JustGiveUpMan.sln -c Release
```

Build core only:

```powershell
Set-Location "C:\Users\meteh\RiderProjects\JustGiveUpMan"
dotnet build .\JGUM\JGUM.csproj -c Release
```

Build bridge only:

```powershell
Set-Location "C:\Users\meteh\RiderProjects\JustGiveUpMan"
dotnet build .\JGUM.MCMBridge\JGUM.MCMBridge.csproj -c Release
```

Post-build deploy behavior:
- `JGUM.csproj` deploys module files to `Modules/JGUM`
- `JGUM.MCMBridge.csproj` copies `JGUM.MCMBridge.dll` into `Modules/JGUM/bin/Win64_Shipping_Client`

## Configuration

Settings backend manager:
- `JGUM/Config/JgumSettingsManager.cs`

Backend precedence:
1. Bridge-provided settings provider (if bridge active)
2. JSON fallback (`config.json`)

JSON hot-reload command:
- `jgum.reload_config`

## Localization

Localization files:
- `JGUM/ModuleData/Languages/EN/jgum_strings.xml`
- `JGUM/ModuleData/Languages/TR/jgum_strings.xml`

String access pattern:
- `StringCalculator.GetString(baseId, fallback)`

When adding new localized text:
1. Add key to EN
2. Add same key to TR
3. Keep IDs synchronized

## Notes

- No automated tests are included in this repository.
- In-game validation is required for siege/lord/patrol/negotiation dialogue paths.

## Contributing / Issues

If you find bugs or regressions, open an issue with:
- Bannerlord version
- Whether MCM framework chain is installed/enabled
- Repro steps
- Crash log / stack trace (if available)

## License

CC BY-NC-SA 4.0
