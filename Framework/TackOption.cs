using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>One selectable art file (a coat sheet or an overlay).</summary>
    internal sealed class TackOption
    {
        /// <summary>Stable id stored in modData, e.g. "saddles/brown" for assets/saddles/Brown.png.</summary>
        public string Id { get; init; } = "";
        public TackLayer Layer { get; init; }
        /// <summary>Full label, e.g. "Spirit's Eve: Pumpkin".</summary>
        public string DisplayName { get; init; } = "";
        /// <summary>Label without the collection, e.g. "Pumpkin".</summary>
        public string ShortName { get; init; } = "";
        /// <summary>Collection shown in the wizard filter, e.g. "Spirit's Eve" or "Appaloosa".</summary>
        public string Collection { get; init; } = "";
        /// <summary>Short source tag shown in the wizard: "HorseTack" or "Elle".</summary>
        public string Source { get; init; } = "";
        /// <summary>Body layout this sheet is drawn for (coats) or was drawn against (overlays).</summary>
        public BodyShape Shape { get; init; }
        /// <summary>Optional "@elle" variant of an overlay, fitted to Elle-shaped bodies (path relative to the mod folder).</summary>
        public string? ElleVariantRelativePath { get; set; }
        /// <summary>Human name of the art source (for log messages).</summary>
        public string SourceName { get; init; } = "";
        /// <summary>Content helper used to load the file (this mod's own, or the read-only Elle bridge).</summary>
        public IModContentHelper Content { get; init; } = null!;
        /// <summary>Path relative to the mod folder.</summary>
        public string RelativePath { get; init; } = "";
        /// <summary>SHA-1 of the file bytes (used to skip duplicate copies).</summary>
        public string Hash { get; init; } = "";
        /// <summary>Optional per-season files ("Name.fall.png" -> key "fall", "Name.fall@elle.png" -> key "fall@elle"), paths relative to the mod folder.</summary>
        public Dictionary<string, string> SeasonalPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether this option has any per-season files.</summary>
        public bool IsSeasonal => this.SeasonalPaths.Count > 0;

        /// <summary>The file to draw for a body fit and season. The body fit wins over the season: an Elle-shaped body uses the "@elle" seasonal file, else the "@elle" fit, and only falls back to the vanilla-fit seasonal file if there's no "@elle" fit at all.</summary>
        public string ResolvePath(bool elleFit, string? season)
        {
            if (season != null && this.SeasonalPaths.Count > 0)
            {
                if (elleFit && this.SeasonalPaths.TryGetValue(season + AssetRegistry.ElleVariantSuffix, out string? elleSeasonal))
                    return elleSeasonal;
                if ((!elleFit || this.ElleVariantRelativePath == null) && this.SeasonalPaths.TryGetValue(season, out string? seasonal))
                    return seasonal;
            }
            return elleFit && this.ElleVariantRelativePath != null ? this.ElleVariantRelativePath : this.RelativePath;
        }
    }
}
