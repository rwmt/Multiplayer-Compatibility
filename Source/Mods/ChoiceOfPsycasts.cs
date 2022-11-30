using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Choice Of Psycasts by Azuraal</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2293460251"/>
    [MpCompatFor("azuraal.choiceofpsycasts")]
    public class ChoiceOfPsycastsCompat
    {
        static Type learnPsycastsType;
        static AccessTools.FieldRef<object, int> levelField;
        static AccessTools.FieldRef<object, Pawn> parentField;
        
        public ChoiceOfPsycastsCompat(ModContentPack mod)
        {
            var type = learnPsycastsType = AccessTools.TypeByName("ChoiceOfPsycasts.LearnPsycasts");

            MpCompat.RegisterLambdaDelegate(type, "Choice", 1);
            MpCompat.RegisterLambdaDelegate(type, "ChoiceCustom", 1);

            levelField = AccessTools.FieldRefAccess<int>(type, "Level");
            parentField = AccessTools.FieldRefAccess<Pawn>(type, "Parent");
            MP.RegisterSyncWorker<Command_Action>(SyncLearnPsycasts, type);
        }

        static void SyncLearnPsycasts(SyncWorker sync, ref Command_Action command)
        {
            if (sync.isWriting) {
                sync.Write(levelField(command));
                sync.Write(parentField(command));
            } else {
                command = (Command_Action) Activator.CreateInstance(learnPsycastsType, sync.Read<int>(), sync.Read<Pawn>());
            }
        }
    }    
}
