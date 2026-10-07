// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Resize;

/// <summary>
/// Resizes the planes of a frame to another size, as the encoder does for the source and the references of a scaled
/// layer.
/// </summary>
/// <remarks>
/// <para>
/// libaom has two resizers. The normative one convolves each 16x16 output tile with an interpolation kernel at a
/// fixed phase offset, first along rows and then along columns. The x64 build uses it for eight-bit planes whose size
/// ratio divides sixteen in both directions, or is three quarters. The other one, which libaom calls nonnormative,
/// halves a line with a symmetric filter until it is less than twice the target length and then interpolates the rest,
/// first along rows and then along columns. It takes every other ratio and every plane of more than eight bits.
/// </para>
/// <para>
/// Both resizers are separable sums of eight weighted samples, rounded by seven bits and clipped after each direction.
/// This class plans each direction as a list of output lines, each naming eight input lines and eight coefficients,
/// and applies the plan down the columns of a plane with one vector across the columns. A pass along rows runs on a
/// transposed copy, so it is a pass down columns too. The arithmetic of each output sample is the same as libaom's,
/// so the result is the same.
/// </para>
/// </remarks>
internal static partial class Av1FrameResizer
{
    /// <summary>
    /// The fractional bits of a filter coefficient. Reference: FILTER_BITS.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The number of taps of every resize filter. Reference: SUBPEL_TAPS.
    /// </summary>
    private const int Taps = 8;

    /// <summary>
    /// The fractional bits of a position in the normative resizer. Reference: SUBPEL_BITS.
    /// </summary>
    private const int SubpixelBits = 4;

    /// <summary>
    /// The mask of the fractional part of a position in the normative resizer. Reference: SUBPEL_MASK.
    /// </summary>
    private const int SubpixelMask = (1 << SubpixelBits) - 1;

    /// <summary>
    /// The width and height of an output tile of the normative resizer. Reference: the 16x16 tiles of
    /// av1_resize_and_extend_frame_c().
    /// </summary>
    private const int TileSize = 16;

    /// <summary>
    /// The fractional bits of a filter phase in the nonnormative resizer. Reference: RS_SUBPEL_BITS.
    /// </summary>
    private const int ResizeSubpixelBits = 6;

    /// <summary>
    /// The mask of a filter phase in the nonnormative resizer. Reference: RS_SUBPEL_MASK.
    /// </summary>
    private const int ResizeSubpixelMask = (1 << ResizeSubpixelBits) - 1;

    /// <summary>
    /// The fractional bits of a position in the nonnormative resizer. Reference: RS_SCALE_SUBPEL_BITS.
    /// </summary>
    private const int ResizeScaleSubpixelBits = 14;

    /// <summary>
    /// The position bits below the filter phase. Reference: RS_SCALE_EXTRA_BITS.
    /// </summary>
    private const int ResizeScaleExtraBits = ResizeScaleSubpixelBits - ResizeSubpixelBits;

    /// <summary>
    /// The rounding offset of the position bits below the filter phase. Reference: RS_SCALE_EXTRA_OFF.
    /// </summary>
    private const int ResizeScaleExtraOffset = 1 << (ResizeScaleExtraBits - 1);

    /// <summary>
    /// Returns whether the x64 kernels of the normative resizer handle the size change of one plane: a reduction to a
    /// half, a quarter or three quarters of both sizes, or a doubling of both at phase zero from a width that is a
    /// multiple of eight. Reference: has_normative_scaler_ssse3().
    /// </summary>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="destinationWidth">The target width.</param>
    /// <param name="destinationHeight">The target height.</param>
    /// <param name="phase">The phase offset, in sixteenths of a sample.</param>
    /// <returns><see langword="true"/> when the x64 kernels handle the size change.</returns>
    private static bool HasGroupScaler(int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight, int phase)
        => (2 * destinationWidth == sourceWidth && 2 * destinationHeight == sourceHeight) ||
            (4 * destinationWidth == sourceWidth && 4 * destinationHeight == sourceHeight) ||
            (4 * destinationWidth == 3 * sourceWidth && 4 * destinationHeight == 3 * sourceHeight) ||
            (destinationWidth == sourceWidth * 2 && destinationHeight == sourceHeight * 2 && phase == 0 && sourceWidth % 8 == 0);

    /// <summary>
    /// Returns whether the normative resizer of the x64 build handles a size change: each size is between a quarter
    /// and sixteen times the other, sixteen times each size is a multiple of the other, or the target is three quarters
    /// of the source. Reference: av1_has_optimized_scaler() with the HAVE_SSSE3 extension.
    /// </summary>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="destinationWidth">The target width.</param>
    /// <param name="destinationHeight">The target height.</param>
    /// <returns><see langword="true"/> when the normative resizer handles the size change.</returns>
    public static bool HasOptimizedScaler(int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight)
    {
        bool optimized =
            destinationWidth * 4 >= sourceWidth && destinationHeight * 4 >= sourceHeight &&
            destinationWidth <= sourceWidth * 16 && destinationHeight <= sourceHeight * 16 &&
            (16 * destinationWidth) % sourceWidth == 0 && (16 * sourceWidth) % destinationWidth == 0 &&
            (16 * destinationHeight) % sourceHeight == 0 && (16 * sourceHeight) % destinationHeight == 0;

        return optimized || (4 * destinationWidth == 3 * sourceWidth && 4 * destinationHeight == 3 * sourceHeight);
    }

    /// <summary>
    /// Returns the kernel and the phase offset with which the encoder resizes the source and the references of a frame
    /// of another size: the phase is half a sample, and the kernel is bilinear for a 2:1 reduction of a frame larger
    /// than 320x180, regular for a 4:3 reduction, and smooth otherwise. Reference: the filter_scaler and phase_scaler
    /// of encode_without_recode() without spatial layer coding.
    /// </summary>
    /// <param name="frameSize">The size of the frame being coded.</param>
    /// <param name="sourceSize">The size of the source frame.</param>
    /// <returns>The interpolation kernel and the phase offset in sixteenths of a sample.</returns>
    public static (Av1InterpolationFilter Filter, int Phase) GetFrameScaler(Size frameSize, Size sourceSize)
    {
        Av1InterpolationFilter filter = Av1InterpolationFilter.Smooth;
        if (frameSize.Width << 1 == sourceSize.Width && frameSize.Height << 1 == sourceSize.Height)
        {
            filter = frameSize.Width * frameSize.Height <= 320 * 180 ? Av1InterpolationFilter.Smooth : Av1InterpolationFilter.Bilinear;
        }
        else if (frameSize.Width << 2 == 3 * sourceSize.Width && frameSize.Height << 2 == 3 * sourceSize.Height)
        {
            filter = Av1InterpolationFilter.Regular;
        }

        return (filter, 8);
    }

    /// <summary>
    /// Resizes every plane of an eight-bit frame to the size of the destination frame and extends its borders. The
    /// normative resizer takes the planes when it handles both the luma and the chroma size change, and the
    /// nonnormative one takes them otherwise. Reference: the use_optimized_scaler branch of
    /// av1_realloc_and_scale_if_required() and av1_scale_references(), with av1_resize_and_extend_frame() and
    /// av1_resize_and_extend_frame_nonnormative().
    /// </summary>
    /// <param name="allocator">The allocator of the intermediate planes.</param>
    /// <param name="source">The source frame. Its borders must be extended, because the normative resizer reads them.</param>
    /// <param name="destination">The destination frame, whose visible size is the target size.</param>
    /// <param name="filter">The interpolation kernel of the normative resizer: bilinear, smooth or regular.</param>
    /// <param name="phase">The phase offset of the normative resizer, in sixteenths of a sample.</param>
    public static void ResizeFrame(
        MemoryAllocator allocator,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> destination,
        Av1InterpolationFilter filter,
        int phase)
    {
        bool optimized = HasOptimizedScaler(source.Width, source.Height, destination.Width, destination.Height);

        // The x64 build takes its own kernels only when every plane has a size change that it handles. Reference:
        // the has_normative_scaler test of av1_resize_and_extend_frame_ssse3().
        bool groupKernels = HasGroupScaler(source.Width, source.Height, destination.Width, destination.Height, phase);
        if (!source.IsMonochrome)
        {
            Size sourceChroma = GetChromaSize(source);
            Size destinationChroma = GetChromaSize(destination);
            optimized &= HasOptimizedScaler(sourceChroma.Width, sourceChroma.Height, destinationChroma.Width, destinationChroma.Height);
            groupKernels &= HasGroupScaler(sourceChroma.Width, sourceChroma.Height, destinationChroma.Width, destinationChroma.Height, phase);
        }

        int planeCount = source.IsMonochrome ? 1 : 3;
        for (int plane = 0; plane < planeCount; plane++)
        {
            Av1PlaneRegion<byte> sourcePlane = source.CodedView.GetPlane((Av1Plane)plane);
            Av1PlaneRegion<byte> destinationPlane = destination.CodedView.GetPlane((Av1Plane)plane);
            Size sourceSize = plane == 0 ? new Size(source.Width, source.Height) : GetChromaSize(source);
            Size destinationSize = plane == 0 ? new Size(destination.Width, destination.Height) : GetChromaSize(destination);
            if (optimized)
            {
                ScalePlane(allocator, sourcePlane, sourceSize, destinationPlane, destinationSize, filter, phase, groupKernels);
            }
            else
            {
                ResizePlane<byte, ByteOperator>(allocator, sourcePlane, sourceSize, destinationPlane, destinationSize, byte.MaxValue);
            }
        }

        destination.ExtendBorders();
    }

    /// <summary>
    /// Resizes every plane of a frame of more than eight bits to the size of the destination frame with the
    /// nonnormative resizer, and extends its borders. Reference: the high bit depth branch of
    /// av1_realloc_and_scale_if_required() and av1_scale_references(), with
    /// av1_resize_and_extend_frame_nonnormative().
    /// </summary>
    /// <param name="allocator">The allocator of the intermediate planes.</param>
    /// <param name="source">The source frame.</param>
    /// <param name="destination">The destination frame, whose visible size is the target size.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    public static void ResizeFrame(
        MemoryAllocator allocator,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> destination,
        int bitDepth)
    {
        int planeCount = source.IsMonochrome ? 1 : 3;
        for (int plane = 0; plane < planeCount; plane++)
        {
            Size sourceSize = plane == 0 ? new Size(source.Width, source.Height) : GetChromaSize(source);
            Size destinationSize = plane == 0 ? new Size(destination.Width, destination.Height) : GetChromaSize(destination);
            ResizePlane<ushort, UInt16Operator>(
                allocator,
                source.CodedView.GetPlane((Av1Plane)plane),
                sourceSize,
                destination.CodedView.GetPlane((Av1Plane)plane),
                destinationSize,
                (1 << bitDepth) - 1);
        }

        destination.ExtendBorders();
    }

    /// <summary>
    /// Returns the visible size of the chroma planes of a frame. Reference: uv_crop_width and uv_crop_height.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <param name="frame">The frame.</param>
    /// <returns>The chroma size.</returns>
    private static Size GetChromaSize<TSample>(Av1EncoderFrame<TSample> frame)
        where TSample : unmanaged
        => new(
            (frame.Width + frame.ChromaSubsamplingX) >> frame.ChromaSubsamplingX,
            (frame.Height + frame.ChromaSubsamplingY) >> frame.ChromaSubsamplingY);

    /// <summary>
    /// Resizes one eight-bit plane with the normative resizer. Each output tile of 16x16 samples starts its positions
    /// from the tile corner, so the positions inside a tile step by the integer ratio. The rows and the columns are
    /// planned once for the whole plane, which gives each output sample the inputs and the kernel phase of its tile.
    /// Reference: av1_resize_and_extend_frame_c() and aom_scaled_2d_c().
    /// </summary>
    /// <param name="allocator">The allocator of the intermediate planes.</param>
    /// <param name="source">
    /// The source plane. The resizer reads up to three samples before and five after its visible edges.
    /// </param>
    /// <param name="sourceSize">The visible size of the source plane.</param>
    /// <param name="destination">The destination plane.</param>
    /// <param name="destinationSize">The visible size of the destination plane.</param>
    /// <param name="filter">The interpolation kernel.</param>
    /// <param name="phase">The phase offset, in sixteenths of a sample.</param>
    /// <param name="groupKernels">
    /// Whether every plane of the frame has a size change that the x64 kernels handle, so a plane reduced to three
    /// quarters takes the positions of those kernels.
    /// </param>
    internal static void ScalePlane(
        MemoryAllocator allocator,
        Av1PlaneRegion<byte> source,
        Size sourceSize,
        Av1PlaneRegion<byte> destination,
        Size destinationSize,
        Av1InterpolationFilter filter,
        int phase,
        bool groupKernels)
    {
        int width = destinationSize.Width;
        int height = destinationSize.Height;
        using IMemoryOwner<int> columnRowsOwner = allocator.Allocate<int>(width * Taps);
        using IMemoryOwner<short> columnCoefficientsOwner = allocator.Allocate<short>(width * Taps);
        using IMemoryOwner<int> rowRowsOwner = allocator.Allocate<int>(height * Taps);
        using IMemoryOwner<short> rowCoefficientsOwner = allocator.Allocate<short>(height * Taps);
        TapPlan columns = new(columnRowsOwner.Memory.Span[..(width * Taps)], columnCoefficientsOwner.Memory.Span[..(width * Taps)], width);
        TapPlan rows = new(rowRowsOwner.Memory.Span[..(height * Taps)], rowCoefficientsOwner.Memory.Span[..(height * Taps)], height);

        // The x64 build reduces a plane to three quarters of both sizes with its own kernel, which places every group
        // of three outputs on four inputs. Every other size change it handles takes the positions of the 16x16 tiles.
        // Reference: the 4 to 3 branch of av1_resize_and_extend_frame_ssse3().
        bool threeQuarters = groupKernels && 4 * width == 3 * sourceSize.Width && 4 * height == 3 * sourceSize.Height;
        if (threeQuarters)
        {
            PlanThreeQuarters(width, phase, filter, ref columns);
            PlanThreeQuarters(height, phase, filter, ref rows);
        }
        else
        {
            PlanNormative(sourceSize.Width, width, phase, filter, ref columns);
            PlanNormative(sourceSize.Height, height, phase, filter, ref rows);
        }

        // The source rows and columns that any tap reads, with the border samples beyond the visible edges.
        (int firstColumn, int lastColumn) = columns.GetRange();
        (int firstRow, int lastRow) = rows.GetRange();
        int columnCount = lastColumn - firstColumn + 1;
        int rowCount = lastRow - firstRow + 1;
        columns.Shift(-firstColumn);
        rows.Shift(-firstRow);

        // The pass along rows runs down the columns of the transposed source, then its result is transposed back.
        using IMemoryOwner<byte> transposedOwner = allocator.Allocate<byte>(columnCount * rowCount);
        using IMemoryOwner<byte> filteredOwner = allocator.Allocate<byte>(width * rowCount);
        using IMemoryOwner<byte> horizontalOwner = allocator.Allocate<byte>(rowCount * width);
        Span<byte> transposed = transposedOwner.Memory.Span;
        Span<byte> filtered = filteredOwner.Memory.Span;
        Span<byte> horizontal = horizontalOwner.Memory.Span;
        Transpose<byte>(source.Samples, source.GetOffset(firstColumn, firstRow), source.Stride, transposed, rowCount, rowCount, columnCount);
        ApplyPass<byte, ByteOperator>(transposed, 0, rowCount, filtered, 0, rowCount, columns, rowCount, byte.MaxValue);
        Transpose<byte>(filtered, 0, rowCount, horizontal, width, width, rowCount);

        // The pass along columns writes the destination rows.
        ApplyPass<byte, ByteOperator>(horizontal, 0, width, destination.Samples, destination.GetOffset(0, 0), destination.Stride, rows, width, byte.MaxValue);
    }

    /// <summary>
    /// Resizes one plane with the nonnormative resizer: every row, then every column, is halved while that keeps it at
    /// least as long as the target, and the remaining ratio is interpolated. Reference: av1_resize_plane(),
    /// highbd_resize_plane(), resize_multistep() and highbd_resize_multistep().
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="allocator">The allocator of the intermediate planes.</param>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceSize">The visible size of the source plane.</param>
    /// <param name="destination">The destination plane.</param>
    /// <param name="destinationSize">The visible size of the destination plane.</param>
    /// <param name="maximum">The largest sample value.</param>
    internal static void ResizePlane<TSample, TOperator>(
        MemoryAllocator allocator,
        Av1PlaneRegion<TSample> source,
        Size sourceSize,
        Av1PlaneRegion<TSample> destination,
        Size destinationSize,
        int maximum)
        where TSample : unmanaged
        where TOperator : struct, IAv1ResizeSampleOperator<TSample>
    {
        int width = sourceSize.Width;
        int height = sourceSize.Height;
        int width2 = destinationSize.Width;
        int height2 = destinationSize.Height;

        // The rows are resized on the transposed source: each source column is a row of the copy. The longest
        // intermediate line is the source line itself.
        int longest = Math.Max(width, width2);
        using IMemoryOwner<TSample> firstOwner = allocator.Allocate<TSample>(longest * height);
        using IMemoryOwner<TSample> secondOwner = allocator.Allocate<TSample>(longest * height);
        Span<TSample> first = firstOwner.Memory.Span;
        Span<TSample> second = secondOwner.Memory.Span;
        Transpose<TSample>(source.Samples, source.GetOffset(0, 0), source.Stride, first, height, height, width);
        bool resultInFirst = ResizeLines<TSample, TOperator>(allocator, first, second, width, width2, height, maximum);

        // The resized rows are transposed back, and the columns are resized in place of the rows.
        using IMemoryOwner<TSample> horizontalOwner = allocator.Allocate<TSample>(width2 * Math.Max(height, height2));
        using IMemoryOwner<TSample> otherOwner = allocator.Allocate<TSample>(width2 * Math.Max(height, height2));
        Span<TSample> horizontal = horizontalOwner.Memory.Span;
        Span<TSample> other = otherOwner.Memory.Span;
        Transpose<TSample>(resultInFirst ? first : second, 0, height, horizontal, width2, width2, height);
        bool verticalInFirst = ResizeLines<TSample, TOperator>(allocator, horizontal, other, height, height2, width2, maximum);

        ReadOnlySpan<TSample> result = verticalInFirst ? horizontal : other;
        Span<TSample> destinationSamples = destination.Samples;
        for (int y = 0; y < height2; y++)
        {
            result.Slice(y * width2, width2).CopyTo(destinationSamples.Slice(destination.GetOffset(0, y), width2));
        }
    }

    /// <summary>
    /// Resizes the lines of a plane laid out one line per row, from one length to another. Each step halves the
    /// lines or interpolates them, and the steps alternate between the two buffers. Reference: resize_multistep().
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="allocator">The allocator of the step plans.</param>
    /// <param name="first">The buffer that holds the lines, one line per row and <paramref name="lineCount"/> samples per row.</param>
    /// <param name="second">The other buffer, as large as the first.</param>
    /// <param name="length">The length of the lines.</param>
    /// <param name="targetLength">The resized length.</param>
    /// <param name="lineCount">The number of lines.</param>
    /// <param name="maximum">The largest sample value.</param>
    /// <returns><see langword="true"/> when the resized lines are in <paramref name="first"/>.</returns>
    private static bool ResizeLines<TSample, TOperator>(
        MemoryAllocator allocator,
        Span<TSample> first,
        Span<TSample> second,
        int length,
        int targetLength,
        int lineCount,
        int maximum)
        where TSample : unmanaged
        where TOperator : struct, IAv1ResizeSampleOperator<TSample>
    {
        // Lines of the target length are kept as they are. Reference: the memcpy of resize_multistep().
        if (length == targetLength)
        {
            return true;
        }

        using IMemoryOwner<int> rowsOwner = allocator.Allocate<int>(Math.Max(length, targetLength) * Taps);
        using IMemoryOwner<short> coefficientsOwner = allocator.Allocate<short>(Math.Max(length, targetLength) * Taps);
        bool inFirst = true;
        int steps = GetDown2Steps(length, targetLength);
        int filteredLength = length;
        Span<int> rows = rowsOwner.Memory.Span;
        Span<short> coefficients = coefficientsOwner.Memory.Span;
        for (int step = 0; step < steps; step++)
        {
            // An odd line halves with the odd-length filter, an even line with the even-length one.
            int halvedLength = (filteredLength + 1) >> 1;
            TapPlan plan = new(rows[..(halvedLength * Taps)], coefficients[..(halvedLength * Taps)], halvedLength);
            PlanDown2(filteredLength, ref plan);
            ApplyPass<TSample, TOperator>(inFirst ? first : second, 0, lineCount, inFirst ? second : first, 0, lineCount, plan, lineCount, maximum);
            inFirst = !inFirst;
            filteredLength = halvedLength;
        }

        if (filteredLength != targetLength)
        {
            TapPlan plan = new(rowsOwner.Memory.Span[..(targetLength * Taps)], coefficientsOwner.Memory.Span[..(targetLength * Taps)], targetLength);
            PlanInterpolation(filteredLength, targetLength, ref plan);
            ApplyPass<TSample, TOperator>(inFirst ? first : second, 0, lineCount, inFirst ? second : first, 0, lineCount, plan, lineCount, maximum);
            inFirst = !inFirst;
        }

        return inFirst;
    }

    /// <summary>
    /// Returns the number of times a line can be halved while it stays at least as long as the target. Reference:
    /// get_down2_steps().
    /// </summary>
    /// <param name="length">The line length.</param>
    /// <param name="targetLength">The target length.</param>
    /// <returns>The number of halving steps.</returns>
    private static int GetDown2Steps(int length, int targetLength)
    {
        int steps = 0;
        int halvedLength;
        while ((halvedLength = (length + 1) >> 1) >= targetLength)
        {
            steps++;
            length = halvedLength;

            // A line of one sample stays one sample long, so it would halve forever.
            if (length == 1)
            {
                break;
            }
        }

        return steps;
    }

    /// <summary>
    /// Plans one direction of the normative resizer. Each output sample of a tile reads from the source position of
    /// the tile corner, scaled to sixteenths and offset by the phase, then steps by the integer ratio. Equal sizes read
    /// each sample at phase 0. Reference: the x_q4 and y_q4 positions of av1_resize_and_extend_frame_c(), with
    /// convolve_horiz() and convolve_vert() of aom_scaled_2d_c().
    /// </summary>
    /// <param name="length">The source length.</param>
    /// <param name="targetLength">The target length.</param>
    /// <param name="phase">The phase offset, in sixteenths of a sample.</param>
    /// <param name="filter">The interpolation kernel.</param>
    /// <param name="plan">Receives the source lines, relative to the first visible line, and the coefficients.</param>
    private static void PlanNormative(int length, int targetLength, int phase, Av1InterpolationFilter filter, ref TapPlan plan)
    {
        bool scaled = length != targetLength;
        int step = 16 * length / targetLength;
        for (int output = 0; output < targetLength; output++)
        {
            int tileStart = output & ~(TileSize - 1);
            int local = output - tileStart;

            // libaom computes the tile positions in 32-bit integers; the 64-bit products here give the same value for
            // every size whose product fits in 32 bits.
            int start = scaled ? (int)((tileStart * 16L * length / targetLength) + phase) : 0;
            int tileBase = (int)(tileStart * (long)length / targetLength);
            int position = (start & SubpixelMask) + (local * step);
            int center = tileBase + (position >> SubpixelBits);
            ReadOnlySpan<short> kernel = GetKernel(filter, position & SubpixelMask);
            for (int tap = 0; tap < Taps; tap++)
            {
                plan.Rows[(output * Taps) + tap] = center - (Taps / 2) + 1 + tap;
                plan.Coefficients[(output * Taps) + tap] = kernel[tap];
            }
        }
    }

    /// <summary>
    /// Plans one direction of the x64 reduction to three quarters. Output 3g + k reads around input 4g plus the
    /// whole part of the phase offset plus k times 21 sixteenths, with the kernel of the fractional part, so every
    /// group restarts on a multiple of four inputs. Reference: the offset1_q4 and offset2_q4 positions of
    /// scale_plane_4_to_3_general(), whose eight-tap sums are exact.
    /// </summary>
    /// <param name="targetLength">The target length, three quarters of the source length.</param>
    /// <param name="phase">The phase offset, in sixteenths of a sample.</param>
    /// <param name="filter">The interpolation kernel.</param>
    /// <param name="plan">Receives the source lines, relative to the first visible line, and the coefficients.</param>
    private static void PlanThreeQuarters(int targetLength, int phase, Av1InterpolationFilter filter, ref TapPlan plan)
    {
        // Reference: step_q4 = 16 * 4 / 3.
        const int step = 16 * 4 / 3;
        for (int output = 0; output < targetLength; output++)
        {
            int group = output / 3;
            int offset = phase + ((output - (3 * group)) * step);
            int center = (4 * group) + (offset >> SubpixelBits);
            ReadOnlySpan<short> kernel = GetKernel(filter, offset & SubpixelMask);
            for (int tap = 0; tap < Taps; tap++)
            {
                plan.Rows[(output * Taps) + tap] = center - (Taps / 2) + 1 + tap;
                plan.Coefficients[(output * Taps) + tap] = kernel[tap];
            }
        }
    }

    /// <summary>
    /// Plans the halving of a line: each output sample is the symmetric filter of the input samples around an even
    /// position, with positions past either end clamped to the end. An even-length line uses an eight-tap filter
    /// centered between two samples, and an odd-length one a seven-tap filter centered on a sample. Reference:
    /// down2_symeven() and down2_symodd(), with av1_down2_symeven_half_filter and av1_down2_symodd_half_filter.
    /// </summary>
    /// <param name="length">The input line length.</param>
    /// <param name="plan">Receives the input samples and the coefficients of each output sample.</param>
    private static void PlanDown2(int length, ref TapPlan plan)
    {
        bool odd = (length & 1) != 0;
        ReadOnlySpan<short> half = odd ? Down2SymmetricOddHalfFilter : Down2SymmetricEvenHalfFilter;
        for (int output = 0; output < plan.Count; output++)
        {
            int center = 2 * output;
            int tap = output * Taps;
            for (int k = 0; k < Taps; k++)
            {
                // Tap k reads center - 3 + k. The even filter weighs center - j and center + 1 + j by half[j]; the odd
                // filter weighs center - j and center + j by half[j], so its eighth tap has no weight.
                int position = center - 3 + k;
                int distance = odd ? Math.Abs(position - center) : position <= center ? center - position : position - center - 1;
                plan.Rows[tap + k] = Math.Clamp(position, 0, length - 1);
                plan.Coefficients[tap + k] = distance < half.Length && !(odd && k == Taps - 1) ? half[distance] : (short)0;
            }
        }
    }

    /// <summary>
    /// Plans the interpolation of a line to another length. Each output sample sits at a fixed step from the one
    /// before it, in units of a sixteen-thousandth of a sample, and reads eight input samples around its position with
    /// the kernel of its phase. Positions past either end are clamped to the end. The kernel bank is narrower for a
    /// larger reduction. Reference: interpolate_core(), highbd_interpolate_core() and choose_interp_filter().
    /// </summary>
    /// <param name="length">The input line length.</param>
    /// <param name="targetLength">The output line length.</param>
    /// <param name="plan">Receives the input samples and the coefficients of each output sample.</param>
    private static void PlanInterpolation(int length, int targetLength, ref TapPlan plan)
    {
        int delta = (int)((((uint)length << ResizeScaleSubpixelBits) + (uint)(targetLength / 2)) / (uint)targetLength);
        int offset = length > targetLength
            ? (((length - targetLength) << (ResizeScaleSubpixelBits - 1)) + (targetLength / 2)) / targetLength
            : -((((targetLength - length) << (ResizeScaleSubpixelBits - 1)) + (targetLength / 2)) / targetLength);

        ReadOnlySpan<short> bank = GetInterpolationBank(length, targetLength);
        int position = offset + ResizeScaleExtraOffset;
        for (int output = 0; output < targetLength; output++, position += delta)
        {
            int integer = position >> ResizeScaleSubpixelBits;
            int phase = (position >> ResizeScaleExtraBits) & ResizeSubpixelMask;
            int tap = output * Taps;
            for (int k = 0; k < Taps; k++)
            {
                plan.Rows[tap + k] = Math.Clamp(integer - (Taps / 2) + 1 + k, 0, length - 1);
                plan.Coefficients[tap + k] = bank[(phase * Taps) + k];
            }
        }
    }

    /// <summary>
    /// Returns the kernel of one phase of the interpolation filter that the normative resizer uses. Reference:
    /// av1_interp_filter_params_list.
    /// </summary>
    /// <param name="filter">The interpolation kernel: bilinear, smooth or regular.</param>
    /// <param name="phase">The phase, in sixteenths of a sample.</param>
    /// <returns>The eight coefficients.</returns>
    private static ReadOnlySpan<short> GetKernel(Av1InterpolationFilter filter, int phase) => filter switch
    {
        Av1InterpolationFilter.Bilinear => Av1TranslationalInterPredictor.BilinearOperator.GetCoefficients(phase, false),
        Av1InterpolationFilter.Smooth => Av1TranslationalInterPredictor.SmoothOperator.GetCoefficients(phase, false),
        _ => Av1TranslationalInterPredictor.RegularOperator.GetCoefficients(phase, false)
    };

    /// <summary>
    /// Copies a block of samples so that each source column becomes a destination row.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceOffset">The offset of the first sample of the block.</param>
    /// <param name="sourceStride">The distance between source rows.</param>
    /// <param name="destination">The destination samples.</param>
    /// <param name="destinationStride">The distance between destination rows.</param>
    /// <param name="sourceRows">The number of source rows, which is the length of each destination row.</param>
    /// <param name="sourceColumns">The number of source columns, which is the number of destination rows.</param>
    private static void Transpose<TSample>(
        ReadOnlySpan<TSample> source,
        int sourceOffset,
        int sourceStride,
        Span<TSample> destination,
        int destinationStride,
        int sourceRows,
        int sourceColumns)
        where TSample : unmanaged
    {
        // Blocks of 16x16 samples keep both the source and the destination rows in the cache.
        const int block = 16;
        for (int rowBlock = 0; rowBlock < sourceRows; rowBlock += block)
        {
            int rowEnd = Math.Min(rowBlock + block, sourceRows);
            for (int columnBlock = 0; columnBlock < sourceColumns; columnBlock += block)
            {
                int columnEnd = Math.Min(columnBlock + block, sourceColumns);
                for (int row = rowBlock; row < rowEnd; row++)
                {
                    ReadOnlySpan<TSample> sourceRow = source.Slice(sourceOffset + (row * sourceStride));
                    for (int column = columnBlock; column < columnEnd; column++)
                    {
                        destination[(column * destinationStride) + row] = sourceRow[column];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Names the eight input lines and the eight coefficients of each output line of one resize pass.
    /// </summary>
    private ref struct TapPlan
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TapPlan"/> struct.
        /// </summary>
        /// <param name="rows">The input line of each tap, eight per output line.</param>
        /// <param name="coefficients">The coefficient of each tap, eight per output line.</param>
        /// <param name="count">The number of output lines.</param>
        public TapPlan(Span<int> rows, Span<short> coefficients, int count)
        {
            this.Rows = rows;
            this.Coefficients = coefficients;
            this.Count = count;
        }

        /// <summary>
        /// Gets the input line of each tap, eight per output line.
        /// </summary>
        public Span<int> Rows { get; }

        /// <summary>
        /// Gets the coefficient of each tap, eight per output line.
        /// </summary>
        public Span<short> Coefficients { get; }

        /// <summary>
        /// Gets the number of output lines.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Returns the first and the last input line that any tap reads.
        /// </summary>
        /// <returns>The first and the last input line.</returns>
        public readonly (int First, int Last) GetRange()
        {
            int first = int.MaxValue;
            int last = int.MinValue;
            foreach (int row in this.Rows[..(this.Count * Taps)])
            {
                first = Math.Min(first, row);
                last = Math.Max(last, row);
            }

            return (first, last);
        }

        /// <summary>
        /// Moves every input line by the same amount.
        /// </summary>
        /// <param name="amount">The amount to add to each input line.</param>
        public readonly void Shift(int amount)
        {
            foreach (ref int row in this.Rows[..(this.Count * Taps)])
            {
                row += amount;
            }
        }
    }
}
