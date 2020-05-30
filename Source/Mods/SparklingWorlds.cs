using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat.Mods
{
    /// <summary>PowerSwitch by Albion</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1123043922"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1195241161"/>
    [MpCompatFor("Albion.SparklingWorlds.Full")]
    [MpCompatFor("Albion.SparklingWorlds.Core")]
    public class SparklingWorlds
    {
        public SparklingWorlds(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(() => MP.RegisterSyncMethod(AccessTools.TypeByName("SparklingWorlds.CompOrbitalLaunchSW"), "TryLaunch"));
        }
    }
}
