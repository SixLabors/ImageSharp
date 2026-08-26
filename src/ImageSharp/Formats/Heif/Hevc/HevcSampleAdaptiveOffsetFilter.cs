// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Applies HEVC sample-adaptive offsets to reconstructed component blocks.
/// </summary>
/// <remarks>
/// Each SIMD lane classifies one reconstructed sample. Band-offset operators derive the class directly from the current
/// value, while edge-offset operators compare aligned lanes from the two neighboring coordinates. The resulting class
/// indices select one of the signaled offsets, after which addition and bit-depth clipping remain lane-wise. A scalar
/// continuation handles only incomplete vectors at picture edges.
/// </remarks>
internal static class HevcSampleAdaptiveOffsetFilter
{
    /// <summary>
    /// Defines the sample classifier shared by the SIMD row traversal and scalar tail.
    /// </summary>
    private interface ISampleClassifier
    {
        /// <summary>
        /// Gets a value indicating whether classification reads the two neighboring sample rows.
        /// </summary>
        public static abstract bool UsesNeighbors { get; }

        /// <summary>
        /// Classifies thirty-two current samples against their two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample lanes.</param>
        /// <param name="neighbor0">The first neighboring sample lanes.</param>
        /// <param name="neighbor1">The second neighboring sample lanes.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table indices.</returns>
        public static abstract Vector512<short> Classify(
            Vector512<short> current,
            Vector512<short> neighbor0,
            Vector512<short> neighbor1,
            in KernelParameters kernel);

        /// <summary>
        /// Classifies sixteen current samples against their two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample lanes.</param>
        /// <param name="neighbor0">The first neighboring sample lanes.</param>
        /// <param name="neighbor1">The second neighboring sample lanes.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table indices.</returns>
        public static abstract Vector256<short> Classify(
            Vector256<short> current,
            Vector256<short> neighbor0,
            Vector256<short> neighbor1,
            in KernelParameters kernel);

        /// <summary>
        /// Classifies eight current samples against their two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample lanes.</param>
        /// <param name="neighbor0">The first neighboring sample lanes.</param>
        /// <param name="neighbor1">The second neighboring sample lanes.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table indices.</returns>
        public static abstract Vector128<short> Classify(
            Vector128<short> current,
            Vector128<short> neighbor0,
            Vector128<short> neighbor1,
            in KernelParameters kernel);

        /// <summary>
        /// Classifies one current sample against its two classifier inputs.
        /// </summary>
        /// <param name="current">The current sample.</param>
        /// <param name="neighbor0">The first neighboring sample.</param>
        /// <param name="neighbor1">The second neighboring sample.</param>
        /// <param name="kernel">The scaled offset and band-class state.</param>
        /// <returns>The zero-based offset-table index.</returns>
        public static abstract int Classify(short current, short neighbor0, short neighbor1, in KernelParameters kernel);
    }

    /// <summary>
    /// Applies one resolved sample-adaptive-offset mode to a component coding-tree block.
    /// </summary>
    /// <param name="source">The immutable pre-SAO picture used for every classification.</param>
    /// <param name="destination">The picture receiving filtered samples.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The block's left coordinate in component samples.</param>
    /// <param name="y">The block's top coordinate in component samples.</param>
    /// <param name="width">The block width in component samples.</param>
    /// <param name="height">The block height in component samples.</param>
    /// <param name="parameters">The resolved coded offsets and classifier.</param>
    /// <param name="offsetScaleLog2">The component offset scale from the picture range-extension parameters.</param>
    /// <param name="leftAvailable">Whether classification may read the block immediately to the left.</param>
    /// <param name="rightAvailable">Whether classification may read the block immediately to the right.</param>
    /// <param name="aboveAvailable">Whether classification may read the block immediately above.</param>
    /// <param name="belowAvailable">Whether classification may read the block immediately below.</param>
    /// <param name="aboveLeftAvailable">Whether classification may read the upper-left diagonal block.</param>
    /// <param name="aboveRightAvailable">Whether classification may read the upper-right diagonal block.</param>
    /// <param name="belowLeftAvailable">Whether classification may read the lower-left diagonal block.</param>
    /// <param name="belowRightAvailable">Whether classification may read the lower-right diagonal block.</param>
    public static void ApplyBlock(
        HevcPictureBuffer source,
        HevcPictureBuffer destination,
        HevcPlane plane,
        int x,
        int y,
        int width,
        int height,
        in HevcSampleAdaptiveOffsetParameters parameters,
        int offsetScaleLog2,
        bool leftAvailable,
        bool rightAvailable,
        bool aboveAvailable,
        bool belowAvailable,
        bool aboveLeftAvailable,
        bool aboveRightAvailable,
        bool belowLeftAvailable,
        bool belowRightAvailable)
    {
        if (parameters.Type == HevcSampleAdaptiveOffsetType.Off)
        {
            return;
        }

        KernelParameters kernel = new(parameters, source.GetBitDepth(plane), offsetScaleLog2);
        switch (parameters.Type)
        {
            case HevcSampleAdaptiveOffsetType.Band:
                ApplyBand(source, destination, plane, x, y, width, height, in kernel);
                break;
            case HevcSampleAdaptiveOffsetType.EdgeHorizontal:
                ApplyHorizontalEdges(source, destination, plane, x, y, width, height, leftAvailable, rightAvailable, in kernel);
                break;
            case HevcSampleAdaptiveOffsetType.EdgeVertical:
                ApplyVerticalEdges(source, destination, plane, x, y, width, height, aboveAvailable, belowAvailable, in kernel);
                break;
            case HevcSampleAdaptiveOffsetType.EdgeDescending:
                ApplyDescendingEdges(
                    source,
                    destination,
                    plane,
                    x,
                    y,
                    width,
                    height,
                    leftAvailable,
                    rightAvailable,
                    aboveAvailable,
                    belowAvailable,
                    aboveLeftAvailable,
                    belowRightAvailable,
                    in kernel);
                break;
            case HevcSampleAdaptiveOffsetType.EdgeAscending:
                ApplyAscendingEdges(
                    source,
                    destination,
                    plane,
                    x,
                    y,
                    width,
                    height,
                    leftAvailable,
                    rightAvailable,
                    aboveAvailable,
                    belowAvailable,
                    aboveRightAvailable,
                    belowLeftAvailable,
                    in kernel);
                break;
        }
    }

    /// <summary>
    /// Applies band offsets to every sample in a component block.
    /// </summary>
    /// <param name="source">The immutable pre-SAO picture.</param>
    /// <param name="destination">The destination picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The block's left coordinate.</param>
    /// <param name="y">The block's top coordinate.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="kernel">The scaled offset and band-class state.</param>
    private static void ApplyBand(
        HevcPictureBuffer source,
        HevcPictureBuffer destination,
        HevcPlane plane,
        int x,
        int y,
        int width,
        int height,
        in KernelParameters kernel)
    {
        for (int row = y; row < y + height; row++)
        {
            ReadOnlySpan<ushort> sourceRow = source.GetRowSpan(plane, row).Slice(x, width);
            Span<ushort> destinationRow = destination.GetRowSpan(plane, row).Slice(x, width);

            // Band classification depends only on the current sample. The closed classifier's UsesNeighbors value removes
            // the two neighbor loads when this generic traversal is specialized for BandClassifier.
            ApplyRow<BandClassifier>(sourceRow, sourceRow, sourceRow, destinationRow, in kernel);
        }
    }

    /// <summary>
    /// Applies horizontal edge offsets within the available left and right boundaries.
    /// </summary>
    /// <param name="source">The immutable pre-SAO picture.</param>
    /// <param name="destination">The destination picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The block's left coordinate.</param>
    /// <param name="y">The block's top coordinate.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="leftAvailable">Whether the left neighboring block is available.</param>
    /// <param name="rightAvailable">Whether the right neighboring block is available.</param>
    /// <param name="kernel">The scaled offset state.</param>
    private static void ApplyHorizontalEdges(
        HevcPictureBuffer source,
        HevcPictureBuffer destination,
        HevcPlane plane,
        int x,
        int y,
        int width,
        int height,
        bool leftAvailable,
        bool rightAvailable,
        in KernelParameters kernel)
    {
        int start = x + (leftAvailable ? 0 : 1);
        int end = x + width - (rightAvailable ? 0 : 1);
        int count = end - start;
        if (count <= 0)
        {
            return;
        }

        for (int row = y; row < y + height; row++)
        {
            ReadOnlySpan<ushort> sourceRow = source.GetRowSpan(plane, row);
            ApplyRow<EdgeClassifier>(
                sourceRow.Slice(start, count),
                sourceRow.Slice(start - 1, count),
                sourceRow.Slice(start + 1, count),
                destination.GetRowSpan(plane, row).Slice(start, count),
                in kernel);
        }
    }

    /// <summary>
    /// Applies vertical edge offsets within the available upper and lower boundaries.
    /// </summary>
    /// <param name="source">The immutable pre-SAO picture.</param>
    /// <param name="destination">The destination picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The block's left coordinate.</param>
    /// <param name="y">The block's top coordinate.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="aboveAvailable">Whether the upper neighboring block is available.</param>
    /// <param name="belowAvailable">Whether the lower neighboring block is available.</param>
    /// <param name="kernel">The scaled offset state.</param>
    private static void ApplyVerticalEdges(
        HevcPictureBuffer source,
        HevcPictureBuffer destination,
        HevcPlane plane,
        int x,
        int y,
        int width,
        int height,
        bool aboveAvailable,
        bool belowAvailable,
        in KernelParameters kernel)
    {
        int start = y + (aboveAvailable ? 0 : 1);
        int end = y + height - (belowAvailable ? 0 : 1);
        for (int row = start; row < end; row++)
        {
            ApplyRow<EdgeClassifier>(
                source.GetRowSpan(plane, row).Slice(x, width),
                source.GetRowSpan(plane, row - 1).Slice(x, width),
                source.GetRowSpan(plane, row + 1).Slice(x, width),
                destination.GetRowSpan(plane, row).Slice(x, width),
                in kernel);
        }
    }

    /// <summary>
    /// Applies descending-diagonal edge offsets within the eight resolved block boundaries.
    /// </summary>
    /// <param name="source">The immutable pre-SAO picture.</param>
    /// <param name="destination">The destination picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The block's left coordinate.</param>
    /// <param name="y">The block's top coordinate.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="leftAvailable">Whether the left neighboring block is available.</param>
    /// <param name="rightAvailable">Whether the right neighboring block is available.</param>
    /// <param name="aboveAvailable">Whether the upper neighboring block is available.</param>
    /// <param name="belowAvailable">Whether the lower neighboring block is available.</param>
    /// <param name="aboveLeftAvailable">Whether the upper-left neighboring block is available.</param>
    /// <param name="belowRightAvailable">Whether the lower-right neighboring block is available.</param>
    /// <param name="kernel">The scaled offset state.</param>
    private static void ApplyDescendingEdges(
        HevcPictureBuffer source,
        HevcPictureBuffer destination,
        HevcPlane plane,
        int x,
        int y,
        int width,
        int height,
        bool leftAvailable,
        bool rightAvailable,
        bool aboveAvailable,
        bool belowAvailable,
        bool aboveLeftAvailable,
        bool belowRightAvailable,
        in KernelParameters kernel)
    {
        int commonStart = x + (leftAvailable ? 0 : 1);
        int commonEnd = x + width - (rightAvailable ? 0 : 1);
        int lastRow = y + height - 1;
        for (int row = y; row <= lastRow; row++)
        {
            int start = commonStart;
            int end = commonEnd;
            if (row == y)
            {
                start = aboveLeftAvailable ? x : x + 1;
                end = aboveAvailable ? commonEnd : x + 1;
            }

            if (row == lastRow)
            {
                start = Math.Max(start, belowAvailable ? commonStart : x + width - 1);
                end = Math.Min(end, belowRightAvailable ? x + width : x + width - 1);
            }

            int count = end - start;
            if (count <= 0)
            {
                continue;
            }

            ApplyRow<EdgeClassifier>(
                source.GetRowSpan(plane, row).Slice(start, count),
                source.GetRowSpan(plane, row - 1).Slice(start - 1, count),
                source.GetRowSpan(plane, row + 1).Slice(start + 1, count),
                destination.GetRowSpan(plane, row).Slice(start, count),
                in kernel);
        }
    }

    /// <summary>
    /// Applies ascending-diagonal edge offsets within the eight resolved block boundaries.
    /// </summary>
    /// <param name="source">The immutable pre-SAO picture.</param>
    /// <param name="destination">The destination picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The block's left coordinate.</param>
    /// <param name="y">The block's top coordinate.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="leftAvailable">Whether the left neighboring block is available.</param>
    /// <param name="rightAvailable">Whether the right neighboring block is available.</param>
    /// <param name="aboveAvailable">Whether the upper neighboring block is available.</param>
    /// <param name="belowAvailable">Whether the lower neighboring block is available.</param>
    /// <param name="aboveRightAvailable">Whether the upper-right neighboring block is available.</param>
    /// <param name="belowLeftAvailable">Whether the lower-left neighboring block is available.</param>
    /// <param name="kernel">The scaled offset state.</param>
    private static void ApplyAscendingEdges(
        HevcPictureBuffer source,
        HevcPictureBuffer destination,
        HevcPlane plane,
        int x,
        int y,
        int width,
        int height,
        bool leftAvailable,
        bool rightAvailable,
        bool aboveAvailable,
        bool belowAvailable,
        bool aboveRightAvailable,
        bool belowLeftAvailable,
        in KernelParameters kernel)
    {
        int commonStart = x + (leftAvailable ? 0 : 1);
        int commonEnd = x + width - (rightAvailable ? 0 : 1);
        int lastRow = y + height - 1;
        for (int row = y; row <= lastRow; row++)
        {
            int start = commonStart;
            int end = commonEnd;
            if (row == y)
            {
                start = aboveAvailable ? commonStart : x + width - 1;
                end = aboveRightAvailable ? x + width : x + width - 1;
            }

            if (row == lastRow)
            {
                start = Math.Max(start, belowLeftAvailable ? x : x + 1);
                end = Math.Min(end, belowAvailable ? commonEnd : x + 1);
            }

            int count = end - start;
            if (count <= 0)
            {
                continue;
            }

            ApplyRow<EdgeClassifier>(
                source.GetRowSpan(plane, row).Slice(start, count),
                source.GetRowSpan(plane, row - 1).Slice(start + 1, count),
                source.GetRowSpan(plane, row + 1).Slice(start - 1, count),
                destination.GetRowSpan(plane, row).Slice(start, count),
                in kernel);
        }
    }

    /// <summary>
    /// Applies one closed classifier to a contiguous row range using every accelerated SIMD width before the scalar tail.
    /// </summary>
    /// <typeparam name="TClassifier">The band or edge classifier selected before entering the row.</typeparam>
    /// <param name="current">The current source samples.</param>
    /// <param name="neighbor0">The first classifier input samples.</param>
    /// <param name="neighbor1">The second classifier input samples.</param>
    /// <param name="destination">The destination samples.</param>
    /// <param name="kernel">The scaled offset and clamp state.</param>
    private static void ApplyRow<TClassifier>(
        ReadOnlySpan<ushort> current,
        ReadOnlySpan<ushort> neighbor0,
        ReadOnlySpan<ushort> neighbor1,
        Span<ushort> destination,
        in KernelParameters kernel)
        where TClassifier : struct, ISampleClassifier
    {
        ref ushort currentBase = ref MemoryMarshal.GetReference(current);
        ref ushort neighbor0Base = ref MemoryMarshal.GetReference(neighbor0);
        ref ushort neighbor1Base = ref MemoryMarshal.GetReference(neighbor1);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        // HEVC's exposed 8/10/12-bit profiles keep every sample and scaled offset inside Int16. Signed lanes therefore
        // provide comparisons, addition, and saturation without the two widening stages an Int32 implementation needs.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<short> minimum = Vector512<short>.Zero;
            Vector512<short> maximum = Vector512.Create(kernel.Maximum);
            for (; index <= current.Length - Vector512<ushort>.Count; index += Vector512<ushort>.Count)
            {
                Vector512<short> value = Vector512.LoadUnsafe(ref currentBase, (nuint)index).AsInt16();
                Vector512<short> first = TClassifier.UsesNeighbors ? Vector512.LoadUnsafe(ref neighbor0Base, (nuint)index).AsInt16() : default;
                Vector512<short> second = TClassifier.UsesNeighbors ? Vector512.LoadUnsafe(ref neighbor1Base, (nuint)index).AsInt16() : default;
                Vector512<short> classes = TClassifier.Classify(value, first, second, in kernel);
                Vector512<short> filtered = Vector512.Clamp(value + SelectOffset(classes, in kernel), minimum, maximum);
                filtered.AsUInt16().StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<short> minimum = Vector256<short>.Zero;
            Vector256<short> maximum = Vector256.Create(kernel.Maximum);
            for (; index <= current.Length - Vector256<ushort>.Count; index += Vector256<ushort>.Count)
            {
                Vector256<short> value = Vector256.LoadUnsafe(ref currentBase, (nuint)index).AsInt16();
                Vector256<short> first = TClassifier.UsesNeighbors ? Vector256.LoadUnsafe(ref neighbor0Base, (nuint)index).AsInt16() : default;
                Vector256<short> second = TClassifier.UsesNeighbors ? Vector256.LoadUnsafe(ref neighbor1Base, (nuint)index).AsInt16() : default;
                Vector256<short> classes = TClassifier.Classify(value, first, second, in kernel);
                Vector256<short> filtered = Vector256.Clamp(value + SelectOffset(classes, in kernel), minimum, maximum);
                filtered.AsUInt16().StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<short> minimum = Vector128<short>.Zero;
            Vector128<short> maximum = Vector128.Create(kernel.Maximum);
            for (; index <= current.Length - Vector128<ushort>.Count; index += Vector128<ushort>.Count)
            {
                Vector128<short> value = Vector128.LoadUnsafe(ref currentBase, (nuint)index).AsInt16();
                Vector128<short> first = TClassifier.UsesNeighbors ? Vector128.LoadUnsafe(ref neighbor0Base, (nuint)index).AsInt16() : default;
                Vector128<short> second = TClassifier.UsesNeighbors ? Vector128.LoadUnsafe(ref neighbor1Base, (nuint)index).AsInt16() : default;
                Vector128<short> classes = TClassifier.Classify(value, first, second, in kernel);
                Vector128<short> filtered = Vector128.Clamp(value + SelectOffset(classes, in kernel), minimum, maximum);
                filtered.AsUInt16().StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        for (; index < current.Length; index++)
        {
            short currentValue = (short)Unsafe.Add(ref currentBase, index);
            short first = TClassifier.UsesNeighbors ? (short)Unsafe.Add(ref neighbor0Base, index) : default;
            short second = TClassifier.UsesNeighbors ? (short)Unsafe.Add(ref neighbor1Base, index) : default;
            int offsetIndex = TClassifier.Classify(currentValue, first, second, in kernel);

            int filtered = Unsafe.Add(ref currentBase, index) + SelectOffset(offsetIndex, in kernel);
            Unsafe.Add(ref destinationBase, index) = (ushort)Math.Clamp(filtered, 0, kernel.Maximum);
        }
    }

    /// <summary>
    /// Selects one of five signed offsets for thirty-two classifier indices.
    /// </summary>
    /// <param name="classes">The zero-based classifier indices.</param>
    /// <param name="kernel">The five scaled offsets.</param>
    /// <returns>The selected signed offset in every lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> SelectOffset(Vector512<short> classes, in KernelParameters kernel)
    {
        // The table contains only five values and there is no portable 16-bit gather. A comparison chain keeps every
        // class lane in registers and leaves unrecognized classes at the required zero offset.
        Vector512<short> selected = Vector512<short>.Zero;
        selected = Vector512.ConditionalSelect(Vector512.Equals(classes, Vector512.Create((short)0)), Vector512.Create(kernel.Offset0), selected);
        selected = Vector512.ConditionalSelect(Vector512.Equals(classes, Vector512.Create((short)1)), Vector512.Create(kernel.Offset1), selected);
        selected = Vector512.ConditionalSelect(Vector512.Equals(classes, Vector512.Create((short)2)), Vector512.Create(kernel.Offset2), selected);
        selected = Vector512.ConditionalSelect(Vector512.Equals(classes, Vector512.Create((short)3)), Vector512.Create(kernel.Offset3), selected);
        return Vector512.ConditionalSelect(Vector512.Equals(classes, Vector512.Create((short)4)), Vector512.Create(kernel.Offset4), selected);
    }

    /// <summary>
    /// Selects one of five signed offsets for sixteen classifier indices.
    /// </summary>
    /// <param name="classes">The zero-based classifier indices.</param>
    /// <param name="kernel">The five scaled offsets.</param>
    /// <returns>The selected signed offset in every lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> SelectOffset(Vector256<short> classes, in KernelParameters kernel)
    {
        Vector256<short> selected = Vector256<short>.Zero;
        selected = Vector256.ConditionalSelect(Vector256.Equals(classes, Vector256.Create((short)0)), Vector256.Create(kernel.Offset0), selected);
        selected = Vector256.ConditionalSelect(Vector256.Equals(classes, Vector256.Create((short)1)), Vector256.Create(kernel.Offset1), selected);
        selected = Vector256.ConditionalSelect(Vector256.Equals(classes, Vector256.Create((short)2)), Vector256.Create(kernel.Offset2), selected);
        selected = Vector256.ConditionalSelect(Vector256.Equals(classes, Vector256.Create((short)3)), Vector256.Create(kernel.Offset3), selected);
        return Vector256.ConditionalSelect(Vector256.Equals(classes, Vector256.Create((short)4)), Vector256.Create(kernel.Offset4), selected);
    }

    /// <summary>
    /// Selects one of five signed offsets for eight classifier indices.
    /// </summary>
    /// <param name="classes">The zero-based classifier indices.</param>
    /// <param name="kernel">The five scaled offsets.</param>
    /// <returns>The selected signed offset in every lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> SelectOffset(Vector128<short> classes, in KernelParameters kernel)
    {
        Vector128<short> selected = Vector128<short>.Zero;
        selected = Vector128.ConditionalSelect(Vector128.Equals(classes, Vector128.Create((short)0)), Vector128.Create(kernel.Offset0), selected);
        selected = Vector128.ConditionalSelect(Vector128.Equals(classes, Vector128.Create((short)1)), Vector128.Create(kernel.Offset1), selected);
        selected = Vector128.ConditionalSelect(Vector128.Equals(classes, Vector128.Create((short)2)), Vector128.Create(kernel.Offset2), selected);
        selected = Vector128.ConditionalSelect(Vector128.Equals(classes, Vector128.Create((short)3)), Vector128.Create(kernel.Offset3), selected);
        return Vector128.ConditionalSelect(Vector128.Equals(classes, Vector128.Create((short)4)), Vector128.Create(kernel.Offset4), selected);
    }

    /// <summary>
    /// Selects one of five signed offsets for one classifier index.
    /// </summary>
    /// <param name="classification">The zero-based classifier index.</param>
    /// <param name="kernel">The five scaled offsets.</param>
    /// <returns>The selected signed offset, or zero for an unmodified class.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectOffset(int classification, in KernelParameters kernel)
        => classification switch
        {
            0 => kernel.Offset0,
            1 => kernel.Offset1,
            2 => kernel.Offset2,
            3 => kernel.Offset3,
            4 => kernel.Offset4,
            _ => 0,
        };

    /// <summary>
    /// Classifies samples by one of thirty-two most-significant-value bands.
    /// </summary>
    private readonly struct BandClassifier : ISampleClassifier
    {
        /// <inheritdoc/>
        public static bool UsesNeighbors => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Classify(
            Vector512<short> current,
            Vector512<short> neighbor0,
            Vector512<short> neighbor1,
            in KernelParameters kernel)
            => (Vector512.ShiftRightArithmetic(current, kernel.BandShift) - Vector512.Create(kernel.BandPosition)) & Vector512.Create((short)31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Classify(
            Vector256<short> current,
            Vector256<short> neighbor0,
            Vector256<short> neighbor1,
            in KernelParameters kernel)
            => (Vector256.ShiftRightArithmetic(current, kernel.BandShift) - Vector256.Create(kernel.BandPosition)) & Vector256.Create((short)31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Classify(
            Vector128<short> current,
            Vector128<short> neighbor0,
            Vector128<short> neighbor1,
            in KernelParameters kernel)
            => (Vector128.ShiftRightArithmetic(current, kernel.BandShift) - Vector128.Create(kernel.BandPosition)) & Vector128.Create((short)31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Classify(short current, short neighbor0, short neighbor1, in KernelParameters kernel)
            => ((current >> kernel.BandShift) - kernel.BandPosition) & 31;
    }

    /// <summary>
    /// Classifies samples by the sum of their signs relative to two directional neighbors.
    /// </summary>
    private readonly struct EdgeClassifier : ISampleClassifier
    {
        /// <inheritdoc/>
        public static bool UsesNeighbors => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Classify(
            Vector512<short> current,
            Vector512<short> neighbor0,
            Vector512<short> neighbor1,
            in KernelParameters kernel)
        {
            // Each comparison pair produces -1, 0, or 1. Adding two maps the normative edge classes onto the
            // contiguous zero-through-four offset-table indices used by the selection kernel.
            Vector512<short> one = Vector512.Create((short)1);
            Vector512<short> sign0 = (Vector512.GreaterThan(current, neighbor0) & one) - (Vector512.LessThan(current, neighbor0) & one);
            Vector512<short> sign1 = (Vector512.GreaterThan(current, neighbor1) & one) - (Vector512.LessThan(current, neighbor1) & one);
            return sign0 + sign1 + Vector512.Create((short)2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Classify(
            Vector256<short> current,
            Vector256<short> neighbor0,
            Vector256<short> neighbor1,
            in KernelParameters kernel)
        {
            Vector256<short> one = Vector256.Create((short)1);
            Vector256<short> sign0 = (Vector256.GreaterThan(current, neighbor0) & one) - (Vector256.LessThan(current, neighbor0) & one);
            Vector256<short> sign1 = (Vector256.GreaterThan(current, neighbor1) & one) - (Vector256.LessThan(current, neighbor1) & one);
            return sign0 + sign1 + Vector256.Create((short)2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Classify(
            Vector128<short> current,
            Vector128<short> neighbor0,
            Vector128<short> neighbor1,
            in KernelParameters kernel)
        {
            Vector128<short> one = Vector128.Create((short)1);
            Vector128<short> sign0 = (Vector128.GreaterThan(current, neighbor0) & one) - (Vector128.LessThan(current, neighbor0) & one);
            Vector128<short> sign1 = (Vector128.GreaterThan(current, neighbor1) & one) - (Vector128.LessThan(current, neighbor1) & one);
            return sign0 + sign1 + Vector128.Create((short)2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Classify(short current, short neighbor0, short neighbor1, in KernelParameters kernel)
            => Math.Sign(current - neighbor0) + Math.Sign(current - neighbor1) + 2;
    }

    /// <summary>
    /// Contains one block's scaled offsets and invariant classification values.
    /// </summary>
    private readonly struct KernelParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KernelParameters"/> struct.
        /// </summary>
        /// <param name="parameters">The decoded signed offsets.</param>
        /// <param name="bitDepth">The component sample precision.</param>
        /// <param name="offsetScaleLog2">The component offset scale.</param>
        public KernelParameters(in HevcSampleAdaptiveOffsetParameters parameters, int bitDepth, int offsetScaleLog2)
        {
            // Range Extensions scales each coded offset once before filtering. Hoisting the shifts here keeps the
            // classification loops to comparisons, table selection, one addition, and saturation.
            this.Offset0 = (short)(parameters.Offset0 << offsetScaleLog2);
            this.Offset1 = (short)(parameters.Offset1 << offsetScaleLog2);
            this.Offset2 = (short)(parameters.Offset2 << offsetScaleLog2);
            this.Offset3 = (short)(parameters.Offset3 << offsetScaleLog2);
            this.Offset4 = (short)(parameters.Offset4 << offsetScaleLog2);
            this.BandPosition = (short)parameters.BandPosition;
            this.BandShift = bitDepth - 5;
            this.Maximum = (short)((1 << bitDepth) - 1);
        }

        /// <summary>
        /// Gets the first scaled class offset.
        /// </summary>
        public short Offset0 { get; }

        /// <summary>
        /// Gets the second scaled class offset.
        /// </summary>
        public short Offset1 { get; }

        /// <summary>
        /// Gets the third scaled class offset.
        /// </summary>
        public short Offset2 { get; }

        /// <summary>
        /// Gets the fourth scaled class offset.
        /// </summary>
        public short Offset3 { get; }

        /// <summary>
        /// Gets the fifth scaled class offset.
        /// </summary>
        public short Offset4 { get; }

        /// <summary>
        /// Gets the first active band class.
        /// </summary>
        public short BandPosition { get; }

        /// <summary>
        /// Gets the number of low sample bits discarded to form one of thirty-two band classes.
        /// </summary>
        public int BandShift { get; }

        /// <summary>
        /// Gets the largest component sample value.
        /// </summary>
        public short Maximum { get; }
    }
}
