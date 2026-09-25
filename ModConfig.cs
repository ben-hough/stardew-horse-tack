using StardewModdingAPI.Utilities;

namespace MrGlim.HorseTack
{
    /// <summary>The mod settings (config.json).</summary>
    internal sealed class ModConfig
    {
        /// <summary>Keybind that opens the stable wizard anywhere. Empty = off.</summary>
        public KeybindList OpenWizardKey { get; set; } = new();

        /// <summary>Let any player restyle any horse (default: only the horse's owner or the host).</summary>
        public bool AnyoneCanEdit { get; set; } = false;

        /// <summary>Add the wizard action spot to the stable's front posts.</summary>
        public bool StableActionTiles { get; set; } = true;

        /// <summary>"Normal" or "Verbose" logging.</summary>
        public string LogVerbosity { get; set; } = "Normal";
    }
}
