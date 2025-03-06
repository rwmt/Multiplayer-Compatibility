using HarmonyLib;
using Multiplayer.API;
using Verse;
using System.Reflection;
using System;
namespace Multiplayer.Compat
{
    /// <summary>Eccentric Extras - Flares by Aelanna</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2552628275"/>
    [MpCompatFor("Aelanna.EccentricTech.Flares2")]
    internal class EccentricTechFlares
    {

        private static ConstructorInfo commandThrowIlluminatorConstructor;
        private static Type compIlluminatorPackType;
        public EccentricTechFlares(ModContentPack mod)
        {
            // RNG
            {
            }

            // Gizmos
            {
                var commandThrowIlluminatorType = AccessTools.TypeByName("EccentricFlares.Command_ThrowIlluminator");
                compIlluminatorPackType = AccessTools.TypeByName("EccentricFlares.CompIlluminatorPack");

                commandThrowIlluminatorConstructor = AccessTools.DeclaredConstructor(commandThrowIlluminatorType, [compIlluminatorPackType]);

                MP.RegisterSyncWorker<object>(SyncCommandThrowIlluminator, commandThrowIlluminatorType);
                MP.RegisterSyncMethod(commandThrowIlluminatorType, "SelectOption");



                LongEventHandler.ExecuteWhenFinished(LatePatch);
            }

        }
        public static void LatePatch()
        {
            var type = AccessTools.TypeByName("EccentricFlares.CompIlluminatorPack");

            MP.RegisterSyncMethod(compIlluminatorPackType, "DoStartThrow");
            MpCompat.RegisterLambdaMethod(type, "CompGetWornGizmosExtra", 1);
        }

        private static void SyncCommandThrowIlluminator(SyncWorker sync, ref object command)
        {

            if (sync.isWriting)
            {
                Traverse traverse = Traverse.Create(command);
                ThingComp comp = traverse.Field("pack").GetValue<ThingComp>();
                sync.Write(comp);
            }
            else
            {
                command = commandThrowIlluminatorConstructor.Invoke([sync.Read<ThingComp>()]);
            }
        }
    }
}
