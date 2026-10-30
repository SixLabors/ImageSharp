// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal class JxlFrameEncoder
{
    public static uint GetGroupSizeShift(int width, int height, JxlCompressParameters cparams)
    {
        if (!cparams.ModularMode)
        {
            return 1u;
        }

        if (cparams.ModularGroupSizeShift >= 0)
        {
            return (uint)cparams.ModularGroupSizeShift;
        }

        if (cparams.DecodingSpeedTier >= 2 ||
            (cparams.DecodingSpeedTier >= 1 && cparams.Responsive == 1 && cparams.IsLossless))
        {
            return 0;
        }

        // Suppress warnings here, as:
        //   (<= 128, <= 128) - produces warning to fix formatting
        //   Applying code fix -> ( <= 128, <= 128) - produces warning, open parentheses must not have whitespace
        //   Applying code fix - produces (<= 128, <= 128). Then first warning again.
#pragma warning disable format
        return (width, height) switch
        {
            (<= 128, <= 128) => 0,
            (<= 256, <= 256) => 1,
            (<= 400, <= 400) => 2,
            _ => 1
        };
#pragma warning restore format
    }

    /// <summary>
    /// Replaces invisible samples in encoder-owned working planes with zeros or the native causal neighbor estimate.
    /// </summary>
    /// <param name="image">The owned working color planes to update.</param>
    /// <param name="alpha">The borrowed unassociated alpha plane with matching logical dimensions.</param>
    /// <param name="lossless">Whether to clear invisible samples instead of estimating them from neighbors.</param>
    /// <remarks>
    /// The encoder caller decides whether invisible samples may change. This method does not select a transparency policy.
    /// </remarks>
    public static void SimplifyInvisible(JxlImage3<float> image, JxlPlane<float> alpha, bool lossless)
    {
        int width = image.XSize;
        int height = image.YSize;
        ReadOnlySpan<float> alphaSamples = alpha.GetRow(0);
        int alphaStride = alpha.PixelsPerRow;

        for (int c = 0; c < 3; c++)
        {
            JxlPlane<float> plane = image.Plane(c);
            Span<float> samples = plane.GetRow(0);
            int stride = plane.PixelsPerRow;

            if (lossless)
            {
                // Independent components share the existing SIMD traversal. Exclude physical padding from both input and output.
                for (int y = 0; y < height; y++)
                {
                    EncodingUtilities.ReplaceTransparentPixels(alphaSamples.Slice(y * alphaStride, width), samples.Slice(y * stride, width));
                }

                continue;
            }

            // Native traversal uses the updated left and previous-row samples, but the original right and next-row samples.
            // Preserve channel, row, and column order. Adjacent transparent pixels cannot be evaluated independently with SIMD.
            for (int y = 0; y < height; y++)
            {
                Span<float> row = samples.Slice(y * stride, width);
                ReadOnlySpan<float> previousRow = y > 0 ? samples.Slice((y - 1) * stride, width) : default;
                ReadOnlySpan<float> nextRow = y + 1 < height ? samples.Slice((y + 1) * stride, width) : default;
                ReadOnlySpan<float> alphaRow = alphaSamples.Slice(y * alphaStride, width);
                ReadOnlySpan<float> previousAlpha = y > 0 ? alphaSamples.Slice((y - 1) * alphaStride, width) : default;
                ReadOnlySpan<float> nextAlpha = y + 1 < height ? alphaSamples.Slice((y + 1) * alphaStride, width) : default;

                for (int x = 0; x < width; ++x)
                {
                    if (alphaRow[x] == 0F)
                    {
                        float weightSum = 0F;
                        row[x] = 0;

                        // Left and upper-right samples extend the existing estimate with weight one, even when invisible.
                        // Visible neighbors receive weight two. Keep the native addition order to preserve its float rounding.
                        if (x > 0)
                        {
                            row[x] += row[x - 1];
                            weightSum++;

                            if (alphaRow[x - 1] > 0F)
                            {
                                row[x] += row[x - 1];
                                weightSum++;
                            }
                        }

                        if (x < width - 1)
                        {
                            if (y > 0)
                            {
                                row[x] += previousRow[x + 1];
                                weightSum++;
                            }

                            if (alphaRow[x + 1] > 0F)
                            {
                                row[x] += 2F * row[x + 1];
                                weightSum += 2F;
                            }

                            if (y > 0 && previousAlpha[x + 1] > 0F)
                            {
                                row[x] += 2F * previousRow[x + 1];
                                weightSum += 2F;
                            }

                            if (y + 1 < height && nextAlpha[x + 1] > 0F)
                            {
                                row[x] += 2F * nextRow[x + 1];
                                weightSum += 2F;
                            }
                        }

                        if (y > 0 && previousAlpha[x] > 0F)
                        {
                            row[x] += 2F * previousRow[x];
                            weightSum += 2F;
                        }

                        if (y + 1 < height && nextAlpha[x] > 0F)
                        {
                            row[x] += 2F * nextRow[x];
                            weightSum += 2F;
                        }

                        // No contributing neighbor leaves zero. A single weight-one contribution needs no division.
                        if (weightSum > 1F)
                        {
                            row[x] /= weightSum;
                        }
                    }
                }
            }
        }
    }
}
