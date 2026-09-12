using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Multiplayer.Compat
{
    /// <summary>Radius UI - Quest Menu by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3786840944"/>
    [MpCompatFor("astryl.RadiusUI.QuestMenu")]
    internal class RadiusUIQuestMenu
    {
        private static ISyncField syncQuestDismissed;
        private static FastInvokeHandler chooseRewardMethod;
        private static MethodInfo chosenRewardIndexMethod;
        private static MethodInfo choicePartMethod;
        private static MethodInfo modelSetDirtyMethod;
        private static FieldInfo modelField;

        public RadiusUIQuestMenu(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        private static void LatePatch()
        {
            var syncFieldsType = AccessTools.TypeByName("Multiplayer.Client.SyncFields");
            syncQuestDismissed = (ISyncField)AccessTools.Field(syncFieldsType, "SyncQuestDismissed")?.GetValue(null);

            var patchQuestChoicesType = AccessTools.TypeByName("Multiplayer.Client.PatchQuestChoices");
            var chooseMethodInfo = AccessTools.Method(patchQuestChoicesType, "Choose");
            if (chooseMethodInfo != null)
                chooseRewardMethod = MethodInvoker.GetHandler(chooseMethodInfo);

            var questWindowType = AccessTools.TypeByName("RadiusUIQuestMenu.RadiusQuestWindow");
            var questModelType = AccessTools.TypeByName("RadiusUIQuestMenu.QuestModel");

            chosenRewardIndexMethod = AccessTools.Method(questWindowType, "ChosenRewardIndex");
            choicePartMethod = AccessTools.Method(questModelType, "ChoicePart");
            modelField = AccessTools.Field(questWindowType, "Model");
            modelSetDirtyMethod = AccessTools.Method(questModelType, "SetDirty");

            // Intercept DoAccept to ensure reward choices are synced via Multiplayer before accepting
            MpCompat.harmony.Patch(
                AccessTools.Method(questWindowType, "DoAccept", new[] { typeof(Quest), typeof(Pawn) }),
                prefix: new HarmonyMethod(typeof(RadiusUIQuestMenu), nameof(PrefixDoAccept)));

            // Watch quest dismissed toggle
            MpCompat.harmony.Patch(
                AccessTools.Method(questWindowType, "ToggleSetAside", new[] { typeof(Quest) }),
                prefix: new HarmonyMethod(typeof(RadiusUIQuestMenu), nameof(PrefixToggleSetAside)));
        }

        private static bool PrefixDoAccept(object __instance, Quest q, Pawn by)
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (choicePartMethod != null && chosenRewardIndexMethod != null && chooseRewardMethod != null)
            {
                var choicePart = choicePartMethod.Invoke(null, new object[] { q }) as QuestPart_Choice;
                if (choicePart != null && choicePart.choices.Count >= 2)
                {
                    int chosenIndex = (int)chosenRewardIndexMethod.Invoke(__instance, new object[] { q });
                    if (chosenIndex < 0)
                        return false;

                    // Sync choice selection through Multiplayer's registered sync method
                    chooseRewardMethod(null, choicePart, chosenIndex);
                }
            }

            SoundStarter.PlayOneShotOnCamera(SoundDefOf.Quest_Accepted, null);
            // Quest.Accept is already a registered sync method in Multiplayer
            q.Accept(by);

            syncQuestDismissed?.Watch(q);
            q.dismissed = false;

            if (modelField != null && modelSetDirtyMethod != null)
            {
                var model = modelField.GetValue(__instance);
                if (model != null)
                    modelSetDirtyMethod.Invoke(model, null);
            }

            return false;
        }

        private static void PrefixToggleSetAside(Quest q)
        {
            if (MP.IsInMultiplayer && q != null)
            {
                syncQuestDismissed?.Watch(q);
            }
        }
    }
}
