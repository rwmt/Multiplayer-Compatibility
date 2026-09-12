using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Radius UI - Colonist Bar by Astryl</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3788278207"/>
    [MpCompatFor("astryl.radiusui.colonistbar")]
    internal class RadiusUIColonistBar
    {
        private static readonly Dictionary<string, Graphic> graphicCache = new();

        private static bool patched;

        public RadiusUIColonistBar(ModContentPack mod)
        {
            TryPatch();
            LongEventHandler.ExecuteWhenFinished(TryPatch);
        }

        private static void TryPatch()
        {
            if (patched)
                return;

            var graphicForMethod = AccessTools.Method("RadiusColonistBar.KitPreview:GraphicFor");
            if (graphicForMethod != null)
            {
                MpCompat.harmony.Patch(
                    graphicForMethod,
                    prefix: new HarmonyMethod(typeof(RadiusUIColonistBar), nameof(PrefixGraphicFor)));
                patched = true;
            }
        }

        private static bool PrefixGraphicFor(ThingDef def, BodyTypeDef bt, ref Graphic __result)
        {
            if (def == null || bt == null)
            {
                __result = null;
                return false;
            }

            string key = def.defName + "|" + bt.defName;
            if (graphicCache.TryGetValue(key, out var cached))
            {
                __result = cached;
                return false;
            }

            Graphic graphic = null;
            try
            {
                ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                var apparel = (Apparel)Activator.CreateInstance(def.thingClass);
                apparel.def = def;
                apparel.SetStuffDirect(stuff);

                if (ApparelGraphicRecordGetter.TryGetGraphicApparel(apparel, bt, false, out var rec))
                {
                    graphic = rec.graphic;
                }
            }
            catch
            {
            }

            graphicCache[key] = graphic;
            __result = graphic;
            return false;
        }
    }
}
