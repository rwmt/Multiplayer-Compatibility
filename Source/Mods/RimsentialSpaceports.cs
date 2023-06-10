using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Rimsential - Spaceports by SomewhereOutInSpace</summary>
    /// <see href="https://github.com/SomewhereOutInSpace/Rimworld-Spaceports"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2663999215"/>
    [MpCompatFor("SomewhereOutInSpace.Spaceports")]
    internal class RimsentialSpaceports
    {
        public RimsentialSpaceports(ModContentPack mod)
        {
            // Gizmos
            {
                var type = AccessTools.TypeByName("Spaceports.Buildings.Building_Beacon");
                // Toggle forced lockdown, dismiss all, recall all
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 1, 2, 3);
                MP.RegisterSyncMethod(type, "ConfirmAction");

                type = AccessTools.TypeByName("Spaceports.Buildings.Building_Shuttle");
                // Force immediate departure, recall party
                MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1);

                type = AccessTools.TypeByName("Spaceports.Buildings.Building_ShuttlePad");
                MP.RegisterSyncMethod(type, "SetAccessState");

                type = AccessTools.TypeByName("Spaceports.Buildings.Building_ShuttleSpot");
                MP.RegisterSyncMethod(type, "SetAccessState");
            }

            // Choice letters
            {
                var type = AccessTools.TypeByName("Multiplayer.Client.Patches.CloseDialogsForExpiredLetters");
                // We should probably add this to the API the next time we update it
                var registerAction = AccessTools.DeclaredMethod(type, "RegisterDefaultLetterChoice");

                type = AccessTools.TypeByName("Spaceports.Letters.PrisonerTransferLetter");
                var methods = MpMethodUtil.GetLambda(type, "Choices", MethodType.Getter, null, 0, 1, 2).ToArray();
                MP.RegisterSyncMethod(methods[0]);
                MP.RegisterSyncMethod(methods[1]);
                MP.RegisterSyncMethod(methods[2]);
                registerAction.Invoke(null, new object[] { methods[2], type });

                var typeNames = new[]
                {
                    "Spaceports.Letters.InterstellarDerelictLetter",
                    "Spaceports.Letters.MedevacLetter",
                    "Spaceports.Letters.MysteryCargoLetter",
                    "Spaceports.Letters.SpicyPawnLendingLetter",
                };

                foreach (var typeName in typeNames)
                {
                    type = AccessTools.TypeByName(typeName);
                    methods = MpMethodUtil.GetLambda(type, "Choices", MethodType.Getter, null, 0, 1).ToArray();
                    MP.RegisterSyncMethod(methods[0]);
                    MP.RegisterSyncMethod(methods[1]);
                    registerAction.Invoke(null, new object[] { methods[1], type });
                }
            }
        }
    }
}
