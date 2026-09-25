using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Builds (and caches) one composite texture per base+selection, and applies it to horse sprites right before they draw.</summary>
    /// <remarks>
    /// A composite is a Texture2D owned by HorseTack, never a content-manager asset, so SMAPI's asset propagation (which edits cached
    /// textures like <c>Animals/horse</c> in place) can't overwrite it. A chosen HorseTack coat is composed only from HorseTack's own
    /// pixels and never reads the game's horse texture, so seasonal horse packs can't change it. Keep current horses are composed from the
    /// live game texture and are rebuilt (one tick later, after propagation) whenever that texture is invalidated.
    /// </remarks>
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
        private bool ReapplyPending;

        /// <summary>The composite generation (bumped whenever cached composites are dropped).</summary>
        public int CurrentGeneration => this.Generation;

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
            if (this.AppliedSprites.TryGetValue(sprite, out Applied? state) && state.Key == key && state.Generation == this.Generation && state.Texture is { IsDisposed: false })
            {
                if (!ReferenceEquals(sprite.spriteTexture, state.Texture))
                    this.Assign(sprite, state.Texture, baseName);
                return;
            }

            if (!this.Composites.TryGetValue(key, out Texture2D? tex) || tex is { IsDisposed: true })
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
                sprite.loadedTexture = null; // AnimatedSprite.Texture reloads from textureName on next access...
                _ = sprite.Texture;          // ...which we trigger now, so the sprite never keeps pointing at one of our composites
            }
        }

        /// <summary>Whether this sprite currently shows a HorseTack composite.</summary>
        public bool IsComposite(AnimatedSprite sprite, out int generation)
        {
            generation = -1;
            if (this.AppliedSprites.TryGetValue(sprite, out Applied? state) && ReferenceEquals(sprite.spriteTexture, state.Texture))
            {
                generation = state.Generation;
                return true;
            }
            return false;
        }

        /*********
        ** Re-apply to every horse
        *********/
        /// <summary>Re-apply on the next update tick (so it runs after SMAPI's asset propagation and other mods' handlers).</summary>
        public void RequestReapply() => this.ReapplyPending = true;

        /// <summary>Call every update tick: if requested, re-apply composites to every horse in the world, including horses nobody is looking at.</summary>
        /// <remarks>Horses are normally updated right before they draw, but a horse in another location isn't drawn, and menus such as the
        /// Animals tab read <c>horse.Sprite.Texture</c> directly. This keeps every sprite on a current composite (or the live game texture).</remarks>
        public void ProcessPendingReapply()
        {
            if (!this.ReapplyPending || !Context.IsWorldReady)
                return;
            this.ReapplyPending = false;
            this.ReapplyAll();
        }

        public void ReapplyAll()
        {
            foreach (Horse horse in HorseUtil.GetAllHorses())
            {
                try
                {
                    this.ApplyTo(horse);
                }
                catch (Exception ex)
                {
                    Log.WarnOnce("reapply", $"Couldn't re-apply horse tack to '{horse.displayName}': {ex.Message}");
                }
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
        /// <summary>Compose base (chosen coat or current texture) + style + pad + saddle + bridle. Returns null if nothing applies.</summary>
        private Texture2D? Compose(string baseName, TackSelection sel, string forWhat)
        {
            PixelData? px = ComposePixels(
                sel,
                getPixels: (id, shape) => this.Registry.GetPixels(id, shape),
                coatShape: id => this.Registry.TryGet(id, out TackOption coat) && coat.Layer == TackLayer.Coat ? coat.Shape : null,
                keepCurrentShape: this.Registry.KeepCurrentShape,
                overlayOrder: this.Registry.OverlayOrder(sel),
                loadBase: () => this.GetBasePixels(baseName)
            );
            if (px == null)
                return null;

            var tex = new Texture2D(Game1.graphics.GraphicsDevice, px.Width, px.Height);
            tex.SetData(px.Data);
            tex.Name = "MrGlim.HorseTack/" + sel.Key + this.Registry.SeasonKey(sel);
            Log.Trace($"Composed {sel.Key} on {(sel.Coat != "" && this.Registry.TryGet(sel.Coat, out _) ? "HorseTack coat" : baseName)} for {forWhat}.");
            return tex;
        }

        /// <summary>Pure pixel composition (no graphics device). A chosen coat this computer has is the whole base: the game's horse texture
        /// (<paramref name="loadBase"/>) is only read for Keep current, or for a synced coat this computer doesn't have.</summary>
        /// <returns>The composed sheet, or null if nothing changes the base (Keep current with no overlays, or nothing installed).</returns>
        internal static PixelData? ComposePixels(TackSelection sel, Func<string, BodyShape, PixelData?> getPixels, Func<string, BodyShape?> coatShape,
            BodyShape keepCurrentShape, IReadOnlyList<TackLayer> overlayOrder, Func<PixelData?> loadBase)
        {
            PixelData? basePx = null;
            bool changed = false;

            // Overlays pick the variant that fits the body underneath: a chosen coat knows its own shape; the game's own
            // texture (Keep current, or a synced coat this computer doesn't have) is Elle-shaped when her pack is loaded here.
            BodyShape shape = keepCurrentShape;
            if (sel.Coat != "" && coatShape(sel.Coat) is BodyShape coatBody)
            {
                basePx = getPixels(sel.Coat, BodyShape.Vanilla);
                if (basePx != null)
                {
                    shape = coatBody;
                    changed = true;
                }
            }
            basePx ??= loadBase(); // Keep current, or the chosen coat is missing here: the live game texture
            if (basePx == null)
                return null;

            Color[] result = (Color[])basePx.Data.Clone();
            foreach (TackLayer layer in overlayOrder)
            {
                string id = sel.Get(layer);
                if (id == "")
                    continue;
                PixelData? overlay = getPixels(id, shape);
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

            return changed ? new PixelData(basePx.Width, basePx.Height, result) : null;
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
            this.ReapplyPending = true; // move every horse onto a fresh composite next tick (after asset propagation)
        }

        /// <summary>Whether an invalidated asset is a horse base texture that composites were built from.</summary>
        public bool UsesBase(string assetName)
        {
            string norm = assetName.Replace('/', '\\');
            return norm.StartsWith(VanillaHorse, StringComparison.OrdinalIgnoreCase)
                || this.BasePixels.Keys.Any(k => string.Equals(k.Replace('/', '\\'), norm, StringComparison.OrdinalIgnoreCase));
        }

        private void Retire(Texture2D tex)
        {
            if (!this.Retired.Any(r => ReferenceEquals(r.Texture, tex)))
                this.Retired.Add((tex, Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0));
        }

        /// <summary>Dispose retired textures after a grace period (call from an update tick). A texture still shown by any horse sprite
        /// (e.g. a horse in another location that hasn't drawn since) is moved to a current composite first, never disposed under it.</summary>
        public void DisposeRetired()
        {
            double now = Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0;
            List<int> due = new();
            for (int i = this.Retired.Count - 1; i >= 0; i--)
            {
                if (now - this.Retired[i].RetiredAt > 10)
                    due.Add(i);
            }
            if (due.Count == 0)
                return;

            HashSet<Texture2D> inUse = new(ReferenceEqualityComparer.Instance);
            if (Context.IsWorldReady)
            {
                var dueTextures = new HashSet<Texture2D>(due.Select(i => this.Retired[i].Texture), ReferenceEqualityComparer.Instance);
                foreach (Horse horse in HorseUtil.GetAllHorses())
                {
                    AnimatedSprite? sprite = horse.Sprite;
                    if (sprite?.spriteTexture == null || !dueTextures.Contains(sprite.spriteTexture))
                        continue;
                    try
                    {
                        this.ApplyTo(horse);
                    }
                    catch (Exception ex)
                    {
                        Log.WarnOnce("retire-reapply", $"Couldn't re-apply horse tack to '{horse.displayName}': {ex.Message}");
                    }
                    if (sprite.spriteTexture != null && dueTextures.Contains(sprite.spriteTexture))
                        inUse.Add(sprite.spriteTexture); // still shown: keep it alive and try again next time
                }
            }

            foreach (int i in due) // descending indexes
            {
                if (inUse.Contains(this.Retired[i].Texture))
                    continue;
                this.Retired[i].Texture.Dispose();
                this.Retired.RemoveAt(i);
            }
        }
    }
}
