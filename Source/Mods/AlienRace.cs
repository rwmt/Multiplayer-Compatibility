using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Humanoid Alien Races by erdelf</summary>
    /// <see href="https://github.com/RimWorld-CCL-Reborn/AlienRaces"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=839005762"/>
    [MpCompatFor("erdelf.HumanoidAlienRaces")]
    public class AlienRace
    {
        private static Type alienCompType;
        private static MethodInfo overwriteColorChannelMethod;
        private static FieldInfo originPawnField;
        private static FieldInfo addonVariantsField;
        private static FieldInfo addonColorsField;
        private static FieldInfo colorChannelsField;
        private static FieldInfo colorChannelLinksField;
        private static Type colorChannelLinkDataType;
        private static Type colorChannelLinkTargetDataType;
        private static FieldInfo linkOriginalChannelField;
        private static FieldInfo linkTargetsOneField;
        private static FieldInfo linkTargetsTwoField;
        private static FieldInfo linkTargetChannelField;
        private static FieldInfo linkTargetCategoryIndexField;

        public AlienRace(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        private static void LatePatch()
        {
            alienCompType = AccessTools.TypeByName("AlienRace.AlienPartGenerator+AlienComp");
            if (alienCompType == null) return;

            overwriteColorChannelMethod = AccessTools.Method(alienCompType, "OverwriteColorChannel", new[] { typeof(string), typeof(Color), typeof(Color) });
            originPawnField             = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_DummyPawn"), "origPawn");
            addonVariantsField          = AccessTools.Field(alienCompType, "addonVariants");
            addonColorsField            = AccessTools.Field(alienCompType, "addonColors");
            colorChannelsField          = AccessTools.Field(alienCompType, "colorChannels");
            colorChannelLinksField      = AccessTools.Field(alienCompType, "colorChannelLinks");

            colorChannelLinkDataType       = AccessTools.TypeByName("AlienRace.AlienPartGenerator+AlienComp+ColorChannelLinkData");
            colorChannelLinkTargetDataType = AccessTools.TypeByName("AlienRace.AlienPartGenerator+AlienComp+ColorChannelLinkData+ColorChannelLinkTargetData");
            linkOriginalChannelField       = AccessTools.Field(colorChannelLinkDataType, "originalChannel");
            linkTargetsOneField            = AccessTools.Field(colorChannelLinkDataType, "targetsChannelOne");
            linkTargetsTwoField            = AccessTools.Field(colorChannelLinkDataType, "targetsChannelTwo");
            linkTargetChannelField         = AccessTools.Field(colorChannelLinkTargetDataType, "targetChannel");
            linkTargetCategoryIndexField   = AccessTools.Field(colorChannelLinkTargetDataType, "categoryIndex");

            MpCompat.harmony.Patch(
                AccessTools.Constructor(typeof(Dialog_StylingStation), [typeof(Pawn), typeof(Thing)]),
                prefix: new HarmonyMethod(typeof(AlienRace), nameof(StylingStationCtorPrefix)));

            var onAccept = AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_HandleAccept"), "OnAccept");
            MpCompat.harmony.Patch(onAccept,
                prefix: new HarmonyMethod(typeof(AlienRace), nameof(OnAcceptPrefix)),
                postfix: new HarmonyMethod(typeof(AlienRace), nameof(OnAcceptPostfix)));

            MP.RegisterSyncWorker<AlienCompData>(SyncAlienCompData);
            MP.RegisterSyncMethod(typeof(AlienRace), nameof(SyncAlienData));

        }

        static void StylingStationCtorPrefix(Pawn pawn)
        {
            if (pawn.AllComps != null && pawn.AllComps.Any(c => c.GetType() == alienCompType)) return;
            pawn.InitializeComps();

            var dummyComp = pawn.AllComps?.FirstOrDefault(c => c.GetType() == alienCompType);
            if (dummyComp == null) return;

            if (originPawnField?.GetValue(pawn) is Pawn originPawn)
            {
                var origComp = originPawn.AllComps?.FirstOrDefault(c => c.GetType() == alienCompType);
                if (origComp != null)
                    // Directly copy fields — CopyAlienData returns early if renderTree unresolved on dummy pawn
                    DirectCopyAlienComp(origComp, dummyComp);
            }
        }

        static void DirectCopyAlienComp(object src, object dst)
        {
            // Set colorChannels field directly to bypass lazy-init random generation
            var srcChannels = (IDictionary)colorChannelsField.GetValue(src);
            if (srcChannels != null)
            {
                var dstChannels = (IDictionary)Activator.CreateInstance(colorChannelsField.FieldType);
                foreach (DictionaryEntry entry in srcChannels)
                {
                    var t = entry.Value;
                    var f = (Color)t.GetType().GetField("first").GetValue(t);
                    var s = (Color)t.GetType().GetField("second").GetValue(t);
                    dstChannels[entry.Key] = Activator.CreateInstance(t.GetType(), f, s);
                }
                colorChannelsField.SetValue(dst, dstChannels);
            }

            // Copy addonVariants
            var variants = addonVariantsField.GetValue(src) as IEnumerable<int>;
            addonVariantsField.SetValue(dst, variants?.ToList() ?? new List<int>());

            // Copy addonColors
            var srcColors = addonColorsField.GetValue(src) as IEnumerable;
            if (srcColors != null)
            {
                var dstColors = (IList)Activator.CreateInstance(addonColorsField.FieldType);
                foreach (var item in srcColors)
                {
                    var f = (Color?)item.GetType().GetField("first").GetValue(item);
                    var s = (Color?)item.GetType().GetField("second").GetValue(item);
                    dstColors.Add(CreateNullableColorTuple(f, s));
                }
                addonColorsField.SetValue(dst, dstColors);
            }
        }

        // Captured before dialog closes
        private static (Pawn origPawn, AlienCompData data)? pendingAlienSync;

        static void OnAcceptPrefix()
        {
            pendingAlienSync = null;
            var dialog = Find.WindowStack.WindowOfType<Dialog_StylingStation>();
            if (dialog?.pawn == null) return;

            var dummyComp = dialog.pawn.AllComps?.FirstOrDefault(c => c.GetType() == alienCompType);
            if (dummyComp == null) return;

            if (originPawnField?.GetValue(dialog.pawn) is not Pawn origPawn) return;

            pendingAlienSync = (origPawn, SnapshotAlienComp(dummyComp));
        }

        static void OnAcceptPostfix()
        {
            if (pendingAlienSync is var (origPawn, data))
                SyncAlienData(origPawn, data);
            pendingAlienSync = null;
        }

        static void SyncAlienData(Pawn pawn, AlienCompData data)
        {
            ApplyAlienCompData(pawn, data);
        }

        static void ApplyAlienCompData(Pawn pawn, AlienCompData data)
        {
            var comp = pawn.AllComps?.FirstOrDefault(c => c.GetType() == alienCompType);
            if (comp == null) return;

            foreach (var kv in data.colorChannels)
                overwriteColorChannelMethod.Invoke(comp, [kv.Key, kv.Value.Item1, kv.Value.Item2]);

            addonVariantsField.SetValue(comp, data.addonVariants);

            var addonColorsList = (IList)Activator.CreateInstance(addonColorsField.FieldType);
            foreach (var (f, s) in data.addonColors)
                addonColorsList.Add(CreateNullableColorTuple(f, s));
            addonColorsField.SetValue(comp, addonColorsList);

            var links = (IDictionary)Activator.CreateInstance(colorChannelLinksField.FieldType);
            foreach (var kv in data.colorChannelLinks)
            {
                var linkData = Activator.CreateInstance(colorChannelLinkDataType);
                linkOriginalChannelField.SetValue(linkData, kv.Key);
                var t1 = (ICollection)linkTargetsOneField.GetValue(linkData);
                var t2 = (ICollection)linkTargetsTwoField.GetValue(linkData);
                foreach (var (ch, idx) in kv.Value.targetsOne) AddToCollection(t1, ch, idx);
                foreach (var (ch, idx) in kv.Value.targetsTwo) AddToCollection(t2, ch, idx);
                links[kv.Key] = linkData;
            }
            colorChannelLinksField.SetValue(comp, links);

            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        static void AddToCollection(ICollection col, string channel, int categoryIndex)
        {
            var item = Activator.CreateInstance(colorChannelLinkTargetDataType);
            linkTargetChannelField.SetValue(item, channel);
            linkTargetCategoryIndexField.SetValue(item, categoryIndex);
            col.GetType().GetMethod("Add").Invoke(col, [item]);
        }

        static object CreateNullableColorTuple(Color? first, Color? second)
        {
            var tupleType = addonColorsField.FieldType.GetGenericArguments()[0];
            return Activator.CreateInstance(tupleType, first, second);
        }

        static AlienCompData SnapshotAlienComp(object comp)
        {
            var data = new AlienCompData();

            var channels = (IDictionary)colorChannelsField.GetValue(comp);
            if (channels != null)
                foreach (DictionaryEntry entry in channels)
                {
                    var t = entry.Value;
                    var f = (Color)t.GetType().GetField("first").GetValue(t);
                    var s = (Color)t.GetType().GetField("second").GetValue(t);
                    data.colorChannels[(string)entry.Key] = (f, s);
                }

            data.addonVariants = (addonVariantsField.GetValue(comp) as IEnumerable<int>)?.ToList() ?? new List<int>();

            var addonColors = addonColorsField.GetValue(comp) as IEnumerable;
            if (addonColors != null)
                foreach (var item in addonColors)
                {
                    var f = (Color?)item.GetType().GetField("first").GetValue(item);
                    var s = (Color?)item.GetType().GetField("second").GetValue(item);
                    data.addonColors.Add((f, s));
                }

            var links = (IDictionary)colorChannelLinksField.GetValue(comp);
            if (links != null)
                foreach (DictionaryEntry entry in links)
                {
                    var ld = new AlienCompData.LinkData();
                    foreach (var t in (IEnumerable)linkTargetsOneField.GetValue(entry.Value))
                        ld.targetsOne.Add(((string)linkTargetChannelField.GetValue(t), (int)linkTargetCategoryIndexField.GetValue(t)));
                    foreach (var t in (IEnumerable)linkTargetsTwoField.GetValue(entry.Value))
                        ld.targetsTwo.Add(((string)linkTargetChannelField.GetValue(t), (int)linkTargetCategoryIndexField.GetValue(t)));
                    data.colorChannelLinks[(string)entry.Key] = ld;
                }

            return data;
        }

        static void SyncAlienCompData(SyncWorker sync, ref AlienCompData data)
        {
            if (sync.isWriting)
            {
                // colorChannels
                sync.Write(data.colorChannels.Count);
                foreach (var kv in data.colorChannels)
                {
                    sync.Write(kv.Key);
                    sync.Write(kv.Value.Item1);
                    sync.Write(kv.Value.Item2);
                }
                // addonVariants
                sync.Write(data.addonVariants.Count);
                foreach (var v in data.addonVariants)
                    sync.Write(v);
                // addonColors
                sync.Write(data.addonColors.Count);
                foreach (var (f, s) in data.addonColors)
                {
                    sync.Write(f.HasValue);
                    if (f.HasValue) sync.Write(f.Value);
                    sync.Write(s.HasValue);
                    if (s.HasValue) sync.Write(s.Value);
                }
                // colorChannelLinks
                sync.Write(data.colorChannelLinks.Count);
                foreach (var kv in data.colorChannelLinks)
                {
                    sync.Write(kv.Key);
                    WriteTargets(sync, kv.Value.targetsOne);
                    WriteTargets(sync, kv.Value.targetsTwo);
                }
            }
            else
            {
                data = new AlienCompData();
                // colorChannels
                int count = sync.Read<int>();
                for (int i = 0; i < count; i++)
                {
                    var key = sync.Read<string>();
                    var f   = sync.Read<Color>();
                    var s   = sync.Read<Color>();
                    data.colorChannels[key] = (f, s);
                }
                // addonVariants
                count = sync.Read<int>();
                for (int i = 0; i < count; i++)
                    data.addonVariants.Add(sync.Read<int>());
                // addonColors
                count = sync.Read<int>();
                for (int i = 0; i < count; i++)
                {
                    Color? f = sync.Read<bool>() ? sync.Read<Color>() : (Color?)null;
                    Color? s = sync.Read<bool>() ? sync.Read<Color>() : (Color?)null;
                    data.addonColors.Add((f, s));
                }
                // colorChannelLinks
                count = sync.Read<int>();
                for (int i = 0; i < count; i++)
                {
                    var key = sync.Read<string>();
                    var ld  = new AlienCompData.LinkData();
                    ld.targetsOne = ReadTargets(sync);
                    ld.targetsTwo = ReadTargets(sync);
                    data.colorChannelLinks[key] = ld;
                }
            }
        }

        static void WriteTargets(SyncWorker sync, List<(string channel, int categoryIndex)> targets)
        {
            sync.Write(targets.Count);
            foreach (var (ch, idx) in targets)
            {
                sync.Write(ch);
                sync.Write(idx);
            }
        }

        static List<(string channel, int categoryIndex)> ReadTargets(SyncWorker sync)
        {
            var list = new List<(string, int)>();
            int count = sync.Read<int>();
            for (int i = 0; i < count; i++)
                list.Add((sync.Read<string>(), sync.Read<int>()));
            return list;
        }

        public class AlienCompData
        {
            public Dictionary<string, (Color, Color)> colorChannels = new();
            public List<int> addonVariants = new();
            public List<(Color? first, Color? second)> addonColors = new();
            public Dictionary<string, LinkData> colorChannelLinks = new();

            public class LinkData
            {
                public List<(string channel, int categoryIndex)> targetsOne = new();
                public List<(string channel, int categoryIndex)> targetsTwo = new();
            }
        }
    }
}
