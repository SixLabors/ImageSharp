// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Components;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Color;

/// <summary>
/// Exposes borrowed rows from an owned AV1 presentation buffer to the shared HEIF color converter.
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
/// <typeparam name="TBuffer">The reconstructed AV1 plane adapter owned by the presentation buffer.</typeparam>
internal readonly struct Av1PresentationSampleBufferView<TSample, TBuffer> : IHeifPlanarSampleBuffer<TSample>
    where TSample : unmanaged
    where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
{
    /// <summary>
    /// The owner that keeps all exposed rows alive.
    /// </summary>
    private readonly Av1PresentationSampleBuffer<TSample, TBuffer> owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PresentationSampleBufferView{TSample, TBuffer}"/> struct.
    /// </summary>
    /// <param name="owner">The scaled plane owner.</param>
    public Av1PresentationSampleBufferView(Av1PresentationSampleBuffer<TSample, TBuffer> owner) => this.owner = owner;

    /// <inheritdoc/>
    public int Width => this.owner.Width;

    /// <inheritdoc/>
    public int Height => this.owner.Height;

    /// <inheritdoc/>
    public int LumaBitDepth => this.owner.LumaBitDepth;

    /// <inheritdoc/>
    public int ChromaBitDepth => this.owner.ChromaBitDepth;

    /// <inheritdoc/>
    public bool IsMonochrome => this.owner.IsMonochrome;

    /// <inheritdoc/>
    public int ChromaSubsamplingX => this.owner.ChromaSubsamplingX;

    /// <inheritdoc/>
    public int ChromaSubsamplingY => this.owner.ChromaSubsamplingY;

    /// <inheritdoc/>
    public int ChromaPositionX => this.owner.ChromaPositionX;

    /// <inheritdoc/>
    public int ChromaPositionY => this.owner.ChromaPositionY;

    /// <inheritdoc/>
    public Span<TSample> GetLumaRowSpan(int row) => this.owner.GetRowSpan(Av1Plane.Y, row);

    /// <inheritdoc/>
    public Span<TSample> GetChromaBlueRowSpan(int row) => this.owner.GetRowSpan(Av1Plane.U, row);

    /// <inheritdoc/>
    public Span<TSample> GetChromaRedRowSpan(int row) => this.owner.GetRowSpan(Av1Plane.V, row);
}
