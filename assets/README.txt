Horse Tack & Styling - assets folder
====================================

The mod only uses art from this folder (plus each horse's current look for "Keep current").
It doesn't read any other mod's files, and doesn't need Content Patcher.
If these folders are empty the stable wizard still works; it just offers Keep current / None.

Put PNGs in these folders:

  coats\     full horse sheets (the whole horse). Shown under "Coat".
  saddles\   saddle overlays.
  pads\      saddle pad overlays (only drawn when a saddle is chosen).
  bridles\   bridle overlays.
  styles\    styling overlays, e.g. a coloured mane (drawn over the coat, under the tack).

Layer order, bottom to top: coat, style, saddle, pad, bridle.

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
- Folder and file names aren't case-sensitive. Two files with the same name, or identical copies,
  are shown once.

After adding files, restart the game or type horsetack_reload in the SMAPI console.

Multiplayer: only the choice is synced, not the pictures, so every player needs the same PNGs
(same file names) in this folder.

Only add art you made or have permission to use.
