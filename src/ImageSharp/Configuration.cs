// <copyright file="Configuration.cs" company="James Jackson-South">
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
    using ImageSharp.IO;

    /// <summary>
    /// Provides initialization code which allows extending the library.
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// A lazily initialized configuration default instance.
        /// </summary>
        private static readonly Lazy<Configuration> Lazy = new Lazy<Configuration>(() => CreateDefaultInstance());

        /// <summary>
        /// An object that can be used to synchronize access to the <see cref="Configuration"/>.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The list of supported <see cref="IImageEncoder"/>.
        /// </summary>
        private readonly List<IImageEncoder> encoders = new List<IImageEncoder>();

        /// <summary>
        /// The list of supported <see cref="IImageDecoder"/>.
        /// </summary>
        private readonly List<IImageDecoder> decoders = new List<IImageDecoder>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        public Configuration()
        {
        }

        /// <summary>
        /// Gets the default <see cref="Configuration"/> instance.
        /// </summary>
        public static Configuration Default { get; } = Lazy.Value;

        /// <summary>
        /// Gets the collection of supported <see cref="IImageEncoder"/>
        /// </summary>
        public IReadOnlyCollection<IImageEncoder> ImageEncoders => new ReadOnlyCollection<IImageEncoder>(this.encoders);

        /// <summary>
        /// Gets the collection of supported <see cref="IImageDecoder"/>
        /// </summary>
        public IReadOnlyCollection<IImageDecoder> ImageDecoders => new ReadOnlyCollection<IImageDecoder>(this.decoders);

        /// <summary>
        /// Gets the global parallel options for processing tasks in parallel.
        /// </summary>
        public ParallelOptions ParallelOptions { get; } = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        /// <summary>
        /// Gets the maximum header size of all formats.
        /// </summary>
        internal int MaxHeaderSize { get; private set; }

#if !NETSTANDARD1_1
        /// <summary>
        /// Gets or sets the fielsystem helper for accessing the local file system.
        /// </summary>
        internal IFileSystem FileSystem { get; set; } = new LocalFileSystem();
#endif

        /// <summary>
        /// Adds a new <see cref="IImageEncoder"/> to the collection of supported image formats.
        /// </summary>
        /// <param name="decoder">The new format to add.</param>
        public void AddImageFormat(IImageDecoder decoder)
        {
            Guard.NotNull(decoder, nameof(decoder));
            Guard.NotNullOrEmpty(decoder.FileExtensions, nameof(decoder.FileExtensions));
            Guard.NotNullOrEmpty(decoder.MimeTypes, nameof(decoder.MimeTypes));

            lock (this.syncRoot)
            {
                this.decoders.Add(decoder);

                this.SetMaxHeaderSize();
            }
        }

        /// <summary>
        /// Adds a new <see cref="IImageDecoder"/> to the collection of supported image formats.
        /// </summary>
        /// <param name="encoder">The new format to add.</param>
        public void AddImageFormat(IImageEncoder encoder)
        {
            Guard.NotNull(encoder, nameof(encoder));
            Guard.NotNullOrEmpty(encoder.FileExtensions, nameof(encoder.FileExtensions));
            Guard.NotNullOrEmpty(encoder.MimeTypes, nameof(encoder.MimeTypes));
            lock (this.syncRoot)
            {
                this.encoders.Add(encoder);
            }
        }

        /// <summary>
        /// Creates the default instance, with Png, Jpeg, Gif and Bmp preregisterd (if they have been referenced)
        /// </summary>
        /// <returns>The default configuration of <see cref="Configuration"/> </returns>
        internal static Configuration CreateDefaultInstance()
        {
            Configuration config = new Configuration();

            // lets try auto loading the known image formats
            config.AddImageFormat(new Formats.PngEncoder());
            config.AddImageFormat(new Formats.JpegEncoder());
            config.AddImageFormat(new Formats.GifEncoder());
            config.AddImageFormat(new Formats.BmpEncoder());

            config.AddImageFormat(new Formats.PngDecoder());
            config.AddImageFormat(new Formats.JpegDecoder());
            config.AddImageFormat(new Formats.GifDecoder());
            config.AddImageFormat(new Formats.BmpDecoder());
            return config;
        }

        /// <summary>
        /// Sets max header size.
        /// </summary>
        private void SetMaxHeaderSize()
        {
            this.MaxHeaderSize = this.decoders.Max(x => x.HeaderSize);
        }
    }
}
