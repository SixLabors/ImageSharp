using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Tuples;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal class FromYCbCrSimdAvx2 : ColorConverters.JpegColorConverter
        {
            public FromYCbCrSimdAvx2()
                : base(JpegColorSpace.YCbCr)
            {
            }

            public static bool IsAvailable => Vector.IsHardwareAccelerated && Vector<float>.Count == 8;

            public override void ConvertToRGBA(ComponentValues values, Span<Vector4> result)
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
            internal static void ConvertCore(ComponentValues values, Span<Vector4> result)
            {
                // This implementation is actually AVX specific.
                // An AVX register is capable of storing 8 float-s.
                if (!IsAvailable)
                {
                    throw new InvalidOperationException(
                        "JpegColorConverter.FromYCbCrSimd256 can be used only on architecture having 256 byte floating point SIMD registers!");
                }

                ref Vector<float> yBase =
                    ref Unsafe.As<float, Vector<float>>(ref values.Component0.DangerousGetPinnableReference());
                ref Vector<float> cbBase =
                    ref Unsafe.As<float, Vector<float>>(ref values.Component1.DangerousGetPinnableReference());
                ref Vector<float> crBase =
                    ref Unsafe.As<float, Vector<float>>(ref values.Component2.DangerousGetPinnableReference());

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref result.DangerousGetPinnableReference());

                var chromaOffset = new Vector<float>(-128f);

                // Walking 8 elements at one step:
                int n = result.Length / 8;

                for (int i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    Vector<float> y = Unsafe.Add(ref yBase, i);
                    Vector<float> cb = Unsafe.Add(ref cbBase, i) + chromaOffset;
                    Vector<float> cr = Unsafe.Add(ref crBase, i) + chromaOffset;

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    // Adding & multiplying 8 elements at one time:
                    Vector<float> r = y + (cr * new Vector<float>(1.402F));
                    Vector<float> g = y - (cb * new Vector<float>(0.344136F)) - (cr * new Vector<float>(0.714136F));
                    Vector<float> b = y + (cb * new Vector<float>(1.772F));

                    // Vector<float> has no .Clamp(), need to switch to Vector4 for the next operation:
                    // TODO: Is it worth to use Vector<float> at all?
                    Vector4Pair rr = Unsafe.As<Vector<float>, Vector4Pair>(ref r);
                    Vector4Pair gg = Unsafe.As<Vector<float>, Vector4Pair>(ref g);
                    Vector4Pair bb = Unsafe.As<Vector<float>, Vector4Pair>(ref b);

                    rr.RoundAndDownscaleAvx2();
                    gg.RoundAndDownscaleAvx2();
                    bb.RoundAndDownscaleAvx2();

                    // Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Collect(ref rr, ref gg, ref bb);
                }
            }
        }
    }
}