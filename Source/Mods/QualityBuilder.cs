using System;
using HarmonyLib;
using Multiplayer.API;
using Verse;
using RimWorld;
using System.Reflection;

namespace Multiplayer.Compat
{
    /// <summary>Quality Builder by Hatti</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=754637870"/>
    [MpCompatFor("hatti.qualitybuilder")]
    class QualityBuilder
    {
        // The skilled-builder designator remembers the min-quality the player picked from its
        // right-click menu in an instance field (curQualityCat). This ref lets us serialize that
        // choice so a designation resolves to the same quality on every client.
        private static AccessTools.FieldRef<Designator, QualityCategory?> desiredQualityField;

        public QualityBuilder(ModContentPack mod)
        {
            Type[] argTypes = { typeof(QualityCategory) };

            // --- "Skilled" toggle gizmo on the building comp ---
            Type builderCompType = AccessTools.TypeByName("QualityBuilder.CompQualityBuilder");
            if (builderCompType != null)
                MP.RegisterSyncMethod(builderCompType, "ToggleSkilled");

            // The gizmo's right-click "set min quality" lambda is non-capturing, so it still lives in a
            // cached <>c closure. Match it by signature rather than by a brittle lambda ordinal.
            Type toggleCommandType = AccessTools.TypeByName("QualityBuilder.CompQualityBuilder+ToggleCommand+<>c");
            MethodInfo toggleCommandMethod = toggleCommandType != null
                ? MpMethodUtil.GetFirstMethodBySignature(toggleCommandType, argTypes)
                : null;
            if (toggleCommandMethod != null)
            {
                MP.RegisterSyncWorker<object>(SyncTypes, toggleCommandType);
                MP.RegisterSyncMethod(toggleCommandMethod).SetContext(SyncContext.MapSelected);
            }
            else
            {
                Log.Warning("MPCompat :: QualityBuilder: couldn't resolve the ToggleCommand right-click sync method; the min-quality gizmo menu won't be synced.");
            }

            // --- "Skilled builder" designator (right-click min-quality choice) ---
            // Previously this synced the choice-setting lambda through its <>c closure. QualityBuilder made
            // curQualityCat a non-static (per-instance) field, so that lambda now captures 'this' and the
            // <>c closure no longer exists -- looking it up returned null and NRE'd the whole patch. Instead,
            // sync the designator's state directly: MP replays the designation with this worker, so every
            // client resolves the same desired quality. This mirrors how other modded designators are synced
            // (e.g. Dubs Bad Hygiene) and is robust to that closure refactor.
            Type designatorType = AccessTools.TypeByName("QualityBuilder._Designator_SkilledBuilder");
            if (designatorType != null)
            {
                desiredQualityField = AccessTools.FieldRefAccess<QualityCategory?>(designatorType, "curQualityCat");
                MP.RegisterSyncWorker<Designator>(SyncSkilledDesignator, designatorType, shouldConstruct: true);
            }
            else
            {
                Log.Warning("MPCompat :: QualityBuilder: couldn't find _Designator_SkilledBuilder; its designations won't be synced.");
            }
        }

        // Serializes the skilled-builder designator's chosen min quality (a nullable QualityCategory).
        // The designator is freshly constructed (shouldConstruct: true), so curQualityCat starts null;
        // only overwrite it when the writer actually had a value.
        private static void SyncSkilledDesignator(SyncWorker sync, ref Designator designator)
        {
            if (sync.isWriting)
            {
                QualityCategory? chosen = desiredQualityField(designator);
                sync.Write(chosen.HasValue);
                if (chosen.HasValue)
                    sync.Write(chosen.Value);
            }
            else
            {
                if (sync.Read<bool>())
                    desiredQualityField(designator) = sync.Read<QualityCategory>();
            }
        }

        void SyncTypes(SyncWorker sync, ref object c)
        {
            if (sync.isWriting)
            {
                sync.Write(c.GetType());
            } else
            {
                c = Activator.CreateInstance(sync.Read<Type>());
            }
        }
    }
}
