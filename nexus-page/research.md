# Research notes for the Horse Tack & Styling page (25 Sep 2026)

The Nexus upload-form rules (tabs, field limits, AI tagging rules, permissions presets, SMAPI UpdateKeys) are unchanged from the Ghostwood Crook kit; see `E:\Codex-Mods\stardew\GhostwoodCrook\nexus-page\research.md` for the quotes and sources. HorseTack-specific checks:

## Live Nexus lookups (public GraphQL, https://api.nexusmods.com/v2/graphql, no login)
- `mods(filter:{gameDomainName:"stardewvalley", name:"Cuter Horses"})` -> **20042 "Elle's Cuter Horses"**, category **Pets / Horses**.
- `name:"Multiplayer Horse Reskin"` -> **7681 "Multiplayer Horse Reskin"** (DellyBelly) and **43368 "MultiplayerHorseReskin - Continued"** (TadeDM), both **Pets / Horses**.
- `name:"Elk"` -> **24483 "Bog's Elk Spirit Horse and Stable"** (BogWyytch), category **Visuals and Graphics**. (The task called it "Bog's Elk Forest Spirit"; the page uses the real title.)
- `legacyTags(gameId:1303)`: the exact tag names used on the page exist: `AI-Generated Content`, `AI Media`, `AI Assisted` (Generative AI Usage); `SMAPI` (Requirements); `Version 1.6 Compatible` (Compatibility); `Sprites`, `User Interface` (Components). There's no Cosmetic/Visual attribute tag.
- Stardew categories include **Pets / Horses (8)** and **Visuals and Graphics (25)**.

## Claims checked against the code/assets at commit 1bbac0fa (1.4.0)
- Manifest: Name "Horse Tack & Styling", Version 1.4.0, UniqueID MrGlim.HorseTack, MinimumApiVersion 4.0.0, MinimumGameVersion 1.6.0, optional deps GMCM + Elle.CuterHorses, `UpdateKeys: []`.
- Piece count: 57 base files = 16 coats, 12 saddles, 11 pads, 9 bridles, 9 styles (seasonal `.spring/.fall/.winter` files are trims of Moss Cloak and Antler Crown, not extra options). Every overlay has an `@elle` fit variant.
- Collections and display names: `assets/collections.json`.
- One piece per layer (`TackSelection` has single Coat/Style/Saddle/Pad/Bridle strings); pad step only when a saddle is chosen; draw order coat, style, pad, saddle, bridle.
- Controls (`Menus/TackWizardMenu.cs`): Left/Right = collection filter, PageUp/PageDown + bumpers = page, wheel = 3 rows, Up/Down/Enter/Backspace (keyboard, non-snapping), triggers = Back/Next.
- Multiplayer (`Framework/TackService.cs`): host validates (owner/host or AnyoneCanEdit; not while a farmhand rides; tractors refused) and writes modData; unknown-but-well-formed ids accepted so each computer draws what it has; farmhand gets a message if the host lacks the mod.
- Multiplayer Horse Reskin warning: `ModEntry.cs` checks `DelphinWave.MultiplayerHorseReskin`.
- Config defaults: `ModConfig.cs`. Console commands: `ModEntry.cs`.
- Known limitations: `docs/ART-CATALOG.md` weak spots and README compatibility notes.
- Not claimed: split-screen, macOS/Linux/Android (untested).
