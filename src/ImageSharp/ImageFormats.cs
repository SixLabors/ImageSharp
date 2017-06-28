// <copyright file="IImageFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.Formats;

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
        public static readonly IImageFormat Bitmap = new BmpFormat();
    }
}