using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    internal abstract partial class JpegColorConverter
    {
        internal class FromYCbCrBasic : JpegColorConverter
        {
            public FromYCbCrBasic()
                : base(JpegColorSpace.YCbCr)
            {
            }

            public override void ConvertToRGBA(ComponentValues values, Span<Vector4> result)
            {
                ConvertCore(values, result);
            }

            internal static void ConvertCore(ComponentValues values, Span<Vector4> result)
            {
                // TODO: We can optimize a lot here with Vector<float> and SRCS.Unsafe()!
                ReadOnlySpan<float> yVals = values.Component0;
                ReadOnlySpan<float> cbVals = values.Component1;
                ReadOnlySpan<float> crVals = values.Component2;

                var v = new Vector4(0, 0, 0, 1);

                var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

                for (int i = 0; i < result.Length; i++)
                {
                    float y = yVals[i];
                    float cb = cbVals[i] - 128F;
                    float cr = crVals[i] - 128F;

                    v.X = MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero);
                    v.Y = MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero);
                    v.Z = MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero);

                    v *= scale;

                    result[i] = v;
                }
            }
        }

        internal class FromYCbCrSimd256 : JpegColorConverter
        {
            public FromYCbCrSimd256()
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

                    rr.RoundAndDownscale();
                    gg.RoundAndDownscale();
                    bb.RoundAndDownscale();

                    // Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Collect(ref rr, ref gg, ref bb);
                }
            }

            private struct Vector4Pair
            {
                public Vector4 A;

                public Vector4 B;

                private static readonly Vector4 Scale = new Vector4(1 / 255F);

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void RoundAndDownscale()
                {
                    this.A = this.A.PseudoRound() * Scale;
                    this.B = this.B.PseudoRound() * Scale;
                }
            }

            private struct Vector4Octet
            {
#pragma warning disable SA1132 // Do not combine fields
                public Vector4 V0, V1, V2, V3, V4, V5, V6, V7;

                public void Collect(ref Vector4Pair rr, ref Vector4Pair gg, ref Vector4Pair bb)
                {
                    this.V0.X = rr.A.X;
                    this.V0.Y = gg.A.X;
                    this.V0.Z = bb.A.X;
                    this.V0.W = 1f;

                    this.V1.X = rr.A.Y;
                    this.V1.Y = gg.A.Y;
                    this.V1.Z = bb.A.Y;
                    this.V1.W = 1f;

                    this.V2.X = rr.A.Z;
                    this.V2.Y = gg.A.Z;
                    this.V2.Z = bb.A.Z;
                    this.V2.W = 1f;

                    this.V3.X = rr.A.W;
                    this.V3.Y = gg.A.W;
                    this.V3.Z = bb.A.W;
                    this.V3.W = 1f;

                    this.V4.X = rr.B.X;
                    this.V4.Y = gg.B.X;
                    this.V4.Z = bb.B.X;
                    this.V4.W = 1f;

                    this.V5.X = rr.B.Y;
                    this.V5.Y = gg.B.Y;
                    this.V5.Z = bb.B.Y;
                    this.V5.W = 1f;

                    this.V6.X = rr.B.Z;
                    this.V6.Y = gg.B.Z;
                    this.V6.Z = bb.B.Z;
                    this.V6.W = 1f;

                    this.V7.X = rr.B.W;
                    this.V7.Y = gg.B.W;
                    this.V7.Z = bb.B.W;
                    this.V7.W = 1f;
                }
            }
        }
    }
}