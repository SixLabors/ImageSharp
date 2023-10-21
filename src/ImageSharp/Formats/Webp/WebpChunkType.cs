// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Contains a list of different webp chunk types.
/// </summary>
/// <remarks>See Webp Container Specification for more details: https://developers.google.com/speed/webp/docs/riff_container </remarks>
internal enum WebpChunkType : uint
{
    /// <summary>
    /// Header signaling the use of the VP8 format.
    /// </summary>
    /// <remarks>VP8 (Single)</remarks>
    Vp8 = 0x56503820U,

    /// <summary>
    /// Header signaling the image uses lossless encoding.
    /// </summary>
    /// <remarks>VP8L (Single)</remarks>
    Vp8L = 0x5650384CU,

    /// <summary>
    /// Header for a extended-VP8 chunk.
    /// </summary>
    /// <remarks>VP8X (Single)</remarks>
    Vp8X = 0x56503858U,

    /// <summary>
    /// Chunk contains information about the alpha channel.
    /// </summary>
    /// <remarks>ALPH (Single)</remarks>
    Alpha = 0x414C5048U,

    /// <summary>
    /// Chunk which contains a color profile.
    /// </summary>
    /// <remarks>ICCP (Single)</remarks>
    Iccp = 0x49434350U,

    /// <summary>
    /// Chunk which contains EXIF metadata about the image.
    /// </summary>
    /// <remarks>EXIF (Single)</remarks>
    Exif = 0x45584946U,

    /// <summary>
    /// Chunk contains XMP metadata about the image.
    /// </summary>
    /// <remarks>XMP (Single)</remarks>
    Xmp = 0x584D5020U,

    /// <summary>
    /// For an animated image, this chunk contains the global parameters of the animation.
    /// </summary>
    /// <remarks>ANIM (Single)</remarks>
    AnimationParameter = 0x414E494D,

    /// <summary>
    /// For animated images, this chunk contains information about a single frame. If the Animation flag is not set, then this chunk SHOULD NOT be present.
    /// </summary>
    /// <remarks>ANMF (Multiple)</remarks>
    Animation = 0x414E4D46,
}
