// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Provides matrices for chromatic adaptation, facilitating the adjustment of color values
/// under different light sources to maintain color constancy. This class supports common
/// adaptation transforms based on the von Kries coefficient law, which assumes independent
/// scaling of the cone responses in the human eye. These matrices can be applied to convert
/// color coordinates between different illuminants, ensuring consistent color appearance
/// across various lighting conditions.
/// </summary>
/// <remarks>
/// Supported adaptation matrices include the Bradford, von Kries, and Sharp transforms.
/// These matrices are typically used in conjunction with color space conversions, such as from XYZ
/// to RGB, to achieve accurate color rendition in digital imaging applications.
/// </remarks>
public static class KnownChromaticAdaptationMatrices
{
    /// <summary>
    /// von Kries chromatic adaptation transform matrix (Hunt-Pointer-Estevez adjusted for D65)
    /// </summary>
    public static readonly Matrix4x4 VonKriesHPEAdjusted
        = Matrix4x4.Transpose(new Matrix4x4
        {
            M11 = 0.40024F,
            M12 = 0.7076F,
            M13 = -0.08081F,
            M21 = -0.2263F,
            M22 = 1.16532F,
            M23 = 0.0457F,
            M31 = 0,
            M32 = 0,
            M33 = 0.91822F,
            M44 = 1F // Important for inverse transforms.
        });

    /// <summary>
    /// von Kries chromatic adaptation transform matrix (Hunt-Pointer-Estevez for equal energy)
    /// </summary>
    public static readonly Matrix4x4 VonKriesHPE
        = Matrix4x4.Transpose(new Matrix4x4
        {
            M11 = 0.3897F,
            M12 = 0.6890F,
            M13 = -0.0787F,
            M21 = -0.2298F,
            M22 = 1.1834F,
            M23 = 0.0464F,
            M31 = 0,
            M32 = 0,
            M33 = 1F,
            M44 = 1F
        });

    /// <summary>
    /// XYZ scaling chromatic adaptation transform matrix
    /// </summary>
    public static readonly Matrix4x4 XyzScaling = Matrix4x4.Transpose(Matrix4x4.Identity);

    /// <summary>
    /// Bradford chromatic adaptation transform matrix (used in CMCCAT97)
    /// </summary>
    public static readonly Matrix4x4 Bradford
        = Matrix4x4.Transpose(new Matrix4x4
        {
            M11 = 0.8951F,
            M12 = 0.2664F,
            M13 = -0.1614F,
            M21 = -0.7502F,
            M22 = 1.7135F,
            M23 = 0.0367F,
            M31 = 0.0389F,
            M32 = -0.0685F,
            M33 = 1.0296F,
            M44 = 1F
        });

    /// <summary>
    /// Spectral sharpening and the Bradford transform
    /// </summary>
    public static readonly Matrix4x4 BradfordSharp
        = Matrix4x4.Transpose(new Matrix4x4
        {
            M11 = 1.2694F,
            M12 = -0.0988F,
            M13 = -0.1706F,
            M21 = -0.8364F,
            M22 = 1.8006F,
            M23 = 0.0357F,
            M31 = 0.0297F,
            M32 = -0.0315F,
            M33 = 1.0018F,
            M44 = 1F
        });

    /// <summary>
    /// CMCCAT2000 (fitted from all available color data sets)
    /// </summary>
    public static readonly Matrix4x4 CMCCAT2000
        = Matrix4x4.Transpose(new Matrix4x4
        {
            M11 = 0.7982F,
            M12 = 0.3389F,
            M13 = -0.1371F,
            M21 = -0.5918F,
            M22 = 1.5512F,
            M23 = 0.0406F,
            M31 = 0.0008F,
            M32 = 0.239F,
            M33 = 0.9753F,
            M44 = 1F
        });

    /// <summary>
    /// CAT02 (optimized for minimizing CIELAB differences)
    /// </summary>
    public static readonly Matrix4x4 CAT02
        = Matrix4x4.Transpose(new Matrix4x4
        {
            M11 = 0.7328F,
            M12 = 0.4296F,
            M13 = -0.1624F,
            M21 = -0.7036F,
            M22 = 1.6975F,
            M23 = 0.0061F,
            M31 = 0.0030F,
            M32 = 0.0136F,
            M33 = 0.9834F,
            M44 = 1F
        });
}
