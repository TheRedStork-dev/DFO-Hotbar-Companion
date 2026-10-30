# DFO Companion

An unofficial Windows companion for **Daggerfall Unity Online (DFO)** that combines a live dungeon scanner, loot search, and four switchable hotbar pages in one application.

## Download

Download **DFO Hotbar Companion.exe** from the [latest GitHub Release](https://github.com/TheRedStork-dev/DFO-Hotbar-Companion/releases/latest).

## Choose only the modules you want

The two modules have independent **Enabled** switches, and the choices are remembered.

- **Dungeon Scanner on, Hotbar off:** launches DFO with the local live scanner but skips all hotbar patching and build work.
- **Hotbar on, Dungeon Scanner off:** updates the hotbar feature and launches DFO without starting the scanner.
- **Both on:** updates the hotbar when needed and runs the scanner at the same time.
- **Both off:** acts as a simple DFO launcher.

Turning the Hotbar module off stops future patch/update work. It does not undo a hotbar package that was already installed.

## Requirements

- Windows 10 or Windows 11, 64-bit
- DFO Online installed in its standard per-user location
- A classic Daggerfall ARENA2 folder for dungeon names and loot-table reference data
- [Git for Windows](https://git-scm.com/download/win) and [Node.js with npm](https://nodejs.org/en/download) only when the Hotbar module is enabled

The release executable includes its own .NET runtime.

## Dungeon Scanner

- Receives dungeon locations accepted by the running DFO client instead of relying on static online coordinates.
- Shows the closest confirmed dungeon first with live direction and distance.
- Identifies Regular and Elite dungeons, dungeon size, block count, type, and map position.
- Filters by dungeon name and region.
- Includes optional loot searching for weapons, armour, ingredients, religious items, books, clothing, enchantments, and King's Mark jewellery.
- Uses only loopback connections on 127.0.0.1; scanner data never leaves the computer.

Keep the companion open while using the scanner. DFO must be launched through the companion with Dungeon Scanner enabled so the local connection is available.

## Multi-page Hotbar

- Four normal hotbar pages.
- Supports the regular 10-slot keyboard bar and Enhanced Plus 16-slot crossbar.
- Existing single-hotbar saves migrate automatically: the original bar becomes page 1.
- Pages 2–4 and the selected page are stored in the normal character save.
- DFO's separate PvP hotbar set remains intact.
- Bind **Previous Hotbar Page** and **Next Hotbar Page** in **DFO Settings > Controls**.
- Keeps a backup under %LOCALAPPDATA%\DFO Hotbar Companion\backups.
- Stops safely before installation if a DFO update is no longer compatible.

The first Hotbar setup downloads and builds the matching official DFO source. Later launches reuse it and rebuild only when DFO updates.

## Building

From PowerShell with the .NET 7 SDK installed, publish app/DfoHotbarCompanion.csproj for win-x64 as a self-contained single file. The included GitHub Actions workflow does this automatically.

Pushing a version tag such as v0.2.0 runs the workflow and publishes the executable.

## Compatibility

DFO updates frequently. The scanner attaches at runtime and normally needs no per-release patch. The hotbar module rebuilds from the current official source, but an update that changes the affected hotbar systems may require a compatibility update. The installed game is changed only after patch, build, and validation checks succeed.

This project is unofficial and is not affiliated with the DFO developers or Bethesda Softworks.


