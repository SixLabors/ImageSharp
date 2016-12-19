// <copyright file="Bootstrapper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;

    using Formats;

    /// <summary>
    /// Provides initialization code which allows extending the library.
    /// </summary>
    public static class Bootstrapper
    {
        /// <summary>
        /// The list of supported <see cref="IImageFormat"/>.
        /// </summary>
        private static readonly List<IImageFormat> ImageFormatsList;

        /// <summary>
        /// An object that can be used to synchronize access to the <see cref="Bootstrapper"/>.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Initializes static members of the <see cref="Bootstrapper"/> class.
        /// </summary>
        static Bootstrapper()
        {
            ImageFormatsList = new List<IImageFormat>
            {
                new BmpFormat(),
                new JpegFormat(),
                new PngFormat(),
                new GifFormat()
            };
            SetMaxHeaderSize();
            ParallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        }

        /// <summary>
        /// Gets the collection of supported <see cref="IImageFormat"/>
        /// </summary>
        public static IReadOnlyCollection<IImageFormat> ImageFormats => new ReadOnlyCollection<IImageFormat>(ImageFormatsList);

        /// <summary>
        /// Gets the global parallel options for processing tasks in parallel.
        /// </summary>
        public static ParallelOptions ParallelOptions { get; }

        /// <summary>
        /// Gets the maximum header size of all formats.
        /// </summary>
        internal static int MaxHeaderSize { get; private set; }

        /// <summary>
        /// Adds a new <see cref="IImageFormat"/> to the collection of supported image formats.
        /// </summary>
        /// <param name="format">The new format to add.</param>
        public static void AddImageFormat(IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));
            Guard.NotNull(format.Encoder, nameof(format), "The encoder should not be null.");
            Guard.NotNull(format.Decoder, nameof(format), "The decoder should not be null.");
            Guard.NotNullOrEmpty(format.MimeType, nameof(format), "The mime type should not be null or empty.");
            Guard.NotNullOrEmpty(format.Extension, nameof(format), "The extension should not be null or empty.");
            Guard.NotNullOrEmpty(format.SupportedExtensions, nameof(format), "The supported extensions not be null or empty.");

            AddImageFormatLocked(format);
        }

        private static void AddImageFormatLocked(IImageFormat format)
        {
            lock (SyncRoot)
            {
                GuardDuplicate(format);

                ImageFormatsList.Add(format);

                SetMaxHeaderSize();
            }
        }

        private static void GuardDuplicate(IImageFormat format)
        {
            if (!format.SupportedExtensions.Contains(format.Extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The supported extensions should contain the default extension.", nameof(format));
            }

            if (format.SupportedExtensions.Any(e => string.IsNullOrWhiteSpace(e)))
            {
                throw new ArgumentException("The supported extensions should not contain empty values.", nameof(format));
            }

            foreach (var imageFormat in ImageFormatsList)
            {
                if (imageFormat.Extension.Equals(format.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("There is already a format with the same extension.", nameof(format));
                }

                if (imageFormat.SupportedExtensions.Intersect(format.SupportedExtensions, StringComparer.OrdinalIgnoreCase).Any())
                {
                    throw new ArgumentException("There is already a format that supports the same extension.", nameof(format));
                }
            }
        }

        private static void SetMaxHeaderSize()
        {
            MaxHeaderSize = ImageFormatsList.Max(x => x.HeaderSize);
        }
    }
}
