// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

/// <content>
/// Adapts native channel planes to shared vector blending without pixel storage conversion.
/// </content>
internal static partial class JxlBlending
{
    /// <summary>
    /// Composes native component rows and their selected reconstruction weights.
    /// </summary>
    /// <param name="background">The original background components and weights.</param>
    /// <param name="source">The original source components and weights.</param>
    /// <param name="destination">The output planes. Component0 is required. Empty Component1, Component2, or Weight planes discard those components.</param>
    /// <param name="associated">Whether components already include their selected weight.</param>
    /// <param name="clampWeight">Whether the bitstream requires source weights to be bounded to [0,1].</param>
    /// <remarks>Component0 determines the sample count. Every nonempty plane must cover that count.</remarks>
    public static void SourceOver(JxlBlendInput background, JxlBlendInput source, JxlBlendOutput destination, bool associated, bool clampWeight)
    {
        // The shared blender consumes complete four-component vectors. Keep only one fixed block for each input
        // and reuse the source block for its result. Sixteen samples fill one 512-bit plane register, independent of image dimensions.
        InlineArray16<Vector4> backgroundStorage = default;
        InlineArray16<Vector4> sourceStorage = default;
        InlineArray16<float> boundedWeightStorage = default;
        Span<Vector4> backgroundVectors = backgroundStorage;
        Span<Vector4> sourceVectors = sourceStorage;
        Span<float> boundedWeights = boundedWeightStorage;
        int length = destination.Component0.Length;

        for (int offset = 0; offset < length; offset += backgroundVectors.Length)
        {
            int count = Math.Min(backgroundVectors.Length, length - offset);
            ReadOnlySpan<float> sourceWeight = source.Weight.Slice(offset, count);

            if (clampWeight)
            {
                // The source frame is borrowed. Bound only this small weight block; never mutate its stored samples.
                sourceWeight.CopyTo(boundedWeights);
                ColorNumerics.ClampToFinite(boundedWeights[..count], 0F, 1F);
                sourceWeight = boundedWeights[..count];
            }

            SimdUtils.InterleaveFloatPlanes(
                Slice(background.Component0, offset, count),
                Slice(background.Component1, offset, count),
                Slice(background.Component2, offset, count),
                background.Weight.Slice(offset, count),
                backgroundVectors[..count]);

            SimdUtils.InterleaveFloatPlanes(
                Slice(source.Component0, offset, count),
                Slice(source.Component1, offset, count),
                Slice(source.Component2, offset, count),
                sourceWeight,
                sourceVectors[..count]);

            if (!associated)
            {
                // Native weights can be outside [0,1]. Raw association preserves them without invoking pixel storage conversion.
                Numerics.Premultiply(backgroundVectors[..count]);
                Numerics.Premultiply(sourceVectors[..count]);
            }

            // Reuse the existing blender's SIMD loops and associated source-over operator. The codec adapter
            // only replaces the output weight with the format's required complement-product calculation.
            // The shared loop loads both inputs before storing a result, so the source block can be reused in place.
            PixelBlenders.Blend<ReconstructionSourceOverOperator>(sourceVectors[..count], backgroundVectors[..count], sourceVectors[..count], 1F);

            if (!associated)
            {
                UnassociateResult(sourceVectors[..count]);
            }

            SimdUtils.DeinterleaveFloatPlanes(
                sourceVectors[..count],
                Slice(destination.Component0, offset, count),
                Slice(destination.Component1, offset, count),
                Slice(destination.Component2, offset, count),
                Slice(destination.Weight, offset, count));
        }
    }

    /// <summary>
    /// Composes one native extra channel through the same vector-blending path as the color channels.
    /// </summary>
    /// <param name="background">The background samples.</param>
    /// <param name="backgroundWeight">The background reconstruction weights.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceWeight">The source reconstruction weights.</param>
    /// <param name="destination">The output samples. All inputs must cover its length.</param>
    /// <param name="associated">Whether the samples already include their selected weight.</param>
    /// <param name="clampWeight">Whether to bound the source weights with the shared finite clamp.</param>
    public static void SourceOver(
        ReadOnlySpan<float> background,
        ReadOnlySpan<float> backgroundWeight,
        ReadOnlySpan<float> source,
        ReadOnlySpan<float> sourceWeight,
        Span<float> destination,
        bool associated,
        bool clampWeight)
    {
        if (background == backgroundWeight && source == sourceWeight)
        {
            // The selected weight channel needs only the native output-weight calculation. Do not pack or blend unused components.
            WriteOutputWeights(backgroundWeight, sourceWeight, destination, clampWeight);
            return;
        }

        JxlBlendInput bottom = new(background, default, default, backgroundWeight);
        JxlBlendInput top = new(source, default, default, sourceWeight);
        JxlBlendOutput result = new(destination, default, default, default);

        // An extra channel occupies X. Missing Y/Z planes use zero registers and are discarded on output.
        // This uses the shared blender without allocating duplicate channel rows or changing the pixel alpha contract.
        SourceOver(bottom, top, result, associated, clampWeight);
    }

    /// <summary>
    /// Borrows a block from a native input plane, retaining an absent component plane.
    /// </summary>
    /// <param name="values">The input plane, or an empty span for an absent component.</param>
    /// <param name="offset">The first sample in a present plane.</param>
    /// <param name="count">The number of samples to borrow.</param>
    /// <returns>The selected block or an empty span.</returns>
    private static ReadOnlySpan<float> Slice(ReadOnlySpan<float> values, int offset, int count)
        => values.IsEmpty ? default : values.Slice(offset, count);

    /// <summary>
    /// Borrows a block from a native output plane, retaining a discarded component plane.
    /// </summary>
    /// <param name="values">The output plane, or an empty span to discard the component.</param>
    /// <param name="offset">The first output sample in a present plane.</param>
    /// <param name="count">The number of samples to borrow.</param>
    /// <returns>The selected block or an empty span.</returns>
    private static Span<float> Slice(Span<float> values, int offset, int count)
        => values.IsEmpty ? default : values.Slice(offset, count);
}
