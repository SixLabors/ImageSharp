// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Implements sRGB companding.
/// </summary>
/// <remarks>
/// For more info see:
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
/// </remarks>
public static partial class SRgbCompanding
{
    private static Func<double, double, double> CompressFunction
        => (d, _) =>
        {
            if (d <= (0.04045 / 12.92))
            {
                return d * 12.92;
            }

            return (1.055 * Math.Pow(d, 1.0 / 2.4)) - 0.055;
        };

    private static Func<double, double, double> ExpandFunction
        => (d, _) =>
        {
            if (d <= 0.04045)
            {
                return d / 12.92;
            }

            return Math.Pow((d + 0.055) / 1.055, 2.4);
        };

    /// <summary>
    /// Compresses linear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Compress(Span<Vector4> vectors)
        => CompressExtended(vectors);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors to transform.</param>
    /// <param name="bitDepth">The required component precision. Eight-bit conversion clips RGB to [0,1].</param>
    public static void Compress(Span<Vector4> vectors, int bitDepth)
    {
        // Select the table only for eight-bit precision. Other paths keep full-range floating-point values.
        if (bitDepth == 8)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetCompressLookupTable<SRgbCompandingKey>(CompressFunction));
        }
        else
        {
            CompressExtended(vectors);
        }
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public static void Expand(Span<Vector4> vectors)
        => ExpandExtended(vectors);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors to transform.</param>
    /// <param name="bitDepth">The required component precision. Eight-bit conversion clips RGB to [0,1].</param>
    public static void Expand(Span<Vector4> vectors, int bitDepth)
    {
        // Select the table only for eight-bit precision. Other paths keep full-range floating-point values.
        if (bitDepth == 8)
        {
            CompandingUtilities.Compand(vectors, CompandingUtilities.GetExpandLookupTable<SRgbCompandingKey>(ExpandFunction));
        }
        else
        {
            ExpandExtended(vectors);
        }
    }

    /// <summary>
    /// Compresses linear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Compress(Vector4 vector)
        => CompressExtended(vector);

    /// <summary>
    /// Compresses linear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The colors to transform.</param>
    /// <param name="bitDepth">The required component precision. Eight-bit conversion clips RGB to [0,1].</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector, int bitDepth)
    {
        // Select the table only for eight-bit precision. Other paths keep full-range floating-point values.
        if (bitDepth == 8)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetCompressLookupTable<SRgbCompandingKey>(CompressFunction));
        }

        return CompressExtended(vector);
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping and retains alpha.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public static Vector4 Expand(Vector4 vector)
        => ExpandExtended(vector);

    /// <summary>
    /// Expands nonlinear RGB at the requested component precision and retains alpha.
    /// </summary>
    /// <param name="vector">The colors to transform.</param>
    /// <param name="bitDepth">The required component precision. Eight-bit conversion clips RGB to [0,1].</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector, int bitDepth)
    {
        // Select the table only for eight-bit precision. Other paths keep full-range floating-point values.
        if (bitDepth == 8)
        {
            return CompandingUtilities.Compand(vector, CompandingUtilities.GetExpandLookupTable<SRgbCompandingKey>(ExpandFunction));
        }

        return ExpandExtended(vector);
    }

    /// <summary>
    /// Expands nonlinear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors, including values outside the SDR range.</param>
    internal static void ExpandExtended(Span<Vector4> vectors)
        => TensorPrimitives_.Invoke(vectors, new ExpandOperator(1.055F, 0.055F, 0.04045F));

    /// <summary>
    /// Expands nonlinear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 ExpandExtended(Vector4 vector)
        => TensorPrimitives_.Invoke(vector, new ExpandOperator(1.055F, 0.055F, 0.04045F));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vectors">The colors, including values outside the SDR range.</param>
    internal static void CompressExtended(Span<Vector4> vectors)
        => TensorPrimitives_.Invoke(vectors, new CompressOperator(1.055F, 0.055F, 0.04045F / 12.92F));

    /// <summary>
    /// Compresses linear RGB without clipping the result and retains alpha.
    /// </summary>
    /// <param name="vector">The color, including values outside the SDR range.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    internal static Vector4 CompressExtended(Vector4 vector)
        => TensorPrimitives_.Invoke(vector, new CompressOperator(1.055F, 0.055F, 0.04045F / 12.92F));

    private class SRgbCompandingKey;
}
