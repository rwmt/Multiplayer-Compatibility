using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Random Relations Blocker & Romance Whitelist by seeki</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3649058364"/>
    [MpCompatFor("seekiworksmod.no19")]
    public class seekiworks_RandomRelationsBlockerandRomanceWhitelist
    {
        static PropertyInfo PropCompRandomRelationsBlockerandRomanceWhitelist;
        static ISyncField FieldSuppressRandomRelation;
        static ISyncField FieldSuppressRomance;

        static FieldInfo FieldCompRandomRelationsBlockerandRomanceWhitelist;
        static FieldInfo FieldRomanceWhiteList;

        public seekiworks_RandomRelationsBlockerandRomanceWhitelist(ModContentPack mod)
        {
            // ITabs
            var type = AccessTools.TypeByName("seekiworks_RandomRelationsBlockerandRomanceWhitelist.ITab_RandomRelationsBlockerandRomanceWhitelist");

            MpCompat.harmony.Patch(AccessTools.Method(type,nameof(ITab.FillTab)),
                prefix: new HarmonyMethod(typeof(ITab_RandomRelationsBlockerandRomanceWhitelist_FillTab_Patch), nameof(ITab_RandomRelationsBlockerandRomanceWhitelist_FillTab_Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(ITab_RandomRelationsBlockerandRomanceWhitelist_FillTab_Patch), nameof(ITab_RandomRelationsBlockerandRomanceWhitelist_FillTab_Patch.Postfix))
                );

            PropCompRandomRelationsBlockerandRomanceWhitelist = AccessTools.Property(type, "CompRandomRelationsBlockerandRomanceWhitelist");


            // Windows
            type = AccessTools.TypeByName("seekiworks_RandomRelationsBlockerandRomanceWhitelist.DiaLog_EditRomanceWhiteList");
            MpCompat.harmony.Patch(AccessTools.Method(type, nameof(Window.DoWindowContents)),
                prefix: new HarmonyMethod(typeof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch), nameof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch.Prefix)),
                postfix: new HarmonyMethod(typeof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch), nameof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch.Postfix))
                );

            FieldCompRandomRelationsBlockerandRomanceWhitelist = AccessTools.Field(type, "compRandomRelationsBlockerandRomanceWhitelist");

            // Comp itself's fields to watch
            type = AccessTools.TypeByName("seekiworks_RandomRelationsBlockerandRomanceWhitelist.CompRandomRelationsBlockerandRomanceWhitelist");
            FieldSuppressRandomRelation = MP.RegisterSyncField(type, "suppressRandomRelation");
            FieldSuppressRomance = MP.RegisterSyncField(type, "suppressRomance");
            FieldRomanceWhiteList = AccessTools.Field(type, "romanceWhiteList");

            MP.RegisterSyncMethod(AccessTools.Method(typeof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch), nameof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch.SyncedRemoveFromWhiteList)));
            MP.RegisterSyncMethod(AccessTools.Method(typeof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch), nameof(DiaLog_EditRomanceWhiteList_DoWindowContents_Patch.SyncedAddToWhiteList)));

        }

        class ITab_RandomRelationsBlockerandRomanceWhitelist_FillTab_Patch
        {
            public static void Prefix(ITab __instance)
            {
                MP.WatchBegin();
                ThingComp comp = PropCompRandomRelationsBlockerandRomanceWhitelist.GetValue(__instance) as ThingComp;
                FieldSuppressRandomRelation.Watch(comp);
                FieldSuppressRomance.Watch(comp);
            }
            public static void Postfix()
            {
                MP.WatchEnd();
            }
        }

        class DiaLog_EditRomanceWhiteList_DoWindowContents_Patch
        {
            public static void Prefix(ITab __instance, ref HashSet<Pawn> __state)
            {
                ThingComp comp = FieldCompRandomRelationsBlockerandRomanceWhitelist.GetValue(__instance) as ThingComp;
                __state = [.. FieldRomanceWhiteList.GetValue(comp) as HashSet<Pawn>];
            }
            public static void Postfix(ITab __instance, HashSet<Pawn> __state)
            {
                ThingComp comp = FieldCompRandomRelationsBlockerandRomanceWhitelist.GetValue(__instance) as ThingComp;
                HashSet < Pawn > cur_state = FieldRomanceWhiteList.GetValue(comp) as HashSet<Pawn>;
                if (__state.Count > cur_state.Count)
                {
                    foreach(Pawn pawn in __state.Where(p => !cur_state.Contains(p)))
                    {
                        SyncedRemoveFromWhiteList(comp, pawn);
                    }
                }
                else
                {
                    foreach (Pawn pawn in cur_state.Where(p => !__state.Contains(p)))
                    {
                        SyncedAddToWhiteList(comp, pawn);
                    }
                }
            }
            public static void SyncedRemoveFromWhiteList(ThingComp comp, Pawn pawn)
            {
                HashSet<Pawn> list = FieldRomanceWhiteList.GetValue(comp) as HashSet<Pawn>;
                if (list != null)
                {
                    list.Remove(pawn);
                }
            }
            public static void SyncedAddToWhiteList(ThingComp comp, Pawn pawn)
            {
                HashSet<Pawn> list = FieldRomanceWhiteList.GetValue(comp) as HashSet<Pawn>;
                if (list != null)
                {
                    list.Add(pawn);
                }
            }
        }
    }
}