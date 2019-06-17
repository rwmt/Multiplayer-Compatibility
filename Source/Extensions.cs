using System;
using System.Collections.Generic;

namespace Multiplayer.Compat
{
    public static class Extensions
    {
        public static void Insert<T>(this List<T> list, int index, params T[] items)
        {
            list.InsertRange(index, items);
        }
    }
}
