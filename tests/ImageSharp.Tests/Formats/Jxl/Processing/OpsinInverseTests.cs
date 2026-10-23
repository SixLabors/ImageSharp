// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class OpsinInverseTests
{
    /// <summary>
    /// Converts Y'Cb'Cr to RGB without any SIMD/parallel acceleration. This is
    /// a reference, so it's meant to be stable. The conversion is performed
    /// as specified in ITU-R BT.601.
    /// </summary>
    /// <param name="yCbCr">Input Y'Cb'Cr source.</param>
    /// <param name="rgb">Output RGB.</param>
    /// <param name="rect">Region in which conversion will happen.</param>
    private static void SlowYCbCrToRgb(JxlImage3F yCbCr, JxlImage3F rgb, Rectangle rect)
    {
        int width = rect.Width;
        int height = rect.Height;

        if (width == 0 || height == 0)
        {
            // When either dimension has 0 pixels, there's really
            // nothing to process. So return immediately.
            return;
        }

        // Defining constants used by the conversion process
        // as specified by ITU-R BT.601
        const float c128 = 128.0f / 255;
        const float crcr = 1.402f;
        const float cgcb = -0.114f * 1.772f / 0.587f;
        const float cgcr = -0.299f * 1.402f / 0.587f;
        const float cbcb = 1.772f;

        // Main conversion loop here
        for (int y = 0; y < height; y++)
        {
            // First we retrieve the input Y'Cb'Cr rows
            // for each channel.
            // NOTE: The Y'Cb'Cr planes are loaded in the
            // { 1, 0, 2 } order instead of the traditional
            // { 0, 1, 2 } order. See
            // https://github.com/libjxl/libjxl/blob/777e88f46a6b35948d1967afd9ac5c7fb5595d95/lib/jxl/opsin_inverse_test.cc#L69-L71.
            Span<float> yRow = yCbCr.Plane(1).GetRow(rect, y);
            Span<float> cbRow = yCbCr.Plane(0).GetRow(rect, y);
            Span<float> crRow = yCbCr.Plane(2).GetRow(rect, y);

            // Similarly, we retrieve the output rows. Conversion
            // outputs go here.
            Span<float> rRow = rgb.Plane(0).GetRow(rect, y);
            Span<float> gRow = rgb.Plane(1).GetRow(rect, y);
            Span<float> bRow = rgb.Plane(2).GetRow(rect, y);

            for (int x = 0; x < width; x++)
            {
                // Input Y'Cb'Cr pixels
                float yValue = yRow[x] + c128;
                float cbValue = cbRow[x];
                float crValue = crRow[x];

                // The pixels are converted here.
                float rVec = (crcr * crValue) + yValue;
                float gVec = (cgcr * crValue) + (cgcb * cbValue) + yValue;
                float bVec = (cbcb * cbValue) + yValue;

                // Storing them back to the output rows
                rRow[x] = rVec;
                gRow[x] = gVec;
                bRow[x] = bVec;
            }
        }
    }
}
