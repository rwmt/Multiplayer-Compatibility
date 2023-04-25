using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Races Expanded - Sanguophage by Oskar Potocki, Sarg Bjornson, Erin</summary>
    /// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Sanguophage"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2963116383"/>
    [MpCompatFor("vanillaracesexpanded.sanguophage")]
    public class VanillaRacesSanguophage
    {
        private static GameConditionDef bloodmoonConditionDef;

        private static Type singleUseAbilitiesCommandType;
        private static AccessTools.FieldRef<Command, Building> singleUseAbilitiesCommandBuildingField;

        public VanillaRacesSanguophage(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);

            var type = singleUseAbilitiesCommandType = AccessTools.TypeByName("VanillaRacesExpandedSanguophage.Command_SingleUseAbilities");
            singleUseAbilitiesCommandBuildingField = AccessTools.FieldRefAccess<Building>(type, "building");
            MP.RegisterSyncWorker<Command>(SyncSingleUseAbilitiesCommand, type);
            MpCompat.RegisterLambdaDelegate(type, "ProcessInput", 0);

            type = AccessTools.TypeByName("VanillaRacesExpandedSanguophage.CompDraincasket");
            // Called from <CompGetGizmosExtra>b__47_0
            MP.RegisterSyncMethod(type, "EjectContents");
            // Creates a job (should be handled through MP), and operates on the comp (which we need to sync) 
            MpCompat.RegisterLambdaDelegate(type, "AddCarryToBatteryJobs", 0);
        }

        // TODO: Remove if fixed in the mod itself (fixed by following PR: https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Sanguophage/pull/1)
        private static void LatePatch()
        {
            var unsafeCall = AccessTools.DeclaredPropertyGetter(typeof(Game), nameof(Game.CurrentMap));
            var vfePatch = AccessTools.DeclaredMethod("VanillaRacesExpandedSanguophage.VanillaRacesExpandedSanguophage_GeneResourceDrainUtility_OffsetResource_Apply_Patch:DoubleHemogenLoss");

            if (!PatchProcessor.GetCurrentInstructions(vfePatch).Any(ci => ci.operand is MethodInfo method && method == unsafeCall))
            {
                Log.Warning($"It looks like VFE patch for {nameof(GeneResourceDrainUtility)}.{nameof(GeneResourceDrainUtility.OffsetResource)} was fixed. MP compat patch can now be safely removed from the code. The patch is currently inactive.");
                return;
            }

            bloodmoonConditionDef = (GameConditionDef)AccessTools.Field("VanillaRacesExpandedSanguophage.InternalDefOf:VRE_BloodMoonCondition")?.GetValue(null);
            if (bloodmoonConditionDef == null)
                Log.Error("GameConditionDef `VRE_BloodMoonCondition` not found. Double hemogen loss during blood moons will be disabled in MP.");

            MpCompat.harmony.Patch(
                vfePatch,
                prefix: new HarmonyMethod(typeof(VanillaRacesSanguophage), nameof(PreVfeOffsetResource)));
        }

        private static void SyncSingleUseAbilitiesCommand(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
                sync.Write(singleUseAbilitiesCommandBuildingField(command));
            else
                command = (Command)Activator.CreateInstance(singleUseAbilitiesCommandType, sync.Read<Building>());
        }

        // TODO: Remove if fixed in the mod itself (fixed by following PR: https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Sanguophage/pull/1)
        private static bool PreVfeOffsetResource(IGeneResourceDrain drain, float amnt)
        {
            // The method we're patching doesn't do anything if it's positive/zero, skip executing it
            if (amnt >= 0)
                return false;

            // Let the mod itself handle this stuff, especially if we failed getting 
            if (!MP.IsInMultiplayer)
                return true;

            // Can't handle the blood moon, so just skip the original method and don't do anything the def for blood moon
            if (bloodmoonConditionDef == null)
                return false;

            // Handle the extra change if the blood moon is active (and pawn/map aren't null)
            if (drain.Pawn?.Map?.GameConditionManager.ConditionIsActive(bloodmoonConditionDef) == true)
            {
                var value = drain.Resource.Value;
                drain.Resource.Value += amnt;
                GeneResourceDrainUtility.PostResourceOffset(drain, value);
            }

            return false;
        }
    }
}