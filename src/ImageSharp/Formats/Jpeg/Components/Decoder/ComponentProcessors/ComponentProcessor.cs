// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Base class for processing component spectral data and converting it to raw color data.
/// </summary>
internal abstract class ComponentProcessor : IDisposable
{
    public ComponentProcessor(MemoryAllocator memoryAllocator, JpegFrame frame, Size postProcessorBufferSize, IJpegComponent component, int blockSize)
    {
        this.Frame = frame;
        this.Component = component;

        this.BlockAreaSize = component.SubSamplingDivisors * blockSize;
        this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
            postProcessorBufferSize.Width,
            postProcessorBufferSize.Height,
            this.BlockAreaSize.Height);
    }

    protected JpegFrame Frame { get; }

    protected IJpegComponent Component { get; }

    protected Buffer2D<float> ColorBuffer { get; }

    protected Size BlockAreaSize { get; }

    /// <summary>
    /// Converts spectral data to color data accessible via <see cref="GetColorBufferRowSpan(int)"/>.
    /// </summary>
    /// <param name="row">Spectral row index to convert.</param>
    /// <param name="clearBlocks">
    /// <see langword="true"/> to clear each spectral block after the transform reads it. Baseline interleaved decoding
    /// uses this, because it decodes the next MCU row into the same blocks. It writes the DC coefficient and only the non-zero AC coefficients.
    /// </param>
    public abstract void CopyBlocksToColorBuffer(int row, bool clearBlocks);

    /// <summary>
    /// Gets converted color buffer row.
    /// </summary>
    /// <param name="row">Row index.</param>
    /// <returns>Color buffer row.</returns>
    public Span<float> GetColorBufferRowSpan(int row) =>
        this.ColorBuffer.DangerousGetRowSpan(row);

    public void Dispose() => this.ColorBuffer.Dispose();
}
