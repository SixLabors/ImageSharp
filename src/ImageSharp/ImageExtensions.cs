// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Writes the image to the given file path using an encoder detected from the path.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="NotSupportedException">No encoder available for provided path.</exception>
        public static void Save(this Image source, string path)
            => source.Save(path, source.DetectEncoder(path));

        /// <summary>
        /// Writes the image to the given file path using an encoder detected from the path.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="NotSupportedException">No encoder available for provided path.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsync(this Image source, string path, CancellationToken cancellationToken = default)
            => source.SaveAsync(path, source.DetectEncoder(path), cancellationToken);

        /// <summary>
        /// Writes the image to the given file path using the given image encoder.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The encoder is null.</exception>
        public static void Save(this Image source, string path, IImageEncoder encoder)
        {
            Guard.NotNull(path, nameof(path));
            Guard.NotNull(encoder, nameof(encoder));
            using (Stream fs = source.GetConfiguration().FileSystem.Create(path))
            {
                source.Save(fs, encoder);
            }
        }

        /// <summary>
        /// Writes the image to the given file path using the given image encoder.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="path">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The encoder is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task SaveAsync(
            this Image source,
            string path,
            IImageEncoder encoder,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(path, nameof(path));
            Guard.NotNull(encoder, nameof(encoder));

            using (Stream fs = source.GetConfiguration().FileSystem.Create(path))
            {
                await source.SaveAsync(fs, encoder, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Writes the image to the given stream using the given image format.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image in.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The format is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not writable.</exception>
        /// <exception cref="NotSupportedException">No encoder available for provided format.</exception>
        public static void Save(this Image source, Stream stream, IImageFormat format)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(format, nameof(format));

            if (!stream.CanWrite)
            {
                throw new NotSupportedException("Cannot write to the stream.");
            }

            IImageEncoder encoder = source.GetConfiguration().ImageFormatsManager.FindEncoder(format);

            if (encoder is null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("No encoder was found for the provided mime type. Registered encoders include:");

                foreach (KeyValuePair<IImageFormat, IImageEncoder> val in source.GetConfiguration().ImageFormatsManager.ImageEncoders)
                {
                    sb.AppendFormat(" - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
                }

                throw new NotSupportedException(sb.ToString());
            }

            source.Save(stream, encoder);
        }

        /// <summary>
        /// Writes the image to the given stream using the given image format.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image in.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The format is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not writable.</exception>
        /// <exception cref="NotSupportedException">No encoder available for provided format.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task SaveAsync(
            this Image source,
            Stream stream,
            IImageFormat format,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(format, nameof(format));

            if (!stream.CanWrite)
            {
                throw new NotSupportedException("Cannot write to the stream.");
            }

            IImageEncoder encoder = source.GetConfiguration().ImageFormatsManager.FindEncoder(format);

            if (encoder is null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("No encoder was found for the provided mime type. Registered encoders include:");

                foreach (KeyValuePair<IImageFormat, IImageEncoder> val in source.GetConfiguration().ImageFormatsManager.ImageEncoders)
                {
                    sb.AppendFormat(" - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
                }

                throw new NotSupportedException(sb.ToString());
            }

            return source.SaveAsync(stream, encoder, cancellationToken);
        }

        /// <summary>
        /// Returns a Base64 encoded string from the given image.
        /// The result is prepended with a Data URI <see href="https://en.wikipedia.org/wiki/Data_URI_scheme"/>
        /// <para>
        /// <example>
        /// For example:
        /// <see href="data:image/gif;base64,R0lGODlhAQABAIABAEdJRgAAACwAAAAAAQABAAACAkQBAA=="/>
        /// </example>
        /// </para>
        /// </summary>
        /// <param name="source">The source image</param>
        /// <param name="format">The format.</param>
        /// <exception cref="ArgumentNullException">The format is null.</exception>
        /// <returns>The <see cref="string"/></returns>
        public static string ToBase64String(this Image source, IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));

            using var stream = new MemoryStream();
            source.Save(stream, format);

            // Always available.
            stream.TryGetBuffer(out ArraySegment<byte> buffer);
            return $"data:{format.DefaultMimeType};base64,{Convert.ToBase64String(buffer.Array, 0, (int)stream.Length)}";
        }
    }
}
