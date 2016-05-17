// <copyright file="Bootstrapper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using ImageProcessorCore.Formats;

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
            this.imageFormats = new List<IImageFormat>(new List<IImageFormat>
            {
                new BmpFormat(),
                new JpegFormat(),
                new PngFormat(),
                new GifFormat()
            });
        }

        /// <summary>
        /// Gets the current bootstrapper instance.
        /// </summary>
        public static Bootstrapper Instance = Lazy.Value;

        /// <summary>
        /// Gets the list of supported <see cref="IImageFormat"/>
        /// </summary>
        public IReadOnlyCollection<IImageFormat> ImageFormats => new ReadOnlyCollection<IImageFormat>(this.imageFormats);

        /// <summary>
        /// Adds a new <see cref="IImageFormat"/> to the collection of supported image formats.
        /// </summary>
        /// <param name="format">The new format to add.</param>
        public void AddImageFormat(IImageFormat format)
        {
            this.imageFormats.Add(format);
        }
    }
}
