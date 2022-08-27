// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Contains a list of different webp chunk types.
    /// </summary>
    /// <remarks>See Webp Container Specification for more details: https://developers.google.com/speed/webp/docs/riff_container </remarks>
    internal enum WebpChunkType : uint
    {
        /// <summary>
        /// Header signaling the use of the VP8 format.
        /// </summary>
        Vp8 = 0x56503820U,

        /// <summary>
        /// Header signaling the image uses lossless encoding.
        /// </summary>
        Vp8L = 0x5650384CU,

        /// <summary>
        /// Header for a extended-VP8 chunk.
        /// </summary>
        Vp8X = 0x56503858U,

        /// <summary>
        /// Chunk contains information about the alpha channel.
        /// </summary>
        Alpha = 0x414C5048U,

        /// <summary>
        /// Chunk which contains a color profile.
        /// </summary>
        Iccp = 0x49434350U,

        /// <summary>
        /// Chunk which contains EXIF metadata about the image.
        /// </summary>
        Exif = 0x45584946U,

        /// <summary>
        /// Chunk contains XMP metadata about the image.
        /// </summary>
        Xmp = 0x584D5020U,

        /// <summary>
        /// For an animated image, this chunk contains the global parameters of the animation.
        /// </summary>
        AnimationParameter = 0x414E494D,

        /// <summary>
        /// For animated images, this chunk contains information about a single frame. If the Animation flag is not set, then this chunk SHOULD NOT be present.
        /// </summary>
        Animation = 0x414E4D46,
    }
}
