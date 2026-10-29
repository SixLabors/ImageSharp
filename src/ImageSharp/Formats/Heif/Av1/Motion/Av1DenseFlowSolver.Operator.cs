// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Defines the arithmetic contracts consumed by the patch traversals.
/// </content>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// Applies the two three-tap passes of one separable gradient filter.
    /// </summary>
    /// <remarks>
    /// The filter is a derivative on one axis and a smoothing on the other. The horizontal and the vertical gradients differ only in which pass
    /// carries which kernel. Thus the two directions are two operators over one traversal. The derivative kernel is <c>{1, 0, -1}</c>, and the
    /// smoothing kernel is <c>{1, 2, 1}</c>.
    /// </remarks>
    internal interface IAv1SobelOperator
    {
        /// <summary>
        /// Filters one sample along a row.
        /// </summary>
        /// <param name="left">The sample one column before the output.</param>
        /// <param name="centre">The sample at the output column.</param>
        /// <param name="right">The sample one column after the output.</param>
        /// <returns>The filtered sample.</returns>
        public static abstract int FilterRow(int left, int centre, int right);

        /// <summary>
        /// Filters one row of eight samples along the rows.
        /// </summary>
        /// <param name="left">The samples one column before the outputs.</param>
        /// <param name="centre">The samples at the output columns.</param>
        /// <param name="right">The samples one column after the outputs.</param>
        /// <returns>The filtered samples.</returns>
        public static abstract Vector128<short> FilterRow(Vector128<short> left, Vector128<short> centre, Vector128<short> right);

        /// <summary>
        /// Filters one sample along a column.
        /// </summary>
        /// <param name="above">The sample one row before the output.</param>
        /// <param name="centre">The sample at the output row.</param>
        /// <param name="below">The sample one row after the output.</param>
        /// <returns>The filtered sample.</returns>
        public static abstract int FilterColumn(int above, int centre, int below);

        /// <summary>
        /// Filters one row of eight samples along the columns.
        /// </summary>
        /// <param name="above">The samples one row before the outputs.</param>
        /// <param name="centre">The samples at the output row.</param>
        /// <param name="below">The samples one row after the outputs.</param>
        /// <returns>The filtered samples.</returns>
        public static abstract Vector128<short> FilterColumn(Vector128<short> above, Vector128<short> centre, Vector128<short> below);
    }

    /// <summary>
    /// Applies one four-tap cubic interpolation with a rounded right shift.
    /// </summary>
    /// <remarks>
    /// Each patch computes the taps from the fractional part of the current flow vector, so the taps are arguments, not constants. Both interpolation
    /// passes of the warp use this operator. They differ only in the shift.
    /// </remarks>
    internal interface IAv1CubicOperator
    {
        /// <summary>
        /// Interpolates one sample.
        /// </summary>
        /// <param name="s0">The sample one position before the window.</param>
        /// <param name="s1">The first sample inside the window.</param>
        /// <param name="s2">The second sample inside the window.</param>
        /// <param name="s3">The sample one position after the window.</param>
        /// <param name="k0">The tap of <paramref name="s0"/>.</param>
        /// <param name="k1">The tap of <paramref name="s1"/>.</param>
        /// <param name="k2">The tap of <paramref name="s2"/>.</param>
        /// <param name="k3">The tap of <paramref name="s3"/>.</param>
        /// <param name="roundBits">The rounded right shift applied to the weighted sum.</param>
        /// <returns>The interpolated sample.</returns>
        public static abstract int Filter(int s0, int s1, int s2, int s3, int k0, int k1, int k2, int k3, int roundBits);

        /// <summary>
        /// Interpolates four samples.
        /// </summary>
        /// <param name="s0">The samples one position before the windows.</param>
        /// <param name="s1">The first samples inside the windows.</param>
        /// <param name="s2">The second samples inside the windows.</param>
        /// <param name="s3">The samples one position after the windows.</param>
        /// <param name="k0">The tap of <paramref name="s0"/> in every lane.</param>
        /// <param name="k1">The tap of <paramref name="s1"/> in every lane.</param>
        /// <param name="k2">The tap of <paramref name="s2"/> in every lane.</param>
        /// <param name="k3">The tap of <paramref name="s3"/> in every lane.</param>
        /// <param name="roundBits">The rounded right shift applied to the weighted sums.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector128<int> Filter(
            Vector128<int> s0,
            Vector128<int> s1,
            Vector128<int> s2,
            Vector128<int> s3,
            Vector128<int> k0,
            Vector128<int> k1,
            Vector128<int> k2,
            Vector128<int> k3,
            int roundBits);
    }

    /// <summary>
    /// Loads eight samples of one plane row as signed sixteen-bit lanes.
    /// </summary>
    /// <param name="plane">The first sample of the storage of the level.</param>
    /// <param name="offset">The sample offset, which is never negative.</param>
    /// <returns>The widened samples.</returns>
    /// <remarks>
    /// The method reads exactly eight bytes, so a load never reads past the samples that the filter taps need. The samples are unsigned and less
    /// than 256, so the signed view of the widened lanes is exact.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> WidenSamples(ref byte plane, nuint offset)
        => Vector128.WidenLower(Vector128.Create(Vector64.LoadUnsafe(ref plane, offset), Vector64<byte>.Zero)).AsInt16();

    /// <summary>
    /// Fills one patch of gradients with the arithmetic of a closed gradient operator.
    /// </summary>
    /// <typeparam name="TOperator">The gradient direction.</typeparam>
    /// <remarks>
    /// A patch row is eight samples, which is exactly one 128-bit vector of sixteen-bit lanes. A wider vector spans two patch rows. Both passes
    /// read a row on each side of the row that they produce. Thus packed rows need cross-row shifts that cost more than they save, so no wider path
    /// exists. The intermediate stays sixteen bits wide and wraps at sixteen bits. This keeps the gradients bit-exact with other AV1 encoders.
    /// </remarks>
    private static class Sobel<TOperator>
        where TOperator : struct, IAv1SobelOperator
    {
        /// <summary>
        /// The number of intermediate rows, one above and one below the patch.
        /// </summary>
        private const int IntermediateRows = PatchSize + 2;

        /// <summary>
        /// Fills one patch of gradients.
        /// </summary>
        /// <param name="plane">The whole storage of the level.</param>
        /// <param name="patch">The index of the first sample of the patch.</param>
        /// <param name="stride">The row stride of the level.</param>
        /// <param name="destination">Receives the gradients, eight per row.</param>
        public static void Apply(ReadOnlySpan<byte> plane, int patch, int stride, Span<short> destination)
        {
            // The first pass covers ten rows, one above and one below the patch. The second pass reads a row on each side of every row that it
            // writes. Index zero holds the row above the patch, so every later read is offset by one row.
            Span<short> intermediate = stackalloc short[PatchSize * IntermediateRows];

            if (Vector128.IsHardwareAccelerated)
            {
                ApplyVector128(plane, patch, stride, destination, intermediate);
                return;
            }

            ApplyScalar(plane, patch, stride, destination, intermediate);
        }

        /// <summary>
        /// Fills one patch of gradients one row at a time.
        /// </summary>
        /// <param name="plane">The whole storage of the level.</param>
        /// <param name="patch">The index of the first sample of the patch.</param>
        /// <param name="stride">The row stride of the level.</param>
        /// <param name="destination">Receives the gradients, eight per row.</param>
        /// <param name="intermediate">The ten rows written by the first pass.</param>
        private static void ApplyVector128(
            ReadOnlySpan<byte> plane,
            int patch,
            int stride,
            Span<short> destination,
            Span<short> intermediate)
        {
            ref byte planeBase = ref MemoryMarshal.GetReference(plane);
            ref short intermediateBase = ref MemoryMarshal.GetReference(intermediate);
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);

            for (int row = -1; row < PatchSize + 1; row++)
            {
                // The three taps are the samples one column before, at, and one column after each output. Thus they are three overlapping loads
                // of the same row and need no shuffle.
                nuint rowOffset = (nuint)(patch + (row * stride));
                Vector128<short> left = WidenSamples(ref planeBase, rowOffset - 1);
                Vector128<short> centre = WidenSamples(ref planeBase, rowOffset);
                Vector128<short> right = WidenSamples(ref planeBase, rowOffset + 1);

                TOperator.FilterRow(left, centre, right)
                    .StoreUnsafe(ref intermediateBase, (nuint)((row + 1) * PatchSize));
            }

            for (int row = 0; row < PatchSize; row++)
            {
                // Output row r reads intermediate rows r-1, r and r+1. The intermediate begins one row above the patch, so these rows are stored at
                // r, r+1 and r+2.
                Vector128<short> above = Vector128.LoadUnsafe(ref intermediateBase, (nuint)(row * PatchSize));
                Vector128<short> centre = Vector128.LoadUnsafe(ref intermediateBase, (nuint)((row + 1) * PatchSize));
                Vector128<short> below = Vector128.LoadUnsafe(ref intermediateBase, (nuint)((row + 2) * PatchSize));

                TOperator.FilterColumn(above, centre, below)
                    .StoreUnsafe(ref destinationBase, (nuint)(row * PatchSize));
            }
        }

        /// <summary>
        /// Fills one patch of gradients one sample at a time.
        /// </summary>
        /// <param name="plane">The whole storage of the level.</param>
        /// <param name="patch">The index of the first sample of the patch.</param>
        /// <param name="stride">The row stride of the level.</param>
        /// <param name="destination">Receives the gradients, eight per row.</param>
        /// <param name="intermediate">The ten rows written by the first pass.</param>
        /// <remarks>
        /// The intermediate is sixteen bits wide here too. Thus a runtime without acceleration wraps at the same points as the vector path.
        /// </remarks>
        private static void ApplyScalar(
            ReadOnlySpan<byte> plane,
            int patch,
            int stride,
            Span<short> destination,
            Span<short> intermediate)
        {
            for (int row = -1; row < PatchSize + 1; row++)
            {
                int rowOffset = patch + (row * stride);
                for (int column = 0; column < PatchSize; column++)
                {
                    intermediate[((row + 1) * PatchSize) + column] = (short)TOperator.FilterRow(
                        plane[rowOffset + column - 1],
                        plane[rowOffset + column],
                        plane[rowOffset + column + 1]);
                }
            }

            for (int row = 0; row < PatchSize; row++)
            {
                for (int column = 0; column < PatchSize; column++)
                {
                    destination[(row * PatchSize) + column] = (short)TOperator.FilterColumn(
                        intermediate[(row * PatchSize) + column],
                        intermediate[((row + 1) * PatchSize) + column],
                        intermediate[((row + 2) * PatchSize) + column]);
                }
            }
        }
    }
}
