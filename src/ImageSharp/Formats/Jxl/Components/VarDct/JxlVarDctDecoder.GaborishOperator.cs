// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Applies one normalized Gaborish row to logical source rows with mirrored horizontal borders.
    /// </summary>
    /// <param name="top">The preceding mirrored logical row.</param>
    /// <param name="middle">The current original logical row.</param>
    /// <param name="bottom">The following mirrored logical row.</param>
    /// <param name="destination">The logical output row, which does not overlap the source rows.</param>
    /// <param name="operation">The normalized center, axial and diagonal weights.</param>
    private static void FilterGaborishRow(
        ReadOnlySpan<float> top, ReadOnlySpan<float> middle, ReadOnlySpan<float> bottom, Span<float> destination, GaborishOperator operation)
    {
        ref float t = ref MemoryMarshal.GetReference(top);
        ref float m = ref MemoryMarshal.GetReference(middle);
        ref float b = ref MemoryMarshal.GetReference(bottom);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int width = destination.Length;
        int interiorLength = Math.Max(width - 2, 0);
        int interiorEnd = interiorLength + 1;
        int x = 1;

        // Each lane computes one interior column. Limiting batches to [1,width-1) keeps both shifted
        // neighbor loads inside the logical source rows. Widths one and two have no interior columns.
        // Unaligned loads and the scalar remainder retain the same axial and diagonal sum grouping.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (interiorEnd - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                nuint position = (nuint)x;
                Vector512<float> center = Vector512.LoadUnsafe(ref m, position);
                Vector512<float> axial = (Vector512.LoadUnsafe(ref m, position - 1) + Vector512.LoadUnsafe(ref m, position + 1))
                    + (Vector512.LoadUnsafe(ref t, position) + Vector512.LoadUnsafe(ref b, position));

                Vector512<float> diagonal = (Vector512.LoadUnsafe(ref t, position - 1) + Vector512.LoadUnsafe(ref t, position + 1))
                    + (Vector512.LoadUnsafe(ref b, position - 1) + Vector512.LoadUnsafe(ref b, position + 1));

                operation.Invoke(center, axial, diagonal).StoreUnsafe(ref output, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (interiorEnd - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                nuint position = (nuint)x;
                Vector256<float> center = Vector256.LoadUnsafe(ref m, position);
                Vector256<float> axial = (Vector256.LoadUnsafe(ref m, position - 1) + Vector256.LoadUnsafe(ref m, position + 1))
                    + (Vector256.LoadUnsafe(ref t, position) + Vector256.LoadUnsafe(ref b, position));

                Vector256<float> diagonal = (Vector256.LoadUnsafe(ref t, position - 1) + Vector256.LoadUnsafe(ref t, position + 1))
                    + (Vector256.LoadUnsafe(ref b, position - 1) + Vector256.LoadUnsafe(ref b, position + 1));

                operation.Invoke(center, axial, diagonal).StoreUnsafe(ref output, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (interiorEnd - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                nuint position = (nuint)x;
                Vector128<float> center = Vector128.LoadUnsafe(ref m, position);
                Vector128<float> axial = (Vector128.LoadUnsafe(ref m, position - 1) + Vector128.LoadUnsafe(ref m, position + 1))
                    + (Vector128.LoadUnsafe(ref t, position) + Vector128.LoadUnsafe(ref b, position));

                Vector128<float> diagonal = (Vector128.LoadUnsafe(ref t, position - 1) + Vector128.LoadUnsafe(ref t, position + 1))
                    + (Vector128.LoadUnsafe(ref b, position - 1) + Vector128.LoadUnsafe(ref b, position + 1));

                operation.Invoke(center, axial, diagonal).StoreUnsafe(ref output, (nuint)x);
            }
        }

        for (; x < interiorEnd; x++)
        {
            float axial = (Unsafe.Add(ref m, x - 1) + Unsafe.Add(ref m, x + 1))
                + (Unsafe.Add(ref t, x) + Unsafe.Add(ref b, x));

            float diagonal = (Unsafe.Add(ref t, x - 1) + Unsafe.Add(ref t, x + 1))
                + (Unsafe.Add(ref b, x - 1) + Unsafe.Add(ref b, x + 1));

            Unsafe.Add(ref output, x) = operation.Invoke(Unsafe.Add(ref m, x), axial, diagonal);
        }

        // Half-sample reflection repeats the first or last sample for a one-column neighbor.
        // A one-column row has one edge; every wider row has two distinct edges. Keep the same
        // sum grouping and operator as the interior rather than a separate border kernel.
        for (int edge = 0; edge < Math.Min(width, 2); edge++)
        {
            int position = edge == 0 ? 0 : width - 1;
            int left = Math.Max(position - 1, 0);
            int right = Math.Min(position + 1, width - 1);
            float axial = (Unsafe.Add(ref m, left) + Unsafe.Add(ref m, right))
                + (Unsafe.Add(ref t, position) + Unsafe.Add(ref b, position));

            float diagonal = (Unsafe.Add(ref t, left) + Unsafe.Add(ref t, right))
                + (Unsafe.Add(ref b, left) + Unsafe.Add(ref b, right));

            Unsafe.Add(ref output, position) = operation.Invoke(Unsafe.Add(ref m, position), axial, diagonal);
        }
    }

    /// <summary>
    /// Defines the normalized center, four axial-neighbor and four diagonal-neighbor weights of one Gaborish component.
    /// </summary>
    private readonly struct GaborishOperator
    {
        private readonly float center;
        private readonly float axial;
        private readonly float diagonal;

        /// <summary>
        /// Initializes a new instance of the <see cref="GaborishOperator"/> struct with the native kernel normalization.
        /// </summary>
        /// <param name="axial">The encoded horizontal/vertical neighbor weight.</param>
        /// <param name="diagonal">The encoded diagonal neighbor weight.</param>
        public GaborishOperator(float axial, float diagonal)
        {
            this.center = 1F / (1F + (4F * (axial + diagonal)));
            this.axial = axial * this.center;
            this.diagonal = diagonal * this.center;
        }

        /// <summary>
        /// Combines one center sample and the four-sample axial and diagonal sums.
        /// </summary>
        /// <param name="value">The center sample.</param>
        /// <param name="axialSum">The four axial samples, grouped as horizontal plus vertical pairs.</param>
        /// <param name="diagonalSum">The four diagonal samples, grouped by upper and lower rows.</param>
        /// <returns>The normalized output sample.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value, float axialSum, float diagonalSum)
            => float.MultiplyAddEstimate(diagonalSum, this.diagonal, float.MultiplyAddEstimate(axialSum, this.axial, value * this.center));

        /// <inheritdoc cref="Invoke(float,float,float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value, Vector128<float> axialSum, Vector128<float> diagonalSum)
            => Vector128.MultiplyAddEstimate(
                diagonalSum,
                Vector128.Create(this.diagonal),
                Vector128.MultiplyAddEstimate(axialSum, Vector128.Create(this.axial), value * Vector128.Create(this.center)));

        /// <inheritdoc cref="Invoke(float,float,float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value, Vector256<float> axialSum, Vector256<float> diagonalSum)
            => Vector256.MultiplyAddEstimate(
                diagonalSum,
                Vector256.Create(this.diagonal),
                Vector256.MultiplyAddEstimate(axialSum, Vector256.Create(this.axial), value * Vector256.Create(this.center)));

        /// <inheritdoc cref="Invoke(float,float,float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value, Vector512<float> axialSum, Vector512<float> diagonalSum)
            => Vector512.MultiplyAddEstimate(
                diagonalSum,
                Vector512.Create(this.diagonal),
                Vector512.MultiplyAddEstimate(axialSum, Vector512.Create(this.axial), value * Vector512.Create(this.center)));
    }
}
