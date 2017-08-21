// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// The static collection of all the default image formats
    /// </summary>
    public static class ImageFormats
    {
        /// <summary>
        /// The format details for the jpegs.
        /// </summary>
        public static readonly IImageFormat Jpeg = new JpegFormat();

        /// <summary>
        /// The format details for the pngs.
        /// </summary>
        public static readonly IImageFormat Png = new PngFormat();

        /// <summary>
        /// The format details for the gifs.
        /// </summary>
        public static readonly IImageFormat Gif = new GifFormat();

        /// <summary>
        /// The format details for the bitmaps.
        /// </summary>
        public static readonly IImageFormat Bmp = new BmpFormat();
    }
}