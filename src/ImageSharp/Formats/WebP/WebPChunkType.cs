// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Contains a list of different webp chunk types.
    /// </summary>
    public enum WebPChunkType : uint
    {
        /// <summary>
        /// Header signaling the use of VP8 format.
        /// </summary>
        Vp8 = 0x56503820U,

        /// <summary>
        /// Header for a extended-VP8 chunk.
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

        /// <summary>
        /// TODO: not sure what this is for yet.
        /// </summary>
        FRGM = 0x4652474D,
    }
}
