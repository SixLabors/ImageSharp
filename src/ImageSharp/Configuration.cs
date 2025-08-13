// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Concurrent;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides configuration which allows altering default behaviour or extending the library.
/// </summary>
public sealed class Configuration
{
    /// <summary>
    /// A lazily initialized configuration default instance.
    /// </summary>
    private static readonly Lazy<Configuration> Lazy = new(CreateDefaultInstance);
    private const int DefaultStreamProcessingBufferSize = 8096;
    private int streamProcessingBufferSize = DefaultStreamProcessingBufferSize;
    private int maxDegreeOfParallelism = Environment.ProcessorCount;
    private MemoryAllocator memoryAllocator = MemoryAllocator.Default;

    /// <summary>
    /// Initializes a new instance of the <see cref="Configuration" /> class.
    /// </summary>
    public Configuration()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Configuration" /> class.
    /// </summary>
    /// <param name="configurationModules">A collection of configuration modules to register.</param>
    public Configuration(params IImageFormatConfigurationModule[] configurationModules)
    {
        if (configurationModules != null)
        {
            foreach (IImageFormatConfigurationModule p in configurationModules)
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
            if (value is 0 or < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(this.MaxDegreeOfParallelism));
            }

            this.maxDegreeOfParallelism = value;
        }
    }

    /// <summary>
    /// Gets or sets the size of the buffer to use when working with streams.
    /// Initialized with <see cref="DefaultStreamProcessingBufferSize"/> by default.
    /// </summary>
    public int StreamProcessingBufferSize
    {
        get => this.streamProcessingBufferSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            this.streamProcessingBufferSize = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to force image buffers to be contiguous whenever possible.
    /// </summary>
    /// <remarks>
    /// Contiguous allocations are not possible, if the image needs a buffer larger than <see cref="int.MaxValue"/>.
    /// </remarks>
    public bool PreferContiguousImageBuffers { get; set; }

    /// <summary>
    /// Gets a set of properties for the Configuration.
    /// </summary>
    /// <remarks>This can be used for storing global settings and defaults to be accessible to processors.</remarks>
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
    /// Gets or the <see cref="ImageFormatManager"/> that is currently in use.
    /// </summary>
    public ImageFormatManager ImageFormatsManager { get; private set; } = new();

    /// <summary>
    /// Gets or sets the <see cref="Memory.MemoryAllocator"/> that is currently in use.
    /// Defaults to <see cref="MemoryAllocator.Default"/>.
    /// <para />
    /// Allocators are expensive, so it is strongly recommended to use only one busy instance per process.
    /// In case you need to customize it, you can ensure this by changing
    /// </summary>
    /// <remarks>
    /// It's possible to reduce allocator footprint by assigning a custom instance created with
    /// <see cref="MemoryAllocator.Create(MemoryAllocatorOptions)"/>, but note that since the default pooling
    /// allocators are expensive, it is strictly recommended to use a single process-wide allocator.
    /// You can ensure this by altering the allocator of <see cref="Default"/>, or by implementing custom application logic that
    /// manages allocator lifetime.
    /// <para />
    /// If an allocator has to be dropped for some reason, <see cref="MemoryAllocator.ReleaseRetainedResources"/>
    /// shall be invoked after disposing all associated <see cref="Image"/> instances.
    /// </remarks>
    public MemoryAllocator MemoryAllocator
    {
        get => this.memoryAllocator;
        set
        {
            Guard.NotNull(value, nameof(this.MemoryAllocator));
            this.memoryAllocator = value;
        }
    }

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
    public void Configure(IImageFormatConfigurationModule configuration)
    {
        Guard.NotNull(configuration, nameof(configuration));
        configuration.Configure(this);
    }

    /// <summary>
    /// Creates a shallow copy of the <see cref="Configuration"/>.
    /// </summary>
    /// <returns>A new configuration instance.</returns>
    public Configuration Clone() => new()
    {
        MaxDegreeOfParallelism = this.MaxDegreeOfParallelism,
        StreamProcessingBufferSize = this.StreamProcessingBufferSize,
        ImageFormatsManager = this.ImageFormatsManager,
        memoryAllocator = this.memoryAllocator,
        ImageOperationsProvider = this.ImageOperationsProvider,
        ReadOrigin = this.ReadOrigin,
        FileSystem = this.FileSystem,
        WorkingBufferSizeHintInBytes = this.WorkingBufferSizeHintInBytes,
    };

    /// <summary>
    /// Creates the default instance with the following <see cref="IImageFormatConfigurationModule"/>s preregistered:
    /// <see cref="PngConfigurationModule"/>
    /// <see cref="JpegConfigurationModule"/>
    /// <see cref="GifConfigurationModule"/>
    /// <see cref="BmpConfigurationModule"/>.
    /// <see cref="PbmConfigurationModule"/>.
    /// <see cref="TgaConfigurationModule"/>.
    /// <see cref="TiffConfigurationModule"/>.
    /// <see cref="WebpConfigurationModule"/>.
    /// <see cref="QoiConfigurationModule"/>.
    /// <see cref="HeifConfigurationModule"/>.
    /// </summary>
    /// <returns>The default configuration of <see cref="Configuration"/>.</returns>
    internal static Configuration CreateDefaultInstance() => new(
            new PngConfigurationModule(),
            new JpegConfigurationModule(),
            new GifConfigurationModule(),
            new BmpConfigurationModule(),
            new PbmConfigurationModule(),
            new TgaConfigurationModule(),
            new TiffConfigurationModule(),
            new WebpConfigurationModule(),
            new QoiConfigurationModule(),
            new HeifConfigurationModule(),
            new IcoConfigurationModule(),
            new CurConfigurationModule());
}
