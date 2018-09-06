// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Common.Tuples;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal class FromYCbCrSimd : JpegColorConverter
        {
            public FromYCbCrSimd()
                : base(JpegColorSpace.YCbCr)
            {
            }

            public override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                int remainder = result.Length % 8;
                int simdCount = result.Length - remainder;
                if (simdCount > 0)
                {
                    ConvertCore(values.Slice(0, simdCount), result.Slice(0, simdCount));
                }

                FromYCbCrBasic.ConvertCore(values.Slice(simdCount, remainder), result.Slice(simdCount, remainder));
            }

            /// <summary>
            /// SIMD convert using buffers of sizes divisable by 8.
            /// </summary>
            internal static void ConvertCore(in ComponentValues values, Span<Vector4> result)
            {
                DebugGuard.IsTrue(result.Length % 8 == 0, nameof(result), "result.Length should be divisable by 8!");

                ref Vector4Pair yBase =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector4Pair cbBase =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector4Pair crBase =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component2));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                var chromaOffset = new Vector4(-128f);

                // Walking 8 elements at one step:
                int n = result.Length / 8;

                for (int i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    Vector4Pair y = Unsafe.Add(ref yBase, i);

                    // cb = cbVals[i] - 128F;
                    Vector4Pair cb = Unsafe.Add(ref cbBase, i);
                    cb.AddInplace(chromaOffset);

                    // cr = crVals[i] - 128F;
                    Vector4Pair cr = Unsafe.Add(ref crBase, i);
                    cr.AddInplace(chromaOffset);

                    // r = y + (1.402F * cr);
                    Vector4Pair r = y;
                    Vector4Pair tmp = cr;
                    tmp.MultiplyInplace(1.402F);
                    r.AddInplace(ref tmp);

                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    Vector4Pair g = y;
                    tmp = cb;
                    tmp.MultiplyInplace(-0.344136F);
                    g.AddInplace(ref tmp);
                    tmp = cr;
                    tmp.MultiplyInplace(-0.714136F);
                    g.AddInplace(ref tmp);

                    // b = y + (1.772F * cb);
                    Vector4Pair b = y;
                    tmp = cb;
                    tmp.MultiplyInplace(1.772F);
                    b.AddInplace(ref tmp);

                    if (Vector<float>.Count == 4)
                    {
                        // TODO: Find a way to properly run & test this path on AVX2 PC-s! (Have I already mentioned that Vector<T> is terrible?)
                        r.RoundAndDownscalePreAvx2();
                        g.RoundAndDownscalePreAvx2();
                        b.RoundAndDownscalePreAvx2();
                    }
                    else if (SimdUtils.IsAvx2CompatibleArchitecture)
                    {
                        r.RoundAndDownscaleAvx2();
                        g.RoundAndDownscaleAvx2();
                        b.RoundAndDownscaleAvx2();
                    }
                    else
                    {
                        // TODO: Run fallback scalar code here
                        // However, no issues expected before someone implements this: https://github.com/dotnet/coreclr/issues/12007
                        throw new NotImplementedException("Your CPU architecture is too modern!");
                    }

                    // Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Collect(ref r, ref g, ref b);
                }
            }
        }
    }
}