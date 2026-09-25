Horse Tack & Styling 1.4.1 by MrGlim

A stable wizard for dressing up your horse: pick its coat, saddle, saddle pad, bridle
and styling separately, with a live preview. Includes 57 original pieces
(16 coats, 12 saddles, 11 pads, 9 bridles, 9 styling overlays). Synced in multiplayer.

INSTALL
1. Needs Stardew Valley 1.6 and SMAPI 4.x. Generic Mod Config Menu is optional.
2. Unzip so the HorseTack folder sits inside Stardew Valley\Mods
   (you should have Mods\HorseTack\manifest.json).
3. Multiplayer: every player needs HorseTack, the same version (the host must have it).
4. REMOVE "Multiplayer Horse Reskin" (MultiplayerHorseReskin folder) if you have it.
   It also replaces horse textures, so it conflicts with HorseTack.
   HorseTack warns about it in the SMAPI console.
5. Optional companion: Elle's Cuter Horses (needs Content Patcher)
   https://www.nexusmods.com/stardewvalley/mods/20042
   When it's installed, its coats, prismatic hair and coloured saddles/pads/bridles
   show up in the wizard next to HorseTack's own art. HorseTack reads them from your
   own install of her mod and doesn't include any of her art. HorseTack's own art
   works with or without it.

ART
HorseTack ships its own festival/Stardew-themed coats, saddles, pads, bridles and
styling (Spirit's Eve pumpkin saddle, Winter Star holly, Lucky Purple Shorts pad, ...),
breed-inspired coats, a Witchy set (broomstick saddle, witch hat, potion vials, ...) and a
Forest Spirit set whose Moss Cloak and Antler Crown change with the season.
In multiplayer, use matching art: same HorseTack version, and Elle's Cuter Horses on
everyone's computer if anyone picks Elle's art. If you're missing a piece someone else
picked, you just see that layer skipped / the normal coat on your screen - no error.
You can add your own 224x128 PNGs too: see assets\README.txt.

USE
Stand in front of either front corner post of the stable (bottom-left or bottom-right
tile, not the middle where the horse stands) and press the action button.
The wizard walks through horse (if you have several), coat, saddle, pad (only with a
saddle), bridle, styling, confirm. Left/Right (or the arrows beside the collection name)
changes the collection filter; mouse wheel / PageUp / PageDown / bumpers scroll fast.
Only the horse's owner or the host can restyle a horse (host config: AnyoneCanEdit).
Optional hotkey: set OpenWizardKey in Mods\HorseTack\config.json (created on first
launch) or in Generic Mod Config Menu.
SMAPI console: horsetack_open, horsetack_list, horsetack_options, horsetack_set,
horsetack_reset, horsetack_reload, horsetack_textures (debug).

UNINSTALL
Delete Mods\HorseTack. Horses go back to their normal look; the saved choices are
small text entries on each horse that the game ignores without the mod.

NOTES
Seasonal horse packs (Content Patcher packs that change the game's horse by season,
such as Bog's Witchy Farm Buildings) still change horses left on "Keep current".
A coat you pick in HorseTack is HorseTack's own texture and stays, with its tack.
Built and tested with SMAPI 4.5.2 on Stardew Valley 1.6.15 (Windows).
Found a problem? Please report it on the mod's Nexus Mods page or on GitHub, and
say which coat + which piece if something looks misaligned.

Source (MIT license): https://github.com/ben-hough/stardew-horse-tack
AI disclosure: this mod's code, and the scripts that draw its bundled art, were written
with an AI coding assistant, directed and reviewed by MrGlim.
