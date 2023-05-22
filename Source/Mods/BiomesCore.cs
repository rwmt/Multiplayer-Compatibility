using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Biomes! Core by The Biomes Mod Team</summary>
    /// <see href="https://github.com/biomes-team/BiomesCore"/>
    /// <see href="https://steamcommunity.com/workshop/filedetails/?id=2038000893"/>
    [MpCompatFor("BiomesTeam.BiomesCore")]
    public class BiomesCore
    {
        private static Type terrainCompType;
        private static AccessTools.FieldRef<object, object> terrainCompParentField;
        private static AccessTools.FieldRef<object, IntVec3> terrainInstancePositionField;

        public BiomesCore(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            // Gizmos (only dev mode gizmos)
            {
                var typeNames = new[]
                {
                    "BiomesCore.CompHarvestAnimalProduct",

                    "BiomesCore.ThingComponents.CompAnimalsAroundThing",
                    "BiomesCore.ThingComponents.CompAnimalThingSpawner",
                };

                foreach (var type in typeNames)
                    MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 0).SetDebugOnly();

                var methodNames = new[]
                {
                    "BiomesCore.CompCleanFilthAround:ClearFilth",
                    "BiomesCore.CompPlantPolluteOverTime:Pollute",
                    "BiomesCore.CompPlantProximityExplosive:Detonate",
                };

                foreach (var method in methodNames)
                    MP.RegisterSyncMethod(AccessTools.DeclaredMethod(method)).SetDebugOnly();
            }

            // Current map usage
            {
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("BiomesCore.GameCondition_Earthquake:GameConditionTick"),
                    transpiler: new HarmonyMethod(typeof(BiomesCore), nameof(UseParentMap)));
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("BiomesCore.IncidentWorker_Earthquake:TryExecuteWorker"),
                    transpiler: new HarmonyMethod(typeof(BiomesCore), nameof(UseParentMap)));
            }

            // RNG + GenView.ShouldSpawnMotesAt
            {
                // BiomesCore.GenSteps.ValleyPatch:Postfix - System RNG called, but never used

                PatchingUtilities.PatchPushPopRand(new[]
                {
                    "BiomesCore.CompPlantReleaseSpore:ThrowPoisonSmoke",
                    "BiomesCore.TerrainComp_MoteSpawner:ThrowMote",
                });
            }

            // Stopwatch
            {
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("BiomesCore.SpecialTerrainList:TerrainUpdate"),
                    prefix: new HarmonyMethod(typeof(BiomesCore), nameof(RemoveTerrainUpdateTimeBudget)));
            }
        }

        private static void LatePatch()
        {
            // HashCode on unsafe type
            {
                // Some extra setup for stuff that will be used by the patch
                terrainCompType = AccessTools.TypeByName("BiomesCore.TerrainComp");
                terrainCompParentField = AccessTools.FieldRefAccess<object>(terrainCompType, "parent");
                terrainInstancePositionField = AccessTools.FieldRefAccess<IntVec3>("BiomesCore.TerrainInstance:positionInt");

                // Patch the method using hash code to use different object if it's unsupported
                MpCompat.harmony.Patch(AccessTools.DeclaredMethod("BiomesCore.ActiveTerrainUtility:HashCodeToMod"),
                    prefix: new HarmonyMethod(typeof(BiomesCore), nameof(HashCodeOnSafeObject)));
            }
        }

        private static void RemoveTerrainUpdateTimeBudget(ref long timeBudget)
        {
            if (MP.IsInMultiplayer)
                timeBudget = long.MaxValue; // Basically limitless time

            // The method is limited in updating a max of 1/3 of all active special terrains.
            // If we'd want to work on having a performance option of some sort, we'd have to
            // base it around amount of terrain updates per tick, instead of basing it on actual time.
        }

        private static void HashCodeOnSafeObject(ref object obj)
        {
            // We don't care out of MP
            if (!MP.IsInMultiplayer)
                return;

            if (obj is ThingComp comp)
                obj = comp.parent;
            else if (obj.GetType().IsAssignableFrom(terrainCompType))
            {
                // Get the parent field (TerrainInstance)
                obj = terrainCompParentField(obj);
                // Get the IntVec3 and use that for hash code (since it's safe for MP)
                obj = terrainInstancePositionField(obj);
            }
        }

        private static IEnumerable<CodeInstruction> UseParentMap(IEnumerable<CodeInstruction> instr, MethodBase baseMethod)
        {
            var patched = false;
            var targetMethod = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));
            var isIncidentWorker = baseMethod.DeclaringType!.IsSubclassOf(typeof(IncidentWorker));

            foreach (var ci in instr)
            {
                yield return ci;

                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method && method == targetMethod)
                {
                    if (isIncidentWorker)
                    {
                        // Replace current instruction instead of skipping (keep labels in case something tries to jump here)
                        // Load first argument (0 being `this`)
                        ci.opcode = OpCodes.Ldarg_1;
                        ci.operand = null;

                        // Load the `IncidentParms.target` field on the first argument
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(IncidentParms), nameof(IncidentParms.target)));

                        // Cast the argument to Map
                        yield return new CodeInstruction(OpCodes.Castclass, typeof(Map));
                    }
                    else
                    {
                        // Replace current instruction instead of skipping (keep labels in case something tries to jump here)
                        // Call `this.`
                        ci.opcode = OpCodes.Ldarg_0;
                        ci.operand = null;

                        // Call the `SingleMap` getter
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(GameCondition), nameof(GameCondition.SingleMap)));
                    }

                    patched = true;
                }
            }

            if (!patched)
                Log.Warning("Failed patching");
        }
    }
}