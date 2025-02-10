using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Adaptive Storage Framework by Soul, Phaneron, Bradson</summary>
/// <see href="https://github.com/bbradson/Adaptive-Storage-Framework"/>
/// <see href="https://steamcommunity.com/workshop/filedetails/?id=3033901359"/>
[MpCompatFor("adaptive.storage.framework")]
public class AdaptiveStorageFramework
{
    #region Fields

    private static FastInvokeHandler thingClassAnyFreeSlotsMethod;

    #endregion

    #region Main patch

    public AdaptiveStorageFramework(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

    private static void LatePatch()
    {
        MpCompatPatchLoader.LoadPatch<AdaptiveStorageFramework>();

        MP.RegisterSyncMethod(AccessTools.DeclaredMethod("AdaptiveStorage.ContentsITab:OnDropThing"))
            .SetContext(SyncContext.MapSelected)
            .CancelIfAnyArgNull()
            .CancelIfNoSelectedMapObjects();

        var type = AccessTools.TypeByName("AdaptiveStorage.ThingClass");
        thingClassAnyFreeSlotsMethod = MethodInvoker.GetHandler(AccessTools.DeclaredPropertyGetter(type, "AnyFreeSlots"));

        var inner = AccessTools.Inner(type, "GodModeGizmos");
        // Dev: Add stack of random items allowed by storage.
        var method = MpMethodUtil.GetLambda(inner, null, MethodType.Constructor, [type], 0);
        MP.RegisterSyncDelegate(inner, method.DeclaringType!.Name, method.Name, null).SetDebugOnly();
        MpCompat.harmony.Patch(method, prefix: new HarmonyMethod(CancelExecutionIfFull));

        // The other 2 dev gizmos are likely not needed to be synced, or too much effort.
        // The first one opens a dev mode window to edit def of the selected object.
        // Too much effort for something that a casual user shouldn't really use.
        // The second one updates the graphics of the selected objects, which well...
        // Is more of a client-only interaction, I believe. No point syncing it.
    }

    #endregion

    #region Harmony patches

    [MpCompatPrefix("AdaptiveStorage.ContentsITab", "OnDropThing")]
    private static bool CancelExecutionIfNotContained(ITab_ContentsBase __instance, Thing __0, ref int __1)
    {
        // If the "drop" button was pressed multiple times before the execution
        // was synced, the thing won't be contained in the container on repeat
        // calls, causing an error. By checking if the container contains it
        // we prevent errors.
        if (!__instance.container.Contains(__0))
            return false;

        // If we've synced multiple commands to drop a specific count the stack
        // may not have enough on repeat calls, causing errors. Ensure that
        // the drop count isn't bigger than stack count.
        if (__0.stackCount > __1)
            __1 = __0.stackCount;
        return true;
    }

    // Can't use MpCompatPrefix, can't reference the type directly.
    // Would need to update those attributes to support string types
    // as arguments, or a mix of strings/types.
    private static bool CancelExecutionIfFull(Building_Storage ___Parent)
    {
        // The mod only displays the gizmo if there's any slots free,
        // but there's no checks like that on the method itself.
        // If we sync multiple commands while almost full it'll
        // attempt to spawn items into a full container, causing errors.
        // Prevent it by adding a check for any free slots.
        return (bool)thingClassAnyFreeSlotsMethod(___Parent);
    }

    #endregion

    #region Sync workers

    [MpCompatSyncWorker("AdaptiveStorage.ContentsITab", shouldConstruct = true)]
    private static void NoSync(SyncWorker sync, ref object obj)
    {
        // Don't sync, only construct
    }

    #endregion
}