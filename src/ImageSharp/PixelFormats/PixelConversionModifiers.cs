// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Specifies vector representation transformations applied by
/// <see cref="PixelOperations{TPixel}.ToVector4(Configuration,ReadOnlySpan{TPixel},Span{System.Numerics.Vector4},PixelConversionModifiers)"/>
/// and
/// <see cref="PixelOperations{TPixel}.FromVector4Destructive(Configuration,Span{System.Numerics.Vector4},Span{TPixel},PixelConversionModifiers)"/>
/// during bulk pixel conversion.
/// </summary>
[Flags]
public enum PixelConversionModifiers
{
    /// <summary>
    /// Preserves the pixel format's native numeric range and alpha representation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requests the scaled numeric range represented by <see cref="IPixel.ToScaledVector4"/> and <see cref="IPixel{T}.FromScaledVector4"/>.
    /// </summary>
    Scale = 1 << 0,

    /// <summary>
    /// Requests associated alpha for the vector representation.
    /// When combined with <see cref="UnPremultiply"/>, associated alpha takes precedence.
    /// </summary>
    Premultiply = 1 << 1,

    /// <summary>
    /// Expands sRGB color components when converting pixels to vectors and compresses linear RGB color components when converting vectors to pixels.
    /// </summary>
    SRgbCompand = 1 << 2,

    /// <summary>
    /// Requests unassociated alpha for the vector representation unless <see cref="Premultiply"/> is also specified.
    /// </summary>
    UnPremultiply = 1 << 3,
}
