using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Horse lookup, tractor detection and permissions.</summary>
    internal static class HorseUtil
    {
        private const string TractorModDataKey = "Pathoschild.TractorMod";

        /// <summary>Whether a horse is a Tractor Mod tractor (current modData flag or the legacy name prefix).</summary>
        public static bool IsTractor(Horse? horse)
        {
            if (horse == null)
                return false;
            return horse.modData.ContainsKey(TractorModDataKey)
                || (horse.Name?.StartsWith("tractor/", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        /// <summary>Whether a building is a Tractor Mod garage (or houses a tractor).</summary>
        public static bool IsTractorGarage(Building building)
        {
            if (building.buildingType.Value?.Contains("Tractor", StringComparison.OrdinalIgnoreCase) == true)
                return true;
            return building is Stable stable && IsTractor(stable.getStableHorse());
        }

        /// <summary>All real (non-tractor) horses this computer knows about, including mounted ones.</summary>
        public static List<Horse> GetAllHorses()
        {
            var found = new Dictionary<Guid, Horse>();
            Utility.ForEachLocation(location =>
            {
                foreach (NPC npc in location.characters)
                {
                    if (npc is Horse horse && !IsTractor(horse))
                        found.TryAdd(horse.HorseId, horse);
                }
                return true;
            }, includeInteriors: true, includeGenerated: true);

            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                if (farmer.mount is Horse mount && !IsTractor(mount))
                    found.TryAdd(mount.HorseId, mount);
            }
            return found.Values.ToList();
        }

        public static Horse? FindById(Guid id) => GetAllHorses().FirstOrDefault(h => h.HorseId == id);

        /// <summary>Find a horse by GUID or (case-insensitive) name.</summary>
        public static Horse? Find(string query)
        {
            var horses = GetAllHorses();
            if (Guid.TryParse(query, out Guid id))
                return horses.FirstOrDefault(h => h.HorseId == id);
            return horses.FirstOrDefault(h => string.Equals(h.displayName, query, StringComparison.OrdinalIgnoreCase))
                ?? horses.FirstOrDefault(h => string.Equals(h.Name, query, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Only the owner or the host may restyle a horse, unless anyone is allowed.</summary>
        public static bool CanEdit(Farmer? who, Horse horse, bool anyoneCanEdit)
        {
            if (who == null || IsTractor(horse))
                return false;
            if (anyoneCanEdit || who.IsMainPlayer)
                return true;
            long owner = horse.ownerId.Value;
            return owner != 0 && owner == who.UniqueMultiplayerID;
        }

        public static string Name(Horse horse) => string.IsNullOrWhiteSpace(horse.displayName) ? I18n.Get("horse.unnamed") : horse.displayName;

        public static string OwnerName(Horse horse) => horse.getOwner()?.Name is { Length: > 0 } name ? name : I18n.Get("horse.no-owner");
    }
}
