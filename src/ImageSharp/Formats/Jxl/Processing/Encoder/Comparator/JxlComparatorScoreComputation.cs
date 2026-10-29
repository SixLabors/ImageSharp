// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;

/// <summary>
/// Scores borrowed images, including their appearance on black and white backgrounds when alpha is present.
/// </summary>
internal static class JxlComparatorScoreComputation
{
    /// <summary>
    /// Blends one linear-light plane over a constant background in the gamma-compressed comparison space.
    /// </summary>
    /// <param name="input">The borrowed linear-light color planes.</param>
    /// <param name="c">The selected color plane.</param>
    /// <param name="backgroundLinear">The constant linear-light background component.</param>
    /// <param name="alpha">The borrowed opacity plane.</param>
    /// <param name="output">The result planes. The selected plane can also supply the input.</param>
    private static void AlphaBlend(JxlImage3<float> input, int c, float backgroundLinear, JxlPlane<float> alpha, JxlImage3<float> output)
    {
        float background = (float)JxlGammaCorrect.LinearToSRgb8Direct(backgroundLinear);
        ReadOnlySpan<float> alphaSamples = alpha.GetRow(0);
        JxlPlane<float> source = input.Plane(c);
        JxlPlane<float> destination = output.Plane(c);
        ReadOnlySpan<float> inputSamples = source.GetRow(0);
        Span<float> outputSamples = destination.GetRow(0);

        for (int y = 0; y < output.YSize; y++)
        {
            ReadOnlySpan<float> rowA = alphaSamples[(y * alpha.PixelsPerRow)..];
            ReadOnlySpan<float> rowI = inputSamples[(y * source.PixelsPerRow)..];
            Span<float> rowO = outputSamples[(y * destination.PixelsPerRow)..];

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

    /// <summary>
    /// Flattens an owned comparison copy over a constant background without changing its source bundle.
    /// </summary>
    /// <param name="backgroundLinear">The constant linear-light background component.</param>
    /// <param name="image">The mutable comparison copy.</param>
    private static void AlphaBlend(float backgroundLinear, JxlImageBundle image)
    {
        if (!image.ContainsAlpha)
        {
            return;
        }

        for (int c = 0; c < 3; c++)
        {
            AlphaBlend(image.Color, c, backgroundLinear, image.Alpha!, image.Color);
        }
    }

    /// <summary>
    /// Prepares a reference decomposition and writes the comparison into already allocated result storage.
    /// </summary>
    /// <param name="configuration">The configuration that owns comparison storage.</param>
    /// <param name="rgb0">The borrowed reference samples.</param>
    /// <param name="rgb1">The borrowed distorted samples.</param>
    /// <param name="comparator">The owner that retains the reference decomposition.</param>
    /// <param name="diffMap">The result plane, allocated at the reference dimensions.</param>
    /// <param name="score">The computed perceptual score.</param>
    private static void ComputeScoreImpl(
        Configuration configuration,
        JxlImageBundle rgb0,
        JxlImageBundle rgb1,
        JxlComparator comparator,
        JxlPlane<float> diffMap,
        out float score)
    {
        comparator.SetReferenceImage(configuration, rgb0);
        comparator.CompareWith(configuration, rgb1, diffMap, out score);
    }

    /// <summary>
    /// Computes a perceptual score, retaining the larger black- or white-background error when alpha is included.
    /// </summary>
    /// <param name="configuration">The configuration that owns temporary comparison copies and maps.</param>
    /// <param name="rgb0">The borrowed reference bundle, which remains unchanged.</param>
    /// <param name="rgb1">The borrowed distorted bundle, which remains unchanged.</param>
    /// <param name="comparator">The reusable comparison owner. This operation replaces its reference.</param>
    /// <param name="cms">The color conversion provider for the source encodings.</param>
    /// <param name="score">The computed perceptual score.</param>
    /// <param name="diffMap">The owner that receives the result plane, or null when only the score is needed.</param>
    /// <param name="ignoreAlpha">Whether to compare color samples without background blending.</param>
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
            // This standalone operation owns its result. Search callers instead retain their result map across comparisons.
            using JxlPlane<float> result = new(configuration, rgb0.XSize, rgb0.YSize);
            ComputeScoreImpl(configuration, linearSrgb0, linearSrgb1, comparator, result, out score);

            diffMap?.Swap(result);

            // No background comparison is required when opacity is absent or deliberately ignored.
            return;
        }

        using JxlPlane<float> diffMapBlack = new(configuration, rgb0.XSize, rgb0.YSize);
        float distBlack;
        {
            const float black = 0.0f;

            using JxlImageBundle blendedBlack0 = linearSrgb0.Copy(configuration);
            using JxlImageBundle blendedBlack1 = linearSrgb1.Copy(configuration);

            AlphaBlend(black, blendedBlack0);
            AlphaBlend(black, blendedBlack1);

            ComputeScoreImpl(configuration, blendedBlack0, blendedBlack1, comparator, diffMapBlack, out distBlack);
        }

        using JxlPlane<float> diffMapWhite = new(configuration, rgb0.XSize, rgb0.YSize);
        float distWhite;
        {
            const float white = 1.0f;

            using JxlImageBundle blendedWhite0 = linearSrgb0.Copy(configuration);
            using JxlImageBundle blendedWhite1 = linearSrgb1.Copy(configuration);

            AlphaBlend(white, blendedWhite0);
            AlphaBlend(white, blendedWhite1);

            ComputeScoreImpl(configuration, blendedWhite0, blendedWhite1, comparator, diffMapWhite, out distWhite);
        }

        if (diffMap is not null)
        {
            int xSize = rgb0.XSize;
            int ySize = rgb0.YSize;

            using JxlPlane<float> outputDiffMap = new(configuration, xSize, ySize);
            ReadOnlySpan<float> blackSamples = diffMapBlack.GetRow(0);
            ReadOnlySpan<float> whiteSamples = diffMapWhite.GetRow(0);
            Span<float> outputSamples = outputDiffMap.GetRow(0);

            for (int y = 0; y < ySize; y++)
            {
                // Select logical rows from the spans borrowed before traversal. Each plane can have a different padded stride.
                ReadOnlySpan<float> rowBlack = blackSamples.Slice(y * diffMapBlack.PixelsPerRow, xSize);
                ReadOnlySpan<float> rowWhite = whiteSamples.Slice(y * diffMapWhite.PixelsPerRow, xSize);
                Span<float> rowOut = outputSamples.Slice(y * outputDiffMap.PixelsPerRow, xSize);

                TensorPrimitives.Max(rowBlack, rowWhite, rowOut);
            }

            // The caller receives the computed plane. The temporary takes and releases the caller's previous storage.
            diffMap.Swap(outputDiffMap);
        }

        score = MathF.Max(distBlack, distWhite);
    }
}
