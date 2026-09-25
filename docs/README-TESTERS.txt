Horse Tack & Styling 1.1.0 (test build) by MrGlim

INSTALL
1. Needs Stardew Valley 1.6 and SMAPI 4.x. No other mods are needed
   (no Content Patcher). Generic Mod Config Menu is optional, for an in-game settings page.
2. Unzip so the HorseTack folder sits inside Stardew Valley\Mods
   (you should have Mods\HorseTack\manifest.json).
3. Everyone in the multiplayer game should install it. The host must have it.

ART
The mod is self-contained and doesn't read other mods' files. Out of the box the
Mods\HorseTack\assets folders are empty: the wizard still opens and offers
"Keep current" (the horse's current look) and "None".
To add coats, saddles, pads, bridles or styling, drop 224x128 PNGs into
Mods\HorseTack\assets\coats, saddles, pads, bridles, styles (see assets\README.txt).
In multiplayer, everyone needs the same PNGs there.

USE
Stand in front of either front corner post of the stable (bottom-left or bottom-right
tile, not the middle where the horse stands) and press the action button.
The wizard walks through horse (if you have several), coat, saddle, pad (only with a
saddle), bridle, styling, confirm.
Only the horse's owner or the host can restyle a horse (config: AnyoneCanEdit).
Optional hotkey: set OpenWizardKey in Mods\HorseTack\config.json (created on first
launch) or in Generic Mod Config Menu. SMAPI console: horsetack_open, horsetack_list.

Source: https://github.com/ben-hough/stardew-horse-tack
AI disclosure: this mod's code was generated with AI assistance.
