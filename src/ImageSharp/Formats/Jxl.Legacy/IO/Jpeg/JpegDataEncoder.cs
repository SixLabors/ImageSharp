// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

internal static class JpegDataEncoder
{
    public static void SetChromaSubsamplingFromJpegData(JpegData jpg, JxlYCbCrChromaSubsampling cs)
    {
        int numberOfComponents = jpg.Components.Count;

        if (numberOfComponents is not 1 and not 3)
        {
            throw new InvalidOperationException("JPEGs only support 1 or 3 channels");
        }

        if (numberOfComponents == 3)
        {
            Span<byte> hsample = [0, 0, 0];
            Span<byte> vsample = [0, 0, 0];

            for (int i = 0; i < 3; i++)
            {
                hsample[i] = (byte)jpg.Components[i].HorizontalSampleFactor;
                vsample[i] = (byte)jpg.Components[i].VerticalSampleFactor;
            }

            if (!cs.Set(hsample, vsample))
            {
                throw new InvalidOperationException("Could not set horizontal / vertical sample");
            }
        }
        else if (numberOfComponents == 1)
        {
            Span<byte> hsample = [0, 0, 0];
            Span<byte> vsample = [0, 0, 0];

            for (int i = 0; i < 3; i++)
            {
                hsample[i] = (byte)jpg.Components[0].HorizontalSampleFactor;
                vsample[i] = (byte)jpg.Components[0].VerticalSampleFactor;
            }

            if (!cs.Set(hsample, vsample))
            {
                throw new InvalidOperationException("Could not set horizontal / vertical sample");
            }
        }
    }
}
