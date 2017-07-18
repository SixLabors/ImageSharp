// <copyright file="Configuration.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Formats;
    using ImageSharp.IO;

    /// <summary>
    /// Provides initialization code which allows extending the library.
    /// </summary>
    public sealed class Configuration
    {
        /// <summary>
        /// A lazily initialized configuration default instance.
        /// </summary>
        private static readonly Lazy<Configuration> Lazy = new Lazy<Configuration>(CreateDefaultInstance);

        /// <summary>
        /// The list of supported <see cref="IImageEncoder"/> keyed to mime types.
        /// </summary>
        private readonly ConcurrentDictionary<IImageFormat, IImageEncoder> mimeTypeEncoders = new ConcurrentDictionary<IImageFormat, IImageEncoder>();

        /// <summary>
        /// The list of supported <see cref="IImageEncoder"/> keyed to mime types.
        /// </summary>
        private readonly ConcurrentDictionary<IImageFormat, IImageDecoder> mimeTypeDecoders = new ConcurrentDictionary<IImageFormat, IImageDecoder>();

        /// <summary>
        /// The list of supported <see cref="IImageFormat"/>s.
        /// </summary>
        private readonly ConcurrentBag<IImageFormat> imageFormats = new ConcurrentBag<IImageFormat>();

        /// <summary>
        /// The list of supported <see cref="IImageFormatDetector"/>s.
        /// </summary>
        private ConcurrentBag<IImageFormatDetector> imageFormatDetectors = new ConcurrentBag<IImageFormatDetector>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        public Configuration()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        /// <param name="configurationModules">A collection of configuration modules to register</param>
        public Configuration(params IConfigurationModule[] configurationModules)
        {
            if (configurationModules != null)
            {
                foreach (IConfigurationModule p in configurationModules)
                {
                    p.Configure(this);
                }
            }
        }

        /// <summary>
        /// Gets the default <see cref="Configuration"/> instance.
        /// </summary>
        public static Configuration Default { get; } = Lazy.Value;

        /// <summary>
        /// Gets the global parallel options for processing tasks in parallel.
        /// </summary>
        public ParallelOptions ParallelOptions { get; } = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        /// <summary>
        /// Gets the maximum header size of all the formats.
        /// </summary>
        internal int MaxHeaderSize { get; private set; }

        /// <summary>
        /// Gets the currently registered <see cref="IImageFormatDetector"/>s.
        /// </summary>
        internal IEnumerable<IImageFormatDetector> FormatDetectors => this.imageFormatDetectors;

        /// <summary>
        /// Gets the currently registered <see cref="IImageDecoder"/>s.
        /// </summary>
        internal IEnumerable<KeyValuePair<IImageFormat, IImageDecoder>> ImageDecoders => this.mimeTypeDecoders;

        /// <summary>
        /// Gets the currently registered <see cref="IImageEncoder"/>s.
        /// </summary>
        internal IEnumerable<KeyValuePair<IImageFormat, IImageEncoder>> ImageEncoders => this.mimeTypeEncoders;

        /// <summary>
        /// Gets the currently registered <see cref="IImageFormat"/>s.
        /// </summary>
        internal IEnumerable<IImageFormat> ImageFormats => this.imageFormats;

#if !NETSTANDARD1_1
        /// <summary>
        /// Gets or sets the fielsystem helper for accessing the local file system.
        /// </summary>
        internal IFileSystem FileSystem { get; set; } = new LocalFileSystem();
#endif

        /// <summary>
        /// Registers a new format provider.
        /// </summary>
        /// <param name="configuration">The configuration provider to call configure on.</param>
        public void Configure(IConfigurationModule configuration)
        {
            Guard.NotNull(configuration, nameof(configuration));
            configuration.Configure(this);
        }

        /// <summary>
        /// Registers a new format provider.
        /// </summary>
        /// <param name="format">The format to register as a well know format.</param>
        public void AddImageFormat(IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));
            Guard.NotNull(format.MimeTypes, nameof(format.MimeTypes));
            Guard.NotNull(format.FileExtensions, nameof(format.FileExtensions));
            this.imageFormats.Add(format);
        }

        /// <summary>
        /// For the specified file extensions type find the e <see cref="IImageFormat"/>.
        /// </summary>
        /// <param name="extension">The extension to discover</param>
        /// <returns>The <see cref="IImageFormat"/> if found otherwise null</returns>
        public IImageFormat FindFormatByFileExtensions(string extension)
        {
            return this.imageFormats.FirstOrDefault(x => x.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// For the specified mime type find the <see cref="IImageFormat"/>.
        /// </summary>
        /// <param name="mimeType">The mime-type to discover</param>
        /// <returns>The <see cref="IImageFormat"/> if found otherwise null</returns>
        public IImageFormat FindFormatByMimeType(string mimeType)
        {
            return this.imageFormats.FirstOrDefault(x => x.MimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Sets a specific image encoder as the encoder for a specific image format.
        /// </summary>
        /// <param name="imageFormat">The image format to register the encoder for.</param>
        /// <param name="encoder">The encoder to use,</param>
        public void SetEncoder(IImageFormat imageFormat, IImageEncoder encoder)
        {
            Guard.NotNull(imageFormat, nameof(imageFormat));
            Guard.NotNull(encoder, nameof(encoder));
            this.AddImageFormat(imageFormat);
            this.mimeTypeEncoders.AddOrUpdate(imageFormat, encoder, (s, e) => encoder);
        }

        /// <summary>
        /// Sets a specific image decoder as the decoder for a specific image format.
        /// </summary>
        /// <param name="imageFormat">The image format to register the encoder for.</param>
        /// <param name="decoder">The decoder to use,</param>
        public void SetDecoder(IImageFormat imageFormat, IImageDecoder decoder)
        {
            Guard.NotNull(imageFormat, nameof(imageFormat));
            Guard.NotNull(decoder, nameof(decoder));
            this.AddImageFormat(imageFormat);
            this.mimeTypeDecoders.AddOrUpdate(imageFormat, decoder, (s, e) => decoder);
        }

        /// <summary>
        /// Removes all the registered image format detectors.
        /// </summary>
        public void ClearImageFormatDetectors()
        {
            this.imageFormatDetectors = new ConcurrentBag<IImageFormatDetector>();
        }

        /// <summary>
        /// Adds a new detector for detecting mime types.
        /// </summary>
        /// <param name="detector">The detector to add</param>
        public void AddImageFormatDetector(IImageFormatDetector detector)
        {
            Guard.NotNull(detector, nameof(detector));
            this.imageFormatDetectors.Add(detector);
            this.SetMaxHeaderSize();
        }

        /// <summary>
        /// Creates the default instance with the following <see cref="IConfigurationModule"/>s preregistered:
        /// <para><see cref="PngConfigurationModule"/></para>
        /// <para><see cref="JpegConfigurationModule"/></para>
        /// <para><see cref="GifConfigurationModule"/></para>
        /// <para><see cref="BmpConfigurationModule"/></para>
        /// </summary>
        /// <returns>The default configuration of <see cref="Configuration"/></returns>
        internal static Configuration CreateDefaultInstance()
        {
            return new Configuration(
                new PngConfigurationModule(),
                new JpegConfigurationModule(),
                new GifConfigurationModule(),
                new BmpConfigurationModule());
        }

        /// <summary>
        /// For the specified mime type find the decoder.
        /// </summary>
        /// <param name="format">The format to discover</param>
        /// <returns>The <see cref="IImageDecoder"/> if found otherwise null</returns>
        internal IImageDecoder FindDecoder(IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));
            if (this.mimeTypeDecoders.TryGetValue(format, out IImageDecoder decoder))
            {
                return decoder;
            }

            return null;
        }

        /// <summary>
        /// For the specified mime type find the encoder.
        /// </summary>
        /// <param name="format">The format to discover</param>
        /// <returns>The <see cref="IImageEncoder"/> if found otherwise null</returns>
        internal IImageEncoder FindEncoder(IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));
            if (this.mimeTypeEncoders.TryGetValue(format, out IImageEncoder encoder))
            {
                return encoder;
            }

            return null;
        }

        /// <summary>
        /// Sets the max header size.
        /// </summary>
        private void SetMaxHeaderSize()
        {
            this.MaxHeaderSize = this.imageFormatDetectors.Max(x => x.HeaderSize);
        }
    }
}
