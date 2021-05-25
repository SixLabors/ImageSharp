// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Quantization methods.
    /// </summary>
    internal static class QuantEnc
    {
        private static readonly byte[] Zigzag = { 0, 1, 4, 8, 5, 2, 3, 6, 9, 12, 13, 10, 7, 11, 14, 15 };

        private const int MaxLevel = 2047;

        // Diffusion weights. We under-correct a bit (15/16th of the error is actually
        // diffused) to avoid 'rainbow' chessboard pattern of blocks at q~=0.
        private const int C1 = 7;    // fraction of error sent to the 4x4 block below
        private const int C2 = 8;    // fraction of error sent to the 4x4 block on the right
        private const int DSHIFT = 4;
        private const int DSCALE = 1;   // storage descaling, needed to make the error fit byte

        public static int Quantize2Blocks(Span<short> input, Span<short> output, Vp8Matrix mtx)
        {
            var nz = QuantEnc.QuantizeBlock(input, output, mtx) << 0;
            nz |= QuantEnc.QuantizeBlock(input.Slice(1 * 16), output.Slice(1 * 16), mtx) << 1;
            return nz;
        }

        public static int QuantizeBlock(Span<short> input, Span<short> output, Vp8Matrix mtx)
        {
            int last = -1;
            int n;
            for (n = 0; n < 16; ++n)
            {
                int j = Zigzag[n];
                bool sign = input[j] < 0;
                uint coeff = (uint)((sign ? -input[j] : input[j]) + mtx.Sharpen[j]);
                if (coeff > mtx.ZThresh[j])
                {
                    uint q = mtx.Q[j];
                    uint iQ = mtx.IQ[j];
                    uint b = mtx.Bias[j];
                    int level = QuantDiv(coeff, iQ, b);
                    if (level > MaxLevel)
                    {
                        level = MaxLevel;
                    }

                    if (sign)
                    {
                        level = -level;
                    }

                    input[j] = (short)(level * (int)q);
                    output[n] = (short)level;
                    if (level != 0)
                    {
                        last = n;
                    }
                }
                else
                {
                    output[n] = 0;
                    input[j] = 0;
                }
            }

            return (last >= 0) ? 1 : 0;
        }

        // Quantize as usual, but also compute and return the quantization error.
        // Error is already divided by DSHIFT.
        public static int QuantizeSingle(Span<short> v, Vp8Matrix mtx)
        {
            int v0 = v[0];
            bool sign = v0 < 0;
            if (sign)
            {
                v0 = -v0;
            }

            if (v0 > (int)mtx.ZThresh[0])
            {
                int qV = QuantDiv((uint)v0, mtx.IQ[0], mtx.Bias[0]) * mtx.Q[0];
                int err = v0 - qV;
                v[0] = (short)(sign ? -qV : qV);
                return (sign ? -err : err) >> DSCALE;
            }

            v[0] = 0;
            return (sign ? -v0 : v0) >> DSCALE;
        }

        public static void CorrectDcValues(Vp8EncIterator it, Vp8Matrix mtx, short[] tmp, Vp8ModeScore rd)
        {
#pragma warning disable SA1005 // Single line comments should begin with single space
            //         | top[0] | top[1]
            // --------+--------+---------
            // left[0] | tmp[0]   tmp[1]  <->   err0 err1
            // left[1] | tmp[2]   tmp[3]        err2 err3
            //
            // Final errors {err1,err2,err3} are preserved and later restored
            // as top[]/left[] on the next block.
#pragma warning restore SA1005 // Single line comments should begin with single space
            for (int ch = 0; ch <= 1; ++ch)
            {
                Span<sbyte> top = it.TopDerr.AsSpan((it.X * 4) + ch, 2);
                Span<sbyte> left = it.LeftDerr.AsSpan(ch, 2);
                Span<short> c = tmp.AsSpan(ch * 4 * 16, 4 * 16);
                c[0] += (short)(((C1 * top[0]) + (C2 * left[0])) >> (DSHIFT - DSCALE));
                var err0 = QuantEnc.QuantizeSingle(c, mtx);
                c[1 * 16] += (short)(((C1 * top[1]) + (C2 * err0)) >> (DSHIFT - DSCALE));
                var err1 = QuantEnc.QuantizeSingle(c.Slice(1 * 16), mtx);
                c[2 * 16] += (short)(((C1 * err0) + (C2 * left[1])) >> (DSHIFT - DSCALE));
                var err2 = QuantEnc.QuantizeSingle(c.Slice(2 * 16), mtx);
                c[3 * 16] += (short)(((C1 * err1) + (C2 * err2)) >> (DSHIFT - DSCALE));
                var err3 = QuantEnc.QuantizeSingle(c.Slice(3 * 16), mtx);

                // TODO: set errors in rd
                // rd->derr[ch][0] = (int8_t)err1;
                // rd->derr[ch][1] = (int8_t)err2;
                // rd->derr[ch][2] = (int8_t)err3;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int QuantDiv(uint n, uint iQ, uint b)
        {
            return (int)(((n * iQ) + b) >> WebpConstants.QFix);
        }
    }
}
