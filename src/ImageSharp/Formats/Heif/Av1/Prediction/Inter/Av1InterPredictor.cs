// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Reconstructs single-reference translational AV1 inter-prediction blocks.
/// </summary>
/// <remarks>
/// <para>
/// <c>sourceOrigin</c> identifies the integer sample selected by motion-vector scaling within the complete
/// padded reference plane. A filtered axis can consume three samples before the block and four samples after it. Byte
/// rows narrower than sixteen samples and 16-bit rows narrower than eight samples must additionally permit a complete
/// 128-bit source load at every selected tap. The frame prediction border provides this storage; no destination padding
/// is required.
/// </para>
/// <para>
/// Two-dimensional filtering uses caller-owned scratch so block reconstruction does not allocate. The scratch span must
/// contain at least <see cref="GetScratchLength(int, int)"/> elements when both phases are nonzero and may be empty for
/// copy or one-dimensional filtering.
/// </para>
/// </remarks>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// The number of fractional bits in each interpolation coefficient.
    /// </summary>
    internal const int FilterBits = 7;

    /// <summary>
    /// The normal first-round shift used by libaom single-reference convolution.
    /// </summary>
    internal const int Round0Bits = 3;

    /// <summary>
    /// The maximum number of source rows added by an eight-tap vertical filter.
    /// </summary>
    private const int MaximumExtraRows = FilterCoefficientCount - 1;

    /// <summary>
    /// The minimum scratch stride that lets a 128-bit byte kernel handle four- and eight-sample blocks.
    /// </summary>
    internal const int MinimumScratchStride = 16;

    /// <summary>
    /// Gets the maximum number of signed 16-bit elements required for one two-dimensional prediction block.
    /// </summary>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <returns>The scratch capacity required by either sample-storage overload.</returns>
    public static int GetScratchLength(int width, int height) => Math.Max(width, MinimumScratchStride) * (height + MaximumExtraRows);

    /// <summary>
    /// Reconstructs an 8-bit translational prediction using the widest supported SIMD kernel.
    /// </summary>
    /// <param name="source">The complete padded reference plane containing every source sample used by the block.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The nonnegative, zero-based index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="scratch">
    /// Caller-owned signed intermediate storage sized by <see cref="GetScratchLength"/> when both phases are nonzero.
    /// </param>
    public static void Predict(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch)
        => Dispatch(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalFilter,
            verticalFilter,
            horizontalPhase,
            verticalPhase,
            scratch);

    /// <summary>
    /// Reconstructs an 8-, 10-, or 12-bit translational prediction using the widest supported SIMD kernel.
    /// </summary>
    /// <param name="source">The complete padded reference plane containing every source sample used by the block.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The nonnegative, zero-based index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="bitDepth">The decoded sample precision: 8, 10, or 12 bits.</param>
    /// <param name="scratch">
    /// Caller-owned signed intermediate storage sized by <see cref="GetScratchLength"/> when both phases are nonzero.
    /// </param>
    public static void Predict(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> scratch)
        => Dispatch(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalFilter,
            verticalFilter,
            horizontalPhase,
            verticalPhase,
            bitDepth,
            scratch);

    /// <summary>
    /// Reconstructs an 8-bit translational prediction without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The complete padded reference plane containing every source sample used by the block.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The nonnegative, zero-based index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="scratch">
    /// Caller-owned signed intermediate storage sized by <see cref="GetScratchLength"/> when both phases are nonzero.
    /// </param>
    public static void PredictScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch)
        => DispatchScalar(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalFilter,
            verticalFilter,
            horizontalPhase,
            verticalPhase,
            scratch);

    /// <summary>
    /// Reconstructs an 8-, 10-, or 12-bit translational prediction without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The complete padded reference plane containing every source sample used by the block.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The nonnegative, zero-based index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="bitDepth">The decoded sample precision: 8, 10, or 12 bits.</param>
    /// <param name="scratch">
    /// Caller-owned signed intermediate storage sized by <see cref="GetScratchLength"/> when both phases are nonzero.
    /// </param>
    public static void PredictScalar(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> scratch)
        => DispatchScalar(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalFilter,
            verticalFilter,
            horizontalPhase,
            verticalPhase,
            bitDepth,
            scratch);
}
