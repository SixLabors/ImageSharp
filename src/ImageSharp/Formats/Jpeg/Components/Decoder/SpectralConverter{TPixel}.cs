// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <inheritdoc/>
/// <remarks>
/// Color decoding scheme:
/// <list type = "number" >
/// <listheader>
///     <item>Decode spectral data to Jpeg color space</item>
///     <item>Convert from Jpeg color space to RGB</item>
///     <item>Convert from RGB to target pixel space</item>
/// </listheader>
/// </list>
/// </remarks>
internal class SpectralConverter<TPixel> : SpectralConverter, IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private JpegFrame frame;

    private IRawJpegData jpegData;

    /// <summary>
    /// Jpeg component converters from decompressed spectral to color data.
    /// </summary>
    private ComponentProcessor[] componentProcessors;

    /// <summary>
    /// Color converter from jpeg color space to target pixel color space.
    /// </summary>
    private JpegColorConverterBase colorConverter;

    /// <summary>
    /// Intermediate buffer of RGB components used in color conversion.
    /// </summary>
    private IMemoryOwner<byte> rgbBuffer;

    /// <summary>
    /// Proxy buffer used in packing from RGB to target TPixel pixels.
    /// </summary>
    private IMemoryOwner<TPixel> paddedProxyPixelRow;

    /// <summary>
    /// Resulting 2D pixel buffer.
    /// </summary>
    private Buffer2D<TPixel> pixelBuffer;

    /// <summary>
    /// How many pixel rows are processed in one 'stride'.
    /// </summary>
    private int pixelRowsPerStep;

    /// <summary>
    /// How many pixel rows were processed.
    /// </summary>
    private int pixelRowCounter;

    /// <summary>
    /// Represent target size after decoding for scaling decoding mode.
    /// </summary>
    /// <remarks>
    /// Null if no scaling is required.
    /// </remarks>
    private Size? targetSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectralConverter{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="targetSize">Optional target size for decoded image.</param>
    public SpectralConverter(Configuration configuration, Size? targetSize = null)
    {
        this.Configuration = configuration;
        this.targetSize = targetSize;
    }

    /// <summary>
    /// Gets the configuration instance associated with current decoding routine.
    /// </summary>
    public Configuration Configuration { get; }

    /// <summary>
    /// Gets a value indicating whether the converter has a pixel buffer.
    /// </summary>
    /// <returns><see langword="true"/> if the converter has a pixel buffer; otherwise, <see langword="false"/>.</returns>
    public override bool HasPixelBuffer() => this.pixelBuffer is not null;

    /// <summary>
    /// Gets converted pixel buffer.
    /// </summary>
    /// <remarks>
    /// For non-baseline interleaved jpeg this method does a 'lazy' spectral
    /// conversion from spectral to color.
    /// </remarks>
    /// <param name="iccProfile">Optional ICC profile for color conversion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pixel buffer.</returns>
    public Buffer2D<TPixel> GetPixelBuffer(IccProfile iccProfile, CancellationToken cancellationToken)
    {
        if (!this.Converted)
        {
            this.PrepareForDecoding();

            int steps = (int)Math.Ceiling(this.pixelBuffer.Height / (float)this.pixelRowsPerStep);

            for (int step = 0; step < steps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.ConvertStride(step, iccProfile);
            }
        }

        Buffer2D<TPixel> buffer = this.pixelBuffer;
        this.pixelBuffer = null;
        return buffer;
    }

    /// <summary>
    /// Converts single spectral jpeg stride to color stride.
    /// </summary>
    /// <param name="spectralStep">Spectral stride index.</param>
    /// <param name="iccProfile">Optional ICC profile for color conversion.</param>
    private void ConvertStride(int spectralStep, IccProfile iccProfile)
    {
        int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

        for (int i = 0; i < this.componentProcessors.Length; i++)
        {
            this.componentProcessors[i].CopyBlocksToColorBuffer(spectralStep);
        }

        int width = this.pixelBuffer.Width;

        for (int yy = this.pixelRowCounter; yy < maxY; yy++)
        {
            int y = yy - this.pixelRowCounter;

            JpegColorConverterBase.ComponentValues values = new(this.componentProcessors, y);

            values = values.Slice(0, width); // slice away Jpeg padding

            if (iccProfile != null)
            {
                this.colorConverter.ConvertToRgbInPlaceWithIcc(this.Configuration, in values, iccProfile);
            }
            else
            {
                this.colorConverter.ConvertToRgbInPlace(in values);
            }

            Span<byte> r = this.rgbBuffer.Slice(0, width);
            Span<byte> g = this.rgbBuffer.Slice(width, width);
            Span<byte> b = this.rgbBuffer.Slice(width * 2, width);

            SimdUtils.NormalizedFloatToByteSaturate(values.Component0, r);
            SimdUtils.NormalizedFloatToByteSaturate(values.Component1, g);
            SimdUtils.NormalizedFloatToByteSaturate(values.Component2, b);

            // PackFromRgbPlanes expects the destination to be padded, so try to get padded span containing extra elements from the next row.
            // If we can't get such a padded row because we are on a MemoryGroup boundary or at the last row,
            // pack pixels to a temporary, padded proxy buffer, then copy the relevant values to the destination row.
            if (this.pixelBuffer.DangerousTryGetPaddedRowSpan(yy, 3, out Span<TPixel> destRow))
            {
                PixelOperations<TPixel>.Instance.PackFromRgbPlanes(r, g, b, destRow);
            }
            else
            {
                Span<TPixel> proxyRow = this.paddedProxyPixelRow.GetSpan();
                PixelOperations<TPixel>.Instance.PackFromRgbPlanes(r, g, b, proxyRow);
                proxyRow[..width].CopyTo(this.pixelBuffer.DangerousGetRowSpan(yy));
            }
        }

        this.pixelRowCounter += this.pixelRowsPerStep;
    }

    /// <inheritdoc/>
    public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
    {
        this.frame = frame;
        this.jpegData = jpegData;
    }

    /// <inheritdoc/>
    public override void PrepareForDecoding()
    {
        DebugGuard.IsTrue(this.colorConverter == null, "SpectralConverter.PrepareForDecoding() must be called once.");

        MemoryAllocator allocator = this.Configuration.MemoryAllocator;

        // Color converter from RGB to TPixel
        JpegColorConverterBase converter = this.GetColorConverter(this.frame, this.jpegData);
        this.colorConverter = converter;

        // Resulting image size
        Size pixelSize = CalculateResultingImageSize(this.frame.PixelSize, this.targetSize, out int blockPixelSize);

        // Iteration data
        int majorBlockWidth = this.frame.Components.Max((component) => component.SizeInBlocks.Width);
        int majorVerticalSamplingFactor = this.frame.Components.Max((component) => component.SamplingFactors.Height);

        this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelSize;

        // Pixel buffer for resulting image
        this.pixelBuffer = allocator.Allocate2D<TPixel>(
            pixelSize.Width,
            pixelSize.Height,
            this.Configuration.PreferContiguousImageBuffers,
            AllocationOptions.Clean);
        this.paddedProxyPixelRow = allocator.Allocate<TPixel>(pixelSize.Width + 3);

        // Component processors from spectral to RGB
        int bufferWidth = majorBlockWidth * blockPixelSize;

        // Converters process pixels in batches and require target buffer size to be divisible by a batch size
        // Corner case: image size including jpeg padding is already divisible by a batch size or remainder == 0
        int elementsPerBatch = converter.ElementsPerBatch;
        int batchRemainder = bufferWidth & (elementsPerBatch - 1);
        int widthComplementaryValue = batchRemainder == 0 ? 0 : elementsPerBatch - batchRemainder;

        Size postProcessorBufferSize = new(bufferWidth + widthComplementaryValue, this.pixelRowsPerStep);
        this.componentProcessors = this.CreateComponentProcessors(this.frame, this.jpegData, blockPixelSize, postProcessorBufferSize);

        // Single 'stride' rgba32 buffer for conversion between spectral and TPixel
        this.rgbBuffer = allocator.Allocate<byte>(pixelSize.Width * 3);
    }

    /// <inheritdoc/>
    public override void ConvertStrideBaseline(IccProfile iccProfile)
    {
        // Convert next pixel stride using single spectral `stride'
        // Note that zero passing eliminates extra virtual call
        this.ConvertStride(spectralStep: 0, iccProfile);

        foreach (ComponentProcessor cpp in this.componentProcessors)
        {
            cpp.ClearSpectralBuffers();
        }
    }

    protected ComponentProcessor[] CreateComponentProcessors(JpegFrame frame, IRawJpegData jpegData, int blockPixelSize, Size processorBufferSize)
    {
        MemoryAllocator allocator = this.Configuration.MemoryAllocator;
        ComponentProcessor[] componentProcessors = new ComponentProcessor[frame.Components.Length];
        for (int i = 0; i < componentProcessors.Length; i++)
        {
            componentProcessors[i] = blockPixelSize switch
            {
                4 => new DownScalingComponentProcessor2(allocator, frame, jpegData, processorBufferSize, frame.Components[i]),
                2 => new DownScalingComponentProcessor4(allocator, frame, jpegData, processorBufferSize, frame.Components[i]),
                1 => new DownScalingComponentProcessor8(allocator, frame, jpegData, processorBufferSize, frame.Components[i]),
                _ => new DirectComponentProcessor(allocator, frame, jpegData, processorBufferSize, frame.Components[i]),
            };
        }

        return componentProcessors;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.componentProcessors != null)
        {
            foreach (ComponentProcessor cpp in this.componentProcessors)
            {
                cpp.Dispose();
            }
        }

        this.rgbBuffer?.Dispose();
        this.paddedProxyPixelRow?.Dispose();
        this.pixelBuffer?.Dispose();
    }
}
