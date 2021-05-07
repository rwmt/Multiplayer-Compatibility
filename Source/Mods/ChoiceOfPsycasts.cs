using System;
using System.Reflection;
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
        static FieldInfo levelField, parentField;
        
        public ChoiceOfPsycastsCompat(ModContentPack mod)
        {
            var type = learnPsycastsType = AccessTools.TypeByName("RimWorld.ChoiceOfPsycasts.LearnPsycasts");
            
            MP.RegisterSyncDelegate(type, "<>c__DisplayClass3_0", "<Choice>b__0");

            levelField = AccessTools.Field(type, "Level");
            parentField = AccessTools.Field(type, "Parent");
            MP.RegisterSyncWorker<Command_Action>(SyncLearnPsycasts, type);
        }

        static void SyncLearnPsycasts(SyncWorker sync, ref Command_Action command)
        {
            if (sync.isWriting) {
                sync.Write((int)levelField.GetValue(command));
                sync.Write((Pawn)parentField.GetValue(command));
            } else {
                command = (Command_Action) Activator.CreateInstance(learnPsycastsType, new object[] {
                    sync.Read<int>(),
                    sync.Read<Pawn>()
                });
            }
        }
    }    
}
