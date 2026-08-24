// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders;

/// <summary>
/// Collection of Porter Duff Color Blending and Alpha Composition Functions.
/// </summary>
/// <remarks>
/// These functions are designed to be a general solution for all color cases,
/// that is, they take in account the alpha value of both the backdrop
/// and source, and there's no need to alpha-premultiply neither the backdrop
/// nor the source.
/// Note there are faster functions for when the backdrop color is known
/// to be opaque
/// </remarks>
internal static partial class PorterDuffFunctions
{
    private const int BlendAlphaControl = 0b_10_00_10_00;
    private const int ShuffleAlphaControl = 0b_11_11_11_11;

    /// <summary>
    /// Returns the result of the "Normal" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Normal(Vector4 backdrop, Vector4 source)
        => source;

    /// <summary>
    /// Returns the result of the "Normal" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Normal(Vector256<float> backdrop, Vector256<float> source)
        => source;

    /// <summary>
    /// Returns the result of the "Normal" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Normal(Vector512<float> backdrop, Vector512<float> source)
        => source;

    /// <summary>
    /// Returns the result of the "Multiply" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Multiply(Vector4 backdrop, Vector4 source)
        => backdrop * source;

    /// <summary>
    /// Returns the result of the "Multiply" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Multiply(Vector256<float> backdrop, Vector256<float> source)
        => backdrop * source;

    /// <summary>
    /// Returns the result of the "Multiply" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Multiply(Vector512<float> backdrop, Vector512<float> source)
        => backdrop * source;

    /// <summary>
    /// Returns the result of the "Add" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Add(Vector4 backdrop, Vector4 source)
        => Vector4.Min(Vector4.One, backdrop + source);

    /// <summary>
    /// Returns the result of the "Add" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Add(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Min(Vector256.Create(1F), backdrop + source);

    /// <summary>
    /// Returns the result of the "Add" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Add(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Min(Vector512.Create(1F), backdrop + source);

    /// <summary>
    /// Returns the result of the "Subtract" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Subtract(Vector4 backdrop, Vector4 source)
        => Vector4.Max(Vector4.Zero, backdrop - source);

    /// <summary>
    /// Returns the result of the "Subtract" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Subtract(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Max(Vector256<float>.Zero, backdrop - source);

    /// <summary>
    /// Returns the result of the "Subtract" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Subtract(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Max(Vector512<float>.Zero, backdrop - source);

    /// <summary>
    /// Returns the result of the "Screen" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Screen(Vector4 backdrop, Vector4 source)
        => Vector4.One - ((Vector4.One - backdrop) * (Vector4.One - source));

    /// <summary>
    /// Returns the result of the "Screen" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Screen(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> vOne = Vector256.Create(1F);
        return Vector256_.MultiplyAddNegated(vOne, vOne - backdrop, vOne - source);
    }

    /// <summary>
    /// Returns the result of the "Screen" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Screen(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> vOne = Vector512.Create(1F);
        return Vector512_.MultiplyAddNegated(vOne, vOne - backdrop, vOne - source);
    }

    /// <summary>
    /// Returns the result of the "Darken" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Darken(Vector4 backdrop, Vector4 source)
        => Vector4.Min(backdrop, source);

    /// <summary>
    /// Returns the result of the "Darken" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Darken(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Min(backdrop, source);

    /// <summary>
    /// Returns the result of the "Darken" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Darken(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Min(backdrop, source);

    /// <summary>
    /// Returns the result of the "Lighten" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Lighten(Vector4 backdrop, Vector4 source) => Vector4.Max(backdrop, source);

    /// <summary>
    /// Returns the result of the "Lighten" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Lighten(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Max(backdrop, source);

    /// <summary>
    /// Returns the result of the "Lighten" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Lighten(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Max(backdrop, source);

    /// <summary>
    /// Returns the result of the "Overlay" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Overlay(Vector4 backdrop, Vector4 source)
    {
        float cr = OverlayValueFunction(backdrop.X, source.X);
        float cg = OverlayValueFunction(backdrop.Y, source.Y);
        float cb = OverlayValueFunction(backdrop.Z, source.Z);

        return Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0));
    }

    /// <summary>
    /// Returns the result of the "Overlay" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Overlay(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> color = OverlayValueFunction(backdrop, source);
        return Vector256.Min(Vector256.Create(1F), Avx.Blend(color, Vector256<float>.Zero, BlendAlphaControl));
    }

    /// <summary>
    /// Returns the result of the "Overlay" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Overlay(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> color = OverlayValueFunction(backdrop, source);
        return Vector512.Min(Vector512.Create(1F), Vector512.ConditionalSelect(AlphaMask512(), Vector512<float>.Zero, color));
    }

    /// <summary>
    /// Returns the result of the "HardLight" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 HardLight(Vector4 backdrop, Vector4 source)
    {
        float cr = OverlayValueFunction(source.X, backdrop.X);
        float cg = OverlayValueFunction(source.Y, backdrop.Y);
        float cb = OverlayValueFunction(source.Z, backdrop.Z);

        return Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0));
    }

    /// <summary>
    /// Returns the result of the "HardLight" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> HardLight(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> color = OverlayValueFunction(source, backdrop);
        return Vector256.Min(Vector256.Create(1F), Avx.Blend(color, Vector256<float>.Zero, BlendAlphaControl));
    }

    /// <summary>
    /// Returns the result of the "HardLight" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> HardLight(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> color = OverlayValueFunction(source, backdrop);
        return Vector512.Min(Vector512.Create(1F), Vector512.ConditionalSelect(AlphaMask512(), Vector512<float>.Zero, color));
    }

    /// <summary>
    /// Returns the result of the "ColorDodge" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ColorDodge(Vector4 backdrop, Vector4 source)
        => new(
            ColorDodgeValue(backdrop.X, source.X),
            ColorDodgeValue(backdrop.Y, source.Y),
            ColorDodgeValue(backdrop.Z, source.Z),
            0F);

    /// <inheritdoc cref="ColorDodge(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> ColorDodge(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> one = Vector256.Create(1F);
        Vector256<float> result = Vector256.Min(one, backdrop / (one - source));

        // The explicit singular cases avoid the undefined 0 / 0 result while preserving the order required by the specification.
        result = Avx.BlendVariable(result, one, Avx.CompareEqual(source, one));
        result = Avx.BlendVariable(result, Vector256<float>.Zero, Avx.CompareEqual(backdrop, Vector256<float>.Zero));
        return Avx.Blend(result, Vector256<float>.Zero, BlendAlphaControl);
    }

    /// <inheritdoc cref="ColorDodge(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> ColorDodge(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> one = Vector512.Create(1F);
        Vector512<float> result = Vector512.Min(one, backdrop / (one - source));
        result = Vector512.ConditionalSelect(Vector512.Equals(source, one), one, result);
        result = Vector512.ConditionalSelect(Vector512.Equals(backdrop, Vector512<float>.Zero), Vector512<float>.Zero, result);
        return Vector512.ConditionalSelect(AlphaMask512(), Vector512<float>.Zero, result);
    }

    /// <summary>
    /// Returns the result of the "ColorBurn" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ColorBurn(Vector4 backdrop, Vector4 source)
        => new(
            ColorBurnValue(backdrop.X, source.X),
            ColorBurnValue(backdrop.Y, source.Y),
            ColorBurnValue(backdrop.Z, source.Z),
            0F);

    /// <inheritdoc cref="ColorBurn(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> ColorBurn(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> one = Vector256.Create(1F);
        Vector256<float> result = one - Vector256.Min(one, (one - backdrop) / source);

        // As with ColorDodge, resolve the branch singularities explicitly so transparent lanes never produce NaN.
        result = Avx.BlendVariable(result, Vector256<float>.Zero, Avx.CompareEqual(source, Vector256<float>.Zero));
        result = Avx.BlendVariable(result, one, Avx.CompareEqual(backdrop, one));
        return Avx.Blend(result, Vector256<float>.Zero, BlendAlphaControl);
    }

    /// <inheritdoc cref="ColorBurn(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> ColorBurn(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> one = Vector512.Create(1F);
        Vector512<float> result = one - Vector512.Min(one, (one - backdrop) / source);
        result = Vector512.ConditionalSelect(Vector512.Equals(source, Vector512<float>.Zero), Vector512<float>.Zero, result);
        result = Vector512.ConditionalSelect(Vector512.Equals(backdrop, one), one, result);
        return Vector512.ConditionalSelect(AlphaMask512(), Vector512<float>.Zero, result);
    }

    /// <summary>
    /// Returns the result of the "SoftLight" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 SoftLight(Vector4 backdrop, Vector4 source)
        => new(
            SoftLightValue(backdrop.X, source.X),
            SoftLightValue(backdrop.Y, source.Y),
            SoftLightValue(backdrop.Z, source.Z),
            0F);

    /// <inheritdoc cref="SoftLight(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> SoftLight(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> one = Vector256.Create(1F);
        Vector256<float> two = Vector256.Create(2F);
        Vector256<float> dark = backdrop - ((one - (two * source)) * backdrop * (one - backdrop));
        Vector256<float> polynomial = ((((Vector256.Create(16F) * backdrop) - Vector256.Create(12F)) * backdrop) + Vector256.Create(4F)) * backdrop;
        Vector256<float> d = Avx.BlendVariable(polynomial, Avx.Sqrt(backdrop), Avx.CompareGreaterThan(backdrop, Vector256.Create(.25F)));
        Vector256<float> light = backdrop + (((two * source) - one) * (d - backdrop));
        Vector256<float> result = Avx.BlendVariable(dark, light, Avx.CompareGreaterThan(source, Vector256.Create(.5F)));
        return Avx.Blend(result, Vector256<float>.Zero, BlendAlphaControl);
    }

    /// <inheritdoc cref="SoftLight(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> SoftLight(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> one = Vector512.Create(1F);
        Vector512<float> two = Vector512.Create(2F);
        Vector512<float> dark = backdrop - ((one - (two * source)) * backdrop * (one - backdrop));
        Vector512<float> polynomial = ((((Vector512.Create(16F) * backdrop) - Vector512.Create(12F)) * backdrop) + Vector512.Create(4F)) * backdrop;
        Vector512<float> d = Vector512.ConditionalSelect(Avx512F.CompareGreaterThan(backdrop, Vector512.Create(.25F)), Vector512.Sqrt(backdrop), polynomial);
        Vector512<float> light = backdrop + (((two * source) - one) * (d - backdrop));
        Vector512<float> result = Vector512.ConditionalSelect(Avx512F.CompareGreaterThan(source, Vector512.Create(.5F)), light, dark);
        return Vector512.ConditionalSelect(AlphaMask512(), Vector512<float>.Zero, result);
    }

    /// <summary>
    /// Returns the result of the "Difference" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Difference(Vector4 backdrop, Vector4 source)
        => Vector4.Abs(backdrop - source);

    /// <inheritdoc cref="Difference(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Difference(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Abs(backdrop - source);

    /// <inheritdoc cref="Difference(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Difference(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Abs(backdrop - source);

    /// <summary>
    /// Returns the result of the "Exclusion" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Exclusion(Vector4 backdrop, Vector4 source)
        => backdrop + source - (2F * backdrop * source);

    /// <inheritdoc cref="Exclusion(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Exclusion(Vector256<float> backdrop, Vector256<float> source)
        => backdrop + source - (Vector256.Create(2F) * backdrop * source);

    /// <inheritdoc cref="Exclusion(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Exclusion(Vector512<float> backdrop, Vector512<float> source)
        => backdrop + source - (Vector512.Create(2F) * backdrop * source);

    /// <summary>
    /// Returns the result of the "Hue" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Hue(Vector4 backdrop, Vector4 source)
        => SetLuminosity(SetSaturation(source, Saturation(backdrop)), Luminosity(backdrop));

    /// <inheritdoc cref="Hue(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Hue(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Create(
            Hue(backdrop.GetLower().AsVector4(), source.GetLower().AsVector4()).AsVector128(),
            Hue(backdrop.GetUpper().AsVector4(), source.GetUpper().AsVector4()).AsVector128());

    /// <inheritdoc cref="Hue(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Hue(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Create(Hue(backdrop.GetLower(), source.GetLower()), Hue(backdrop.GetUpper(), source.GetUpper()));

    /// <summary>
    /// Returns the result of the "Saturation" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Saturation(Vector4 backdrop, Vector4 source)
        => SetLuminosity(SetSaturation(backdrop, Saturation(source)), Luminosity(backdrop));

    /// <inheritdoc cref="Saturation(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Saturation(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Create(
            Saturation(backdrop.GetLower().AsVector4(), source.GetLower().AsVector4()).AsVector128(),
            Saturation(backdrop.GetUpper().AsVector4(), source.GetUpper().AsVector4()).AsVector128());

    /// <inheritdoc cref="Saturation(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Saturation(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Create(Saturation(backdrop.GetLower(), source.GetLower()), Saturation(backdrop.GetUpper(), source.GetUpper()));

    /// <summary>
    /// Returns the result of the "Color" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Color(Vector4 backdrop, Vector4 source)
        => SetLuminosity(source, Luminosity(backdrop));

    /// <inheritdoc cref="Color(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Color(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Create(
            Color(backdrop.GetLower().AsVector4(), source.GetLower().AsVector4()).AsVector128(),
            Color(backdrop.GetUpper().AsVector4(), source.GetUpper().AsVector4()).AsVector128());

    /// <inheritdoc cref="Color(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Color(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Create(Color(backdrop.GetLower(), source.GetLower()), Color(backdrop.GetUpper(), source.GetUpper()));

    /// <summary>
    /// Returns the result of the "Luminosity" compositing equation.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The blended color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Luminosity(Vector4 backdrop, Vector4 source)
        => SetLuminosity(backdrop, Luminosity(source));

    /// <inheritdoc cref="Luminosity(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Luminosity(Vector256<float> backdrop, Vector256<float> source)
        => Vector256.Create(
            Luminosity(backdrop.GetLower().AsVector4(), source.GetLower().AsVector4()).AsVector128(),
            Luminosity(backdrop.GetUpper().AsVector4(), source.GetUpper().AsVector4()).AsVector128());

    /// <inheritdoc cref="Luminosity(Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Luminosity(Vector512<float> backdrop, Vector512<float> source)
        => Vector512.Create(Luminosity(backdrop.GetLower(), source.GetLower()), Luminosity(backdrop.GetUpper(), source.GetUpper()));

    /// <summary>
    /// Applies raster coverage to a Porter-Duff composition result.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The Porter-Duff composition result.</param>
    /// <param name="coverage">The coverage. Range 0..1.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 BlendWithCoverage(Vector4 backdrop, Vector4 source, float coverage)
    {
        Vector4 backdropAlpha = Numerics.PermuteW(backdrop);
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        Vector4 backdropPremultiplied = Numerics.WithW(backdrop * backdropAlpha, backdropAlpha);
        Vector4 sourcePremultiplied = Numerics.WithW(source * sourceAlpha, sourceAlpha);

        // Use the same fused operation as the wider paths so exact midpoints cannot change across vector widths.
        Vector4 result = Vector128.MultiplyAddEstimate((sourcePremultiplied - backdropPremultiplied).AsVector128(), Vector128.Create(coverage), backdropPremultiplied.AsVector128()).AsVector4();

        Numerics.UnPremultiply(ref result);
        return result;
    }

    /// <summary>
    /// Applies raster coverage to a Porter-Duff composition result.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The Porter-Duff composition result.</param>
    /// <param name="coverage">The coverage. Range 0..1.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> BlendWithCoverage(Vector256<float> backdrop, Vector256<float> source, Vector256<float> coverage)
    {
        Vector256<float> backdropAlpha = Avx.Permute(backdrop, ShuffleAlphaControl);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> backdropPremultiplied = Avx.Blend(backdrop * backdropAlpha, backdropAlpha, BlendAlphaControl);
        Vector256<float> sourcePremultiplied = Avx.Blend(source * sourceAlpha, sourceAlpha, BlendAlphaControl);
        Vector256<float> result = Vector256.MultiplyAddEstimate(sourcePremultiplied - backdropPremultiplied, coverage, backdropPremultiplied);

        return Numerics.UnPremultiply(result, Avx.Permute(result, ShuffleAlphaControl));
    }

    /// <summary>
    /// Applies raster coverage to a Porter-Duff composition result.
    /// </summary>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The Porter-Duff composition result.</param>
    /// <param name="coverage">The coverage. Range 0..1.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> BlendWithCoverage(Vector512<float> backdrop, Vector512<float> source, Vector512<float> coverage)
    {
        Vector512<float> backdropAlpha = Vector512_.ShuffleNative(backdrop, ShuffleAlphaControl);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> alphaMask = AlphaMask512();
        Vector512<float> backdropPremultiplied = Vector512.ConditionalSelect(alphaMask, backdropAlpha, backdrop * backdropAlpha);
        Vector512<float> sourcePremultiplied = Vector512.ConditionalSelect(alphaMask, sourceAlpha, source * sourceAlpha);
        Vector512<float> result = Vector512.MultiplyAddEstimate(sourcePremultiplied - backdropPremultiplied, coverage, backdropPremultiplied);

        return Numerics.UnPremultiply(result, Vector512_.ShuffleNative(result, ShuffleAlphaControl));
    }

    /// <summary>
    /// Calculates one ColorDodge component, including the singular cases defined by the blending model.
    /// </summary>
    /// <param name="backdrop">The backdrop component.</param>
    /// <param name="source">The source component.</param>
    /// <returns>The blended component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ColorDodgeValue(float backdrop, float source)
        => backdrop == 0F ? 0F : source == 1F ? 1F : MathF.Min(1F, backdrop / (1F - source));

    /// <summary>
    /// Calculates one ColorBurn component, including the singular cases defined by the blending model.
    /// </summary>
    /// <param name="backdrop">The backdrop component.</param>
    /// <param name="source">The source component.</param>
    /// <returns>The blended component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ColorBurnValue(float backdrop, float source)
        => backdrop == 1F ? 1F : source == 0F ? 0F : 1F - MathF.Min(1F, (1F - backdrop) / source);

    /// <summary>
    /// Calculates one SoftLight component.
    /// </summary>
    /// <param name="backdrop">The backdrop component.</param>
    /// <param name="source">The source component.</param>
    /// <returns>The blended component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SoftLightValue(float backdrop, float source)
    {
        if (source <= .5F)
        {
            return backdrop - ((1F - (2F * source)) * backdrop * (1F - backdrop));
        }

        // The cubic segment joins the square-root segment smoothly while avoiding its steep slope near zero.
        float d = backdrop <= .25F
            ? (((((16F * backdrop) - 12F) * backdrop) + 4F) * backdrop)
            : MathF.Sqrt(backdrop);

        return backdrop + (((2F * source) - 1F) * (d - backdrop));
    }

    /// <summary>
    /// Calculates the saturation of an RGB color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The difference between the maximum and minimum RGB components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Saturation(Vector4 color)
        => MathF.Max(color.X, MathF.Max(color.Y, color.Z)) - MathF.Min(color.X, MathF.Min(color.Y, color.Z));

    /// <summary>
    /// Calculates the luminosity of an RGB color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The weighted RGB luminosity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Luminosity(Vector4 color)
        => (.3F * color.X) + (.59F * color.Y) + (.11F * color.Z);

    /// <summary>
    /// Replaces the saturation of an RGB color while retaining its channel ordering.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="saturation">The required saturation.</param>
    /// <returns>The adjusted color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 SetSaturation(Vector4 color, float saturation)
    {
        float minimum = MathF.Min(color.X, MathF.Min(color.Y, color.Z));
        float range = MathF.Max(color.X, MathF.Max(color.Y, color.Z)) - minimum;
        if (range == 0F)
        {
            return Vector4.Zero;
        }

        // Translating the minimum to zero and scaling the range is equivalent to the specification's ordered-channel construction.
        return (color - new Vector4(minimum)) * (saturation / range);
    }

    /// <summary>
    /// Replaces the luminosity of an RGB color and clips the result to the representable gamut.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="luminosity">The required luminosity.</param>
    /// <returns>The adjusted color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 SetLuminosity(Vector4 color, float luminosity)
    {
        color += new Vector4(luminosity - Luminosity(color));

        float minimum = MathF.Min(color.X, MathF.Min(color.Y, color.Z));
        if (minimum < 0F)
        {
            // Pull every component toward the requested luminosity by the same ratio so hue is preserved.
            color = new Vector4(luminosity) + ((color - new Vector4(luminosity)) * (luminosity / (luminosity - minimum)));
        }

        float maximum = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        if (maximum > 1F)
        {
            color = new Vector4(luminosity) + ((color - new Vector4(luminosity)) * ((1F - luminosity) / (maximum - luminosity)));
        }

        return Numerics.WithW(color, Vector4.Zero);
    }

    /// <summary>
    /// Helper function for Overlay and HardLight modes
    /// </summary>
    /// <param name="backdrop">Backdrop color element</param>
    /// <param name="source">Source color element</param>
    /// <returns>Overlay value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float OverlayValueFunction(float backdrop, float source)
        => backdrop <= 0.5f ? (2 * backdrop * source) : 1 - (2 * (1 - source) * (1 - backdrop));

    /// <summary>
    /// Helper function for Overlay and HardLight modes
    /// </summary>
    /// <param name="backdrop">Backdrop color element</param>
    /// <param name="source">Source color element</param>
    /// <returns>Overlay value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> OverlayValueFunction(Vector256<float> backdrop, Vector256<float> source)
    {
        Vector256<float> vOne = Vector256.Create(1F);
        Vector256<float> left = (backdrop + backdrop) * source;

        Vector256<float> vOneMinusSource = Avx.Subtract(vOne, source);
        Vector256<float> right = Vector256_.MultiplyAddNegated(vOne, vOneMinusSource + vOneMinusSource, vOne - backdrop);
        Vector256<float> cmp = Avx.CompareGreaterThan(backdrop, Vector256.Create(.5F));
        return Avx.BlendVariable(left, right, cmp);
    }

    /// <summary>
    /// Helper function for Overlay and HardLight modes
    /// </summary>
    /// <param name="backdrop">Backdrop color element</param>
    /// <param name="source">Source color element</param>
    /// <returns>Overlay value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> OverlayValueFunction(Vector512<float> backdrop, Vector512<float> source)
    {
        Vector512<float> vOne = Vector512.Create(1F);
        Vector512<float> left = (backdrop + backdrop) * source;

        Vector512<float> vOneMinusSource = vOne - source;
        Vector512<float> right = Vector512_.MultiplyAddNegated(vOne, vOneMinusSource + vOneMinusSource, vOne - backdrop);
        Vector512<float> cmp = Avx512F.CompareGreaterThan(backdrop, Vector512.Create(.5F));
        return Vector512.ConditionalSelect(cmp, right, left);
    }

    /// <summary>
    /// Returns the result of the "Over" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="blend">The amount to blend. Range 0..1</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Over(Vector4 destination, Vector4 source, Vector4 blend)
    {
        // calculate weights
        Vector4 sW = Numerics.PermuteW(source);
        Vector4 dW = Numerics.PermuteW(destination);

        Vector4 blendW = sW * dW;
        Vector4 dstW = dW - blendW;
        Vector4 srcW = sW - blendW;

        // calculate final alpha
        Vector4 alpha = dstW + sW;

        // calculate final color
        Vector4 color = (destination * dstW) + (source * srcW) + (blend * blendW);

        // unpremultiply
        Numerics.UnPremultiply(ref color, alpha);
        return color;
    }

    /// <summary>
    /// Returns the result of the "Over" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="blend">The amount to blend. Range 0..1</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Over(Vector256<float> destination, Vector256<float> source, Vector256<float> blend)
    {
        // calculate weights
        Vector256<float> sW = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> dW = Avx.Permute(destination, ShuffleAlphaControl);

        Vector256<float> blendW = sW * dW;
        Vector256<float> dstW = dW - blendW;
        Vector256<float> srcW = sW - blendW;

        // calculate final alpha
        Vector256<float> alpha = dstW + sW;

        // calculate final color
        Vector256<float> color = destination * dstW;
        color = Vector256.MultiplyAddEstimate(source, srcW, color);
        color = Vector256.MultiplyAddEstimate(blend, blendW, color);

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "Over" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="blend">The amount to blend. Range 0..1</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Over(Vector512<float> destination, Vector512<float> source, Vector512<float> blend)
    {
        // calculate weights
        Vector512<float> sW = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> dW = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);

        Vector512<float> blendW = sW * dW;
        Vector512<float> dstW = dW - blendW;
        Vector512<float> srcW = sW - blendW;

        // calculate final alpha
        Vector512<float> alpha = dstW + sW;

        // calculate final color
        Vector512<float> color = destination * dstW;
        color = Vector512.MultiplyAddEstimate(source, srcW, color);
        color = Vector512.MultiplyAddEstimate(blend, blendW, color);

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "Atop" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="blend">The amount to blend. Range 0..1</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Atop(Vector4 destination, Vector4 source, Vector4 blend)
    {
        // calculate weights
        Vector4 sW = Numerics.PermuteW(source);
        Vector4 dW = Numerics.PermuteW(destination);

        Vector4 blendW = sW * dW;
        Vector4 dstW = dW - blendW;

        // calculate final alpha
        Vector4 alpha = dW;

        // calculate final color
        Vector4 color = (destination * dstW) + (blend * blendW);

        // unpremultiply
        Numerics.UnPremultiply(ref color, alpha);
        return color;
    }

    /// <summary>
    /// Returns the result of the "Atop" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="blend">The amount to blend. Range 0..1</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Atop(Vector256<float> destination, Vector256<float> source, Vector256<float> blend)
    {
        // calculate final alpha
        Vector256<float> alpha = Avx.Permute(destination, ShuffleAlphaControl);

        // calculate weights
        Vector256<float> sW = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> blendW = sW * alpha;
        Vector256<float> dstW = alpha - blendW;

        // calculate final color
        Vector256<float> color = Vector256.MultiplyAddEstimate(destination, dstW, Avx.Multiply(blend, blendW));

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "Atop" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="blend">The amount to blend. Range 0..1</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Atop(Vector512<float> destination, Vector512<float> source, Vector512<float> blend)
    {
        // calculate final alpha
        Vector512<float> alpha = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);

        // calculate weights
        Vector512<float> sW = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> blendW = sW * alpha;
        Vector512<float> dstW = alpha - blendW;

        // calculate final color
        Vector512<float> color = Vector512.MultiplyAddEstimate(destination, dstW, blend * blendW);

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "In" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 In(Vector4 destination, Vector4 source)
    {
        Vector4 sW = Numerics.PermuteW(source);
        Vector4 dW = Numerics.PermuteW(destination);
        Vector4 alpha = dW * sW;

        Vector4 color = source * alpha;            // premultiply
        Numerics.UnPremultiply(ref color, alpha);  // unpremultiply
        return color;
    }

    /// <summary>
    /// Returns the result of the "In" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> In(Vector256<float> destination, Vector256<float> source)
    {
        // calculate alpha
        Vector256<float> alpha = Avx.Permute(source * destination, ShuffleAlphaControl);

        // premultiply
        Vector256<float> color = source * alpha;

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "In" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> In(Vector512<float> destination, Vector512<float> source)
    {
        // calculate alpha
        Vector512<float> alpha = Vector512_.ShuffleNative(source * destination, ShuffleAlphaControl);

        // premultiply
        Vector512<float> color = source * alpha;

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "Out" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Out(Vector4 destination, Vector4 source)
    {
        Vector4 sW = Numerics.PermuteW(source);
        Vector4 dW = Numerics.PermuteW(destination);
        Vector4 alpha = (Vector4.One - dW) * sW;

        Vector4 color = source * alpha;            // premultiply
        Numerics.UnPremultiply(ref color, alpha);  // unpremultiply
        return color;
    }

    /// <summary>
    /// Returns the result of the "Out" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Out(Vector256<float> destination, Vector256<float> source)
    {
        // calculate alpha
        Vector256<float> alpha = Avx.Permute(source * (Vector256.Create(1F) - destination), ShuffleAlphaControl);

        // premultiply
        Vector256<float> color = source * alpha;

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "Out" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Out(Vector512<float> destination, Vector512<float> source)
    {
        // calculate alpha
        Vector512<float> alpha = Vector512_.ShuffleNative(source * (Vector512.Create(1F) - destination), ShuffleAlphaControl);

        // premultiply
        Vector512<float> color = source * alpha;

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "XOr" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Xor(Vector4 destination, Vector4 source)
    {
        Vector4 sW = Numerics.PermuteW(source);
        Vector4 dW = Numerics.PermuteW(destination);

        Vector4 srcW = Vector4.One - dW;
        Vector4 dstW = Vector4.One - sW;

        Vector4 alpha = (sW * srcW) + (dW * dstW);
        Vector4 color = (sW * source * srcW) + (dW * destination * dstW);

        // unpremultiply
        Numerics.UnPremultiply(ref color, alpha);
        return color;
    }

    /// <summary>
    /// Returns the result of the "XOr" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Xor(Vector256<float> destination, Vector256<float> source)
    {
        // calculate weights
        Vector256<float> sW = Avx.Shuffle(source, source, ShuffleAlphaControl);
        Vector256<float> dW = Avx.Shuffle(destination, destination, ShuffleAlphaControl);

        Vector256<float> vOne = Vector256.Create(1F);
        Vector256<float> srcW = vOne - dW;
        Vector256<float> dstW = vOne - sW;

        // calculate alpha
        Vector256<float> alpha = Vector256.MultiplyAddEstimate(sW, srcW, Avx.Multiply(dW, dstW));
        Vector256<float> color = Vector256.MultiplyAddEstimate(Avx.Multiply(sW, source), srcW, Avx.Multiply(Avx.Multiply(dW, destination), dstW));

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "XOr" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Xor(Vector512<float> destination, Vector512<float> source)
    {
        // calculate weights
        Vector512<float> sW = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> dW = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);

        Vector512<float> vOne = Vector512.Create(1F);
        Vector512<float> srcW = vOne - dW;
        Vector512<float> dstW = vOne - sW;

        // calculate alpha
        Vector512<float> alpha = Vector512.MultiplyAddEstimate(sW, srcW, dW * dstW);
        Vector512<float> color = Vector512.MultiplyAddEstimate(sW * source, srcW, (dW * destination) * dstW);

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    /// <summary>
    /// Returns the result of the "Plus" compositing equation.
    /// </summary>
    /// <param name="destination">The destination vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="blend">The blended source and destination color.</param>
    /// <returns>The composition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Plus(Vector4 destination, Vector4 source, Vector4 blend)
    {
        Vector4 sourceAlpha = Numerics.PermuteW(source);
        Vector4 destinationAlpha = Numerics.PermuteW(destination);
        Vector4 overlap = sourceAlpha * destinationAlpha;

        // Plus retains both non-overlapping regions and uses the selected blend mode in their overlap.
        Vector4 color = (destination * destinationAlpha) + (source * (sourceAlpha - overlap)) + (blend * overlap);
        Vector4 alpha = Vector4.Min(Vector4.One, sourceAlpha + destinationAlpha);
        color = Vector4.Min(Vector4.One, color);

        Numerics.UnPremultiply(ref color, alpha);
        return color;
    }

    /// <inheritdoc cref="Plus(Vector4, Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Plus(Vector256<float> destination, Vector256<float> source, Vector256<float> blend)
    {
        Vector256<float> one = Vector256.Create(1F);
        Vector256<float> sourceAlpha = Avx.Permute(source, ShuffleAlphaControl);
        Vector256<float> destinationAlpha = Avx.Permute(destination, ShuffleAlphaControl);
        Vector256<float> overlap = sourceAlpha * destinationAlpha;
        Vector256<float> color = (destination * destinationAlpha) + (source * (sourceAlpha - overlap)) + (blend * overlap);
        Vector256<float> alpha = Vector256.Min(one, sourceAlpha + destinationAlpha);

        return Numerics.UnPremultiply(Vector256.Min(one, color), alpha);
    }

    /// <inheritdoc cref="Plus(Vector4, Vector4, Vector4)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Plus(Vector512<float> destination, Vector512<float> source, Vector512<float> blend)
    {
        Vector512<float> one = Vector512.Create(1F);
        Vector512<float> sourceAlpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> destinationAlpha = Vector512_.ShuffleNative(destination, ShuffleAlphaControl);
        Vector512<float> overlap = sourceAlpha * destinationAlpha;
        Vector512<float> color = (destination * destinationAlpha) + (source * (sourceAlpha - overlap)) + (blend * overlap);
        Vector512<float> alpha = Vector512.Min(one, sourceAlpha + destinationAlpha);

        return Numerics.UnPremultiply(Vector512.Min(one, color), alpha);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 Clear(Vector4 backdrop, Vector4 source) => Vector4.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Clear(Vector256<float> backdrop, Vector256<float> source) => Vector256<float>.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> Clear(Vector512<float> backdrop, Vector512<float> source) => Vector512<float>.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> AlphaMask512()
        => Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
}
