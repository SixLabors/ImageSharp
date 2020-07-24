// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides configuration which allows altering default behaviour or extending the library.
    /// </summary>
    public sealed class Configuration
    {
        /// <summary>
        /// A lazily initialized configuration default instance.
        /// </summary>
        private static readonly Lazy<Configuration> Lazy = new Lazy<Configuration>(CreateDefaultInstance);
        private const int DefaultStreamProcessingBufferSize = 8096;
        private int streamProcessingBufferSize = DefaultStreamProcessingBufferSize;
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
                if (value == 0 || value < -1)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.MaxDegreeOfParallelism));
                }

                this.maxDegreeOfParallelism = value;
            }
        }

        /// <summary>
        /// Gets or sets the size of the buffer to use when working with streams.
        /// Intitialized with <see cref="DefaultStreamProcessingBufferSize"/> by default.
        /// </summary>
        public int StreamProcessingBufferSize
        {
            get => this.streamProcessingBufferSize;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.StreamProcessingBufferSize));
                }

                this.streamProcessingBufferSize = value;
            }
        }

        /// <summary>
        /// Gets a set of properties for the Congiguration.
        /// </summary>
        /// <remarks>This can be used for storing global settings and defaults to be accessable to processors.</remarks>
        public IDictionary<object, object> Properties { get; } = new ConcurrentDictionary<object, object>();

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

        /// <summary>
        /// Gets or sets the filesystem helper for accessing the local file system.
        /// </summary>
        internal IFileSystem FileSystem { get; set; } = new LocalFileSystem();

        /// <summary>
        /// Gets or sets the working buffer size hint for image processors.
        /// The default value is 1MB.
        /// </summary>
        /// <remarks>
        /// Currently only used by Resize. If the working buffer is expected to be discontiguous,
        /// min(WorkingBufferSizeHintInBytes, BufferCapacityInBytes) should be used.
        /// </remarks>
        internal int WorkingBufferSizeHintInBytes { get; set; } = 1 * 1024 * 1024;

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
        /// Creates a shallow copy of the <see cref="Configuration"/>.
        /// </summary>
        /// <returns>A new configuration instance.</returns>
        public Configuration Clone()
        {
            return new Configuration
            {
                MaxDegreeOfParallelism = this.MaxDegreeOfParallelism,
                StreamProcessingBufferSize = this.StreamProcessingBufferSize,
                ImageFormatsManager = this.ImageFormatsManager,
                MemoryAllocator = this.MemoryAllocator,
                ImageOperationsProvider = this.ImageOperationsProvider,
                ReadOrigin = this.ReadOrigin,
                FileSystem = this.FileSystem,
                WorkingBufferSizeHintInBytes = this.WorkingBufferSizeHintInBytes,
            };
        }

        /// <summary>
        /// Creates the default instance with the following <see cref="IConfigurationModule"/>s preregistered:
        /// <see cref="PngConfigurationModule"/>
        /// <see cref="JpegConfigurationModule"/>
        /// <see cref="GifConfigurationModule"/>
        /// <see cref="BmpConfigurationModule"/>.
        /// <see cref="TgaConfigurationModule"/>.
        /// </summary>
        /// <returns>The default configuration of <see cref="Configuration"/>.</returns>
        internal static Configuration CreateDefaultInstance()
        {
            return new Configuration(
                new PngConfigurationModule(),
                new JpegConfigurationModule(),
                new GifConfigurationModule(),
                new BmpConfigurationModule(),
                new TgaConfigurationModule());
        }
    }
}
