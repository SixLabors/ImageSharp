// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
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

    private readonly Buffer2D<TPixel> pixelBuffer;

    private readonly IMemoryOwner<float> redLane;

    private readonly IMemoryOwner<float> greenLane;

    private readonly IMemoryOwner<float> blueLane;

    private readonly int alignedPixelWidth;

    private readonly JpegColorConverterBase colorConverter;

    public SpectralConverter(JpegFrame frame, Image<TPixel> image, Block8x8F[] dequantTables)
    {
        MemoryAllocator allocator = image.Configuration.MemoryAllocator;

        // iteration data
        int majorBlockWidth = frame.Components.Max((component) => component.SizeInBlocks.Width);
        int majorVerticalSamplingFactor = frame.Components.Max((component) => component.SamplingFactors.Height);

        const int blockPixelHeight = 8;
        this.pixelRowsPerStep = majorVerticalSamplingFactor * blockPixelHeight;

        // pixel buffer of the image
        this.pixelBuffer = image.GetRootFramePixelBuffer();

        // component processors from spectral to Rgb24
        const int blockPixelWidth = 8;
        this.alignedPixelWidth = majorBlockWidth * blockPixelWidth;
        Size postProcessorBufferSize = new(this.alignedPixelWidth, this.pixelRowsPerStep);
        this.componentProcessors = new ComponentProcessor[frame.Components.Length];
        for (int i = 0; i < this.componentProcessors.Length; i++)
        {
            Component component = frame.Components[i];
            this.componentProcessors[i] = new(
                allocator,
                component,
                postProcessorBufferSize,
                dequantTables[component.QuantizationTableIndex]);
        }

        this.redLane = allocator.Allocate<float>(this.alignedPixelWidth, AllocationOptions.Clean);
        this.greenLane = allocator.Allocate<float>(this.alignedPixelWidth, AllocationOptions.Clean);
        this.blueLane = allocator.Allocate<float>(this.alignedPixelWidth, AllocationOptions.Clean);

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

    private void ConvertStride(int spectralStep)
    {
        int start = this.pixelRowCounter;
        int end = start + this.pixelRowsPerStep;

        int pixelBufferLastVerticalIndex = this.pixelBuffer.Height - 1;

        // Pixel strides must be padded with the last pixel of the stride
        int paddingStartIndex = this.pixelBuffer.Width;
        int paddedPixelsCount = this.alignedPixelWidth - this.pixelBuffer.Width;

        Span<float> rLane = this.redLane.GetSpan();
        Span<float> gLane = this.greenLane.GetSpan();
        Span<float> bLane = this.blueLane.GetSpan();

        for (int yy = start; yy < end; yy++)
        {
            int y = yy - this.pixelRowCounter;

            // Unpack TPixel to r/g/b planes
            // TODO: The individual implementation code would be much easier here if
            // we scaled to [0-1] before passing to the individual converters.
            int srcIndex = Math.Min(yy, pixelBufferLastVerticalIndex);
            Span<TPixel> sourceRow = this.pixelBuffer.DangerousGetRowSpan(srcIndex);
            PixelOperations<TPixel>.Instance.UnpackIntoRgbPlanes(rLane, gLane, bLane, sourceRow);

            rLane.Slice(paddingStartIndex).Fill(rLane[paddingStartIndex - 1]);
            gLane.Slice(paddingStartIndex).Fill(gLane[paddingStartIndex - 1]);
            bLane.Slice(paddingStartIndex).Fill(bLane[paddingStartIndex - 1]);

            // Convert from rgb24 to target pixel type
            JpegColorConverterBase.ComponentValues values = new(this.componentProcessors, y);
            this.colorConverter.ConvertFromRgb(values, rLane, gLane, bLane);
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

        this.redLane.Dispose();
        this.greenLane.Dispose();
        this.blueLane.Dispose();
    }
}
