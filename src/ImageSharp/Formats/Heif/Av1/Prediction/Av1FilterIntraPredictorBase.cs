// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Reconstructs AV1 filter-intra prediction blocks from prepared neighboring samples.
/// </summary>
/// <remarks>
/// The implementation follows the recursive filter-intra process in section 7.11.2.3 of the AV1 specification.
/// </remarks>
internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// The row stride of the recursive prediction workspace.
    /// </summary>
    public const int BufferStride = 33;

    /// <summary>
    /// The number of samples in the recursive prediction workspace.
    /// </summary>
    public const int ScratchLength = BufferStride * BufferStride;

    /// <summary>
    /// The DC filter-intra predictor.
    /// </summary>
    private static readonly Av1FilterIntraPredictor<DcOperator> DcPredictor = new();

    /// <summary>
    /// The vertical filter-intra predictor.
    /// </summary>
    private static readonly Av1FilterIntraPredictor<VerticalOperator> VerticalPredictor = new();

    /// <summary>
    /// The horizontal filter-intra predictor.
    /// </summary>
    private static readonly Av1FilterIntraPredictor<HorizontalOperator> HorizontalPredictor = new();

    /// <summary>
    /// The 157-degree directional filter-intra predictor.
    /// </summary>
    private static readonly Av1FilterIntraPredictor<Directional157Operator> Directional157Predictor = new();

    /// <summary>
    /// The Paeth filter-intra predictor.
    /// </summary>
    private static readonly Av1FilterIntraPredictor<PaethOperator> PaethPredictor = new();

    /// <summary>
    /// Gets the filter-intra mode implemented by this predictor.
    /// </summary>
    public abstract Av1FilterIntraMode Mode { get; }

    /// <summary>
    /// Gets the closed predictor for a filter-intra mode.
    /// </summary>
    /// <param name="mode">The decoded filter-intra mode.</param>
    /// <returns>The predictor for <paramref name="mode"/>.</returns>
    public static Av1FilterIntraPredictorBase GetPredictor(Av1FilterIntraMode mode)
        => mode switch
        {
            Av1FilterIntraMode.DC => DcPredictor,
            Av1FilterIntraMode.Vertical => VerticalPredictor,
            Av1FilterIntraMode.Horizontal => HorizontalPredictor,
            Av1FilterIntraMode.Directional157 => Directional157Predictor,
            Av1FilterIntraMode.Paeth => PaethPredictor,
            _ => throw new InvalidImageContentException($"Filter-intra mode {mode} is not defined by AV1."),
        };

    /// <summary>
    /// Predicts an 8-bit filter-intra block.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="scratch">The caller-owned recursive prediction workspace.</param>
    public abstract void Predict(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height, Span<byte> scratch);

    /// <summary>
    /// Predicts a high-bit-depth filter-intra block.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The reconstructed sample precision.</param>
    /// <param name="scratch">The caller-owned recursive prediction workspace.</param>
    public abstract void Predict(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth, Span<short> scratch);

    /// <summary>
    /// Predicts an 8-bit filter-intra block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="scratch">The caller-owned recursive prediction workspace.</param>
    public abstract void PredictScalar(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height, Span<byte> scratch);

    /// <summary>
    /// Predicts a high-bit-depth filter-intra block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The reconstructed sample precision.</param>
    /// <param name="scratch">The caller-owned recursive prediction workspace.</param>
    public abstract void PredictScalar(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height, int bitDepth, Span<short> scratch);
}
