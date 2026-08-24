// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block with the sample-domain midpoint when neither top nor left neighbors are available.
/// </summary>
internal readonly struct Av1DcFillPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of samples written to each destination row.
    /// </summary>
    private readonly uint blockWidth;

    /// <summary>
    /// The number of destination rows.
    /// </summary>
    private readonly uint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcFillPredictor"/> struct for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1DcFillPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcFillPredictor"/> struct for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1DcFillPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block with the 8-bit midpoint value.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The unused top-neighbor buffer required by the common predictor signature.</param>
    /// <param name="left">The unused left-neighbor buffer required by the common predictor signature.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1DcFillPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        // With no reference edge, AV1 uses the midpoint of the unsigned 8-bit sample domain as the DC predictor.
        const byte expectedDc = 0x80;
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte destinationRef = ref destination[0];
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destinationRef, expectedDc, this.blockWidth);
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
