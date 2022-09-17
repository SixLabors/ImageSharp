// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// A <see langword="struct"/> implementing the grayscale levels logic as <see cref="IRowOperation"/>.
/// </summary>
internal readonly struct GrayscaleLevelsRowOperation<TPixel> : IRowOperation
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Rectangle bounds;
    private readonly IMemoryOwner<int> histogramBuffer;
    private readonly Buffer2D<TPixel> source;
    private readonly int luminanceLevels;

    [MethodImpl(InliningOptions.ShortMethod)]
    public GrayscaleLevelsRowOperation(
        Rectangle bounds,
        IMemoryOwner<int> histogramBuffer,
        Buffer2D<TPixel> source,
        int luminanceLevels)
    {
        this.bounds = bounds;
        this.histogramBuffer = histogramBuffer;
        this.source = source;
        this.luminanceLevels = luminanceLevels;
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void Invoke(int y)
    {
        ref int histogramBase = ref MemoryMarshal.GetReference(this.histogramBuffer.GetSpan());
        Span<TPixel> pixelRow = this.source.DangerousGetRowSpan(y);
        int levels = this.luminanceLevels;

        for (int x = 0; x < this.bounds.Width; x++)
        {
            // TODO: We should bulk convert here.
            var vector = pixelRow[x].ToVector4();
            int luminance = ColorNumerics.GetBT709Luminance(ref vector, levels);
            Interlocked.Increment(ref Unsafe.Add(ref histogramBase, luminance));
        }
    }
}
