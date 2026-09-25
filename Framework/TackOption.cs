using StardewModdingAPI;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>One selectable art file (a coat sheet or an overlay).</summary>
    internal sealed class TackOption
    {
        /// <summary>Stable id stored in modData, e.g. "saddles/brown" for assets/saddles/Brown.png.</summary>
        public string Id { get; init; } = "";
        public TackLayer Layer { get; init; }
        public string DisplayName { get; init; } = "";
        /// <summary>Human name of the art source (for log messages).</summary>
        public string SourceName { get; init; } = "";
        /// <summary>Content helper used to load the file (this mod's own content helper).</summary>
        public IModContentHelper Content { get; init; } = null!;
        /// <summary>Path relative to the mod folder.</summary>
        public string RelativePath { get; init; } = "";
        /// <summary>SHA-1 of the file bytes (used to skip duplicate copies).</summary>
        public string Hash { get; init; } = "";
    }
}
