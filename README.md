# BetterMapTools

Map tools for Slay the Spire 2. Adds a **route solver**, **map drawing tools**, and a configurable preset system to the map screen.

**Features:**
- Route solver with customizable presets — optimize your path based on metrics like shops, elites, campfires, and rest sites
- Map drawing tools: freehand pencil, color picker, and undo
- Per-preset route highlight colors
- Persistent settings via ModManagerSettings
- Multiplayer compatible

## Dependencies

- Requires [ModManagerSettings](https://github.com/customjack/sts2_ModManagerSettings)

Install and enable ModManagerSettings before installing this mod.

## Install

1. Install [ModManagerSettings](https://github.com/customjack/sts2_ModManagerSettings) first.
2. Download the latest release zip from the [Releases](../../releases) page.
3. Close Slay the Spire 2.
4. In Steam, right-click `Slay the Spire 2` -> `Properties` -> `Installed Files` -> `Browse`.
5. Create a `mods` folder in the game directory if it does not exist.
6. Extract the zip and drag the `BetterMapTools` folder into `mods`.
7. Confirm these files are present in `mods/BetterMapTools`:
   - `BetterMapTools.dll`
   - `BetterMapTools.pck`
   - `BetterMapTools.json`
8. Launch Slay the Spire 2. If prompted to enable mods, accept and relaunch.
9. In-game, go to `Settings` -> `General` -> `Mods` and enable both `ModManagerSettings` and `BetterMapTools`.

## Usage

On the map screen, toolbar buttons appear at the bottom:
- **Route Solver** — click to open the solver popup, choose a preset, and solve for the best path
- **Pencil** — draw freehand on the map
- **Color Picker** — change your drawing color
- **Undo** — undo the last drawing action

To configure presets, go to `Settings` -> `General` -> `Mods` -> `BetterMapTools` -> `Settings`.

## Developer Notes

**Requirements:** .NET SDK, Godot 4 export templates, WSL or Linux shell.

**Setup:**
1. Copy `.env.example` to `.env`.
2. Set `STS2_INSTALL_DIR` to your game install path.

**Build and install:**
```bash
./scripts/bash/build_and_stage.sh
./scripts/bash/make_pck.sh
./scripts/bash/install_to_game.sh
```

## License

MIT — see [LICENSE](LICENSE).
