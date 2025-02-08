using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Recycling Expanded by OskarPotocki, Sarg Bjornson, xrushha</summary>
[MpCompatFor("VanillaExpanded.Recycling")]
public class VanillaRecyclingExpanded
{
    public VanillaRecyclingExpanded(ModContentPack mod)
    {
        MpCompatPatchLoader.LoadPatch(this);

        var type = AccessTools.TypeByName("VanillaRecyclingExpanded.CompBiopackDissolution");
        // Dev: Single dissolution event (0), dissolution events until destroyed (1), +25% dissolution progress (2)
        MpCompat.RegisterLambdaMethod(type, nameof(ThingComp.CompGetGizmosExtra), 0, 1, 2).SetDebugOnly();
        // Dev: Set next dissolve time
        MP.RegisterSyncDelegateLambda(type, nameof(ThingComp.CompGetGizmosExtra), 4).SetDebugOnly();

        type = AccessTools.TypeByName("VanillaRecyclingExpanded.CompSuperSimpleProcessor");
        MP.RegisterSyncMethod(type, "EjectContents");
        // In interface only called as dev mode command
        MP.RegisterSyncMethod(type, "DoAtomize").SetDebugOnly();
        // Toggle auto load
        MP.RegisterSyncMethodLambda(type, nameof(ThingComp.CompGetGizmosExtra), 1);
        // Dev: Set next processing time
        MP.RegisterSyncDelegateLambda(type, nameof(ThingComp.CompGetGizmosExtra), 3).SetDebugOnly();
    }

    // Stop methods from running (most likely after syncing) when it would cause errors or other issues.
    [MpCompatPrefix("VanillaRecyclingExpanded.CompSuperSimpleProcessor", "DoAtomize")]
    private static bool PreDoAtomize(CompThingContainer __instance) => __instance.ContainedThing is { stackCount: > 0 };
}