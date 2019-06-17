using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Harmony;

using Multiplayer.API;

using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Hospitality by Orion</summary>
    /// <remarks>Everything seems to work.</remarks>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=753498552"/>
    /// <see href="https://github.com/OrionFive/Hospitality"/>
    [MpCompatFor("Hospitality")]
    public class HospitalityCompat
    {
        static MethodInfo GetCompMethod;
        static ISyncField _chat, _recruit, _arrived, _sentAway;
        static ISyncField _areaGuest, _areaShop;

        static MethodInfo GetMapCompMethod;
        static ISyncField _defMode, _defAreaGuest, _defAreaShop;

        public HospitalityCompat(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LateLoad);
        }

        static void LateLoad()
        {
            Type type;
            {
                MP.RegisterSyncMethod(AccessTools.Method("Hospitality.Building_GuestBed:Swap"));
            }
            {
                MP.RegisterSyncMethod(AccessTools.Method("Hospitality.GuestUtility:ForceRecruit"));
            }
            {
                type = AccessTools.TypeByName("Hospitality.ITab_Pawn_Guest");

                MP.RegisterSyncMethod(AccessTools.Method(type, "SendHome"));

                MpCompat.harmony.Patch(AccessTools.Method(type, "FillTabGuest"),
                    prefix: new HarmonyMethod(typeof(HospitalityCompat), nameof(WatchPrefix)),
                    postfix: new HarmonyMethod(typeof(HospitalityCompat), nameof(WatchPostfix))
                    );
            }
            {
                type = AccessTools.TypeByName("Hospitality.CompGuest");

                _chat = MP.RegisterSyncField(type, "chat");
                _recruit = MP.RegisterSyncField(type, "recruit");
                _arrived = MP.RegisterSyncField(type, "arrived");
                _sentAway = MP.RegisterSyncField(type, "sentAway");

                _areaGuest = MP.RegisterSyncField(type, "guestArea_int").SetBufferChanges();
                _areaShop = MP.RegisterSyncField(type, "shoppingArea_int").SetBufferChanges();

                GetCompMethod = AccessTools.Method("Verse.ThingWithComps:GetComp").MakeGenericMethod(type);

                MP.RegisterSyncWorker<ThingComp>(SyncWorkerForCompGuest, type);
            }
            {
                MpCompat.harmony.Patch(AccessTools.Method("Hospitality.GenericUtility:DoAreaRestriction"),
                    transpiler: new HarmonyMethod(typeof(HospitalityCompat), nameof(StopRecursiveCall))
                    );
            }
            {
                type = AccessTools.TypeByName("Hospitality.Hospitality_MapComponent");

                _defMode = MP.RegisterSyncField(type, "defaultInteractionMode");
                _defAreaGuest = MP.RegisterSyncField(type, "defaultAreaRestriction");
                _defAreaShop = MP.RegisterSyncField(type, "defaultAreaShopping");

                GetMapCompMethod = AccessTools.Method(type, "Instance");

                MP.RegisterSyncWorker<MapComponent>(SyncWorkerForMapComp, type);
            }
        }

        #region CompGuest

        static void SyncWorkerForCompGuest(SyncWorker sync, ref ThingComp comp)
        {
            if (sync.isWriting) {
                sync.Write(comp.parent);
            } else {
                ThingWithComps pawn = sync.Read<Pawn>();

                comp = (ThingComp) GetCompMethod.Invoke(pawn, null);
            }
        }

        static void WatchPrefix(ITab_Pawn_Visitor __instance)
        {
            if (MP.IsInMultiplayer) {
                MP.WatchBegin();

                Pawn pawn = __instance.SelPawn;

                object comp = GetCompMethod.Invoke(pawn, null);

                _chat.Watch(comp);
                _recruit.Watch(comp);
                _arrived.Watch(comp);
                _sentAway.Watch(comp);

                _areaGuest.Watch(comp);
                _areaShop.Watch(comp);

                object worldComp = GetMapCompMethod.Invoke(null, new object[] { pawn.Map });

                _defMode.Watch(worldComp);
                _defAreaGuest.Watch(worldComp);
                _defAreaShop.Watch(worldComp);
            }
        }

        static void WatchPostfix()
        {
            if (MP.IsInMultiplayer) {
                MP.WatchEnd();
            }
        }

        static IEnumerable<CodeInstruction> StopRecursiveCall(IEnumerable<CodeInstruction> e, ILGenerator generator) {

            var voidMethod = AccessTools.Method(typeof(HospitalityCompat), nameof(Void));
            var offender = AccessTools.Property(AccessTools.TypeByName("RimWorld.Pawn_PlayerSettings"), "AreaRestriction").GetSetMethod();

            return new CodeMatcher(e, generator)
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, offender))
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, voidMethod))
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, offender))
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, voidMethod))
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, offender))
                .SetInstruction(new CodeInstruction(OpCodes.Call, voidMethod))
                .Instructions();
        }

        static void Void(object obj, Area area)
        {

        }

        #endregion

        static void SyncWorkerForMapComp(SyncWorker sync, ref MapComponent comp)
        {
            if (sync.isWriting) {
                sync.Write(comp.map);
            } else {
                Map map = sync.Read<Map>();

                comp = (MapComponent) GetMapCompMethod.Invoke(null, new object[] { map });
            }
        }
    }
}
