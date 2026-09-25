using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Host-authoritative changes: the host validates and writes modData; the game syncs modData to everyone.</summary>
    internal sealed class TackService
    {
        public const string RequestType = "SetTack";
        public const string ResultType = "SetTackResult";

        private readonly IModHelper Helper;
        private readonly IManifest Manifest;
        private readonly AssetRegistry Registry;
        private readonly Func<ModConfig> Config;

        public TackService(IModHelper helper, IManifest manifest, AssetRegistry registry, Func<ModConfig> config)
        {
            this.Helper = helper;
            this.Manifest = manifest;
            this.Registry = registry;
            this.Config = config;
        }

        /// <summary>Ask for a change as the local player (applies directly on the host, otherwise sends a request).</summary>
        public void RequestChange(Horse horse, TackSelection sel)
        {
            sel = sel.Clone().Normalize();
            if (Context.IsMainPlayer)
            {
                SetTackResult result = this.ApplyAsHost(Game1.player, horse.HorseId, sel);
                ShowResult(result);
                return;
            }

            IMultiplayerPeer? host = this.Helper.Multiplayer.GetConnectedPlayer(Game1.MasterPlayer.UniqueMultiplayerID);
            if (host?.GetMod(this.Manifest.UniqueID) == null)
            {
                Game1.showRedMessage(I18n.Get("message.host-missing-mod"));
                return;
            }

            this.Helper.Multiplayer.SendMessage(
                SetTackRequest.From(horse.HorseId, sel),
                RequestType,
                modIDs: new[] { this.Manifest.UniqueID },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID }
            );
            Log.Trace($"Sent tack request for {horse.displayName}: {sel}");
        }

        /// <summary>Handle a farmhand request on the host.</summary>
        public void OnRequest(long fromPlayerId, SetTackRequest request)
        {
            if (!Context.IsMainPlayer)
                return;
            Farmer? who = Game1.GetPlayer(fromPlayerId);
            SetTackResult result = this.ApplyAsHost(who, request.HorseId, request.ToSelection());
            Log.Trace($"Tack request from {who?.Name ?? fromPlayerId.ToString()} for {request.HorseId}: {(result.Ok ? "OK" : result.MessageKey)}");
            this.Helper.Multiplayer.SendMessage(result, ResultType, modIDs: new[] { this.Manifest.UniqueID }, playerIDs: new[] { fromPlayerId });
        }

        /// <summary>Validate and write a change (host only).</summary>
        public SetTackResult ApplyAsHost(Farmer? who, Guid horseId, TackSelection sel)
        {
            var result = new SetTackResult { HorseId = horseId };
            Horse? horse = HorseUtil.FindById(horseId);
            if (horse == null)
                return Fail(result, "message.horse-not-found");
            result.Arg = HorseUtil.Name(horse);

            if (HorseUtil.IsTractor(horse))
                return Fail(result, "message.tractor");
            if (!HorseUtil.CanEdit(who, horse, this.Config().AnyoneCanEdit))
                return Fail(result, "message.not-allowed");
            // a horse ridden by a farmhand is synced from that farmhand's side, so the host can't reliably write it
            if (horse.rider != null && !horse.rider.IsMainPlayer)
                return Fail(result, "message.dismount-first");

            sel.Normalize();
            foreach (TackLayer layer in TackLayers.DrawOrder)
            {
                string id = sel.Get(layer);
                if (!this.Registry.IsValid(layer, id))
                {
                    result.Arg = id;
                    return Fail(result, "message.unknown-option");
                }
                // store the canonical id, so a copy of the same art (bundled vs installed) resolves the same on every computer
                sel.Set(layer, this.Registry.Canonical(id));
            }

            sel.WriteTo(horse);
            Log.Info($"{who?.Name ?? "Someone"} restyled {HorseUtil.Name(horse)}: {sel}");
            result.Ok = true;
            result.MessageKey = "message.saved";
            return result;
        }

        public static void ShowResult(SetTackResult result)
        {
            string text = I18n.Get(result.MessageKey, new { horse = result.Arg, id = result.Arg });
            if (result.Ok)
                Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type));
            else
                Game1.showRedMessage(text);
        }

        private static SetTackResult Fail(SetTackResult result, string key)
        {
            result.Ok = false;
            result.MessageKey = key;
            return result;
        }
    }
}
