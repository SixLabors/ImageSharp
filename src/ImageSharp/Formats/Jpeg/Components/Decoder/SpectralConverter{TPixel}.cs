// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;
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
    /// <summary>
    /// <see cref="Configuration"/> instance associated with current
    /// decoding routine.
    /// </summary>
    private readonly Configuration configuration;

    private JpegFrame frame = null!;

    private IRawJpegData jpegData = null!;

    /// <summary>
    /// Jpeg component converters from decompressed spectral to color data.
    /// </summary>
    private ComponentProcessor[] componentProcessors = null!;

    /// <summary>
    /// Color converter from jpeg color space to target pixel color space.
    /// </summary>
    private JpegColorConverterBase colorConverter = null!;

    /// <summary>
    /// Intermediate buffer of RGB components used in color conversion.
    /// </summary>
    private IMemoryOwner<byte> rgbBuffer = null!;

    /// <summary>
    /// Proxy buffer used in packing from RGB to target TPixel pixels.
    /// </summary>
    private IMemoryOwner<TPixel> paddedProxyPixelRow = null!;

    /// <summary>
    /// Resulting 2D pixel buffer.
    /// </summary>
    private Buffer2D<TPixel>? pixelBuffer;

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
        this.configuration = configuration;

        this.targetSize = targetSize;
    }

    /// <summary>
    /// Gets converted pixel buffer.
    /// </summary>
    /// <remarks>
    /// For non-baseline interleaved jpeg this method does a 'lazy' spectral
    /// conversion from spectral to color.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pixel buffer.</returns>
    public Buffer2D<TPixel> GetPixelBuffer(CancellationToken cancellationToken)
    {
        if (!this.Converted)
        {
            this.PrepareForDecoding();

            ArgumentNullException.ThrowIfNull(this.pixelBuffer);

            int steps = (int)Math.Ceiling(this.pixelBuffer.Height / (float)this.pixelRowsPerStep);

            for (int step = 0; step < steps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.ConvertStride(step);
            }
        }

        Buffer2D<TPixel>? buffer = this.pixelBuffer;
        this.pixelBuffer = null;
        return buffer!;
    }

    /// <summary>
    /// Converts single spectral jpeg stride to color stride.
    /// </summary>
    /// <param name="spectralStep">Spectral stride index.</param>
    private void ConvertStride(int spectralStep)
    {
        ArgumentNullException.ThrowIfNull(this.pixelBuffer);

        int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

        for (int i = 0; i < this.componentProcessors.Length; i++)
        {
            this.componentProcessors[i].CopyBlocksToColorBuffer(spectralStep);
        }

        int width = this.pixelBuffer.Width;

        for (int yy = this.pixelRowCounter; yy < maxY; yy++)
        {
            int y = yy - this.pixelRowCounter;

            var values = new JpegColorConverterBase.ComponentValues(this.componentProcessors, y);

            this.colorConverter.ConvertToRgbInplace(values);
            values = values.Slice(0, width); // slice away Jpeg padding

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

        MemoryAllocator allocator = this.configuration.MemoryAllocator;

        // color converter from RGB to TPixel
        JpegColorConverterBase converter = this.GetColorConverter(this.frame, this.jpegData);
        this.colorConverter = converter;

        // resulting image size
        Size pixelSize = CalculateResultingImageSize(this.frame.PixelSize, this.targetSize, out int blockPixelSize);

        // iteration data
        ArgumentNullException.ThrowIfNull(this.frame.Components);

        int majorBlockWidth = this.frame.Components.Max((component) => component.SizeInBlocks.Width);
        int majorVerticalSamplingFactor = this.frame.Components.Max((component) => component.SamplingFactors.Height);

        this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelSize;

        // pixel buffer for resulting image
        this.pixelBuffer = allocator.Allocate2D<TPixel>(
            pixelSize.Width,
            pixelSize.Height,
            this.configuration.PreferContiguousImageBuffers);
        this.paddedProxyPixelRow = allocator.Allocate<TPixel>(pixelSize.Width + 3);

        // component processors from spectral to RGB
        int bufferWidth = majorBlockWidth * blockPixelSize;
        int batchSize = converter.ElementsPerBatch;
        int batchRemainder = bufferWidth & (batchSize - 1);
        var postProcessorBufferSize = new Size(bufferWidth + (batchSize - batchRemainder), this.pixelRowsPerStep);
        this.componentProcessors = this.CreateComponentProcessors(this.frame, this.jpegData, blockPixelSize, postProcessorBufferSize);

        // single 'stride' rgba32 buffer for conversion between spectral and TPixel
        this.rgbBuffer = allocator.Allocate<byte>(pixelSize.Width * 3);
    }

    /// <inheritdoc/>
    public override void ConvertStrideBaseline()
    {
        // Convert next pixel stride using single spectral `stride'
        // Note that zero passing eliminates extra virtual call
        this.ConvertStride(spectralStep: 0);

        foreach (ComponentProcessor cpp in this.componentProcessors)
        {
            cpp.ClearSpectralBuffers();
        }
    }

    protected ComponentProcessor[] CreateComponentProcessors(JpegFrame frame, IRawJpegData jpegData, int blockPixelSize, Size processorBufferSize)
    {
        MemoryAllocator allocator = this.configuration.MemoryAllocator;

        ArgumentNullException.ThrowIfNull(frame.Components);

        var componentProcessors = new ComponentProcessor[frame.Components.Length];
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
