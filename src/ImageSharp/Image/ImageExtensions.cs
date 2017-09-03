// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods over Image{TPixel}
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Rectangle Bounds<TPixel>(this ImageBase<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => new Rectangle(0, 0, source.Width, source.Height);

        /// <summary>
        /// Gets the size of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Size Size<TPixel>(this ImageBase<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => new Size(source.Width, source.Height);

#if !NETSTANDARD1_1
        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void Save<TPixel>(this Image<TPixel> source, string filePath)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNullOrEmpty(filePath, nameof(filePath));

            string ext = Path.GetExtension(filePath).Trim('.');
            IImageFormat format = source.Configuration.FindFormatByFileExtension(ext);
            if (format == null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"Can't find a format that is associated with the file extention '{ext}'. Registered formats with there extensions include:");
                foreach (IImageFormat fmt in source.Configuration.ImageFormats)
                {
                    stringBuilder.AppendLine($" - {fmt.Name} : {string.Join(", ", fmt.FileExtensions)}");
                }

                throw new NotSupportedException(stringBuilder.ToString());
            }

            IImageEncoder encoder = source.Configuration.FindEncoder(format);

            if (encoder == null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"Can't find encoder for file extention '{ext}' using image format '{format.Name}'. Registered encoders include:");
                foreach (KeyValuePair<IImageFormat, IImageEncoder> enc in source.Configuration.ImageEncoders)
                {
                    stringBuilder.AppendLine($" - {enc.Key} : {enc.Value.GetType().Name}");
                }

                throw new NotSupportedException(stringBuilder.ToString());
            }

            source.Save(filePath, encoder);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the encoder is null.</exception>
        public static void Save<TPixel>(this Image<TPixel> source, string filePath, IImageEncoder encoder)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(encoder, nameof(encoder));
            using (Stream fs = source.Configuration.FileSystem.Create(filePath))
            {
                source.Save(fs, encoder);
            }
        }
#endif

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void Save<TPixel>(this Image<TPixel> source, Stream stream, IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(format, nameof(format));
            IImageEncoder encoder = source.Configuration.FindEncoder(format);

            if (encoder == null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Can't find encoder for provided mime type. Available encoded:");

                foreach (KeyValuePair<IImageFormat, IImageEncoder> val in source.Configuration.ImageEncoders)
                {
                    stringBuilder.AppendLine($" - {val.Key.Name} : {val.Value.GetType().Name}");
                }

                throw new NotSupportedException(stringBuilder.ToString());
            }

            source.Save(stream, encoder);
        }

        /// <summary>
        /// Saves the raw image to the given bytes.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <param name="buffer">The buffer to save the raw pixel data to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SavePixelData<TPixel>(this Image<TPixel> source, Span<byte> buffer)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<byte> byteBuffer = source.GetPixelSpan().AsBytes();
            Guard.MustBeGreaterThanOrEqualTo(buffer.Length, byteBuffer.Length, nameof(buffer));

            byteBuffer.CopyTo(buffer);
        }

        /// <summary>
        /// Returns a Base64 encoded string from the given image.
        /// </summary>
        /// <example><see href="data:image/gif;base64,R0lGODlhAQABAIABAEdJRgAAACwAAAAAAQABAAACAkQBAA=="/></example>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
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
    }
}
