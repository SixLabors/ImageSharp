// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Converts TIFF YCbCr values to RGB using the stored reference levels and luma coefficients.
/// </summary>
internal readonly struct TiffYCbCrOperator
{
    private static readonly Rational[] DefaultLuma = [new(299, 1000), new(587, 1000), new(114, 1000)];
    private static readonly Rational[] DefaultReferenceBlackWhite = [new(0, 1), new(255, 1), new(128, 1), new(255, 1), new(128, 1), new(255, 1)];

    private readonly float yScale;
    private readonly float yOffset;
    private readonly float cbScale;
    private readonly float cbOffset;
    private readonly float crScale;
    private readonly float crOffset;
    private readonly float crToRed;
    private readonly float cbToBlue;
    private readonly float yToGreen;
    private readonly float crToGreen;
    private readonly float cbToGreen;
    private readonly Vector3 referenceBlackOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffYCbCrOperator"/> struct.
    /// </summary>
    /// <param name="referenceBlackAndWhite">The component reference ranges.</param>
    /// <param name="coefficients">The luma coefficients.</param>
    public TiffYCbCrOperator(Rational[] referenceBlackAndWhite, Rational[] coefficients)
    {
        referenceBlackAndWhite ??= DefaultReferenceBlackWhite;
        coefficients ??= DefaultLuma;

        if (referenceBlackAndWhite.Length != 6)
        {
            TiffThrowHelper.ThrowImageFormatException("reference black and white array should have 6 entry's");
        }

        if (coefficients.Length != 3)
        {
            TiffThrowHelper.ThrowImageFormatException("luma coefficients array should have 3 entries");
        }

        float yBlack = referenceBlackAndWhite[0].ToSingle();
        float yWhite = referenceBlackAndWhite[1].ToSingle();
        float cbBlack = referenceBlackAndWhite[2].ToSingle();
        float cbWhite = referenceBlackAndWhite[3].ToSingle();
        float crBlack = referenceBlackAndWhite[4].ToSingle();
        float crWhite = referenceBlackAndWhite[5].ToSingle();

        // TIFF's Y black and white references map to 0 and 255. Each chroma black and white pair maps to 0 and 127.
        // A chroma sample below its black reference produces a negative color difference.
        // For byte output, multiply each code before subtracting its scaled black reference.
        // This keeps the established floating-point order; round only the final RGB output.
        this.yScale = 255F / (yWhite - yBlack);
        this.yOffset = this.yScale * yBlack;
        this.cbScale = 127F / (cbWhite - cbBlack);
        this.cbOffset = this.cbScale * cbBlack;
        this.crScale = 127F / (crWhite - crBlack);
        this.crOffset = this.crScale * crBlack;

        float lumaRed = coefficients[0].ToSingle();
        float lumaGreen = coefficients[1].ToSingle();
        float lumaBlue = coefficients[2].ToSingle();

        // These terms solve the TIFF luma equation for RGB after the range expansion.
        this.crToRed = 2 - (2 * lumaRed);
        this.cbToBlue = 2 - (2 * lumaBlue);
        this.yToGreen = (1 - lumaBlue - lumaRed) / lumaGreen;
        this.crToGreen = 2 * lumaRed * (lumaRed - 1) / lumaGreen;
        this.cbToGreen = 2 * lumaBlue * (lumaBlue - 1) / lumaGreen;
        this.referenceBlackOffset = new Vector3(yBlack, cbBlack, crBlack) / 255F;
    }

    /// <summary>
    /// Gets the transform between TIFF YCbCr samples and device RGB.
    /// </summary>
    public YCbCrTransform ProfileTransform
    {
        get
        {
            // An RGB ICC profile expects TIFF YCbCr converted to RGB. ICC then maps that RGB to sRGB.
            //
            // The TIFF reader divides stored codes by 255. Black reference values are also divided by 255.
            // The shared converter subtracts each black reference from its matching Y, Cb, or Cr sample.

            // TIFF reference ranges and luma coefficients set the conversion weights.
            //
            // The first three rows output R, G, and B; their columns hold Y, Cb, and Cr differences.
            // The shared converter transposes this matrix to match Vector3.Transform's multiplication order.
            Matrix4x4 yCbCrToRgb = new()
            {
                M11 = this.yScale,
                M13 = this.crScale * this.crToRed,
                M21 = this.yScale * this.yToGreen,
                M22 = this.cbScale * this.cbToGreen,
                M23 = this.crScale * this.crToGreen,
                M31 = this.yScale,
                M32 = this.cbScale * this.cbToBlue,

                // Unassigned channel terms are zero. Keep the fourth axis unchanged when inverting the matrix.
                M44 = 1F,
            };

            // The shared transform requires both conversion directions. Decoder setup derives RGB to YCbCr once.
            // Decoding uses only the YCbCr to RGB matrix.
            Matrix4x4.Invert(yCbCrToRgb, out Matrix4x4 rgbToYCbCr);
            return new YCbCrTransform(rgbToYCbCr, yCbCrToRgb, this.referenceBlackOffset);
        }
    }

    /// <summary>
    /// Converts one stored byte-code triple to clipped device RGB codes.
    /// </summary>
    /// <param name="y">The luminance code.</param>
    /// <param name="cb">The blue-difference code.</param>
    /// <param name="cr">The red-difference code.</param>
    /// <param name="red">The rounded red code.</param>
    /// <param name="green">The rounded green code.</param>
    /// <param name="blue">The rounded blue code.</param>
    public void ConvertByte(float y, float cb, float cr, out byte red, out byte green, out byte blue)
    {
        this.ConvertRaw(y, cb, cr, out float r, out float g, out float b);
        red = (byte)Numerics.Clamp((int)MathF.Round(r), 0, 255);
        green = (byte)Numerics.Clamp((int)MathF.Round(g), 0, 255);
        blue = (byte)Numerics.Clamp((int)MathF.Round(b), 0, 255);
    }

    /// <summary>
    /// Converts four stored byte-code triples to clipped device RGB codes.
    /// </summary>
    /// <param name="y">Luminance lanes.</param>
    /// <param name="cb">Blue-difference lanes.</param>
    /// <param name="cr">Red-difference lanes.</param>
    /// <param name="red">Rounded red codes.</param>
    /// <param name="green">Rounded green codes.</param>
    /// <param name="blue">Rounded blue codes.</param>
    public void ConvertByte(
        Vector128<float> y,
        Vector128<float> cb,
        Vector128<float> cr,
        out Vector128<int> red,
        out Vector128<int> green,
        out Vector128<int> blue)
    {
        this.ConvertRaw(y, cb, cr, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b);

        // The byte limits are integers, so clamping before rounding produces the same final byte.
        // Bounded samples can use shared round-to-even conversion without generic integer-overflow handling.
        Vector128<float> minimum = Vector128<float>.Zero;
        Vector128<float> maximum = Vector128.Create(255F);
        red = Vector128_.ConvertToInt32RoundToEven(Numerics.Clamp(r, minimum, maximum));
        green = Vector128_.ConvertToInt32RoundToEven(Numerics.Clamp(g, minimum, maximum));
        blue = Vector128_.ConvertToInt32RoundToEven(Numerics.Clamp(b, minimum, maximum));
    }

    /// <summary>
    /// Converts eight stored byte-code triples to clipped device RGB codes.
    /// </summary>
    /// <param name="y">Luminance lanes.</param>
    /// <param name="cb">Blue-difference lanes.</param>
    /// <param name="cr">Red-difference lanes.</param>
    /// <param name="red">Rounded red codes.</param>
    /// <param name="green">Rounded green codes.</param>
    /// <param name="blue">Rounded blue codes.</param>
    public void ConvertByte(
        Vector256<float> y,
        Vector256<float> cb,
        Vector256<float> cr,
        out Vector256<int> red,
        out Vector256<int> green,
        out Vector256<int> blue)
    {
        this.ConvertRaw(y, cb, cr, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b);

        // The byte limits are integers, so clamping before rounding produces the same final byte.
        // Bounded samples can use shared round-to-even conversion without generic integer-overflow handling.
        Vector256<float> minimum = Vector256<float>.Zero;
        Vector256<float> maximum = Vector256.Create(255F);
        red = Vector256_.ConvertToInt32RoundToEven(Numerics.Clamp(r, minimum, maximum));
        green = Vector256_.ConvertToInt32RoundToEven(Numerics.Clamp(g, minimum, maximum));
        blue = Vector256_.ConvertToInt32RoundToEven(Numerics.Clamp(b, minimum, maximum));
    }

    /// <summary>
    /// Converts sixteen stored byte-code triples to clipped device RGB codes.
    /// </summary>
    /// <param name="y">Luminance lanes.</param>
    /// <param name="cb">Blue-difference lanes.</param>
    /// <param name="cr">Red-difference lanes.</param>
    /// <param name="red">Rounded red codes.</param>
    /// <param name="green">Rounded green codes.</param>
    /// <param name="blue">Rounded blue codes.</param>
    public void ConvertByte(
        Vector512<float> y,
        Vector512<float> cb,
        Vector512<float> cr,
        out Vector512<int> red,
        out Vector512<int> green,
        out Vector512<int> blue)
    {
        this.ConvertRaw(y, cb, cr, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b);

        // The byte limits are integers, so clamping before rounding produces the same final byte.
        // Bounded samples can use shared round-to-even conversion without generic integer-overflow handling.
        Vector512<float> minimum = Vector512<float>.Zero;
        Vector512<float> maximum = Vector512.Create(255F);
        red = Vector512_.ConvertToInt32RoundToEven(Numerics.Clamp(r, minimum, maximum));
        green = Vector512_.ConvertToInt32RoundToEven(Numerics.Clamp(g, minimum, maximum));
        blue = Vector512_.ConvertToInt32RoundToEven(Numerics.Clamp(b, minimum, maximum));
    }

    /// <summary>
    /// Expands one stored YCbCr triple and applies the TIFF luma matrix without clipping.
    /// </summary>
    /// <param name="y">Stored luminance code.</param>
    /// <param name="cb">Stored blue-difference code.</param>
    /// <param name="cr">Stored red-difference code.</param>
    /// <param name="red">Unclipped red code.</param>
    /// <param name="green">Unclipped green code.</param>
    /// <param name="blue">Unclipped blue code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConvertRaw(float y, float cb, float cr, out float red, out float green, out float blue)
    {
        float yy = (y * this.yScale) - this.yOffset;
        float cc = (cb * this.cbScale) - this.cbOffset;
        float rr = (cr * this.crScale) - this.crOffset;
        red = (rr * this.crToRed) + yy;
        green = (this.yToGreen * yy) + (this.crToGreen * rr) + (this.cbToGreen * cc);
        blue = (cc * this.cbToBlue) + yy;
    }

    /// <summary>
    /// Expands four stored YCbCr triples and applies the TIFF luma matrix without clipping.
    /// </summary>
    /// <param name="y">Stored luminance lanes.</param>
    /// <param name="cb">Stored blue-difference lanes.</param>
    /// <param name="cr">Stored red-difference lanes.</param>
    /// <param name="red">Unclipped red lanes.</param>
    /// <param name="green">Unclipped green lanes.</param>
    /// <param name="blue">Unclipped blue lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConvertRaw(
        Vector128<float> y,
        Vector128<float> cb,
        Vector128<float> cr,
        out Vector128<float> red,
        out Vector128<float> green,
        out Vector128<float> blue)
    {
        Vector128<float> yy = (y * Vector128.Create(this.yScale)) - Vector128.Create(this.yOffset);
        Vector128<float> cc = (cb * Vector128.Create(this.cbScale)) - Vector128.Create(this.cbOffset);
        Vector128<float> rr = (cr * Vector128.Create(this.crScale)) - Vector128.Create(this.crOffset);
        red = rr * Vector128.Create(this.crToRed);
        red += yy;
        green = Vector128.Create(this.yToGreen) * yy;
        green += Vector128.Create(this.crToGreen) * rr;
        green += Vector128.Create(this.cbToGreen) * cc;
        blue = cc * Vector128.Create(this.cbToBlue);
        blue += yy;
    }

    /// <summary>
    /// Expands eight stored YCbCr triples and applies the TIFF luma matrix without clipping.
    /// </summary>
    /// <param name="y">Stored luminance lanes.</param>
    /// <param name="cb">Stored blue-difference lanes.</param>
    /// <param name="cr">Stored red-difference lanes.</param>
    /// <param name="red">Unclipped red lanes.</param>
    /// <param name="green">Unclipped green lanes.</param>
    /// <param name="blue">Unclipped blue lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConvertRaw(
        Vector256<float> y,
        Vector256<float> cb,
        Vector256<float> cr,
        out Vector256<float> red,
        out Vector256<float> green,
        out Vector256<float> blue)
    {
        Vector256<float> yy = (y * Vector256.Create(this.yScale)) - Vector256.Create(this.yOffset);
        Vector256<float> cc = (cb * Vector256.Create(this.cbScale)) - Vector256.Create(this.cbOffset);
        Vector256<float> rr = (cr * Vector256.Create(this.crScale)) - Vector256.Create(this.crOffset);
        red = rr * Vector256.Create(this.crToRed);
        red += yy;
        green = Vector256.Create(this.yToGreen) * yy;
        green += Vector256.Create(this.crToGreen) * rr;
        green += Vector256.Create(this.cbToGreen) * cc;
        blue = cc * Vector256.Create(this.cbToBlue);
        blue += yy;
    }

    /// <summary>
    /// Expands sixteen stored YCbCr triples and applies the TIFF luma matrix without clipping.
    /// </summary>
    /// <param name="y">Stored luminance lanes.</param>
    /// <param name="cb">Stored blue-difference lanes.</param>
    /// <param name="cr">Stored red-difference lanes.</param>
    /// <param name="red">Unclipped red lanes.</param>
    /// <param name="green">Unclipped green lanes.</param>
    /// <param name="blue">Unclipped blue lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConvertRaw(
        Vector512<float> y,
        Vector512<float> cb,
        Vector512<float> cr,
        out Vector512<float> red,
        out Vector512<float> green,
        out Vector512<float> blue)
    {
        Vector512<float> yy = (y * Vector512.Create(this.yScale)) - Vector512.Create(this.yOffset);
        Vector512<float> cc = (cb * Vector512.Create(this.cbScale)) - Vector512.Create(this.cbOffset);
        Vector512<float> rr = (cr * Vector512.Create(this.crScale)) - Vector512.Create(this.crOffset);
        red = rr * Vector512.Create(this.crToRed);
        red += yy;
        green = Vector512.Create(this.yToGreen) * yy;
        green += Vector512.Create(this.crToGreen) * rr;
        green += Vector512.Create(this.cbToGreen) * cc;
        blue = cc * Vector512.Create(this.cbToBlue);
        blue += yy;
    }
}
