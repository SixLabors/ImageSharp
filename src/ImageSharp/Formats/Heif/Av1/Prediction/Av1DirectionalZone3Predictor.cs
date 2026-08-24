// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Produces 8-bit AV1 directional intra predictions for angles in zone 3.
/// </summary>
/// <remarks>
/// Zone 3 projects the left reference samples into the block for prediction angles greater than 180 degrees.
/// The scalar prediction follows the directional prediction process in section 7.11.2.4 of the AV1 specification.
/// </remarks>
internal readonly struct Av1DirectionalZone3Predictor
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
    /// Initializes a new instance of the <see cref="Av1DirectionalZone3Predictor"/> struct for the specified block dimensions.
    /// </summary>
    /// <param name="blockSize">The dimensions of the prediction block.</param>
    public Av1DirectionalZone3Predictor(Size blockSize)
    {
        this.blockWidth = (nuint)blockSize.Width;
        this.blockHeight = (nuint)blockSize.Height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DirectionalZone3Predictor"/> struct for the specified transform size.
    /// </summary>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    public Av1DirectionalZone3Predictor(Av1TransformSize transformSize)
    {
        this.blockWidth = (nuint)transformSize.GetWidth();
        this.blockHeight = (nuint)transformSize.GetHeight();
    }

    /// <summary>
    /// Produces an 8-bit zone 3 directional prediction for a transform block.
    /// </summary>
    /// <param name="transformSize">The transform size that determines the prediction block dimensions.</param>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="left">The left reference samples, including any required extension.</param>
    /// <param name="upsampleLeft">A value indicating whether the left reference samples were upsampled.</param>
    /// <param name="dx">The horizontal projection derivative, which must be one in zone 3.</param>
    /// <param name="dy">The vertical projection derivative in Q6 precision.</param>
    public static void PredictScalar(Av1TransformSize transformSize, Span<byte> destination, nuint stride, Span<byte> left, bool upsampleLeft, int dx, int dy)
        => new Av1DirectionalZone3Predictor(transformSize).PredictScalar(destination, stride, left, upsampleLeft, dx, dy);

    /// <summary>
    /// Produces an 8-bit zone 3 directional prediction for this block.
    /// </summary>
    /// <param name="destination">The buffer that receives the predicted samples.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="left">The left reference samples, including any required extension.</param>
    /// <param name="upsample">A value indicating whether the left reference samples were upsampled.</param>
    /// <param name="dx">The horizontal projection derivative, which must be one in zone 3.</param>
    /// <param name="dy">The vertical projection derivative in Q6 precision.</param>
    /// <remarks>Corresponds to <c>svt_av1_dr_prediction_z3_c</c> in SVT-AV1.</remarks>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> left, bool upsample, int dx, int dy)
    {
        Guard.MustBeGreaterThanOrEqualTo(stride, this.blockWidth, nameof(stride));
        Guard.MustBeSizedAtLeast(left, (int)this.blockHeight, nameof(left));
        Guard.MustBeSizedAtLeast(destination, (int)this.blockHeight * (int)stride, nameof(destination));
        int upsampleLeft = upsample ? 1 : 0;
        ref byte leftRef = ref left[0];
        ref byte destinationRef = ref destination[0];
        Guard.IsTrue(dx == 1, nameof(dx), "Dx expected to be always equal to 1 for directional Zone 3 prediction.");
        Guard.MustBeGreaterThan(dy, 0, nameof(dy));

        int maxBasisY = ((int)this.blockWidth + (int)this.blockHeight - 1) << upsampleLeft;
        int fractionBitCount = 6 - upsampleLeft;
        int basisIncrement = 1 << upsampleLeft;
        int y = dy;
        for (nuint c = 0; c < this.blockWidth; ++c)
        {
            // Zone 3 is the transpose of zone 1: columns advance along the projected left edge,
            // while rows advance through the reference samples for each destination column.
            int basis = y >> fractionBitCount;
            int shift = ((y << upsampleLeft) & 0x3F) >> 1;

            for (nuint r = 0; r < this.blockHeight; ++r)
            {
                if (basis < maxBasisY)
                {
                    int val;
                    val = (Unsafe.Add(ref leftRef, basis) * (32 - shift)) + (Unsafe.Add(ref leftRef, basis + 1) * shift);
                    val = Av1Math.RoundPowerOf2(val, 5);
                    Unsafe.Add(ref destinationRef, (r * stride) + c) = (byte)Av1Math.Clamp(val, 0, 255);
                }
                else
                {
                    for (; r < this.blockHeight; ++r)
                    {
                        Unsafe.Add(ref destinationRef, (r * stride) + c) = left[maxBasisY];
                    }

                    break;
                }

                basis += basisIncrement;
            }

            y += dy;
        }
    }
}
