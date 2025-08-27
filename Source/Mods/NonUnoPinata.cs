using System;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Non uno Pinata by Avil</summary>
    /// <see href="https://github.com/catgirlfighter/RimWorld_NonUnoPinata"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1778821244"/>
    [MpCompatFor("avilmask.NonUnoPinata")]
    public class NonUnoPinata
    {
        
        private static Type compStripCheckerType;

        public NonUnoPinata(ModContentPack mod)
        {
            compStripCheckerType = AccessTools.TypeByName("NonUnoPinata.CompStripChecker");

            // Register the worker that encodes/decodes the CompStripChecker
            MP.RegisterSyncWorker<ThingComp>(SyncWorkerForCompStripChecker, compStripCheckerType);

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // This method is called whenever the user clicks the strip button in the ITab
            MP.RegisterSyncMethod(AccessTools.Method("NonUnoPinata.NUPUtility:SetShouldStrip"));

            // Under Multiplayer we can't have the state altered without a SyncMethod.
            // CompStripChecker.GetChecker creates the comp if not found, which is disallowed.
            // To keep the logic being used in ITab_Pawn_GearPatch, it's required that the comp exists.
            // This workaround has no impact on performance
            InjectComp();
        }

        // Sends the ThingWithComps parent for reference on write
        // Retrieves CompStripChecker from the parent on read
        static void SyncWorkerForCompStripChecker(SyncWorker sw, ref ThingComp comp)
        {
            if (sw.isWriting)
            {
                sw.Write(comp.parent);
            }
            else
            {
                comp = sw.Read<ThingWithComps>().AllComps.First(c => c.GetType() == compStripCheckerType);
            }
        }

        // Searches for haulable items and adds the CompStripChecker
        static void InjectComp()
        {
            var compProperties = new CompProperties { compClass = compStripCheckerType };
            var defs = DefDatabase<ThingDef>.AllDefs.Where(
                def => typeof(ThingWithComps).IsAssignableFrom(def.thingClass)
                    && def.EverHaulable
                    && !def.HasComp(compStripCheckerType)
            );
            foreach (var def in defs)
            {
                def.comps.Add(compProperties);
            }
        }
    }
}