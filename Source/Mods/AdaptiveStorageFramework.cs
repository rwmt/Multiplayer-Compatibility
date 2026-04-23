using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Adaptive Storage Framework by bbradson</summary>
    /// <see href="https://github.com/bbradson/Adaptive-Storage-Framework"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3033901359"/>
    [MpCompatFor("adaptive.storage.framework")]
    class AdaptiveStorageFramework
    {
        public AdaptiveStorageFramework(ModContentPack mod)
        {
            // Deferred — AS's ThingExtensions cctor reads DefDatabase<ThingDef>
            // and throws if it's empty; mod-ctor is too early. After LongEvent
            // finishes DefDatabase is loaded.
            LongEventHandler.ExecuteWhenFinished(() => MpCompatPatchLoader.LoadPatch(this));
        }

        // Isolate spawn-time graphic randomization (MinifiedThing graphic chain
        // reaches Graphic_Random.get_MatSingle which consumes Verse.Rand) from
        // the seeded Map.FinalizeLoading scope. Both entry points need the wrap:
        //   - InitializeStoredThings: bulk re-add when the container spawns
        //   - Notify_ItemRegisteredAtCell: per-item add via the ThingGrid.
        //     RegisterInCell transpiler hook (fires on every item spawn that
        //     lands in an AS storage cell)
        [MpCompatPrefix("AdaptiveStorage.ThingClass", "InitializeStoredThings")]
        [MpCompatPrefix("AdaptiveStorage.ThingClass", "Notify_ItemRegisteredAtCell")]
        private static void PreStoredThingsChange(Thing __instance, ref bool __state)
        {
            Rand.PushState(__instance.thingIDNumber);
            __state = true;
        }

        [MpCompatFinalizer("AdaptiveStorage.ThingClass", "InitializeStoredThings")]
        [MpCompatFinalizer("AdaptiveStorage.ThingClass", "Notify_ItemRegisteredAtCell")]
        private static void PostStoredThingsChange(bool __state)
        {
            if (__state)
                Rand.PopState();
        }
    }
}
