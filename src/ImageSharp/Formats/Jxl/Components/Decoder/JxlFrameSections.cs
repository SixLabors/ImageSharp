// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Owns one frame's section sizes and optional logical-to-physical section order.
/// </summary>
/// <remarks>
/// Sizes are stored in physical codestream order. An empty order means that logical and physical indices are equal.
/// Identify uses the same parser without retaining either array and skips the combined payload through the bit reader.
/// </remarks>
internal sealed class JxlFrameSections : IDisposable
{
    /// <summary>
    /// The two-bit U32 selector and its shortest ten-bit payload require twelve bits per size entry.
    /// </summary>
    private const int MinimumBitsPerEntry = 12;

    /// <summary>
    /// The interval between cancellation checks while reading potentially large section tables and permutations.
    /// </summary>
    private const int CancellationInterval = 1024;

    private readonly IMemoryOwner<uint> sizes;
    private readonly IMemoryOwner<uint>? order;

    /// <summary>
    /// Indicates whether this owner has released its section tables.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlFrameSections"/> class and takes ownership of its completed arrays.
    /// </summary>
    /// <param name="sizes">The physical section sizes.</param>
    /// <param name="order">The logical-to-physical indices, or null for identity order.</param>
    /// <param name="payloadOffset">The logical codestream byte offset of the first physical section.</param>
    /// <param name="totalLength">The combined section payload length, in bytes.</param>
    private JxlFrameSections(IMemoryOwner<uint> sizes, IMemoryOwner<uint>? order, long payloadOffset, long totalLength)
    {
        this.sizes = sizes;
        this.order = order;
        this.PayloadOffset = payloadOffset;
        this.TotalLength = totalLength;
    }

    /// <summary>
    /// Gets the section payload sizes in physical codestream order. Cache its span before traversing the table.
    /// </summary>
    public ReadOnlyMemory<uint> Sizes => this.sizes.Memory;

    /// <summary>
    /// Gets the physical index for each logical section. An empty memory means identity order.
    /// </summary>
    public ReadOnlyMemory<uint> Order => this.order is null ? ReadOnlyMemory<uint>.Empty : this.order.Memory;

    /// <summary>
    /// Gets the logical codestream byte offset of the first physical section.
    /// </summary>
    public long PayloadOffset { get; }

    /// <summary>
    /// Gets the combined section payload length, in bytes.
    /// </summary>
    public long TotalLength { get; }

    /// <summary>
    /// Reads the section table of a frame and keeps the sizes and the order that the reconstruction needs.
    /// </summary>
    /// <param name="reader">The bit reader, at the position after the frame header.</param>
    /// <param name="allocator">The memory allocator of the decoder.</param>
    /// <param name="sectionCount">The positive entry count from the checked frame header.</param>
    /// <param name="handling">The policy for a damaged or truncated table after the image exists.</param>
    /// <param name="imageExists">Whether the decoder created the image before this frame. Before that, each table error throws.</param>
    /// <param name="cancellationToken">The token that cancels the table processing.</param>
    /// <returns>
    /// The table owner, or null if the mode ignores a table error. The reader stays at the start of the section payloads.
    /// </returns>
    public static JxlFrameSections? Read(
        ref JxlBitReader reader,
        MemoryAllocator allocator,
        int sectionCount,
        SegmentIntegrityHandling handling,
        bool imageExists,
        CancellationToken cancellationToken)
    {
        long length = ReadTable(
            ref reader, allocator, sectionCount, true, handling, imageExists, cancellationToken, out IMemoryOwner<uint>? sizes, out IMemoryOwner<uint>? order);

        // ReadTable returns the owners only for a complete table. The new object owns both arrays.
        return length < 0 ? null : new JxlFrameSections(sizes!, order, reader.BytePosition, length);
    }

    /// <summary>
    /// Checks the section table of a frame and skips its payloads. The method does not keep the sizes or construct a permutation.
    /// </summary>
    /// <param name="reader">The bit reader, at the position after the frame header.</param>
    /// <param name="allocator">The allocator of the decoder for the entropy tables and the optional LZ77 history.</param>
    /// <param name="sectionCount">The positive entry count from the checked frame header.</param>
    /// <param name="handling">The policy for an incomplete payload, and for a table error after the first displayed frame.</param>
    /// <param name="imageExists">Whether a displayed frame comes before this frame. Before that, each table error throws.</param>
    /// <param name="cancellationToken">The token that cancels the table processing.</param>
    /// <returns>Whether the method skipped all payload bytes. False occurs only in the IgnoreImageData mode.</returns>
    public static bool Skip(
        ref JxlBitReader reader,
        MemoryAllocator allocator,
        int sectionCount,
        SegmentIntegrityHandling handling,
        bool imageExists,
        CancellationToken cancellationToken)
    {
        long length = ReadTable(ref reader, allocator, sectionCount, false, handling, imageExists, cancellationToken, out _, out _);
        if (length < 0)
        {
            return false;
        }

        bool complete = reader.TrySkipBytes(length);
        if (!complete)
        {
            // A short payload is truncated image data. Strict and IgnoreAncillary throw here.
            handling.ThrowOrIgnoreImageDataError("The JPEG XL section payload is truncated.");
        }

        return complete;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.order?.Dispose();
        this.sizes.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    /// Parses the shared table syntax. If <paramref name="retain"/> is <see langword="true"/>, the method keeps the sizes and uses their storage as the
    /// permutation workspace.
    /// </summary>
    /// <param name="reader">The bit reader, at the position after the frame header.</param>
    /// <param name="allocator">The allocator of the decoder.</param>
    /// <param name="sectionCount">The positive number of entries.</param>
    /// <param name="retain">Whether to keep the sizes and the order for the reconstruction. Identify keeps neither.</param>
    /// <param name="handling">The policy for a damaged or truncated table after the image exists.</param>
    /// <param name="imageExists">Whether the decoder created the image before this frame.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="sizes">The retained size owner, or null if the method does not keep the sizes or the table is not complete.</param>
    /// <param name="order">The optional order owner, or null for identity order or a table that is not complete.</param>
    /// <returns>The combined section payload length in bytes, or -1 if the mode ignores a table error.</returns>
    private static long ReadTable(
        ref JxlBitReader reader,
        MemoryAllocator allocator,
        int sectionCount,
        bool retain,
        SegmentIntegrityHandling handling,
        bool imageExists,
        CancellationToken cancellationToken,
        out IMemoryOwner<uint>? sizes,
        out IMemoryOwner<uint>? order)
    {
        sizes = null;
        order = null;

        // Each entry needs at least twelve bits: the two-bit U32 selector and its shortest ten-bit payload. One permutation flag comes before the sizes.
        // The method checks this minimum once, before an allocation from the image-derived entry count.
        // This check also limits the size loop below to the actual input.
        if (!reader.CheckBitBudget(((ulong)sectionCount * MinimumBitsPerEntry) + 1))
        {
            handling.ThrowOrIgnoreImageDataError(imageExists, "The JPEG XL section table is truncated.");
            return -1;
        }

        try
        {
            sizes = retain ? allocator.Allocate<uint>(sectionCount) : null;
            Span<uint> sizeValues = sizes is null ? [] : sizes.Memory.Span;
            if (reader.ReadBoolean())
            {
                order = ReadPermutation(ref reader, allocator, sectionCount, sizeValues, cancellationToken);
            }

            bool zeroPadding = reader.AlignToByte();
            long total = 0;
            for (int i = 0; i < sectionCount; i++)
            {
                if ((i & (CancellationInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                uint size = reader.ReadU32((10, 0), (14, 1024), (22, 17408), (30, 4211712));
                if (!sizeValues.IsEmpty)
                {
                    sizeValues[i] = size;
                }

                // Each U32 choice is at most 4211712 + (2^30-1). Even int.MaxValue such entries fit in a long.
                // Keep the sum in bytes: converting that maximum total to bits could overflow an unsigned 64-bit count.
                total += size;
            }

            zeroPadding &= reader.AlignToByte();

            // Reads past the end return zeros. The method checks the bounds and both paddings once for the complete table. A complete table stays usable when
            // image payload bytes are missing. The payload owner decides if recovery is permitted.
            if (!reader.AllReadsWithinBounds || !zeroPadding)
            {
                string message = reader.AllReadsWithinBounds ? "The JPEG XL section table padding is not zero." : "The JPEG XL section table is truncated.";

                order?.Dispose();
                sizes?.Dispose();
                order = null;
                sizes = null;
                handling.ThrowOrIgnoreImageDataError(imageExists, message);
                return -1;
            }

            return total;
        }
        catch
        {
            order?.Dispose();
            sizes?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Validates entropy-coded Lehmer digits and constructs the section order only when reconstruction requests it.
    /// </summary>
    /// <param name="reader">The bit reader positioned after the permutation flag.</param>
    /// <param name="allocator">The decoder's allocator.</param>
    /// <param name="sectionCount">The positive permutation size.</param>
    /// <param name="workspace">The retained sizes buffer reused as a rank tree, or an empty span for Identify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The logical-to-physical order owner, or null for Identify or a zero-length encoded permutation.</returns>
    private static IMemoryOwner<uint>? ReadPermutation(
        ref JxlBitReader reader, MemoryAllocator allocator, int sectionCount, Span<uint> workspace, CancellationToken cancellationToken)
    {
        using JxlEntropyDecoder codes = JxlEntropyDecoder.Read(ref reader, allocator, JxlPermutation.ContextCount, true);
        JxlEntropyDecoder.Reader symbols = codes.CreateReader(ref reader);
        uint position = 0;
        uint end = JxlPermutation.ReadLength(ref reader, ref symbols, sectionCount, 0, [], ref position);
        if (end == 0)
        {
            symbols.CheckFinalState();
            return null;
        }

        // The first length has already been decoded. Include it when preparing the shared circular symbol history.
        // Coefficient orders use the same helper with one larger history retained across all component permutations.
        int historyLength = codes.UsesLz77 ? (int)BitOperations.RoundUpToPowerOf2(Math.Min(end + 1, JxlEntropyDecoder.MaximumLz77Distance)) : 0;
        using IMemoryOwner<uint>? historyOwner = historyLength == 0 ? null : allocator.Allocate<uint>(historyLength);
        Span<uint> history = historyOwner is null ? [] : historyOwner.Memory.Span;
        if (!history.IsEmpty)
        {
            history[0] = end;
        }

        IMemoryOwner<uint>? orderOwner = null;
        try
        {
            Span<uint> order = [];
            if (!workspace.IsEmpty)
            {
                orderOwner = allocator.Allocate<uint>(sectionCount);
                order = orderOwner.Memory.Span;
            }

            JxlPermutation.ReadDigits(
                ref reader, ref symbols, sectionCount, 0, end, [], order, workspace, history, ref position, cancellationToken);

            symbols.CheckFinalState();
            return orderOwner;
        }
        catch
        {
            orderOwner?.Dispose();
            throw;
        }
    }
}
