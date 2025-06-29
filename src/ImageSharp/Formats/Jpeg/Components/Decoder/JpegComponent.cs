// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Represents a single frame component.
/// </summary>
internal class JpegComponent : IDisposable, IJpegComponent
{
    private readonly MemoryAllocator memoryAllocator;

    public JpegComponent(MemoryAllocator memoryAllocator, JpegFrame frame, byte id, int horizontalFactor, int verticalFactor, byte quantizationTableIndex, int index)
    {
        this.memoryAllocator = memoryAllocator;
        this.Frame = frame;
        this.Id = id;

        this.HorizontalSamplingFactor = horizontalFactor;
        this.VerticalSamplingFactor = verticalFactor;
        this.SamplingFactors = new Size(this.HorizontalSamplingFactor, this.VerticalSamplingFactor);

        this.QuantizationTableIndex = quantizationTableIndex;
        this.Index = index;
    }

    /// <summary>
    /// Gets the component id.
    /// </summary>
    public byte Id { get; }

    /// <summary>
    /// Gets or sets DC coefficient predictor.
    /// </summary>
    public int DcPredictor { get; set; }

    /// <summary>
    /// Gets the horizontal sampling factor.
    /// </summary>
    public int HorizontalSamplingFactor { get; }

    /// <summary>
    /// Gets the vertical sampling factor.
    /// </summary>
    public int VerticalSamplingFactor { get; }

    /// <inheritdoc />
    public Buffer2D<Block8x8> SpectralBlocks { get; private set; }

    /// <inheritdoc />
    public Size SubSamplingDivisors { get; private set; }

    /// <inheritdoc />
    public int QuantizationTableIndex { get; }

    /// <inheritdoc />
    public int Index { get; }

    /// <inheritdoc />
    public Size SizeInBlocks { get; private set; }

    /// <inheritdoc />
    public Size SamplingFactors { get; set; }

    /// <summary>
    /// Gets the number of blocks per line.
    /// </summary>
    public int WidthInBlocks { get; private set; }

    /// <summary>
    /// Gets the number of blocks per column.
    /// </summary>
    public int HeightInBlocks { get; private set; }

    /// <summary>
    /// Gets or sets the index for the DC Huffman table.
    /// </summary>
    public int DcTableId { get; set; }

    /// <summary>
    /// Gets or sets the index for the AC Huffman table.
    /// </summary>
    public int AcTableId { get; set; }

    public JpegFrame Frame { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.SpectralBlocks?.Dispose();
        this.SpectralBlocks = null;
    }

    /// <summary>
    /// Initializes component for future buffers initialization.
    /// </summary>
    /// <param name="maxSubFactorH">Maximal horizontal subsampling factor among all the components.</param>
    /// <param name="maxSubFactorV">Maximal vertical subsampling factor among all the components.</param>
    public void Init(int maxSubFactorH, int maxSubFactorV)
    {
        this.WidthInBlocks = (int)MathF.Ceiling(
            MathF.Ceiling(this.Frame.PixelWidth / 8F) * this.HorizontalSamplingFactor / maxSubFactorH);

        this.HeightInBlocks = (int)MathF.Ceiling(
            MathF.Ceiling(this.Frame.PixelHeight / 8F) * this.VerticalSamplingFactor / maxSubFactorV);

        int blocksPerLineForMcu = this.Frame.McusPerLine * this.HorizontalSamplingFactor;
        int blocksPerColumnForMcu = this.Frame.McusPerColumn * this.VerticalSamplingFactor;
        this.SizeInBlocks = new Size(blocksPerLineForMcu, blocksPerColumnForMcu);

        this.SubSamplingDivisors = new Size(maxSubFactorH, maxSubFactorV).DivideBy(this.SamplingFactors);

        if (this.SubSamplingDivisors.Width == 0 || this.SubSamplingDivisors.Height == 0)
        {
            JpegThrowHelper.ThrowBadSampling();
        }
    }

    /// <inheritdoc/>
    public void AllocateSpectral(bool fullScan)
    {
        if (this.SpectralBlocks != null)
        {
            // This method will be called each scan marker so we need to allocate only once.
            return;
        }

        int spectralAllocWidth = this.SizeInBlocks.Width;
        int spectralAllocHeight = fullScan ? this.SizeInBlocks.Height : this.VerticalSamplingFactor;

        this.SpectralBlocks = this.memoryAllocator.Allocate2D<Block8x8>(spectralAllocWidth, spectralAllocHeight, AllocationOptions.Clean);
    }
}
