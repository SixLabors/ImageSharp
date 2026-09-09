// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Components.Alpha;

/// <summary>
/// Supplies normalized auxiliary samples for the exact color region being converted.
/// </summary>
internal abstract class HeifAlphaRowSource : IDisposable
{
    /// <summary>
    /// Reads the next row in increasing source-row order.
    /// </summary>
    /// <param name="y">The row relative to the selected color region.</param>
    /// <returns>The normalized samples, valid until the next row is read.</returns>
    public abstract Span<float> ReadRow(int y);

    /// <inheritdoc/>
    public abstract void Dispose();
}

/// <summary>
/// Reads or resamples native auxiliary samples without packing them into image pixels.
/// </summary>
/// <typeparam name="TBuffer">The native component-plane view.</typeparam>
/// <typeparam name="TSample">The native unsigned sample type.</typeparam>
/// <typeparam name="TLoader">The sample widening operator.</typeparam>
internal sealed class HeifAlphaRowSource<TBuffer, TSample, TLoader> : HeifAlphaRowSource
    where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
    where TSample : unmanaged
    where TLoader : struct, IHeifSampleConverter<TSample>
{
    private readonly TBuffer buffer;
    private readonly HeifColorConversionParameters parameters;
    private readonly Rectangle window;
    private readonly IMemoryOwner<float> rowOwner;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifAlphaRowSource{TBuffer, TSample, TLoader}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing scratch storage.</param>
    /// <param name="buffer">The native auxiliary plane retained by the decoder.</param>
    /// <param name="parameters">The auxiliary sample range.</param>
    /// <param name="window">The exact color region within that extent.</param>
    public HeifAlphaRowSource(
        Configuration configuration,
        TBuffer buffer,
        in HeifColorConversionParameters parameters,
        Rectangle window)
    {
        this.buffer = buffer;
        this.parameters = parameters;
        this.window = window;
        this.rowOwner = configuration.MemoryAllocator.Allocate<float>(window.Width);
    }

    /// <inheritdoc/>
    public override Span<float> ReadRow(int y)
    {
        Span<float> row = this.rowOwner.GetSpan()[..this.window.Width];
        ReadOnlySpan<TSample> source = this.buffer.GetLumaRowSpan(this.window.Y + y).Slice(this.window.X, this.window.Width);
        HeifPlanarAlphaCompositor.NormalizeAlphaRow<TSample, TLoader>(source, row, in this.parameters);
        return row;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.rowOwner.Dispose();
    }
}
