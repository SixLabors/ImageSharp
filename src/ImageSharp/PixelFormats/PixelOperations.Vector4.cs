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
    /// Bounds one normalized floating-point RGBA color for pixel conversion.
    /// </summary>
    /// <param name="value">The normalized color.</param>
    /// <returns>Finite RGB in [-65504,65504] and alpha in [0,1]. NaN maps to the lower bound.</returns>
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
    /// Bounds one normalized RGBA pixel for pixel conversion, then applies <typeparamref name="TAlpha"/>.
    /// </summary>
    /// <remarks>
    /// The scalar pixel conversions and the final pixels of the span conversion use this form.
    /// </remarks>
    /// <typeparam name="TAlpha">The alpha step for the bounded pixel.</typeparam>
    /// <param name="value">The normalized pixel.</param>
    /// <returns>The bounded pixel after the alpha step.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> ClampToPixelRange<TAlpha>(Vector128<float> value)
        where TAlpha : struct, IAlphaOperator
        => TAlpha.Apply(ClampToPixelRange(value));

    /// <summary>
    /// Bounds two normalized RGBA pixels for pixel conversion, then applies <typeparamref name="TAlpha"/>.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for the bounded pixels.</typeparam>
    /// <param name="value">The normalized pixels.</param>
    /// <returns>The bounded pixels after the alpha step.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> ClampToPixelRange<TAlpha>(Vector256<float> value)
        where TAlpha : struct, IAlphaOperator
        => TAlpha.Apply(ClampToPixelRange(value));

    /// <summary>
    /// Bounds four normalized RGBA pixels for pixel conversion, then applies <typeparamref name="TAlpha"/>.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for the bounded pixels.</typeparam>
    /// <param name="value">The normalized pixels.</param>
    /// <returns>The bounded pixels after the alpha step.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> ClampToPixelRange<TAlpha>(Vector512<float> value)
        where TAlpha : struct, IAlphaOperator
        => TAlpha.Apply(ClampToPixelRange(value));

    /// <summary>
    /// Bounds normalized RGB and alpha while writing the destination.
    /// </summary>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="destination">Caller-owned storage with at least the source length.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void ClampToPixelRange(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
        => ClampToPixelRange<NoAlphaOperator>(source, destination);

    /// <summary>
    /// Bounds normalized RGB and alpha, and applies <typeparamref name="TAlpha"/> to the bounded vectors in the same pass,
    /// while writing the destination.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each bounded pixel.</typeparam>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="destination">Caller-owned storage with at least the source length.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void ClampToPixelRange<TAlpha>(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
        where TAlpha : struct, IAlphaOperator
    {
        // Source and destination can be the same span. Load a complete pixel group before writing that group.
        // Typed callers supply sufficient destination storage; traversal writes only source.Length pixels.
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits and two at 256 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = ClampToPixelRange<TAlpha>(value);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = ClampToPixelRange<TAlpha>(value);
            }
        }

        // The 128-bit form holds one pixel. It serves the final pixels and the path without hardware acceleration,
        // so each pixel uses the same method at every width.
        for (; index < source.Length; index++)
        {
            Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
            Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = ClampToPixelRange<TAlpha>(value);
        }
    }

    /// <summary>
    /// Applies <typeparamref name="TAlpha"/> to each pixel while writing the destination.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
    /// <param name="source">The source pixel vectors.</param>
    /// <param name="destination">Caller-owned storage with at least the source length.</param>
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void Apply<TAlpha>(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
        where TAlpha : struct, IAlphaOperator
    {
        // Source and destination can be the same span. Load a complete pixel group before writing that group.
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // Registers hold complete RGBA groups: four pixels at 512 bits and two at 256 bits.
        // Each narrower loop consumes only the remainder. Unaligned access needs no padding or temporary storage.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 4; index += 4)
            {
                Vector512<float> value = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = TAlpha.Apply(value);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= source.Length - 2; index += 2)
            {
                Vector256<float> value = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = TAlpha.Apply(value);
            }
        }

        // The 128-bit operator holds one pixel. It serves the final pixels and the path without hardware acceleration,
        // so each pixel uses the same arithmetic at every width.
        for (; index < source.Length; index++)
        {
            Vector128<float> value = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
            Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = TAlpha.Apply(value);
        }
    }
}
