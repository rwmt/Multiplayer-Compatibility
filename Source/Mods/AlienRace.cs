using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Humanoid Alien Races by erdelf</summary>
    /// <see href="https://github.com/RimWorld-CCL-Reborn/AlienRaces"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=839005762"/>
    [MpCompatFor("erdelf.HumanoidAlienRaces")]
    public class AlienRace
    {
        private static Type alienCompType;
        private static MethodInfo copyAlienDataMethod;
        private static FieldInfo originPawnField;

        public AlienRace(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            alienCompType = AccessTools.TypeByName("AlienRace.AlienPartGenerator+AlienComp");
            if (alienCompType == null) return;

            copyAlienDataMethod   = AccessTools.Method(alienCompType, "CopyAlienData", new[] { alienCompType, alienCompType });
            originPawnField         = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_DummyPawn"), "origPawn");

            MpCompat.harmony.Patch(
                AccessTools.Constructor(typeof(Dialog_StylingStation), [typeof(Pawn), typeof(Thing)]),
                prefix: new HarmonyMethod(typeof(AlienRace), nameof(StylingStationCtorPrefix)));
        }

        static void StylingStationCtorPrefix(Pawn pawn)
        {
            if (pawn.AllComps != null && pawn.AllComps.Any(c => c.GetType() == alienCompType)) return;
            pawn.InitializeComps();

            var dummyComp = pawn.AllComps?.FirstOrDefault(c => c.GetType() == alienCompType);
            if (dummyComp == null) return;

            if (originPawnField?.GetValue(pawn) is Pawn originPawn)
            {
                var origPawn = originPawn.AllComps?.FirstOrDefault(c => c.GetType() == alienCompType);
                if (origPawn != null)
                    copyAlienDataMethod?.Invoke(null, [origPawn, dummyComp]);
            }
        }
    }
}
