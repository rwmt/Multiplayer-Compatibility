using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Android tiers by Atlas, aRandomKiwi</summary>
    [MpCompatFor("Atlas.AndroidTiers")]
    internal class AndroidTiers
    {
        // CompSurrogateOwner
        private static Type compSurrogateOwnerType;
        private static AccessTools.FieldRef<object, Thing> surrogateOwnerSkyCloudHostField;

        // CompSkyCloudCore
        private static Type compSkyCloudCoreType;
        private static AccessTools.FieldRef<object, List<Pawn>> skyCloudCoreStoredMindsField;
        private static FastInvokeHandler skyCloudCoreGetGizmos6;
        private static FastInvokeHandler skyCloudCoreGetGizmos11;
        private static FastInvokeHandler skyCloudCoreGetGizmos27;

        // Designator_SurrogateToHack
        private static ConstructorInfo surrogateToHackConstructor;
        private static AccessTools.FieldRef<object, IntVec3> surrogateToHackPosField;
        private static AccessTools.FieldRef<object, Pawn> surrogateToHackTargetField;
        private static AccessTools.FieldRef<object, Map> surrogateToHackMapField;
        private static AccessTools.FieldRef<object, int> surrogateToHackHackTypeField;

        // Designator_AndroidToControl
        private static ConstructorInfo androidToControlConstructor;
        private static AccessTools.FieldRef<object, Pawn> androidToControlPawnField;
        private static AccessTools.FieldRef<object, bool> androidToControlFromSkyCloudField;
        private static AccessTools.FieldRef<object, int> androidToControlKindOfTargetField;
        private static AccessTools.FieldRef<object, IntVec3> androidToControlPosField;
        private static AccessTools.FieldRef<object, Thing> androidToControlTargetField;
        private static AccessTools.FieldRef<object, Map> androidToControlMapField;

        // Dialog_SkillUp
        private static ConstructorInfo skillUpDialogConstructor;
        private static FastInvokeHandler skillUpGetNbPointsWantedToBuyMethod;
        private static AccessTools.FieldRef<object, Pawn> skillUpAndroidField;
        private static AccessTools.FieldRef<object, List<SkillDef>> skillUpSkillDefListField;
        private static AccessTools.FieldRef<object, List<int>> skillUpPointsField;
        private static AccessTools.FieldRef<object, List<int>> skillUpPassionsStateField;
        private static AccessTools.FieldRef<object, int> skillUpPointsNeededPerSkillField;
        private static AccessTools.FieldRef<object, int> skillUppointsNeededToIncreasePassionField;

        // GC_ATPP
        private static AccessTools.FieldRef<GameComponent> gcAttpCompField;
        private static FastInvokeHandler gameComponentDecSkillPointsMethod;
        private static FastInvokeHandler gameComponentConnectUserMethod;
        private static AccessTools.FieldRef<object, HashSet<Thing>> gameComponentConnectedThingField;
        private static AccessTools.FieldRef<object, int> gameComponentNbSlotField;

        public AndroidTiers(ModContentPack mod)
        {
            // RNG
            {
                var methods = new[]
                {
                    "MOARANDROIDS.CompHeatSensitive:PostSpawnSetup",
                    "MOARANDROIDS.CompHeatSensitive:CheckTemperature",
                    "MOARANDROIDS.StartingPawnUtility_Patch+NewGeneratedStartingPawn_Patch:Listener",
                    "MOARANDROIDS.AndroidFactionDialogOverride:Postfix",
                    "MOARANDROIDS.MultiplePawnRacesAtStart:Postfix",
                    "MOARANDROIDS.Recipe_MemoryCorruptionChance:RandomCorruption",
                    "MOARANDROIDS.Recipe_AndroidRewireSurgery:RandomCorruption",
                    "MOARANDROIDS.Recipe_RemoveSentience:RandomCorruption",
                    "MOARANDROIDS.Recipe_RerollTraits:RandomCorruption",
                    "MOARANDROIDS.StockGenerator_SlaveAndroids:GenerateThings",
                };

                PatchingUtilities.PatchSystemRand(methods, false);
            }

            // Gizmos
            {
                var type = AccessTools.TypeByName("MOARANDROIDS.CompAndroidState");
                MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 0, 2, 4, 5, 7, 10, 11, 12, 14);

                type = AccessTools.TypeByName("MOARANDROIDS.CompAutoDoor");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0, 1);

                type = AccessTools.TypeByName("MOARANDROIDS.CompRemotelyControlledTurret");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 0);

                // Really ugly... We can't sync the pawns directly here, as they're inaccessible - the ThingComp is holding them inside
                // however, it does not implement IThingHolder or whatever it's supposed to in cases like those
                // so we do a lot of workarounds to not rely on the pawn sync worker
                type = compSkyCloudCoreType = AccessTools.TypeByName("MOARANDROIDS.CompSkyCloudCore");
                skyCloudCoreStoredMindsField = AccessTools.FieldRefAccess<List<Pawn>>(type, "storedMinds");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 6, 10, 11, 12, 13, 26, 27);
                var methods = MpMethodUtil.GetLambda(type, "CompGetGizmosExtra", MethodType.Normal, null, 4, 16, 24);
                foreach (var method in methods)
                {
                    MP.RegisterSyncMethod(method);
                    MP.RegisterSyncWorker<object>(SyncInnerClassWithPawn, method.DeclaringType, shouldConstruct: true);
                }
                var methodsArray = MpMethodUtil.GetLambda(type, "CompGetGizmosExtra", MethodType.Normal, null, 6, 11, 27).ToArray();
                skyCloudCoreGetGizmos6 = MethodInvoker.GetHandler(methodsArray[0]);
                skyCloudCoreGetGizmos11 = MethodInvoker.GetHandler(methodsArray[1]);
                skyCloudCoreGetGizmos27 = MethodInvoker.GetHandler(methodsArray[2]);
                MP.RegisterSyncMethod(typeof(AndroidTiers), nameof(SyncedGetGizmos6));
                MP.RegisterSyncMethod(typeof(AndroidTiers), nameof(SyncedGetGizmos11));
                MP.RegisterSyncMethod(typeof(AndroidTiers), nameof(SyncedGetGizmos27));
                MpCompat.harmony.Patch(methodsArray[0], prefix: new HarmonyMethod(typeof(AndroidTiers), nameof(PreCompGetGizmos6)));
                MpCompat.harmony.Patch(methodsArray[1], prefix: new HarmonyMethod(typeof(AndroidTiers), nameof(PreCompGetGizmos11)));
                MpCompat.harmony.Patch(methodsArray[2], prefix: new HarmonyMethod(typeof(AndroidTiers), nameof(PreCompGetGizmos27)));

                type = AccessTools.TypeByName("MOARANDROIDS.CompSkyMind");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1, 3);

                type = compSurrogateOwnerType = AccessTools.TypeByName("MOARANDROIDS.CompSurrogateOwner");
                surrogateOwnerSkyCloudHostField = AccessTools.FieldRefAccess<Thing>(type, "skyCloudHost");
                MpCompat.RegisterLambdaDelegate(type, "CompGetGizmosExtra", 1, 3, 5, 9, 10, 13, 16, 19, 23);

                type = AccessTools.TypeByName("MOARANDROIDS.CPaths");
                MpCompat.RegisterLambdaDelegate(type, "QEE_BuildingPawnVatGrower_GetGizmosPostfix", 2, 5, 8);

                // CompComputer - all gizmos end up using a designator to pick a target, so we sync the designator
            }

            // Designators
            {
                var type = AccessTools.TypeByName("MOARANDROIDS.Designator_SurrogateToHack");
                surrogateToHackConstructor = AccessTools.Constructor(type, new[] { typeof(int) });
                surrogateToHackPosField = AccessTools.FieldRefAccess<IntVec3>(type, "pos");
                surrogateToHackTargetField = AccessTools.FieldRefAccess<Pawn>(type, "target");
                surrogateToHackMapField = AccessTools.FieldRefAccess<Map>(type, "cmap");
                surrogateToHackHackTypeField = AccessTools.FieldRefAccess<int>(type, "hackType");
                MP.RegisterSyncWorker<Designator>(SyncSurrogateToHackDesignator, type);

                type = AccessTools.TypeByName("MOARANDROIDS.Designator_AndroidToControl");
                androidToControlConstructor = AccessTools.Constructor(type, new[] { typeof(Pawn), typeof(bool) });
                androidToControlPawnField = AccessTools.FieldRefAccess<Pawn>(type, "controller");
                androidToControlFromSkyCloudField = AccessTools.FieldRefAccess<bool>(type, "fromSkyCloud");
                androidToControlKindOfTargetField = AccessTools.FieldRefAccess<int>(type, "kindOfTarget");
                androidToControlPosField = AccessTools.FieldRefAccess<IntVec3>(type, "pos");
                androidToControlTargetField = AccessTools.FieldRefAccess<Thing>(type, "target");
                androidToControlMapField = AccessTools.FieldRefAccess<Map>(type, "cmap");
                MP.RegisterSyncWorker<Designator>(SyncAndroidToControlDesignator, type);
                MP.RegisterSyncMethod(typeof(AndroidTiers), nameof(SyncedConnectToSkyMind));
                MpCompat.harmony.Patch(AccessTools.Method(type, "CanDesignateThing"),
                    transpiler: new HarmonyMethod(typeof(AndroidTiers), nameof(ReplaceConnectToSkyMind)));
            }

            // Dialogs
            {
                var type = AccessTools.TypeByName("MOARANDROIDS.Dialog_SkillUp");
                skillUpDialogConstructor = AccessTools.Constructor(type, new[] { typeof(Pawn), typeof(bool) });
                skillUpGetNbPointsWantedToBuyMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "getNbPointsWantedToBuy"));
                skillUpAndroidField = AccessTools.FieldRefAccess<Pawn>(type, "android");
                skillUpSkillDefListField = AccessTools.FieldRefAccess<List<SkillDef>>(type, "sd");
                skillUpPointsField = AccessTools.FieldRefAccess<List<int>>(type, "points");
                skillUpPassionsStateField = AccessTools.FieldRefAccess<List<int>>(type, "points");
                skillUpPointsNeededPerSkillField = AccessTools.FieldRefAccess<int>(type, "pointsNeededPerSkill");
                skillUppointsNeededToIncreasePassionField = AccessTools.FieldRefAccess<int>(type, "pointsNeededToIncreasePassion");
                MP.RegisterSyncMethod(typeof(AndroidTiers), nameof(SyncedSkillUpDialogAccept));
                MP.RegisterSyncWorker<Window>(SyncSkillUpDialog, type);
                MpCompat.harmony.Patch(AccessTools.Method(type, "DoWindowContents"),
                    transpiler: new HarmonyMethod(typeof(AndroidTiers), nameof(ReplaceButton)));

                type = AccessTools.TypeByName("MOARANDROIDS.Utils");
                gcAttpCompField = AccessTools.StaticFieldRefAccess<GameComponent>(AccessTools.Field(type, "GCATPP"));

                type = AccessTools.TypeByName("MOARANDROIDS.GC_ATPP");
                gameComponentDecSkillPointsMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "decSkillPoints"));
                gameComponentConnectUserMethod = MethodInvoker.GetHandler(AccessTools.Method(type, "connectUser"));
                gameComponentConnectedThingField = AccessTools.FieldRefAccess<HashSet<Thing>>(type, "connectedThing");
                gameComponentNbSlotField = AccessTools.FieldRefAccess<int>(type, "nbSlot");
            }

            // Choice letters
            {
                var type = AccessTools.TypeByName("Multiplayer.Client.Patches.CloseDialogsForExpiredLetters");
                // We should probably add this to the API the next time we update it
                var rejectMethods = (Dictionary<Type, MethodInfo>)AccessTools.Field(type, "rejectMethods").GetValue(null);

                var typeNames = new[]
                {
                    "MOARANDROIDS.ChoiceLetter_RansomDemand",
                    "MOARANDROIDS.ChoiceLetter_RansomwareDemand",
                };

                foreach (var typeName in typeNames)
                {
                    type = AccessTools.TypeByName(typeName);
                    rejectMethods.Add(type, AccessTools.Method(typeof(ChoiceLetter), nameof(ChoiceLetter.Option_Reject))); // Generic reject option
                    var method = MpMethodUtil.GetLambda(type, "Choices", MethodType.Getter, lambdaOrdinal: 0);
                    MP.RegisterSyncMethod(method);
                }
            }
        }

        private static void SyncSurrogateToHackDesignator(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting)
            {
                sync.Write((byte)surrogateToHackHackTypeField(designator));
                sync.Write(surrogateToHackTargetField(designator));
                var map = surrogateToHackMapField(designator);

                // Looks like the map and cell are set in only one method, which is never called
                // so we'll most likely end up never syncing them. I'm leaving it for safety, though.
                if (map == null)
                {
                    sync.Write(IntVec3.Invalid);
                }
                else
                {
                    sync.Write(surrogateToHackPosField(designator));
                    sync.Write(map);
                }
            }
            else
            {
                designator = (Designator)surrogateToHackConstructor.Invoke(new object[] { (int)sync.Read<byte>() });
                surrogateToHackTargetField(designator) = sync.Read<Pawn>();
                var pos = sync.Read<IntVec3>();

                if (pos.IsValid)
                {
                    surrogateToHackPosField(designator) = pos;
                    surrogateToHackMapField(designator) = sync.Read<Map>();
                }
            }
        }

        // Pawn is inaccessible (in some of the cases I think)... Ugly workarounds required
        private static void SyncAndroidToControlDesignator(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting)
            {
                var pawn = androidToControlPawnField(designator);
                var comp = pawn.AllComps.FirstOrDefault(c => c.GetType() == compSurrogateOwnerType);
                var thing = (ThingWithComps)surrogateOwnerSkyCloudHostField(comp);

                sync.Write(thing == null);
                if (thing == null)
                    sync.Write(pawn);
                else
                    sync.Write(thing);
                sync.Write(pawn.thingIDNumber);
                sync.Write(androidToControlFromSkyCloudField(designator));
                sync.Write(androidToControlKindOfTargetField(designator));
                sync.Write(androidToControlTargetField(designator));

                var map = androidToControlMapField(designator);

                // Looks like the map and cell are set in only one method, which is never called
                // so we'll most likely end up never syncing them. I'm leaving it for safety, though.
                if (map == null)
                {
                    sync.Write(IntVec3.Invalid);
                }
                else
                {
                    sync.Write(androidToControlPosField(designator));
                    sync.Write(map);
                }
            }
            else
            {
                var canAccessPawn = sync.Read<bool>();

                Pawn pawn;
                if (canAccessPawn)
                    pawn = sync.Read<Pawn>();
                else
                {
                    var compHolder = sync.Read<ThingWithComps>();
                    var comp = compHolder.AllComps.FirstOrDefault(c => c.GetType() == compSkyCloudCoreType);
                    var pawnId = sync.Read<int>();
                    pawn = GetPawnFromComp(comp, pawnId);
                }

                var fromSkyCloud = sync.Read<bool>();

                designator = (Designator)androidToControlConstructor.Invoke(new object[] { pawn, fromSkyCloud });
                androidToControlKindOfTargetField(designator) = sync.Read<int>();
                androidToControlTargetField(designator) = sync.Read<Thing>();

                var pos = sync.Read<IntVec3>();

                if (pos.IsValid)
                {
                    androidToControlPosField(designator) = pos;
                    androidToControlMapField(designator) = sync.Read<Map>();
                }
            }
        }

        private static void SyncSkillUpDialog(SyncWorker sync, ref Window dialog)
        {
            if (sync.isWriting)
            {
                sync.Write(skillUpAndroidField(dialog));
                sync.Write(skillUpSkillDefListField(dialog));
                sync.Write(skillUpPointsField(dialog));
                sync.Write(skillUpPassionsStateField(dialog));
                sync.Write(skillUpPointsNeededPerSkillField(dialog));
                sync.Write(skillUppointsNeededToIncreasePassionField(dialog));
            }
            else
            {
                var pawn = sync.Read<Pawn>();
                dialog = (Window)skillUpDialogConstructor.Invoke(new object[] { pawn, false });
                skillUpSkillDefListField(dialog) = sync.Read<List<SkillDef>>();
                skillUpPointsField(dialog) = sync.Read<List<int>>();
                skillUpPassionsStateField(dialog) = sync.Read<List<int>>();
                skillUpPointsNeededPerSkillField(dialog) = sync.Read<int>();
                skillUppointsNeededToIncreasePassionField(dialog) = sync.Read<int>();
            }
        }

        private static void SyncInnerClassWithPawn(SyncWorker sync, ref object inner)
        {
            var traverse = Traverse.Create(inner);

            if (sync.isWriting)
            {
                var comp = (ThingComp)traverse.Field("<>4__this").GetValue();
                var pawn = (Pawn)traverse.Field("p").GetValue();

                sync.Write(comp);
                sync.Write(pawn.thingIDNumber);
            }
            else
            {
                var comp = sync.Read<ThingComp>();
                var id = sync.Read<int>();
                var pawn = GetPawnFromComp(comp, id);

                traverse.Field("<>4__this").SetValue(comp);
                if (pawn != null)
                    traverse.Field("p").SetValue(pawn);
            }
        }

        private static void SyncedSkillUpDialogAccept(Pawn android, List<SkillDef> sd, List<int> points, List<int> passionsState, int pointsPerSkill, int pointsPerPassion)
        {
            var dialog = (Window)skillUpDialogConstructor.Invoke(new object[] { android, false });
            skillUpSkillDefListField(dialog) = sd;
            skillUpPointsField(dialog) = points;
            skillUpPassionsStateField(dialog) = passionsState;
            skillUpPointsNeededPerSkillField(dialog) = pointsPerSkill;
            skillUppointsNeededToIncreasePassionField(dialog) = pointsPerPassion;

            var pointsWantedToBuy = (int)skillUpGetNbPointsWantedToBuyMethod(dialog, -1);
            if (pointsWantedToBuy == 0)
                return;

            gameComponentDecSkillPointsMethod(gcAttpCompField(), pointsWantedToBuy * pointsPerSkill);

            for (var i = 0; i != sd.Count; i++)
            {
                var skill = android.skills.GetSkill(sd[i]);
                if (skill == null) continue;

                skill.levelInt += points[i];

                if (passionsState[i] - (int)skill.passion > 0)
                {
                    skill.passion = (Passion)passionsState[i];
                }
            }

            if (MP.IsExecutingSyncCommandIssuedBySelf)
            {
                Messages.Message("ATPP_SkillsWorkshopPointsConverted".Translate(pointsWantedToBuy, android.LabelShortCap), MessageTypeDefOf.PositiveEvent);
                Find.WindowStack.TryRemove(dialog.GetType());
            }
        }

        private static bool RedirectedMethod(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, Window instance)
        {
            if (Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active))
            {
                if (!MP.IsInMultiplayer)
                    return true;

                var pawn = skillUpAndroidField(instance);
                var skillDef = skillUpSkillDefListField(instance);
                var points = skillUpPointsField(instance);
                var passions = skillUpPassionsStateField(instance);
                var pointsPerSkill = skillUpPointsNeededPerSkillField(instance);
                var pointsPerPassion = skillUppointsNeededToIncreasePassionField(instance);

                SyncedSkillUpDialogAccept(pawn, skillDef, points, passions, pointsPerSkill, pointsPerPassion);
            }

            return false;
        }

        private static void SyncedConnectToSkyMind(Thing pawn, bool broadcastEvent)
        {
            var comp = gcAttpCompField();
            gameComponentConnectUserMethod.Invoke(comp, pawn, broadcastEvent);
        }

        private static bool InjectedConnectUser(GameComponent comp, Thing pawn, bool broadcastEvent)
        {
            if (!MP.IsInMultiplayer)
                return (bool)gameComponentConnectUserMethod.Invoke(comp, pawn, broadcastEvent);

            var set = gameComponentConnectedThingField(comp);

            if (set.Contains(pawn))
                return true;
            if (set.Count >= gameComponentNbSlotField(comp))
                return false;

            SyncedConnectToSkyMind(pawn, broadcastEvent);
            return true;
        }

        private static Pawn GetPawnFromComp(ThingComp comp, int id)
        {
            var list = skyCloudCoreStoredMindsField(comp);
            return list.FirstOrDefault(x => x.thingIDNumber == id);
        }

        private static void SyncedGetGizmos6(ThingComp comp, int id) => skyCloudCoreGetGizmos6.Invoke(comp, GetPawnFromComp(comp, id));
        private static void SyncedGetGizmos11(ThingComp comp, int id) => skyCloudCoreGetGizmos11.Invoke(comp, GetPawnFromComp(comp, id));
        private static void SyncedGetGizmos27(ThingComp comp, int id) => skyCloudCoreGetGizmos27.Invoke(comp, GetPawnFromComp(comp, id));

        private static bool PreCompGetGizmos6(ThingComp __instance, Pawn p)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            SyncedGetGizmos6(__instance, p.thingIDNumber);
            return false;
        }

        private static bool PreCompGetGizmos11(ThingComp __instance, Pawn p)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            SyncedGetGizmos11(__instance, p.thingIDNumber);
            return false;
        }

        private static bool PreCompGetGizmos27(ThingComp __instance, Pawn p)
        {
            if (!MP.IsInMultiplayer || MP.IsExecutingSyncCommand)
                return true;

            SyncedGetGizmos27(__instance, p.thingIDNumber);
            return false;
        }

        private static IEnumerable<CodeInstruction> ReplaceButton(IEnumerable<CodeInstruction> instr)
        {
            var method = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool) });
            var toReplace = false;
            var finished = false;

            foreach (var ci in instr)
            {
                if (!finished)
                {
                    if (toReplace)
                    {
                        if (ci.opcode == OpCodes.Call && (MethodInfo)ci.operand == method)
                        {
                            // Inject another argument (instance)
                            yield return new CodeInstruction(OpCodes.Ldarg_0);

                            ci.operand = AccessTools.Method(typeof(AndroidTiers), nameof(RedirectedMethod));
                            finished = true;
                        }
                    }
                    else if (ci.opcode == OpCodes.Ldstr && (string)ci.operand == "OK")
                        toReplace = true;
                }

                yield return ci;
            }
        }

        private static IEnumerable<CodeInstruction> ReplaceConnectToSkyMind(IEnumerable<CodeInstruction> instr)
        {
            var method = AccessTools.Method("MOARANDROIDS.GC_ATPP:connectUser");
            var replacement = AccessTools.Method(typeof(AndroidTiers), nameof(InjectedConnectUser));

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Callvirt && (MethodInfo)ci.operand == method)
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = replacement;
                }

                yield return ci;
            }
        }
    }
}
