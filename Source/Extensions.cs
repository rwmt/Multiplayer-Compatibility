using System;
using Multiplayer.API;

namespace Multiplayer.Compat
{
    public static class Extensions
    {
        internal static string After(this string s, char c)
        {
            if (s.IndexOf(c) == -1)
                throw new Exception($"Char {c} not found in string {s}");
            return s.Substring(s.IndexOf(c) + 1);
        }

        internal static string Until(this string s, char c)
        {
            if (s.IndexOf(c) == -1)
                throw new Exception($"Char {c} not found in string {s}");
            return s.Substring(0, s.IndexOf(c));
        }

        internal static int CharacterCount(this string s, char c)
        {
            int num = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == c)
                    num++;
            return num;
        }

        public static void SetDebugOnly(this ISyncMethod[] syncMethods)
        {
            foreach (var method in syncMethods)
            {
                method.SetDebugOnly();
            }
        }

        public static void SetDebugOnly(this ISyncDelegate[] syncMethods)
        {
            foreach (var method in syncMethods)
            {
                method.SetDebugOnly();
            }
        }

        public static void SetContext(this ISyncMethod[] syncDelegates, SyncContext context)
        {
            foreach (var method in syncDelegates)
            {
                method.SetContext(context);
            }
        }

        public static void SetContext(this ISyncDelegate[] syncDelegates, SyncContext context)
        {
            foreach (var method in syncDelegates)
            {
                method.SetContext(context);
            }
        }

        private const string SteamSuffix = "_steam";
        private const string CopySuffix = "_copy";

        internal static string NoModIdSuffix(this string modId)
        {
            while (true)
            {
                if (modId.EndsWith(SteamSuffix))
                {
                    modId = modId.Substring(0, modId.Length - SteamSuffix.Length);
                    continue;
                }

                if (modId.EndsWith(CopySuffix))
                {
                    modId = modId.Substring(0, modId.Length - CopySuffix.Length);
                    continue;
                }

                return modId;
            }
        }
    }
}