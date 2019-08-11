// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream with the tiff format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>
        /// The <see cref="Image{TPixel}"/>.
        /// </returns>
        public static Image<TPixel> SaveAsTiff<TPixel>(this Image<TPixel> source, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            return SaveAsTiff(source, stream, null);
        }

        /// <summary>
        /// Saves the image to the given stream with the tiff format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The options for the encoder.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>
        /// The <see cref="Image{TPixel}"/>.
        /// </returns>
        public static Image<TPixel> SaveAsTiff<TPixel>(this Image<TPixel> source, Stream stream, TiffEncoder encoder)
            where TPixel : struct, IPixel<TPixel>
        {
            encoder = encoder ?? new TiffEncoder();
            encoder.Encode(source, stream);

            return source;
        }
    }
}
