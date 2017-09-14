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

        internal class FromYCbCrSimd : JpegColorConverter
        {
            public FromYCbCrSimd()
                : base(JpegColorSpace.YCbCr)
            {
            }

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
                DebugGuard.IsTrue(result.Length % 8 == 0, nameof(result), "result.Length should be divisable by 8!");

                ref Vector4Pair yBase =
                    ref Unsafe.As<float, Vector4Pair>(ref values.Component0.DangerousGetPinnableReference());
                ref Vector4Pair cbBase =
                    ref Unsafe.As<float, Vector4Pair>(ref values.Component1.DangerousGetPinnableReference());
                ref Vector4Pair crBase =
                    ref Unsafe.As<float, Vector4Pair>(ref values.Component2.DangerousGetPinnableReference());

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref result.DangerousGetPinnableReference());

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

                    r.RoundAndDownscale();
                    g.RoundAndDownscale();
                    b.RoundAndDownscale();

                    // Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Collect(ref r, ref g, ref b);
                }
            }

            /// <summary>
            /// Its faster to process multiple Vector4-s together
            /// </summary>
            private struct Vector4Pair
            {
                public Vector4 A;

                public Vector4 B;

                private static readonly Vector4 Scale = new Vector4(1 / 255f);

                private static readonly Vector4 Half = new Vector4(0.5f);

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void RoundAndDownscale()
                {
                    // Emulate rounding:
                    this.A += Half;
                    this.B += Half;

                    // Downscale by 1/255
                    this.A *= Scale;
                    this.B *= Scale;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void MultiplyInplace(float value)
                {
                    this.A *= value;
                    this.B *= value;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void AddInplace(Vector4 value)
                {
                    this.A += value;
                    this.B += value;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void AddInplace(ref Vector4Pair other)
                {
                    this.A += other.A;
                    this.B += other.B;
                }
            }

            private struct Vector4Octet
            {
#pragma warning disable SA1132 // Do not combine fields
                public Vector4 V0, V1, V2, V3, V4, V5, V6, V7;

                /// <summary>
                /// Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order.
                /// </summary>
                public void Collect(ref Vector4Pair r, ref Vector4Pair g, ref Vector4Pair b)
                {
                    this.V0.X = r.A.X;
                    this.V0.Y = g.A.X;
                    this.V0.Z = b.A.X;
                    this.V0.W = 1f;

                    this.V1.X = r.A.Y;
                    this.V1.Y = g.A.Y;
                    this.V1.Z = b.A.Y;
                    this.V1.W = 1f;

                    this.V2.X = r.A.Z;
                    this.V2.Y = g.A.Z;
                    this.V2.Z = b.A.Z;
                    this.V2.W = 1f;

                    this.V3.X = r.A.W;
                    this.V3.Y = g.A.W;
                    this.V3.Z = b.A.W;
                    this.V3.W = 1f;

                    this.V4.X = r.B.X;
                    this.V4.Y = g.B.X;
                    this.V4.Z = b.B.X;
                    this.V4.W = 1f;

                    this.V5.X = r.B.Y;
                    this.V5.Y = g.B.Y;
                    this.V5.Z = b.B.Y;
                    this.V5.W = 1f;

                    this.V6.X = r.B.Z;
                    this.V6.Y = g.B.Z;
                    this.V6.Z = b.B.Z;
                    this.V6.W = 1f;

                    this.V7.X = r.B.W;
                    this.V7.Y = g.B.W;
                    this.V7.Z = b.B.W;
                    this.V7.W = 1f;
                }
            }
        }
    }
}