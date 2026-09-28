// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// A <see langword="struct"/> implementing the grayscale levels logic as <see cref="IRowOperation{Vector4}"/>.
/// </summary>
internal readonly struct GrayscaleLevelsRowOperation<TPixel> : IRowOperation<Vector4>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly Rectangle bounds;
    private readonly IMemoryOwner<int> histogramBuffer;
    private readonly Buffer2D<TPixel> source;
    private readonly int luminanceLevels;

    [MethodImpl(InliningOptions.ShortMethod)]
    public GrayscaleLevelsRowOperation(
        Configuration configuration,
        Rectangle bounds,
        IMemoryOwner<int> histogramBuffer,
        Buffer2D<TPixel> source,
        int luminanceLevels)
    {
        this.configuration = configuration;
        this.bounds = bounds;
        this.histogramBuffer = histogramBuffer;
        this.source = source;
        this.luminanceLevels = luminanceLevels;
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public int GetRequiredBufferLength(Rectangle bounds) => bounds.Width;

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void Invoke(int y, Span<Vector4> span)
    {
        Span<Vector4> vectorBuffer = span.Slice(0, this.bounds.Width);
        ref Vector4 vectorRef = ref MemoryMarshal.GetReference(vectorBuffer);
        ref int histogramBase = ref MemoryMarshal.GetReference(this.histogramBuffer.GetSpan());
        int levels = this.luminanceLevels;

        Span<TPixel> pixelRow = this.source.DangerousGetRowSpan(y).Slice(this.bounds.X, this.bounds.Width);
        PixelOperations<TPixel>.Instance.ToVector4(this.configuration, pixelRow, vectorBuffer, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

        for (int x = 0; x < this.bounds.Width; x++)
        {
            Vector4 vector = Unsafe.Add(ref vectorRef, (uint)x);
            int luminance = GetLuminanceIndex(vector, levels);
            Interlocked.Increment(ref Unsafe.Add(ref histogramBase, (uint)luminance));
        }
    }

    /// <summary>
    /// Gets a histogram index from BT.709 luminance, saturating out-of-range values and mapping NaN to zero.
    /// </summary>
    /// <param name="vector">The source color.</param>
    /// <param name="levels">The number of histogram bins.</param>
    /// <returns>The bounded histogram index.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int GetLuminanceIndex(Vector4 vector, int levels)
    {
        // A floating-point pixel can exceed the histogram range or produce NaN.
        float luminance = Numerics.Clamp(ColorNumerics.GetBT709Luminance(vector), 0F, 1F);
        return (int)MathF.Round(luminance * (levels - 1));
    }
}
