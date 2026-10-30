// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Applies the encoder's approximate inverse Gaborish filter.
/// </summary>
internal static class JxlGaborish
{
    private static ReadOnlySpan<float> GaborishLookup => [
        -0.09495815671340026f, -0.041031725066768575f, 0.013710004822696948f,
        0.006510206083837737f, -0.0014789063378272242f];

    /// <summary>
    /// Filters an expanded rectangle in place while retaining the original neighboring samples in distinct planes.
    /// </summary>
    /// <param name="configuration">The configuration that owns temporary plane storage.</param>
    /// <param name="inOut">The image to filter.</param>
    /// <param name="rect">The requested rectangle, expanded by three samples within the image.</param>
    /// <param name="mul">The inverse-filter strength for each channel.</param>
    public static void InverseGaborish(Configuration configuration, JxlImage3<float> inOut, Rectangle rect, InlineArray3<float> mul)
    {
        InlineArray3<JxlWeightsSymmetric5> weights = default;

        for (int i = 0; i < 3; ++i)
        {
            // Native prepares the sum in float, from left to right, before adding the double-precision unit center.
            // Regrouping the lookup entries changes the normalization and every filtered sample.
            float neighborSum = (((GaborishLookup[0] + GaborishLookup[1]) + GaborishLookup[2]) + GaborishLookup[4]) + (2 * GaborishLookup[3]);
            double sum = 1D + (mul[i] * 4 * neighborSum);
            // The native inverse filter floors the sum so normalization remains finite at large filter strengths.
            sum = Math.Max(sum, 1e-5);

            float normalize = (float)(1.0f / sum);
            float normalizeMul = mul[i] * normalize;

            weights[i] = new JxlWeightsSymmetric5()
            {
                C = JxlWeightsSymmetric5.CreateVector4(normalize),
                R = JxlWeightsSymmetric5.CreateVector4(normalizeMul * GaborishLookup[0]),
                R2 = JxlWeightsSymmetric5.CreateVector4(normalizeMul * GaborishLookup[2]),
                D = JxlWeightsSymmetric5.CreateVector4(normalizeMul * GaborishLookup[1]),
                D2 = JxlWeightsSymmetric5.CreateVector4(normalizeMul * GaborishLookup[4]),
                L = JxlWeightsSymmetric5.CreateVector4(normalizeMul * GaborishLookup[3])
            };
        }

        // Keep original blue while red and green results reuse the existing blue and red planes. Each convolution
        // reads distinct storage, so later rows cannot see filtered neighbors. Swap the planes back after all three passes.
        using JxlPlane<float> temp = new(configuration, inOut.Plane(2).XSize, inOut.Plane(2).YSize);

        JxlImageOperations.ForceCopyImage(inOut.Plane(2), temp);

        Rectangle xRect = RectangleUtils.Extend(rect, 3, inOut.GetRectangle());

        if (!JxlConvolve.Symmetric5(inOut.Plane(0), xRect, ref weights[0], inOut.Plane(2), xRect))
        {
            throw new InvalidOperationException("Symmetric5 convolution failed");
        }

        if (!JxlConvolve.Symmetric5(inOut.Plane(1), xRect, ref weights[1], inOut.Plane(0), xRect))
        {
            throw new InvalidOperationException("Symmetric5 convolution failed");
        }

        if (!JxlConvolve.Symmetric5(temp, xRect, ref weights[2], inOut.Plane(1), xRect))
        {
            throw new InvalidOperationException("Symmetric5 convolution failed");
        }

        inOut.Plane(0).Swap(inOut.Plane(1));
        inOut.Plane(0).Swap(inOut.Plane(2));
    }
}
