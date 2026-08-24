// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Predicts an 8-bit AV1 block from the rounded average of its available top neighboring samples.
/// </summary>
internal class Av1DcTopPredictor : IAv1Predictor
{
    /// <summary>
    /// The number of top samples averaged and samples written to each destination row.
    /// </summary>
    private readonly uint blockWidth;

    /// <summary>
    /// The number of destination rows.
    /// </summary>
    private readonly uint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcTopPredictor"/> class for explicit block dimensions.
    /// </summary>
    /// <param name="blockSize">The predicted block dimensions in samples.</param>
    public Av1DcTopPredictor(Size blockSize)
    {
        this.blockWidth = (uint)blockSize.Width;
        this.blockHeight = (uint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DcTopPredictor"/> class for a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size whose dimensions define the predicted block.</param>
    public Av1DcTopPredictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (uint)transformSize.GetWidth();
        this.blockHeight = (uint)transformSize.GetHeight();
    }

    /// <summary>
    /// Predicts a transform block from its top neighboring samples.
    /// </summary>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top neighboring samples.</param>
    /// <param name="left">The unused left-neighbor buffer required by the common predictor signature.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
        => new Av1DcTopPredictor(transformSize).PredictScalar(destination, stride, above, left);

    /// <inheritdoc/>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left)
    {
        int sum = 0;
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(above, (int)this.blockWidth, nameof(above));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        ref byte aboveRef = ref above[0];
        ref byte destinationRef = ref destination[0];
        for (uint i = 0; i < this.blockWidth; i++)
        {
            sum += Unsafe.Add(ref aboveRef, i);
        }

        // Adding half the sample count implements the normative nearest-integer DC average before division.
        byte expectedDc = (byte)((sum + (this.blockWidth >> 1)) / this.blockWidth);
        for (uint r = 0; r < this.blockHeight; r++)
        {
            Unsafe.InitBlock(ref destinationRef, expectedDc, this.blockWidth);
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
