using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Time of day switches by merthsoft</summary>
    /// <see href="https://github.com/merthsoft/time-of-day-switch"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=776114412"/>
    [MpCompatFor("Merthsoft.TimeOfDaySwitch")]
    class TimeOfDAySwitch
    {
        // Temporary states. When pasting will temporarily store synced states,
        // when executing the paste method it will temporarily store current
        // player's copied state, and restore it after executing.
        private static bool[] tempState;
        private static AccessTools.FieldRef<bool[]> copiedStatesField;

        public TimeOfDAySwitch(ModContentPack mod)
        {
            var type = AccessTools.TypeByName("Merthsoft.TimerSwitches.TimeOfDaySwitch");
            MP.RegisterSyncMethod(type, "SetState");
            // Pasting requires us to also sync the copied state which will be pasted,
            // so we need to transform the target to sync it as well.
            MP.RegisterSyncMethod(type, "Paste")
                .SetPreInvoke(PrePaste)
                .SetPostInvoke(PostPaste)
                .TransformTarget(Serializer.New(
                    (Building_PowerSwitch building) => (building, states: copiedStatesField()),
                    tuple =>
                    {
                        tempState = tuple.states;
                        return tuple.building;
                    }), true);

            // Field ref for accessing the copied state, we'll need to change those when copy/pasting stuff.
            copiedStatesField = AccessTools.StaticFieldRefAccess<bool[]>(
                AccessTools.DeclaredField("Merthsoft.TimerSwitches.ClipBoard:states"));
        }

        private static void PrePaste(object instance, object[] args)
        {
            ref var state = ref copiedStatesField();
            (state, tempState) = (tempState, state);
        }

        private static void PostPaste(object instance, object[] args)
        {
            copiedStatesField() = tempState;
            tempState = null;
        }
    }
}
