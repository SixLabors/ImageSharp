// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Reconstructs non-directional AV1 intra-prediction blocks from prepared neighboring samples.
/// </summary>
/// <remarks>
/// The implementation covers the non-directional prediction processes in section 7.11.2 of the AV1 specification.
/// </remarks>
internal abstract partial class Av1NonDirectionalIntraPredictorBase
{
    /// <summary>
    /// The horizontal prediction operator.
    /// </summary>
    private static readonly Av1NonDirectionalIntraPredictor<HorizontalOperator> HorizontalPredictor = new();

    /// <summary>
    /// The vertical prediction operator.
    /// </summary>
    private static readonly Av1NonDirectionalIntraPredictor<VerticalOperator> VerticalPredictor = new();

    /// <summary>
    /// The Paeth prediction operator.
    /// </summary>
    private static readonly Av1NonDirectionalIntraPredictor<PaethOperator> PaethPredictor = new();

    /// <summary>
    /// The two-dimensional smooth prediction operator.
    /// </summary>
    private static readonly Av1NonDirectionalIntraPredictor<SmoothOperator> SmoothPredictor = new();

    /// <summary>
    /// The horizontal smooth prediction operator.
    /// </summary>
    private static readonly Av1NonDirectionalIntraPredictor<SmoothHorizontalOperator> SmoothHorizontalPredictor = new();

    /// <summary>
    /// The vertical smooth prediction operator.
    /// </summary>
    private static readonly Av1NonDirectionalIntraPredictor<SmoothVerticalOperator> SmoothVerticalPredictor = new();

    /// <summary>
    /// Gets the Q8 smooth weights for every supported block dimension.
    /// </summary>
    private static ReadOnlySpan<int> SmoothWeights =>
    [

        // The first two entries are unused because the smallest AV1 prediction dimension is four samples.
        0, 0,
        255, 128,
        255, 149, 85, 64,
        255, 197, 146, 105, 73, 50, 37, 32,
        255, 225, 196, 170, 145, 123, 102, 84, 68, 54, 43, 33, 26, 20, 17, 16,
        255, 240, 225, 210, 196, 182, 169, 157, 145, 133, 122, 111, 101, 92, 83, 74,
        66, 59, 52, 45, 39, 34, 29, 25, 21, 17, 14, 12, 10, 9, 8, 8,
        255, 248, 240, 233, 225, 218, 210, 203, 196, 189, 182, 176, 169, 163, 156,
        150, 144, 138, 133, 127, 121, 116, 111, 106, 101, 96, 91, 86, 82, 77, 73, 69,
        65, 61, 57, 54, 50, 47, 44, 41, 38, 35, 32, 29, 27, 25, 22, 20, 18, 16, 15,
        13, 12, 10, 9, 8, 7, 6, 6, 5, 5, 4, 4, 4,
    ];

    /// <summary>
    /// Gets the prediction mode implemented by this predictor.
    /// </summary>
    public abstract Av1PredictionMode Mode { get; }

    /// <summary>
    /// Gets the closed predictor for a non-directional AV1 prediction mode.
    /// </summary>
    /// <param name="mode">The decoded non-directional prediction mode.</param>
    /// <returns>The predictor for <paramref name="mode"/>.</returns>
    public static Av1NonDirectionalIntraPredictorBase GetPredictor(Av1PredictionMode mode)
        => mode switch
        {
            Av1PredictionMode.Horizontal => HorizontalPredictor,
            Av1PredictionMode.Vertical => VerticalPredictor,
            Av1PredictionMode.Paeth => PaethPredictor,
            Av1PredictionMode.Smooth => SmoothPredictor,
            Av1PredictionMode.SmoothHorizontal => SmoothHorizontalPredictor,
            Av1PredictionMode.SmoothVertical => SmoothVerticalPredictor,
            _ => throw new InvalidImageContentException($"Prediction mode {mode} is not a non-directional AV1 mode."),
        };

    /// <summary>
    /// Predicts an 8-bit block.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public abstract void Predict(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height);

    /// <summary>
    /// Predicts a high-bit-depth block.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public abstract void Predict(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height);

    /// <summary>
    /// Predicts an 8-bit block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public abstract void PredictScalar(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height);

    /// <summary>
    /// Predicts a high-bit-depth block without hardware intrinsics.
    /// </summary>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride in samples.</param>
    /// <param name="above">The prepared top reference, preceded in memory by the top-left sample.</param>
    /// <param name="left">The prepared left reference.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    public abstract void PredictScalar(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height);
}
