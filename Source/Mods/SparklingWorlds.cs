using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat.Mods
{
    /// <summary>PowerSwitch by Haplo</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=717632155"/>
    /// <see href="https://github.com/HaploX1/RimWorld-PowerSwitch"/>
    [MpCompatFor("Albion.SparklingWorlds.Full")]
    //[MpCompatFor("Albion.SparklingWorlds.Core")]
    public class SparklingWorlds
    {
        public SparklingWorlds(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(() => MP.RegisterSyncMethod(AccessTools.TypeByName("SparklingWorlds.CompOrbitalLaunchSW"), "TryLaunch"));
        }
    }
}
