using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat;

/// <summary>Vanilla Races Expanded - Insector by Oskar Potocki, Taranchuk, Sarg</summary>
/// <see href="https://github.com/Vanilla-Expanded/VanillaRacesExpanded-Insector"/>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3260509684"/>
[MpCompatFor("vanillaracesexpanded.insector")]
public class VanillaRacesInsector
{
    #region Fields

    // GameComponent_Genelines
    private static AccessTools.FieldRef<GameComponent> genelinesInstanceField;
    private static AccessTools.FieldRef<GameComponent, IList> genelinesListField;
    private static FastInvokeHandler createGenelineMethod;
    private static FastInvokeHandler addGenelineMethod;

    // Geneline
    private static AccessTools.FieldRef<object, int> genelineIdField;
    private static FastInvokeHandler editGenelineMethod;

    #endregion

    #region Main patch

    public VanillaRacesInsector(ModContentPack mod)
    {
        MpCompatPatchLoader.LoadPatch(this);

        // A few methods aren't synced, as they call TryTakeOrderedJob - general MP syncing handles those

        #region Genelines

        {
            var type = AccessTools.TypeByName("VanillaRacesExpandedInsector.GameComponent_Genelines");
            // Setup field refs
            genelinesInstanceField = AccessTools.StaticFieldRefAccess<GameComponent>(AccessTools.DeclaredField(type, "Instance"));
            genelinesListField = AccessTools.FieldRefAccess<IList>(type, "genelines");
            // Setup methods
            createGenelineMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "CreateGeneline"));
            addGenelineMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "AddGeneline"));
            // Remove geneline and all pawns that have it.
            MP.RegisterSyncMethod(type, "DeleteGeneline");

            type = AccessTools.TypeByName("VanillaRacesExpandedInsector.Geneline");
            // Setup field refs
            genelineIdField = AccessTools.FieldRefAccess<int>(type, "id");
            // Setup methods
            editGenelineMethod = MethodInvoker.GetHandler(AccessTools.DeclaredMethod(type, "EditGeneline"));
            // Change a pawn's geneline and turn them into a metapod
            MP.RegisterSyncMethod(type, "AddPawnWithMetapod");

            // Metapod gizmos
            MP.RegisterSyncMethodLambda(AccessTools.TypeByName("VanillaRacesExpandedInsector.Metapod"), nameof(Thing.GetGizmos), 0).SetDebugOnly();
        }

        #endregion

        #region Pregnancy

        {
            var types = new[]
            {
                "VanillaRacesExpandedInsector.HediffComp_ChestburstPregnancyVictim",
                "VanillaRacesExpandedInsector.HediffComp_ChestburstPregnancyVictimHidden",
            };

            // Dev progress pregnancy
            foreach (var typeName in types)
                MP.RegisterSyncMethodLambda(AccessTools.TypeByName(typeName), nameof(HediffComp.CompGetGizmos), 0).SetDebugOnly();

            // Self impregnate
            MP.RegisterSyncMethod(AccessTools.DeclaredMethod("VanillaRacesExpandedInsector.HediffComp_Parthenogenesis:TryParthenogenesis"));
        }

        #endregion

        #region RNG

        {
            // RNG calls after ShouldSpawnMotesAt
            PatchingUtilities.PatchPushPopRand("VanillaRacesExpandedInsector.IncomingSmoker:ThrowBlackSmoke");
        }

        #endregion
    }

    #endregion

    #region Sync Workers

    [MpCompatSyncWorker("VanillaRacesExpandedInsector.Geneline")]
    private static void SyncGeneline(SyncWorker sync, ref object geneline)
    {
        if (sync.isWriting)
        {
            if (geneline != null)
                sync.Write(genelineIdField(geneline));
            else
                sync.Write(-1);
        }
        else
        {
            var id = sync.Read<int>();
            if (id < 0)
                return;

            foreach (var g in genelinesListField(genelinesInstanceField()))
            {
                if (genelineIdField(g) == id)
                {
                    geneline = g;
                    break;
                }
            }
        }
    }

    #endregion

    #region Genelines

    [MpCompatSyncMethod]
    private static void SyncedCreateGeneline(List<GeneDef> selectedGenes, string genelineName)
    {
        var genelineGameComp = genelinesInstanceField();
        var geneline = createGenelineMethod(genelineGameComp);
        editGenelineMethod(geneline, selectedGenes, genelineName);
        addGenelineMethod(genelineGameComp, geneline);
    }

    [MpCompatSyncMethod]
    private static void SyncedEditGeneline(int genelineId, List<GeneDef> selectedGenes, string genelineName)
    {
        foreach (var g in genelinesListField(genelinesInstanceField()))
        {
            if (genelineId == genelineIdField(g))
            {
                editGenelineMethod(g, selectedGenes, genelineName);
                return;
            }
        }

        Log.Warning($"Trying to modify geneline with ID {genelineId}, but it did not exist. Was it removed?");
    }

    [MpCompatPrefix("VanillaRacesExpandedInsector.Window_EditGeneline", "Accept")]
    private static bool PreAcceptEditGeneline(Window __instance, Action ___callback, object ___geneline, IList ___selectedGenes, string ___genelineName)
    {
        if (!MP.IsInMultiplayer)
            return true;

        var genes = ___selectedGenes.Cast<GeneDef>().ToList();

        // No callback, the geneline is being modified
        if (___callback == null)
            SyncedEditGeneline(genelineIdField(___geneline), genes, ___genelineName);
        // Callback, the geneline is being created
        else
            SyncedCreateGeneline(genes, ___genelineName);

        __instance.Close();
        return false;
    }

    #endregion
}