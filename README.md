# RoutingHelper

`RoutingHelper` adds route-planning tools on the map screen and integrates with `ModManagerSettings` for persistent configuration.

## Dependencies

- Requires `ModManagerSettings`.
- Install and enable `ModManagerSettings`. Available at [sts2_ModManagerSettings](https://github.com/customjack/sts2_ModManagerSettings)

## Install (Manual)

1. Close Slay the Spire 2.
2. Extract the release zip for `RoutingHelper`.
3. In Steam, right-click `Slay the Spire 2` -> `Properties` -> `Installed Files` -> `Browse`.
4. In the game folder that opens, create a `mods` folder if it does not exist.
5. Drag the extracted `RoutingHelper` folder into `mods`.
6. Confirm these files exist in `mods/RoutingHelper`:
   - `RoutingHelper.dll`
   - `RoutingHelper.pck`
7. Launch Slay the Spire 2. If prompted to enable mods, accept and relaunch.
8. In-game, open `Settings` -> `General` -> `Mods` and make sure:
   - `ModManagerSettings` is enabled
   - `RoutingHelper` is enabled

## Developer Notes

- Build (WSL/Linux scripts):
  - `./scripts/bash/build_and_stage.sh`
  - `./scripts/bash/make_pck.sh`
  - `./scripts/bash/install_to_game.sh`
- Environment:
  - Copy `.env.example` to `.env`
  - Set `STS2_INSTALL_DIR` in `.env`
