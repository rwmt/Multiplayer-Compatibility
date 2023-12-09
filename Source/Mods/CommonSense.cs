using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Common Sense by avil</summary>
    /// <see href="https://github.com/catgirlfighter/RimWorld_CommonSense"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1561769193"/>
    [MpCompatFor("avilmask.CommonSense")]
    internal class CommonSense
    {
        private delegate ThingComp GetChecker(Thing thing, bool initShouldUnload, bool initWasInInventory);

        private static ISyncField shouldUnloadSyncField;
        private static GetChecker getCompUnlockerCheckerMethod;
        private static Type rpgStyleInventoryTabType;

        public CommonSense(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("CommonSense.CompUnloadChecker");
            // We need to make a sync worker for this Comp, as it is initialized dynamically and might not exists at the time
            MP.RegisterSyncWorker<ThingComp>(SyncComp, type);
            shouldUnloadSyncField = MP.RegisterSyncField(AccessTools.Field(type, "ShouldUnload"));
            // The GetChecker method either gets an existing, or creates a new comp
            getCompUnlockerCheckerMethod = AccessTools.MethodDelegate<GetChecker>(AccessTools.Method(type, "GetChecker"));

            // RNG Patch
            PatchingUtilities.PatchUnityRand("CommonSense.JobGiver_Wander_TryGiveJob_CommonSensePatch:Postfix", false);

            // Gizmo patch
            MpCompat.RegisterLambdaMethod("CommonSense.DoCleanComp", "CompGetGizmosExtra", 1);

            // RPG Style Inventory patch sync (popup menu)
            rpgStyleInventoryTabType = AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab");
            // If the type doesn't exist (no RPG style inventory active), skip syncing and patching the relevant parts
            if (rpgStyleInventoryTabType != null)
            {
                type = AccessTools.TypeByName("CommonSense.RPGStyleInventory_PopupMenu_CommonSensePatch");
                var method = MpMethodUtil.GetLambda(type, "Postfix");
                // Unload - skip the `object __instance` field, as we can't exactly sync it (without sync transformers, which aren't in API yet)
                MP.RegisterSyncDelegate(type, method.DeclaringType!.Name, method.Name, new[] { "pawn", "thing", "c" })
                    .SetContext(SyncContext.MapSelected);
                // Can't really sync fields of type `object`, so init it before we run the method
                MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(typeof(CommonSense), nameof(RpgStyleCompatPrefix)));
                // Cancel unload - only sync the CompUnloadCheker field, this method doesn't use anything else
                MpCompat.RegisterLambdaDelegate("CommonSense.RPGStyleInventory_PopupMenu_CommonSensePatch", "Postfix", new[] { "c" }, 1);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Watch unload bool changes
            MpCompat.harmony.Patch(AccessTools.Method("CommonSense.Utility:DrawThingRow"),
                prefix: new HarmonyMethod(typeof(CommonSense), nameof(CommonSensePatchPrefix)),
                postfix: new HarmonyMethod(typeof(CommonSense), nameof(CommonSensePatchPostfix)));
        }

        private static void CommonSensePatchPrefix(Thing thing, ref bool __state)
        {
            if (!MP.IsInMultiplayer)
                return;

            var comp = getCompUnlockerCheckerMethod(thing, false, false);
            if (comp == null)
                return;

            __state = true;
            MP.WatchBegin();
            shouldUnloadSyncField.Watch(comp);
        }

        private static void CommonSensePatchPostfix(bool __state)
        {
            if (__state)
                MP.WatchEnd();
        }

        private static void RpgStyleCompatPrefix(ref object _____instance)
        {
            // Yes, it should have 5 `_` symbols. The field has 2 in name, and we need to add 3 to access it through harmony argument
            // The __instance field used by the mod 
            if (MP.IsInMultiplayer)
                _____instance ??= Activator.CreateInstance(rpgStyleInventoryTabType);
        }

        private static void SyncComp(SyncWorker sync, ref ThingComp thing)
        {
            if (sync.isWriting)
                sync.Write<Thing>(thing.parent);
            else
                // Get existing or create a new comp
                thing = getCompUnlockerCheckerMethod(sync.Read<Thing>(), false, false);
        }
    }
}