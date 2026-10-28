// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal class JxlImageBundleEncoder
{
    public static bool TryApplyColorTransform(
        Configuration configuration,
        JxlColorEncoding currentEncoding,
        float intensityTarget,
        JxlImage3<float> color,
        JxlPlane<float>? black,
        Rectangle rect,
        JxlColorEncoding desiredEncoding,
        JxlCmsInterface cms,
        ref JxlImage3<float> output)
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

        for (int y = 0; y < rect.Height; y++)
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

            Span<float> rowOut0 = output.PlaneRow(0, y);
            Span<float> rowOut1 = output.PlaneRow(1, y);
            Span<float> rowOut2 = output.PlaneRow(2, y);

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
        }

        return false;
    }

    // Copies ib:rect, converts, and copies into out.
    public static bool CopyToT(
        Configuration configuration,
        JxlImageMetadata metadata,
        JxlImageBundle ib,
        Rectangle rectangle,
        JxlColorEncoding desiredEncoding,
        JxlCmsInterface cms,
        JxlImage3<float> output)
        => TryApplyColorTransform(
            configuration,
            ib.CurrentColorEncoding!,
            metadata.IntensityTarget,
            ib.Color!,
            ib.Black!,
            rectangle,
            desiredEncoding,
            cms,
            output);

    public static bool TransformIfNeeded(
        Configuration configuration,
        JxlImageBundle input,
        JxlColorEncoding desiredEncoding,
        JxlCmsInterface cms,
        JxlImageBundle store,
        ref JxlImageBundle output)
    {
        if (input.CurrentColorEncoding!.IsSameColorEncoding(desiredEncoding) && !input.ContainsBlack)
        {
            output = input;
            return true;
        }

        using JxlImage3<float> color = new(configuration, input.Color!.XSize, input.Color.YSize);

        if (!JxlImageOperations.CopyImage(input.Color!, color))
        {
            return false;
        }

        if (!store.SetFromImage(color, input.CurrentColorEncoding!))
        {
            return false;
        }

        if (input.ContainsExtraChannels())
        {
            List<JxlPlane<float>> extraChannels = [];

            foreach (JxlPlane<float> extraChannel in input.EnumerateExtraChannels())
            {
                JxlPlane<float> ec = new(configuration, extraChannel.XSize, extraChannel.YSize);

                if (!JxlImageOperations.CopyImage(extraChannel, ec))
                {
                    return false;
                }

                extraChannels.Add(ec);
                extraChannel.Dispose();
            }

            if (!store.TrySetExtraChannels(extraChannels))
            {
                return false;
            }
        }

        if (!store.TransformTo(configuration, desiredEncoding, cms))
        {
            return false;
        }

        output = store;
        return true;
    }
}
