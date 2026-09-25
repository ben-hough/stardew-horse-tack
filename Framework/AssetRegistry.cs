using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Which body layout a coat sheet uses. Overlays can ship an "@elle" variant fitted to Elle's Cuter Horses bodies.</summary>
    internal enum BodyShape { Vanilla, Elle }

    /// <summary>Finds coat and overlay art in this mod's own assets folder and (read-only, at runtime) in Elle's Cuter Horses if installed, and loads pixels on demand.</summary>
    internal sealed class AssetRegistry
    {
        private const string BundledSource = "HorseTack";
        public const string ElleSource = "Elle";
        public const string ElleModId = "Elle.CuterHorses";
        private const string ElleBridgeId = "MrGlim.HorseTack.EllesCuterHorsesBridge";
        public const string ElleVariantSuffix = "@elle";
        public const int SheetWidth = 224;
        public const int SheetHeight = 128;

        /// <summary>Season names accepted in "Name.season.png" file names (the game's season keys).</summary>
        public static readonly string[] Seasons = { "spring", "summer", "fall", "winter" };
        private static readonly Regex SeasonSuffix = new(@"^(.+)\.(spring|summer|fall|winter)$", RegexOptions.IgnoreCase);

        private static readonly string[] EllePlainColours = { "Red", "Orange", "Yellow", "Green", "Teal", "Turquoise", "Blue", "Purple", "Pink" };
        private static readonly string[] ElleFamilies = { "Appaloosa", "Pinto", "Solid", "Speckled", "Roan", "Void" };

        private readonly IModHelper Helper;
        private readonly Dictionary<TackLayer, List<TackOption>> Options = new();
        private readonly Dictionary<string, TackOption> ById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TackOption> ByHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PixelData?> Pixels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D?> Textures = new(StringComparer.OrdinalIgnoreCase);
        private IContentPack? ElleBridge;
        private string? ElleBridgeDir;

        /// <summary>Current season key ("spring", "summer", "fall", "winter") used to pick per-season files. Every player's game follows the host's date, so all computers pick the same season.</summary>
        public Func<string> SeasonProvider { get; set; } = GameSeason;

        /// <summary>The mod's assets folder (Mods/HorseTack/assets).</summary>
        public string AssetsDirectory => FindChildDirectory(this.Helper.DirectoryPath, "assets") ?? Path.Combine(this.Helper.DirectoryPath, "assets");

        /// <summary>Elle's Cuter Horses folder, if installed.</summary>
        public string? ElleDirectory { get; private set; }

        /// <summary>The body layout of the game's own horse texture on this computer: Elle's Content Patcher pack always replaces it when installed.</summary>
        public BodyShape KeepCurrentShape => this.Helper.ModRegistry.IsLoaded(ElleModId) ? BodyShape.Elle : BodyShape.Vanilla;

        public AssetRegistry(IModHelper helper)
        {
            this.Helper = helper;
            foreach (TackLayer layer in TackLayers.DrawOrder)
                this.Options[layer] = new List<TackOption>();
        }

        public IReadOnlyList<TackOption> Get(TackLayer layer) => this.Options[layer];

        /// <summary>Collection names in a layer, in list order (empty collections never appear).</summary>
        public IReadOnlyList<string> Collections(TackLayer layer) => this.Options[layer].Select(o => o.Collection).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public int TotalCount => this.Options.Values.Sum(p => p.Count);

        public int CountFrom(string source) => this.Options.Values.Sum(p => p.Count(o => o.Source == source));

        public bool TryGet(string id, out TackOption option) => this.ById.TryGetValue(id, out option!);

        /// <summary>Whether an id is acceptable for a layer. Known ids must match the layer; unknown ids are accepted if well-formed, because another player may have art this computer doesn't (each computer then draws what it has).</summary>
        public bool IsValid(TackLayer layer, string id)
        {
            if (id == "")
                return true;
            if (this.ById.TryGetValue(id, out TackOption? o))
                return o.Layer == layer;
            return IsWellFormedId(id);
        }

        /// <summary>A safe-looking id such as "saddles/brown" or "Elle.CuterHorses/Saddle_Brown".</summary>
        public static bool IsWellFormedId(string id) => id.Length <= 120 && Regex.IsMatch(id, @"^[A-Za-z0-9][A-Za-z0-9._\-]*/[A-Za-z0-9][A-Za-z0-9._\- ]*$");

        public string DisplayName(TackLayer layer, string id)
        {
            if (id == "")
                return layer == TackLayer.Coat ? I18n.Get("option.keep") : I18n.Get("option.none");
            return this.ById.TryGetValue(id, out TackOption? o) ? o.DisplayName : I18n.Get("option.missing", new { id });
        }

        /// <summary>Rescan the assets folder and Elle's Cuter Horses.</summary>
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
            int ours = this.TotalCount;

            var elleStats = new ScanStats();
            this.LoadElle(elleStats);
            int elle = this.TotalCount - ours;

            if (this.TotalCount == 0)
            {
                Log.Info($"No horse art found in {this.AssetsDirectory} yet, so the stable wizard will only offer Keep current. "
                    + "Drop 224x128 PNGs into its coats, saddles, pads, bridles or styles folders (see README.txt there), or install Elle's Cuter Horses, then run horsetack_reload or restart.");
            }
            else
            {
                string elleText = this.ElleDirectory != null ? $"{elle} from Elle's Cuter Horses ({elleStats})" : "Elle's Cuter Horses not installed (optional)";
                Log.Info($"Found {this.Get(TackLayer.Coat).Count} coats, {this.Get(TackLayer.Saddle).Count} saddles, {this.Get(TackLayer.Pad).Count} pads, {this.Get(TackLayer.Bridle).Count} bridles, {this.Get(TackLayer.Style).Count} styles: {ours} from HorseTack's assets folder ({stats}), {elleText}.");
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

        /// <summary>Get premultiplied pixels for an option drawn on a body of the given shape, or null if this computer doesn't have it (noted once in the trace log; the caller skips that layer).</summary>
        public PixelData? GetPixels(string id, BodyShape shape = BodyShape.Vanilla)
        {
            if (!this.ById.TryGetValue(id, out TackOption? option))
            {
                Log.TraceOnce("missing:" + id, $"Horse art '{id}' isn't installed on this computer (another player may have art you don't); drawing that layer as vanilla/none here.");
                return null;
            }

            bool elleFit = shape == BodyShape.Elle && option.Layer != TackLayer.Coat;
            string relative = option.ResolvePath(elleFit, option.IsSeasonal ? this.CurrentSeason() : null);
            string cacheKey = relative == option.RelativePath ? option.Id : option.Id + "|" + relative;
            if (this.Pixels.TryGetValue(cacheKey, out PixelData? cached))
                return cached;

            PixelData? result = null;
            Texture2D? tex = relative == option.RelativePath ? this.GetTexture(option) : this.LoadTexture(option, relative, cacheKey);
            if (tex != null)
            {
                var data = new Color[tex.Width * tex.Height];
                tex.GetData(data);
                result = new PixelData(tex.Width, tex.Height, data);
            }
            this.Pixels[cacheKey] = result;
            return result;
        }

        /// <summary>Get the loaded texture for an option (used for menu swatches), or null.</summary>
        public Texture2D? GetTexture(string id) => this.ById.TryGetValue(id, out TackOption? o) ? this.GetTexture(o) : null;

        private Texture2D? GetTexture(TackOption option) => this.LoadTexture(option, option.RelativePath, option.Id);

        /// <summary>A cache-key suffix naming the season if any chosen layer has per-season files (empty otherwise), so composites are rebuilt when the season changes.</summary>
        public string SeasonKey(TackSelection sel)
        {
            foreach (TackLayer layer in TackLayers.DrawOrder)
            {
                string id = sel.Get(layer);
                if (id != "" && this.ById.TryGetValue(id, out TackOption? o) && o.IsSeasonal)
                    return "@" + this.CurrentSeason();
            }
            return "";
        }

        /// <summary>Overlay layers in draw order: style, pad, saddle, bridle. Pads go under saddles (like real tack), so a pad never paints over a saddle from another set:
        /// Elle's pads are full blankets with a hole shaped for her saddle, and HorseTack's pads leave HorseTack's saddle footprint free. Within one set nothing changes, because a set's pad and saddle don't overlap.</summary>
        public IReadOnlyList<TackLayer> OverlayOrder(TackSelection sel) => OverlayDrawOrder;

        private static readonly TackLayer[] OverlayDrawOrder = { TackLayer.Style, TackLayer.Pad, TackLayer.Saddle, TackLayer.Bridle };

        /// <summary>The current season key, normalised; "summer" if it can't be read.</summary>
        public string CurrentSeason()
        {
            string season;
            try
            {
                season = this.SeasonProvider()?.Trim().ToLowerInvariant() ?? "";
            }
            catch
            {
                season = "";
            }
            return Seasons.Contains(season) ? season : "summer";
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string GameSeason() => StardewValley.Game1.currentSeason ?? "summer";

        private Texture2D? LoadTexture(TackOption option, string relativePath, string cacheKey)
        {
            if (this.Textures.TryGetValue(cacheKey, out Texture2D? cached))
                return cached;

            Texture2D? tex = null;
            try
            {
                tex = option.Content.Load<Texture2D>(relativePath);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("load:" + cacheKey, $"Couldn't load '{relativePath}' from {option.SourceName}; skipping that layer.\n{ex.Message}");
            }
            this.Textures[cacheKey] = tex;
            return tex;
        }

        /*********
        ** Scanning: HorseTack's own assets folder
        *********/
        /// <summary>Scan <c>{root}/assets/{layer folder}/*.png</c> (folder and file names matched case-insensitively). "Name@elle.png" files are attached to "Name.png" as its fit for Elle-shaped bodies.</summary>
        private void ScanAssets(string root, IModContentHelper content, string source, ScanStats stats)
        {
            string? assets = FindChildDirectory(root, "assets");
            if (assets == null)
                return;

            CollectionCatalog catalog = CollectionCatalog.Load(assets);
            var variants = new List<(TackLayer Layer, string Stem, string Relative)>();
            var seasonal = new List<(TackLayer Layer, string Stem, string Key, string Relative)>();

            foreach (string dir in SafeDirectories(assets))
            {
                if (!TryGetFolderLayer(Path.GetFileName(dir), out TackLayer folderLayer))
                    continue;

                foreach (string file in SafePngFiles(dir))
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (!this.ValidateSheet(file, relative, source, stats, out byte[] bytes))
                        continue;

                    bool isElleFit = stem.EndsWith(ElleVariantSuffix, StringComparison.OrdinalIgnoreCase);
                    Match seasonMatch = SeasonSuffix.Match(isElleFit ? stem[..^ElleVariantSuffix.Length] : stem);
                    if (seasonMatch.Success)
                    {
                        string baseStem = seasonMatch.Groups[1].Value;
                        string seasonKey = seasonMatch.Groups[2].Value.ToLowerInvariant() + (isElleFit ? ElleVariantSuffix : "");
                        seasonal.Add((RouteLayer(folderLayer, baseStem), baseStem, seasonKey, relative));
                        continue;
                    }

                    if (isElleFit)
                    {
                        string baseStem = stem[..^ElleVariantSuffix.Length];
                        variants.Add((RouteLayer(folderLayer, baseStem), baseStem, relative));
                        continue;
                    }

                    TackLayer layer = RouteLayer(folderLayer, stem);
                    string key = CanonicalKey(layer, stem);
                    if (key == "")
                    {
                        stats.Invalid++;
                        continue;
                    }
                    string id = $"{TackLayers.FolderName(layer)}/{key}";
                    (string collection, string name) = catalog.Describe(layer, stem);
                    this.TryAdd(new TackOption
                    {
                        Id = id,
                        Layer = layer,
                        DisplayName = collection == CollectionCatalog.DefaultCollection ? name : $"{collection}: {name}",
                        ShortName = name,
                        Collection = collection,
                        Source = source,
                        SourceName = "HorseTack's assets folder",
                        Shape = BodyShape.Vanilla,
                        Content = content,
                        RelativePath = relative,
                        Hash = Convert.ToHexString(SHA1.HashData(bytes))
                    }, stats);
                }
            }

            foreach (var variant in variants)
            {
                string id = $"{TackLayers.FolderName(variant.Layer)}/{CanonicalKey(variant.Layer, variant.Stem)}";
                if (this.ById.TryGetValue(id, out TackOption? option) && option.Source == source && variant.Layer != TackLayer.Coat)
                    option.ElleVariantRelativePath ??= variant.Relative;
                else
                    Log.Trace($"Ignored '{variant.Relative}': no matching '{variant.Stem}.png' overlay next to it.");
            }

            foreach (var season in seasonal)
            {
                string id = $"{TackLayers.FolderName(season.Layer)}/{CanonicalKey(season.Layer, season.Stem)}";
                if (this.ById.TryGetValue(id, out TackOption? option) && option.Source == source && !(season.Layer == TackLayer.Coat && season.Key.EndsWith(ElleVariantSuffix, StringComparison.OrdinalIgnoreCase)))
                    option.SeasonalPaths.TryAdd(season.Key, season.Relative);
                else
                    Log.Trace($"Ignored '{season.Relative}': no matching '{season.Stem}.png' next to it (per-season files need the plain file as the fallback).");
            }
        }

        /// <summary>Validate a candidate file: a PNG laid out like the vanilla horse sheet.</summary>
        private bool ValidateSheet(string file, string relative, string source, ScanStats stats, out byte[] bytes)
        {
            if (!TryReadPng(file, out int width, out int height, out bytes))
            {
                stats.Invalid++;
                Log.WarnOnce("invalid:" + file, $"Skipped {source} file '{relative}': not a readable PNG.");
                return false;
            }
            if (width != SheetWidth || height != SheetHeight)
            {
                stats.Invalid++;
                Log.WarnOnce("size:" + file, $"Skipped {source} file '{relative}': it's {width}x{height}, but horse layers must be {SheetWidth}x{SheetHeight} (the vanilla horse sheet layout).");
                return false;
            }
            return true;
        }

        /// <summary>Add an option unless it duplicates an existing id or identical image in the same layer.</summary>
        private void TryAdd(TackOption option, ScanStats stats)
        {
            TackOption? existing = null;
            if (this.ById.TryGetValue(option.Id, out TackOption? byId))
                existing = byId;
            else if (this.ByHash.TryGetValue($"{option.Layer}:{option.Hash}", out TackOption? byHash))
                existing = byHash;
            if (existing != null)
            {
                if (existing.Layer == option.Layer)
                    this.AddAlias(option.Id, existing);
                stats.Duplicates++;
                Log.Trace($"Skipped duplicate {option.SourceName} file '{option.RelativePath}' (same as {existing.SourceName} '{existing.RelativePath}').");
                return;
            }

            this.ById[option.Id] = option;
            this.ByHash[$"{option.Layer}:{option.Hash}"] = option;
            this.Options[option.Layer].Add(option);
            stats.Loaded++;
        }

        private void AddAlias(string alias, TackOption option)
        {
            if (!this.ById.ContainsKey(alias))
                this.ById[alias] = option;
        }

        /*********
        ** Scanning: Elle's Cuter Horses (optional, read-only)
        *********/
        private void LoadElle(ScanStats stats)
        {
            this.ElleDirectory = this.FindElleDirectory();
            if (this.ElleDirectory == null)
                return;

            try
            {
                if (this.ElleBridge == null || this.ElleBridgeDir != this.ElleDirectory)
                {
                    this.ElleBridge = this.Helper.ContentPacks.CreateTemporary(
                        directoryPath: this.ElleDirectory,
                        id: ElleBridgeId,
                        name: "Elle's Cuter Horses (read by HorseTack)",
                        description: "Read-only view of Elle's Cuter Horses art.",
                        author: "Elle/Junimods",
                        version: new SemanticVersion(1, 0, 0)
                    );
                    this.ElleBridgeDir = this.ElleDirectory;
                }
            }
            catch (Exception ex)
            {
                Log.Trace($"Couldn't open Elle's Cuter Horses folder, so only HorseTack's own art is offered: {ex.Message}");
                return;
            }

            IModContentHelper content = this.ElleBridge.ModContent;
            string? assets = FindChildDirectory(this.ElleDirectory, "assets");
            if (assets == null)
                return;

            string? horseDir = FindChildDirectory(assets, "Horse");
            IEnumerable<string> horseFiles = (horseDir != null ? SafePngFiles(horseDir) : Array.Empty<string>())
                .OrderBy(p => ElleCoatRank(Path.GetFileNameWithoutExtension(p))); // stable: alphabetical within each family
            foreach (string file in horseFiles)
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                string relative = Path.GetRelativePath(this.ElleDirectory, file).Replace('\\', '/');
                if (!this.ValidateSheet(file, relative, "Elle's Cuter Horses", stats, out byte[] bytes))
                    continue;
                bool isOverlay = stem.Contains("overlay", StringComparison.OrdinalIgnoreCase);
                string name = isOverlay ? StyleName(stem) : Pretty(stem);
                this.TryAdd(new TackOption
                {
                    Id = $"{ElleModId}/{stem}",
                    Layer = isOverlay ? TackLayer.Style : TackLayer.Coat,
                    DisplayName = name,
                    ShortName = name,
                    Collection = isOverlay ? I18n.Get("collection.elle-tack") : ElleCoatFamily(stem),
                    Source = ElleSource,
                    SourceName = "Elle's Cuter Horses",
                    Shape = BodyShape.Elle,
                    Content = content,
                    RelativePath = relative,
                    Hash = Convert.ToHexString(SHA1.HashData(bytes))
                }, stats);
            }

            string? tackDir = FindChildDirectory(assets, "Saddles");
            foreach (string file in tackDir != null ? SafePngFiles(tackDir) : Array.Empty<string>())
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                TackLayer? layer = TackPrefix(stem, out _);
                if (layer == null)
                    continue;
                string relative = Path.GetRelativePath(this.ElleDirectory, file).Replace('\\', '/');
                if (!this.ValidateSheet(file, relative, "Elle's Cuter Horses", stats, out byte[] bytes))
                    continue;
                string name = Pretty(StripTackPrefix(stem));
                this.TryAdd(new TackOption
                {
                    Id = $"{ElleModId}/{stem}",
                    Layer = layer.Value,
                    DisplayName = name,
                    ShortName = name,
                    Collection = I18n.Get("collection.elle-tack"),
                    Source = ElleSource,
                    SourceName = "Elle's Cuter Horses",
                    Shape = BodyShape.Elle,
                    Content = content,
                    RelativePath = relative,
                    Hash = Convert.ToHexString(SHA1.HashData(bytes))
                }, stats);
            }
        }

        /// <summary>List order for Elle's coats: Solid, Appaloosa, Pinto, Speckled, Roan, Shire, Void, bright colours, breeds, then overlays.</summary>
        private static int ElleCoatRank(string stem)
        {
            if (stem.Contains("overlay", StringComparison.OrdinalIgnoreCase))
                return 99;
            string[] order = { "Solid", "Appaloosa", "Pinto", "Speckled", "Roan" };
            for (int i = 0; i < order.Length; i++)
            {
                if (stem.StartsWith(order[i], StringComparison.OrdinalIgnoreCase) && stem.Length > order[i].Length)
                    return i;
            }
            if (stem.StartsWith("Void", StringComparison.OrdinalIgnoreCase) && stem.Length > 4)
                return 6;
            if (stem.EndsWith("Shire", StringComparison.OrdinalIgnoreCase))
                return 5;
            return EllePlainColours.Contains(stem, StringComparer.OrdinalIgnoreCase) ? 7 : 8;
        }

        /// <summary>Group Elle's coats into her families (Appaloosa, Pinto, ...), bright colours and breeds.</summary>
        public static string ElleCoatFamily(string stem)
        {
            foreach (string family in ElleFamilies)
            {
                if (stem.StartsWith(family, StringComparison.OrdinalIgnoreCase) && stem.Length > family.Length)
                    return I18n.Get("collection.elle-family", new { family });
            }
            if (stem.EndsWith("Shire", StringComparison.OrdinalIgnoreCase))
                return I18n.Get("collection.elle-family", new { family = "Shire" });
            if (EllePlainColours.Contains(stem, StringComparer.OrdinalIgnoreCase))
                return I18n.Get("collection.elle-colours");
            return I18n.Get("collection.elle-breeds");
        }

        /// <summary>Find Elle's folder through the SMAPI mod registry, falling back to scanning the Mods folder.</summary>
        private string? FindElleDirectory()
        {
            IModInfo? info = this.Helper.ModRegistry.Get(ElleModId);
            if (info == null)
                return null; // not installed/loaded: stay silent
            try
            {
                // IModInfo doesn't expose the folder publicly; SMAPI's implementation (IModMetadata) has DirectoryPath.
                string? dir = info.GetType().GetProperty("DirectoryPath")?.GetValue(info) as string;
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    return dir;
            }
            catch (Exception ex)
            {
                Log.Trace($"Mod registry lookup for Elle's folder failed: {ex.Message}");
            }

            // fallback: scan the Mods folder for Elle's manifest (handles registry changes in future SMAPI versions)
            try
            {
                string? modsRoot = Directory.GetParent(this.Helper.DirectoryPath)?.FullName;
                if (modsRoot == null)
                    return null;
                foreach (string manifest in Directory.EnumerateFiles(modsRoot, "manifest.json", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3, IgnoreInaccessible = true }))
                {
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                        if (doc.RootElement.TryGetProperty("UniqueID", out JsonElement id) && string.Equals(id.GetString(), ElleModId, StringComparison.OrdinalIgnoreCase))
                            return Path.GetDirectoryName(manifest);
                    }
                    catch { /* ignore unreadable manifests */ }
                }
            }
            catch (Exception ex)
            {
                Log.Trace($"Mods folder scan failed: {ex.Message}");
            }
            return null;
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
