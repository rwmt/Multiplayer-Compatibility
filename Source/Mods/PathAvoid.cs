using System;
using System.Reflection;

using HarmonyLib;
using Multiplayer.API;
using Verse;
namespace Multiplayer.Compat
{
    /// <summary>Path Avoid by Kiame Vivacity</summary>
    /// <remarks>Everything works</remarks>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1180719857"/>
    /// <see href="https://github.com/KiameV/rimworld-pathavoid"/>
    [MpCompatFor("pathavoid.kv.rw")]
    public class PathAvoidCompat
    {
        static Type PathAvoidDefType;
        static Type Designator_PathAvoidType;
        static FieldInfo PathAvoidDefField;

        public PathAvoidCompat(ModContentPack mod)
        {
            PathAvoidDefType = AccessTools.TypeByName("PathAvoid.PathAvoidDef");
            Designator_PathAvoidType = AccessTools.TypeByName("PathAvoid.Designator_PathAvoid");
            PathAvoidDefField = AccessTools.Field(Designator_PathAvoidType, "def");

            MP.RegisterSyncWorker<Designator>(PathAvoidDesignatorSyncWorker, Designator_PathAvoidType);
        }

        static void PathAvoidDesignatorSyncWorker(SyncWorker sw, ref Designator designator)
        {
            Def def = null;

            if (sw.isWriting) {
                def = (Def) PathAvoidDefField.GetValue(designator);

                sw.Write(def.defName);
            } else {
                string defName = sw.Read<string>();

                def = GenDefDatabase.GetDef(PathAvoidDefType, defName, false);

                designator = (Designator) Activator.CreateInstance(Designator_PathAvoidType, new object[] { def });
            }
        }
    }
}
