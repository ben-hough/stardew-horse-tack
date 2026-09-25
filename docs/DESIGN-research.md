# HorseTack option 2 – layered horse + saddle + pad + bridle, synced in multiplayer

> Historical pre-build research notes. The shipped mod differs: UniqueID `MrGlim.HorseTack`, modData keys `MrGlim.HorseTack/coat|saddle|pad|bridle|style`, a standalone compositor (no dependency on or code from Multiplayer Horse Reskin), and a stable-wizard UI. See README.md.

## Parent mod facts (Multiplayer Horse Reskin, Nexus 7681, DelphinWave/"DellyBelly")
- Source: https://github.com/DelphinWave/MultiplayerHorseReskin (MIT, (c) 2024 DelphinWave).
  - `master` = v1.1.2 (2021, net452, SMAPI 3): skins must be `assets/horse_1..N.png`, N from config
    `AmountOfHorseSkins`; choice stored in vanilla field `horse.Manners` (!).
  - branch `1.6-update` = v2.0.0/2.1.0 on Nexus (net6/SMAPI 4): scans `assets/*.png` (flat, non-recursive);
    skin id = file name, stored in `horse.modData["DelphinWave.MultiplayerHorseReskin/skinId"]`;
    extra config `HayRequired` (default **true**). Branch has no LICENSE file (MIT is on master) and
    does not compile as committed (`IGenericModConfigMenuApi` referenced but not in repo).
- Nexus 43368 "MultiplayerHorseReskin - Continued" (TadeDM, Mar 2026) is based on v1.1.2 again
  (horse_N.png naming + AmountOfHorseSkins), no public source link found.
- Picking: action button while standing on the stable's footprint -> `HorseReskinMenu` (one preview,
  left/right arrows, OK; triggers on gamepad). Console: `list_horses`, `reskin_horse <name> <id>`,
  `reskin_horse_id <guid> <id>`.
- Sync: farmhand -> host ModMessage `HorseReskin`; host writes modData and broadcasts `HorseSpriteReload`;
  host re-broadcasts all choices on PeerConnected. Farmhand must run the exact same mod version.
- Drawing: loads PNGs via `ModContent` (not the game content pipeline -> Content Patcher can't touch them),
  assigns `horse.Sprite.spriteTexture` on load/warp/day start and re-assigns every 3 ticks (MP) / 30 ticks (SP).
- No API, no content-pack support.

## Bugs (reported on Nexus + spotted in code, v2.x unless noted)
1. Farmhands can't open the menu / only host can reskin: `LoadAllSprites()` runs only for the host on
   SaveLoaded; a farmhand only loads textures after it receives a reload message, so until the host has
   reskinned something the farmhand's texture map is empty -> "menu is not available because there are no
   textures". (Nexus bugs: "Only works for the Host", "Only host can change and see reskin" x3.)
2. `HayRequired` defaults to true and isn't in the Nexus description -> "Doesn't bring up menu"; and
   `Game1.player.CurrentItem.Name` throws a NullReferenceException when the hand is empty.
3. Menu hijacks any action press on the stable footprint: opening a chest, mounting the horse (the menu
   opens and the click is suppressed; users report accidental resets).
4. Tractor Mod: the tractor check is `horse.Name.StartsWith("tractor/")`, which current Tractor Mod no longer
   matches, so the tractor garage opens the menu and can permanently reskin the tractor (Nexus sticky).
5. Skin lost after warps / on other maps / "reverts to default": 1.6 re-creates `AnimatedSprite` on net
   resync; the mod patches `spriteTexture` by polling, so there's up to 0.5 s of vanilla texture flicker and
   horses the farmhand hasn't indexed are skipped.
6. `GetHorseById` uses `dict[key]` -> KeyNotFoundException (message handler crash) when a farmhand gets a
   message for a horse outside its active locations; `ReLoadHorseSprites` reads modData without
   `ContainsKey` (same issue before modData syncs).
7. Menu always starts at index 0 (not the current skin); order is `Directory.GetFiles` order; hundreds of
   combos = hundreds of arrow clicks.
8. Exact-version lock between host and farmhand; `MinimumApiVersion` 3.0.0 though it needs SMAPI 4.
9. v1.1.2/Continued: repurposes `horse.Manners`, requires renaming files to horse_N.png.

## Recommended design: new standalone mod ("HorseTack"), MIT, credit DelphinWave
Why not a companion: parent has no API and keeps re-assigning `spriteTexture` every 3 ticks, so a companion
would have to fight it (Harmony prefix on draw) and inherit bugs 1-6. Parent is abandoned (author's sticky,
Apr 2024), tiny (~600 lines) and MIT, so a rewrite that borrows ideas is cheap. Provide a one-time
migration: if a horse has `DelphinWave.MultiplayerHorseReskin/skinId`, map that file name to our horse layer.

### Data / sync
- Host-authoritative per-horse modData (synced + saved by the game, no custom broadcast needed):
  `Ben.HorseTack/horse`, `/saddle`, `/pad`, `/bridle`, `/hair` (values = layer ids, empty = none).
- Farmhand edits: ModMessage `SetTack {horseId, layer, id}` -> host validates (owner or host, horse exists,
  id known) -> writes modData. Everyone re-applies when modData changes.
- Unknown id on a client (asset missing) -> skip that layer + one warning; don't crash.

### Drawing (1.6-native, no polling)
- Each client composes textures through the content pipeline: asset name
  `Mods/Ben.HorseTack/Horse/<horse>/<saddle>/<pad>/<bridle>/<hair>`; `AssetRequested` loads the horse PNG and
  `PatchImage(..., PatchMode.Overlay)` each layer (the same thing Elle's CP pack does to Animals/Horse).
  SMAPI caches it; Content Patcher packs can even EditImage these assets.
- Apply with `horse.Sprite.LoadTexture(assetName, syncTextureName: false)` (1.6 `AnimatedSprite` local
  override - not synced, so vanilla/other clients never get a missing asset name).
- Harmony prefix on `Horse.draw(SpriteBatch)`: if `Sprite.overrideTextureName != desired` re-apply
  (string compare per draw; survives sprite re-creation/warps/mounting; zero flicker).
- Skip tractors (Tractor Mod modData/building type check + name prefix), skip horses with no tack data
  (so Elle's CP global texture / vanilla still shows).
- Menu icon / horse flute recolor stay Elle's CP job (global, per-client).

### UI
- Custom `IClickableMenu`: big live preview (animated walk frames), one row per layer
  (Horse / Saddle / Pad / Bridle / Hair) with arrows + name label, "Random", OK/Cancel; starts at current
  values; gamepad bumpers switch rows, triggers change value.
- Open: configurable keybind while standing next to your horse (default e.g. `H`), or interact with the
  horse while holding a "Tack Kit"/Hay (config). No stable-footprint hijack.
- Console fallback: `horsetack list`, `horsetack set <horse|guid> <layer> <id>`.
- GMCM only for per-client config (keybind, owner-only editing, preview scale) - not for per-horse choices.

### Asset layout
Built-in folders (flat) + SMAPI content packs (`"ContentPackFor": {"UniqueID": "Ben.HorseTack"}`):
```
assets/horses/*.png    full 224x128 sheets (vanilla Animals/horse layout, 7x4 frames of 32x32)
assets/saddles/*.png   224x128 overlays
assets/pads/*.png      224x128 overlays (optional; can require a saddle)
assets/bridles/*.png   224x128 overlays
assets/hair/*.png      optional overlays (e.g. Elle's PrismaticOverlay)
```
Id = `<packId>/<fileStem>`; optional `tack.json` for display names / "requires saddle".
A tiny importer script can copy Elle's `Horse/`, `Saddle_*`, `Pad_*`, `Bridle_*` into this layout (license
permitting - Elle's assets can't be redistributed without permission; users point at their own copy).

### Effort (one experienced SMAPI dev)
- Core (modData, host validation, asset composition, draw prefix, console cmds): ~1-1.5 days
- Menu with preview + gamepad: ~1 day
- Content packs + migration + GMCM: ~0.5 day
- MP testing (host + farmhand, warps, mounted, Tractor Mod, Multiple Horses): ~1 day
Total ~3-4 days for a solid 1.0; an MVP (console + single-screen menu, built-in folders only) ~1-1.5 days.
