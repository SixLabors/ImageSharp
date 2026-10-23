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

    public static unsafe void SimplifyInvisible(JxlImage3F image, JxlImageF alpha, bool lossless)
    {
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

                // We create a pointer to 'a', to remove bounds checks in a hot loop.
                // We also use a debug guard, so the range is valid.
                DebugGuard.MustBeGreaterThanOrEqualTo(a.Length, image.XSize, nameof(a));
                fixed (float* unsafeA = a)
                {
                    for (int x = 0; x < image.XSize; ++x)
                    {
                        if (unsafeA[x] == 0)
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

                                if (unsafeA[x - 1] > 0.0f)
                                {
                                    row[x] += row[x - 1];
                                    d++;
                                }
                            }

                            if (x < image.XSize - 1)
                            {
                                if (y > 0)
                                {
                                    row[x] += prow[x + 1];
                                    d++;
                                }

                                if (unsafeA[x + 1] > 0.0f)
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
}
