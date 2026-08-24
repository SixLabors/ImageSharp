// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Produces 8-bit AV1 directional intra predictions for angles in zone 1.
/// </summary>
/// <remarks>
/// Zone 1 projects the top reference samples into the block for prediction angles less than 90 degrees.
/// The scalar prediction follows the directional prediction process in section 7.11.2.4 of the AV1 specification.
/// </remarks>
internal class Av1DirectionalZone1Predictor
{
    /// <summary>
    /// The width of the prediction block in samples.
    /// </summary>
    private readonly nuint blockWidth;

    /// <summary>
    /// The height of the prediction block in samples.
    /// </summary>
    private readonly nuint blockHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DirectionalZone1Predictor"/> class for the specified block dimensions.
    /// </summary>
    /// <param name="blockSize">The dimensions of the prediction block.</param>
    public Av1DirectionalZone1Predictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DirectionalZone1Predictor"/> class for the specified transform size.
    /// </summary>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    public Av1DirectionalZone1Predictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    /// <summary>
    /// Produces an 8-bit zone 1 directional prediction for a transform block.
    /// </summary>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top reference samples, including any required extension.</param>
    /// <param name="upsampleAbove">A value indicating whether the top reference samples were upsampled.</param>
    /// <param name="dx">The horizontal projection derivative in Q6 precision.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> above, bool upsampleAbove, int dx)
        => new Av1DirectionalZone1Predictor(transformSize).PredictScalar(destination, stride, above, upsampleAbove, dx);

    /// <summary>
    /// Produces an 8-bit zone 1 directional prediction for this block.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The top reference samples, including any required extension.</param>
    /// <param name="upsample">A value indicating whether the top reference samples were upsampled.</param>
    /// <param name="dx">The horizontal projection derivative in Q6 precision.</param>
    /// <remarks>Corresponds to <c>svt_av1_dr_prediction_z1_c</c> in SVT-AV1.</remarks>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, bool upsample, int dx)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        int upsampleAbove = upsample ? 1 : 0;
        ref byte aboveRef = ref above[0];
        ref byte destinationRef = ref destination[0];
        int maxBasisX = (((int)this.blockWidth + (int)this.blockHeight) - 1) << upsampleAbove;
        int fractionBitCount = 6 - upsampleAbove;
        int basisIncrement = 1 << upsampleAbove;
        int x = dx;
        for (nuint r = 0; r < this.blockHeight; ++r)
        {
            // AV1 retains six fractional projection bits, or five after reference upsampling.
            // The interpolation weights sum to 32 because the low projection bit is discarded.
            int basis = x >> fractionBitCount, shift = ((x << upsampleAbove) & 0x3F) >> 1;

            if (basis >= maxBasisX)
            {
                for (nuint i = r; i < this.blockHeight; ++i)
                {
                    Unsafe.InitBlock(ref destinationRef, Unsafe.Add(ref aboveRef, maxBasisX), (uint)this.blockWidth);
                    destinationRef = ref Unsafe.Add(ref destinationRef, stride);
                }

                return;
            }

            for (nuint c = 0; c < this.blockWidth; ++c)
            {
                if (basis < maxBasisX)
                {
                    int val;
                    val = (Unsafe.Add(ref aboveRef, basis) * (32 - shift)) + (Unsafe.Add(ref aboveRef, basis + 1) * shift);
                    val = Av1Math.RoundPowerOf2(val, 5);
                    Unsafe.Add(ref destinationRef, c) = (byte)Av1Math.Clamp(val, 0, 255);
                }
                else
                {
                    Unsafe.Add(ref destinationRef, c) = Unsafe.Add(ref aboveRef, maxBasisX);
                }

                basis += basisIncrement;
            }

            x += dx;
            destinationRef = ref Unsafe.Add(ref destinationRef, stride);
        }
    }
}
