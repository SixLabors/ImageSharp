// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Retains ready modular samples in an owned buffer or a borrowed region.
/// </summary>
internal struct JxlModularChannel : IDisposable
{
    private Buffer2D<int>? ownedBuffer;
    private Buffer2D<short>? ownedShortBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularChannel"/> struct with its complete layout and sample region.
    /// </summary>
    /// <param name="layout">The validated logical layout.</param>
    /// <param name="region">The ready sample region, or an empty region only when the layout contains no samples.</param>
    /// <param name="ownedBuffer">The sample owner, or null for borrowed or empty channels.</param>
    private JxlModularChannel(JxlModularChannelLayout layout, Buffer2DRegion<int> region, Buffer2D<int>? ownedBuffer)
    {
        this.Layout = layout;
        this.Region = region;
        this.ownedBuffer = ownedBuffer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularChannel"/> struct with signed 16-bit storage.
    /// </summary>
    /// <param name="layout">The validated logical layout.</param>
    /// <param name="region">The ready short sample region, or empty storage for an empty layout.</param>
    /// <param name="ownedBuffer">The sample owner, or null for borrowed or empty channels.</param>
    private JxlModularChannel(JxlModularChannelLayout layout, Buffer2DRegion<short> region, Buffer2D<short>? ownedBuffer)
    {
        this.Layout = layout;
        this.Region16 = region;
        this.ownedShortBuffer = ownedBuffer;
        this.Uses16BitStorage = true;
    }

    /// <summary>
    /// Gets the logical dimensions and resolution of the ready samples.
    /// </summary>
    public JxlModularChannelLayout Layout { get; }

    /// <summary>
    /// Gets the logical dimensions.
    /// </summary>
    public Size Size => this.Layout.Size;

    /// <summary>
    /// Gets the logical width.
    /// </summary>
    public int Width => this.Layout.Width;

    /// <summary>
    /// Gets the logical height.
    /// </summary>
    public int Height => this.Layout.Height;

    /// <summary>
    /// Gets the horizontal base-two resolution shift, or minus one for metadata.
    /// </summary>
    public int HorizontalShift => this.Layout.HorizontalShift;

    /// <summary>
    /// Gets the vertical base-two resolution shift, or minus one for metadata.
    /// </summary>
    public int VerticalShift => this.Layout.VerticalShift;

    /// <summary>
    /// Gets a value indicating whether this channel contains palette metadata.
    /// </summary>
    public bool IsMetadata => this.Layout.IsMetadata;

    /// <summary>
    /// Gets a value indicating whether this channel contains no samples.
    /// </summary>
    public bool IsEmpty => this.Layout.IsEmpty;

    /// <summary>
    /// Gets the ready sample region. Empty channels have no backing samples; their logical shape remains in <see cref="Layout"/>.
    /// </summary>
    public Buffer2DRegion<int> Region { get; }

    /// <summary>
    /// Gets the ready signed 16-bit region when Uses16BitStorage is true.
    /// </summary>
    public Buffer2DRegion<short> Region16 { get; }

    /// <summary>
    /// Gets a value indicating whether this channel stores signed 16-bit samples instead of signed 32-bit samples.
    /// </summary>
    public bool Uses16BitStorage { get; }

    /// <summary>
    /// Borrows one row using the sample type selected before stream or channel traversal.
    /// </summary>
    /// <typeparam name="TSample">Short for 16-bit storage, or int for 32-bit storage.</typeparam>
    /// <param name="y">The validated row coordinate in this region.</param>
    /// <returns>The active typed row. The channel retains ownership of its storage.</returns>
    /// <remarks>
    /// The stream dispatcher supplies the sample type. This method only casts a span over that same primitive
    /// type; it does not reinterpret a Buffer2D, a managed reference or a row with another sample width.
    /// The JIT removes the type selection from each closed worker.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<TSample> GetRow<TSample>(int y)
        where TSample : unmanaged, IBinaryInteger<TSample>
        => typeof(TSample) == typeof(short)
            ? MemoryMarshal.Cast<short, TSample>(this.Region16.DangerousGetRowSpan(y))
            : MemoryMarshal.Cast<int, TSample>(this.Region.DangerousGetRowSpan(y));

    /// <summary>
    /// Borrows the wide row used for sequential prediction at this sample coordinate.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected before traversal.</typeparam>
    /// <param name="y">The validated row coordinate.</param>
    /// <param name="rows">Three int rows for short storage, or an empty span for int storage.</param>
    /// <returns>The wide current or preceding prediction row.</returns>
    /// <remarks>
    /// Short samples use the same three-row int window as jxl-rs modular decode/channel.rs.
    /// Keeping the full prediction result in that window preserves dependent arithmetic before the
    /// completed row is narrowed to storage. Int channels already supply their own wide rows.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<int> GetPredictionRow<TSample>(int y, Span<int> rows)
        where TSample : unmanaged, IBinaryInteger<TSample>
        => typeof(TSample) == typeof(short)
            ? rows.Slice((y % 3) * this.Width, this.Width)
            : this.Region.DangerousGetRowSpan(y);

    /// <summary>
    /// Stores one completed wide prediction row in its selected modular sample width.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected before traversal.</typeparam>
    /// <param name="y">The completed row coordinate.</param>
    /// <param name="row">The full signed int prediction results.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CommitPredictionRow<TSample>(int y, ReadOnlySpan<int> row)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        // Int prediction already writes directly into its final storage. Short storage retains
        // the wide row for subsequent prediction, then stores its low 16 bits with the shared SIMD API.
        if (typeof(TSample) == typeof(short))
        {
            TensorPrimitives.ConvertTruncating<int, TSample>(row, this.GetRow<TSample>(y));
        }
    }

    /// <summary>
    /// Creates a ready channel that owns zeroed signed 32-bit sample storage for its validated layout.
    /// </summary>
    /// <param name="allocator">The decoder's tracked sample allocator.</param>
    /// <param name="layout">The complete logical layout after transform parsing and validation.</param>
    /// <returns>A ready owned channel, or an empty channel that needs no sample allocation.</returns>
    public static JxlModularChannel Allocate(MemoryAllocator allocator, JxlModularChannelLayout layout)
        => Allocate(allocator, layout, false, AllocationOptions.Clean);

    /// <summary>
    /// Creates owned sample storage using the image's declared modular integer width.
    /// </summary>
    /// <param name="allocator">The decoder's tracked sample allocator.</param>
    /// <param name="layout">The complete validated layout.</param>
    /// <param name="use16BitStorage">True when the image guarantees signed 16-bit modular sample storage.</param>
    /// <param name="options">
    /// <see cref="AllocationOptions.Clean"/> when samples can be read before they are written.
    /// <see cref="AllocationOptions.None"/> when the caller writes every sample before any read.
    /// </param>
    /// <returns>The ready owned channel, or an empty channel for an empty layout.</returns>
    public static JxlModularChannel Allocate(MemoryAllocator allocator, JxlModularChannelLayout layout, bool use16BitStorage, AllocationOptions options)
    {
        // Empty palette and squeeze layouts are legal. Their ready sample set is empty, not an unallocated image.
        // Nonempty channels are constructed only after both their allocation and complete region exist.
        if (use16BitStorage)
        {
            if (layout.IsEmpty)
            {
                return new JxlModularChannel(layout, default(Buffer2DRegion<short>), null);
            }

            Buffer2D<short> shortBuffer = allocator.Allocate2D<short>(layout.Size, options);
            return new JxlModularChannel(layout, shortBuffer.GetRegion(), shortBuffer);
        }

        if (layout.IsEmpty)
        {
            return new JxlModularChannel(layout, default(Buffer2DRegion<int>), null);
        }

        Buffer2D<int> buffer = allocator.Allocate2D<int>(layout.Size, options);
        return new JxlModularChannel(layout, buffer.GetRegion(), buffer);
    }

    /// <summary>
    /// Borrows an untransformed frame region without taking ownership of its underlying samples.
    /// </summary>
    /// <param name="bounds">The validated rectangle in channel coordinates.</param>
    /// <returns>A ready borrowed channel. The source retains ownership of the sample memory.</returns>
    public JxlModularChannel Borrow(Rectangle bounds)
        => this.Borrow(bounds, this.HorizontalShift, this.VerticalShift);

    /// <summary>
    /// Borrows ready sample storage with the logical resolution required by another modular substream.
    /// </summary>
    /// <param name="bounds">The validated rectangle in the backing channel.</param>
    /// <param name="horizontalShift">The borrowed channel's horizontal resolution shift.</param>
    /// <param name="verticalShift">The borrowed channel's vertical resolution shift.</param>
    /// <returns>A ready borrowed channel. The source retains its sample allocation.</returns>
    public JxlModularChannel Borrow(Rectangle bounds, int horizontalShift, int verticalShift)
    {
        // DC samples and AC correlation metadata can borrow the same scratch allocation with different encoded shifts.
        // The new layout changes only the logical view; it does not resize or take ownership of the backing samples.
        JxlModularChannelLayout layout = new(bounds.Size, horizontalShift, verticalShift);
        if (this.Uses16BitStorage)
        {
            Buffer2DRegion<short> shortRegion = layout.IsEmpty ? default : this.Region16.GetSubRegion(bounds);
            return new JxlModularChannel(layout, shortRegion, null);
        }

        Buffer2DRegion<int> region = layout.IsEmpty ? default : this.Region.GetSubRegion(bounds);
        return new JxlModularChannel(layout, region, null);
    }

    /// <summary>
    /// Compares the dimensions and resolution used by transforms and cross-channel prediction.
    /// </summary>
    /// <param name="other">The ready channel to compare.</param>
    /// <returns>Whether both channels use the same logical sample layout.</returns>
    public bool HasSameShape(JxlModularChannel other) => this.Layout.HasSameShape(other.Layout);

    /// <inheritdoc/>
    public void Dispose()
    {
        // Borrowed and empty channels have no owner. Clear only this descriptor's ownership so failure cleanup
        // can revisit a consumed squeeze channel without returning its buffer twice.
        this.ownedBuffer?.Dispose();
        this.ownedBuffer = null;
        this.ownedShortBuffer?.Dispose();
        this.ownedShortBuffer = null;
    }

    /// <summary>
    /// Loads 4 stored samples as signed 32-bit SIMD lanes.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected for this channel.</typeparam>
    /// <param name="source">The ready typed sample row.</param>
    /// <param name="index">The first of 4 validated columns.</param>
    /// <returns>4 signed samples, without changing their column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Load128<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref TSample first = ref MemoryMarshal.GetReference(source);
        if (typeof(TSample) == typeof(short))
        {
            // Read exactly 4 shorts. Widen only this populated half, so the last SIMD block
            // needs no padding beyond the validated sample row.
            Vector64<short> packed = Vector64.LoadUnsafe(ref first, (nuint)index).As<TSample, short>();
            return Vector128.WidenLower(packed.ToVector128());
        }

        return Vector128.LoadUnsafe(ref first, (nuint)index).As<TSample, int>();
    }

    /// <summary>
    /// Stores 4 signed SIMD values in the channel's selected integer width.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected for this channel.</typeparam>
    /// <param name="destination">The ready typed output row.</param>
    /// <param name="index">The first of 4 validated columns.</param>
    /// <param name="value">The restored signed 32-bit values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store128<TSample>(Span<TSample> destination, int index, Vector128<int> value)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref TSample first = ref MemoryMarshal.GetReference(destination);
        if (typeof(TSample) == typeof(short))
        {
            // Narrowing retains the low 16 bits, as in the reference's signed short store.
            // Only the populated lower half is written; no following columns are modified.
            Vector64<short> packed = Vector128.Narrow(value, Vector128<int>.Zero).GetLower();
            packed.As<short, TSample>().StoreUnsafe(ref first, (nuint)index);
        }
        else
        {
            value.As<int, TSample>().StoreUnsafe(ref first, (nuint)index);
        }
    }

    /// <summary>
    /// Loads 8 stored samples as signed 32-bit SIMD lanes.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected for this channel.</typeparam>
    /// <param name="source">The ready typed sample row.</param>
    /// <param name="index">The first of 8 validated columns.</param>
    /// <returns>8 signed samples, without changing their column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Load256<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref TSample first = ref MemoryMarshal.GetReference(source);
        if (typeof(TSample) == typeof(short))
        {
            // Read exactly 8 shorts. Widen only this populated half, so the last SIMD block
            // needs no padding beyond the validated sample row.
            Vector128<short> packed = Vector128.LoadUnsafe(ref first, (nuint)index).As<TSample, short>();
            return Vector256.WidenLower(packed.ToVector256());
        }

        return Vector256.LoadUnsafe(ref first, (nuint)index).As<TSample, int>();
    }

    /// <summary>
    /// Stores 8 signed SIMD values in the channel's selected integer width.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected for this channel.</typeparam>
    /// <param name="destination">The ready typed output row.</param>
    /// <param name="index">The first of 8 validated columns.</param>
    /// <param name="value">The restored signed 32-bit values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store256<TSample>(Span<TSample> destination, int index, Vector256<int> value)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref TSample first = ref MemoryMarshal.GetReference(destination);
        if (typeof(TSample) == typeof(short))
        {
            // Narrowing retains the low 16 bits, as in the reference's signed short store.
            // Only the populated lower half is written; no following columns are modified.
            Vector128<short> packed = Vector256.Narrow(value, Vector256<int>.Zero).GetLower();
            packed.As<short, TSample>().StoreUnsafe(ref first, (nuint)index);
        }
        else
        {
            value.As<int, TSample>().StoreUnsafe(ref first, (nuint)index);
        }
    }

    /// <summary>
    /// Loads 16 stored samples as signed 32-bit SIMD lanes.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected for this channel.</typeparam>
    /// <param name="source">The ready typed sample row.</param>
    /// <param name="index">The first of 16 validated columns.</param>
    /// <returns>16 signed samples, without changing their column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> Load512<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref TSample first = ref MemoryMarshal.GetReference(source);
        if (typeof(TSample) == typeof(short))
        {
            // Read exactly 16 shorts. Widen only this populated half, so the last SIMD block
            // needs no padding beyond the validated sample row.
            Vector256<short> packed = Vector256.LoadUnsafe(ref first, (nuint)index).As<TSample, short>();
            return Vector512.WidenLower(packed.ToVector512());
        }

        return Vector512.LoadUnsafe(ref first, (nuint)index).As<TSample, int>();
    }

    /// <summary>
    /// Stores 16 signed SIMD values in the channel's selected integer width.
    /// </summary>
    /// <typeparam name="TSample">The short or int representation selected for this channel.</typeparam>
    /// <param name="destination">The ready typed output row.</param>
    /// <param name="index">The first of 16 validated columns.</param>
    /// <param name="value">The restored signed 32-bit values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store512<TSample>(Span<TSample> destination, int index, Vector512<int> value)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        ref TSample first = ref MemoryMarshal.GetReference(destination);
        if (typeof(TSample) == typeof(short))
        {
            // Narrowing retains the low 16 bits, as in the reference's signed short store.
            // Only the populated lower half is written; no following columns are modified.
            Vector256<short> packed = Vector512.Narrow(value, Vector512<int>.Zero).GetLower();
            packed.As<short, TSample>().StoreUnsafe(ref first, (nuint)index);
        }
        else
        {
            value.As<int, TSample>().StoreUnsafe(ref first, (nuint)index);
        }
    }
}
