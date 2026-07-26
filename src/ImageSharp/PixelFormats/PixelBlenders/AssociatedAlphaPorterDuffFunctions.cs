// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders;

/// <summary>
/// Provides Porter-Duff composition functions for associated-alpha vectors.
/// </summary>
internal static partial class AssociatedAlphaPorterDuffFunctions
{
    private const int BlendAlphaControl = 0b_10_00_10_00;
    private const int ShuffleAlphaControl = 0b_11_11_11_11;

    /// <summary>
    /// Calculates the associated overlap term for Multiply blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Multiply(Vector4 backdrop, Vector4 source) => backdrop * source;

    /// <inheritdoc cref="Multiply(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Multiply(Vector256<float> backdrop, Vector256<float> source) => backdrop * source;

    /// <inheritdoc cref="Multiply(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Multiply(Vector512<float> backdrop, Vector512<float> source) => backdrop * source;

    /// <summary>
    /// Calculates the associated overlap term for Add blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Add(Vector4 backdrop, Vector4 source)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        return Vector4.Min(backdropAlpha * sourceAlpha, (backdrop * sourceAlpha) + (source * backdropAlpha));
    }

    /// <inheritdoc cref="Add(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Add(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        return Vector256.Min(backdropAlpha * sourceAlpha, (backdrop * sourceAlpha) + (source * backdropAlpha));
    }

    /// <inheritdoc cref="Add(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Add(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        return Vector512.Min(backdropAlpha * sourceAlpha, (backdrop * sourceAlpha) + (source * backdropAlpha));
    }

    /// <summary>
    /// Calculates the associated overlap term for Subtract blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Subtract(Vector4 backdrop, Vector4 source)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        return Vector4.Max(Vector4.Zero, (backdrop * sourceAlpha) - (source * backdropAlpha));
    }

    /// <inheritdoc cref="Subtract(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Subtract(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        return Vector256.Max(Vector256<float>.Zero, (backdrop * sourceAlpha) - (source * backdropAlpha));
    }

    /// <inheritdoc cref="Subtract(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Subtract(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        return Vector512.Max(Vector512<float>.Zero, (backdrop * sourceAlpha) - (source * backdropAlpha));
    }

    /// <summary>
    /// Calculates the associated overlap term for Screen blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Screen(Vector4 backdrop, Vector4 source)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        return (backdrop * sourceAlpha) + (source * backdropAlpha) - (backdrop * source);
    }

    /// <inheritdoc cref="Screen(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Screen(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        return (backdrop * sourceAlpha) + (source * backdropAlpha) - (backdrop * source);
    }

    /// <inheritdoc cref="Screen(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Screen(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        return (backdrop * sourceAlpha) + (source * backdropAlpha) - (backdrop * source);
    }

    /// <summary>
    /// Calculates the associated overlap term for Darken blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Darken(Vector4 backdrop, Vector4 source)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        return Vector4.Min(backdrop * sourceAlpha, source * backdropAlpha);
    }

    /// <inheritdoc cref="Darken(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Darken(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        return Vector256.Min(backdrop * sourceAlpha, source * backdropAlpha);
    }

    /// <inheritdoc cref="Darken(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Darken(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        return Vector512.Min(backdrop * sourceAlpha, source * backdropAlpha);
    }

    /// <summary>
    /// Calculates the associated overlap term for Lighten blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Lighten(Vector4 backdrop, Vector4 source)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        return Vector4.Max(backdrop * sourceAlpha, source * backdropAlpha);
    }

    /// <inheritdoc cref="Lighten(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Lighten(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        return Vector256.Max(backdrop * sourceAlpha, source * backdropAlpha);
    }

    /// <inheritdoc cref="Lighten(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Lighten(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        return Vector512.Max(backdrop * sourceAlpha, source * backdropAlpha);
    }

    /// <summary>
    /// Calculates the associated overlap term for Overlay blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Overlay(Vector4 backdrop, Vector4 source)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);

        return new Vector4(
            OverlayValue(backdrop.X, backdropAlpha.X, source.X, sourceAlpha.X),
            OverlayValue(backdrop.Y, backdropAlpha.Y, source.Y, sourceAlpha.Y),
            OverlayValue(backdrop.Z, backdropAlpha.Z, source.Z, sourceAlpha.Z),
            0F);
    }

    /// <inheritdoc cref="Overlay(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Overlay(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> left = (backdrop + backdrop) * source;
        Vector256<float> right = (backdropAlpha * sourceAlpha) - (((backdropAlpha - backdrop) * (sourceAlpha - source)) * Vector256.Create(2F));
        Vector256<float> useRight = Avx.CompareGreaterThan(backdrop + backdrop, backdropAlpha);
        return Avx.BlendVariable(left, right, useRight);
    }

    /// <inheritdoc cref="Overlay(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Overlay(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> left = (backdrop + backdrop) * source;
        Vector512<float> right = (backdropAlpha * sourceAlpha) - (((backdropAlpha - backdrop) * (sourceAlpha - source)) * Vector512.Create(2F));
        Vector512<float> useRight = Avx512F.CompareGreaterThan(backdrop + backdrop, backdropAlpha);
        return Vector512.ConditionalSelect(useRight, right, left);
    }

    /// <summary>
    /// Calculates the associated overlap term for HardLight blending.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated overlap term.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 HardLight(Vector4 backdrop, Vector4 source)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);

        return new Vector4(
            OverlayValue(source.X, sourceAlpha.X, backdrop.X, backdropAlpha.X),
            OverlayValue(source.Y, sourceAlpha.Y, backdrop.Y, backdropAlpha.Y),
            OverlayValue(source.Z, sourceAlpha.Z, backdrop.Z, backdropAlpha.Z),
            0F);
    }

    /// <inheritdoc cref="HardLight(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> HardLight(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> left = (backdrop + backdrop) * source;
        Vector256<float> right = (backdropAlpha * sourceAlpha) - (((backdropAlpha - backdrop) * (sourceAlpha - source)) * Vector256.Create(2F));
        Vector256<float> useRight = Avx.CompareGreaterThan(source + source, sourceAlpha);
        return Avx.BlendVariable(left, right, useRight);
    }

    /// <inheritdoc cref="HardLight(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> HardLight(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> left = (backdrop + backdrop) * source;
        Vector512<float> right = (backdropAlpha * sourceAlpha) - (((backdropAlpha - backdrop) * (sourceAlpha - source)) * Vector512.Create(2F));
        Vector512<float> useRight = Avx512F.CompareGreaterThan(source + source, sourceAlpha);
        return Vector512.ConditionalSelect(useRight, right, left);
    }

    /// <summary>
    /// Composites an associated source over an associated destination without a color-blending function.
    /// </summary>
    /// <param name="destination">The associated destination vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 OverNormal(Vector4 destination, Vector4 source)
    {
        // Associated source-over is Ps + Pb(1 - As); both color and alpha therefore use the same coefficient.
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        return source + (destination * (Vector4.One - sourceAlpha));
    }

    /// <inheritdoc cref="OverNormal(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> OverNormal(Vector256<float> destination, Vector256<float> source)
    {
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        return source + (destination * (Vector256.Create(1F) - sourceAlpha));
    }

    /// <inheritdoc cref="OverNormal(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> OverNormal(Vector512<float> destination, Vector512<float> source)
    {
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        return source + (destination * (Vector512.Create(1F) - sourceAlpha));
    }

    /// <summary>
    /// Composites an associated source atop an associated destination without a color-blending function.
    /// </summary>
    /// <param name="destination">The associated destination vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 AtopNormal(Vector4 destination, Vector4 source)
    {
        // Source-atop retains the destination alpha while replacing its covered contribution with the source.
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        Vector4 destinationAlpha = Numerics.PermuteW(destination);
        return (source * destinationAlpha) + (destination * (Vector4.One - sourceAlpha));
    }

    /// <inheritdoc cref="AtopNormal(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> AtopNormal(Vector256<float> destination, Vector256<float> source)
    {
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> destinationAlpha = Avx.Permute(destination, ShuffleAlphaControl);
        return (source * destinationAlpha) + (destination * (Vector256.Create(1F) - sourceAlpha));
    }

    /// <inheritdoc cref="AtopNormal(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> AtopNormal(Vector512<float> destination, Vector512<float> source)
    {
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> destinationAlpha = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);
        return (source * destinationAlpha) + (destination * (Vector512.Create(1F) - sourceAlpha));
    }

    /// <summary>
    /// Composites an associated source over an associated destination using an unassociated blended color.
    /// </summary>
    /// <param name="destination">The associated destination vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <param name="overlap">The associated overlap term produced by the color-blending function.</param>
    /// <returns>The associated composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Over(Vector4 destination, Vector4 source, Vector4 overlap)
    {
        // The three terms cover destination-only, source-only, and overlapping color respectively.
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        Vector4 destinationAlpha = Numerics.PermuteW(destination);
        Vector4 result = (destination * (Vector4.One - sourceAlpha)) + (source * (Vector4.One - destinationAlpha)) + overlap;
        Vector4 alpha = source + (destination * (Vector4.One - sourceAlpha));
        return Numerics.WithW(result, alpha);
    }

    /// <inheritdoc cref="Over(Vector4, Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Over(Vector256<float> destination, Vector256<float> source, Vector256<float> overlap)
    {
        Vector256<float> one = Vector256.Create(1F);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> destinationAlpha = Avx.Permute(destination, ShuffleAlphaControl);
        Vector256<float> result = (destination * (one - sourceAlpha)) + (source * (one - destinationAlpha)) + overlap;
        Vector256<float> alpha = source + (destination * (one - sourceAlpha));
        return Avx.Blend(result, alpha, BlendAlphaControl);
    }

    /// <inheritdoc cref="Over(Vector4, Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Over(Vector512<float> destination, Vector512<float> source, Vector512<float> overlap)
    {
        Vector512<float> one = Vector512.Create(1F);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> destinationAlpha = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);
        Vector512<float> result = (destination * (one - sourceAlpha)) + (source * (one - destinationAlpha)) + overlap;
        Vector512<float> alpha = source + (destination * (one - sourceAlpha));
        return Vector512.ConditionalSelect(AlphaMask512(), alpha, result);
    }

    /// <summary>
    /// Composites an associated source atop an associated destination using an unassociated blended color.
    /// </summary>
    /// <param name="destination">The associated destination vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <param name="overlap">The associated overlap term produced by the color-blending function.</param>
    /// <returns>The associated composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Atop(Vector4 destination, Vector4 source, Vector4 overlap)
    {
        // Atop discards source-only color and retains the destination alpha unchanged.
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        Vector4 destinationAlpha = Numerics.PermuteW(destination);
        Vector4 coefficient = Vector4.One - sourceAlpha;
        Vector4 result = Vector128_.FusedMultiplyAdd(destination.AsVector128(), coefficient.AsVector128(), overlap.AsVector128()).AsVector4();

        return Numerics.WithW(result, destinationAlpha);
    }

    /// <inheritdoc cref="Atop(Vector4, Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Atop(Vector256<float> destination, Vector256<float> source, Vector256<float> overlap)
    {
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> destinationAlpha = Avx.Permute(destination, ShuffleAlphaControl);
        Vector256<float> result = Vector256_.FusedMultiplyAdd(destination, Vector256.Create(1F) - sourceAlpha, overlap);

        return Avx.Blend(result, destinationAlpha, BlendAlphaControl);
    }

    /// <inheritdoc cref="Atop(Vector4, Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Atop(Vector512<float> destination, Vector512<float> source, Vector512<float> overlap)
    {
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> destinationAlpha = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);
        Vector512<float> result = Vector512_.FusedMultiplyAdd(destination, Vector512.Create(1F) - sourceAlpha, overlap);

        return Vector512.ConditionalSelect(AlphaMask512(), destinationAlpha, result);
    }

    /// <summary>
    /// Retains the associated source within the destination coverage.
    /// </summary>
    /// <param name="destination">The associated destination vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 In(Vector4 destination, Vector4 source) => source * Numerics.PermuteW(destination);

    /// <inheritdoc cref="In(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> In(Vector256<float> destination, Vector256<float> source)
        => source * Avx.Permute(destination, ShuffleAlphaControl);

    /// <inheritdoc cref="In(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> In(Vector512<float> destination, Vector512<float> source)
        => source * Vector512_.ShuffleNative(destination, ShuffleAlphaControl);

    /// <summary>
    /// Retains the associated source outside the destination coverage.
    /// </summary>
    /// <param name="destination">The associated destination vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Out(Vector4 destination, Vector4 source)
        => source * (Vector4.One - Numerics.PermuteW(destination));

    /// <inheritdoc cref="Out(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Out(Vector256<float> destination, Vector256<float> source)
        => source * (Vector256.Create(1F) - Avx.Permute(destination, ShuffleAlphaControl));

    /// <inheritdoc cref="Out(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Out(Vector512<float> destination, Vector512<float> source)
        => source * (Vector512.Create(1F) - Vector512_.ShuffleNative(destination, ShuffleAlphaControl));

    /// <summary>
    /// Retains only the non-overlapping parts of two associated vectors.
    /// </summary>
    /// <param name="destination">The associated destination vector.</param>
    /// <param name="source">The associated source vector.</param>
    /// <returns>The associated composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Xor(Vector4 destination, Vector4 source)
    {
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        Vector4 destinationAlpha = Numerics.PermuteW(destination);
        return (source * (Vector4.One - destinationAlpha)) + (destination * (Vector4.One - sourceAlpha));
    }

    /// <inheritdoc cref="Xor(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Xor(Vector256<float> destination, Vector256<float> source)
    {
        Vector256<float> one = Vector256.Create(1F);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> destinationAlpha = Avx.Permute(destination, ShuffleAlphaControl);
        return (source * (one - destinationAlpha)) + (destination * (one - sourceAlpha));
    }

    /// <inheritdoc cref="Xor(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Xor(Vector512<float> destination, Vector512<float> source)
    {
        Vector512<float> one = Vector512.Create(1F);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> destinationAlpha = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);
        return (source * (one - destinationAlpha)) + (destination * (one - sourceAlpha));
    }

    /// <summary>
    /// Applies raster coverage to an associated composition result.
    /// </summary>
    /// <param name="backdrop">The associated backdrop vector.</param>
    /// <param name="source">The associated composition result.</param>
    /// <param name="coverage">The raster coverage in the range 0 through 1.</param>
    /// <returns>The covered associated result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 BlendWithCoverage(Vector4 backdrop, Vector4 source, float coverage)
    {
        // Use the same fused operation as the wider paths so exact midpoints cannot change across vector widths.
        return Vector128_.FusedMultiplyAdd((source - backdrop).AsVector128(), Vector128.Create(coverage), backdrop.AsVector128()).AsVector4();
    }

    /// <inheritdoc cref="BlendWithCoverage(Vector4, Vector4, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> BlendWithCoverage(Vector256<float> backdrop, Vector256<float> source, Vector256<float> coverage)
        => Vector256_.FusedMultiplyAdd(source - backdrop, coverage, backdrop);

    /// <inheritdoc cref="BlendWithCoverage(Vector4, Vector4, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> BlendWithCoverage(Vector512<float> backdrop, Vector512<float> source, Vector512<float> coverage)
        => Vector512_.FusedMultiplyAdd(source - backdrop, coverage, backdrop);

    /// <summary>
    /// Calculates one associated Overlay overlap component without recovering either straight component.
    /// </summary>
    /// <param name="backdrop">The associated backdrop component.</param>
    /// <param name="backdropAlpha">The backdrop alpha.</param>
    /// <param name="source">The associated source component.</param>
    /// <param name="sourceAlpha">The source alpha.</param>
    /// <returns>The associated overlap component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float OverlayValue(float backdrop, float backdropAlpha, float source, float sourceAlpha)
    {
        // Comparing 2Pb with Ab is equivalent to comparing the straight backdrop component with one half.
        return (backdrop + backdrop) <= backdropAlpha
            ? (backdrop + backdrop) * source
            : (backdropAlpha * sourceAlpha) - (2F * (backdropAlpha - backdrop) * (sourceAlpha - source));
    }

    /// <summary>
    /// Creates a SIMD lane mask selecting the alpha component of each packed vector.
    /// </summary>
    /// <returns>The alpha-component mask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> AlphaMask512()
        => Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
}
