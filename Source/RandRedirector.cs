using System;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>This class allows replacing any <see cref="System.Random"/> calls with <see cref="Verse.Rand"/> calls</summary>
    public class RandRedirector : Random
    {
        public override int Next() => Rand.Range(0, int.MaxValue);

        public override int Next(int maxValue) => Rand.Range(0, maxValue);

        public override int Next(int minValue, int maxValue) => Rand.Range(minValue, maxValue);

        public override void NextBytes(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)Rand.RangeInclusive(0, 255);
        }

        public override double NextDouble() => Rand.Range(0f, 1f);
    }
}
