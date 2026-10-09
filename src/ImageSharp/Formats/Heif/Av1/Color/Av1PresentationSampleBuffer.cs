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

            // The constructor publishes ownership only after it allocates and initializes every required plane.
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
    /// Scales one component plane with an integer bilinear filter. The filter gives the same samples as common AVIF decoders.
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
                ScaleRowUp2Linear(GetSourceRow(source, plane, y)[..sourceWidth], destination.DangerousGetRowSpan(y));
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
                ScaleHorizontal(GetSourceRow(source, plane, y)[..sourceWidth], destination.DangerousGetRowSpan(y), rowHorizontalStep);
            }

            return;
        }

        // Layer selection presents a lower spatial layer at the full item extent, so both dimensions are monotonic. The general path maps destination centers
        // in 16.16 fixed point and keeps only two horizontally filtered rows. Thus no second full-plane intermediate is necessary, and the path stays
        // group-safe with small allocators.
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
    /// Applies an edge-aware two-times bilinear kernel to one full plane.
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
        ref TSample topSourceBase = ref MemoryMarshal.GetReference(topSource);
        ref TSample bottomSourceBase = ref MemoryMarshal.GetReference(bottomSource);
        ref TSample topDestinationBase = ref MemoryMarshal.GetReference(topDestination);
        ref TSample bottomDestinationBase = ref MemoryMarshal.GetReference(bottomDestination);

        // The first and the last destination samples have only one source column. They therefore take the two-tap edge form, not the four-tap interior form.
        int firstTop = ReadSample(ref topSourceBase);
        int firstBottom = ReadSample(ref bottomSourceBase);
        WriteSample(ref topDestinationBase, 0, ((3 * firstTop) + firstBottom + 2) >> 2);
        WriteSample(ref bottomDestinationBase, 0, (firstTop + (3 * firstBottom) + 2) >> 2);

        int lastSource = topSource.Length - 1;
        int x = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            // Eight source positions produce sixteen destination samples. The pair that each position produces is interleaved on the store, so one iteration
            // covers sixteen destination columns of both rows.
            for (; x + Vector128<ushort>.Count <= lastSource; x += Vector128<ushort>.Count)
            {
                CalculateBilinearPairs(
                    LoadEight(ref topSourceBase, x),
                    LoadEight(ref topSourceBase, x + 1),
                    LoadEight(ref bottomSourceBase, x),
                    LoadEight(ref bottomSourceBase, x + 1),
                    out Vector128<ushort> upperEven,
                    out Vector128<ushort> upperOdd,
                    out Vector128<ushort> lowerEven,
                    out Vector128<ushort> lowerOdd);

                StoreInterleaved(upperEven, upperOdd, ref topDestinationBase, 1 + (2 * x));
                StoreInterleaved(lowerEven, lowerOdd, ref bottomDestinationBase, 1 + (2 * x));
            }
        }

        for (; x < lastSource; x++)
        {
            int top0 = ReadSample(ref topSourceBase, x);
            int top1 = ReadSample(ref topSourceBase, x + 1);
            int bottom0 = ReadSample(ref bottomSourceBase, x);
            int bottom1 = ReadSample(ref bottomSourceBase, x + 1);
            int destination = 1 + (2 * x);
            WriteSample(ref topDestinationBase, destination, ((9 * top0) + (3 * top1) + (3 * bottom0) + bottom1 + 8) >> 4);
            WriteSample(ref topDestinationBase, destination + 1, ((3 * top0) + (9 * top1) + bottom0 + (3 * bottom1) + 8) >> 4);
            WriteSample(ref bottomDestinationBase, destination, ((3 * top0) + top1 + (9 * bottom0) + (3 * bottom1) + 8) >> 4);
            WriteSample(ref bottomDestinationBase, destination + 1, (top0 + (3 * top1) + (3 * bottom0) + (9 * bottom1) + 8) >> 4);
        }

        int finalTop = ReadSample(ref topSourceBase, lastSource);
        int finalBottom = ReadSample(ref bottomSourceBase, lastSource);
        int lastDestination = topDestination.Length - 1;
        WriteSample(ref topDestinationBase, lastDestination, ((3 * finalTop) + finalBottom + 2) >> 2);
        WriteSample(ref bottomDestinationBase, lastDestination, (finalTop + (3 * finalBottom) + 2) >> 2);
    }

    /// <summary>
    /// Reads one sample of either depth.
    /// </summary>
    /// <param name="source">The first sample of the row.</param>
    /// <param name="offset">The sample offset.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadSample(ref TSample source, int offset = 0)
    {
        ref TSample sample = ref Unsafe.Add(ref source, offset);
        return Unsafe.SizeOf<TSample>() == 1
            ? Unsafe.As<TSample, byte>(ref sample)
            : Unsafe.As<TSample, ushort>(ref sample);
    }

    /// <summary>
    /// Writes one sample of either depth.
    /// </summary>
    /// <param name="destination">The first sample of the row.</param>
    /// <param name="offset">The sample offset.</param>
    /// <param name="value">The sample value, already inside the range of the depth.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteSample(ref TSample destination, int offset, int value)
    {
        ref TSample sample = ref Unsafe.Add(ref destination, offset);
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            Unsafe.As<TSample, byte>(ref sample) = (byte)value;
            return;
        }

        Unsafe.As<TSample, ushort>(ref sample) = (ushort)value;
    }

    /// <summary>
    /// Loads eight samples of either depth as unsigned sixteen-bit lanes.
    /// </summary>
    /// <param name="source">The first sample of the row.</param>
    /// <param name="offset">The sample offset.</param>
    /// <returns>The samples in increasing column order.</returns>
    /// <remarks>
    /// An eight-bit row is read through a packed integer and widened, which touches only the eight bytes that this iteration owns. A high-bit-depth row is
    /// already sixteen bits wide, so it loads directly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> LoadEight(ref TSample source, int offset)
    {
        ref TSample sample = ref Unsafe.Add(ref source, offset);
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<TSample, byte>(ref sample));
            return Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
        }

        return Vector128.LoadUnsafe(ref Unsafe.As<TSample, ushort>(ref sample));
    }

    /// <summary>
    /// Interleaves eight pairs of results and stores them at either depth.
    /// </summary>
    /// <param name="even">The left-biased results.</param>
    /// <param name="odd">The right-biased results.</param>
    /// <param name="destination">The first sample of the row.</param>
    /// <param name="offset">The destination sample offset.</param>
    /// <remarks>
    /// Each source position produces two adjacent destination samples, so the two result vectors interleave. An eight-bit row then narrows the sixteen
    /// interleaved lanes into one vector of bytes. The narrowing is exact because every result is already inside the byte range.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreInterleaved(
        Vector128<ushort> even,
        Vector128<ushort> odd,
        ref TSample destination,
        int offset)
    {
        Vector128<ushort> lower = Vector128_.UnpackLow(even.AsInt16(), odd.AsInt16()).AsUInt16();
        Vector128<ushort> upper = Vector128_.UnpackHigh(even.AsInt16(), odd.AsInt16()).AsUInt16();
        ref TSample sample = ref Unsafe.Add(ref destination, offset);
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            Vector128.Narrow(lower, upper).StoreUnsafe(ref Unsafe.As<TSample, byte>(ref sample));
            return;
        }

        ref ushort wide = ref Unsafe.As<TSample, ushort>(ref sample);
        lower.StoreUnsafe(ref wide);
        upper.StoreUnsafe(ref wide, (nuint)Vector128<ushort>.Count);
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

        // The largest twelve-bit weighted sum is 16 * 4095 + 8, which remains within unsigned 16-bit lanes. Thus eight independent source positions per vector
        // need no widening. The exact add-before-shift rounding stays the same for byte and high-bit-depth presentation planes.
        upperEven = (((top0 << 3) + top0) + ((top1 << 1) + top1) + ((bottom0 << 1) + bottom0) + bottom1 + rounding) >> 4;
        upperOdd = (((top0 << 1) + top0) + ((top1 << 3) + top1) + bottom0 + ((bottom1 << 1) + bottom1) + rounding) >> 4;
        lowerEven = (((top0 << 1) + top0) + top1 + ((bottom0 << 3) + bottom0) + ((bottom1 << 1) + bottom1) + rounding) >> 4;
        lowerOdd = (top0 + ((top1 << 1) + top1) + ((bottom0 << 1) + bottom0) + ((bottom1 << 3) + bottom1) + rounding) >> 4;
    }

    /// <summary>
    /// Applies an edge-aware horizontal two-times linear kernel.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="destination">The destination row.</param>
    private static void ScaleRowUp2Linear(ReadOnlySpan<TSample> source, Span<TSample> destination)
    {
        if (typeof(TSample) == typeof(byte))
        {
            ScaleRowUp2LinearByte(MemoryMarshal.Cast<TSample, byte>(source), MemoryMarshal.Cast<TSample, byte>(destination));

            return;
        }

        ScaleRowUp2LinearUInt16(MemoryMarshal.Cast<TSample, ushort>(source), MemoryMarshal.Cast<TSample, ushort>(destination));
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
    /// Horizontally maps one source row with 16.16 fixed-point bilinear positions.
    /// </summary>
    /// <remarks>
    /// Eight-bit rows use a 7-bit fraction and the rounding offset 0x40. High-bit-depth rows use the full 16-bit fraction and the rounding offset 0x8000.
    /// </remarks>
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
    /// Divides two decremented lengths into a 16.16 step that maps the first and last samples onto each other.
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

    /// <summary>
    /// Holds the two scaled chroma planes as one owned pair.
    /// </summary>
    private readonly struct ChromaPlanes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChromaPlanes"/> struct.
        /// </summary>
        /// <param name="blue">The scaled blue-difference plane.</param>
        /// <param name="red">The scaled red-difference plane.</param>
        public ChromaPlanes(Buffer2D<TSample> blue, Buffer2D<TSample> red)
        {
            this.Blue = blue;
            this.Red = red;
        }

        /// <summary>
        /// Gets the scaled blue-difference plane.
        /// </summary>
        public Buffer2D<TSample> Blue { get; }

        /// <summary>
        /// Gets the scaled red-difference plane.
        /// </summary>
        public Buffer2D<TSample> Red { get; }
    }

    /// <summary>
    /// Holds the complete set of owned presentation planes.
    /// </summary>
    private readonly struct PresentationPlanes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PresentationPlanes"/> struct.
        /// </summary>
        /// <param name="luma">The scaled luma plane.</param>
        /// <param name="chroma">The scaled chroma planes, or <see langword="null"/> for a monochrome image.</param>
        public PresentationPlanes(Buffer2D<TSample> luma, ChromaPlanes? chroma)
        {
            this.Luma = luma;
            this.Chroma = chroma;
        }

        /// <summary>
        /// Gets the scaled luma plane.
        /// </summary>
        public Buffer2D<TSample> Luma { get; }

        /// <summary>
        /// Gets the scaled chroma planes, or <see langword="null"/> for a monochrome image.
        /// </summary>
        public ChromaPlanes? Chroma { get; }
    }
}
