// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
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
    /// The converter from <see cref="iccConverterProfile"/> to sRGB, created on the first stride with a profile.
    /// </summary>
    private ColorProfileConverter iccConverter;

    /// <summary>
    /// The ICC row of scaled vectors. The converter allocates it only for an image with a profile, and uses it again for each row.
    /// </summary>
    private IMemoryOwner<Vector4> iccRowBuffer;

    /// <summary>
    /// The profile <see cref="iccConverter"/> was built for.
    /// </summary>
    private IccProfile iccConverterProfile;

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
                this.ConvertStride(step, iccProfile, false);
            }
        }

        Buffer2D<TPixel> buffer = this.pixelBuffer;

        // PrepareForDecoding does not clear the pixel buffer, because each stride writes all of its rows.
        // A truncated baseline scan stops before the last stride. The loop clears the rows that no stride wrote.
        for (int y = this.pixelRowCounter; y < buffer.Height; y++)
        {
            buffer.DangerousGetRowSpan(y).Clear();
        }

        this.pixelBuffer = null;
        return buffer;
    }

    /// <summary>
    /// Converts single spectral jpeg stride to color stride.
    /// </summary>
    /// <param name="spectralStep">Spectral stride index.</param>
    /// <param name="iccProfile">Optional ICC profile for color conversion.</param>
    /// <param name="clearBlocks"><see langword="true"/> to clear each spectral block after the transform reads it.</param>
    private void ConvertStride(int spectralStep, IccProfile iccProfile, bool clearBlocks)
    {
        int maxY = Math.Min(this.pixelBuffer.Height, this.pixelRowCounter + this.pixelRowsPerStep);

        for (int i = 0; i < this.componentProcessors.Length; i++)
        {
            this.componentProcessors[i].CopyBlocksToColorBuffer(spectralStep, clearBlocks);
        }

        int width = this.pixelBuffer.Width;

        // The profile is fixed for the image, so its converter and transforms are built once, not per row.
        if (iccProfile != null && !ReferenceEquals(iccProfile, this.iccConverterProfile))
        {
            this.iccConverter = new ColorProfileConverter(new ColorConversionOptions
            {
                MemoryAllocator = this.Configuration.MemoryAllocator,
                SourceIccProfile = iccProfile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            });

            this.iccConverterProfile = iccProfile;

            // One vector for each pixel can hold all JPEG source models, and the RGB result in the same place.
            this.iccRowBuffer ??= this.Configuration.MemoryAllocator.Allocate<Vector4>(width);
        }

        for (int yy = this.pixelRowCounter; yy < maxY; yy++)
        {
            int y = yy - this.pixelRowCounter;

            JpegColorConverterBase.ComponentValues values = new(this.componentProcessors, y);

            // Container formats can prepare their source samples before either eager or deferred JPEG ICC conversion.
            this.PrepareColorComponents(in values, this.frame.Precision, iccProfile);

            if (iccProfile != null)
            {
                Span<Vector4> row = this.iccRowBuffer.GetSpan()[..width];

                // The method converts only the visible samples. The row gets straight sRGB vectors with opaque alpha.
                this.colorConverter.ConvertToRgb(values.Slice(0, width), row, this.iccConverter);

                // ICC can produce fractional RGB and values outside [0,1], even from bounded JPEG components.
                // Let TPixel apply its own range and precision instead of clipping every destination through bytes.
                // The method packs the full row in one call, because bulk pixel operations use SIMD only above a minimum length.
                // The row is not necessary after this call. Thus, the destructive packer can use it again.
                PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.Configuration, row, this.pixelBuffer.DangerousGetRowSpan(yy));

                continue;
            }

            Span<byte> r = this.rgbBuffer.Slice(0, width);
            Span<byte> g = this.rgbBuffer.Slice(width, width);
            Span<byte> b = this.rgbBuffer.Slice(width * 2, width);

            // One pass converts the visible samples to RGB bytes. In the SIMD steps, the normalized RGB values stay in registers.
            this.colorConverter.ConvertToRgb(in values, r, g, b);

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

    /// <summary>
    /// Prepares source color components before JPEG color and ICC conversion.
    /// </summary>
    /// <param name="values">The source component planes for the current row.</param>
    /// <param name="precision">The JPEG sample precision in bits.</param>
    /// <param name="iccProfile">The selected source ICC profile, if present.</param>
    protected virtual void PrepareColorComponents(in JpegColorConverterBase.ComponentValues values, int precision, IccProfile iccProfile)
    {
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

        // This is the pixel buffer for the resulting image.
        // Each stride writes all of its rows, and GetPixelBuffer clears the rows that a truncated scan does not reach.
        // Thus, the buffer does not need a clean allocation.
        this.pixelBuffer = allocator.Allocate2D<TPixel>(
            pixelSize.Width,
            pixelSize.Height,
            this.Configuration.PreferContiguousImageBuffers);

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
        // This call converts the next pixel stride from the single spectral stride.
        // The scan decoder writes the DC coefficient and only the non-zero AC coefficients, and it decodes the next MCU row into the same blocks.
        // Thus, the transform clears each block after it reads it.
        this.ConvertStride(0, iccProfile, true);
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

        this.iccRowBuffer?.Dispose();
        this.rgbBuffer?.Dispose();
        this.paddedProxyPixelRow?.Dispose();
        this.pixelBuffer?.Dispose();
    }
}
