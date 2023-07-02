using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Dubs Bad Hygiene by Dubwise</summary>
    /// <see href="https://github.com/Dubwise56/Dubs-Bad-Hygiene"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=836308268"/>
    [MpCompatFor("Dubwise.DubsBadHygiene")]
    public class BadHygiene
    {
        private static AccessTools.FieldRef<Designator, ThingDef> removePlumbingRemovalModeField;
        private static AccessTools.FieldRef<Designator, bool> placeFertilizerAddingField;

        public BadHygiene(ModContentPack mod)
        {
            // RNG
            {
                var type = AccessTools.TypeByName("DubsBadHygiene.Comp_SaunaHeater");
                var methods = new[]
                {
                    AccessTools.Method(type, "SteamyNow"),
                    AccessTools.Method(type, "CompTick"),
                };

                PatchingUtilities.PatchPushPopRand(methods);
            }

            // Designators
            {
                var type = AccessTools.TypeByName("DubsBadHygiene.Designator_AreaPlaceFertilizer");
                placeFertilizerAddingField = AccessTools.FieldRefAccess<bool>(type, "Adding");
                MP.RegisterSyncWorker<Designator>(SyncFertilizerAreaDesignator, type, shouldConstruct: true);

                type = AccessTools.TypeByName("DubsBadHygiene.Designator_RemovePlumbing");
                removePlumbingRemovalModeField = AccessTools.FieldRefAccess<ThingDef>(type, "RemovalMode");
                MP.RegisterSyncWorker<Designator>(SyncRemovePlumbingDesignator, type, shouldConstruct: true);
            }
        }

        private static void SyncRemovePlumbingDesignator(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting)
                sync.Write(removePlumbingRemovalModeField(designator));
            else
                removePlumbingRemovalModeField(designator) = sync.Read<ThingDef>();
        }

        private static void SyncFertilizerAreaDesignator(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting)
                sync.Write(placeFertilizerAddingField(designator));
            else
                placeFertilizerAddingField(designator) = sync.Read<bool>();
        }
    }
}