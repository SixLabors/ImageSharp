// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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
        => Avx.Multiply(backdrop, source);

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
        => Avx.Min(Vector256.Create(1F), Avx.Add(backdrop, source));

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
        => Avx.Max(Vector256<float>.Zero, Avx.Subtract(backdrop, source));

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
        return SimdUtils.HwIntrinsics.MultiplyAddNegated(Avx.Subtract(vOne, backdrop), Avx.Subtract(vOne, source), vOne);
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
        => Avx.Min(backdrop, source);

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
        => Avx.Max(backdrop, source);

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
        return Avx.Min(Vector256.Create(1F), Avx.Blend(color, Vector256<float>.Zero, BlendAlphaControl));
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
        return Avx.Min(Vector256.Create(1F), Avx.Blend(color, Vector256<float>.Zero, BlendAlphaControl));
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
        Vector256<float> left = Avx.Multiply(Avx.Add(backdrop, backdrop), source);

        Vector256<float> vOneMinusSource = Avx.Subtract(vOne, source);
        Vector256<float> right = SimdUtils.HwIntrinsics.MultiplyAddNegated(Avx.Add(vOneMinusSource, vOneMinusSource), Avx.Subtract(vOne, backdrop), vOne);
        Vector256<float> cmp = Avx.CompareGreaterThan(backdrop, Vector256.Create(.5F));
        return Avx.BlendVariable(left, right, cmp);
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

        Vector256<float> blendW = Avx.Multiply(sW, dW);
        Vector256<float> dstW = Avx.Subtract(dW, blendW);
        Vector256<float> srcW = Avx.Subtract(sW, blendW);

        // calculate final alpha
        Vector256<float> alpha = Avx.Add(dstW, sW);

        // calculate final color
        Vector256<float> color = Avx.Multiply(destination, dstW);
        color = SimdUtils.HwIntrinsics.MultiplyAdd(color, source, srcW);
        color = SimdUtils.HwIntrinsics.MultiplyAdd(color, blend, blendW);

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
        Vector256<float> blendW = Avx.Multiply(sW, alpha);
        Vector256<float> dstW = Avx.Subtract(alpha, blendW);

        // calculate final color
        Vector256<float> color = SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(blend, blendW), destination, dstW);

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
        Vector256<float> alpha = Avx.Permute(Avx.Multiply(source, destination), ShuffleAlphaControl);

        // premultiply
        Vector256<float> color = Avx.Multiply(source, alpha);

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
        Vector256<float> alpha = Avx.Permute(Avx.Multiply(source, Avx.Subtract(Vector256.Create(1F), destination)), ShuffleAlphaControl);

        // premultiply
        Vector256<float> color = Avx.Multiply(source, alpha);

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
        Vector256<float> srcW = Avx.Subtract(vOne, dW);
        Vector256<float> dstW = Avx.Subtract(vOne, sW);

        // calculate alpha
        Vector256<float> alpha = SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(dW, dstW), sW, srcW);
        Vector256<float> color = SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(Avx.Multiply(dW, destination), dstW), Avx.Multiply(sW, source), srcW);

        // unpremultiply
        return Numerics.UnPremultiply(color, alpha);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 Clear(Vector4 backdrop, Vector4 source) => Vector4.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Clear(Vector256<float> backdrop, Vector256<float> source) => Vector256<float>.Zero;
}
