// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Converts between floating-point TIFF grayscale samples and RGB without clipping extended values.
/// </summary>
internal readonly struct TiffGrayFloatOperator : ITiffColorOperator<float>
{
    private readonly bool whiteIsZero;
    private readonly bool useFirstComponent;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGrayFloatOperator"/> struct.
    /// </summary>
    /// <param name="whiteIsZero">Whether stored intensity is the complement of decoded intensity.</param>
    public TiffGrayFloatOperator(bool whiteIsZero)
        : this(whiteIsZero, useFirstComponent: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGrayFloatOperator"/> struct with the encoding input layout.
    /// </summary>
    /// <param name="whiteIsZero">Whether stored intensity is the complement of decoded intensity.</param>
    /// <param name="useFirstComponent">Whether encoding input already repeats one grayscale device value in RGB.</param>
    public TiffGrayFloatOperator(bool whiteIsZero, bool useFirstComponent)
    {
        this.whiteIsZero = whiteIsZero;
        this.useFirstComponent = useFirstComponent;
    }

    /// <inheritdoc />
    public PixelAlphaRepresentation AlphaRepresentation => PixelAlphaRepresentation.None;

    /// <summary>
    /// Expands one stored grayscale value to opaque RGB.
    /// </summary>
    /// <param name="c0">The stored grayscale value, replaced by red.</param>
    /// <param name="c1">Replaced by green.</param>
    /// <param name="c2">Replaced by blue.</param>
    /// <param name="c3">Replaced by opaque alpha.</param>
    public void ConvertToRgb(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        // BlackIsZero passes the original bits through. WhiteIsZero converts stored intensity with 1 - value.
        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = 1F;
    }

    /// <inheritdoc cref="ConvertToRgb(ref float, ref float, ref float, ref float)" />
    public void ConvertToRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        // Four lanes contain four independent grayscale samples. Repeat the converted values in RGB and supply opaque alpha.
        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = Vector128.Create(1F);
    }

    /// <inheritdoc cref="ConvertToRgb(ref float, ref float, ref float, ref float)" />
    public void ConvertToRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        // Eight lanes use the same intensity operation as the four-lane path, without mixing pixels between lanes.
        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = Vector256.Create(1F);
    }

    /// <inheritdoc cref="ConvertToRgb(ref float, ref float, ref float, ref float)" />
    public void ConvertToRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        // Sixteen lanes cover a complete 512-bit group. The traversal selects narrower groups and the scalar tail separately.
        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = Vector512.Create(1F);
    }

    /// <summary>
    /// Converts RGB to a stored grayscale value, repeated in the color components with opaque alpha.
    /// </summary>
    /// <param name="c0">Red, replaced by stored grayscale.</param>
    /// <param name="c1">Green, replaced by stored grayscale.</param>
    /// <param name="c2">Blue, replaced by stored grayscale.</param>
    /// <param name="c3">Source alpha, replaced by opaque alpha.</param>
    public void ConvertFromRgb(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        // A grayscale profile already supplies its device intensity in each RGB component. Reweighting it could add rounding.
        // Ordinary RGB instead uses the existing BT.709 luminance calculation before the TIFF intensity convention is applied.
        if (!this.useFirstComponent)
        {
            c0 = ColorNumerics.GetBT709Luminance(new Vector4(c0, c1, c2, c3));
        }

        // WhiteIsZero uses the same subtraction in both directions. The grayscale store writes only c0.
        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = 1F;
    }

    /// <inheritdoc cref="ConvertFromRgb(ref float, ref float, ref float, ref float)" />
    public void ConvertFromRgb(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        if (!this.useFirstComponent)
        {
            // Each lane holds one pixel. Keep the scalar operation order, including alpha's zero-weight term:
            // NaN or infinite alpha must affect the result in the same way as the scalar dot product.
            Vector4 weights = ColorNumerics.Bt709;
            c0 = (((c0 * Vector128.Create(weights.X)) + (c1 * Vector128.Create(weights.Y)))
                + (c2 * Vector128.Create(weights.Z))) + (c3 * Vector128.Create(weights.W));
        }

        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = Vector128.Create(1F);
    }

    /// <inheritdoc cref="ConvertFromRgb(ref float, ref float, ref float, ref float)" />
    public void ConvertFromRgb(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        if (!this.useFirstComponent)
        {
            // Eight lanes use the same weights and operation order as the four-lane and scalar paths.
            Vector4 weights = ColorNumerics.Bt709;
            c0 = (((c0 * Vector256.Create(weights.X)) + (c1 * Vector256.Create(weights.Y)))
                + (c2 * Vector256.Create(weights.Z))) + (c3 * Vector256.Create(weights.W));
        }

        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = Vector256.Create(1F);
    }

    /// <inheritdoc cref="ConvertFromRgb(ref float, ref float, ref float, ref float)" />
    public void ConvertFromRgb(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        if (!this.useFirstComponent)
        {
            // Sixteen lanes cover one full Vector512<float> group. The shared traversal handles narrower groups and the scalar tail.
            Vector4 weights = ColorNumerics.Bt709;
            c0 = (((c0 * Vector512.Create(weights.X)) + (c1 * Vector512.Create(weights.Y)))
                + (c2 * Vector512.Create(weights.Z))) + (c3 * Vector512.Create(weights.W));
        }

        c0 = this.ApplyWhiteIsZero(c0);
        c1 = c2 = c0;
        c3 = Vector512.Create(1F);
    }

    /// <summary>
    /// Applies WhiteIsZero inversion when selected. BlackIsZero returns the original bits without arithmetic.
    /// </summary>
    /// <param name="value">The grayscale intensity or stored sample.</param>
    /// <returns>One minus the value for WhiteIsZero; otherwise, the original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ApplyWhiteIsZero(float value) => this.whiteIsZero ? 1F - value : value;

    /// <inheritdoc cref="ApplyWhiteIsZero(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector128<float> ApplyWhiteIsZero(Vector128<float> value) => this.whiteIsZero ? Vector128.Create(1F) - value : value;

    /// <inheritdoc cref="ApplyWhiteIsZero(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector256<float> ApplyWhiteIsZero(Vector256<float> value) => this.whiteIsZero ? Vector256.Create(1F) - value : value;

    /// <inheritdoc cref="ApplyWhiteIsZero(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector512<float> ApplyWhiteIsZero(Vector512<float> value) => this.whiteIsZero ? Vector512.Create(1F) - value : value;

    // Decode pipelines use Convert from the existing operator contract. Keep its direction explicit here.
    // The writer selects the FromRgb struct below, so the shared traversal needs no direction flag.

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ITiffColorOperator<float>.Convert(ref float c0, ref float c1, ref float c2, ref float c3)
        => this.ConvertToRgb(ref c0, ref c1, ref c2, ref c3);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ITiffColorOperator<float>.Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        => this.ConvertToRgb(ref c0, ref c1, ref c2, ref c3);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ITiffColorOperator<float>.Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        => this.ConvertToRgb(ref c0, ref c1, ref c2, ref c3);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ITiffColorOperator<float>.Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        => this.ConvertToRgb(ref c0, ref c1, ref c2, ref c3);

    /// <summary>
    /// Selects RGB-to-grayscale arithmetic for the same source/operator/store traversal used by decoding.
    /// </summary>
    /// <remarks>
    /// The closed struct selects the direction before traversal. Each call handles scalar values or registers;
    /// it adds no per-pixel direction branch, row storage, or second traversal.
    /// </remarks>
    internal readonly struct FromRgb : ITiffColorOperator<float>
    {
        private readonly TiffGrayFloatOperator colorOperator;

        /// <summary>
        /// Initializes a new instance of the <see cref="FromRgb"/> struct with the resolved grayscale settings.
        /// </summary>
        /// <param name="colorOperator">The shared grayscale model and encoding input layout.</param>
        public FromRgb(TiffGrayFloatOperator colorOperator) => this.colorOperator = colorOperator;

        /// <inheritdoc />
        public PixelAlphaRepresentation AlphaRepresentation => PixelAlphaRepresentation.None;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
            => this.colorOperator.ConvertFromRgb(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
            => this.colorOperator.ConvertFromRgb(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
            => this.colorOperator.ConvertFromRgb(ref c0, ref c1, ref c2, ref c3);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
            => this.colorOperator.ConvertFromRgb(ref c0, ref c1, ref c2, ref c3);
    }
}
