using System;
using System.Collections.Generic;
using System.Text;

namespace ReedSolomonTests
{
    public static class Helpers
    {
        public static string FormatSpan(Span<byte> span)
        {
            StringBuilder stringBuilder = new StringBuilder(span.Length * 4);

            for (int i = 0; i < span.Length; i++)
            {
                if (i % 16 == 0)
                {
                    stringBuilder.AppendLine();
                }

                stringBuilder.AppendFormat("0x{0:X2}, ", span[i]);
            }
            stringBuilder.AppendLine();

            return stringBuilder.ToString();
        }

        public static int CompareSpans(Span<byte> span1, Span<byte> Span2)
        {
            if (span1.Length != Span2.Length) throw new ArgumentException("Span lengths must be equal");

            int differenceCount = 0;
            for (int i = 0; i < span1.Length; i++)
            {
                if (span1[i] != Span2[i])
                {
                    differenceCount++;
                }
            }

            return differenceCount;
        }

        public static void CorruptSpan(Span<byte> span, double chance, Random random)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (chance > 0 && (chance >= 1 || random.NextDouble() < chance))
                {
                    span[i] ^= (byte)random.Next(1, 255);
                }
            }
        }
    }
}
