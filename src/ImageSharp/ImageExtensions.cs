// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods over Image{TPixel}.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Writes the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void Save(this Image source, string filePath)
        {
            Guard.NotNullOrWhiteSpace(filePath, nameof(filePath));

            string ext = Path.GetExtension(filePath);
            IImageFormat format = source.GetConfiguration().ImageFormatsManager.FindFormatByFileExtension(ext);
            if (format is null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"No encoder was found for extension '{ext}'. Registered encoders include:");
                foreach (IImageFormat fmt in source.GetConfiguration().ImageFormats)
                {
                    sb.AppendLine($" - {fmt.Name} : {string.Join(", ", fmt.FileExtensions)}");
                }

                throw new NotSupportedException(sb.ToString());
            }

            IImageEncoder encoder = source.GetConfiguration().ImageFormatsManager.FindEncoder(format);

            if (encoder is null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"No encoder was found for extension '{ext}' using image format '{format.Name}'. Registered encoders include:");
                foreach (KeyValuePair<IImageFormat, IImageEncoder> enc in source.GetConfiguration().ImageFormatsManager.ImageEncoders)
                {
                    sb.AppendLine($" - {enc.Key} : {enc.Value.GetType().Name}");
                }

                throw new NotSupportedException(sb.ToString());
            }

            source.Save(filePath, encoder);
        }

        /// <summary>
        /// Writes the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="ArgumentNullException">Thrown if the encoder is null.</exception>
        public static void Save(this Image source, string filePath, IImageEncoder encoder)
        {
            Guard.NotNull(encoder, nameof(encoder));
            using (Stream fs = source.GetConfiguration().FileSystem.Create(filePath))
            {
                source.Save(fs, encoder);
            }
        }

        /// <summary>
        /// Writes the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image in.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void Save(this Image source, Stream stream, IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));
            IImageEncoder encoder = source.GetConfiguration().ImageFormatsManager.FindEncoder(format);

            if (encoder is null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("No encoder was found for the provided mime type. Registered encoders include:");

                foreach (KeyValuePair<IImageFormat, IImageEncoder> val in source.GetConfiguration().ImageFormatsManager.ImageEncoders)
                {
                    sb.AppendLine($" - {val.Key.Name} : {val.Value.GetType().Name}");
                }

                throw new NotSupportedException(sb.ToString());
            }

            source.Save(stream, encoder);
        }

        /// <summary>
        /// Returns a Base64 encoded string from the given image.
        /// </summary>
        /// <example><see href="data:image/gif;base64,R0lGODlhAQABAIABAEdJRgAAACwAAAAAAQABAAACAkQBAA=="/></example>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <param name="format">The format.</param>
        /// <returns>The <see cref="string"/></returns>
        public static string ToBase64String<TPixel>(this Image<TPixel> source, IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var stream = new MemoryStream())
            {
                source.Save(stream, format);
                stream.Flush();
                return $"data:{format.DefaultMimeType};base64,{Convert.ToBase64String(stream.ToArray())}";
            }
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if the image is disposed.
        /// </summary>
        internal static void EnsureNotDisposed(this Image image)
        {
            if (image.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(image), "Trying to execute an operation on a disposed image.");
            }
        }
    }
}
