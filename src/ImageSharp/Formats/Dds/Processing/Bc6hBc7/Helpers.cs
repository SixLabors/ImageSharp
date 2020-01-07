using System;
using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Dds.Processing.Bc6hBc7
{
    internal static class Helpers
    {
        public static bool IsFixUpOffset(byte uPartitions, byte uShape, int uOffset)
        {
            Debug.Assert(uPartitions < 3 && uShape < 64 && uOffset < 16 && uOffset >= 0);
            for (byte p = 0; p <= uPartitions; p++)
            {
                if (uOffset == Constants.g_aFixUp[uPartitions][uShape][p])
                {
                    return true;
                }
            }
            return false;
        }

        public static void TransformInverse(INTEndPntPair[] aEndPts, LDRColorA Prec, bool bSigned)
        {
            var WrapMask = new INTColor((1 << Prec.r) - 1, (1 << Prec.g) - 1, (1 << Prec.b) - 1);
            aEndPts[0].B += aEndPts[0].A; aEndPts[0].B &= WrapMask;
            aEndPts[1].A += aEndPts[0].A; aEndPts[1].A &= WrapMask;
            aEndPts[1].B += aEndPts[0].A; aEndPts[1].B &= WrapMask;
            if (bSigned)
            {
                aEndPts[0].B.SignExtend(Prec);
                aEndPts[1].A.SignExtend(Prec);
                aEndPts[1].B.SignExtend(Prec);
            }
        }

        public static int DivRem(int a, int b, out int result)
        {
            int div = a / b;
            result = a - (div * b);
            return div;
        }

        // Fill colors where each pixel is 4 bytes (rgba)
        public static void FillWithErrorColors(Span<byte> pOut, ref int index, int numPixels, byte divSize, int stride)
        {
            int rem;
            for (int i = 0; i < numPixels; ++i)
            {
                pOut[index++] = 0;
                pOut[index++] = 0;
                pOut[index++] = 0;
                pOut[index++] = 255;
                DivRem(i + 1, divSize, out rem);
                if (rem == 0)
                {
                    index += 4 * (stride - divSize);
                }
            }
        }
    }
}
