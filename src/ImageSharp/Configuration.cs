// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
#if !NETSTANDARD1_1
using SixLabors.ImageSharp.IO;
#endif
using SixLabors.ImageSharp.Processing;
using SixLabors.Memory;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides configuration code which allows altering default behaviour or extending the library.
    /// </summary>
    public sealed class Configuration
    {
        /// <summary>
        /// A lazily initialized configuration default instance.
        /// </summary>
        private static readonly Lazy<Configuration> Lazy = new Lazy<Configuration>(CreateDefaultInstance);

        private int maxDegreeOfParallelism = Environment.ProcessorCount;

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
        /// Gets or sets the maximum number of concurrent tasks enabled in ImageSharp algorithms
        /// configured with this <see cref="Configuration"/> instance.
        /// Initialized with <see cref="Environment.ProcessorCount"/> by default.
        /// </summary>
        public int MaxDegreeOfParallelism
        {
            get => this.maxDegreeOfParallelism;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.MaxDegreeOfParallelism));
                }

                this.maxDegreeOfParallelism = value;
            }
        }

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
        /// Gets or sets the <see cref="MemoryAllocator"/> that is currently in use.
        /// </summary>
        public MemoryAllocator MemoryAllocator { get; set; } = ArrayPoolMemoryAllocator.CreateDefault();

        /// <summary>
        /// Gets the maximum header size of all the formats.
        /// </summary>
        internal int MaxHeaderSize => this.ImageFormatsManager.MaxHeaderSize;

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
        /// Creates a shallow copy of the <see cref="Configuration"/>
        /// </summary>
        /// <returns>A new configuration instance</returns>
        public Configuration Clone()
        {
            return new Configuration
            {
                MaxDegreeOfParallelism = this.MaxDegreeOfParallelism,
                ImageFormatsManager = this.ImageFormatsManager,
                MemoryAllocator = this.MemoryAllocator,
                ImageOperationsProvider = this.ImageOperationsProvider,
                ReadOrigin = this.ReadOrigin,

#if !NETSTANDARD1_1
                FileSystem = this.FileSystem
#endif
            };
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