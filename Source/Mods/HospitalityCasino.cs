using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>
    /// Hospitality: Casino by Adamas
    /// </summary>
    /// <see href="https://github.com/tomvd/HospitalityCasino"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2939292644"/>
    [MpCompatFor("Adamas.HospitalityCasino")]
    public class HospitalityCasino
    {
        public HospitalityCasino(ModContentPack mod)
        {
            InitializeGizmos("HospitalityCasino");

            // The method is called from SlotMachineComp.CompTick and calls ThingMaker.MakeThing.
            // The method itself is only called once (sets initialized to true and skips execution
            // if true). Due to the method making a thing during ticking, but max only once per
            // game launch, it'll cause issues with MP when someone joins a game with already built
            // slot machines. Making the thing will use up thing IDs in Thing.PostMake (and may
            // potentially result in RNG calls, but doesn't seem like it's the case this time).
            // Anyway, just ensure the textures are initialized on game launch instead of when
            // playing the game to prevent issues (seems like making them during startup is safe).
            LongEventHandler.ExecuteWhenFinished(
                () => AccessTools.DeclaredMethod("HospitalityCasino.HospitalityCasinoMod:InitialiseTextures")
                    .Invoke(null, Array.Empty<object>()));
        }

        public static void InitializeGizmos(string targetNamespace)
        {
            var type = AccessTools.TypeByName($"{targetNamespace}.CompVendingMachine");

            // Gizmos
            MP.RegisterSyncMethod(type, "ToggleActive");
            MP.RegisterSyncMethod(type, "SetPricing");
            // The setter is never used, but may as well sync it in case it's ever used in the future.
            MP.RegisterSyncMethod(type, "Pricing");
        }
    }
}