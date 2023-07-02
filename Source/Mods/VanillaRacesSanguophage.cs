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
        private static Type singleUseAbilitiesCommandType;
        private static AccessTools.FieldRef<Command, Building> singleUseAbilitiesCommandBuildingField;

        public VanillaRacesSanguophage(ModContentPack mod)
        {
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

        private static void SyncSingleUseAbilitiesCommand(SyncWorker sync, ref Command command)
        {
            if (sync.isWriting)
                sync.Write(singleUseAbilitiesCommandBuildingField(command));
            else
                command = (Command)Activator.CreateInstance(singleUseAbilitiesCommandType, sync.Read<Building>());
        }
    }
}