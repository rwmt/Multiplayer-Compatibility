using HarmonyLib;
using Multiplayer.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Follow Me by Fluffy</summary>
    /// <see href="https://github.com/fluffy-mods/FollowMe"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=715759739"/>
    [MpCompatFor("Fluffy.FollowMe")]
    class FollowMe
    {
        public FollowMe(ModContentPack mod) => LongEventHandler.ExecuteWhenFinished(LatePatch);

        public static Type cinematicCamera = AccessTools.TypeByName("FollowMe.CinematicCamera");
        public static MethodInfo followNewSubject = AccessTools.Method("FollowMe.CinematicCamera:FollowNewSubject");
        public static MethodInfo verseRandRange = AccessTools.Method("Verse.Rand:Range", new Type[] { typeof(int), typeof(int) });

        private static uint state;

        private static uint myCheapRand()
        {
            uint x = state;
            x ^= (x << 13);
            x ^= (x >> 17);
            x ^= (x << 5);
            state = x;
            return x;
        }

        private static float myCheapRandFloat()
        {
            return (float)myCheapRand() / (float)uint.MaxValue;
        }
        private static int myCheapRange(int min, int max)
        {
            float r = myCheapRandFloat();

            return (int)(min + ((max - min) * r));
        }

        public static IEnumerable<CodeInstruction> transpileCheapRand(IEnumerable<CodeInstruction> instr)
        {
            foreach(var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo callee && callee == verseRandRange)
                {
                    ci.operand = AccessTools.Method(typeof(FollowMe), nameof(myCheapRange));
                }
                yield return ci;
            }
        }

        private static void LatePatch()
        {
            state = (uint)(DateTime.UtcNow - new DateTime(1970, 01, 01)).TotalMilliseconds;
            var transpiler = new HarmonyMethod(typeof(FollowMe), nameof(transpileCheapRand));

            MpCompat.harmony.Patch(followNewSubject, transpiler: transpiler);
        }
    }
}
