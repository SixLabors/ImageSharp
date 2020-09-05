// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Extension methods over Image{TPixel}
    /// </summary>
    public static class AdvancedImageExtensions
    {
        /// <summary>
        /// For a given file path find the best encoder to use via its extension.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="filePath">The target file path to save the image to.</param>
        /// <exception cref="ArgumentNullException">The file path is null.</exception>
        /// <exception cref="NotSupportedException">No encoder available for provided path.</exception>
        /// <returns>The matching <see cref="IImageEncoder"/>.</returns>
        public static IImageEncoder DetectEncoder(this Image source, string filePath)
        {
            Guard.NotNull(filePath, nameof(filePath));

            string ext = Path.GetExtension(filePath);
            IImageFormat format = source.GetConfiguration().ImageFormatsManager.FindFormatByFileExtension(ext);
            if (format is null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"No encoder was found for extension '{ext}'. Registered encoders include:");
                foreach (IImageFormat fmt in source.GetConfiguration().ImageFormats)
                {
                    sb.AppendFormat(" - {0} : {1}{2}", fmt.Name, string.Join(", ", fmt.FileExtensions), Environment.NewLine);
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
                    sb.AppendFormat(" - {0} : {1}{2}", enc.Key, enc.Value.GetType().Name, Environment.NewLine);
                }

                throw new NotSupportedException(sb.ToString());
            }

            return encoder;
        }

        /// <summary>
        /// Accepts a <see cref="IImageVisitor"/> to implement a double-dispatch pattern in order to
        /// apply pixel-specific operations on non-generic <see cref="Image"/> instances
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="visitor">The image visitor.</param>
        public static void AcceptVisitor(this Image source, IImageVisitor visitor)
            => source.Accept(visitor);

        /// <summary>
        /// Accepts a <see cref="IImageVisitor"/> to implement a double-dispatch pattern in order to
        /// apply pixel-specific operations on non-generic <see cref="Image"/> instances
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="visitor">The image visitor.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A  <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task AcceptVisitorAsync(this Image source, IImageVisitorAsync visitor, CancellationToken cancellationToken = default)
            => source.AcceptAsync(visitor, cancellationToken);

        /// <summary>
        /// Gets the configuration for the image.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <returns>Returns the configuration.</returns>
        public static Configuration GetConfiguration(this Image source)
            => GetConfiguration((IConfigurationProvider)source);

        /// <summary>
        /// Gets the configuration for the image frame.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <returns>Returns the configuration.</returns>
        public static Configuration GetConfiguration(this ImageFrame source)
            => GetConfiguration((IConfigurationProvider)source);

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        private static Configuration GetConfiguration(IConfigurationProvider source)
            => source?.Configuration ?? Configuration.Default;

        /// <summary>
        /// Gets the representation of the pixels as a <see cref="IMemoryGroup{T}"/> containing the backing pixel data of the image
        /// stored in row major order, as a list of contiguous <see cref="Memory{T}"/> blocks in the source image's pixel format.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <returns>The <see cref="IMemoryGroup{T}"/>.</returns>
        /// <remarks>
        /// Certain Image Processors may invalidate the returned <see cref="IMemoryGroup{T}"/> and all it's buffers,
        /// therefore it's not recommended to mutate the image while holding a reference to it's <see cref="IMemoryGroup{T}"/>.
        /// </remarks>
        public static IMemoryGroup<TPixel> GetPixelMemoryGroup<TPixel>(this ImageFrame<TPixel> source)
            where TPixel : unmanaged, IPixel<TPixel>
            => source?.PixelBuffer.FastMemoryGroup.View ?? throw new ArgumentNullException(nameof(source));

        /// <summary>
        /// Gets the representation of the pixels as a <see cref="IMemoryGroup{T}"/> containing the backing pixel data of the image
        /// stored in row major order, as a list of contiguous <see cref="Memory{T}"/> blocks in the source image's pixel format.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <returns>The <see cref="IMemoryGroup{T}"/>.</returns>
        /// <remarks>
        /// Certain Image Processors may invalidate the returned <see cref="IMemoryGroup{T}"/> and all it's buffers,
        /// therefore it's not recommended to mutate the image while holding a reference to it's <see cref="IMemoryGroup{T}"/>.
        /// </remarks>
        public static IMemoryGroup<TPixel> GetPixelMemoryGroup<TPixel>(this Image<TPixel> source)
            where TPixel : unmanaged, IPixel<TPixel>
            => source?.Frames.RootFrame.GetPixelMemoryGroup() ?? throw new ArgumentNullException(nameof(source));

        /// <summary>
        /// Gets the representation of the pixels as a <see cref="Span{T}"/> of contiguous memory
        /// at row <paramref name="rowIndex"/> beginning from the the first pixel on that row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="rowIndex">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        public static Memory<TPixel> GetPixelRowMemory<TPixel>(this ImageFrame<TPixel> source, int rowIndex)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(source, nameof(source));
            Guard.MustBeGreaterThanOrEqualTo(rowIndex, 0, nameof(rowIndex));
            Guard.MustBeLessThan(rowIndex, source.Height, nameof(rowIndex));

            return source.PixelBuffer.GetSafeRowMemory(rowIndex);
        }

        /// <summary>
        /// Gets the representation of the pixels as <see cref="Span{T}"/> of of contiguous memory
        /// at row <paramref name="rowIndex"/> beginning from the the first pixel on that row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="rowIndex">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        public static Memory<TPixel> GetPixelRowMemory<TPixel>(this Image<TPixel> source, int rowIndex)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(source, nameof(source));
            Guard.MustBeGreaterThanOrEqualTo(rowIndex, 0, nameof(rowIndex));
            Guard.MustBeLessThan(rowIndex, source.Height, nameof(rowIndex));

            return source.Frames.RootFrame.PixelBuffer.GetSafeRowMemory(rowIndex);
        }

        /// <summary>
        /// Gets the <see cref="MemoryAllocator"/> assigned to 'source'.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <returns>Returns the configuration.</returns>
        internal static MemoryAllocator GetMemoryAllocator(this IConfigurationProvider source)
            => GetConfiguration(source).MemoryAllocator;
    }
}
