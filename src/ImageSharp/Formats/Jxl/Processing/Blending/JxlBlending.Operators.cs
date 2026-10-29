// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

/// <content>
/// Keeps native reconstruction-weight rules outside shared pixel blending and pixel alpha conversion.
/// </content>
internal static partial class JxlBlending
{
    /// <summary>
    /// Selects the fourth component in each packed four-component group.
    /// </summary>
    private const byte WeightShuffleControl = 0b_11_11_11_11;

    /// <summary>
    /// Uses shared associated source-over composition and supplies the native output weight.
    /// </summary>
    private readonly struct ReconstructionSourceOverOperator : IPixelBlenderOperator
    {
        /// <inheritdoc />
        public static bool IsAssociatedAlpha => true;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Invoke(Vector4 background, Vector4 source, float amount)
        {
            Vector4 result = AssociatedAlphaPixelBlenderOperators.NormalSrcOver.Invoke(background, source, amount);

            // Native reconstruction specifies this evaluation order, including weights outside [0,1].
            float outputWeight = GetOutputWeight(background.W, source.W * amount);

            result.W = outputWeight;
            return result;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Invoke(Vector256<float> background, Vector256<float> source, Vector256<float> amount)
        {
            Vector256<float> result = AssociatedAlphaPixelBlenderOperators.NormalSrcOver.Invoke(background, source, amount);

            // Native reconstruction specifies this evaluation order, including weights outside [0,1].
            Vector256<float> sourceWeight = Vector256_.ShuffleNative(source, WeightShuffleControl) * amount;
            Vector256<float> backgroundWeight = Vector256_.ShuffleNative(background, WeightShuffleControl);
            Vector256<float> outputWeight = GetOutputWeight(backgroundWeight, sourceWeight);

            return Numerics.WithW(result, outputWeight);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Invoke(Vector512<float> background, Vector512<float> source, Vector512<float> amount)
        {
            Vector512<float> result = AssociatedAlphaPixelBlenderOperators.NormalSrcOver.Invoke(background, source, amount);

            // Native reconstruction specifies this evaluation order, including weights outside [0,1].
            Vector512<float> sourceWeight = Vector512_.ShuffleNative(source, WeightShuffleControl) * amount;
            Vector512<float> backgroundWeight = Vector512_.ShuffleNative(background, WeightShuffleControl);
            Vector512<float> outputWeight = GetOutputWeight(backgroundWeight, sourceWeight);

            return Numerics.WithW(result, outputWeight);
        }
    }

    /// <summary>
    /// Calculates native output weights in the format's complement-product evaluation order.
    /// </summary>
    /// <param name="background">The original background weights.</param>
    /// <param name="source">The selected source weights after any encoded clamp.</param>
    /// <returns>The native output weights without pixel-range conversion.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetOutputWeight(float background, float source)
        => 1F - ((1F - source) * (1F - background));

    /// <summary>
    /// Calculates native output weights in the format's complement-product evaluation order.
    /// </summary>
    /// <param name="background">The original background weights.</param>
    /// <param name="source">The selected source weights after any encoded clamp.</param>
    /// <returns>The native output weights without pixel-range conversion.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> GetOutputWeight(Vector128<float> background, Vector128<float> source)
        => Vector128.Create(1F) - ((Vector128.Create(1F) - source) * (Vector128.Create(1F) - background));

    /// <summary>
    /// Calculates native output weights in the format's complement-product evaluation order.
    /// </summary>
    /// <param name="background">The original background weights.</param>
    /// <param name="source">The selected source weights after any encoded clamp.</param>
    /// <returns>The native output weights without pixel-range conversion.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> GetOutputWeight(Vector256<float> background, Vector256<float> source)
        => Vector256.Create(1F) - ((Vector256.Create(1F) - source) * (Vector256.Create(1F) - background));

    /// <summary>
    /// Calculates native output weights in the format's complement-product evaluation order.
    /// </summary>
    /// <param name="background">The original background weights.</param>
    /// <param name="source">The selected source weights after any encoded clamp.</param>
    /// <returns>The native output weights without pixel-range conversion.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> GetOutputWeight(Vector512<float> background, Vector512<float> source)
        => Vector512.Create(1F) - ((Vector512.Create(1F) - source) * (Vector512.Create(1F) - background));

    /// <summary>
    /// Writes only native output weights, without color packing or component composition.
    /// </summary>
    /// <param name="background">The background weights.</param>
    /// <param name="source">The source weights.</param>
    /// <param name="destination">The output weights. Both inputs must cover its length.</param>
    /// <param name="clampWeight">Whether to apply the encoded source clamp to [0,1].</param>
    private static void WriteOutputWeights(ReadOnlySpan<float> background, ReadOnlySpan<float> source, Span<float> destination, bool clampWeight)
    {
        ref float backgroundRef = ref MemoryMarshal.GetReference(background);
        ref float sourceRef = ref MemoryMarshal.GetReference(source);
        ref float destinationRef = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector512Count<float>();

            // Each lane is one weight. Both complete input blocks are read before an exact in-place output store.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> top = Vector512.LoadUnsafe(ref sourceRef, (uint)i);
                if (clampWeight)
                {
                    top = ColorNumerics.ClampToFinite(top, Vector512<float>.Zero, Vector512.Create(1F));
                }

                GetOutputWeight(Vector512.LoadUnsafe(ref backgroundRef, (uint)i), top).StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector256Count<float>();

            // Each lane is one weight. Both complete input blocks are read before an exact in-place output store.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> top = Vector256.LoadUnsafe(ref sourceRef, (uint)i);
                if (clampWeight)
                {
                    top = ColorNumerics.ClampToFinite(top, Vector256<float>.Zero, Vector256.Create(1F));
                }

                GetOutputWeight(Vector256.LoadUnsafe(ref backgroundRef, (uint)i), top).StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector128Count<float>();

            // Each lane is one weight. Both complete input blocks are read before an exact in-place output store.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> top = Vector128.LoadUnsafe(ref sourceRef, (uint)i);
                if (clampWeight)
                {
                    top = ColorNumerics.ClampToFinite(top, Vector128<float>.Zero, Vector128.Create(1F));
                }

                GetOutputWeight(Vector128.LoadUnsafe(ref backgroundRef, (uint)i), top).StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        // The encoded clamp and weight equation are identical for the remaining scalar samples.
        for (; i < destination.Length; i++)
        {
            float top = Unsafe.Add(ref sourceRef, (uint)i);
            if (clampWeight)
            {
                top = ColorNumerics.ClampToFinite(top, 0F, 1F);
            }

            Unsafe.Add(ref destinationRef, (uint)i) = GetOutputWeight(Unsafe.Add(ref backgroundRef, (uint)i), top);
        }
    }

    /// <summary>
    /// Normalizes associated results using the native output-weight reciprocal and nonpositive-weight rule.
    /// </summary>
    /// <param name="values">Associated results with their native output weights already in W.</param>
    private static void UnassociateResult(Span<Vector4> values)
    {
        ref float first = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(values));
        int offset = 0;
        int length = values.Length * 4;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (length - offset).Vector512Count<float>();

            // Each packed register contains complete four-component samples; no lane crosses a sample boundary.
            for (nuint block = 0; block < count; block++, offset += Vector512<float>.Count)
            {
                Vector512<float> value = Vector512.LoadUnsafe(ref first, (uint)offset);
                Vector512<float> weight = Vector512_.ShuffleNative(value, WeightShuffleControl);
                Vector512<float> positive = Vector512.GreaterThan(weight, Vector512<float>.Zero);
                Vector512<float> reciprocal = Vector512.ConditionalSelect(positive, Vector512.Create(1F) / weight, Vector512<float>.Zero);

                // Native alpha.cc first calculates a reciprocal, then multiplies. Preserve that rounding order.
                // Weights can cancel to zero with a nonzero numerator; nonpositive weights use a zero reciprocal.
                value *= reciprocal;
                Numerics.WithW(value, weight).StoreUnsafe(ref first, (uint)offset);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (length - offset).Vector256Count<float>();

            // Each packed register contains complete four-component samples; no lane crosses a sample boundary.
            for (nuint block = 0; block < count; block++, offset += Vector256<float>.Count)
            {
                Vector256<float> value = Vector256.LoadUnsafe(ref first, (uint)offset);
                Vector256<float> weight = Vector256_.ShuffleNative(value, WeightShuffleControl);
                Vector256<float> positive = Vector256.GreaterThan(weight, Vector256<float>.Zero);
                Vector256<float> reciprocal = Vector256.ConditionalSelect(positive, Vector256.Create(1F) / weight, Vector256<float>.Zero);

                // Native alpha.cc first calculates a reciprocal, then multiplies. Preserve that rounding order.
                // Weights can cancel to zero with a nonzero numerator; nonpositive weights use a zero reciprocal.
                value *= reciprocal;
                Numerics.WithW(value, weight).StoreUnsafe(ref first, (uint)offset);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (length - offset).Vector128Count<float>();

            // Each packed register contains complete four-component samples; no lane crosses a sample boundary.
            for (nuint block = 0; block < count; block++, offset += Vector128<float>.Count)
            {
                Vector128<float> value = Vector128.LoadUnsafe(ref first, (uint)offset);
                Vector128<float> weight = Vector128_.ShuffleNative(value, WeightShuffleControl);
                Vector128<float> positive = Vector128.GreaterThan(weight, Vector128<float>.Zero);
                Vector128<float> reciprocal = Vector128.ConditionalSelect(positive, Vector128.Create(1F) / weight, Vector128<float>.Zero);

                // Native alpha.cc first calculates a reciprocal, then multiplies. Preserve that rounding order.
                // Weights can cancel to zero with a nonzero numerator; nonpositive weights use a zero reciprocal.
                value *= reciprocal;
                Numerics.WithW(value, weight).StoreUnsafe(ref first, (uint)offset);
            }
        }

        // Complete Vector4 samples remain when hardware intrinsics are disabled. Use the identical native reciprocal rule.
        for (int i = offset / 4; i < values.Length; i++)
        {
            Vector4 value = values[i];
            float weight = value.W;
            float reciprocal = weight > 0F ? 1F / weight : 0F;
            value *= reciprocal;
            value.W = weight;
            values[i] = value;
        }
    }
}
