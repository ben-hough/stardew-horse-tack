using StardewModdingAPI;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>One selectable art file (a coat sheet or an overlay).</summary>
    internal sealed class TackOption
    {
        /// <summary>Stable id stored in modData, e.g. "Elle.CuterHorses/Saddle_Brown".</summary>
        public string Id { get; init; } = "";
        public TackLayer Layer { get; init; }
        public string DisplayName { get; init; } = "";
        /// <summary>Human name of the art source (e.g. "Elle's Cuter Horses").</summary>
        public string SourceName { get; init; } = "";
        /// <summary>Content pack used to load the file (may be a temporary pack for Elle's folder).</summary>
        public IContentPack Pack { get; init; } = null!;
        /// <summary>Path relative to the pack folder.</summary>
        public string RelativePath { get; init; } = "";
    }
}
