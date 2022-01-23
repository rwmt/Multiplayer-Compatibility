using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;
using RimWorld;
using System.Reflection;

namespace Multiplayer.Compat
{
    /// <summary>Quality Builder by Hatti</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=754637870"/>
    [MpCompatFor("hatti.qualitybuilder")]
    class QualityBuilder
    {
        public QualityBuilder(ModContentPack mod)
        {
            Type builderCompType = AccessTools.TypeByName("QualityBuilder.CompQualityBuilder");
            Type toggleCommandType = AccessTools.TypeByName("QualityBuilder.CompQualityBuilder+ToggleCommand+<>c");
            Type designatorType = AccessTools.TypeByName("QualityBuilder._Designator_SkilledBuilder+<>c");

            Type[] argTypes = { typeof(QualityCategory) };

            MethodInfo toggleCommandMethod = MpMethodUtil.GetFirstMethodBySignature(toggleCommandType, argTypes);
            MethodInfo designatorMethod = MpMethodUtil.GetFirstMethodBySignature(designatorType, argTypes);

            //my decompiler shows the method name is <get_RightClickFloatMenuOptions>b__3_0 
            //while harmony saids it is <get_RightClickFloatMenuOptions>b__2_0, weird..."

            MP.RegisterSyncWorker<object>(SyncTypes, toggleCommandType);
            MP.RegisterSyncWorker<object>(SyncTypes, designatorType);

            MP.RegisterSyncMethod(builderCompType, "ToggleSkilled");
            MP.RegisterSyncMethod(toggleCommandMethod).SetContext(SyncContext.MapSelected);
            MP.RegisterSyncMethod(designatorMethod);
        
        }

        void SyncTypes(SyncWorker sync, ref object c)
        {
            if (sync.isWriting)
            {
                sync.Write(c.GetType());
            } else
            {
                c = Activator.CreateInstance(sync.Read<Type>());
            }
        }
    }
}
