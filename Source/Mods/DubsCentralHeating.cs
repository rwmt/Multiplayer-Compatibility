using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Dubs Central Heating by Dubwise</summary>
    /// <see href="https://github.com/Dubwise56/Dubs-Central-Heating"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2619214952"/>
    [MpCompatFor("Dubwise.DubsCentralHeating")]
    public class DubsCentralHeating
    {
        private static AccessTools.FieldRef<Designator, ThingDef> removePlumbingRemovalModeField;

        public DubsCentralHeating(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            // Designators
            // Same patch as Bad Hygiene, different namespace.
            var type = AccessTools.TypeByName("DubsCentralHeating.Designator_RemovePlumbing");
            removePlumbingRemovalModeField = AccessTools.FieldRefAccess<ThingDef>(type, "RemovalMode");
            MP.RegisterSyncWorker<Designator>(SyncRemovePlumbingDesignator, type, shouldConstruct: true);

            // Patch all methods in the mod.
            // It looks like it has all the required methods marked with SyncMethod attribute. It just never registers it.
            // This is likely due to the mod source code being based on Bad Hygiene, just with unnecessary stuff stripped away.
            MP.RegisterAll(type.Assembly);
        }

        private static void SyncRemovePlumbingDesignator(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting)
                sync.Write(removePlumbingRemovalModeField(designator));
            else
                removePlumbingRemovalModeField(designator) = sync.Read<ThingDef>();
        }
    }
}