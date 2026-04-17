# Just Give Up Man! (JGUM)

`JGUM` is a Mount & Blade II: Bannerlord singleplayer mod that adds surrender mechanics for:
- besieged settlements,
- hostile lord encounters,
- patrol encounters.

The goal is to reduce repetitive battles when one side is clearly overwhelmed, while keeping player choice (accept or reject surrender) and trait consequences.

## Module Variants

This repository builds and deploys two variants from the same codebase:

1. `JGUM` (Standalone, default)
   - No MCM dependency
   - Reads settings from JSON (`JgumSettingsManager`)

2. `JGUM_MCM` (optional)
   - Requires MCM dependency chain
   - Reads settings from MCM UI

Variant metadata templates:
- `JGUM/SubModule.Standalone.xml`
- `JGUM/SubModule.MCM.xml`

## Features

- Dynamic settlement surrender checks during siege starvation flow
- Lord encounter surrender dialog interception (multi-party aware)
- Patrol encounter surrender behavior (`PatrolEncounterSurrenderBehavior`)
- Player accept/reject consequences with Mercy trait impact
- EN/TR localization via `ModuleData/Languages/*/jgum_strings.xml`

## Installation

1. Place module folder(s) into Bannerlord `Modules` directory.
2. Enable either:
   - `JGUM` (Standalone), or
   - `JGUM_MCM` (MCM variant).
3. Keep the chosen module after native modules in load order.

Typical modules path:
`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules`

## Requirements

### Standalone (`JGUM`)
- Bannerlord base game modules only (Native/Sandbox stack)

### MCM (`JGUM_MCM`)
- Bannerlord base game modules
- MCM v5 adapter chain referenced in project:
  - `Harmony`
  - `Butterlib`
  - `UIExtenderEx`
  - `MCMv5`

## Build From Source

Project target:
- `net472`, `x64`

Solution:
- `JustGiveUpMan.sln`

Main project:
- `JGUM/JGUM.csproj`

Build profiles include:
- `Debug`, `Release`, `Debug_MCM`, `Release_MCM`

Dual-variant build is enabled in `JGUM.csproj`, so building one profile also builds its counterpart.

```powershell
Set-Location "C:\Users\meteh\RiderProjects\JustGiveUpMan"
dotnet build .\JGUM\JGUM.csproj -c Release -p:Platform=x64
```

Post-build deploy copies output to:
- `Modules\JGUM` (Standalone)
- `Modules\JGUM_MCM` (MCM)

## Configuration

Settings backend is managed by:
- `JGUM/Config/JgumSettingsManager.cs`

- Standalone: JSON-backed settings (`config.json`)
- MCM: `USE_MCM` compile symbol path

## Localization

Localization files:
- `JGUM/ModuleData/Languages/EN/jgum_strings.xml`
- `JGUM/ModuleData/Languages/TR/jgum_strings.xml`

String access pattern in code:
- `StringCalculator.GetString(baseId, fallback)`

When adding new localized text:
1. Add key to EN
2. Add same key to TR
3. Keep IDs synchronized

## Notes

- No automated tests are included in this repository.
- In-game validation is required for siege/lord/patrol dialogue paths.

## Contributing / Issues

If you find bugs or regressions, open an issue with:
- Bannerlord version
- Active variant (`JGUM` or `JGUM_MCM`)
- Repro steps
- Crash log / stack trace (if available)

## License

CC BY-NC-SA 4.0
