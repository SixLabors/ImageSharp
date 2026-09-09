// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefEncoder
{
    /// <summary>
    /// Supplies sample-width-specific copying, filtering, and error measurement for the frame traversal.
    /// </summary>
    /// <typeparam name="TSample">The native component storage type.</typeparam>
    public interface IEncodingOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Copies a source rectangle into the bordered sixteen-bit filtering workspace.
        /// </summary>
        /// <param name="source">The source rectangle's first sample and remaining plane storage.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="destination">The first destination sample and remaining workspace.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        public static abstract void Copy(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            Span<ushort> destination,
            int destinationStride,
            int width,
            int height);

        /// <summary>
        /// Filters one block into native component storage.
        /// </summary>
        /// <param name="source">The bordered source workspace.</param>
        /// <param name="offset">The block's source offset.</param>
        /// <param name="destination">The block's first destination sample and remaining storage.</param>
        /// <param name="stride">The destination stride.</param>
        /// <param name="primary">The scaled primary strength.</param>
        /// <param name="secondary">The scaled secondary strength.</param>
        /// <param name="direction">The block direction.</param>
        /// <param name="damping">The plane's scaled damping.</param>
        /// <param name="shift">The number of sample bits above eight.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        public static abstract void Filter(
            ReadOnlySpan<ushort> source,
            int offset,
            Span<TSample> destination,
            int stride,
            int primary,
            int secondary,
            int direction,
            int damping,
            int shift,
            int width,
            int height);

        /// <summary>
        /// Measures unnormalized squared sample error over one rectangle.
        /// </summary>
        /// <param name="source">The source rectangle's first sample and remaining storage.</param>
        /// <param name="sourceStride">The source stride.</param>
        /// <param name="filtered">The filtered rectangle's first sample and remaining storage.</param>
        /// <param name="filteredStride">The filtered stride.</param>
        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        /// <returns>The sum of squared sample differences.</returns>
        public static abstract long GetError(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> filtered,
            int filteredStride,
            int width,
            int height);
    }

    /// <summary>
    /// Selects eight-bit storage operations for the closed frame traversal.
    /// </summary>
    public readonly struct ByteOperator : IEncodingOperator<byte>
    {
        /// <inheritdoc/>
        public static void Copy(ReadOnlySpan<byte> source, int sourceStride, Span<ushort> destination, int destinationStride, int width, int height)
            => Av1CdefFilter.CopyPlane(source, 0, sourceStride, destination, 0, destinationStride, width, height);

        /// <inheritdoc/>
        public static void Filter(
            ReadOnlySpan<ushort> source,
            int offset,
            Span<byte> destination,
            int stride,
            int primary,
            int secondary,
            int direction,
            int damping,
            int shift,
            int width,
            int height)
            => Av1CdefFilter.FilterBlock(
                source,
                offset,
                SourceStride,
                destination,
                0,
                stride,
                primary,
                secondary,
                direction,
                damping,
                damping,
                shift,
                width,
                height);

        /// <inheritdoc/>
        public static long GetError(ReadOnlySpan<byte> source, int sourceStride, ReadOnlySpan<byte> filtered, int filteredStride, int width, int height)
            => Av1ResidualBuilder.SumSquaredError(source, sourceStride, filtered, filteredStride, width, height);
    }

    /// <summary>
    /// Selects high-bit-depth storage operations for the closed frame traversal.
    /// </summary>
    public readonly struct UInt16Operator : IEncodingOperator<ushort>
    {
        /// <inheritdoc/>
        public static void Copy(ReadOnlySpan<ushort> source, int sourceStride, Span<ushort> destination, int destinationStride, int width, int height)
            => Av1CdefFilter.CopyPlane(source, 0, sourceStride, destination, 0, destinationStride, width, height);

        /// <inheritdoc/>
        public static void Filter(
            ReadOnlySpan<ushort> source,
            int offset,
            Span<ushort> destination,
            int stride,
            int primary,
            int secondary,
            int direction,
            int damping,
            int shift,
            int width,
            int height)
            => Av1CdefFilter.FilterBlock(
                source,
                offset,
                SourceStride,
                destination,
                0,
                stride,
                primary,
                secondary,
                direction,
                damping,
                damping,
                shift,
                width,
                height);

        /// <inheritdoc/>
        public static long GetError(ReadOnlySpan<ushort> source, int sourceStride, ReadOnlySpan<ushort> filtered, int filteredStride, int width, int height)
            => Av1ResidualBuilder.SumSquaredError(source, sourceStride, filtered, filteredStride, width, height);
    }
}
