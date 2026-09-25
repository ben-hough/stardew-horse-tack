using System;
using StardewValley;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>The per-horse choices. An empty string means "keep current" (coat) or "none" (other layers).</summary>
    internal sealed class TackSelection : IEquatable<TackSelection>
    {
        public string Coat { get; set; } = "";
        public string Style { get; set; } = "";
        public string Saddle { get; set; } = "";
        public string Pad { get; set; } = "";
        public string Bridle { get; set; } = "";

        public bool IsEmpty => Coat == "" && Style == "" && Saddle == "" && Pad == "" && Bridle == "";

        public string Get(TackLayer layer) => layer switch
        {
            TackLayer.Coat => Coat,
            TackLayer.Style => Style,
            TackLayer.Saddle => Saddle,
            TackLayer.Pad => Pad,
            TackLayer.Bridle => Bridle,
            _ => ""
        };

        public void Set(TackLayer layer, string? value)
        {
            value = (value ?? "").Trim();
            switch (layer)
            {
                case TackLayer.Coat: this.Coat = value; break;
                case TackLayer.Style: this.Style = value; break;
                case TackLayer.Saddle: this.Saddle = value; break;
                case TackLayer.Pad: this.Pad = value; break;
                case TackLayer.Bridle: this.Bridle = value; break;
            }
        }

        /// <summary>Apply cross-layer rules (a pad needs a saddle, like Elle's pack).</summary>
        public TackSelection Normalize()
        {
            if (this.Saddle == "")
                this.Pad = "";
            return this;
        }

        public TackSelection Clone() => new() { Coat = this.Coat, Style = this.Style, Saddle = this.Saddle, Pad = this.Pad, Bridle = this.Bridle };

        /// <summary>Read the selection stored on a horse.</summary>
        public static TackSelection FromModData(Character horse)
        {
            var sel = new TackSelection();
            foreach (TackLayer layer in TackLayers.DrawOrder)
            {
                if (horse.modData.TryGetValue(TackLayers.ModDataKey(layer), out string? value))
                    sel.Set(layer, value);
            }
            return sel.Normalize();
        }

        /// <summary>Write the selection to a horse (host only - modData is synced by the game).</summary>
        public void WriteTo(Character horse)
        {
            this.Normalize();
            foreach (TackLayer layer in TackLayers.DrawOrder)
            {
                string key = TackLayers.ModDataKey(layer);
                string value = this.Get(layer);
                if (value == "")
                    horse.modData.Remove(key);
                else
                    horse.modData[key] = value;
            }
        }

        /// <summary>A cache key describing the composite (not including the base texture).</summary>
        public string Key => $"{this.Coat}|{this.Style}|{this.Saddle}|{this.Pad}|{this.Bridle}";

        public bool Equals(TackSelection? other) => other != null && this.Key == other.Key;
        public override bool Equals(object? obj) => this.Equals(obj as TackSelection);
        public override int GetHashCode() => this.Key.GetHashCode();
        public override string ToString() => this.Key;
    }
}
