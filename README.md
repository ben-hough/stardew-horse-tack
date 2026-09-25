<p align="center"><img src="docs/icon.png" width="128" alt="Horse Tack & Styling icon"></p>

# Horse Tack & Styling

A Stardew Valley 1.6 SMAPI mod with a **stable wizard**: pick each horse's **coat**, **saddle**, **saddle pad**, **bridle** and **styling** (e.g. prismatic hair) separately, with a live animated preview. The layers are composited on each player's computer, and the choices are stored on the horse and **synced in multiplayer**, so everyone sees everyone's horse.

It's **self-contained**: it doesn't read any other mod's files. *Keep current* uses the horse's current in-game look, and any extra coats, saddles, pads, bridles and styling come from PNGs you drop into the mod's own `assets` folder. With empty folders the wizard still opens and offers *Keep current* / *None*.

> **AI-generated disclosure:** the code, documentation and icon in this repository were written with the help of an AI coding assistant (Grok), directed and reviewed by MrGlim. Test it before relying on it.

## Requirements
- Stardew Valley 1.6+ and [SMAPI](https://smapi.io/) 4.0+ (built against 1.6.15 / SMAPI 4.5.2)
- No other mods are required (no Content Patcher either).
- Optional: [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)
- Multiplayer: **everyone** should install this mod with the same PNGs in `Mods/HorseTack/assets`. The host must have it for anyone to make changes.

## Install
1. Install SMAPI.
2. Unzip `HorseTack` into `Stardew Valley/Mods` (you get `Mods/HorseTack/manifest.json`).
3. Optional: put horse art PNGs into `Mods/HorseTack/assets/coats|saddles|pads|bridles|styles` (see below).
4. Run the game through SMAPI.

## Use
- **Stable spot:** stand in front of your stable and use the action button (right-click / gamepad A) on either **front corner post** of the stable (the bottom-left or bottom-right tile of the building, not the middle where the horse stands). The cursor turns into a hand there.
  - It never replaces mounting the horse, chests, or Tractor Mod's garage, and you don't need to hold anything.
- **Keybind (off by default):** set `OpenWizardKey` in `config.json` (e.g. `"OpenWizardKey": "H"`) or in Generic Mod Config Menu.
- **Console commands** (SMAPI window): `horsetack_open [horse]`, `horsetack_list`, `horsetack_options [layer]`, `horsetack_set <horse> <coat|saddle|pad|bridle|style> <id|none>`, `horsetack_reset <horse>`, `horsetack_reload`.

### Wizard steps
Every step shows the horse walking in all four directions with your current choices. Back / Next / Cancel work with mouse, keyboard (Up/Down, Enter, Backspace, Esc) and gamepad (A select, bumpers change option, triggers Back/Next, B close).

0. **Horse**: only if you can restyle more than one horse
1. **Coat**: *Keep current* (whatever texture your horse has now) or a coat from `assets/coats`
2. **Saddle**: None or a colour
3. **Saddle pad**: only when a saddle is chosen
4. **Bridle**: None or a colour
5. **Styling**: None or an overlay from `assets/styles` (e.g. a coloured mane)
6. **Confirm**: Apply

The wizard opens on the horse's current choices. A step whose folder is empty only offers *None* and says where to add art.

### Permissions & multiplayer
- Only the horse's **owner** (the owner of its stable) or the **host** can restyle it. `AnyoneCanEdit` lets everyone (the host's setting is the one that counts).
- Choices live in the horse's `modData` (`MrGlim.HorseTack/coat`, `/style`, `/saddle`, `/pad`, `/bridle`), so they're saved with the farm and synced by the game. Farmhands send a request; the host checks it and writes it.
- Each computer builds the composite texture itself and applies it right before the horse is drawn, so it survives warps, mounting, day changes and re-syncs without flicker. Tractors (Tractor Mod) are never touched.
- If a computer lacks an art file, that layer is skipped (logged once) instead of crashing.

Layer order, bottom to top: coat, styling, saddle, pad, bridle. A pad is only drawn with a saddle.

## Adding art (the `assets` folder)
```
Mods/HorseTack/assets/coats/*.png     full horse sheets
Mods/HorseTack/assets/saddles/*.png   overlays
Mods/HorseTack/assets/pads/*.png      overlays (drawn only with a saddle)
Mods/HorseTack/assets/bridles/*.png   overlays
Mods/HorseTack/assets/styles/*.png    overlays (hair etc.)
```
- Every PNG must be **224x128**, the vanilla `Animals/horse` layout (7 columns x 4 rows of 32x32 frames). Overlays are transparent except for the tack. Wrong-size or non-PNG files are skipped with a warning.
- The file name becomes the option name (`LightBlue.png` / `light_blue.png` -> "Light Blue"). A `Saddle_` / `Pad_` / `Bridle_` prefix is stripped and decides the layer; a coat file with `Overlay` in its name counts as a style.
- Folder and file names aren't case-sensitive. The same name twice, or byte-identical copies, show up once.
- After adding files, restart or run `horsetack_reload`. Only add art you made or may use. This repo and the release zip ship no art.
- Details: [`assets/README.txt`](assets/README.txt).

## Config
| Setting | Default | |
|---|---|---|
| `OpenWizardKey` | *(empty = off)* | keybind to open the wizard anywhere |
| `AnyoneCanEdit` | `false` | let every player restyle every horse |
| `StableActionTiles` | `true` | add the wizard to the stable's front posts |
| `LogVerbosity` | `Normal` | `Verbose` logs extra details to the console |

## Compatibility / notes
- Horse retexture mods you already use keep working: *Keep current* is whatever texture the game gives the horse (and tack drawn by such a mod stays part of that look).
- **Vanilla horse:** the vanilla sheet has a small built-in saddle; a saddle overlay covers it. Overlays drawn for the 224x128 layout line up frame by frame.
- Mods that also replace a specific horse's texture at draw time (for example Alternative Textures skins on horses) may conflict; use one or the other for a given horse.
- Horses ridden by a farmhand can't be restyled until they dismount (the rider's computer owns that horse while riding).

## Building
```
dotnet build -c Release                      # uses the Steam install
dotnet build -c Release -p:GamePath=../refs  # or a folder with copied game/SMAPI DLLs (never commit them)
```
Output: `bin/Release/` (`HorseTack.dll`, `manifest.json`, `i18n/`, `assets/README.txt`); add the empty `assets/<layer>` folders when packaging. Unit checks:
```
dotnet run --project tests/AssetScanCheck -p:GamePath=../refs         # assets-folder scanning, names, de-duplication, validation
python tests/CompositingCheck/check_compositing.py <folder of 224x128 PNGs> [vanilla_horse.png]   # compositing maths vs Pillow
```

## Credits
- Idea inspired by **DelphinWave**'s [Multiplayer Horse Reskin](https://github.com/DelphinWave/MultiplayerHorseReskin) (MIT). No code from it is used.
- Built on [SMAPI](https://smapi.io/) by Pathoschild.

## License
[MIT](LICENSE) © 2026 MrGlim (Ben Hough). The license covers this repository's code and docs, not game assets or art you add yourself.

## Changelog
- **1.1.0**: self-contained. Art now comes only from the mod's own `assets/{coats,saddles,pads,bridles,styles}` folder (case-insensitive, validated 224x128, de-duplicated); no longer reads Elle's Cuter Horses or content packs. With empty folders the wizard still opens with *Keep current* / *None*.
- **1.0.0**: first build (stable wizard, multiplayer sync, compositor).
