// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;

/// <inheritdoc/>
internal class SpectralConverter<TPixel> : SpectralConverter, IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ComponentProcessor[] componentProcessors;

    private readonly int pixelRowsPerStep;

    private int pixelRowCounter;

    private readonly Buffer2DRegion<TPixel> pixelBuffer;

    /// <summary>
    /// Row storage reused for every scan step. Its layout depends on ICC conversion and transparent-pixel clearing.
    /// </summary>
    private readonly IMemoryOwner<float> rowBuffer;

    /// <summary>
    /// The pixel conversion configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The converter from sRGB to the retained ICC profile, or null when no ICC conversion is required.
    /// </summary>
    private readonly ColorProfileConverter? colorProfileConverter;

    /// <summary>
    /// Whether to set RGB to zero for fully transparent input pixels before JPEG discards alpha.
    /// </summary>
    private readonly bool clearTransparentPixels;

    private readonly int alignedPixelWidth;

    private readonly JpegColorConverterBase colorConverter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectralConverter{TPixel}"/> class.
    /// </summary>
    /// <param name="frame">The configured JPEG frame.</param>
    /// <param name="configuration">The source configuration.</param>
    /// <param name="pixels">The source region, which remains unchanged.</param>
    /// <param name="dequantTables">The quantization tables used by the component processors.</param>
    /// <param name="colorProfileConverter">The converter from sRGB to the retained profile, or null when no ICC conversion is required.</param>
    /// <param name="clearTransparentPixels">Whether to set RGB to zero for fully transparent input pixels before color conversion.</param>
    public SpectralConverter(
        JpegFrame frame,
        Configuration configuration,
        Buffer2DRegion<TPixel> pixels,
        Block8x8F[] dequantTables,
        ColorProfileConverter? colorProfileConverter,
        bool clearTransparentPixels)
    {
        this.configuration = configuration;
        this.colorProfileConverter = colorProfileConverter;
        this.clearTransparentPixels = clearTransparentPixels;
        MemoryAllocator allocator = configuration.MemoryAllocator;

        // iteration data
        int majorBlockWidth = frame.Components.Max((component) => component.SizeInBlocks.Width);
        int majorVerticalSamplingFactor = frame.Components.Max((component) => component.SamplingFactors.Height);

        const int blockPixelHeight = 8;
        this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelHeight;

        // pixel buffer of the image
        this.pixelBuffer = pixels;

        // component processors from spectral to Rgb24
        const int blockPixelWidth = 8;
        this.alignedPixelWidth = majorBlockWidth * blockPixelWidth;
        Size postProcessorBufferSize = new(this.alignedPixelWidth, this.pixelRowsPerStep);
        this.componentProcessors = new ComponentProcessor[frame.Components.Length];
        for (int i = 0; i < this.componentProcessors.Length; i++)
        {
            Component component = frame.Components[i];
            this.componentProcessors[i] = new ComponentProcessor(
                allocator,
                component,
                postProcessorBufferSize,
                dequantTables[component.QuantizationTableIndex]);
        }

        // Reuse this allocation for all rows and scans. Without ICC or clearing, it holds three RGB planes.
        // ICC uses four floats per pixel for straight RGBA and three for RGB input. ICC output can overwrite the RGBA storage.
        // Clearing without ICC uses three RGB planes, four RGBA floats, and one float of space per pixel for packed Rgb24 bytes.
        int components = colorProfileConverter is null ? clearTransparentPixels ? 8 : 3 : 7;
        this.rowBuffer = allocator.Allocate<float>(this.alignedPixelWidth * components, AllocationOptions.Clean);

        // color converter from Rgb24 to YCbCr
        this.colorConverter = JpegColorConverterBase.GetConverter(colorSpace: frame.ColorSpace, precision: 8);
    }

    public void ConvertStrideBaseline()
    {
        // Codestyle suggests expression body but it
        // also requires empty line before comments
        // which looks ugly with expression bodies thus this warning disable
#pragma warning disable IDE0022
        // Convert next pixel stride using single spectral `stride'
        // Note that zero passing eliminates the need of virtual call
        // from JpegComponentPostProcessor
        this.ConvertStride(spectralStep: 0);
#pragma warning restore IDE0022
    }

    public void ConvertFull()
    {
        int steps = (int)Numerics.DivideCeil((uint)this.pixelBuffer.Height, (uint)this.pixelRowsPerStep);
        for (int i = 0; i < steps; i++)
        {
            this.ConvertStride(i);
        }
    }

    /// <summary>
    /// Converts the next padded row group to JPEG components and then spectral blocks.
    /// </summary>
    /// <param name="spectralStep">The destination block-row step.</param>
    private void ConvertStride(int spectralStep)
    {
        int start = this.pixelRowCounter;
        int end = start + this.pixelRowsPerStep;

        int pixelBufferLastVerticalIndex = this.pixelBuffer.Height - 1;

        // Pixel strides must be padded with the last pixel of the stride
        int paddingStartIndex = this.pixelBuffer.Width;
        int paddedPixelsCount = this.alignedPixelWidth - this.pixelBuffer.Width;

        Span<float> buffer = this.rowBuffer.GetSpan();
        ColorProfileConverter? converter = this.colorProfileConverter;
        if (converter is null)
        {
            Span<float> rLane = buffer[..this.alignedPixelWidth];
            Span<float> gLane = buffer.Slice(this.alignedPixelWidth, this.alignedPixelWidth);
            Span<float> bLane = buffer.Slice(this.alignedPixelWidth * 2, this.alignedPixelWidth);

            for (int yy = start; yy < end; yy++)
            {
                int y = yy - this.pixelRowCounter;

                // Unpack TPixel to r/g/b planes
                // TODO: The individual implementation code would be much easier here if
                // we scaled to [0-1] before passing to the individual converters.
                int srcIndex = Math.Min(yy, pixelBufferLastVerticalIndex);
                Span<TPixel> sourceRow = this.pixelBuffer.DangerousGetRowSpan(srcIndex);
                if (this.clearTransparentPixels)
                {
                    // TIFF's Clear option sets RGB to zero where input alpha is zero. JPEG stores no alpha.
                    // Use the row buffer so this operation does not change the source frame.
                    // The RGB planes, straight RGBA vectors and packed Rgb24 bytes occupy separate slices of that buffer.
                    Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(buffer.Slice(this.alignedPixelWidth * 3, this.alignedPixelWidth * 4));
                    Span<Rgb24> packed = MemoryMarshal.Cast<float, Rgb24>(buffer[(this.alignedPixelWidth * 7)..])[..sourceRow.Length];
                    PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.configuration, sourceRow, vectors);
                    EncodingUtilities.ReplaceTransparentPixels(vectors[..sourceRow.Length]);
                    PixelOperations<Rgb24>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, vectors[..sourceRow.Length], packed);
                    PixelOperations<Rgb24>.Instance.UnpackIntoRgbPlanes(rLane, gLane, bLane, packed);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.UnpackIntoRgbPlanes(rLane, gLane, bLane, sourceRow);
                }

                rLane.Slice(paddingStartIndex, paddedPixelsCount).Fill(rLane[paddingStartIndex - 1]);
                gLane.Slice(paddingStartIndex, paddedPixelsCount).Fill(gLane[paddingStartIndex - 1]);
                bLane.Slice(paddingStartIndex, paddedPixelsCount).Fill(bLane[paddingStartIndex - 1]);

                // Convert from rgb24 to target pixel type
                JpegColorConverterBase.ComponentValues values = new(this.componentProcessors, y);
                this.colorConverter.ConvertFromRgb(rLane, gLane, bLane, values);
            }
        }
        else
        {
            Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(buffer[..(this.alignedPixelWidth * 4)]);
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(buffer.Slice(this.alignedPixelWidth * 4, this.alignedPixelWidth * 3));

            for (int yy = start; yy < end; yy++)
            {
                int srcIndex = Math.Min(yy, pixelBufferLastVerticalIndex);
                ReadOnlySpan<TPixel> sourceRow = this.pixelBuffer.DangerousGetRowSpan(srcIndex);

                // Keep native floating-point precision until DCT/quantization. PixelOperations handles
                // the source's alpha representation; JPEG receives straight RGB and stores no alpha.
                PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(this.configuration, sourceRow, vectors);
                if (this.clearTransparentPixels)
                {
                    // Clear fully transparent input pixels before ICC. Alpha is used only for this decision and is not encoded.
                    EncodingUtilities.ReplaceTransparentPixels(vectors[..sourceRow.Length]);
                }

                Rgb.FromScaledVector4(vectors[..sourceRow.Length], rgb[..sourceRow.Length]);

                // Pad in the same source color space before ICC. Vertical padding reuses the last source row.
                rgb[paddingStartIndex..].Fill(rgb[paddingStartIndex - 1]);
                JpegColorConverterBase.ComponentValues values = new(this.componentProcessors, yy - start);
                this.colorConverter.ConvertFromRgb(rgb, values, converter, MemoryMarshal.Cast<Vector4, float>(vectors));
            }
        }

        // Convert pixels to spectral
        for (int i = 0; i < this.componentProcessors.Length; i++)
        {
            this.componentProcessors[i].CopyColorBufferToBlocks(spectralStep);
        }

        this.pixelRowCounter = end;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (ComponentProcessor cpp in this.componentProcessors)
        {
            cpp.Dispose();
        }

        this.rowBuffer.Dispose();
    }
}
