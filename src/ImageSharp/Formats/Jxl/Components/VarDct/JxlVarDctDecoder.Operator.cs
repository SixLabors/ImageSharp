// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Defines DC sample conversion, color correlation and context classification independently of row traversal.
    /// </summary>
    private readonly ref struct DcOperator
    {
        private readonly Vector3 factors;
        private readonly float correlationX;
        private readonly float correlationB;
        private readonly ReadOnlySpan<int> thresholdsX;
        private readonly ReadOnlySpan<int> thresholdsY;
        private readonly ReadOnlySpan<int> thresholdsB;

        /// <summary>
        /// Initializes a new instance of the <see cref="DcOperator"/> struct with complete per-group factors and frame thresholds.
        /// </summary>
        /// <param name="factors">The DC factors in X, Y, B order, including extra precision.</param>
        /// <param name="correlationX">The ratio that restores X from dequantized Y.</param>
        /// <param name="correlationB">The ratio that restores B from dequantized Y.</param>
        /// <param name="thresholdsX">The signed quantized X thresholds.</param>
        /// <param name="thresholdsY">The signed quantized Y thresholds.</param>
        /// <param name="thresholdsB">The signed quantized B thresholds.</param>
        public DcOperator(
            Vector3 factors,
            float correlationX,
            float correlationB,
            ReadOnlySpan<int> thresholdsX,
            ReadOnlySpan<int> thresholdsY,
            ReadOnlySpan<int> thresholdsB)
        {
            this.factors = factors;
            this.correlationX = correlationX;
            this.correlationB = correlationB;
            this.thresholdsX = thresholdsX;
            this.thresholdsY = thresholdsY;
            this.thresholdsB = thresholdsB;
        }

        /// <summary>
        /// Restores one DC sample triple and classifies its original signed integers.
        /// </summary>
        /// <param name="quantizedX">The quantized X sample.</param>
        /// <param name="quantizedY">The quantized Y sample.</param>
        /// <param name="quantizedB">The quantized B sample.</param>
        /// <param name="x">The restored X sample.</param>
        /// <param name="y">The restored Y sample.</param>
        /// <param name="b">The restored B sample.</param>
        /// <param name="context">The combined DC context index, from zero through 63.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int quantizedX, int quantizedY, int quantizedB, out float x, out float y, out float b, out byte context)
        {
            y = Scale(quantizedY, this.factors.Y);
            x = float.MultiplyAddEstimate(y, this.correlationX, Scale(quantizedX, this.factors.X));
            b = float.MultiplyAddEstimate(y, this.correlationB, Scale(quantizedB, this.factors.Z));
            context = this.GetContext(quantizedX, quantizedY, quantizedB);
        }

        /// <summary>
        /// Classifies one original quantized DC triple without converting or scaling its signed integers.
        /// </summary>
        /// <param name="x">The quantized X value.</param>
        /// <param name="y">The quantized Y value.</param>
        /// <param name="b">The quantized B value.</param>
        /// <returns>The combined context index, from zero through 63.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetContext(int x, int y, int b)
        {
            int bucketX = 0;
            int bucketY = 0;
            int bucketB = 0;
            foreach (int threshold in this.thresholdsX)
            {
                bucketX += x > threshold ? 1 : 0;
            }

            foreach (int threshold in this.thresholdsY)
            {
                bucketY += y > threshold ? 1 : 0;
            }

            foreach (int threshold in this.thresholdsB)
            {
                bucketB += b > threshold ? 1 : 0;
            }

            // Y is the innermost bucket, followed by B then X. DC-global validation limits the complete product
            // to 64 combinations, so narrowing the final index to byte cannot lose information.
            return (byte)((((bucketX * (this.thresholdsB.Length + 1)) + bucketB) * (this.thresholdsY.Length + 1)) + bucketY);
        }

        /// <summary>
        /// Converts one signed integer to float before applying its complete DC multiplier.
        /// </summary>
        /// <param name="value">The original quantized sample.</param>
        /// <param name="factor">The component multiplier including extra precision.</param>
        /// <returns>The dequantized single-precision sample.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Scale(int value, float factor) => (float)value * factor;

        /// <summary>
        /// Restores 4 independent DC triples in equally positioned component lanes.
        /// </summary>
        /// <param name="quantizedX">The original signed X lanes.</param>
        /// <param name="quantizedY">The original signed Y lanes.</param>
        /// <param name="quantizedB">The original signed B lanes.</param>
        /// <param name="x">The restored X lanes.</param>
        /// <param name="y">The restored Y lanes.</param>
        /// <param name="b">The restored B lanes.</param>
        /// <param name="contexts">The context indices in their original 32-bit lane positions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector128<int> quantizedX,
            Vector128<int> quantizedY,
            Vector128<int> quantizedB,
            out Vector128<float> x,
            out Vector128<float> y,
            out Vector128<float> b,
            out Vector128<int> contexts)
        {
            // Decode factors remain separate from color correlation. Match native conversion -> multiply -> multiply-add;
            // retaining float intermediates preserves fractional DC without clamping to the pixel storage range.
            y = Scale(quantizedY, this.factors.Y);
            x = Vector128.MultiplyAddEstimate(y, Vector128.Create(this.correlationX), Scale(quantizedX, this.factors.X));
            b = Vector128.MultiplyAddEstimate(y, Vector128.Create(this.correlationB), Scale(quantizedB, this.factors.Z));
            contexts = this.GetContext(quantizedX, quantizedY, quantizedB);
        }

        /// <summary>
        /// Classifies 4 independent DC triples with signed threshold comparisons.
        /// </summary>
        /// <param name="x">The original quantized X lanes.</param>
        /// <param name="y">The original quantized Y lanes.</param>
        /// <param name="b">The original quantized B lanes.</param>
        /// <returns>The combined indices in their original lane order, each at most 63.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> GetContext(Vector128<int> x, Vector128<int> y, Vector128<int> b)
        {
            Vector128<int> bucketX = Vector128<int>.Zero;
            Vector128<int> bucketY = Vector128<int>.Zero;
            Vector128<int> bucketB = Vector128<int>.Zero;
            foreach (int threshold in this.thresholdsX)
            {
                bucketX -= Vector128.GreaterThan(x, Vector128.Create(threshold));
            }

            foreach (int threshold in this.thresholdsY)
            {
                bucketY -= Vector128.GreaterThan(y, Vector128.Create(threshold));
            }

            foreach (int threshold in this.thresholdsB)
            {
                bucketB -= Vector128.GreaterThan(b, Vector128.Create(threshold));
            }

            // True comparison lanes contain -1. Subtracting each mask counts exceeded thresholds exactly.
            // Empty threshold lists naturally produce context zero without another per-block selection branch.
            return (((bucketX * Vector128.Create(this.thresholdsB.Length + 1)) + bucketB) * Vector128.Create(this.thresholdsY.Length + 1)) + bucketY;
        }

        /// <summary>
        /// Converts signed integer lanes to float before applying one component's multiplier.
        /// </summary>
        /// <param name="value">The original quantized lanes.</param>
        /// <param name="factor">The complete multiplier including extra precision.</param>
        /// <returns>The dequantized float lanes in the same order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Scale(Vector128<int> value, float factor)
            => Vector128.ConvertToSingle(value) * Vector128.Create(factor);

        /// <summary>
        /// Restores 8 independent DC triples in equally positioned component lanes.
        /// </summary>
        /// <param name="quantizedX">The original signed X lanes.</param>
        /// <param name="quantizedY">The original signed Y lanes.</param>
        /// <param name="quantizedB">The original signed B lanes.</param>
        /// <param name="x">The restored X lanes.</param>
        /// <param name="y">The restored Y lanes.</param>
        /// <param name="b">The restored B lanes.</param>
        /// <param name="contexts">The context indices in their original 32-bit lane positions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector256<int> quantizedX,
            Vector256<int> quantizedY,
            Vector256<int> quantizedB,
            out Vector256<float> x,
            out Vector256<float> y,
            out Vector256<float> b,
            out Vector256<int> contexts)
        {
            // Decode factors remain separate from color correlation. Match native conversion -> multiply -> multiply-add;
            // retaining float intermediates preserves fractional DC without clamping to the pixel storage range.
            y = Scale(quantizedY, this.factors.Y);
            x = Vector256.MultiplyAddEstimate(y, Vector256.Create(this.correlationX), Scale(quantizedX, this.factors.X));
            b = Vector256.MultiplyAddEstimate(y, Vector256.Create(this.correlationB), Scale(quantizedB, this.factors.Z));
            contexts = this.GetContext(quantizedX, quantizedY, quantizedB);
        }

        /// <summary>
        /// Classifies 8 independent DC triples with signed threshold comparisons.
        /// </summary>
        /// <param name="x">The original quantized X lanes.</param>
        /// <param name="y">The original quantized Y lanes.</param>
        /// <param name="b">The original quantized B lanes.</param>
        /// <returns>The combined indices in their original lane order, each at most 63.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> GetContext(Vector256<int> x, Vector256<int> y, Vector256<int> b)
        {
            Vector256<int> bucketX = Vector256<int>.Zero;
            Vector256<int> bucketY = Vector256<int>.Zero;
            Vector256<int> bucketB = Vector256<int>.Zero;
            foreach (int threshold in this.thresholdsX)
            {
                bucketX -= Vector256.GreaterThan(x, Vector256.Create(threshold));
            }

            foreach (int threshold in this.thresholdsY)
            {
                bucketY -= Vector256.GreaterThan(y, Vector256.Create(threshold));
            }

            foreach (int threshold in this.thresholdsB)
            {
                bucketB -= Vector256.GreaterThan(b, Vector256.Create(threshold));
            }

            // True comparison lanes contain -1. Subtracting each mask counts exceeded thresholds exactly.
            // Empty threshold lists naturally produce context zero without another per-block selection branch.
            return (((bucketX * Vector256.Create(this.thresholdsB.Length + 1)) + bucketB) * Vector256.Create(this.thresholdsY.Length + 1)) + bucketY;
        }

        /// <summary>
        /// Converts signed integer lanes to float before applying one component's multiplier.
        /// </summary>
        /// <param name="value">The original quantized lanes.</param>
        /// <param name="factor">The complete multiplier including extra precision.</param>
        /// <returns>The dequantized float lanes in the same order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Scale(Vector256<int> value, float factor)
            => Vector256.ConvertToSingle(value) * Vector256.Create(factor);

        /// <summary>
        /// Restores 16 independent DC triples in equally positioned component lanes.
        /// </summary>
        /// <param name="quantizedX">The original signed X lanes.</param>
        /// <param name="quantizedY">The original signed Y lanes.</param>
        /// <param name="quantizedB">The original signed B lanes.</param>
        /// <param name="x">The restored X lanes.</param>
        /// <param name="y">The restored Y lanes.</param>
        /// <param name="b">The restored B lanes.</param>
        /// <param name="contexts">The context indices in their original 32-bit lane positions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector512<int> quantizedX,
            Vector512<int> quantizedY,
            Vector512<int> quantizedB,
            out Vector512<float> x,
            out Vector512<float> y,
            out Vector512<float> b,
            out Vector512<int> contexts)
        {
            // Decode factors remain separate from color correlation. Match native conversion -> multiply -> multiply-add;
            // retaining float intermediates preserves fractional DC without clamping to the pixel storage range.
            y = Scale(quantizedY, this.factors.Y);
            x = Vector512.MultiplyAddEstimate(y, Vector512.Create(this.correlationX), Scale(quantizedX, this.factors.X));
            b = Vector512.MultiplyAddEstimate(y, Vector512.Create(this.correlationB), Scale(quantizedB, this.factors.Z));
            contexts = this.GetContext(quantizedX, quantizedY, quantizedB);
        }

        /// <summary>
        /// Classifies 16 independent DC triples with signed threshold comparisons.
        /// </summary>
        /// <param name="x">The original quantized X lanes.</param>
        /// <param name="y">The original quantized Y lanes.</param>
        /// <param name="b">The original quantized B lanes.</param>
        /// <returns>The combined indices in their original lane order, each at most 63.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> GetContext(Vector512<int> x, Vector512<int> y, Vector512<int> b)
        {
            Vector512<int> bucketX = Vector512<int>.Zero;
            Vector512<int> bucketY = Vector512<int>.Zero;
            Vector512<int> bucketB = Vector512<int>.Zero;
            foreach (int threshold in this.thresholdsX)
            {
                bucketX -= Vector512.GreaterThan(x, Vector512.Create(threshold));
            }

            foreach (int threshold in this.thresholdsY)
            {
                bucketY -= Vector512.GreaterThan(y, Vector512.Create(threshold));
            }

            foreach (int threshold in this.thresholdsB)
            {
                bucketB -= Vector512.GreaterThan(b, Vector512.Create(threshold));
            }

            // True comparison lanes contain -1. Subtracting each mask counts exceeded thresholds exactly.
            // Empty threshold lists naturally produce context zero without another per-block selection branch.
            return (((bucketX * Vector512.Create(this.thresholdsB.Length + 1)) + bucketB) * Vector512.Create(this.thresholdsY.Length + 1)) + bucketY;
        }

        /// <summary>
        /// Converts signed integer lanes to float before applying one component's multiplier.
        /// </summary>
        /// <param name="value">The original quantized lanes.</param>
        /// <param name="factor">The complete multiplier including extra precision.</param>
        /// <returns>The dequantized float lanes in the same order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Scale(Vector512<int> value, float factor)
            => Vector512.ConvertToSingle(value) * Vector512.Create(factor);

        /// <summary>
        /// Writes four validated context indices as four bytes without touching the following destination samples.
        /// </summary>
        /// <param name="contexts">Four 32-bit indices in the range zero through 63.</param>
        /// <param name="destination">The first of four output bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreContexts(Vector128<int> contexts, ref byte destination)
        {
            Vector128<ushort> words = Vector128.Narrow(contexts.AsUInt32(), Vector128<uint>.Zero);
            Vector128<byte> bytes = Vector128.Narrow(words, Vector128<ushort>.Zero);
            Unsafe.WriteUnaligned(ref destination, bytes.AsUInt32().ToScalar());
        }

        /// <summary>
        /// Writes eight validated context indices as eight consecutive bytes.
        /// </summary>
        /// <param name="contexts">Eight 32-bit indices in the range zero through 63.</param>
        /// <param name="destination">The first of eight output bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreContexts(Vector256<int> contexts, ref byte destination)
        {
            // Vector.Narrow preserves logical lane order across 128-bit groups. Keep only the populated lower half.
            Vector128<ushort> words = Vector256.Narrow(contexts.AsUInt32(), Vector256<uint>.Zero).GetLower();
            Vector128.Narrow(words, Vector128<ushort>.Zero).GetLower().StoreUnsafe(ref destination);
        }

        /// <summary>
        /// Writes sixteen validated context indices as sixteen consecutive bytes.
        /// </summary>
        /// <param name="contexts">Sixteen 32-bit indices in the range zero through 63.</param>
        /// <param name="destination">The first of sixteen output bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreContexts(Vector512<int> contexts, ref byte destination)
        {
            Vector256<ushort> words = Vector512.Narrow(contexts.AsUInt32(), Vector512<uint>.Zero).GetLower();
            Vector256.Narrow(words, Vector256<ushort>.Zero).GetLower().StoreUnsafe(ref destination);
        }
    }
}
