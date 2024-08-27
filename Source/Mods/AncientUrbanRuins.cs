using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Ancient urban ruins by MO</summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3316062206"/>
[MpCompatFor("XMB.AncientUrbanrUins.MO")]
public class AncientUrbanRuins
{
    #region Main patch

    public AncientUrbanRuins(ModContentPack mod)
    {
        // Mod uses 3 different assemblies, 2 of them use the same namespace.

        MpCompatPatchLoader.LoadPatch(this);

        #region RNG

        {
            var methods = new[]
            {
                "AncientMarket_Libraray.ACM_RandomUtility:ReplaceWallAndBreakThem",
                "AncientMarket_Libraray.ACM_SketchResolver_ACM_Resturant:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_BuildingRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_BuildingRoomWithNoForklift:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_ContainerRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_HoboRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_StoreRoom:ResolveInt",
                "AncientMarket_Libraray.ACM_SketchResolver_UnderRoom:ResolveInt",
                "AncientMarket_Libraray.ComplexThreatWorker_HangingPirates:SpawnThreatPawns",
            };

            PatchingUtilities.PatchUnityRand(methods);
        }

        #endregion

        #region Input

        {
            // Start trade
            MpCompat.RegisterLambdaDelegate("AncientMarket_Libraray.BuildingTrader", nameof(Thing.GetFloatMenuOptions), 0);
            // Start dialogue (seems unused/related feature is unfinished)
            MpCompat.RegisterLambdaDelegate("AncientMarket_Libraray.CompDialogable", nameof(ThingComp.CompFloatMenuOptions), 0);
            // Destroy site
            LongEventHandler.ExecuteWhenFinished(() =>
                MpCompat.RegisterLambdaMethod("AncientMarket_Libraray.CustomSite", nameof(WorldObject.GetGizmos), 1));
        }

        #endregion
    }

    #endregion

    #region Destroy site confirmation dialog

    // If multiple players have the dialog open, close the dialog if one of
    // the players confirmed it as there's no point in having it open.
    // For some reason, this mod uses 2 different confirmation dialogs for
    // different sites, the vanilla confirmation dialog and their own custom one.

    [MpCompatPrefix("AncientMarket_Libraray.Dialog_Confirm", "DoWindowContents")]
    private static void CloseDialogIfSiteDestroyed(Window __instance, Site ___Site)
    {
        if (___Site == null || ___Site.Destroyed)
            __instance.Close();
    }

    // AncientMarket_Libraray.Dialog_Confirm calls WorldObject.Destroy. We could
    // sync that method, but I'd prefer not to sync Vanilla methods

    [MpCompatTranspiler("AncientMarket_Libraray.Dialog_Confirm", "DoWindowContents")]
    private static IEnumerable<CodeInstruction> ReplaceDestroyWithSyncedCall(IEnumerable<CodeInstruction> instr)
    {
        var target = AccessTools.DeclaredMethod(typeof(WorldObject), nameof(WorldObject.Destroy));
        var replacement = MpMethodUtil.MethodOf(SyncedDestroySite);

        foreach (var ci in instr)
        {
            if (ci.Calls(target))
            {
                ci.opcode = OpCodes.Call;
                ci.operand = replacement;
            }

            yield return ci;
        }
    }

    [MpCompatSyncMethod]
    private static void SyncedDestroySite(WorldObject site)
    {
        // Make sure it's not already destroyed to prevent errors
        if (!site.Destroyed)
            site.Destroy();
    }

    #endregion
}