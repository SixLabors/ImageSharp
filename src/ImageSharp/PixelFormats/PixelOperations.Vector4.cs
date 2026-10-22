// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides shared pixel component arithmetic and SIMD traversal.
/// </summary>
/// <remarks>
/// Typed <see cref="PixelOperations{TPixel}"/> methods validate buffers and select conversions.
/// These shared operations apply component bounds and alpha conversion. Packing primitives provide rounding and storage layout.
/// </remarks>
internal static partial class PixelOperations
{
    /// <summary>
    /// Associates normalized floating-point components after bounding RGB and alpha for pixel conversion.
    /// </summary>
    /// <param name="source">The straight RGBA components.</param>
    /// <returns>The associated components with finite RGB in [-65504,65504] and alpha in [0,1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Associate(Vector4 source) => Associate(source.AsVector128()).AsVector4();

    /// <summary>
    /// Bounds one normalized floating-point RGBA color for pixel conversion.
    /// </summary>
    /// <param name="value">The normalized color.</param>
    /// <returns>Finite RGB in [-65504,65504] and alpha in [0,1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> ClampToPixelRange(Vector128<float> value)
    {
        // Each four-lane group is RGBA. Float and half share normalized RGB bounds; alpha represents opacity.
        Vector128<float> minimum = Vector128.Create(ColorNumerics.MinColorValue, ColorNumerics.MinColorValue, ColorNumerics.MinColorValue, 0F);
        Vector128<float> maximum = Vector128.Create(ColorNumerics.MaxColorValue, ColorNumerics.MaxColorValue, ColorNumerics.MaxColorValue, 1F);

        return ColorNumerics.ClampToFinite(value, minimum, maximum);
    }

    /// <summary>
    /// Bounds two normalized floating-point RGBA colors for pixel conversion.
    /// </summary>
    /// <param name="value">The normalized colors.</param>
    /// <returns>Finite RGB in [-65504,65504] and alpha in [0,1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> ClampToPixelRange(Vector256<float> value)
    {
        // Repeat the same RGBA bounds for both pixels. Neighboring pixels do not affect each other's limits.
        Vector128<float> minimum = Vector128.Create(ColorNumerics.MinColorValue, ColorNumerics.MinColorValue, ColorNumerics.MinColorValue, 0F);
        Vector128<float> maximum = Vector128.Create(ColorNumerics.MaxColorValue, ColorNumerics.MaxColorValue, ColorNumerics.MaxColorValue, 1F);

        return ColorNumerics.ClampToFinite(value, Vector256.Create(minimum, minimum), Vector256.Create(maximum, maximum));
    }

    /// <summary>
    /// Bounds four normalized floating-point RGBA colors for pixel conversion.
    /// </summary>
    /// <param name="value">The normalized colors.</param>
    /// <returns>Finite RGB in [-65504,65504] and alpha in [0,1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> ClampToPixelRange(Vector512<float> value)
    {
        // Repeat the same RGBA bounds for all four pixels, as in the narrower paths.
        Vector128<float> minimum = Vector128.Create(ColorNumerics.MinColorValue, ColorNumerics.MinColorValue, ColorNumerics.MinColorValue, 0F);
        Vector128<float> maximum = Vector128.Create(ColorNumerics.MaxColorValue, ColorNumerics.MaxColorValue, ColorNumerics.MaxColorValue, 1F);
        Vector256<float> lower = Vector256.Create(minimum, minimum);
        Vector256<float> upper = Vector256.Create(maximum, maximum);

        return ColorNumerics.ClampToFinite(value, Vector512.Create(lower, lower), Vector512.Create(upper, upper));
    }

    /// <summary>
    /// Restores straight RGB for one normalized RGBA color and bounds the result for pixel conversion.
    /// </summary>
    /// <param name="source">One associated RGBA color.</param>
    /// <returns>Finite straight RGB in [-65504,65504] and alpha in [0,1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> UnPremultiply(Vector128<float> source)
        => ClampToPixelRange(UnPremultiplyForPacking(source));

    /// <summary>
    /// Restores straight RGB immediately before destination packing applies its numeric bounds.
    /// </summary>
    /// <param name="source">One associated RGBA input.</param>
    /// <returns>The intermediate components. The caller must pack or clamp them before returning pixel data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> UnPremultiplyForPacking(Vector128<float> source)
    {
        // Retain positive finite alpha above one: reducing it before division would change the straight RGB.
        // Negative alpha and NaN become zero; positive infinity becomes float.MaxValue.
        // At zero alpha, division preserves stored RGB. The result retains the sanitized alpha lane.
        Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
        alpha = ColorNumerics.ClampToFinite(alpha, Vector128<float>.Zero, Vector128.Create(float.MaxValue));

        // Division can overflow for tiny positive alpha. The complete conversion applies the destination's
        // bounds after this arithmetic; an unconditional floating-point clamp would duplicate SDR packing work.
        return Numerics.UnPremultiply(source, alpha);
    }

    /// <summary>
    /// Restores straight RGB for two normalized RGBA colors and bounds the results for pixel conversion.
    /// </summary>
    /// <param name="source">Two associated RGBA colors.</param>
    /// <returns>Finite straight RGB in [-65504,65504] and alpha in [0,1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> UnPremultiply(Vector256<float> source)
        => ClampToPixelRange(UnPremultiplyForPacking(source));

    /// <inheritdoc cref="UnPremultiplyForPacking(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> UnPremultiplyForPacking(Vector256<float> source)
    {
        // Each four-lane RGBA group uses its own alpha. Apply the same finite divisor rule as the single-color path.
        Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
        alpha = ColorNumerics.ClampToFinite(alpha, Vector256<float>.Zero, Vector256.Create(float.MaxValue));

        return Numerics.UnPremultiply(source, alpha);
    }

    /// <summary>
    /// Restores straight RGB for four normalized RGBA colors and bounds the results for pixel conversion.
    /// </summary>
    /// <param name="source">Four associated RGBA colors.</param>
    /// <returns>Finite straight RGB in [-65504,65504] and alpha in [0,1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> UnPremultiply(Vector512<float> source)
        => ClampToPixelRange(UnPremultiplyForPacking(source));

    /// <inheritdoc cref="UnPremultiplyForPacking(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> UnPremultiplyForPacking(Vector512<float> source)
    {
        // Each four-lane RGBA group uses its own alpha. Apply the same finite divisor rule as the single-color path.
        Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
        alpha = ColorNumerics.ClampToFinite(alpha, Vector512<float>.Zero, Vector512.Create(float.MaxValue));

        return Numerics.UnPremultiply(source, alpha);
    }

    /// <summary>
    /// Bounds one normalized RGBA pixel and multiplies RGB by alpha.
    /// </summary>
    /// <param name="source">The straight RGBA components.</param>
    /// <returns>Associated RGB with the bounded alpha unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Associate(Vector128<float> source)
    {
        // Alpha stays in [0,1], so multiplication cannot increase the magnitude of bounded RGB.
        source = ClampToPixelRange(source);
        Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();

        // Restore every fourth lane. Multiplication applies only to RGB, not to alpha itself.
        return Vector128.ConditionalSelect(alphaMask, alpha, source * alpha);
    }

    /// <summary>
    /// Bounds two normalized RGBA pixels and multiplies RGB by alpha.
    /// </summary>
    /// <param name="source">The straight RGBA components.</param>
    /// <returns>Associated RGB with the bounded alpha unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Associate(Vector256<float> source)
    {
        // Alpha stays in [0,1], so multiplication cannot increase the magnitude of bounded RGB.
        source = ClampToPixelRange(source);
        Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

        // Restore every fourth lane. Multiplication applies only to RGB, not to alpha itself.
        return Vector256.ConditionalSelect(alphaMask, alpha, source * alpha);
    }

    /// <summary>
    /// Bounds four normalized RGBA pixels and multiplies RGB by alpha.
    /// </summary>
    /// <param name="source">The straight RGBA components.</param>
    /// <returns>Associated RGB with the bounded alpha unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Associate(Vector512<float> source)
    {
        // Alpha stays in [0,1], so multiplication cannot increase the magnitude of bounded RGB.
        source = ClampToPixelRange(source);
        Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

        // Restore every fourth lane. Multiplication applies only to RGB, not to alpha itself.
        return Vector512.ConditionalSelect(alphaMask, alpha, source * alpha);
    }

    /// <summary>
    /// Multiplies each RGBA component and adds its offset with the runtime multiply-add estimate.
    /// </summary>
    /// <param name="vectors">The pixel vectors to update in place.</param>
    /// <param name="multiplier">The component multipliers.</param>
    /// <param name="offset">The component offsets.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void MultiplyThenAdd(Span<Vector4> vectors, Vector4 multiplier, Vector4 offset)
    {
        ReadOnlySpan<Vector4> source = vectors;
        Span<Vector4> destination = vectors;
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits, two at 256 bits, and one at 128 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector256<float> multiplierPair = Vector256.Create(multiplier.AsVector128(), multiplier.AsVector128());
            Vector512<float> multiplier512 = Vector512.Create(multiplierPair, multiplierPair);
            Vector256<float> offsetPair = Vector256.Create(offset.AsVector128(), offset.AsVector128());
            Vector512<float> offset512 = Vector512.Create(offsetPair, offsetPair);

            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector512<float> result = Vector512.MultiplyAddEstimate(value, multiplier512, offset512);
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> multiplier256 = Vector256.Create(multiplier.AsVector128(), multiplier.AsVector128());
            Vector256<float> offset256 = Vector256.Create(offset.AsVector128(), offset.AsVector128());

            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<float> result = Vector256.MultiplyAddEstimate(value, multiplier256, offset256);
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> multiplier128 = multiplier.AsVector128();
            Vector128<float> offset128 = offset.AsVector128();

            for (; index <= source.Length - 1; index += 1)
            {
                Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<float> result = Vector128.MultiplyAddEstimate(value, multiplier128, offset128);
                Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        // The fallback uses the same per-pixel arithmetic and writes only the source-length prefix.
        for (; index < source.Length; index++)
        {
            Vector4 value = Unsafe.Add(ref sourceBase, (uint)index);
            Unsafe.Add(ref destinationBase, (uint)index) = Vector4.MultiplyAddEstimate(value, multiplier, offset);
        }
    }

    /// <summary>
    /// Adds each component offset, then divides by its divisor.
    /// </summary>
    /// <param name="vectors">The pixel vectors to update in place.</param>
    /// <param name="offset">The component offsets.</param>
    /// <param name="divisor">The component divisors.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void AddThenDivide(Span<Vector4> vectors, Vector4 offset, Vector4 divisor)
    {
        ReadOnlySpan<Vector4> source = vectors;
        Span<Vector4> destination = vectors;
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits, two at 256 bits, and one at 128 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector256<float> offsetPair = Vector256.Create(offset.AsVector128(), offset.AsVector128());
            Vector512<float> offset512 = Vector512.Create(offsetPair, offsetPair);
            Vector256<float> divisorPair = Vector256.Create(divisor.AsVector128(), divisor.AsVector128());
            Vector512<float> divisor512 = Vector512.Create(divisorPair, divisorPair);

            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector512<float> result = (value + offset512) / divisor512;
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> offset256 = Vector256.Create(offset.AsVector128(), offset.AsVector128());
            Vector256<float> divisor256 = Vector256.Create(divisor.AsVector128(), divisor.AsVector128());

            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<float> result = (value + offset256) / divisor256;
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> offset128 = offset.AsVector128();
            Vector128<float> divisor128 = divisor.AsVector128();

            for (; index <= source.Length - 1; index += 1)
            {
                Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<float> result = (value + offset128) / divisor128;
                Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        // The fallback uses the same per-pixel arithmetic and writes only the source-length prefix.
        for (; index < source.Length; index++)
        {
            Vector4 value = Unsafe.Add(ref sourceBase, (uint)index);
            Unsafe.Add(ref destinationBase, (uint)index) = (value + offset) / divisor;
        }
    }

    /// <summary>
    /// Bounds normalized RGB and alpha while writing the destination.
    /// </summary>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="destination">Caller-owned storage with at least the source length.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void ClampToPixelRange(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
    {
        // Source and destination can be the same span. Load a complete pixel group before writing that group.
        // Typed callers supply sufficient destination storage; traversal writes only source.Length pixels.
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits, two at 256 bits, and one at 128 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector512<float> result = ClampToPixelRange(value);
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<float> result = ClampToPixelRange(value);
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 1; index += 1)
            {
                Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<float> result = ClampToPixelRange(value);
                Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        // The fallback uses the same per-pixel arithmetic and writes only the source-length prefix.
        for (; index < source.Length; index++)
        {
            Vector4 value = Unsafe.Add(ref sourceBase, (uint)index);
            Unsafe.Add(ref destinationBase, (uint)index) = ClampToPixelRange(value.AsVector128()).AsVector4();
        }
    }

    /// <summary>
    /// Bounds normalized components and associates RGB while writing the destination.
    /// </summary>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="destination">Caller-owned storage with at least the source length.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void Associate(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
    {
        // Each register applies the same per-pixel association rule. Alpha broadcast stays within each RGBA group.
        // Read before writing so an in-place conversion needs no temporary row.
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits, two at 256 bits, and one at 128 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector512<float> result = Associate(value);
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<float> result = Associate(value);
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 1; index += 1)
            {
                Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<float> result = Associate(value);
                Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        // The fallback uses the same per-pixel arithmetic and writes only the source-length prefix.
        for (; index < source.Length; index++)
        {
            Vector4 value = Unsafe.Add(ref sourceBase, (uint)index);
            Unsafe.Add(ref destinationBase, (uint)index) = Associate(value);
        }
    }

    /// <summary>
    /// Restores straight RGB and bounds the normalized result while writing the destination.
    /// </summary>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="destination">Caller-owned storage with at least the source length.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void UnPremultiply(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
    {
        // Division can overflow when alpha is tiny. UnPremultiply bounds the result before it reaches destination storage.
        // The same helper serves complete SIMD groups and the final pixel, so row length does not change the bounds.
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits, two at 256 bits, and one at 128 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector512<float> result = UnPremultiply(value);
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<float> result = UnPremultiply(value);
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 1; index += 1)
            {
                Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<float> result = UnPremultiply(value);
                Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        // The fallback uses the same per-pixel arithmetic and writes only the source-length prefix.
        for (; index < source.Length; index++)
        {
            Vector4 value = Unsafe.Add(ref sourceBase, (uint)index);
            Unsafe.Add(ref destinationBase, (uint)index) = UnPremultiply(value.AsVector128()).AsVector4();
        }
    }

    /// <summary>
    /// Restores straight RGB before the destination packer applies its component bounds.
    /// </summary>
    /// <param name="vectors">The pixel vectors to update in place.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void UnPremultiplyForPacking(Span<Vector4> vectors)
    {
        ReadOnlySpan<Vector4> source = vectors;
        Span<Vector4> destination = vectors;
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits, two at 256 bits, and one at 128 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector512<float> result = UnPremultiplyForPacking(value);
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<float> result = UnPremultiplyForPacking(value);
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 1; index += 1)
            {
                Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<float> result = UnPremultiplyForPacking(value);
                Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = result;
            }
        }

        // The fallback uses the same per-pixel arithmetic and writes only the source-length prefix.
        for (; index < source.Length; index++)
        {
            Vector4 value = Unsafe.Add(ref sourceBase, (uint)index);
            Unsafe.Add(ref destinationBase, (uint)index) = UnPremultiplyForPacking(value.AsVector128()).AsVector4();
        }
    }
}
