using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Gestalt Engine and Reinforced Mechanoids 2 by Helixien</summary>
    /// <see href="https://github.com/Helixien/Gestalt-Engine"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3042401691"/>
    [MpCompatFor("hlx.GestaltEngine")]
    [MpCompatFor("hlx.ReinforcedMechanoids2")]
    public class GestaltEngine
    {
        // CompGestaltEngine
        private static AccessTools.FieldRef<IEnumerable> gestaltEnginesField;
        private static AccessTools.FieldRef<ThingComp, Pawn> gestaltEngineDummyPawnField;
        
        // CompUpgradeable
        private static FastInvokeHandler minLevelGetterCall;
        private static FastInvokeHandler maxLevelGetterCall;

        public GestaltEngine(ModContentPack mod)
        {
            var gestaltEngineCompType = AccessTools.TypeByName("GestaltEngine.CompGestaltEngine");

            // Gizmos
            {
                // Both called from targeter inside of lambda methods for CompGetGizmosExtra
                MP.RegisterSyncMethod(gestaltEngineCompType, "StartConnect"); // Connect colony mechanoid
                MP.RegisterSyncMethod(gestaltEngineCompType, "StartConnectNonColonyMech"); // Hack hostile mechanoid
                // Upgrade/downgrade (0/1) and dev instant upgrade/downgrade (2/3)
                MpCompat.RegisterLambdaMethod("GestaltEngine.CompUpgradeable", "CompGetGizmosExtra", 0, 1, 2, 3).TakeLast(2).SetDebugOnly();
                // Tune pawn (2)/engine (3) to band node (methods are identical)
                MpCompat.RegisterLambdaDelegate("GestaltEngine.CompBandNode_CompGetGizmosExtra_Patch", "Postfix", 2, 3);
            }

            // Upgrade safety checks
            {
                var type = AccessTools.TypeByName("GestaltEngine.CompUpgradeable");

                minLevelGetterCall = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "MinLevel"));
                maxLevelGetterCall = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "MaxLevel"));

                // If the gizmo is called multiple times it'll get synced that many times, but it doesn't
                // handle being pressed so much - which will cause it to start multiple upgrades/downgrades
                // (until it's finally synced for the player(s) sending the messages and stopped by the mod).
                // This will prevent issues from arising when a gizmo press has been synced multiple times.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(type, "StartUpgrade"),
                    prefix: new HarmonyMethod(typeof(GestaltEngine), nameof(PreUpgradeSafetyChecks)));
            }

            // Cache
            {
                // Ensure the static constructor is called, as else the cache will always be cleared while hosting right after it's created.
                // This causes many issues with the mod, and is likely due to harmony patch order conflict.
                // Call static constructor by calling the ResetStaticData, as trying to call it directly caused some issues (and it was faster to just use a workaround)
                LongEventHandler.ExecuteWhenFinished(() => AccessTools.DeclaredMethod("GestaltEngine.Utils:ResetStaticData").Invoke(null, Array.Empty<object>()));
            }

            // Mech group issues due to inaccessible (dummy) pawn
            {
                // Trying to change mech group for mechs assigned to the gestalt engine will cause an error (Thing Gestalt Engine is inaccessible).
                // Since the dummy pawn ("Gestalt Engine") that's used by the mod is not spawned and has no holder, MP will not be able to sync it properly.
                // The workaround here is to insert a new sync worker (as the very first one, before the MP one) that will handle this possibility.

                gestaltEnginesField = AccessTools.StaticFieldRefAccess<IEnumerable>(AccessTools.DeclaredField(gestaltEngineCompType, "compGestaltEngines"));
                gestaltEngineDummyPawnField = AccessTools.FieldRefAccess<ThingComp, Pawn>(AccessTools.DeclaredField(gestaltEngineCompType, "dummyPawn"));

                // MP.RegisterSyncWorker() call doesn't support inserting a sync worker at the beginning of the sync workers list.
                // In fact, I'm fairly certain it doesn't allow inserting a sync worker with a bool return type in the first place.
                // I think we could add properly with SyncWorkerAttribute, but this would cause an error due to multiple explicit sync workers being included.

                // Get the sync workers tree
                var syncWorkers = AccessTools.DeclaredField("Multiplayer.Client.SyncDict:syncWorkers").GetValue(null);

                // Get the entry for MechanitorControlGroup
                var method = AccessTools.DeclaredMethod("Multiplayer.Client.SyncWorkerDictionaryTree:GetOrAddEntry");
                var entry = method.Invoke(syncWorkers, new object[] { typeof(MechanitorControlGroup), false, false });

                // Get the method to add a new sync worker and add it - this method will insert it at the beginning if it has a non-void return type.
                method = AccessTools.DeclaredMethod("Multiplayer.Client.SyncWorkerEntry:Add", new []{ typeof(MethodInfo) });
                method.Invoke(entry, new object[] { AccessTools.DeclaredMethod(typeof(GestaltEngine), nameof(SyncMechanitorControlGroup)) });
            }
        }

        private static bool PreUpgradeSafetyChecks(ThingComp __instance, int upgradeOffset, int ___level, int ___upgradeProgressTick, int ___downgradeProgressTick)
        {
            // It should be 100% safe out of MP
            if (!MP.IsInMultiplayer)
                return true;

            // If currently upgrading or downgrading, don't change anything. Will prevent multiple calls from increasing the level past the current limit.
            // Dev instant upgrade/downgrade will finish this upgrade instead (2nd method it calls), and then another use of the gizmo will upgrade/downgrade.
            if (___upgradeProgressTick > 0 || ___downgradeProgressTick > 0)
                return false;

            // Prevent changes going below min or past max level
            var levelAfterChange = ___level + upgradeOffset;
            if (levelAfterChange < (int)minLevelGetterCall(__instance) || levelAfterChange > (int)maxLevelGetterCall(__instance))
                return false;

            return true;
        }

        private static bool SyncMechanitorControlGroup(SyncWorker sync, ref MechanitorControlGroup group)
        {
            // A cleaner approach would be to first sync a bool value to determine if we should handle the syncing,
            // and then return false if we aren't. The issue with this is that if we return false, anything we tried
            // to sync will get overwritten by MP sync worker instead (the reader/writer get reset when returning false).
            // This means that when trying to read data, we will instead be reading the data from the MP sync worker was
            // writing instead of this one.

            if (sync.isWriting)
            {
                var targetPawn = group.tracker.Pawn;
                // We know dummy pawn is not a spawned pawn, so we sync it.
                if (targetPawn.Spawned)
                {
                    sync.Write(true);
                    sync.Write(targetPawn);
                }
                else
                {
                    var comp = gestaltEnginesField().Cast<ThingComp>().FirstOrDefault(c => gestaltEngineDummyPawnField(c) == targetPawn);
                    // If we found a match with a dummy gestalt engine pawn, sync the comp instead.
                    if (comp != null)
                    {
                        sync.Write(false);
                        sync.Write(comp);
                    }
                    // If we didn't find a comp with a matching pawn, sync as a pawn.
                    else
                    {
                        sync.Write(true);
                        sync.Write(targetPawn);
                    }
                }

                // Sync the control group index as normal.
                sync.Write(group.tracker.controlGroups.IndexOf(group));
            }
            else
            {
                Pawn pawn;
                // Read the pawn as normal
                if (sync.Read<bool>())
                    pawn = sync.Read<Pawn>();
                // Read the gestalt engine and get its dummy pawn
                else
                    pawn = gestaltEngineDummyPawnField(sync.Read<ThingComp>());

                // Read the index and get it from the mechanitor control groups
                var index = sync.Read<int>();
                group = pawn.mechanitor.controlGroups[index];
            }

            // Since we fully replace the MP sync worker, just return true and don't let MP sync it itself.
            return true;
        }
    }
}