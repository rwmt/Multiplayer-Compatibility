using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;
using RimWorld;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;

namespace Multiplayer.Compat
{
    /// <summary>Prison Labor by Avius and Hazzer</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1899474310"/>
    [MpCompatFor("avius.prisonlabor")]
    class PrisonLabor
    {
        public PrisonLabor(ModContentPack mod)
        {
            Type billType = AccessTools.TypeByName("PrisonLabor.Core.BillAssignation.BillAssignationUtility");
            Type prisonertabType = AccessTools.TypeByName("PrisonLabor.HarmonyPatches.Patches_GUI.GUI_PrisonerTab.Patch_PrisonerTab");
            Type timerType = AccessTools.TypeByName("PrisonLabor.Core.BaseClasses.SimpleTimer");

            MethodInfo addrecruitButton = AccessTools.Method(prisonertabType, "AddRecruitButton");

            //Sync bill tab button
            MP.RegisterSyncMethod(billType, "SetFor");

            //Sync resocialization offer button
            MpCompat.harmony.Patch(addrecruitButton, null, null, new HarmonyMethod(typeof(PrisonLabor), nameof(PrisonLabor.Transpiler)));
            MP.RegisterSyncMethod(typeof(PrisonLabor), nameof(ConvertPrisoner)).SetContext(SyncContext.MapSelected);
            
            //Sync timer used to track escape
            MP.RegisterSyncMethod(timerType, "Start");
        
        }

        private static IEnumerable<CodeInstruction> Transpiler(ILGenerator gen, IEnumerable<CodeInstruction> instr)
        {
            var instructuionList = instr.ToList();
            int startpos;
            int endpos;
            
            for(int i=0;i<instructuionList.Count;i++)
            {
                if(FoundInstruction(instructuionList, i)) //"i" is at Ldloc_0
                {
                    //assign for later
                    startpos = i;
                    endpos = instructuionList.Count - 3;//10: 0-9, and excluding opcode ret and a nop that a jump will land there

                    //now remove already exist instruction that we are going to inject beforehand
                    instructuionList.RemoveRange(startpos, endpos + 1 - startpos);

                    //now all instruction starting from starpos should be removed, we can inject our own
                    instructuionList.Insert(startpos,new CodeInstruction(OpCodes.Ldloc_0));
                    instructuionList.Insert(startpos + 1,new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PrisonLabor), nameof(ConvertPrisoner))));
                }
            }
            return instructuionList;
        }
    
        //Checking for a specific order of opcode to make sure it is the start of what we want to patch
        private static bool FoundInstruction(List<CodeInstruction> instructionlist, int index)
        {
            try
            {
                return (instructionlist[index].opcode == OpCodes.Ldloc_0 &&
                   instructionlist[index - 1].opcode == OpCodes.Nop &&
                   instructionlist[index - 2].opcode == OpCodes.Brfalse_S);
            }
            catch (Exception) { return false; }
        
        }
        public static void ConvertPrisoner(Pawn pawn)
        {
            pawn.guest.SetGuestStatus(null, false);
            pawn.SetFaction(Faction.OfPlayer, null);
        }
    }
}
