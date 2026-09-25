using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Finds coat and overlay art (Elle's Cuter Horses + HorseTack content packs) and loads pixels on demand.</summary>
    internal sealed class AssetRegistry
    {
        public const string ElleModId = "Elle.CuterHorses";
        private const string ElleBridgeId = "MrGlim.HorseTack.EllesCuterHorsesBridge";

        private readonly IModHelper Helper;
        private readonly Dictionary<TackLayer, List<TackOption>> Options = new();
        private readonly Dictionary<string, TackOption> ById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PixelData?> Pixels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D?> Textures = new(StringComparer.OrdinalIgnoreCase);
        private IContentPack? ElleBridge;
        private string? ElleBridgeDir;

        /// <summary>Folder of Elle's Cuter Horses, if found.</summary>
        public string? ElleDirectory { get; private set; }

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

        /// <summary>Rescan all art sources.</summary>
        public void Reload()
        {
            foreach (var list in this.Options.Values)
                list.Clear();
            this.ById.Clear();
            this.Pixels.Clear();
            this.Textures.Clear();

            this.LoadElle();
            this.LoadContentPacks();

            Log.Info($"Found {this.Get(TackLayer.Coat).Count} coats, {this.Get(TackLayer.Saddle).Count} saddles, {this.Get(TackLayer.Pad).Count} pads, {this.Get(TackLayer.Bridle).Count} bridles, {this.Get(TackLayer.Style).Count} styles."
                + (this.ElleDirectory != null ? $" Elle's Cuter Horses: {this.ElleDirectory}" : " Elle's Cuter Horses not found."));
        }

        /// <summary>Get premultiplied pixels for an option, or null (logged once) if missing/unreadable.</summary>
        public PixelData? GetPixels(string id)
        {
            if (this.Pixels.TryGetValue(id, out PixelData? cached))
                return cached;

            PixelData? result = null;
            if (!this.ById.TryGetValue(id, out TackOption? option))
                Log.WarnOnce("missing:" + id, $"Horse art '{id}' isn't installed on this computer; skipping that layer. (Everyone should use the same art mods.)");
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
                tex = option.Pack.ModContent.Load<Texture2D>(option.RelativePath);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("load:" + option.Id, $"Couldn't load '{option.RelativePath}' from {option.SourceName}; skipping that layer.\n{ex.Message}");
            }
            this.Textures[option.Id] = tex;
            return tex;
        }

        /*********
        ** Elle's Cuter Horses
        *********/
        private void LoadElle()
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
                        name: "Elle's Cuter Horses (read by Horse Tack & Styling)",
                        description: "Read-only view of Elle's Cuter Horses art.",
                        author: "Elle/Junimods",
                        version: new SemanticVersion(1, 0, 0)
                    );
                    this.ElleBridgeDir = this.ElleDirectory;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Couldn't read Elle's Cuter Horses folder: {ex.Message}");
                return;
            }

            const string source = "Elle's Cuter Horses";
            string horseDir = Path.Combine(this.ElleDirectory, "assets", "Horse");
            foreach (string file in SafeFiles(horseDir))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                bool isOverlay = stem.Contains("Overlay", StringComparison.OrdinalIgnoreCase);
                string display = isOverlay ? StyleName(stem) : Pretty(stem);
                this.Add(new TackOption
                {
                    Id = $"{ElleModId}/{stem}",
                    Layer = isOverlay ? TackLayer.Style : TackLayer.Coat,
                    DisplayName = display,
                    SourceName = source,
                    Pack = this.ElleBridge!,
                    RelativePath = $"assets/Horse/{Path.GetFileName(file)}"
                });
            }

            string tackDir = Path.Combine(this.ElleDirectory, "assets", "Saddles");
            foreach (string file in SafeFiles(tackDir))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                int underscore = stem.IndexOf('_');
                if (underscore <= 0)
                    continue;
                string prefix = stem[..underscore];
                TackLayer? layer = prefix.ToLowerInvariant() switch
                {
                    "saddle" => TackLayer.Saddle,
                    "pad" => TackLayer.Pad,
                    "bridle" => TackLayer.Bridle,
                    _ => null
                };
                if (layer == null)
                    continue;
                this.Add(new TackOption
                {
                    Id = $"{ElleModId}/{stem}",
                    Layer = layer.Value,
                    DisplayName = Pretty(stem[(underscore + 1)..]),
                    SourceName = source,
                    Pack = this.ElleBridge!,
                    RelativePath = $"assets/Saddles/{Path.GetFileName(file)}"
                });
            }
        }

        /// <summary>Find Elle's folder through the SMAPI mod registry, falling back to scanning the Mods folder.</summary>
        private string? FindElleDirectory()
        {
            IModInfo? info = this.Helper.ModRegistry.Get(ElleModId);
            if (info != null)
            {
                // IModInfo doesn't expose the folder publicly; SMAPI's implementation (IModMetadata) has DirectoryPath.
                string? dir = info.GetType().GetProperty("DirectoryPath")?.GetValue(info) as string;
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    return dir;
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

        /*********
        ** HorseTack content packs
        *********/
        private void LoadContentPacks()
        {
            foreach (IContentPack pack in this.Helper.ContentPacks.GetOwned())
            {
                int count = 0;
                foreach (TackLayer layer in TackLayers.DrawOrder)
                {
                    string folder = TackLayers.FolderName(layer);
                    foreach (string file in SafeFiles(Path.Combine(pack.DirectoryPath, "assets", folder)))
                    {
                        string stem = Path.GetFileNameWithoutExtension(file);
                        this.Add(new TackOption
                        {
                            Id = $"{pack.Manifest.UniqueID}/{folder}/{stem}",
                            Layer = layer,
                            DisplayName = $"{Pretty(stem)} [{pack.Manifest.Name}]",
                            SourceName = pack.Manifest.Name,
                            Pack = pack,
                            RelativePath = $"assets/{folder}/{Path.GetFileName(file)}"
                        });
                        count++;
                    }
                }
                Log.Trace($"Content pack {pack.Manifest.Name}: {count} images.");
            }
        }

        /*********
        ** Helpers
        *********/
        private void Add(TackOption option)
        {
            if (this.ById.ContainsKey(option.Id))
                return;
            this.ById[option.Id] = option;
            this.Options[option.Layer].Add(option);
        }

        private static IEnumerable<string> SafeFiles(string dir)
        {
            if (!Directory.Exists(dir))
                return Array.Empty<string>();
            return Directory.GetFiles(dir, "*.png").OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase);
        }

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

    /// <summary>Premultiplied pixels of an image.</summary>
    internal sealed record PixelData(int Width, int Height, Color[] Data);
}
