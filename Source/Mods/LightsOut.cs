using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>LightsOut by juanlopez2008</summary>
    /// <see href="https://github.com/NathanIkola/LightsOut"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2584269293"/>
    [MpCompatFor("juanlopez2008.LightsOut")]
    internal class LightsOut
    {
        public LightsOut(ModContentPack mod)
        {
            // Gizmos
            {
                MP.RegisterSyncMethod(AccessTools.PropertySetter("LightsOut.ThingComps.KeepOnComp:KeepOn"));
            }

            // Patched sync methods
            {
                PatchingUtilities.PatchCancelInInterface(
                    // From the patches we care for, PatchTableOff affects gene extractor and subcore scanner
                    "LightsOut.Common.TablesHelper:PatchTableOff",
                    // PatchTableOn doesn't look like it needs a patch, as it's not used on any sync methods
                    "LightsOut.Patches.ModCompatibility.Biotech.PatchGeneAssembly:OnStart",
                    "LightsOut.Patches.ModCompatibility.Biotech.PatchWasteAtomiser:UpdateWorking");
            }
        }
    }
}
