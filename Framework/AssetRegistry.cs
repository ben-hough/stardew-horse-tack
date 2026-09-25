using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Finds coat and overlay art in this mod's own assets folder and loads pixels on demand. No other mod's files are read.</summary>
    internal sealed class AssetRegistry
    {
        private const string BundledSource = "assets folder";
        public const int SheetWidth = 224;
        public const int SheetHeight = 128;

        private readonly IModHelper Helper;
        private readonly Dictionary<TackLayer, List<TackOption>> Options = new();
        private readonly Dictionary<string, TackOption> ById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TackOption> ByHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PixelData?> Pixels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D?> Textures = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The mod's assets folder (Mods/HorseTack/assets).</summary>
        public string AssetsDirectory => FindChildDirectory(this.Helper.DirectoryPath, "assets") ?? Path.Combine(this.Helper.DirectoryPath, "assets");

        public AssetRegistry(IModHelper helper)
        {
            this.Helper = helper;
            foreach (TackLayer layer in TackLayers.DrawOrder)
                this.Options[layer] = new List<TackOption>();
        }

        public IReadOnlyList<TackOption> Get(TackLayer layer) => this.Options[layer];

        public int TotalCount => this.Options.Values.Sum(p => p.Count);

        public bool TryGet(string id, out TackOption option) => this.ById.TryGetValue(id, out option!);

        public bool IsValid(TackLayer layer, string id) => id == "" || (this.ById.TryGetValue(id, out TackOption? o) && o.Layer == layer);

        public string DisplayName(TackLayer layer, string id)
        {
            if (id == "")
                return layer == TackLayer.Coat ? I18n.Get("option.keep") : I18n.Get("option.none");
            return this.ById.TryGetValue(id, out TackOption? o) ? o.DisplayName : I18n.Get("option.missing", new { id });
        }

        /// <summary>Rescan the assets folder.</summary>
        public void Reload()
        {
            foreach (var list in this.Options.Values)
                list.Clear();
            this.ById.Clear();
            this.ByHash.Clear();
            this.Pixels.Clear();
            this.Textures.Clear();

            var stats = new ScanStats();
            this.ScanAssets(this.Helper.DirectoryPath, this.Helper.ModContent, BundledSource, stats);

            if (this.TotalCount == 0)
            {
                Log.Info($"No horse art found in {this.AssetsDirectory} yet, so the stable wizard will only offer Keep current / None. "
                    + "Drop 224x128 PNGs into its coats, saddles, pads, bridles or styles folders (see README.txt there), then run horsetack_reload or restart.");
            }
            else
            {
                Log.Info($"Found {this.Get(TackLayer.Coat).Count} coats, {this.Get(TackLayer.Saddle).Count} saddles, {this.Get(TackLayer.Pad).Count} pads, {this.Get(TackLayer.Bridle).Count} bridles, {this.Get(TackLayer.Style).Count} styles in the assets folder ({stats}).");
            }
        }

        /// <summary>Resolve an id (including aliases of skipped duplicate files) to the option's canonical id. Unknown ids are returned unchanged.</summary>
        public string Canonical(string id) => id != "" && this.ById.TryGetValue(id, out TackOption? o) ? o.Id : id;

        /// <summary>Resolve every layer of a selection to canonical ids.</summary>
        public TackSelection Canonicalize(TackSelection sel)
        {
            foreach (TackLayer layer in TackLayers.DrawOrder)
                sel.Set(layer, this.Canonical(sel.Get(layer)));
            return sel;
        }

        /// <summary>Get premultiplied pixels for an option, or null (logged once) if missing/unreadable.</summary>
        public PixelData? GetPixels(string id)
        {
            if (this.Pixels.TryGetValue(id, out PixelData? cached))
                return cached;

            PixelData? result = null;
            if (!this.ById.TryGetValue(id, out TackOption? option))
                Log.WarnOnce("missing:" + id, $"Horse art '{id}' isn't in this computer's Mods/HorseTack/assets folder; skipping that layer. (Everyone should have the same PNGs there.)");
            else
            {
                Texture2D? tex = this.GetTexture(option);
                if (tex != null)
                {
                    var data = new Color[tex.Width * tex.Height];
                    tex.GetData(data);
                    result = new PixelData(tex.Width, tex.Height, data);
                }
            }
            this.Pixels[id] = result;
            return result;
        }

        /// <summary>Get the loaded texture for an option (used for menu swatches), or null.</summary>
        public Texture2D? GetTexture(string id) => this.ById.TryGetValue(id, out TackOption? o) ? this.GetTexture(o) : null;

        private Texture2D? GetTexture(TackOption option)
        {
            if (this.Textures.TryGetValue(option.Id, out Texture2D? cached))
                return cached;

            Texture2D? tex = null;
            try
            {
                tex = option.Content.Load<Texture2D>(option.RelativePath);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("load:" + option.Id, $"Couldn't load '{option.RelativePath}' from {option.SourceName}; skipping that layer.\n{ex.Message}");
            }
            this.Textures[option.Id] = tex;
            return tex;
        }

        /*********
        ** Scanning
        *********/
        /// <summary>Scan <c>{root}/assets/{layer folder}/*.png</c> (folder and file names matched case-insensitively).</summary>
        private void ScanAssets(string root, IModContentHelper content, string source, ScanStats stats)
        {
            string? assets = FindChildDirectory(root, "assets");
            if (assets == null)
                return;

            foreach (string dir in SafeDirectories(assets))
            {
                if (!TryGetFolderLayer(Path.GetFileName(dir), out TackLayer folderLayer))
                    continue;

                foreach (string file in SafePngFiles(dir))
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    TackLayer layer = RouteLayer(folderLayer, stem);
                    string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

                    // same validation for every source: a PNG laid out like the vanilla horse sheet
                    if (!TryReadPng(file, out int width, out int height, out byte[] bytes))
                    {
                        stats.Invalid++;
                        Log.WarnOnce("invalid:" + file, $"Skipped {source} file '{relative}': not a readable PNG.");
                        continue;
                    }
                    if (width != SheetWidth || height != SheetHeight)
                    {
                        stats.Invalid++;
                        Log.WarnOnce("size:" + file, $"Skipped {source} file '{relative}': it's {width}x{height}, but horse layers must be {SheetWidth}x{SheetHeight} (the vanilla horse sheet layout).");
                        continue;
                    }

                    string key = CanonicalKey(layer, stem);
                    if (key == "")
                    {
                        stats.Invalid++;
                        continue;
                    }
                    string folder = TackLayers.FolderName(layer);
                    string id = $"{folder}/{key}";
                    string hash = Convert.ToHexString(SHA1.HashData(bytes));

                    // de-duplicate: same id (e.g. "Brown.png" and "Saddle_Brown.png") or identical image content in the same layer
                    TackOption? existing = null;
                    if (this.ById.TryGetValue(id, out TackOption? byId))
                        existing = byId;
                    else if (this.ByHash.TryGetValue($"{layer}:{hash}", out TackOption? byHash))
                        existing = byHash;
                    if (existing != null)
                    {
                        if (existing.Layer == layer)
                            this.AddAlias(id, existing);
                        stats.Duplicates++;
                        Log.Trace($"Skipped duplicate {source} file '{relative}' (same as {existing.SourceName} '{existing.RelativePath}').");
                        continue;
                    }

                    string display = layer switch
                    {
                        TackLayer.Style => StyleName(stem),
                        TackLayer.Coat => Pretty(stem),
                        _ => Pretty(StripTackPrefix(stem))
                    };

                    var option = new TackOption
                    {
                        Id = id,
                        Layer = layer,
                        DisplayName = display,
                        SourceName = source,
                        Content = content,
                        RelativePath = relative,
                        Hash = hash
                    };
                    this.ById[id] = option;
                    this.ByHash[$"{layer}:{hash}"] = option;
                    this.Options[layer].Add(option);
                    stats.Loaded++;
                }
            }
        }

        private void AddAlias(string alias, TackOption option)
        {
            if (!this.ById.ContainsKey(alias))
                this.ById[alias] = option;
        }

        /// <summary>Map an assets subfolder name (case-insensitive) to its layer.</summary>
        public static bool TryGetFolderLayer(string folderName, out TackLayer layer)
        {
            switch (folderName.Trim().ToLowerInvariant())
            {
                case "coats": case "coat": layer = TackLayer.Coat; return true;
                case "styles": case "style": layer = TackLayer.Style; return true;
                case "saddles": case "saddle": layer = TackLayer.Saddle; return true;
                case "pads": case "pad": layer = TackLayer.Pad; return true;
                case "bridles": case "bridle": layer = TackLayer.Bridle; return true;
                default: layer = TackLayer.Coat; return false;
            }
        }

        /// <summary>Route a file to its real layer: an "...Overlay" file in the coats folder is a style, and a "Saddle_/Pad_/Bridle_" name prefix wins inside the tack folders.</summary>
        public static TackLayer RouteLayer(TackLayer folderLayer, string stem)
        {
            if (folderLayer == TackLayer.Coat)
                return stem.Contains("overlay", StringComparison.OrdinalIgnoreCase) ? TackLayer.Style : TackLayer.Coat;
            if (folderLayer is TackLayer.Saddle or TackLayer.Pad or TackLayer.Bridle)
            {
                TackLayer? prefixed = TackPrefix(stem, out _);
                if (prefixed != null)
                    return prefixed.Value;
            }
            return folderLayer;
        }

        /// <summary>The name part used in ids, e.g. (Saddle, "Saddle_Light_Blue") -> "light-blue", (Style, "PrismaticOverlay") -> "prismatic".</summary>
        public static string CanonicalKey(TackLayer layer, string stem)
        {
            string name = layer switch
            {
                TackLayer.Style => Regex.Replace(stem, "overlay", "", RegexOptions.IgnoreCase),
                TackLayer.Coat => stem,
                _ => StripTackPrefix(stem)
            };
            name = Pretty(name).ToLowerInvariant();
            name = Regex.Replace(name, "[^a-z0-9]+", "-").Trim('-');
            return name;
        }

        private static TackLayer? TackPrefix(string stem, out int length)
        {
            Match m = Regex.Match(stem, @"^(saddle|pad|bridle)[_\- ]+", RegexOptions.IgnoreCase);
            length = m.Success ? m.Length : 0;
            if (!m.Success)
                return null;
            return m.Groups[1].Value.ToLowerInvariant() switch
            {
                "saddle" => TackLayer.Saddle,
                "pad" => TackLayer.Pad,
                _ => TackLayer.Bridle
            };
        }

        private static string StripTackPrefix(string stem)
        {
            TackPrefix(stem, out int length);
            return length > 0 && length < stem.Length ? stem[length..] : stem;
        }

        /// <summary>Read a PNG's size from its header plus its bytes (for hashing).</summary>
        private static bool TryReadPng(string path, out int width, out int height, out byte[] bytes)
        {
            width = height = 0;
            bytes = Array.Empty<byte>();
            try
            {
                bytes = File.ReadAllBytes(path);
                return TryReadPngSize(bytes, out width, out height);
            }
            catch (Exception ex)
            {
                Log.Trace($"Couldn't read {path}: {ex.Message}");
                return false;
            }
        }

        public static bool TryReadPngSize(byte[] bytes, out int width, out int height)
        {
            width = height = 0;
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(signature) || bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
                return false;
            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            return width > 0 && height > 0;
        }

        private static string? FindChildDirectory(string parent, string name)
        {
            foreach (string dir in SafeDirectories(parent))
            {
                if (string.Equals(Path.GetFileName(dir), name, StringComparison.OrdinalIgnoreCase))
                    return dir;
            }
            return null;
        }

        private static IEnumerable<string> SafeDirectories(string dir)
        {
            try
            {
                return Directory.Exists(dir)
                    ? Directory.GetDirectories(dir).OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToArray()
                    : Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> SafePngFiles(string dir)
        {
            try
            {
                return Directory.GetFiles(dir)
                    .Where(p => string.Equals(Path.GetExtension(p), ".png", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /*********
        ** Helpers
        *********/
        /// <summary>"BrightRed" -> "Bright Red", "light_blue" -> "Light Blue".</summary>
        public static string Pretty(string stem)
        {
            string s = stem.Replace('_', ' ').Replace('-', ' ');
            s = Regex.Replace(s, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static string StyleName(string stem)
        {
            string core = Regex.Replace(stem, "overlay", "", RegexOptions.IgnoreCase).Trim('_', ' ', '-');
            if (core.Equals("Prismatic", StringComparison.OrdinalIgnoreCase))
                return I18n.Get("style.prismatic");
            return I18n.Get("style.generic", new { name = Pretty(core) });
        }
    }

    /// <summary>Per-source scan counts for the log.</summary>
    internal sealed class ScanStats
    {
        public int Loaded;
        public int Duplicates;
        public int Invalid;

        public void Add(ScanStats other)
        {
            this.Loaded += other.Loaded;
            this.Duplicates += other.Duplicates;
            this.Invalid += other.Invalid;
        }

        public override string ToString() => $"{this.Loaded} loaded, {this.Duplicates} duplicates skipped" + (this.Invalid > 0 ? $", {this.Invalid} invalid skipped" : "");
    }

    /// <summary>Premultiplied pixels of an image.</summary>
    internal sealed record PixelData(int Width, int Height, Color[] Data);
}
