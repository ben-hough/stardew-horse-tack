namespace MrGlim.HorseTack.Framework
{
    /// <summary>A selectable layer. Draw order (bottom to top): coat, style, saddle, pad, bridle.</summary>
    internal enum TackLayer
    {
        Coat,
        Style,
        Saddle,
        Pad,
        Bridle
    }

    internal static class TackLayers
    {
        /// <summary>Layers in draw order.</summary>
        public static readonly TackLayer[] DrawOrder = { TackLayer.Coat, TackLayer.Style, TackLayer.Saddle, TackLayer.Pad, TackLayer.Bridle };

        /// <summary>The modData key prefix.</summary>
        public const string ModDataPrefix = "MrGlim.HorseTack/";

        public static string ModDataKey(TackLayer layer) => ModDataPrefix + FolderName(layer).TrimEnd('s');

        /// <summary>Folder name under the mod's assets/ folder.</summary>
        public static string FolderName(TackLayer layer) => layer switch
        {
            TackLayer.Coat => "coats",
            TackLayer.Style => "styles",
            TackLayer.Saddle => "saddles",
            TackLayer.Pad => "pads",
            TackLayer.Bridle => "bridles",
            _ => layer.ToString().ToLowerInvariant()
        };

        public static bool TryParse(string? raw, out TackLayer layer)
        {
            switch (raw?.Trim().ToLowerInvariant())
            {
                case "coat": case "coats": case "horse": layer = TackLayer.Coat; return true;
                case "style": case "styles": case "styling": case "hair": layer = TackLayer.Style; return true;
                case "saddle": case "saddles": layer = TackLayer.Saddle; return true;
                case "pad": case "pads": layer = TackLayer.Pad; return true;
                case "bridle": case "bridles": layer = TackLayer.Bridle; return true;
                default: layer = TackLayer.Coat; return false;
            }
        }
    }
}
