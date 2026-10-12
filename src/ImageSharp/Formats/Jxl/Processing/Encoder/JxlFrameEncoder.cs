// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

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

        // No point using groups when only one group is full and the others are
        // less than half full: multithreading will not really help much, while
        // compression does suffer; but no reason to have group larger than image.
        if (width <= 128 && height <= 128)
        {
            return 0;
        }

        if (width <= 256 && height <= 256)
        {
            return 1;
        }

        if (width <= 400 && height <= 400)
        {
            return 2;
        }

        return 1;
    }

    public static void SimplifyInvisible(JxlImage3F image, JxlImageF alpha, bool lossless)
    {
        // Invisible (alpha = 0) pixels tend to be a mess in optimized PNGs.
        // Since they have no visual impact, we can replace them with
        // something that compresses better and reduces artifacts near the edges. This
        // does some kind of smoothing that seems to work.
        // Replace invisible pixels with a weighted average of the pixel to the left,
        // the pixel to the topright, and non-invisible neighbours.
        // Produces downward-blurry smears, with in the upwards direction only a 1px
        // edge duplication but not more. It would probably be better to smear in all
        // directions. That requires an alpha-weighed convolution with a large enough
        // kernel though, which might be too much.
        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < image.YSize; y++)
            {
                Span<float> row = image.PlaneRow(c, y);
                Span<float> prow = y > 0 ? image.PlaneRow(c, y - 1) : [];
                Span<float> nrow = y + 1 < image.YSize ? image.PlaneRow(c, y + 1) : [];
                Span<float> a = alpha.GetRow(y);
                Span<float> pa = y > 0 ? alpha.GetRow(y - 1) : [];
                Span<float> na = y + 1 < image.YSize ? alpha.GetRow(y + 1) : [];

                for (int x = 0; x < image.XSize; ++x)
                {
                    if (a[x] == 0)
                    {
                        if (lossless)
                        {
                            row[x] = 0;
                            continue;
                        }

                        float d = 0.0f;
                        row[x] = 0;

                        if (x > 0)
                        {
                            row[x] += row[x - 1];
                            d++;

                            if (a[x - 1] > 0.0f)
                            {
                                row[x] += row[x - 1];
                                d++;
                            }
                        }

                        if (x + 1 < image.XSize)
                        {
                            if (y > 0)
                            {
                                row[x] += prow[x + 1];
                                d++;
                            }

                            if (a[x + 1] > 0.0f)
                            {
                                row[x] += 2.0f * row[x + 1];
                                d += 2.0f;
                            }

                            if (y > 0 && pa[x + 1] > 0.0f)
                            {
                                row[x] += 2.0f * prow[x + 1];
                                d += 2.0f;
                            }

                            if (y + 1 < image.YSize && na[x + 1] > 0.0f)
                            {
                                row[x] += 2.0f * nrow[x + 1];
                                d += 2.0f;
                            }
                        }

                        if (y > 0 && pa[x] > 0.0f)
                        {
                            row[x] += 2.0f * prow[x];
                            d += 2.0f;
                        }

                        if (y + 1 < image.YSize && na[x] > 0.0f)
                        {
                            row[x] += 2.0f * nrow[x];
                            d += 2.0f;
                        }

                        if (d > 1.0f)
                        {
                            row[x] /= d;
                        }
                    }
                }
            }
        }
    }
}
