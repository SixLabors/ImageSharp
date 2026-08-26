// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Components;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc.Color;

/// <summary>
/// Adapts reconstructed HEVC planes to the shared HEIF planar color pipeline.
/// </summary>
internal struct HevcPlanarSampleBuffer : IHeifPlanarSampleBuffer<ushort>
{
    /// <summary>
    /// The reconstructed HEVC picture containing the component planes.
    /// </summary>
    private readonly HevcPictureBuffer picture;

    /// <summary>
    /// The progressive-frame 4:2:0 chroma sample location.
    /// </summary>
    private readonly HevcChromaSampleLocation chromaSampleLocation;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcPlanarSampleBuffer"/> struct.
    /// </summary>
    /// <param name="picture">The reconstructed HEVC picture.</param>
    /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
    public HevcPlanarSampleBuffer(HevcPictureBuffer picture, HevcChromaSampleLocation chromaSampleLocation)
    {
        this.picture = picture;
        this.chromaSampleLocation = chromaSampleLocation;
    }

    /// <summary>
    /// Gets the horizontal offset in half-luma-sample units for each HEVC 4:2:0 chroma-location code.
    /// </summary>
    private static ReadOnlySpan<byte> ChromaLocationX => [0, 1, 0, 1, 0, 1];

    /// <summary>
    /// Gets the vertical offset in half-luma-sample units for each HEVC 4:2:0 chroma-location code.
    /// </summary>
    private static ReadOnlySpan<byte> ChromaLocationY => [1, 1, 0, 0, 2, 2];

    /// <inheritdoc/>
    public readonly int Width => this.picture.Width;

    /// <inheritdoc/>
    public readonly int Height => this.picture.Height;

    /// <inheritdoc/>
    public readonly int LumaBitDepth => this.picture.BitDepthLuma;

    /// <inheritdoc/>
    public readonly int ChromaBitDepth => this.picture.BitDepthChroma;

    /// <inheritdoc/>
    public readonly bool IsMonochrome => this.picture.ChromaFormat == 0;

    /// <inheritdoc/>
    public readonly int ChromaSubsamplingX => this.picture.GetSubsamplingX(HevcPlane.Cb);

    /// <inheritdoc/>
    public readonly int ChromaSubsamplingY => this.picture.GetSubsamplingY(HevcPlane.Cb);

    /// <inheritdoc/>
    public readonly int ChromaPositionX
        => this.picture.ChromaFormat == 1 && !this.picture.SeparateColorPlane ? ChromaLocationX[(int)this.chromaSampleLocation] : 0;

    /// <inheritdoc/>
    public readonly int ChromaPositionY
        => this.picture.ChromaFormat == 1 && !this.picture.SeparateColorPlane ? ChromaLocationY[(int)this.chromaSampleLocation] : 0;

    /// <inheritdoc/>
    public Span<ushort> GetLumaRowSpan(int row) => this.picture.GetRowSpan(HevcPlane.Y, row);

    /// <inheritdoc/>
    public Span<ushort> GetChromaBlueRowSpan(int row) => this.picture.GetRowSpan(HevcPlane.Cb, row);

    /// <inheritdoc/>
    public Span<ushort> GetChromaRedRowSpan(int row) => this.picture.GetRowSpan(HevcPlane.Cr, row);
}
