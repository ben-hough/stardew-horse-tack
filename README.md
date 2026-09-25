<p align="center"><img src="docs/icon.png" width="128" alt="Horse Tack & Styling icon"></p>

# Horse Tack & Styling

A Stardew Valley 1.6 SMAPI mod with a **stable wizard**: pick each horse's **coat**, **saddle**, **saddle pad**, **bridle** and **styling** (e.g. prismatic hair) separately, with a live animated preview. The layers are composited on each player's computer, and the choices are stored on the horse and **synced in multiplayer**, so everyone sees everyone's horse.

It uses the art from **[Elle's Cuter Horses](https://www.nexusmods.com/stardewvalley/mods/20042)** (install it separately; this repo contains none of Elle's art) and from optional **Horse Tack & Styling content packs**.

> **AI-generated disclosure:** the code, documentation and icon in this repository were written with the help of an AI coding assistant (Grok), directed and reviewed by MrGlim. Test it before relying on it.

## Requirements
- Stardew Valley 1.6+ and [SMAPI](https://smapi.io/) 4.0+ (built against 1.6.15 / SMAPI 4.5.2)
- [Elle's Cuter Horses](https://www.nexusmods.com/stardewvalley/mods/20042) (the art source; [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915) is its own requirement) and/or HorseTack content packs
- Optional: [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)
- Multiplayer: **everyone** should install this mod and the same art mods. The host must have it for anyone to make changes.

## Install
1. Install SMAPI and Elle's Cuter Horses.
2. Unzip `HorseTack` into `Stardew Valley/Mods`.
3. Run the game through SMAPI.

## Use
- **Stable spot:** stand in front of your stable and use the action button (right-click / gamepad A) on either **front corner post** of the stable (the bottom-left or bottom-right tile of the building, not the middle where the horse stands). The cursor turns into a hand there.
  - It never replaces mounting the horse, chests, or Tractor Mod's garage, and you don't need to hold anything.
- **Keybind (off by default):** set `OpenWizardKey` in `config.json` (e.g. `"OpenWizardKey": "H"`) or in Generic Mod Config Menu.
- **Console commands** (SMAPI window): `horsetack_open [horse]`, `horsetack_list`, `horsetack_options [layer]`, `horsetack_set <horse> <coat|saddle|pad|bridle|style> <id|none>`, `horsetack_reset <horse>`, `horsetack_reload`.

### Wizard steps
Every step shows the horse walking in all four directions with your current choices. Back / Next / Cancel work with mouse, keyboard (Up/Down, Enter, Backspace, Esc) and gamepad (A select, bumpers change option, triggers Back/Next, B close).

0. **Horse**: only if you can restyle more than one horse
1. **Coat**: *Keep current* (whatever texture your horse has now, from any pack) or one of Elle's coats
2. **Saddle**: None or a colour
3. **Saddle pad**: only when a saddle is chosen
4. **Bridle**: None or a colour
5. **Styling**: None or an overlay (Elle's Prismatic hair)
6. **Confirm**: Apply

The wizard opens on the horse's current choices.

### Permissions & multiplayer
- Only the horse's **owner** (the owner of its stable) or the **host** can restyle it. `AnyoneCanEdit` lets everyone (the host's setting is the one that counts).
- Choices live in the horse's `modData` (`MrGlim.HorseTack/coat`, `/style`, `/saddle`, `/pad`, `/bridle`), so they're saved with the farm and synced by the game. Farmhands send a request; the host checks it and writes it.
- Each computer builds the composite texture itself and applies it right before the horse is drawn, so it survives warps, mounting, day changes and re-syncs without flicker. Tractors (Tractor Mod) are never touched.
- If a computer lacks an art file, that layer is skipped (logged once) instead of crashing.

Layer order follows Elle's Cuter Horses: coat, styling, saddle, pad, bridle.

## Content packs
Make a folder in `Mods` with a `manifest.json` that has `"ContentPackFor": { "UniqueID": "MrGlim.HorseTack" }` and any of these folders:

```
assets/coats/*.png     full horse sheets
assets/saddles/*.png   overlays
assets/pads/*.png      overlays
assets/bridles/*.png   overlays
assets/styles/*.png    overlays (hair etc.)
```
All sheets are 224x128 (the vanilla `Animals/horse` layout: 7 columns x 4 rows of 32x32 frames). The file name becomes the option name (`LightBlue.png` -> "Light Blue"). See [`docs/example-content-pack`](docs/example-content-pack).

## Config
| Setting | Default | |
|---|---|---|
| `OpenWizardKey` | *(empty = off)* | keybind to open the wizard anywhere |
| `AnyoneCanEdit` | `false` | let every player restyle every horse |
| `StableActionTiles` | `true` | add the wizard to the stable's front posts |
| `LogVerbosity` | `Normal` | `Verbose` logs extra details to the console |

## Compatibility / notes
- **Elle's Cuter Horses** config still sets your local default horse texture (used by *Keep current*), the menu icon and the flute. If Elle's own saddle/pad/bridle options are on, *Keep current* includes them; set them to `false` and use the wizard instead.
- **Vanilla horse:** Elle's tack lines up with the vanilla sheet (same 224x128 layout); the vanilla horse has a built-in saddle that Elle's saddle covers, and the stirrups stick out 1-2 px in the front/back views. See `tests/CompositingCheck`.
- Mods that also replace a specific horse's texture at draw time (for example Alternative Textures skins on horses) may conflict; use one or the other for a given horse.
- Horses ridden by a farmhand can't be restyled until they dismount (the rider's computer owns that horse while riding).

## Building
```
dotnet build -c Release                      # uses the Steam install
dotnet build -c Release -p:GamePath=../refs  # or a folder with copied game/SMAPI DLLs (never commit them)
```
Output: `bin/Release/` (`HorseTack.dll`, `manifest.json`, `i18n/`). Unit check of the compositing maths against Pillow:
```
python tests/CompositingCheck/check_compositing.py "<Elle's Cuter Horses>/assets" [vanilla_horse.png]
```
`tools/composite_tack.py` is the earlier batch compositor that pre-bakes PNGs (see `docs/option1-compositor.md`).

## Credits
- Idea inspired by **DelphinWave**'s [Multiplayer Horse Reskin](https://github.com/DelphinWave/MultiplayerHorseReskin) (MIT). No code from it is used.
- Horse, saddle, pad, bridle and hair art: **Elle / Junimods**, [Elle's Cuter Horses](https://www.nexusmods.com/stardewvalley/mods/20042). Not included here; install it from Nexus.
- Built on [SMAPI](https://smapi.io/) by Pathoschild.

## License
[MIT](LICENSE) © 2026 MrGlim (Ben Hough). The license covers this repository's code and docs only, not Elle's art or game assets.
