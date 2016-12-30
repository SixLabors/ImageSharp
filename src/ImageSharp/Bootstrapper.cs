﻿// <copyright file="Bootstrapper.cs" company="James Jackson-South">
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
        /// A lazily initialized bootstrapper default instance.
        /// </summary>
        private static readonly Lazy<Bootstrapper> Lazy = new Lazy<Bootstrapper>(() => new Bootstrapper());

        /// <summary>
        /// An object that can be used to synchronize access to the <see cref="Bootstrapper"/>.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// The list of supported <see cref="IImageFormat"/>.
        /// </summary>
        private readonly List<IImageFormat> imageFormatsList = new List<IImageFormat>();

        /// <summary>
        /// Gets the default <see cref="Bootstrapper"/> instance.
        /// </summary>
        public static Bootstrapper Default { get; } = Lazy.Value;

        /// <summary>
        /// Gets the collection of supported <see cref="IImageFormat"/>
        /// </summary>
        public IReadOnlyCollection<IImageFormat> ImageFormats => new ReadOnlyCollection<IImageFormat>(this.imageFormatsList);

        /// <summary>
        /// Gets the global parallel options for processing tasks in parallel.
        /// </summary>
        public ParallelOptions ParallelOptions { get; } = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        /// <summary>
        /// Gets the maximum header size of all formats.
        /// </summary>
        internal int MaxHeaderSize { get; private set; }

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

            this.AddImageFormatLocked(format);
        }

        /// <summary>
        /// Adds image format. The class is locked to make it thread safe.
        /// </summary>
        /// <param name="format">The image format.</param>
        private void AddImageFormatLocked(IImageFormat format)
        {
            lock (SyncRoot)
            {
                if (this.GuardDuplicate(format))
                {
                    this.imageFormatsList.Add(format);

                    this.SetMaxHeaderSize();
                }
            }
        }

        /// <summary>
        /// Checks to ensure duplicate image formats are not added.
        /// </summary>
        /// <param name="format">The image format.</param>
        /// <exception cref="ArgumentException">Thrown if a duplicate is added.</exception>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool GuardDuplicate(IImageFormat format)
        {
            if (!format.SupportedExtensions.Contains(format.Extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The supported extensions should contain the default extension.", nameof(format));
            }

            // ReSharper disable once ConvertClosureToMethodGroup
            // Prevents method group allocation
            if (format.SupportedExtensions.Any(e => string.IsNullOrWhiteSpace(e)))
            {
                throw new ArgumentException("The supported extensions should not contain empty values.", nameof(format));
            }

            // If there is already a format with the same extension or a format that supports that
            // extension return false.
            foreach (IImageFormat imageFormat in this.imageFormatsList)
            {
                if (imageFormat.Extension.Equals(format.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (imageFormat.SupportedExtensions.Intersect(format.SupportedExtensions, StringComparer.OrdinalIgnoreCase).Any())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sets max header size.
        /// </summary>
        private void SetMaxHeaderSize()
        {
            this.MaxHeaderSize = this.imageFormatsList.Max(x => x.HeaderSize);
        }
    }
}
