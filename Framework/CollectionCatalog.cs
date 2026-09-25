using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Maps file-name prefixes ("SpiritsEve_Pumpkin") to collection and display names, from <c>assets/collections.json</c>.</summary>
    internal sealed class CollectionCatalog
    {
        private readonly Dictionary<string, string> Collections = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Collection for art without a known prefix (e.g. files a player dropped in).</summary>
        public static string DefaultCollection => I18n.Get("collection.other");

        public static CollectionCatalog Load(string assetsDir)
        {
            var catalog = new CollectionCatalog();
            try
            {
                string? file = Directory.Exists(assetsDir)
                    ? Directory.GetFiles(assetsDir).FirstOrDefault(p => string.Equals(Path.GetFileName(p), "collections.json", StringComparison.OrdinalIgnoreCase))
                    : null;
                if (file == null)
                    return catalog;
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                ReadMap(doc.RootElement, "Collections", catalog.Collections);
                ReadMap(doc.RootElement, "Names", catalog.Names);
            }
            catch (Exception ex)
            {
                Log.Warn($"Couldn't read assets/collections.json, so art is listed without collection names: {ex.Message}");
            }
            return catalog;
        }

        public static CollectionCatalog FromMaps(IDictionary<string, string> collections, IDictionary<string, string> names)
        {
            var catalog = new CollectionCatalog();
            foreach (var pair in collections) catalog.Collections[pair.Key] = pair.Value;
            foreach (var pair in names) catalog.Names[pair.Key] = pair.Value;
            return catalog;
        }

        /// <summary>Get the collection and short name for a file, e.g. (Saddle, "SpiritsEve_Pumpkin") -> ("Spirit's Eve", "Pumpkin").</summary>
        public (string Collection, string Name) Describe(TackLayer layer, string stem)
        {
            string core = layer switch
            {
                TackLayer.Saddle or TackLayer.Pad or TackLayer.Bridle => Regex.Replace(stem, @"^(saddle|pad|bridle)[_\- ]+", "", RegexOptions.IgnoreCase),
                TackLayer.Style => Regex.Replace(stem, "overlay", "", RegexOptions.IgnoreCase).Trim('_', ' ', '-'),
                _ => stem
            };
            if (core == "")
                core = stem;

            int underscore = core.IndexOf('_');
            if (underscore > 0 && underscore < core.Length - 1 && this.Collections.TryGetValue(core[..underscore], out string? collection))
            {
                string rest = core[(underscore + 1)..];
                string name = this.Names.TryGetValue(core, out string? custom) ? custom : AssetRegistry.Pretty(rest);
                return (collection, name);
            }

            string plain = this.Names.TryGetValue(core, out string? customPlain) ? customPlain : AssetRegistry.Pretty(core);
            return (DefaultCollection, plain);
        }

        private static void ReadMap(JsonElement root, string property, Dictionary<string, string> target)
        {
            foreach (JsonProperty prop in root.EnumerateObject())
            {
                if (!string.Equals(prop.Name, property, StringComparison.OrdinalIgnoreCase) || prop.Value.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (JsonProperty entry in prop.Value.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.Value.GetString()))
                        target[entry.Name] = entry.Value.GetString()!.Trim();
                }
            }
        }
    }
}
