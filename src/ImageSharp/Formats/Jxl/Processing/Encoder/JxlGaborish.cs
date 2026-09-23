// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Gaborish transform
/// </summary>
internal static class JxlGaborish
{
    private static ReadOnlySpan<float> GaborishLookup => [
        -0.09495815671340026f, -0.041031725066768575f, 0.013710004822696948f,
        0.006510206083837737f, -0.0014789063378272242f];

    public static void InverseGaborish(Configuration configuration, JxlImage3F inOut, Rectangle rect, InlineArray3<float> mul)
    {
        InlineArray3<JxlWeightsSymmetric5> weights = default;

        for (int i = 0; i < 3; ++i)
        {
            double sum = 1.0 + (mul[i] * 4 * ((GaborishLookup[0] + GaborishLookup[1]) + (GaborishLookup[2] + GaborishLookup[4]) + (2 * GaborishLookup[3])));
            sum = Math.Max(sum, 1e-5); // if (sum < 1e-5) sum = 1e-5

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

        using JxlImageF temp = new(configuration, inOut.Plane(2).XSize, inOut.Plane(2).YSize);

        if (!JxlImageOperations.CopyImage(inOut.Plane(2), temp))
        {
            throw new InvalidOperationException("Image copying failed");
        }

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
