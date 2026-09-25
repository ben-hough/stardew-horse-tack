using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MrGlim.HorseTack.Framework;
using MrGlim.HorseTack.Integrations;
using MrGlim.HorseTack.Menus;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.GameData.Buildings;

namespace MrGlim.HorseTack
{
    /// <summary>Horse Tack &amp; Styling: a stable wizard to dress each horse with its own coat, saddle, pad, bridle and styling, synced in multiplayer.</summary>
    /// <remarks>Idea credit: DelphinWave's Multiplayer Horse Reskin (MIT). No code from it is used.</remarks>
    internal sealed class ModEntry : Mod
    {
        /// <summary>Tile action added to the stable's front posts.</summary>
        public const string TileActionName = "MrGlim.HorseTack_OpenWizard";

        /// <summary>Host farmer modData key publishing the host's "anyone can restyle" setting (farmer modData syncs to every farmhand).</summary>
        public const string HostAnyoneCanEditKey = "MrGlim.HorseTack/HostAnyoneCanEdit";

        /// <summary>Multiplayer Horse Reskin also replaces horse textures, so the two fight over the same sprite.</summary>
        private const string MultiplayerHorseReskinId = "DelphinWave.MultiplayerHorseReskin";

        private static ModEntry? Instance;

        private ModConfig Config = new();
        private AssetRegistry Registry = null!;
        private string? LastSeason;
        private TextureManager Textures = null!;
        private TackService Service = null!;

        /*********
        ** Entry
        *********/
        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Log.Init(this.Monitor);
            I18n.Init(helper.Translation);

            this.Config = helper.ReadConfig<ModConfig>();
            Log.Verbose = IsVerbose(this.Config);

            this.Registry = new AssetRegistry(helper);
            this.Textures = new TextureManager(this.Registry);
            this.Service = new TackService(helper, this.ModManifest, this.Registry, () => this.Config);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.Textures.InvalidateAll();
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.Content.AssetsInvalidated += this.OnAssetsInvalidated;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
            helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;

            GameLocation.RegisterTileAction(TileActionName, this.OnStableTileAction);

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(Horse), nameof(Horse.draw), new[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Before_Horse_Draw))
            );

            this.AddCommands(helper);
        }

        /*********
        ** Harmony
        *********/
        /// <summary>Right before a horse draws (standing, walking or ridden), make sure its sprite shows its composite.</summary>
        private static void Before_Horse_Draw(Horse __instance)
        {
            try
            {
                Instance?.Textures.ApplyTo(__instance);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("draw-prefix", $"Couldn't apply horse tack while drawing (will keep trying silently): {ex}");
            }
        }

        /*********
        ** Events
        *********/
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.Registry.Reload();
            this.RegisterConfigMenu();

            if (this.Helper.ModRegistry.IsLoaded(MultiplayerHorseReskinId))
            {
                Log.Warn("Multiplayer Horse Reskin is installed. It also replaces horse textures, so the two mods will fight over how horses look "
                    + "(and it shows invisible/missing horses without a skin content pack). Please remove MultiplayerHorseReskin and use HorseTack's stable wizard instead.");
            }
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.Textures.InvalidateAll();
            this.PublishHostRules();
        }

        /// <remarks>Low priority so it runs after Content Patcher's day-start context update (seasonal horse packs).</remarks>
        [EventPriority(EventPriority.Low)]
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.PublishHostRules();
            this.Textures.RequestReapply(); // day-start sprite reloads (and other mods' texture edits) are covered next tick

            // per-season art: composites are keyed by season already; drop the old season's textures when it changes
            string season = this.Registry.CurrentSeason();
            if (this.LastSeason != null && this.LastSeason != season)
            {
                Log.Trace($"Season changed to {season}; rebuilding horse textures with seasonal art.");
                this.Textures.InvalidateAll();
            }
            this.LastSeason = season;
        }

        /// <summary>On the host, publish the restyle permission on the host's own farmer so farmhands' wizards list the same horses the host will accept.</summary>
        private void PublishHostRules()
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer)
                return;
            Game1.player.modData[HostAnyoneCanEditKey] = this.Config.AnyoneCanEdit ? "true" : "false";
        }

        /// <summary>The permission that counts: the host's setting (read from the synced host farmer on farmhands).</summary>
        private bool EffectiveAnyoneCanEdit()
        {
            if (!Context.IsWorldReady || Context.IsMainPlayer)
                return this.Config.AnyoneCanEdit;
            return Game1.MasterPlayer?.modData.TryGetValue(HostAnyoneCanEditKey, out string? value) == true
                && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            this.Textures.ProcessPendingReapply();
            if (e.IsMultipleOf(120))
                this.Textures.DisposeRetired();
        }

        /// <summary>Add the wizard action to the stable's two front posts (solid tiles the horse never stands on).</summary>
        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (!this.Config.StableActionTiles || !e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
                return;

            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, BuildingData>().Data;
                if (!data.TryGetValue("Stable", out BuildingData? stable))
                    return;

                stable.ActionTiles ??= new List<BuildingActionTile>();
                int bottom = stable.Size.Y - 1;
                foreach (Point tile in new[] { new Point(0, bottom), new Point(stable.Size.X - 1, bottom) })
                {
                    if (stable.IsTilePassable(tile.X, tile.Y))
                        continue; // action tiles only work on solid tiles
                    if (stable.ActionTiles.Any(p => p.Tile == tile))
                        continue; // don't replace another mod's action
                    stable.ActionTiles.Add(new BuildingActionTile
                    {
                        Id = $"{this.ModManifest.UniqueID}_Wizard_{tile.X}_{tile.Y}",
                        Tile = tile,
                        Action = TileActionName
                    });
                }
            }, AssetEditPriority.Late);
        }

        /// <summary>A horse base texture changed (e.g. a Content Patcher pack with a seasonal horse, or <c>patch reload</c>).</summary>
        /// <remarks>SMAPI raises this right before it propagates the change (textures are edited in place, so horses on Keep current follow
        /// the new art by themselves). HorseTack's composites are its own textures and aren't touched by propagation; this drops the ones
        /// built from the old base and re-applies every horse on the next tick, after propagation. Chosen HorseTack coats don't read the base
        /// at all, so they come back unchanged.</remarks>
        private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
        {
            if (e.NamesWithoutLocale.Any(name => this.Textures.UsesBase(name.Name)))
            {
                Log.Trace("A horse base texture was invalidated; rebuilding horse composites after propagation.");
                this.Textures.InvalidateAll();
            }
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsPlayerFree || !this.Config.OpenWizardKey.IsBound)
                return;
            if (this.Config.OpenWizardKey.JustPressed())
            {
                this.Helper.Input.SuppressActiveKeybinds(this.Config.OpenWizardKey);
                this.OpenWizard(preferred: null);
            }
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != this.ModManifest.UniqueID)
                return;

            if (e.Type == TackService.RequestType && Context.IsMainPlayer)
                this.Service.OnRequest(e.FromPlayerID, e.ReadAs<SetTackRequest>());
            else if (e.Type == TackService.ResultType && !Context.IsMainPlayer)
                TackService.ShowResult(e.ReadAs<SetTackResult>());
        }

        /// <summary>Handle the stable post action.</summary>
        private bool OnStableTileAction(GameLocation location, string[] args, Farmer who, Point tile)
        {
            if (!who.IsLocalPlayer)
                return false;

            Building? building = location.getBuildingAt(new Vector2(tile.X, tile.Y));
            if (building is not Stable stable || HorseUtil.IsTractorGarage(stable))
                return false; // never hijack Tractor Mod garages or anything else

            Horse? horse = stable.getStableHorse();
            if (horse != null && HorseUtil.IsTractor(horse))
                return false;

            this.OpenWizard(preferred: horse);
            return true;
        }

        /*********
        ** Wizard
        *********/
        private void OpenWizard(Horse? preferred)
        {
            if (Game1.activeClickableMenu != null)
                return;

            Farmer me = Game1.player;
            List<Horse> horses = HorseUtil.GetAllHorses()
                .Where(h => HorseUtil.CanEdit(me, h, this.EffectiveAnyoneCanEdit()))
                .OrderByDescending(h => h.ownerId.Value == me.UniqueMultiplayerID)
                .ThenBy(h => HorseUtil.Name(h), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (preferred != null && !horses.Contains(preferred))
            {
                Game1.showRedMessage(I18n.Get("message.not-allowed", new { horse = HorseUtil.Name(preferred) }));
                return;
            }
            if (horses.Count == 0)
            {
                Game1.showRedMessage(I18n.Get("message.no-horses"));
                return;
            }

            Horse initial = preferred
                ?? (me.mount != null && horses.Contains(me.mount) ? me.mount : null)
                ?? horses.FirstOrDefault(h => h.ownerId.Value == me.UniqueMultiplayerID)
                ?? horses[0];

            Game1.activeClickableMenu = new TackWizardMenu(this.Registry, this.Textures, this.Service, horses, initial);
        }

        /*********
        ** Console commands
        *********/
        private void AddCommands(IModHelper helper)
        {
            helper.ConsoleCommands.Add("horsetack_open", "Open the stable wizard. Usage: horsetack_open [horse name or id]", (_, args) =>
            {
                if (!this.RequireWorld())
                    return;
                Horse? horse = null;
                if (args.Length > 0)
                {
                    horse = HorseUtil.Find(string.Join(" ", args));
                    if (horse == null)
                    {
                        Log.Error($"No horse named '{string.Join(" ", args)}'. Try horsetack_list.");
                        return;
                    }
                }
                this.OpenWizard(horse);
            });

            helper.ConsoleCommands.Add("horsetack_list", "List horses with their owner and current tack.", (_, _) =>
            {
                if (!this.RequireWorld())
                    return;
                foreach (Horse horse in HorseUtil.GetAllHorses())
                {
                    TackSelection sel = TackSelection.FromModData(horse);
                    bool canEdit = HorseUtil.CanEdit(Game1.player, horse, this.EffectiveAnyoneCanEdit());
                    Log.Info($"{HorseUtil.Name(horse)} [{horse.HorseId}] owner={HorseUtil.OwnerName(horse)} editable={(canEdit ? "yes" : "no")}\n"
                        + $"    coat={Describe(TackLayer.Coat, sel.Coat)} style={Describe(TackLayer.Style, sel.Style)} saddle={Describe(TackLayer.Saddle, sel.Saddle)} pad={Describe(TackLayer.Pad, sel.Pad)} bridle={Describe(TackLayer.Bridle, sel.Bridle)}");
                }
            });

            helper.ConsoleCommands.Add("horsetack_options", "List selectable art ids. Usage: horsetack_options [coat|saddle|pad|bridle|style]", (_, args) =>
            {
                IEnumerable<TackLayer> layers = TackLayers.DrawOrder;
                if (args.Length > 0)
                {
                    if (!TackLayers.TryParse(args[0], out TackLayer only))
                    {
                        Log.Error("Layer must be coat, saddle, pad, bridle or style.");
                        return;
                    }
                    layers = new[] { only };
                }
                foreach (TackLayer layer in layers)
                {
                    var options = this.Registry.Get(layer);
                    Log.Info($"{layer} ({options.Count}): none" + (options.Count > 0 ? ", " + string.Join(", ", options.Select(p => $"{p.Id} [{p.Source}]")) : ""));
                }
            });

            helper.ConsoleCommands.Add("horsetack_set", "Change one layer. Usage: horsetack_set <horse name or id> <coat|saddle|pad|bridle|style> <art id|none>", (_, args) =>
            {
                if (!this.RequireWorld())
                    return;
                if (args.Length < 3 || !TackLayers.TryParse(args[^2], out TackLayer layer))
                {
                    Log.Error("Usage: horsetack_set <horse name or id> <coat|saddle|pad|bridle|style> <art id|none>");
                    return;
                }
                string name = string.Join(" ", args[..^2]);
                Horse? horse = HorseUtil.Find(name);
                if (horse == null)
                {
                    Log.Error($"No horse named '{name}'. Try horsetack_list.");
                    return;
                }
                string value = args[^1];
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase) || value.Equals("keep", StringComparison.OrdinalIgnoreCase))
                    value = "";
                else if (this.Registry.TryGet(value, out TackOption option))
                    value = option.Id;
                else
                {
                    Log.Error($"Unknown art id '{value}'. Try horsetack_options {layer.ToString().ToLowerInvariant()}.");
                    return;
                }

                TackSelection sel = TackSelection.FromModData(horse).Clone();
                sel.Set(layer, value);
                this.Service.RequestChange(horse, sel.Normalize());
            });

            helper.ConsoleCommands.Add("horsetack_reset", "Remove all tack from a horse. Usage: horsetack_reset <horse name or id>", (_, args) =>
            {
                if (!this.RequireWorld())
                    return;
                Horse? horse = args.Length > 0 ? HorseUtil.Find(string.Join(" ", args)) : null;
                if (horse == null)
                {
                    Log.Error("Usage: horsetack_reset <horse name or id> (see horsetack_list)");
                    return;
                }
                this.Service.RequestChange(horse, new TackSelection());
            });

            helper.ConsoleCommands.Add("horsetack_textures", "Debug: show which texture each horse is drawing (HorseTack composite or the game's horse texture) and where its coat pixels come from.", (_, _) =>
            {
                if (!this.RequireWorld())
                    return;
                foreach (Horse horse in HorseUtil.GetAllHorses())
                    Log.Info(this.DescribeTexture(horse));
            });

            helper.ConsoleCommands.Add("horsetack_reload", "Rescan art folders and rebuild horse textures.", (_, _) =>
            {
                Log.ResetOnce();
                this.Registry.Reload();
                this.Textures.InvalidateAll();
            });
        }

        /// <summary>One line per horse for <c>horsetack_textures</c>.</summary>
        private string DescribeTexture(Horse horse)
        {
            AnimatedSprite? sprite = horse.Sprite;
            string where = horse.currentLocation?.NameOrUniqueName ?? "?";
            TackSelection sel = TackSelection.FromModData(horse);
            if (sprite?.spriteTexture == null)
                return $"{HorseUtil.Name(horse)} @ {where}: no texture loaded yet (sel={sel.Key})";

            Texture2D tex = sprite.spriteTexture;
            bool ours = this.Textures.IsComposite(sprite, out int gen);
            string line = $"{HorseUtil.Name(horse)} @ {where}: sel=[{sel.Key}] textureName={sprite.overrideTextureName ?? sprite.textureName.Value} "
                + $"active='{tex.Name}' {(ours ? $"HorseTack composite (gen {gen}/{this.Textures.CurrentGeneration})" : "game texture")} disposed={tex.IsDisposed}";
            if (tex.IsDisposed)
                return line;

            // compare the pixels the horse is drawing with the chosen coat and with the live game texture
            try
            {
                var active = new Color[tex.Width * tex.Height];
                tex.GetData(active);
                string Match(Color[]? other) => other == null || other.Length != active.Length ? "n/a" : $"{active.Where((c, i) => c == other[i]).Count() * 100.0 / active.Length:0}%";

                Color[]? coat = sel.Coat != "" ? this.Registry.GetPixels(sel.Coat)?.Data : null;
                Texture2D live = Game1.content.Load<Texture2D>(sprite.overrideTextureName ?? sprite.textureName.Value);
                var livePx = new Color[live.Width * live.Height];
                live.GetData(livePx);
                line += $" | pixels same as chosen coat: {Match(coat)}, same as live game texture: {Match(livePx)}";
            }
            catch (Exception ex)
            {
                line += $" | pixel compare failed: {ex.Message}";
            }
            return line;
        }

        private string Describe(TackLayer layer, string id) => id == "" ? (layer == TackLayer.Coat ? "keep" : "none") : id;

        private bool RequireWorld()
        {
            if (Context.IsWorldReady)
                return true;
            Log.Error("Load a save first.");
            return false;
        }

        /*********
        ** Config
        *********/
        private static bool IsVerbose(ModConfig config) => string.Equals(config.LogVerbosity, "Verbose", StringComparison.OrdinalIgnoreCase);

        private void RegisterConfigMenu()
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () =>
                {
                    this.Helper.WriteConfig(this.Config);
                    Log.Verbose = IsVerbose(this.Config);
                    this.Helper.GameContent.InvalidateCache("Data/Buildings");
                    this.PublishHostRules();
                }
            );
            gmcm.AddKeybindList(this.ModManifest, () => this.Config.OpenWizardKey, v => this.Config.OpenWizardKey = v,
                () => I18n.Get("config.key.name"), () => I18n.Get("config.key.desc"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.AnyoneCanEdit, v => this.Config.AnyoneCanEdit = v,
                () => I18n.Get("config.anyone.name"), () => I18n.Get("config.anyone.desc"));
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.StableActionTiles, v => this.Config.StableActionTiles = v,
                () => I18n.Get("config.tiles.name"), () => I18n.Get("config.tiles.desc"));
            gmcm.AddTextOption(this.ModManifest, () => this.Config.LogVerbosity, v => this.Config.LogVerbosity = v,
                () => I18n.Get("config.log.name"), () => I18n.Get("config.log.desc"),
                allowedValues: new[] { "Normal", "Verbose" },
                formatAllowedValue: v => I18n.Get("config.log." + v.ToLowerInvariant()));
        }
    }
}
