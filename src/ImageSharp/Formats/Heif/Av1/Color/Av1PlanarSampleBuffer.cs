// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Color;

/// <summary>
/// Adapts reconstructed AV1 planes to the shared HEIF planar color pipeline.
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
internal struct Av1PlanarSampleBuffer<TSample> : IHeifPlanarSampleBuffer<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// The reconstructed AV1 frame containing the component planes.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The visible luma plane in byte-backed storage.
    /// </summary>
    private readonly Buffer2DRegion<byte> luma;

    /// <summary>
    /// The visible blue-difference plane in byte-backed storage.
    /// </summary>
    private readonly Buffer2DRegion<byte> chromaBlue;

    /// <summary>
    /// The visible red-difference plane in byte-backed storage.
    /// </summary>
    private readonly Buffer2DRegion<byte> chromaRed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PlanarSampleBuffer{TSample}"/> struct.
    /// </summary>
    /// <param name="frameBuffer">The reconstructed AV1 frame.</param>
    public Av1PlanarSampleBuffer(Av1FrameBuffer<byte> frameBuffer)
    {
        this.frameBuffer = frameBuffer;
        this.luma = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        this.chromaBlue = this.IsMonochrome
            ? default
            : frameBuffer.DeriveBlockPointer(Av1Plane.U, this.ChromaSubsamplingX, this.ChromaSubsamplingY);

        this.chromaRed = this.IsMonochrome
            ? default
            : frameBuffer.DeriveBlockPointer(Av1Plane.V, this.ChromaSubsamplingX, this.ChromaSubsamplingY);
    }

    /// <inheritdoc/>
    public readonly int Width => this.frameBuffer.Width;

    /// <inheritdoc/>
    public readonly int Height => this.frameBuffer.Height;

    /// <inheritdoc/>
    public readonly int LumaBitDepth => this.frameBuffer.BitDepth.GetBitCount();

    /// <inheritdoc/>
    public readonly int ChromaBitDepth => this.frameBuffer.BitDepth.GetBitCount();

    /// <inheritdoc/>
    public readonly bool IsMonochrome => this.frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;

    /// <inheritdoc/>
    public readonly int ChromaSubsamplingX => this.frameBuffer.ColorConfig.SubSamplingX ? 1 : 0;

    /// <inheritdoc/>
    public readonly int ChromaSubsamplingY => this.frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;

    /// <inheritdoc/>
    public readonly int ChromaPositionX
    {
        get
        {
            if (this.ChromaSubsamplingX == 0)
            {
                return 0;
            }

            // AV1 4:2:2 chroma is centered horizontally. For 4:2:0, CSP_UNKNOWN is centered while the two
            // explicitly positioned layouts are co-sited with the left luma sample.
            bool isCentered = this.ChromaSubsamplingY == 0
                || this.frameBuffer.ColorConfig.ChromaSamplePosition == ObuChromoSamplePosition.Unknown;

            return isCentered ? 1 : 0;
        }
    }

    /// <inheritdoc/>
    public readonly int ChromaPositionY
        => this.ChromaSubsamplingY != 0 && this.frameBuffer.ColorConfig.ChromaSamplePosition != ObuChromoSamplePosition.Colocated ? 1 : 0;

    /// <inheritdoc/>
    public Span<TSample> GetLumaRowSpan(int row)
    {
        if (typeof(TSample) == typeof(byte))
        {
            return MemoryMarshal.Cast<byte, TSample>(this.luma.DangerousGetRowSpan(row));
        }

        return MemoryMarshal.Cast<ushort, TSample>(this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, row, 0, 0));
    }

    /// <inheritdoc/>
    public Span<TSample> GetChromaBlueRowSpan(int row)
    {
        if (typeof(TSample) == typeof(byte))
        {
            return MemoryMarshal.Cast<byte, TSample>(this.chromaBlue.DangerousGetRowSpan(row));
        }

        return MemoryMarshal.Cast<ushort, TSample>(
            this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, row, this.ChromaSubsamplingX, this.ChromaSubsamplingY));
    }

    /// <inheritdoc/>
    public Span<TSample> GetChromaRedRowSpan(int row)
    {
        if (typeof(TSample) == typeof(byte))
        {
            return MemoryMarshal.Cast<byte, TSample>(this.chromaRed.DangerousGetRowSpan(row));
        }

        return MemoryMarshal.Cast<ushort, TSample>(
            this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, row, this.ChromaSubsamplingX, this.ChromaSubsamplingY));
    }
}
