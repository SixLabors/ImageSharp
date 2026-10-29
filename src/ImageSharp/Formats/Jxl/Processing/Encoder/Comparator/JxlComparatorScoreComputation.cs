// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;

internal static class JxlComparatorScoreComputation
{
    private static void AlphaBlend(JxlImage3<float> input, int c, float backgroundLinear, JxlPlane<float> alpha, JxlImage3<float> output)
    {
        float background = (float)JxlGammaCorrect.LinearToSRgb8Direct(backgroundLinear);

        for (int y = 0; y < output.YSize; y++)
        {
            ReadOnlySpan<float> rowA = alpha.GetRow(y);
            ReadOnlySpan<float> rowI = input.PlaneRow(c, y);
            Span<float> rowO = output.PlaneRow(c, y);

            for (int x = 0; x < output.XSize; x++)
            {
                float a = rowA[x];

                if (a <= 0.0f)
                {
                    rowO[x] = backgroundLinear;
                }
                else if (a >= 1.0f)
                {
                    rowO[x] = rowI[x];
                }
                else
                {
                    float wFg = a;
                    float wBg = 1.0f - wFg;
                    float fg = wFg * (float)JxlGammaCorrect.LinearToSRgb8Direct(rowI[x]);
                    float bg = wBg * background;

                    rowO[x] = (float)JxlGammaCorrect.SRgb8ToLinearDirect(fg + bg);
                }
            }
        }
    }

    private static void AlphaBlend(float backgroundLinear, JxlImageBundle image)
    {
        if (!image.ContainsAlpha)
        {
            return;
        }

        for (int c = 0; c < 3; c++)
        {
            AlphaBlend(image.Color!, c, backgroundLinear, image.Alpha!, image.Color!);
        }
    }

    private static void ComputeScoreImpl(
        Configuration configuration,
        JxlImageBundle rgb0,
        JxlImageBundle rgb1,
        JxlComparator comparator,
        JxlPlane<float>? diffMap,
        out float score)
    {
        comparator.SetReferenceImage(configuration, rgb0);
        comparator.CompareWith(configuration, rgb1, diffMap, out score);
    }

    public static void ComputeScore(
        Configuration configuration,
        JxlImageBundle rgb0,
        JxlImageBundle rgb1,
        JxlComparator comparator,
        JxlCmsInterface cms,
        out float score,
        JxlPlane<float>? diffMap = null,
        bool ignoreAlpha = false)
    {
        // Original metadata stays borrowed. Conversion changes the bundle's current encoding, not the shared metadata.
        using JxlImageBundle store0 = new(rgb0.Metadata);
        JxlImageBundle linearSrgb0 = rgb0;
        if (!JxlImageBundleEncoder.TransformIfNeeded(configuration, rgb0, JxlColorEncoding.LinearSrgb(rgb0.IsGray), cms, store0, ref linearSrgb0))
        {
            throw new InvalidOperationException("Could not convert the first comparison image to linear sRGB.");
        }

        using JxlImageBundle store1 = new(rgb1.Metadata);
        JxlImageBundle linearSrgb1 = rgb1;
        if (!JxlImageBundleEncoder.TransformIfNeeded(configuration, rgb1, JxlColorEncoding.LinearSrgb(rgb1.IsGray), cms, store1, ref linearSrgb1))
        {
            throw new InvalidOperationException("Could not convert the second comparison image to linear sRGB.");
        }

        if (ignoreAlpha || (!rgb0.ContainsAlpha && !rgb1.ContainsAlpha))
        {
            ComputeScoreImpl(
                configuration,
                linearSrgb0,
                linearSrgb1,
                comparator,
                diffMap,
                out score);

            // No background comparison is required when opacity is absent or deliberately ignored.
            return;
        }

        using JxlPlane<float> diffMapBlack = new();
        float distBlack;
        {
            const float black = 0.0f;

            using JxlImageBundle blendedBlack0 = linearSrgb0.Copy(configuration);
            using JxlImageBundle blendedBlack1 = linearSrgb1.Copy(configuration);

            AlphaBlend(black, blendedBlack0);
            AlphaBlend(black, blendedBlack1);

            ComputeScoreImpl(
                configuration,
                blendedBlack0,
                blendedBlack1,
                comparator,
                diffMapBlack,
                out distBlack);
        }

        using JxlPlane<float> diffMapWhite = new();
        float distWhite;
        {
            const float white = 1.0f;

            using JxlImageBundle blendedWhite0 = linearSrgb0.Copy(configuration);
            using JxlImageBundle blendedWhite1 = linearSrgb1.Copy(configuration);

            AlphaBlend(white, blendedWhite0);
            AlphaBlend(white, blendedWhite1);

            ComputeScoreImpl(
                configuration,
                blendedWhite0,
                blendedWhite1,
                comparator,
                diffMapWhite,
                out distWhite);
        }

        if (diffMap is not null)
        {
            int xSize = rgb0.XSize;
            int ySize = rgb0.YSize;

            using JxlPlane<float> outputDiffMap = new(configuration, xSize, ySize);

            for (int y = 0; y < ySize; y++)
            {
                // GetRow also exposes following rows. Combine only the current logical row.
                ReadOnlySpan<float> rowBlack = diffMapBlack.GetRow(y)[..xSize];
                ReadOnlySpan<float> rowWhite = diffMapWhite.GetRow(y)[..xSize];
                Span<float> rowOut = outputDiffMap.GetRow(y)[..xSize];

                TensorPrimitives.Max(rowBlack, rowWhite, rowOut);
            }

            // The caller receives the computed plane. The temporary takes and releases the caller's previous storage.
            diffMap.Swap(outputDiffMap);
        }

        score = MathF.Max(distBlack, distWhite);
    }
}
