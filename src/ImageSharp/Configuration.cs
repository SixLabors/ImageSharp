// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp
{
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
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        public Configuration()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        /// <param name="configuration">A configuration instance to be copied</param>
        public Configuration(Configuration configuration)
        {
            this.ParallelOptions = configuration.ParallelOptions;
            this.ImageFormatsManager = configuration.ImageFormatsManager;
            this.MemoryManager = configuration.MemoryManager;
            this.ImageOperationsProvider = configuration.ImageOperationsProvider;

            #if !NETSTANDARD1_1
            this.FileSystem = configuration.FileSystem;
            #endif

            this.ReadOrigin = configuration.ReadOrigin;
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
        /// Gets the currently registered <see cref="IImageFormat"/>s.
        /// </summary>
        public IEnumerable<IImageFormat> ImageFormats => this.ImageFormatsManager.ImageFormats;

        /// <summary>
        /// Gets or sets the position in a stream to use for reading when using a seekable stream as an image data source.
        /// </summary>
        public ReadOrigin ReadOrigin { get; set; } = ReadOrigin.Current;

        /// <summary>
        /// Gets or sets the <see cref="ImageFormatManager"/> that is currently in use.
        /// </summary>
        public ImageFormatManager ImageFormatsManager { get; set; } = new ImageFormatManager();

        /// <summary>
        /// Gets or sets the <see cref="MemoryManager"/> that is currently in use.
        /// </summary>
        public MemoryManager MemoryManager { get; set; } = ArrayPoolMemoryManager.CreateDefault();

        /// <summary>
        /// Gets the maximum header size of all the formats.
        /// </summary>
        internal int MaxHeaderSize => this.ImageFormatsManager.MaxHeaderSize;

        /// <summary>
        /// Gets the currently registered <see cref="IImageFormatDetector"/>s.
        /// </summary>
        internal IEnumerable<IImageFormatDetector> FormatDetectors => this.ImageFormatsManager.FormatDetectors;

        /// <summary>
        /// Gets the currently registered <see cref="IImageDecoder"/>s.
        /// </summary>
        internal IEnumerable<KeyValuePair<IImageFormat, IImageDecoder>> ImageDecoders => this.ImageFormatsManager.ImageDecoders;

        /// <summary>
        /// Gets the currently registered <see cref="IImageEncoder"/>s.
        /// </summary>
        internal IEnumerable<KeyValuePair<IImageFormat, IImageEncoder>> ImageEncoders => this.ImageFormatsManager.ImageEncoders;

#if !NETSTANDARD1_1
        /// <summary>
        /// Gets or sets the filesystem helper for accessing the local file system.
        /// </summary>
        internal IFileSystem FileSystem { get; set; } = new LocalFileSystem();
#endif

        /// <summary>
        /// Gets or sets the image operations provider factory.
        /// </summary>
        internal IImageProcessingContextFactory ImageOperationsProvider { get; set; } = new DefaultImageOperationsProviderFactory();

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
        /// <param name="format">The format to register as a known format.</param>
        public void AddImageFormat(IImageFormat format)
        {
            this.ImageFormatsManager.AddImageFormat(format);
        }

        /// <summary>
        /// For the specified file extensions type find the e <see cref="IImageFormat"/>.
        /// </summary>
        /// <param name="extension">The extension to discover</param>
        /// <returns>The <see cref="IImageFormat"/> if found otherwise null</returns>
        public IImageFormat FindFormatByFileExtension(string extension)
        {
            return this.ImageFormatsManager.FindFormatByFileExtension(extension);
        }

        /// <summary>
        /// For the specified mime type find the <see cref="IImageFormat"/>.
        /// </summary>
        /// <param name="mimeType">The mime-type to discover</param>
        /// <returns>The <see cref="IImageFormat"/> if found; otherwise null</returns>
        public IImageFormat FindFormatByMimeType(string mimeType)
        {
            return this.ImageFormatsManager.FindFormatByMimeType(mimeType);
        }

        /// <summary>
        /// Sets a specific image encoder as the encoder for a specific image format.
        /// </summary>
        /// <param name="imageFormat">The image format to register the encoder for.</param>
        /// <param name="encoder">The encoder to use,</param>
        public void SetEncoder(IImageFormat imageFormat, IImageEncoder encoder)
        {
            this.ImageFormatsManager.SetEncoder(imageFormat, encoder);
        }

        /// <summary>
        /// Sets a specific image decoder as the decoder for a specific image format.
        /// </summary>
        /// <param name="imageFormat">The image format to register the encoder for.</param>
        /// <param name="decoder">The decoder to use,</param>
        public void SetDecoder(IImageFormat imageFormat, IImageDecoder decoder)
        {
            this.ImageFormatsManager.SetDecoder(imageFormat, decoder);
        }

        /// <summary>
        /// Removes all the registered image format detectors.
        /// </summary>
        public void ClearImageFormatDetectors()
        {
            this.ImageFormatsManager.ClearImageFormatDetectors();
        }

        /// <summary>
        /// Adds a new detector for detecting mime types.
        /// </summary>
        /// <param name="detector">The detector to add</param>
        public void AddImageFormatDetector(IImageFormatDetector detector)
        {
            this.ImageFormatsManager.AddImageFormatDetector(detector);
        }

        /// <summary>
        /// For the specified mime type find the decoder.
        /// </summary>
        /// <param name="format">The format to discover</param>
        /// <returns>The <see cref="IImageDecoder"/> if found otherwise null</returns>
        public IImageDecoder FindDecoder(IImageFormat format)
        {
            return this.ImageFormatsManager.FindDecoder(format);
        }

        /// <summary>
        /// For the specified mime type find the encoder.
        /// </summary>
        /// <param name="format">The format to discover</param>
        /// <returns>The <see cref="IImageEncoder"/> if found otherwise null</returns>
        public IImageEncoder FindEncoder(IImageFormat format)
        {
            return this.ImageFormatsManager.FindEncoder(format);
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
    }
}