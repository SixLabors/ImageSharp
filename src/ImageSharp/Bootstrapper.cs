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
    public class Bootstrapper
    {
        /// <summary>
        /// A new instance Initializes a new instance of the <see cref="Bootstrapper"/> class.
        /// with lazy initialization.
        /// </summary>
        private static readonly Lazy<Bootstrapper> Lazy = new Lazy<Bootstrapper>(() => new Bootstrapper());

        /// <summary>
        /// The default list of supported <see cref="IImageFormat"/>
        /// </summary>
        private readonly List<IImageFormat> imageFormats;

        /// <summary>
        /// Prevents a default instance of the <see cref="Bootstrapper"/> class from being created.
        /// </summary>
        private Bootstrapper()
        {
            this.imageFormats = new List<IImageFormat>
            {
                new BmpFormat(),
                new JpegFormat(),
                new PngFormat(),
                new GifFormat()
            };
        }

        /// <summary>
        /// Gets the current bootstrapper instance.
        /// </summary>
        public static Bootstrapper Instance => Lazy.Value;

        /// <summary>
        /// Gets the collection of supported <see cref="IImageFormat"/>
        /// </summary>
        public IReadOnlyCollection<IImageFormat> ImageFormats => new ReadOnlyCollection<IImageFormat>(this.imageFormats);

        /// <summary>
        /// Gets or sets the global parallel options for processing tasks in parallel.
        /// </summary>
        public ParallelOptions ParallelOptions { get; set; } = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        /// <summary>
        /// Adds a new <see cref="IImageFormat"/> to the collection of supported image formats.
        /// </summary>
        /// <param name="format">The new format to add.</param>
        public void AddImageFormat(IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));
            Guard.NotNull(format.Encoder, nameof(format), "The encoder should not be null.");
            Guard.NotNull(format.Decoder, nameof(format), "The decoder should not be null.");
            Guard.NotNullOrEmpty(format.MimeType, nameof(format), "The mime type should not be null or empty.");
            Guard.NotNullOrEmpty(format.Extension, nameof(format), "The extension should not be null or empty.");
            Guard.NotNullOrEmpty(format.SupportedExtensions, nameof(format), "The supported extensions not be null or empty.");

            this.GuardDuplicate(format);

            this.imageFormats.Add(format);
        }

        private void GuardDuplicate(IImageFormat format)
        {
            if (!format.SupportedExtensions.Contains(format.Extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The supported extensions should contain the default extension.", nameof(format));
            }

            if (format.SupportedExtensions.Any(e => string.IsNullOrWhiteSpace(e)))
            {
                throw new ArgumentException("The supported extensions should not contain empty values.", nameof(format));
            }

            foreach (var imageFormat in this.imageFormats)
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
    }
}
