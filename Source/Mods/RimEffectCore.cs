using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Rim-Effect: Core by Vanilla Expanded Team and Co.</summary>
    /// <see href="https://github.com/AndroidQuazar/RimEffect-Core"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2479560240"/>
    [MpCompatFor("RimEffect.Core")]
    public class RimEffectCore
    {
        private static ConstructorInfo doorValueCtor;
        private static FieldInfo doorValueKeyField;

        public RimEffectCore(ModContentPack mod)
        {
            // May need patching:
            // AddHumanlikeOrders_Patch - free hostages
            // RimEffect.CompSpawnOnInteract - open

            MpCompat.RegisterLambdaMethod("RimEffect.AmmoBelt", "GetWornGizmos", 1);

            var autoDoorType = AccessTools.TypeByName("RimEffect.Building_AutoDoorLockable");
            var method = MpMethodUtil.GetLambda(autoDoorType, "GetGizmos", lambdaOrdinal: 1);
            var field = AccessTools.Field(method.DeclaringType, "doorState");
            var doorValueType = field.FieldType;
            var genericTypes = doorValueType.GetGenericArguments();
            doorValueCtor = AccessTools.DeclaredConstructor(doorValueType, new[] { genericTypes[0], genericTypes[1] });
            doorValueKeyField = AccessTools.Field(doorValueType, "key");

            MP.RegisterSyncDelegate(autoDoorType, method.DeclaringType.Name, method.Name);
            MP.RegisterSyncWorker<object>(SyncDoorValue, doorValueType);
        }

        private static void SyncDoorValue(SyncWorker sync, ref object doorValue)
        {
            if (sync.isWriting)
                sync.Write((int)doorValueKeyField.GetValue(doorValue));
            else
                doorValue = doorValueCtor.Invoke(new object[] { sync.Read<int>(), string.Empty });
        }
    }
}