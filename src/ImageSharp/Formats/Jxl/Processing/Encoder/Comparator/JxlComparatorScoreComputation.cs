// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;

internal static class JxlComparatorScoreComputation
{
    private static void AlphaBlend(JxlImage3F input, int c, float backgroundLinear, JxlImageF alpha, JxlImage3F output)
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

    private static void ComputeScoreImpl(Configuration configuration, JxlImageBundle rgb0, JxlImageBundle rgb1, JxlComparator comparator, JxlImageF? diffMap, out float score)
    {
        comparator.SetReferenceImage(rgb0);
        comparator.CompareWith(configuration, rgb1, diffMap, out score);
    }

    public static void ComputeScore(
        Configuration configuration,
        JxlImageBundle rgb0,
        JxlImageBundle rgb1,
        JxlComparator comparator,
        JxlCmsInterface cms,
        out float score,
        JxlImageF? diffMap = null,
        bool ignoreAlpha = false)
    {
        JxlImageMetadata metadata0 = rgb0.Metadata.Copy();

        JxlImageBundle store0 = new(metadata0);

        JxlTransformsEncoder.TransformIfNeeded(
            rgb0,
            JxlColorEncoding.LinearSRgb(rgb0.IsGray),
            cms,
            pool,
            store0,
            out JxlImageBundle linearSrgb0);

        JxlImageMetadata metadata1 = rgb1.Metadata.Copy();

        JxlImageBundle store1 = new(metadata1);

        JxlTransformsEncoder.TransformIfNeeded(
            rgb1,
            JxlColorEncoding.LinearSRgb(rgb1.IsGray),
            cms,
            pool,
            store1,
            out JxlImageBundle linearSrgb1);

        if (ignoreAlpha || (!rgb0.ContainsAlpha && !rgb1.ContainsAlpha))
        {
            ComputeScoreImpl(
                configuration,
                linearSrgb0,
                linearSrgb1,
                comparator,
                diffMap,
                out score);
        }

        JxlImageF diffMapBlack = new();
        float distBlack;
        {
            const float black = 0.0f;

            JxlImageBundle blendedBlack0 = linearSrgb0.Copy(configuration);
            JxlImageBundle blendedBlack1 = linearSrgb1.Copy(configuration);

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

        JxlImageF diffMapWhite = new();
        float distWhite;
        {
            const float white = 1.0f;

            JxlImageBundle blendedWhite0 = linearSrgb0.Copy(configuration);
            JxlImageBundle blendedWhite1 = linearSrgb1.Copy(configuration);

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

            JxlImageF outputDiffMap = new(
                configuration,
                xSize,
                ySize);

            for (int y = 0; y < ySize; y++)
            {
                ReadOnlySpan<float> rowBlack = diffMapBlack.GetRow(y);
                ReadOnlySpan<float> rowWhite = diffMapWhite.GetRow(y);
                Span<float> rowOut = diffMap.GetRow(y);

                TensorPrimitives.Max(rowBlack, rowWhite, rowOut);
            }
        }

        score = MathF.Max(distBlack, distWhite);
    }
}
