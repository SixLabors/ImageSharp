// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Flags responsible to select additional operations which could be efficiently applied in
/// <see cref="PixelOperations{TPixel}.ToVector4(Configuration,ReadOnlySpan{TPixel},Span{System.Numerics.Vector4},PixelConversionModifiers)"/>
/// or
/// <see cref="PixelOperations{TPixel}.FromVector4Destructive(Configuration,Span{System.Numerics.Vector4},Span{TPixel},PixelConversionModifiers)"/>
/// knowing the pixel type.
/// </summary>
[Flags]
public enum PixelConversionModifiers
{
    /// <summary>
    /// No special operation is selected
    /// </summary>
    None = 0,

    /// <summary>
    /// Select <see cref="IPixel.ToScaledVector4"/> and <see cref="IPixel{T}.FromScaledVector4"/> instead the standard (non scaled) variants.
    /// </summary>
    Scale = 1 << 0,

    /// <summary>
    /// Enable alpha premultiplication / unpremultiplication
    /// </summary>
    Premultiply = 1 << 1,

    /// <summary>
    /// Enable SRGB companding (defined in <see cref="SRgbCompanding"/>).
    /// </summary>
    SRgbCompand = 1 << 2,
}
