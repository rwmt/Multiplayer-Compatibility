using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using VFEEmpire;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Empire by Oskar Potocki, xrushha, legodude17, Allie</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Empire"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380"/>
    [MpCompatFor("OskarPotocki.VFE.Empire")]
    public class VanillaFactionsEmpire
    {
        #region Init

        public VanillaFactionsEmpire(ModContentPack mod)
        {
            MpSyncWorkers.Requires<LordJob>();

            // TODO: test following
            // VFEEmpire.HarmonyPatches.Patch_AddHumanlikeOrders.<>c__DisplayClass0_1:<Postfix>b__4 - TryTakeOrderedJob (discard poisoned meal)
            // There doesn't seem to be a way to force a pawn to poison a meal, or spawn visitors with hidden deserters who could possibly cause that.
            // Having this occur naturally is taking a bit too long, so I'm leaving this note here in case it's discovered that it causes issues.
            // Important note is that it should be synced, as the method only calls Pawn_JobTracker.TryTakeOrderedJob, which is synced through MP.
            // However, there's some situations where that is not the case.

            // Rituals
            {
                // Art exhibit
                MP.RegisterSyncWorker<LordToil_ArtExhibit_Wait>(SyncArtExhibitWait);
                MP.RegisterSyncWorker<Command_ArtExhibit>(SyncArtExhibitCommand);
                // Open ritual menu
                MpCompat.RegisterLambdaMethod(typeof(LordToil_ArtExhibit_Wait), nameof(LordToil_ArtExhibit_Wait.ExtraFloatMenuOptions), 0);
                MP.RegisterSyncMethod(typeof(Command_ArtExhibit), nameof(Command_ArtExhibit.ProcessInput));
                // Leave/cancel art exhibit
                MpCompat.RegisterLambdaDelegate(typeof(LordJob_ArtExhibit), nameof(LordJob_ArtExhibit.GetPawnGizmos), 0, 2);

                // Grand ball
                MP.RegisterSyncWorker<LordToil_GrandBall_Wait>(SyncGrandBallWait);
                MP.RegisterSyncWorker<Command_GrandBall>(SyncGrandBallCommand);
                // Open ritual menu
                MpCompat.RegisterLambdaMethod(typeof(LordToil_GrandBall_Wait), nameof(LordToil_GrandBall_Wait.ExtraFloatMenuOptions), 0);
                MP.RegisterSyncMethod(typeof(Command_GrandBall), nameof(Command_GrandBall.ProcessInput));
                // Leave/cancel grand ball
                MpCompat.RegisterLambdaDelegate(typeof(LordJob_GrandBall), nameof(LordJob_GrandBall.GetPawnGizmos), 0, 2);

                // Parade
                MP.RegisterSyncWorker<LordToil_Parade_Wait>(SyncParadeWait);
                MP.RegisterSyncWorker<Command_Parade>(SyncParadeCommand);
                // Open ritual menu
                MpCompat.RegisterLambdaMethod(typeof(LordToil_Parade_Wait), nameof(LordToil_Parade_Wait.ExtraFloatMenuOptions), 0);
                MP.RegisterSyncMethod(typeof(Command_Parade), nameof(Command_Parade.ProcessInput));
                // Leave/cancel parade
                MpCompat.RegisterLambdaDelegate(typeof(LordJob_Parade), nameof(LordJob_Parade.GetPawnGizmos), 0, 2);
            }

            // Royalty tab
            {
                MP.RegisterSyncWorker<MainTabWindow_Royalty>(SyncMainTabWindow_Royalty, shouldConstruct: true);
                MP.RegisterSyncWorker<RoyaltyTabWorker>(SyncRoyaltyTabWorker, isImplicit: true, shouldConstruct: true);

                // Hierarchy, not much to patch here
                // (Re)generates data
                MP.RegisterSyncMethod(typeof(RoyaltyTabWorker_Hierarchy), nameof(RoyaltyTabWorker_Hierarchy.Notify_Open));
                // Invite pawn
                // TODO: Uncomment the following two lines once TransformField method is included in API, and remove the temporary patches call/method
                // MpCompat.RegisterLambdaDelegate(typeof(RoyaltyTabWorker_Hierarchy), nameof(RoyaltyTabWorker_Hierarchy.DoMainSection), 1)[0]
                //     .TransformField("CS$<>8__locals2/CS$<>8__locals1/pawn", Serializer.New<Pawn, int>(WriteRoyalPawn, ReadRoyalPawn));
                LongEventHandler.ExecuteWhenFinished(InitTemporaryPatches); // Inside of long event, in case something ever breaks here

                // Syncing adding/removing honors
                MP.RegisterSyncMethod(typeof(HonorUtility), nameof(HonorUtility.AddHonor));
                MP.RegisterSyncMethod(typeof(HonorUtility), nameof(HonorUtility.RemoveHonor));
                MP.RegisterSyncMethod(typeof(HonorUtility), nameof(HonorUtility.RemoveAllHonors));

                // Needed for above
                MP.RegisterSyncWorker<Honor>(SyncHonor);

                // Vassals
                MP.RegisterSyncMethod(typeof(VanillaFactionsEmpire), nameof(SyncedVassalizeSettlement));
                // Deliver tithe
                MpCompat.RegisterLambdaDelegate(typeof(RoyaltyTabWorker_Vassals), nameof(RoyaltyTabWorker_Vassals.DoVassal), 1);
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(RoyaltyTabWorker_Vassals), nameof(RoyaltyTabWorker_Vassals.DoPotentialVassal)),
                    postfix: new HarmonyMethod(typeof(VanillaFactionsEmpire), nameof(PostDoPotentialVassal)));

                // Needed for above
                MP.RegisterSyncWorker<TitheInfo>(SyncTitheInfo);

                // Needed for above
                // This method can be call from UI, and it uses RNG to create TitheInfo for a specific settlement.
                // Push/Pop state (with a seed using settlement ID and tile) to ensure the same tithe will be created for a specific settlement.
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(WorldComponent_Vassals), nameof(WorldComponent_Vassals.GetTitheInfo)),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsEmpire), nameof(PreGetTitheInfo)),
                    postfix: new HarmonyMethod(typeof(VanillaFactionsEmpire), nameof(PostGetTitheInfo)));
                // Called from delegate inside of RoyaltyTabWorker_Vassals.DoLeftBottom
                MP.RegisterSyncMethod(typeof(WorldComponent_Vassals), nameof(WorldComponent_Vassals.ReleaseAllVassalsOf));
            }

            // Permit Workers
            {
                MP.RegisterSyncMethod(typeof(VanillaFactionsEmpire), nameof(SyncSlingBeam));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod(typeof(RoyalTitlePermitWorker_Slicing), nameof(RoyalTitlePermitWorker_Slicing.OrderForceTarget)),
                    prefix: new HarmonyMethod(typeof(VanillaFactionsEmpire), nameof(PreSlicingBeamOrderForceTarget)));

                MP.RegisterSyncMethod(typeof(RoyalTitlePermitWorker_Call), nameof(RoyalTitlePermitWorker_Call.OrderForceTarget));
                MP.RegisterSyncWorker<RoyalTitlePermitWorker_Call>(SyncCallPermit, isImplicit: true);

                MpCompat.RegisterLambdaDelegate(typeof(RoyalTitlePermitWorker_CallAbsolver), nameof(RoyalTitlePermitWorker_CallAbsolver.GetRoyalAidOptions), 0);
                MpCompat.RegisterLambdaDelegate(typeof(RoyalTitlePermitWorker_CallTechfriar), nameof(RoyalTitlePermitWorker_CallTechfriar.GetRoyalAidOptions), 0);
            }

            // Honors Tracker
            {
                // Grant all honors
                MpCompat.RegisterLambdaMethod(typeof(HonorsTracker), nameof(HonorsTracker.GetGizmos), 4).SetDebugOnly();
                MP.RegisterSyncWorker<HonorsTracker>(SyncHonorsTracker);
            }
        }

        #endregion

        #region SyncWorkers

        private static void SyncHonorsTracker(SyncWorker sync, ref HonorsTracker tracker)
        {
            if (sync.isWriting)
                sync.Write(tracker.pawn);
            else
                tracker = sync.Read<Pawn>().Honors();
        }

        private static void SyncHonor(SyncWorker sync, ref Honor honor)
        {
            if (sync.isWriting)
            {
                sync.Write(honor.Pawn);
                sync.Write(honor.idNumber);
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                var id = sync.Read<int>();

                var honors = GameComponent_Honors.Instance.honors as IEnumerable<Honor>;
                if (pawn != null) 
                    honors = honors.Concat(pawn.Honors().AllHonors);

                honor = honors.FirstOrDefault(honor => honor.idNumber == id);
            }
        }

        private static void SyncMainTabWindow_Royalty(SyncWorker sync, ref MainTabWindow_Royalty tab)
        {
            sync.Bind(ref tab.CurCharacter);

            if (sync.isWriting)
                sync.Write(/* MP.CanUseDevMode && */ tab.DevMode); // TODO: Uncomment the CanUseDevMode part once it's included in the API
            else
                tab.DevMode = sync.Read<bool>();
        }

        private static void SyncRoyaltyTabWorker(SyncWorker sync, ref RoyaltyTabWorker worker)
        {
            worker.parent ??= new MainTabWindow_Royalty();
            sync.Bind(ref worker.parent);
        }

        private static void SyncTitheInfo(SyncWorker sync, ref TitheInfo titheInfo)
        {
            if (sync.isWriting)
                sync.Write(titheInfo.Settlement);
            else
                titheInfo = WorldComponent_Vassals.Instance.GetTitheInfo(sync.Read<Settlement>());
        }

        private static void SyncCallPermit(SyncWorker sync, ref RoyalTitlePermitWorker_Call permitWorker) 
            => sync.Bind(ref permitWorker.faction);

        #endregion

        #region Ritual SyncWorkers
        
        private static void SyncArtExhibitWait(SyncWorker sync, ref LordToil_ArtExhibit_Wait toil)
        {
            if (sync.isWriting)
                sync.Write(toil.lord);
            else
                toil = sync.Read<Lord>()?.curLordToil as LordToil_ArtExhibit_Wait;
        }

        private static void SyncArtExhibitCommand(SyncWorker sync, ref Command_ArtExhibit command)
        {
            if (sync.isWriting)
            {
                sync.Write(command.job.lord);
            }
            else
            {
                var lord = sync.Read<Lord>();
                if (lord == null) return;

                if (lord.CurLordToil is LordToil_ArtExhibit_Wait toil)
                    command = (Command_ArtExhibit)toil.GetPawnGizmos(toil.bestNoble).FirstOrDefault();
            }
        }

        private static void SyncGrandBallWait(SyncWorker sync, ref LordToil_GrandBall_Wait toil)
        {
            if (sync.isWriting)
                sync.Write(toil.lord);
            else
                toil = sync.Read<Lord>()?.curLordToil as LordToil_GrandBall_Wait;
        }

        private static void SyncGrandBallCommand(SyncWorker sync, ref Command_GrandBall command)
        {
            if (sync.isWriting)
            {
                sync.Write(command.job.lord);
            }
            else
            {
                var lord = sync.Read<Lord>();
                if (lord == null) return;

                if (lord.CurLordToil is LordToil_GrandBall_Wait toil)
                    command = (Command_GrandBall)toil.GetPawnGizmos(toil.bestNoble).FirstOrDefault();
            }
        }

        private static void SyncParadeWait(SyncWorker sync, ref LordToil_Parade_Wait toil)
        {
            if (sync.isWriting)
                sync.Write(toil.lord);
            else
                toil = sync.Read<Lord>()?.curLordToil as LordToil_Parade_Wait;
        }

        private static void SyncParadeCommand(SyncWorker sync, ref Command_Parade command)
        {
            if (sync.isWriting)
            {
                sync.Write(command.job.lord);
            }
            else
            {
                var lord = sync.Read<Lord>();
                if (lord == null) return;

                if (lord.CurLordToil is LordToil_Parade_Wait toil)
                    command = (Command_Parade)toil.GetPawnGizmos(toil.bestNoble).FirstOrDefault();
            }
        }

        #endregion

        #region Transformers

        // TODO: Uncomment those lines once sync transformers are included in the API
        // private static int WriteRoyalPawn(Pawn pawn) 
        //     => pawn.thingIDNumber;
        //
        // private static Pawn ReadRoyalPawn(int pawnId) 
        //     => WorldComponent_Hierarchy.Instance.TitleHolders?.Find(pawn => pawn.thingIDNumber == pawnId);

        #endregion

        #region Seeded Tithe Info for Settlements

        private static void PreGetTitheInfo(Settlement settlement)
        {
            if (MP.IsInMultiplayer)
            {
                // Ensure that no matter when the settlement tithe info is generated, it'll have identical contents for all players.
                // Using combination of settlement ID and tile to get a somewhat unique seed.
                // We could potentially add more to it, like something based on biome and/or faction. However, those could possibly
                // change due to mods changing biomes/making factions attack each other.
                Rand.PushState(Gen.HashCombineInt(settlement.ID, settlement.Tile));
            }
        }

        private static void PostGetTitheInfo()
        {
            if (MP.IsInMultiplayer)
                Rand.PopState();
        }

        #endregion

        #region Sync Vassalize Settlement

        private static void PostDoPotentialVassal(TitheInfo vassal, bool canVassalize)
        {
            if (!MP.IsInMultiplayer || !canVassalize)
                return;

            if (vassal.Lord == null)
                return;

            var lord = vassal.Lord;
            // Cleanup data before syncing
            vassal.Lord = null;
            if (vassal.Setting != TitheSetting.Special)
                vassal.Setting = TitheSetting.Never;

            SyncedVassalizeSettlement(lord, vassal.Settlement);
        }

        private static void SyncedVassalizeSettlement(Pawn pawn, Settlement settlement)
        {
            var tithe = WorldComponent_Vassals.Instance.GetTitheInfo(settlement);

            tithe.Lord = pawn;
            tithe.DaysSinceDelivery = 0;
            if (tithe.Setting != TitheSetting.Special)
                tithe.Setting = TitheSetting.EveryWeek;
        }

        #endregion

        #region Slicing Beam Syncing

        private static bool PreSlicingBeamOrderForceTarget(RoyalTitlePermitWorker_Slicing __instance, LocalTargetInfo target)
        {
            // If the origin is not valid (not assigned yet) it means it's the first pass of the targetting, let the player pick second one before syncing
            if (!MP.IsInMultiplayer || !__instance.origin.IsValid || MP.IsExecutingSyncCommand)
                return true;

            // Both targets are selected, sync the permit usage
            SyncSlingBeam(__instance, target, __instance.origin, __instance.faction);
            return false;
        }

        private static void SyncSlingBeam(RoyalTitlePermitWorker_Slicing permitWorker, LocalTargetInfo target, LocalTargetInfo origin, Faction faction)
        {
            permitWorker.faction = faction;
            permitWorker.origin = origin;
            permitWorker.OrderForceTarget(target);
        }

        #endregion

        #region Temporary Patches

        // TODO: Once the new API is in, remove this region and uncomment the code under the other TODOs that use the new API.
        private static Type firstInnerType;
        private static Type secondInnerType;

        private static AccessTools.FieldRef<object, Pawn> originalInnerTypePawnField;
        private static AccessTools.FieldRef<object, int> originalInnerTypeHonorCostField;

        private static AccessTools.FieldRef<object, Pawn> secondInnerTypePawnField;
        private static AccessTools.FieldRef<object, RoyaltyTabWorker_Hierarchy> secondInnerTypeParentField;

        private static AccessTools.FieldRef<object, object> firstInnerTypeField;
        private static AccessTools.FieldRef<object, object> secondInnerTypeField;

        private static void InitTemporaryPatches()
        {
            var method = MpMethodUtil.GetLambda(typeof(RoyaltyTabWorker_Hierarchy), nameof(RoyaltyTabWorker_Hierarchy.DoMainSection), MethodType.Normal, null, 1);
            if (method?.DeclaringType == null)
                return;

            var type = method.DeclaringType;
            originalInnerTypePawnField = AccessTools.FieldRefAccess<Pawn>(type, "p");
            originalInnerTypeHonorCostField = AccessTools.FieldRefAccess<int>(type, "honorCost");
            var field = AccessTools.DeclaredField(type, "CS$<>8__locals2");
            firstInnerType = field.FieldType;
            firstInnerTypeField = AccessTools.FieldRefAccess<object, object>(field);

            type = firstInnerType;
            field = AccessTools.DeclaredField(type, "CS$<>8__locals1");
            secondInnerType = field.FieldType;
            secondInnerTypeField = AccessTools.FieldRefAccess<object, object>(field);

            type = secondInnerType;
            secondInnerTypePawnField = AccessTools.FieldRefAccess<Pawn>(type, "pawn");
            secondInnerTypeParentField = AccessTools.FieldRefAccess<RoyaltyTabWorker_Hierarchy>(type, "<>4__this");

            MP.RegisterSyncMethod(method);
            MP.RegisterSyncWorker<object>(SyncHierarchyTabInnerType, method.DeclaringType, shouldConstruct: true);
        }

        private static void SyncHierarchyTabInnerType(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
            {
                sync.Write(originalInnerTypePawnField(obj));
                sync.Write(originalInnerTypeHonorCostField(obj));

                var first = firstInnerTypeField(obj);
                var second = secondInnerTypeField(first);
                
                sync.Write(secondInnerTypePawnField(second).thingIDNumber);
                sync.Write(secondInnerTypeParentField(second));
            }
            else
            {
                originalInnerTypePawnField(obj) = sync.Read<Pawn>();
                originalInnerTypeHonorCostField(obj) = sync.Read<int>();

                var first = Activator.CreateInstance(firstInnerType);
                firstInnerTypeField(obj) = first;

                var second = Activator.CreateInstance(secondInnerType);
                secondInnerTypeField(first) = second;

                var pawnId = sync.Read<int>();
                secondInnerTypePawnField(second) = WorldComponent_Hierarchy.Instance.TitleHolders?.Find(pawn => pawn.thingIDNumber == pawnId);
                secondInnerTypeParentField(second) = sync.Read<RoyaltyTabWorker_Hierarchy>();
            }
        }

        #endregion
    }
}