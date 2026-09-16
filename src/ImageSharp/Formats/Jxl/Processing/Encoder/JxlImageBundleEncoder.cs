// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal class JxlImageBundleEncoder
{
    public static bool TryApplyColorTransform(
        Configuration configuration,
        JxlColorEncoding currentEncoding,
        float intensityTarget,
        JxlImage3F color,
        JxlImageF? black,
        Rectangle rect,
        JxlColorEncoding desiredEncoding,
        JxlCmsInterface cms,
        ref JxlImage3F output)
    {
        if (currentEncoding.IsGray != desiredEncoding.IsGray)
        {
            // both must be either grayscale or not grayscale
            // can't have one that's grayscale and the other that's not
            // so we return false to indicate failure
            return false;
        }

        JxlColorSpaceTransform transform = new(cms);
        bool isGray = currentEncoding.IsGray;

        if (output.XSize < rect.Width || output.YSize < rect.Height)
        {
            // dimensions are within bounds
            output = new(configuration, rect.Width, rect.Height);
        }
        else
        {
            // dimensions are out of bounds
            // try to handle this by shrinking the image
            // if shrinking fails too, then return false
            if (!output.ShrinkTo(rect.Width, rect.Height))
            {
                return false;
            }
        }

        transform.Initialize(configuration, currentEncoding, desiredEncoding, intensityTarget, rect.Width);

        // We can't use a ref variable inside of a lambda expression,
        // so we store its value in a variable. The output variable
        // will not be assigned.
        JxlImage3F outValue = output;

        _ = Parallel.For(0, rect.Height, configuration.GetParallelOptions(), y =>
        {
            Span<float> sourceBuffer = transform.GetSourceBuffer();

            if (isGray)
            {
                sourceBuffer = color.Plane(0).GetRow(rect, y);
            }
            else if (currentEncoding.Cmyk)
            {
                DebugGuard.NotNull(black, nameof(black));

                Span<float> row0 = color.Plane(0).GetRow(rect, y);
                Span<float> row1 = color.Plane(1).GetRow(rect, y);
                Span<float> row2 = color.Plane(2).GetRow(rect, y);
                Span<float> row3 = black.GetRow(rect, y);

                for (int x = 0, x4 = 0; x < rect.Width; x++, x4 += 4)
                {
                    sourceBuffer[x4] = row0[x];
                    sourceBuffer[x4 + 1] = row1[x];
                    sourceBuffer[x4 + 2] = row2[x];
                    sourceBuffer[x4 + 3] = row3[x];
                }
            }
            else
            {
                Span<float> row0 = color.Plane(0).GetRow(rect, y);
                Span<float> row1 = color.Plane(1).GetRow(rect, y);
                Span<float> row2 = color.Plane(2).GetRow(rect, y);

                for (int x = 0, x3 = 0; x < rect.Width; x++, x3 += 3)
                {
                    sourceBuffer[x3] = row0[x];
                    sourceBuffer[x3 + 1] = row1[x];
                    sourceBuffer[x3 + 2] = row2[x];
                }
            }

            Span<float> destBuffer = transform.GetDestinationBuffer();

            if (!transform.Run(sourceBuffer, destBuffer, rect.Width))
            {
                throw new InvalidOperationException("Could not run color space transform");
            }

            Span<float> rowOut0 = outValue.PlaneRow(0, y);
            Span<float> rowOut1 = outValue.PlaneRow(1, y);
            Span<float> rowOut2 = outValue.PlaneRow(2, y);

            if (isGray)
            {
                Span<float> bufferSlice = destBuffer[..rect.Width];
                bufferSlice.CopyTo(rowOut0);
                bufferSlice.CopyTo(rowOut1);
                bufferSlice.CopyTo(rowOut2);
            }
            else
            {
                for (int x = 0, x3 = 0; x < rect.Width; x++, x3 += 3)
                {
                    rowOut0[x] = destBuffer[x3];
                    rowOut1[x] = destBuffer[x3 + 1];
                    rowOut2[x] = destBuffer[x3 + 2];
                }
            }
        });

        return false;
    }
}
