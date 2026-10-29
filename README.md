# DFO Hotbar Companion

An unofficial Windows companion for **Daggerfall Unity Online (DFO)** that adds four switchable hotbar pages without memory injection or fixed executable offsets.

## Download

Download **DFO Hotbar Companion.exe** from the [latest GitHub Release](https://github.com/TheRedStork-dev/DFO-Hotbar-Companion/releases/latest).

## Requirements

- Windows 10 or Windows 11, 64-bit
- [Git for Windows](https://git-scm.com/download/win)
- [Node.js with npm](https://nodejs.org/en/download)
- DFO Online installed in its standard per-user location

The release executable includes its own .NET runtime.

## Using the companion

1. Close DFO Online.
2. Run **DFO Hotbar Companion.exe**.
3. Select **Update & Launch DFO**.
4. In DFO, open **Settings > Controls**.
5. Bind **Previous Hotbar Page** and **Next Hotbar Page**.

The first installation downloads and builds the matching official DFO source. It normally takes approximately 2–5 minutes. Later launches reuse the downloaded source and rebuild only after DFO updates.

## Features

- Four normal hotbar pages.
- Supports the regular 10-slot keyboard bar and Enhanced Plus 16-slot crossbar.
- Page buttons numbered 1–4 above the hotbar.
- Existing single-hotbar saves migrate automatically: the original bar becomes page 1.
- Pages 2–4 and the selected page are stored in the normal character save.
- DFO's separate PvP hotbar set remains intact.
- Previous/Next Hotbar Page controls ship unbound so the player can choose keyboard or controller bindings.
- Checks the newest official `app-v...` release tag when updating.
- Keeps a backup of the installed package under `%LOCALAPPDATA%\DFO Hotbar Companion\backups`.
- Stops before installation if a DFO update is no longer compatible.

## How updates work

The companion does not inject code into the running game. It downloads the official DFO source for the current release, applies the small source patch in `app/hotbar-pages.patch`, performs an official production build, and creates a fresh Electron package.

The installed game is replaced only after the patch, build, package validation, and desktop-shell compatibility checks all succeed. If DFO changes the affected hotbar code, the process stops and leaves the installed game untouched.

The current patch was production-built and verified against official DFO release `app-v0.1.13392`, including its PvP hotbar changes.

## Building the companion

From PowerShell with the .NET 7 SDK installed:

```powershell
dotnet publish app/DfoHotbarCompanion.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

Pushing a version tag such as `v0.1.0` runs the included GitHub Actions workflow and publishes the executable as a GitHub Release asset.

## Compatibility notes

DFO currently updates frequently. Most updates can be rebuilt automatically, but a release that substantially changes the same hotbar systems may require a companion compatibility update. The safety checks are intentionally conservative.

This project is unofficial and is not affiliated with the DFO developers or Bethesda Softworks.

