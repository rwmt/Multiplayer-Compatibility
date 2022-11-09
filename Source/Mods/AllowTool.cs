using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Allow Tool by UnlimitedHugs</summary>
    [MpCompatFor("UnlimitedHugs.AllowTool")]
    public class AllowTool
    {
        private static FastInvokeHandler partyHuntWorldSettingsGetter;

        public AllowTool(ModContentPack mod)
        {
            // Gizmos
            {
                // Drafted hunt
                var getter = AccessTools.PropertyGetter("AllowTool.Command_PartyHunt:WorldSettings");
                partyHuntWorldSettingsGetter = MethodInvoker.GetHandler(getter);

                var type = AccessTools.TypeByName("AllowTool.Settings.PartyHuntSettings");
                MP.RegisterSyncMethod(type, "AutoFinishOff");
                MP.RegisterSyncMethod(type, "HuntDesignatedOnly");
                MP.RegisterSyncMethod(type, "UnforbidDrops");
                MP.RegisterSyncMethod(type, "TogglePawnPartyHunting");
                MP.RegisterSyncWorker<object>(SyncDraftedHuntSettings, type);
            }

            // Right-click designator options
            {
                var type = AccessTools.TypeByName("AllowTool.Context.BaseContextMenuEntry");
                MP.RegisterSyncMethod(type, "ActivateAndHandleResult")
                    .SetContext(SyncContext.MapSelected | SyncContext.CurrentMap); // All use current map, some use selected thing
                // None of them have any constructor arguments, and none of them story any relevant code - we only care for the code they call
                MP.RegisterSyncWorker<object>(ConstructOnlySyncWorker, type, true);
            }

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            // Strip mine
            MP.RegisterSyncMethod(AccessTools.Method("AllowTool.Designator_StripMine:DesignateCells"));
        }

        private static void SyncDraftedHuntSettings(SyncWorker sync, ref object settings)
        {
            if (!sync.isWriting)
                settings = partyHuntWorldSettingsGetter(null);
        }

        private static void ConstructOnlySyncWorker(SyncWorker sync, ref object obj)
        {
            if (sync.isWriting)
                sync.Write(obj.GetType());
            else
                obj = Activator.CreateInstance(sync.Read<Type>());
        }
    }
}