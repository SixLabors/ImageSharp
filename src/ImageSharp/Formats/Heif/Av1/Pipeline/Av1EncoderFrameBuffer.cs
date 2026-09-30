// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Owns the aligned luma and chroma planes used by one AV1 encoder frame.
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
internal sealed class Av1EncoderFrameBuffer<TSample> : IDisposable
    where TSample : unmanaged
{
    /// <summary>
    /// The byte boundary used for SIMD-accessible component planes.
    /// </summary>
    private const int PlaneAlignmentBytes = 32;

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
    /// <param name="lumaBorder">The border width and height in luma samples.</param>
    public Av1EncoderFrameBuffer(
        Configuration configuration,
        int width,
        int height,
        int bitDepth,
        Av1ColorFormat colorFormat,
        int chromaPositionX,
        int chromaPositionY,
        int lumaBorder)
    {
        int subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422 ? 1 : 0;
        int subsamplingY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
        Size codedSize = Av1EncoderFrame<TSample>.GetCodedSize(width, height);
        Size lumaSize = Av1EncoderFrame<TSample>.GetPlaneBufferSize(width, height, 0, 0, lumaBorder);
        int lumaElementCount = checked(lumaSize.Width * lumaSize.Height);
        Size chromaSize = colorFormat == Av1ColorFormat.Yuv400
            ? Size.Empty
            : Av1EncoderFrame<TSample>.GetPlaneBufferSize(width, height, subsamplingX, subsamplingY, lumaBorder);

        int chromaElementCount = checked(chromaSize.Width * chromaSize.Height);
        int planeAlignment = Math.Max(PlaneAlignmentBytes / Unsafe.SizeOf<TSample>(), 1);
        int chromaBlueOffset = Align(lumaElementCount, planeAlignment);
        int chromaRedOffset = Align(checked(chromaBlueOffset + chromaElementCount), planeAlignment);
        int storageLength = colorFormat == Av1ColorFormat.Yuv400
            ? lumaElementCount
            : checked(chromaRedOffset + chromaElementCount);

        // Component planes share one contiguous frame allocation; their offsets preserve the 32-byte plane
        // alignment. Each plane is a slice of it, so a kernel addresses the whole bordered plane with its stride.
        IMemoryOwner<TSample> owner = configuration.MemoryAllocator.Allocate<TSample>(storageLength);
        Memory<TSample> storage = owner.Memory;
        Av1PlaneRegion<TSample> lumaRegion = new(
            storage[..lumaElementCount],
            lumaSize.Width,
            new Rectangle(lumaBorder, lumaBorder, codedSize.Width, codedSize.Height));

        Av1PlaneRegion<TSample> chromaBlueRegion = default;
        Av1PlaneRegion<TSample> chromaRedRegion = default;
        if (colorFormat != Av1ColorFormat.Yuv400)
        {
            Rectangle chromaBounds = new(
                lumaBorder >> subsamplingX,
                lumaBorder >> subsamplingY,
                codedSize.Width >> subsamplingX,
                codedSize.Height >> subsamplingY);

            chromaBlueRegion = new(storage.Slice(chromaBlueOffset, chromaElementCount), chromaSize.Width, chromaBounds);
            chromaRedRegion = new(storage.Slice(chromaRedOffset, chromaElementCount), chromaSize.Width, chromaBounds);
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
    public Av1PlaneRegion<TSample> Luma => this.Frame.CodedView.GetPlane(Av1Plane.Y).GetFullPlane();

    /// <summary>
    /// Gets the complete padded blue-difference plane, or the default region for a monochrome frame.
    /// </summary>
    public Av1PlaneRegion<TSample> ChromaBlue => this.Frame.IsMonochrome ? default : this.Frame.CodedView.GetPlane(Av1Plane.U).GetFullPlane();

    /// <summary>
    /// Gets the complete padded red-difference plane, or the default region for a monochrome frame.
    /// </summary>
    public Av1PlaneRegion<TSample> ChromaRed => this.Frame.IsMonochrome ? default : this.Frame.CodedView.GetPlane(Av1Plane.V).GetFullPlane();

    /// <summary>
    /// Releases the complete frame allocation.
    /// </summary>
    public void Dispose()
    {
        this.owner?.Dispose();
        this.owner = null;
    }

    /// <summary>
    /// Aligns an element offset to the next component-plane boundary.
    /// </summary>
    private static int Align(int value, int alignment)
        => checked((value + alignment - 1) & -alignment);
}
