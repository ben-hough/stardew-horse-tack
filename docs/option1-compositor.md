> Moved from the research phase. The script now lives at `tools/composite_tack.py`; `poc/` outputs are kept locally only (they contain Elle's art) and are not in this repo.

# HorseTack – research + option-1 proof of concept

Generated 2026-09-25 (research only, nothing installed into the game).

## Contents
- `composite_tack.py` – batch compositor (Python 3 + Pillow). `py composite_tack.py -h` for help.
- `poc/` – 3 horses x 3 tack combos made from Elle's Cuter Horses assets:
  - `poc/_preview_fullsheets_3x.png` – every POC sheet, full 224x128 sheet at 3x
  - `poc/_preview_closeup_6x.png` – frames 0/8/14/21 (down / right / up / idle row) at 6x
  - `poc/<Horse>__<Saddle>__<Pad>__<Bridle>.png` – drop-in 224x128 sheets (`none` = layer off)
- `DESIGN.md` – option-2 (layered, multiplayer-synced mod) design sketch + parent-mod findings.

## Quick use (Windows)
```powershell
# Python 3.12 + Pillow are already installed; use the py launcher (plain "python" is the Store stub)
$E = "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\[CP] Elle's Cuter Horses\assets"
# how many files would a full run make?
py composite_tack.py --horses "$E\Horse" --saddles "$E\Saddles\Saddle_*.png" --bridles "$E\Saddles\Bridle_*.png" --out out --dry-run
# curated run with preview
py composite_tack.py --horses "$E\Horse" --only-horses SolidBrown,PintoSilver `
  --saddles "$E\Saddles\Saddle_*.png" --only-saddles none,Brown `
  --pads "$E\Saddles\Pad_*.png" --only-pads none,Red `
  --bridles "$E\Saddles\Bridle_*.png" --only-bridles none,Black `
  --out out --preview out\_preview.png
```
Full cross products are huge (79 horses x 25 saddles x 25 bridles = 49,375 files;
with pads 1,186,975), so the script refuses more than `--max-outputs` (default 2000).
Layer order: horse -> saddle -> pad -> bridle (Elle's order; saddle and pad pixels never overlap).
`PrismaticOverlay.png` in Elle's Horse folder is auto-skipped (it is an overlay, not a horse).
