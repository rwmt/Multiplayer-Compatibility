using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>LightsOut by juanlopez2008</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2584269293"/>
    [MpCompatFor("juanlopez2008.LightsOut")]
    internal class LightsOut
    {
        private static Type commandType;
        private static MethodInfo parentCompGetter;

        public LightsOut(ModContentPack mod)
        {
            commandType = AccessTools.TypeByName("LightsOut.Gizmos.KeepOnGizmo");
            parentCompGetter = AccessTools.PropertyGetter(commandType, "ParentComp");

            MP.RegisterSyncMethod(commandType, "ToggleAction");
            MP.RegisterSyncWorker<Command_Toggle>(SyncToggleCommand, commandType);
        }

        private static void SyncToggleCommand(SyncWorker sync, ref Command_Toggle command)
        {
            if (sync.isWriting) sync.Write(parentCompGetter.Invoke(command, Array.Empty<object>()) as ThingComp);
            else command = Activator.CreateInstance(commandType, sync.Read<ThingComp>()) as Command_Toggle;
        }
    }
}
