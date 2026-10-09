// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Builds AV1 inter-intra prediction masks.
/// </content>
internal static partial class Av1InterIntraMaskBuilder
{
    /// <summary>
    /// Gets the AV1 inter-intra weight curve sampled at every fourth entry. These are the weights of a 32-sample block.
    /// </summary>
    private static ReadOnlySpan<byte> Weights32 =>
    [
        60, 52, 45, 39, 34, 30, 26, 22, 19, 17, 15, 13, 11, 10, 8, 7,
        6, 6, 5, 4, 4, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1, 1,
    ];

    /// <summary>
    /// Gets the inter-intra weight curve sampled at every eighth entry. These are the weights of a 16-sample block.
    /// </summary>
    private static ReadOnlySpan<byte> Weights16 => [60, 45, 34, 26, 19, 15, 11, 8, 6, 5, 4, 3, 2, 2, 1, 1];

    /// <summary>
    /// Gets the inter-intra weight curve sampled at every sixteenth entry. These are the weights of an 8-sample block.
    /// </summary>
    private static ReadOnlySpan<byte> Weights8 => [60, 34, 19, 11, 6, 4, 2, 1];

    /// <summary>
    /// Gets the inter-intra weight curve sampled at every thirty-second entry. These are the weights of a 4-sample block.
    /// </summary>
    private static ReadOnlySpan<byte> Weights4 => [60, 19, 6, 2];

    /// <summary>
    /// Fills a smooth inter-intra mask for one plane.
    /// </summary>
    /// <param name="mask">The mask destination.</param>
    /// <param name="maskStride">The distance between mask rows.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="mode">The inter-intra mode.</param>
    /// <param name="invert">Whether to complement the mask.</param>
    public static void FillInterIntraMask(
        Span<byte> mask,
        int maskStride,
        int width,
        int height,
        Av1InterIntraMode mode,
        bool invert)
    {
        // The alpha at row r and column c is weights[r], weights[c] or weights[min(r, c)]. The weights are the curve sampled at the scale of the block size.
        // Each mask row is therefore a weight-row copy, a fill, or a prefix copy followed by a fill.
        ReadOnlySpan<byte> curve = Math.Max(width, height) switch
        {
            32 => Weights32,
            16 => Weights16,
            8 => Weights8,
            _ => Weights4,
        };

        Span<byte> weights = stackalloc byte[32];
        weights = weights[..curve.Length];
        curve.CopyTo(weights);
        if (invert)
        {
            TensorPrimitives.Subtract((byte)MaximumMaskAlpha, weights, weights);
        }

        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            switch (mode)
            {
                case Av1InterIntraMode.Vertical:
                    maskRow.Fill(weights[row]);
                    break;
                case Av1InterIntraMode.Horizontal:
                    weights[..width].CopyTo(maskRow);
                    break;
                case Av1InterIntraMode.Smooth:
                    int split = Math.Min(row, width);
                    weights[..split].CopyTo(maskRow);
                    maskRow[split..].Fill(weights[row]);
                    break;
                default:
                    // The DC mode weights both predictions equally, and 64 - 32 is 32.
                    maskRow.Fill(MaximumMaskAlpha / 2);
                    break;
            }
        }
    }
}
