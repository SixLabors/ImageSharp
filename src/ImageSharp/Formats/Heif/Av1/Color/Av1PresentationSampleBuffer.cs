// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Color;

/// <summary>
/// Owns AV1 component planes scaled to an image item's presentation extent.
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
/// <typeparam name="TBuffer">The reconstructed AV1 plane adapter.</typeparam>
internal sealed class Av1PresentationSampleBuffer<TSample, TBuffer> : IDisposable
    where TSample : unmanaged
    where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
{
    /// <summary>
    /// The allocator that owns the presentation planes and row workspace.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The complete set of owned presentation planes, or <see langword="null"/> after disposal.
    /// </summary>
    private PresentationPlanes? planes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PresentationSampleBuffer{TSample, TBuffer}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing allocator-owned plane storage.</param>
    /// <param name="source">The unscaled reconstructed AV1 planes.</param>
    /// <param name="width">The presented luma width.</param>
    /// <param name="height">The presented luma height.</param>
    public Av1PresentationSampleBuffer(Configuration configuration, TBuffer source, int width, int height)
    {
        this.memoryAllocator = configuration.MemoryAllocator;
        this.Width = width;
        this.Height = height;
        this.LumaBitDepth = source.LumaBitDepth;
        this.ChromaBitDepth = source.ChromaBitDepth;
        this.IsMonochrome = source.IsMonochrome;
        this.ChromaSubsamplingX = source.ChromaSubsamplingX;
        this.ChromaSubsamplingY = source.ChromaSubsamplingY;
        this.ChromaPositionX = source.ChromaPositionX;
        this.ChromaPositionY = source.ChromaPositionY;

        int sourceChromaWidth = DivideCeiling(source.Width, 1 << source.ChromaSubsamplingX);
        int sourceChromaHeight = DivideCeiling(source.Height, 1 << source.ChromaSubsamplingY);
        int destinationChromaWidth = DivideCeiling(width, 1 << source.ChromaSubsamplingX);
        int destinationChromaHeight = DivideCeiling(height, 1 << source.ChromaSubsamplingY);

        Buffer2D<TSample>? luma = null;
        Buffer2D<TSample>? chromaBlue = null;
        Buffer2D<TSample>? chromaRed = null;
        try
        {
            luma = this.memoryAllocator.Allocate2D<TSample>(width, height);
            this.ScalePlane(source, Av1Plane.Y, source.Width, source.Height, luma);

            ChromaPlanes? chroma = null;

            if (!source.IsMonochrome)
            {
                chromaBlue = this.memoryAllocator.Allocate2D<TSample>(destinationChromaWidth, destinationChromaHeight);
                this.ScalePlane(source, Av1Plane.U, sourceChromaWidth, sourceChromaHeight, chromaBlue);

                chromaRed = this.memoryAllocator.Allocate2D<TSample>(destinationChromaWidth, destinationChromaHeight);
                this.ScalePlane(source, Av1Plane.V, sourceChromaWidth, sourceChromaHeight, chromaRed);
                chroma = new ChromaPlanes(chromaBlue, chromaRed);
            }

            // Publish ownership only after every required plane has been allocated and initialized.
            this.planes = new PresentationPlanes(luma, chroma);
        }
        catch
        {
            luma?.Dispose();
            chromaBlue?.Dispose();
            chromaRed?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the presented luma width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the presented luma height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the luma sample bit depth.
    /// </summary>
    public int LumaBitDepth { get; }

    /// <summary>
    /// Gets the chroma sample bit depth.
    /// </summary>
    public int ChromaBitDepth { get; }

    /// <summary>
    /// Gets a value indicating whether only luma is present.
    /// </summary>
    public bool IsMonochrome { get; }

    /// <summary>
    /// Gets the horizontal chroma-subsampling shift.
    /// </summary>
    public int ChromaSubsamplingX { get; }

    /// <summary>
    /// Gets the vertical chroma-subsampling shift.
    /// </summary>
    public int ChromaSubsamplingY { get; }

    /// <summary>
    /// Gets the horizontal chroma position in half-luma-sample units.
    /// </summary>
    public int ChromaPositionX { get; }

    /// <summary>
    /// Gets the vertical chroma position in half-luma-sample units.
    /// </summary>
    public int ChromaPositionY { get; }

    /// <summary>
    /// Gets a borrowed adapter over the scaled planes.
    /// </summary>
    public Av1PresentationSampleBufferView<TSample, TBuffer> View => new(this);

    /// <summary>
    /// Releases the scaled planes.
    /// </summary>
    public void Dispose()
    {
        PresentationPlanes? planes = this.planes;
        this.planes = null;

        if (planes is null)
        {
            return;
        }

        planes.Value.Luma.Dispose();
        ChromaPlanes? chroma = planes.Value.Chroma;
        if (chroma is not null)
        {
            chroma.Value.Blue.Dispose();
            chroma.Value.Red.Dispose();
        }
    }

    /// <summary>
    /// Gets one scaled component row.
    /// </summary>
    /// <param name="plane">The requested component plane.</param>
    /// <param name="row">The zero-based plane row.</param>
    /// <returns>The visible samples in the requested row.</returns>
    public Span<TSample> GetRowSpan(Av1Plane plane, int row)
    {
        PresentationPlanes planes = this.planes
            ?? throw new ObjectDisposedException(nameof(Av1PresentationSampleBuffer<TSample, TBuffer>));

        Buffer2D<TSample> buffer = plane switch
        {
            Av1Plane.Y => planes.Luma,
            Av1Plane.U => planes.Chroma?.Blue
                ?? throw new InvalidOperationException("The AV1 presentation buffer has no blue-difference plane."),
            _ => planes.Chroma?.Red
                ?? throw new InvalidOperationException("The AV1 presentation buffer has no red-difference plane.")
        };

        return buffer.DangerousGetRowSpan(row);
    }

    /// <summary>
    /// Scales one component plane with the native integer filter used by pinned libavif's libyuv backend.
    /// </summary>
    /// <param name="source">The reconstructed component planes.</param>
    /// <param name="plane">The component plane to scale.</param>
    /// <param name="sourceWidth">The source plane width.</param>
    /// <param name="sourceHeight">The source plane height.</param>
    /// <param name="destination">The scaled destination plane.</param>
    private void ScalePlane(
        TBuffer source,
        Av1Plane plane,
        int sourceWidth,
        int sourceHeight,
        Buffer2D<TSample> destination)
    {
        int destinationWidth = destination.Width;
        int destinationHeight = destination.Height;
        if (sourceWidth == destinationWidth && sourceHeight == destinationHeight)
        {
            for (int y = 0; y < sourceHeight; y++)
            {
                GetSourceRow(source, plane, y)[..sourceWidth].CopyTo(destination.DangerousGetRowSpan(y));
            }

            return;
        }

        bool doublesWidth = (destinationWidth + 1) / 2 == sourceWidth;
        bool doublesHeight = (destinationHeight + 1) / 2 == sourceHeight;
        if (doublesWidth && doublesHeight)
        {
            ScaleUp2(source, plane, sourceWidth, sourceHeight, destination);
            return;
        }

        if (doublesWidth && sourceHeight == destinationHeight)
        {
            for (int y = 0; y < sourceHeight; y++)
            {
                ScaleRowUp2Linear(
                    GetSourceRow(source, plane, y)[..sourceWidth],
                    destination.DangerousGetRowSpan(y));
            }

            return;
        }

        if (sourceHeight == destinationHeight)
        {
            int rowHorizontalStep = sourceWidth > 1 && destinationWidth > 1
                ? FixedDivideOne(sourceWidth, destinationWidth)
                : 0;

            for (int y = 0; y < sourceHeight; y++)
            {
                ScaleHorizontal(
                    GetSourceRow(source, plane, y)[..sourceWidth],
                    destination.DangerousGetRowSpan(y),
                    rowHorizontalStep);
            }

            return;
        }

        // Layer selection presents a lower spatial layer at the full item extent, so both dimensions are monotonic.
        // The general libyuv path maps destination centers in 16.16 fixed point and retains only two horizontally
        // filtered rows. This avoids a second full-plane intermediate and remains group-safe under small allocators.
        using Buffer2D<TSample> horizontalRows = this.memoryAllocator.Allocate2D<TSample>(destinationWidth, 2);
        int horizontalStep = sourceWidth > 1 && destinationWidth > 1
            ? FixedDivideOne(sourceWidth, destinationWidth)
            : 0;

        int verticalStep = sourceHeight > 1 && destinationHeight > 1
            ? FixedDivideOne(sourceHeight, destinationHeight)
            : 0;

        int sourcePositionY = 0;
        int firstSourceRow = -1;
        int secondSourceRow = -1;
        int firstSlot = 0;
        int secondSlot = 1;
        for (int y = 0; y < destinationHeight; y++)
        {
            int sourceRow = sourcePositionY >> 16;
            int nextSourceRow = Math.Min(sourceRow + 1, sourceHeight - 1);
            if (sourceRow == secondSourceRow)
            {
                (firstSourceRow, secondSourceRow) = (secondSourceRow, firstSourceRow);
                (firstSlot, secondSlot) = (secondSlot, firstSlot);
            }

            if (firstSourceRow != sourceRow)
            {
                ScaleHorizontal(
                    GetSourceRow(source, plane, sourceRow)[..sourceWidth],
                    horizontalRows.DangerousGetRowSpan(firstSlot),
                    horizontalStep);

                firstSourceRow = sourceRow;
            }

            if (secondSourceRow != nextSourceRow)
            {
                ScaleHorizontal(
                    GetSourceRow(source, plane, nextSourceRow)[..sourceWidth],
                    horizontalRows.DangerousGetRowSpan(secondSlot),
                    horizontalStep);

                secondSourceRow = nextSourceRow;
            }

            int verticalFraction = (sourcePositionY >> 8) & 255;
            InterpolateRows(
                horizontalRows.DangerousGetRowSpan(firstSlot),
                horizontalRows.DangerousGetRowSpan(secondSlot),
                destination.DangerousGetRowSpan(y),
                verticalFraction);

            sourcePositionY += verticalStep;
        }
    }

    /// <summary>
    /// Applies libyuv's edge-aware two-times bilinear kernel to one complete plane.
    /// </summary>
    /// <param name="source">The reconstructed component planes.</param>
    /// <param name="plane">The component plane to scale.</param>
    /// <param name="sourceWidth">The source plane width.</param>
    /// <param name="sourceHeight">The source plane height.</param>
    /// <param name="destination">The scaled destination plane.</param>
    private static void ScaleUp2(
        TBuffer source,
        Av1Plane plane,
        int sourceWidth,
        int sourceHeight,
        Buffer2D<TSample> destination)
    {
        Span<TSample> firstSource = GetSourceRow(source, plane, 0)[..sourceWidth];
        Span<TSample> firstDestination = destination.DangerousGetRowSpan(0);
        ScaleRowUp2Bilinear(firstSource, firstSource, firstDestination, firstDestination);

        int destinationRow = 1;
        for (int y = 0; y < sourceHeight - 1; y++)
        {
            ScaleRowUp2Bilinear(
                GetSourceRow(source, plane, y)[..sourceWidth],
                GetSourceRow(source, plane, y + 1)[..sourceWidth],
                destination.DangerousGetRowSpan(destinationRow),
                destination.DangerousGetRowSpan(destinationRow + 1));

            destinationRow += 2;
        }

        if ((destination.Height & 1) == 0)
        {
            Span<TSample> lastSource = GetSourceRow(source, plane, sourceHeight - 1)[..sourceWidth];
            Span<TSample> lastDestination = destination.DangerousGetRowSpan(destination.Height - 1);
            ScaleRowUp2Bilinear(lastSource, lastSource, lastDestination, lastDestination);
        }
    }

    /// <summary>
    /// Gets one visible source row without boxing the codec adapter.
    /// </summary>
    /// <param name="source">The reconstructed component planes.</param>
    /// <param name="plane">The requested component plane.</param>
    /// <param name="row">The zero-based plane row.</param>
    /// <returns>The source row.</returns>
    private static Span<TSample> GetSourceRow(TBuffer source, Av1Plane plane, int row)
        => plane switch
        {
            Av1Plane.Y => source.GetLumaRowSpan(row),
            Av1Plane.U => source.GetChromaBlueRowSpan(row),
            _ => source.GetChromaRedRowSpan(row)
        };

    /// <summary>
    /// Applies the edge-aware two-times bilinear row kernel.
    /// </summary>
    /// <param name="topSource">The upper source row.</param>
    /// <param name="bottomSource">The lower source row.</param>
    /// <param name="topDestination">The upper destination row.</param>
    /// <param name="bottomDestination">The lower destination row.</param>
    private static void ScaleRowUp2Bilinear(
        ReadOnlySpan<TSample> topSource,
        ReadOnlySpan<TSample> bottomSource,
        Span<TSample> topDestination,
        Span<TSample> bottomDestination)
    {
        if (typeof(TSample) == typeof(byte))
        {
            ScaleRowUp2BilinearByte(
                MemoryMarshal.Cast<TSample, byte>(topSource),
                MemoryMarshal.Cast<TSample, byte>(bottomSource),
                MemoryMarshal.Cast<TSample, byte>(topDestination),
                MemoryMarshal.Cast<TSample, byte>(bottomDestination));

            return;
        }

        ScaleRowUp2BilinearUInt16(
            MemoryMarshal.Cast<TSample, ushort>(topSource),
            MemoryMarshal.Cast<TSample, ushort>(bottomSource),
            MemoryMarshal.Cast<TSample, ushort>(topDestination),
            MemoryMarshal.Cast<TSample, ushort>(bottomDestination));
    }

    /// <summary>
    /// Applies the byte two-times bilinear row kernel through portable 128-bit lanes and a scalar tail.
    /// </summary>
    /// <param name="topSource">The upper source row.</param>
    /// <param name="bottomSource">The lower source row.</param>
    /// <param name="topDestination">The upper destination row.</param>
    /// <param name="bottomDestination">The lower destination row.</param>
    private static void ScaleRowUp2BilinearByte(
        ReadOnlySpan<byte> topSource,
        ReadOnlySpan<byte> bottomSource,
        Span<byte> topDestination,
        Span<byte> bottomDestination)
    {
        int lastSource = topSource.Length - 1;
        topDestination[0] = (byte)(((3 * topSource[0]) + bottomSource[0] + 2) >> 2);
        bottomDestination[0] = (byte)((topSource[0] + (3 * bottomSource[0]) + 2) >> 2);

        int x = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte topSourceBase = ref MemoryMarshal.GetReference(topSource);
            ref byte bottomSourceBase = ref MemoryMarshal.GetReference(bottomSource);
            ref byte topDestinationBase = ref MemoryMarshal.GetReference(topDestination);
            ref byte bottomDestinationBase = ref MemoryMarshal.GetReference(bottomDestination);
            for (; x + 8 <= lastSource; x += 8)
            {
                Vector128<ushort> top0 = LoadEightBytes(ref topSourceBase, x);
                Vector128<ushort> top1 = LoadEightBytes(ref topSourceBase, x + 1);
                Vector128<ushort> bottom0 = LoadEightBytes(ref bottomSourceBase, x);
                Vector128<ushort> bottom1 = LoadEightBytes(ref bottomSourceBase, x + 1);
                CalculateBilinearPairs(
                    top0,
                    top1,
                    bottom0,
                    bottom1,
                    out Vector128<ushort> upperEven,
                    out Vector128<ushort> upperOdd,
                    out Vector128<ushort> lowerEven,
                    out Vector128<ushort> lowerOdd);

                StoreInterleavedBytes(upperEven, upperOdd, ref topDestinationBase, 1 + (2 * x));
                StoreInterleavedBytes(lowerEven, lowerOdd, ref bottomDestinationBase, 1 + (2 * x));
            }
        }

        for (; x < lastSource; x++)
        {
            int top0 = topSource[x];
            int top1 = topSource[x + 1];
            int bottom0 = bottomSource[x];
            int bottom1 = bottomSource[x + 1];
            int destination = 1 + (2 * x);
            topDestination[destination] = (byte)(((9 * top0) + (3 * top1) + (3 * bottom0) + bottom1 + 8) >> 4);
            topDestination[destination + 1] = (byte)(((3 * top0) + (9 * top1) + bottom0 + (3 * bottom1) + 8) >> 4);
            bottomDestination[destination] = (byte)(((3 * top0) + top1 + (9 * bottom0) + (3 * bottom1) + 8) >> 4);
            bottomDestination[destination + 1] = (byte)((top0 + (3 * top1) + (3 * bottom0) + (9 * bottom1) + 8) >> 4);
        }

        int lastDestination = topDestination.Length - 1;
        topDestination[lastDestination] = (byte)(((3 * topSource[lastSource]) + bottomSource[lastSource] + 2) >> 2);
        bottomDestination[lastDestination] = (byte)((topSource[lastSource] + (3 * bottomSource[lastSource]) + 2) >> 2);
    }

    /// <summary>
    /// Applies the unsigned 16-bit two-times bilinear row kernel through portable 128-bit lanes and a scalar tail.
    /// </summary>
    /// <param name="topSource">The upper source row.</param>
    /// <param name="bottomSource">The lower source row.</param>
    /// <param name="topDestination">The upper destination row.</param>
    /// <param name="bottomDestination">The lower destination row.</param>
    private static void ScaleRowUp2BilinearUInt16(
        ReadOnlySpan<ushort> topSource,
        ReadOnlySpan<ushort> bottomSource,
        Span<ushort> topDestination,
        Span<ushort> bottomDestination)
    {
        int lastSource = topSource.Length - 1;
        topDestination[0] = (ushort)(((3 * topSource[0]) + bottomSource[0] + 2) >> 2);
        bottomDestination[0] = (ushort)((topSource[0] + (3 * bottomSource[0]) + 2) >> 2);

        int x = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref ushort topSourceBase = ref MemoryMarshal.GetReference(topSource);
            ref ushort bottomSourceBase = ref MemoryMarshal.GetReference(bottomSource);
            ref ushort topDestinationBase = ref MemoryMarshal.GetReference(topDestination);
            ref ushort bottomDestinationBase = ref MemoryMarshal.GetReference(bottomDestination);
            for (; x + Vector128<ushort>.Count <= lastSource; x += Vector128<ushort>.Count)
            {
                Vector128<ushort> top0 = Vector128.LoadUnsafe(ref topSourceBase, (nuint)x);
                Vector128<ushort> top1 = Vector128.LoadUnsafe(ref topSourceBase, (nuint)(x + 1));
                Vector128<ushort> bottom0 = Vector128.LoadUnsafe(ref bottomSourceBase, (nuint)x);
                Vector128<ushort> bottom1 = Vector128.LoadUnsafe(ref bottomSourceBase, (nuint)(x + 1));
                CalculateBilinearPairs(
                    top0,
                    top1,
                    bottom0,
                    bottom1,
                    out Vector128<ushort> upperEven,
                    out Vector128<ushort> upperOdd,
                    out Vector128<ushort> lowerEven,
                    out Vector128<ushort> lowerOdd);

                StoreInterleavedUInt16(upperEven, upperOdd, ref topDestinationBase, 1 + (2 * x));
                StoreInterleavedUInt16(lowerEven, lowerOdd, ref bottomDestinationBase, 1 + (2 * x));
            }
        }

        for (; x < lastSource; x++)
        {
            int top0 = topSource[x];
            int top1 = topSource[x + 1];
            int bottom0 = bottomSource[x];
            int bottom1 = bottomSource[x + 1];
            int destination = 1 + (2 * x);
            topDestination[destination] = (ushort)(((9 * top0) + (3 * top1) + (3 * bottom0) + bottom1 + 8) >> 4);
            topDestination[destination + 1] = (ushort)(((3 * top0) + (9 * top1) + bottom0 + (3 * bottom1) + 8) >> 4);
            bottomDestination[destination] = (ushort)(((3 * top0) + top1 + (9 * bottom0) + (3 * bottom1) + 8) >> 4);
            bottomDestination[destination + 1] = (ushort)((top0 + (3 * top1) + (3 * bottom0) + (9 * bottom1) + 8) >> 4);
        }

        int lastDestination = topDestination.Length - 1;
        topDestination[lastDestination] = (ushort)(((3 * topSource[lastSource]) + bottomSource[lastSource] + 2) >> 2);
        bottomDestination[lastDestination] = (ushort)((topSource[lastSource] + (3 * bottomSource[lastSource]) + 2) >> 2);
    }

    /// <summary>
    /// Calculates the four interleaved bilinear products for eight source positions.
    /// </summary>
    /// <param name="top0">The upper-left samples.</param>
    /// <param name="top1">The upper-right samples.</param>
    /// <param name="bottom0">The lower-left samples.</param>
    /// <param name="bottom1">The lower-right samples.</param>
    /// <param name="upperEven">Receives the upper left-biased samples.</param>
    /// <param name="upperOdd">Receives the upper right-biased samples.</param>
    /// <param name="lowerEven">Receives the lower left-biased samples.</param>
    /// <param name="lowerOdd">Receives the lower right-biased samples.</param>
    private static void CalculateBilinearPairs(
        Vector128<ushort> top0,
        Vector128<ushort> top1,
        Vector128<ushort> bottom0,
        Vector128<ushort> bottom1,
        out Vector128<ushort> upperEven,
        out Vector128<ushort> upperOdd,
        out Vector128<ushort> lowerEven,
        out Vector128<ushort> lowerOdd)
    {
        Vector128<ushort> rounding = Vector128.Create((ushort)8);

        // The largest twelve-bit weighted sum is 16 * 4095 + 8, which remains within unsigned 16-bit lanes.
        // Keeping eight independent source positions per vector therefore avoids widening and preserves libyuv's
        // exact add-before-shift rounding for both byte and high-bit-depth presentation planes.
        upperEven = (((top0 << 3) + top0) + ((top1 << 1) + top1) + ((bottom0 << 1) + bottom0) + bottom1 + rounding) >> 4;
        upperOdd = (((top0 << 1) + top0) + ((top1 << 3) + top1) + bottom0 + ((bottom1 << 1) + bottom1) + rounding) >> 4;
        lowerEven = (((top0 << 1) + top0) + top1 + ((bottom0 << 3) + bottom0) + ((bottom1 << 1) + bottom1) + rounding) >> 4;
        lowerOdd = (top0 + ((top1 << 1) + top1) + ((bottom0 << 1) + bottom0) + ((bottom1 << 3) + bottom1) + rounding) >> 4;
    }

    /// <summary>
    /// Loads eight byte samples as unsigned 16-bit lanes.
    /// </summary>
    /// <param name="source">The first source byte.</param>
    /// <param name="offset">The byte offset.</param>
    /// <returns>The widened samples.</returns>
    private static Vector128<ushort> LoadEightBytes(ref byte source, int offset)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, offset));
        return Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
    }

    /// <summary>
    /// Interleaves and stores eight pairs of byte results.
    /// </summary>
    /// <param name="even">The left-biased results.</param>
    /// <param name="odd">The right-biased results.</param>
    /// <param name="destination">The first destination byte.</param>
    /// <param name="offset">The destination byte offset.</param>
    private static void StoreInterleavedBytes(
        Vector128<ushort> even,
        Vector128<ushort> odd,
        ref byte destination,
        int offset)
    {
        Vector128<ushort> lower = Vector128_.UnpackLow(even.AsInt16(), odd.AsInt16()).AsUInt16();
        Vector128<ushort> upper = Vector128_.UnpackHigh(even.AsInt16(), odd.AsInt16()).AsUInt16();
        Vector128.Narrow(lower, upper).StoreUnsafe(ref destination, (nuint)offset);
    }

    /// <summary>
    /// Interleaves and stores eight pairs of unsigned 16-bit results.
    /// </summary>
    /// <param name="even">The left-biased results.</param>
    /// <param name="odd">The right-biased results.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="offset">The destination sample offset.</param>
    private static void StoreInterleavedUInt16(
        Vector128<ushort> even,
        Vector128<ushort> odd,
        ref ushort destination,
        int offset)
    {
        Vector128_.UnpackLow(even.AsInt16(), odd.AsInt16()).AsUInt16().StoreUnsafe(ref destination, (nuint)offset);
        Vector128_.UnpackHigh(even.AsInt16(), odd.AsInt16()).AsUInt16().StoreUnsafe(
            ref destination,
            (nuint)(offset + Vector128<ushort>.Count));
    }

    /// <summary>
    /// Applies libyuv's edge-aware horizontal two-times linear kernel.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="destination">The destination row.</param>
    private static void ScaleRowUp2Linear(ReadOnlySpan<TSample> source, Span<TSample> destination)
    {
        if (typeof(TSample) == typeof(byte))
        {
            ScaleRowUp2LinearByte(
                MemoryMarshal.Cast<TSample, byte>(source),
                MemoryMarshal.Cast<TSample, byte>(destination));

            return;
        }

        ScaleRowUp2LinearUInt16(
            MemoryMarshal.Cast<TSample, ushort>(source),
            MemoryMarshal.Cast<TSample, ushort>(destination));
    }

    /// <summary>
    /// Applies the byte horizontal two-times linear kernel.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="destination">The destination row.</param>
    private static void ScaleRowUp2LinearByte(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        destination[0] = source[0];
        for (int x = 0; x < source.Length - 1; x++)
        {
            int destinationX = 1 + (2 * x);
            destination[destinationX] = (byte)(((3 * source[x]) + source[x + 1] + 2) >> 2);
            destination[destinationX + 1] = (byte)((source[x] + (3 * source[x + 1]) + 2) >> 2);
        }

        destination[^1] = source[^1];
    }

    /// <summary>
    /// Applies the unsigned 16-bit horizontal two-times linear kernel.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="destination">The destination row.</param>
    private static void ScaleRowUp2LinearUInt16(ReadOnlySpan<ushort> source, Span<ushort> destination)
    {
        destination[0] = source[0];
        for (int x = 0; x < source.Length - 1; x++)
        {
            int destinationX = 1 + (2 * x);
            destination[destinationX] = (ushort)(((3 * source[x]) + source[x + 1] + 2) >> 2);
            destination[destinationX + 1] = (ushort)((source[x] + (3 * source[x + 1]) + 2) >> 2);
        }

        destination[^1] = source[^1];
    }

    /// <summary>
    /// Horizontally maps one source row with libyuv's 16.16 fixed-point bilinear positions.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="destination">The destination row.</param>
    /// <param name="step">The 16.16 source-position increment.</param>
    private static void ScaleHorizontal(ReadOnlySpan<TSample> source, Span<TSample> destination, int step)
    {
        if (source.Length == destination.Length)
        {
            source.CopyTo(destination);
            return;
        }

        if (source.Length == 1)
        {
            destination.Fill(source[0]);
            return;
        }

        int sourcePosition = 0;
        if (typeof(TSample) == typeof(byte))
        {
            ReadOnlySpan<byte> sourceBytes = MemoryMarshal.Cast<TSample, byte>(source);
            Span<byte> destinationBytes = MemoryMarshal.Cast<TSample, byte>(destination);
            for (int x = 0; x < destinationBytes.Length; x++)
            {
                int sourceX = sourcePosition >> 16;
                int fraction = (sourcePosition & 0xFFFF) >> 9;
                int left = sourceBytes[sourceX];
                int right = sourceBytes[sourceX + 1];
                destinationBytes[x] = (byte)(left + (((fraction * (right - left)) + 0x40) >> 7));
                sourcePosition += step;
            }

            return;
        }

        ReadOnlySpan<ushort> sourceWords = MemoryMarshal.Cast<TSample, ushort>(source);
        Span<ushort> destinationWords = MemoryMarshal.Cast<TSample, ushort>(destination);
        for (int x = 0; x < destinationWords.Length; x++)
        {
            int sourceX = sourcePosition >> 16;
            int fraction = sourcePosition & 0xFFFF;
            int left = sourceWords[sourceX];
            int right = sourceWords[sourceX + 1];
            destinationWords[x] = (ushort)(left + ((((long)fraction * (right - left)) + 0x8000) >> 16));
            sourcePosition += step;
        }
    }

    /// <summary>
    /// Vertically interpolates two horizontally scaled rows.
    /// </summary>
    /// <param name="top">The upper row.</param>
    /// <param name="bottom">The lower row.</param>
    /// <param name="destination">The destination row.</param>
    /// <param name="bottomWeight">The lower-row weight with a denominator of 256.</param>
    private static void InterpolateRows(
        ReadOnlySpan<TSample> top,
        ReadOnlySpan<TSample> bottom,
        Span<TSample> destination,
        int bottomWeight)
    {
        if (bottomWeight == 0)
        {
            top.CopyTo(destination);
            return;
        }

        int topWeight = 256 - bottomWeight;
        if (typeof(TSample) == typeof(byte))
        {
            ReadOnlySpan<byte> topBytes = MemoryMarshal.Cast<TSample, byte>(top);
            ReadOnlySpan<byte> bottomBytes = MemoryMarshal.Cast<TSample, byte>(bottom);
            Span<byte> destinationBytes = MemoryMarshal.Cast<TSample, byte>(destination);
            for (int x = 0; x < destinationBytes.Length; x++)
            {
                destinationBytes[x] = (byte)(((topBytes[x] * topWeight) + (bottomBytes[x] * bottomWeight) + 128) >> 8);
            }

            return;
        }

        ReadOnlySpan<ushort> topWords = MemoryMarshal.Cast<TSample, ushort>(top);
        ReadOnlySpan<ushort> bottomWords = MemoryMarshal.Cast<TSample, ushort>(bottom);
        Span<ushort> destinationWords = MemoryMarshal.Cast<TSample, ushort>(destination);
        for (int x = 0; x < destinationWords.Length; x++)
        {
            destinationWords[x] = (ushort)(((topWords[x] * topWeight) + (bottomWords[x] * bottomWeight) + 128) >> 8);
        }
    }

    /// <summary>
    /// Divides two decremented lengths into libyuv's 16.16 endpoint-preserving step.
    /// </summary>
    /// <param name="sourceLength">The source length.</param>
    /// <param name="destinationLength">The destination length.</param>
    /// <returns>The 16.16 source-position increment.</returns>
    private static int FixedDivideOne(int sourceLength, int destinationLength)
        => (int)((((long)sourceLength << 16) - 0x00010001) / (destinationLength - 1));

    /// <summary>
    /// Divides a positive value by a positive divisor with ceiling rounding.
    /// </summary>
    /// <param name="value">The value to divide.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>The ceiling-rounded quotient.</returns>
    private static int DivideCeiling(int value, int divisor) => (value + divisor - 1) / divisor;

    private readonly struct ChromaPlanes(Buffer2D<TSample> blue, Buffer2D<TSample> red)
    {
        public Buffer2D<TSample> Blue { get; } = blue;

        public Buffer2D<TSample> Red { get; } = red;
    }

    private readonly struct PresentationPlanes(Buffer2D<TSample> luma, ChromaPlanes? chroma)
    {
        public Buffer2D<TSample> Luma { get; } = luma;

        public ChromaPlanes? Chroma { get; } = chroma;
    }
}
