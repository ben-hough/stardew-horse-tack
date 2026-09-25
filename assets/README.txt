Horse Tack & Styling - assets folder
====================================

HorseTack's own art lives here, and you can add your own. If Elle's Cuter Horses is installed,
its art is also offered (read from its own folder; nothing is copied here).

Put PNGs in these folders:

  coats\     full horse sheets (the whole horse). Shown under "Coat".
  saddles\   saddle overlays.
  pads\      saddle pad overlays (only drawn when a saddle is chosen).
  bridles\   bridle overlays.
  styles\    styling overlays, e.g. a coloured mane (drawn over the coat, under the tack).

Layer order, bottom to top: coat, style, pad, saddle, bridle (pads sit under saddles, so any pad
works with any saddle).

PNG requirements
- Exactly 224 x 128 pixels: the vanilla horse sheet layout (7 columns x 4 rows of 32x32 frames,
  same as Content/Animals/horse).
- Overlays must be transparent everywhere except the tack or hair itself.
- Files with the wrong size, or that aren't PNGs, are skipped with a warning in the SMAPI log.

Names
- The menu name comes from the file name: "LightBlue.png" or "light_blue.png" -> "Light Blue".
- In saddles/pads/bridles a "Saddle_", "Pad_" or "Bridle_" prefix is removed ("Saddle_Brown.png" -> "Brown"),
  and that prefix decides the layer whichever of those three folders the file is in.
- In coats, a file with "Overlay" in its name is treated as a style.
- A prefix listed in collections.json ("SpiritsEve_Pumpkin.png") puts the file in that collection
  ("Spirit's Eve: Pumpkin"). Add your own prefixes there; others are listed under "Other".
- "Name@elle.png" next to "Name.png" is an optional fit of the same overlay for Elle-shaped horse
  bodies; it's used automatically, not listed separately.
- "Name.spring.png", "Name.summer.png", "Name.fall.png", "Name.winter.png" next to "Name.png" are optional
  per-season versions, picked automatically from the in-game season ("Name.fall@elle.png" for the Elle fit).
  "Name.png" is used for any season without its own file. They aren't listed separately.
- Folder and file names aren't case-sensitive. Two files with the same name, or identical copies,
  are shown once.

After adding files, restart the game or type horsetack_reload in the SMAPI console.

Multiplayer: only the choice is synced, not the pictures, so every player should have the same PNGs
(same file names) in this folder. A player missing one just sees that layer skipped.

Only add art you made or have permission to use.
