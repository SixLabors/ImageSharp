// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Owns the aligned luma and chroma planes used by one AV1 encoder frame.
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
internal sealed class Av1EncoderFrameBuffer<TSample> : IDisposable
    where TSample : unmanaged
{
    /// <summary>
    /// The complete frame owner, or <see langword="null"/> after disposal.
    /// </summary>
    private IMemoryOwner<TSample>? owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderFrameBuffer{TSample}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the frame allocator.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="bitDepth">The native component precision.</param>
    /// <param name="colorFormat">The native luma and chroma sampling layout.</param>
    /// <param name="chromaPositionX">The horizontal chroma position in half-luma-sample units.</param>
    /// <param name="chromaPositionY">The vertical chroma position in half-luma-sample units.</param>
    public Av1EncoderFrameBuffer(
        Configuration configuration,
        int width,
        int height,
        int bitDepth,
        Av1ColorFormat colorFormat,
        int chromaPositionX,
        int chromaPositionY)
    {
        int subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422 ? 1 : 0;
        int subsamplingY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
        Size codedSize = Av1EncoderFrame<TSample>.GetCodedSize(width, height);
        Size lumaSize = Av1EncoderFrame<TSample>.GetPlaneBufferSize(width, height, 0, 0);
        int lumaElementCount = checked(lumaSize.Width * lumaSize.Height);
        Size chromaSize = colorFormat == Av1ColorFormat.Yuv400
            ? Size.Empty
            : Av1EncoderFrame<TSample>.GetPlaneBufferSize(width, height, subsamplingX, subsamplingY);

        int chromaElementCount = checked(chromaSize.Width * chromaSize.Height);
        int planeAlignment = Math.Max(32 / Unsafe.SizeOf<TSample>(), 1);
        int chromaBlueOffset = Align(lumaElementCount, planeAlignment);
        int chromaRedOffset = Align(checked(chromaBlueOffset + chromaElementCount), planeAlignment);
        int storageLength = colorFormat == Av1ColorFormat.Yuv400
            ? lumaElementCount
            : checked(chromaRedOffset + chromaElementCount);

        // Libaom keeps the three component planes in one 32-byte-aligned frame allocation. The non-owning
        // Buffer2D views preserve ImageSharp's row API without introducing separate plane rents or copies.
        IMemoryOwner<TSample> owner = configuration.MemoryAllocator.Allocate<TSample>(storageLength);
        Memory<TSample> storage = owner.Memory;
        Buffer2D<TSample> luma = Buffer2D<TSample>.WrapMemory(
            storage[..lumaElementCount],
            lumaSize.Width,
            lumaSize.Height);

        this.Luma = luma;

        Buffer2DRegion<TSample> lumaRegion = luma.GetRegion(
            Av1EncoderFrame<TSample>.LumaBorder,
            Av1EncoderFrame<TSample>.LumaBorder,
            codedSize.Width,
            codedSize.Height);

        Buffer2DRegion<TSample> chromaBlueRegion = default;
        Buffer2DRegion<TSample> chromaRedRegion = default;
        if (colorFormat != Av1ColorFormat.Yuv400)
        {
            Buffer2D<TSample> chromaBlue = Buffer2D<TSample>.WrapMemory(
                storage.Slice(chromaBlueOffset, chromaElementCount),
                chromaSize.Width,
                chromaSize.Height);

            Buffer2D<TSample> chromaRed = Buffer2D<TSample>.WrapMemory(
                storage.Slice(chromaRedOffset, chromaElementCount),
                chromaSize.Width,
                chromaSize.Height);

            this.ChromaBlue = chromaBlue;
            this.ChromaRed = chromaRed;

            int chromaBorderX = Av1EncoderFrame<TSample>.LumaBorder >> subsamplingX;
            int chromaBorderY = Av1EncoderFrame<TSample>.LumaBorder >> subsamplingY;
            int codedChromaWidth = codedSize.Width >> subsamplingX;
            int codedChromaHeight = codedSize.Height >> subsamplingY;
            chromaBlueRegion = chromaBlue.GetRegion(
                chromaBorderX,
                chromaBorderY,
                codedChromaWidth,
                codedChromaHeight);

            chromaRedRegion = chromaRed.GetRegion(
                chromaBorderX,
                chromaBorderY,
                codedChromaWidth,
                codedChromaHeight);
        }

        this.owner = owner;
        this.Frame = new(
            lumaRegion,
            chromaBlueRegion,
            chromaRedRegion,
            width,
            height,
            bitDepth,
            colorFormat,
            chromaPositionX,
            chromaPositionY);
    }

    /// <summary>
    /// Gets the non-owning coded frame view.
    /// </summary>
    public Av1EncoderFrame<TSample> Frame { get; }

    /// <summary>
    /// Gets the complete padded luma plane.
    /// </summary>
    public Buffer2D<TSample> Luma { get; }

    /// <summary>
    /// Gets the complete padded blue-difference chroma plane.
    /// </summary>
    public Buffer2D<TSample>? ChromaBlue { get; }

    /// <summary>
    /// Gets the complete padded red-difference chroma plane.
    /// </summary>
    public Buffer2D<TSample>? ChromaRed { get; }

    /// <summary>
    /// Releases the complete frame allocation.
    /// </summary>
    public void Dispose()
    {
        IMemoryOwner<TSample>? ownedMemory = this.owner;
        this.owner = null;
        if (ownedMemory is null)
        {
            return;
        }

        this.Luma.Dispose();
        this.ChromaBlue?.Dispose();
        this.ChromaRed?.Dispose();
        ownedMemory.Dispose();
    }

    /// <summary>
    /// Aligns an element offset to the next component-plane boundary.
    /// </summary>
    private static int Align(int value, int alignment)
        => checked((value + alignment - 1) & -alignment);
}
