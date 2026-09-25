using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Characters;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Builds (and caches) one composite texture per base+selection, and applies it to horse sprites right before they draw.</summary>
    internal sealed class TextureManager
    {
        private const string VanillaHorse = "Animals\\horse";
        private const int PreviewCacheSize = 16;

        private readonly AssetRegistry Registry;
        private readonly Dictionary<string, Texture2D?> Composites = new();
        private readonly Dictionary<string, PixelData> BasePixels = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<(string Key, Texture2D? Texture)> PreviewCache = new();
        private readonly List<(Texture2D Texture, double RetiredAt)> Retired = new();
        private readonly ConditionalWeakTable<AnimatedSprite, Applied> AppliedSprites = new();
        private int Generation;

        private sealed class Applied
        {
            public string Key = "";
            public int Generation;
            public Texture2D? Texture;
        }

        public TextureManager(AssetRegistry registry)
        {
            this.Registry = registry;
        }

        /*********
        ** Apply to horses (called from the Horse.draw prefix)
        *********/
        public void ApplyTo(Horse horse)
        {
            AnimatedSprite? sprite = horse.Sprite;
            if (sprite == null || HorseUtil.IsTractor(horse))
                return;

            TackSelection sel = TackSelection.FromModData(horse);
            if (sel.IsEmpty)
            {
                this.Restore(sprite);
                return;
            }

            string baseName = BaseTextureName(sprite);
            string key = baseName + "#" + sel.Key + this.Registry.SeasonKey(sel);

            // fast path: already applied and nothing changed
            if (this.AppliedSprites.TryGetValue(sprite, out Applied? state) && state.Key == key && state.Generation == this.Generation && state.Texture != null)
            {
                if (!ReferenceEquals(sprite.spriteTexture, state.Texture))
                    this.Assign(sprite, state.Texture, baseName);
                return;
            }

            if (!this.Composites.TryGetValue(key, out Texture2D? tex))
            {
                tex = this.Compose(baseName, sel, $"horse '{horse.displayName}'");
                this.Composites[key] = tex;
            }

            if (tex == null)
            {
                this.Restore(sprite);
                return;
            }

            this.Assign(sprite, tex, baseName);
            if (state == null)
            {
                state = new Applied();
                this.AppliedSprites.Add(sprite, state);
            }
            state.Key = key;
            state.Generation = this.Generation;
            state.Texture = tex;
        }

        /// <summary>Put our texture on the sprite and mark its own texture as loaded so the game doesn't reload over it.</summary>
        private void Assign(AnimatedSprite sprite, Texture2D tex, string baseName)
        {
            sprite.spriteTexture = tex;
            // AnimatedSprite reloads only when loadedTexture != (override ?? textureName); match it so our texture stays
            sprite.loadedTexture = sprite.overrideTextureName ?? sprite.textureName.Value;
        }

        /// <summary>Undo our override so the sprite reloads its normal texture.</summary>
        private void Restore(AnimatedSprite sprite)
        {
            if (this.AppliedSprites.TryGetValue(sprite, out _))
            {
                this.AppliedSprites.Remove(sprite);
                sprite.loadedTexture = null; // AnimatedSprite.Texture reloads from textureName on next access
            }
        }

        private static string BaseTextureName(AnimatedSprite sprite)
        {
            string? name = sprite.overrideTextureName ?? sprite.textureName.Value;
            return string.IsNullOrWhiteSpace(name) ? VanillaHorse : name;
        }

        /*********
        ** Menu preview
        *********/
        /// <summary>Get a texture showing the selection on this horse (for the wizard preview).</summary>
        public Texture2D? GetPreview(Horse horse, TackSelection sel)
        {
            string baseName = horse.Sprite != null ? BaseTextureName(horse.Sprite) : VanillaHorse;
            if (sel.IsEmpty)
                return LoadGameTexture(baseName);

            string key = $"{this.Generation}:{baseName}#{sel.Key}{this.Registry.SeasonKey(sel)}";
            for (var node = this.PreviewCache.First; node != null; node = node.Next)
            {
                if (node.Value.Key == key)
                {
                    this.PreviewCache.Remove(node);
                    this.PreviewCache.AddFirst(node);
                    return node.Value.Texture ?? LoadGameTexture(baseName);
                }
            }

            Texture2D? tex = this.Compose(baseName, sel, "preview");
            this.PreviewCache.AddFirst((key, tex));
            while (this.PreviewCache.Count > PreviewCacheSize)
            {
                var last = this.PreviewCache.Last!.Value;
                this.PreviewCache.RemoveLast();
                if (last.Texture != null)
                    this.Retire(last.Texture);
            }
            return tex ?? LoadGameTexture(baseName);
        }

        /*********
        ** Composition
        *********/
        /// <summary>Compose base (chosen coat or current texture) + style + saddle + pad + bridle. Returns null if nothing applies.</summary>
        private Texture2D? Compose(string baseName, TackSelection sel, string forWhat)
        {
            PixelData? basePx = null;
            bool changed = false;

            // Overlays pick the variant that fits the body underneath: a chosen coat knows its own shape; the game's own
            // texture (Keep current, or a synced coat this computer doesn't have) is Elle-shaped when her pack is loaded here.
            BodyShape shape = this.Registry.KeepCurrentShape;
            if (sel.Coat != "")
            {
                basePx = this.Registry.GetPixels(sel.Coat);
                if (basePx != null && this.Registry.TryGet(sel.Coat, out TackOption coat))
                {
                    shape = coat.Shape;
                    changed = true;
                }
                else
                    basePx = null; // missing here: fall back to the current (vanilla or Elle) texture
            }
            basePx ??= this.GetBasePixels(baseName);
            if (basePx == null)
                return null;

            Color[] result = (Color[])basePx.Data.Clone();
            foreach (TackLayer layer in this.Registry.OverlayOrder(sel))
            {
                string id = sel.Get(layer);
                if (id == "")
                    continue;
                PixelData? overlay = this.Registry.GetPixels(id, shape);
                if (overlay == null)
                    continue; // not installed on this computer: skip this layer (noted once in the trace log)
                if (overlay.Width != basePx.Width || overlay.Height != basePx.Height)
                {
                    Log.WarnOnce($"size:{id}:{basePx.Width}x{basePx.Height}", $"'{id}' is {overlay.Width}x{overlay.Height} but the horse sheet is {basePx.Width}x{basePx.Height}; skipping that layer.");
                    continue;
                }
                Compositor.OverPremultiplied(result, overlay.Data);
                changed = true;
            }

            if (!changed)
                return null;

            var tex = new Texture2D(Game1.graphics.GraphicsDevice, basePx.Width, basePx.Height);
            tex.SetData(result);
            tex.Name = "MrGlim.HorseTack/" + sel.Key + this.Registry.SeasonKey(sel);
            Log.Trace($"Composed {sel.Key} on {baseName} for {forWhat}.");
            return tex;
        }

        private PixelData? GetBasePixels(string baseName)
        {
            if (this.BasePixels.TryGetValue(baseName, out PixelData? px))
                return px;
            Texture2D? tex = LoadGameTexture(baseName);
            if (tex == null)
                return null;
            var data = new Color[tex.Width * tex.Height];
            tex.GetData(data);
            px = new PixelData(tex.Width, tex.Height, data);
            this.BasePixels[baseName] = px;
            return px;
        }

        private static Texture2D? LoadGameTexture(string name)
        {
            try
            {
                return Game1.content.Load<Texture2D>(name);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("base:" + name, $"Couldn't load horse texture '{name}': {ex.Message}");
                return null;
            }
        }

        /*********
        ** Cache lifetime
        *********/
        /// <summary>Drop all cached composites (they're disposed a few seconds later so nothing draws a disposed texture).</summary>
        public void InvalidateAll()
        {
            foreach (Texture2D? tex in this.Composites.Values)
            {
                if (tex != null)
                    this.Retire(tex);
            }
            this.Composites.Clear();
            foreach (var entry in this.PreviewCache)
            {
                if (entry.Texture != null)
                    this.Retire(entry.Texture);
            }
            this.PreviewCache.Clear();
            this.BasePixels.Clear();
            this.Generation++;
        }

        private void Retire(Texture2D tex)
        {
            if (!this.Retired.Any(r => ReferenceEquals(r.Texture, tex)))
                this.Retired.Add((tex, Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0));
        }

        /// <summary>Dispose retired textures after a grace period (call from an update tick).</summary>
        public void DisposeRetired()
        {
            double now = Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0;
            for (int i = this.Retired.Count - 1; i >= 0; i--)
            {
                if (now - this.Retired[i].RetiredAt > 10)
                {
                    this.Retired[i].Texture.Dispose();
                    this.Retired.RemoveAt(i);
                }
            }
        }
    }
}
