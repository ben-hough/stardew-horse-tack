# Horse Tack & Styling 1.4.0: Nexus Mods page, ready to paste

Target: https://www.nexusmods.com/stardewvalley (Upload -> Upload a mod), account **MrGlim**. Same upload form as the Ghostwood Crook kit (mod 52912): **Create draft -> General -> Media -> Files -> Requirements -> Permissions**, then Preview -> Publish. The form rules and their sources are in the Ghostwood kit's `research.md`; the HorseTack-specific lookups are in `research.md` here.

> **Order of operations (important for SMAPI update checks)**
> 1. Create the draft (title, short description, game, category). Nexus assigns the mod ID right away (the number in `.../stardewvalley/mods/<ID>`).
> 2. Rebuild the zip with `"UpdateKeys": [ "Nexus:<ID>" ]` (version stays **1.4.0**) using the one-command rebuild in section 7, and **then** upload that zip in the Files tab. The current zip has `"UpdateKeys": []` on purpose.

Kit files (this folder):
| File | Use |
|---|---|
| `description.bbcode` | Full description (General tab) |
| `changelog.txt` | Files tab changelog (1.4.0) + full 1.0.0-1.4.0 history |
| `file-description.txt` | Files tab file description |
| `banner.png` | Header image, 1300x372 |
| `thumbnail.png` | Gallery image 1 (becomes the thumbnail), 1920x1080 |
| `gallery-2-witchy.png` ... `gallery-6-mix-and-match.png` | Gallery images 2-6, 1920x1080 |
| `tools/make_images.py` | Re-renders all images from the repo's own `assets/` (HorseTack art only) |
| `tools/zip_check.py` | Zip check (layout, manifest, no third-party art) |
| `tools/rebuild_with_updatekey.sh` + `tools/install-horsetack.ps1` | UpdateKey rebuild (box) + install/copy/hash check (PC) |

---

## 1. Create draft / General tab

| Field | Value |
|---|---|
| **Mod name** | `Horse Tack & Styling` (20 chars; same as the manifest `Name`). |
| **Short description / summary** | `Style your horse at the stable: pick coat, saddle, saddle pad, bridle and styling separately with a live preview. 57 original pieces (festivals, breeds, Witchy, Forest Spirit with seasonal trims). Synced in multiplayer.` (219 characters, under the 255 used by the Nexus API and the old 350 cap.) |
| **Game** | Stardew Valley. **Can't be changed after the draft is created.** |
| **Category** | **Pets / Horses** (category 8). Why: it's purely about horses, and that's the category the live Nexus data shows for the closest mods: Elle's Cuter Horses (20042), Multiplayer Horse Reskin (7681) and its Continued version (43368) are all in Pets / Horses. Runner-up: **Visuals and Graphics** (25), which is where Bog's Elk Spirit Horse and Stable (24483) sits. Either is defensible; Pets / Horses is where horse players browse. |
| **Full description** | Paste `description.bbcode`. |
| **Language** | English |
| **Is this a translation?** | No |
| **Adult content** | No |

### Tags (exact names from the live Stardew tag list, Nexus GraphQL `legacyTags(gameId:1303)`, checked 25 Sep 2026)
Apply these:
- **Generative AI Usage -> `AI-Generated Content`** (REQUIRED). The C# code and the art-generating scripts were written by AI ("If your mod is primarily generated with AI, you should apply the AI-Generated Content tag").
- **Generative AI Usage -> `AI Media`** (REQUIRED). The page text, banner and gallery images were produced with AI help ("AI promotional art, thumbnails, videos, mod page descriptions or media outside of the mod content in-game").
- Do **NOT** use `AI Assisted` (for developer-led code with limited AI involvement; needs strong evidence, and moderators replace it if unjustified).
- Requirements -> `SMAPI`
- Compatibility -> `Version 1.6 Compatible`
- Components -> `Sprites` (it adds sprite art: coats and tack overlays)
- Optional: Components -> `User Interface` (it adds the stable wizard menu). Fine to skip.
- Attributes: none. The mod is cosmetic, and the Stardew list has no "Cosmetic" or "Visual" attribute tag (`Gameplay`, `Replacer` and `Quality of Life` don't fit).
- Language -> `English`
- Skip anything you can't justify ("Abuse of the tagging system ... is prohibited"). No player-count tags, no event tags.

### Extra options (collapsed section on the General tab)
| Field | Value |
|---|---|
| **Author** | `MrGlim` |
| **Version** | `1.4.0` (semantic version, same as the manifest and the file) |
| **Team / other users with access** | none |
| **Comments** | Enabled |
| **Bug tracking** | Enabled (misalignment reports: "which coat + which piece") |
| **Donations** | Your choice; the description doesn't solicit anything. |
| **Community tag voting** | Leave enabled (the AI tags are self-applied accurately). |
| **Download mirrors** | Optional: `GitHub (source)` -> https://github.com/ben-hough/stardew-horse-tack |

## 2. Media tab
| Slot | File (box path) | Notes |
|---|---|---|
| **Header image** | `/workspace/codex-horsetack/nexus-page/banner.png` (1300x372) | Same size as the Ghostwood header. Title on the left, three styled horses on the right (Spirit's Eve / Forest Spirit in fall / Witchy). |
| **Gallery 1 = thumbnail** | `thumbnail.png` (1920x1080) | Title card with four full looks. "The first image you upload automatically becomes your thumbnail." |
| Gallery 2 | `gallery-2-witchy.png` | Two Witchy looks, front / side / back / grazing. |
| Gallery 3 | `gallery-3-forest-spirit-seasons.png` | Two Forest Spirit looks in spring / summer / fall / winter. |
| Gallery 4 | `gallery-4-festivals-and-valley.png` | 18 festival and valley collections. |
| Gallery 5 | `gallery-5-coats.png` | All 16 coats (7 breeds + 9 themed). |
| Gallery 6 | `gallery-6-mix-and-match.png` | Layer build-up (coat -> saddle -> pad -> bridle -> styling) + 12 mixed looks. |
| **Later** | Real in-game screenshots of the wizard (stable post, a step with the preview, the confirm step) | Recommended once you have them. The images above are composed sprite sheets, not screenshots, and the disclosure says so. |
| **Video links** | none | |

All images use only HorseTack's own art from the repo's `assets/` (byte-identical to the PNGs in the zip): no vanilla sheet, no Elle's Cuter Horses art, no `/previews/` sheets. Every look shown is a legal wizard selection (one piece per layer, pad only with a saddle), drawn coat -> styling -> pad -> saddle -> bridle, nearest-neighbour upscaled. Fonts: Pixelify Sans and Silkscreen (SIL OFL 1.1).

## 3. Files tab
| Field | Value |
|---|---|
| **Archive** | `HorseTack-1.4.0.zip` **rebuilt with the UpdateKey** (section 7). One top-level `HorseTack/` folder: `HorseTack.dll, HorseTack.pdb, manifest.json, README-TESTERS.txt, i18n/default.json, assets/ (collections.json, README.txt, 110 PNGs)`. About 340 KB. No config.json. |
| **File (display) name** | `Horse Tack and Styling` (22 chars; the API allows only letters, digits, space and `_'().-`, so no `&`) |
| **Version** | `1.4.0` |
| **Category** | **Main Files** |
| **Description** | Paste `file-description.txt` (435 chars) |
| **Update mod version to match this file** | Yes |
| **Mod manager download** | Allowed; primary mod manager download |
| **Show requirements pop-up** | Yes (SMAPI) |
| **Changelog entry** | Paste the 1.4.0 block from `changelog.txt` |

## 4. Requirements tab
| Type | Entry | Note field |
|---|---|---|
| Nexus requirement | **SMAPI - Stardew Modding API**, https://www.nexusmods.com/stardewvalley/mods/2400 | `Required. Built/tested with SMAPI 4.5.2 (manifest minimum 4.0.0).` |
| Off-site / DLC | none | |
| Elle's Cuter Horses (20042), Generic Mod Config Menu (5098) | **Don't list them as requirements.** Both are optional and linked in the description ("Just try to stick to hard requirements"). If you want Elle's listed anyway, note it as `OPTIONAL companion - HorseTack reads her art from your install; nothing of hers is included`. | |

## 5. Permissions tab
**Other user's assets:** HorseTack's art was generated by its own scripts, but the **coats are recolours of the vanilla horse sprite's shape (ConcernedApe)**, as is normal for Stardew retextures. Pick the option that says assets belong to you or are free-to-use modder's resources only if you're comfortable counting game-derived sprite shapes that way (common practice for Stardew recolours); otherwise pick the "some assets belong to other authors" style option and rely on the file credits below. Either way the zip contains **no Elle art and no third-party files** (section 8).

Recommended presets (same as Ghostwood):
| Permission | Choice |
|---|---|
| Upload permission | Must get permission from me before uploading to other sites |
| Modification permission | Allowed to modify and release fixes/improvements with credit (the code is MIT on GitHub anyway) |
| Conversion permission | Not allowed (Stardew-only SMAPI mod) |
| Asset use permission | Allowed with credit |
| Asset use in mods/files that are being sold | Not allowed |
| Asset use in mods/files that earn Donation Points | Allowed |

**Author notes (custom permission text):**
```
The source code is MIT-licensed on GitHub: https://github.com/ben-hough/stardew-horse-tack
Feel free to fix, update or improve this mod as long as you credit MrGlim and link back to this page. Please ask before re-uploading it elsewhere. Don't include any part of it in anything sold for money.
The MIT license covers the mod's code, docs and its own generated art, not game assets or Elle's Cuter Horses art (which is never included; it's read from the player's own install).
Note: the code and art scripts were produced with AI assistance (see the AI disclosure in the description).
```
**File credits:**
```
ConcernedApe (Stardew Valley; coats are recolours of the vanilla horse sprite's shape); Pathoschild (SMAPI); Andreas Pardeike (Harmony); spacechase0 (Generic Mod Config Menu API); Elle / Junimods (Elle's Cuter Horses, optional companion read at runtime, never bundled); DelphinWave (idea inspiration from Multiplayer Horse Reskin, MIT, no code used). Page images use the fonts Pixelify Sans and Silkscreen (SIL OFL 1.1).
```
**Donation Points:** optional, opted in from the Mod Rewards page. DP requires "only your original work" or cleared assets; game-derived coat shapes and AI-written code make that your call.

## 6. AI disclosure (already in the description; short version for elsewhere)
```
AI disclosure: This mod was built with heavy AI assistance. The C# code, and the Python scripts that procedurally draw all of its art (coats recoloured from the vanilla horse sprite's shape, tack drawn from templates), were written by an AI coding assistant (Grok) under my direction (no image-generation model was used). The mod page text and images were also produced with AI help; the images are the mod's own sprites arranged by a script, not screenshots. I tested it in-game on Windows. Tagged "AI-Generated Content" and "AI Media" per Nexus Mods rules.
```
Declare it in (1) **Tags -> Generative AI Usage**: `AI-Generated Content` + `AI Media` (the actual requirement), (2) the description's AI disclosure section, (3) optionally the Author notes.

## 7. Manifest change after the draft exists (one command)
The zip currently ships `"UpdateKeys": []`. Once the draft has an ID:

**Box (one command):**
```
bash /workspace/codex-horsetack/nexus-page/tools/rebuild_with_updatekey.sh <ID> --commit
```
It sets `"UpdateKeys": [ "Nexus:<ID>" ]` in the repo manifest (Version stays 1.4.0), does the Release build (0 warnings required) and AssetScanCheck, stages and zips `share/HorseTack-1.4.0.zip` (the old zip is kept once as `share/HorseTack-1.4.0-no-updatekey.zip`), runs `zip_check.py --expect-updatekey Nexus:<ID>`, writes `HorseTack-1.4.0.zip.sha256` and prints the SHA-256. `--commit` commits and pushes only `manifest.json` (and refuses if anything else is dirty); leave it off to keep the change local.

**PC (after CopyFromBox of the zip to `C:\Users\Glim\codex-stage\HorseTack-1.4.0.zip`):**
```
powershell -ExecutionPolicy Bypass -File E:\Codex-Mods\stardew\HorseTack\nexus-page\tools\install-horsetack.ps1 -NexusId <ID> -Sha256 <hash printed by the box>
```
It checks the staged zip's hash and manifest, copies it to `E:\Codex-Mods\stardew\HorseTack\release\` (the old zip is kept once as `HorseTack-1.4.0-no-updatekey.zip`), runs `git pull --ff-only` in the E: clone (or patches its manifest in place if the change wasn't pushed), reinstalls `Mods\HorseTack` keeping `config.json` byte-for-byte (backup in `codex-stage\horsetack-config-backup.json`), and hash-checks every installed file against the zip. Both scripts were dry-run tested against temporary folders (fake ID 99999, all PASS) and changed nothing real.

Then upload the new `HorseTack-1.4.0.zip` in the Files tab. Keep the page, file and manifest version all at `1.4.0`.

## 8. Zip check (current 1.4.0 zip, before the UpdateKey)
`python3 /workspace/codex-horsetack/nexus-page/tools/zip_check.py /workspace/codex-horsetack/share/HorseTack-1.4.0.zip`
- sha256 `61a7a0970bdd9ce2e8c5fe95bc4358d9d8025b8e9ca09e6c81b158b363c3753a`, 117 files, single `HorseTack/` folder
- manifest Version `1.4.0`, UniqueID `MrGlim.HorseTack`, UpdateKeys `[]`
- no config.json / deps.json / xnb
- all 110 PNGs byte-identical to the repo's own `assets/`
- no file matches any of 329 third-party reference files (Elle's Cuter Horses reference copy + codex-thirdparty) by SHA-256 or decoded pixels
- RESULT: PASS

## 9. Pre-publish checklist
- [ ] Draft created -> ID noted -> rebuild command run (box + PC) -> zip check PASS with the UpdateKey
- [ ] General: name, summary, category Pets / Horses (or Visuals and Graphics), description pasted, English, tags incl. AI-Generated Content + AI Media
- [ ] Media: banner.png header, thumbnail.png first, gallery 2-6 (real screenshots later)
- [ ] Files: main file 1.4.0, display name `Horse Tack and Styling`, file description, changelog
- [ ] Requirements: SMAPI (2400) only
- [ ] Permissions + author notes + credits
- [ ] Optional polish before the rebuild: `docs/README-TESTERS.txt` (shipped in the zip) still says "(test build)" in its first line
- [ ] Preview -> Publish
