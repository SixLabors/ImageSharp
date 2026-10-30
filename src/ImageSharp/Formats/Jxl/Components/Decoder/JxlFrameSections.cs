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
    /// Reads a frame's section table and retains the sizes and order required for reconstruction.
    /// </summary>
    /// <param name="reader">The bit reader positioned after the frame header.</param>
    /// <param name="allocator">The decoder's memory allocator.</param>
    /// <param name="sectionCount">The positive entry count computed from the validated frame header.</param>
    /// <param name="cancellationToken">The token used to cancel table processing.</param>
    /// <returns>The table owner. The reader remains at the beginning of the section payloads.</returns>
    public static JxlFrameSections Read(ref JxlBitReader reader, MemoryAllocator allocator, int sectionCount, CancellationToken cancellationToken)
    {
        // Check the minimum table size before renting memory from an image-derived entry count.
        // One permutation flag precedes the sizes; padding and permutation tokens can only increase this minimum.
        reader.CheckBitBudget(((ulong)sectionCount * MinimumBitsPerEntry) + 1);
        IMemoryOwner<uint> sizes = allocator.Allocate<uint>(sectionCount);
        IMemoryOwner<uint>? order = null;
        try
        {
            (IMemoryOwner<uint>? tableOrder, long length) = ReadTable(ref reader, allocator, sectionCount, sizes.Memory.Span, cancellationToken);
            order = tableOrder;

            return new JxlFrameSections(sizes, order, reader.BytePosition, length);
        }
        catch
        {
            // The returned object owns both arrays only after construction succeeds.
            order?.Dispose();
            sizes.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Validates a frame's section table and skips its payloads without retaining sizes or constructing a permutation.
    /// Only a short final image payload can follow the image-data recovery policy.
    /// </summary>
    /// <param name="reader">The bit reader positioned after the frame header.</param>
    /// <param name="allocator">The decoder's allocator for entropy tables and optional LZ77 history.</param>
    /// <param name="sectionCount">The positive entry count computed from the validated frame header.</param>
    /// <param name="handling">The policy for an incomplete final payload. Table and container errors remain fatal.</param>
    /// <param name="cancellationToken">The token used to cancel table processing.</param>
    /// <returns>Whether all payload bytes were skipped. False is possible only with IgnoreImageData.</returns>
    public static bool Skip(
        ref JxlBitReader reader, MemoryAllocator allocator, int sectionCount, SegmentIntegrityHandling handling, CancellationToken cancellationToken)
    {
        long length = ReadTable(ref reader, allocator, sectionCount, [], cancellationToken).Length;
        bool complete = reader.TrySkipBytes(length);
        if (!complete && handling != SegmentIntegrityHandling.IgnoreImageData)
        {
            throw new InvalidImageContentException("The JPEG XL section payload is truncated.");
        }

        return complete;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.order?.Dispose();
        this.sizes.Dispose();
    }

    /// <summary>
    /// Parses the shared table syntax, optionally writing sizes and using their storage as permutation workspace.
    /// </summary>
    /// <param name="reader">The bit reader positioned after the frame header.</param>
    /// <param name="allocator">The decoder's allocator.</param>
    /// <param name="sectionCount">The positive number of entries.</param>
    /// <param name="sizes">The retained size storage, or an empty span for Identify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The optional order owner and combined section payload length.</returns>
    private static (IMemoryOwner<uint>? Order, long Length) ReadTable(
        ref JxlBitReader reader, MemoryAllocator allocator, int sectionCount, Span<uint> sizes, CancellationToken cancellationToken)
    {
        bool permuted = reader.ReadBoolean();
        reader.CheckBitBudget((ulong)sectionCount * MinimumBitsPerEntry);
        IMemoryOwner<uint>? order = permuted ? ReadPermutation(ref reader, allocator, sectionCount, sizes, cancellationToken) : null;
        try
        {
            reader.AlignToByte();
            reader.CheckBitBudget((ulong)sectionCount * MinimumBitsPerEntry);

            long total = 0;
            for (int i = 0; i < sectionCount; i++)
            {
                if ((i & (CancellationInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                uint size = reader.ReadU32((10, 0), (14, 1024), (22, 17408), (30, 4211712));
                if (!sizes.IsEmpty)
                {
                    sizes[i] = size;
                }

                // Each U32 choice is at most 4211712 + (2^30-1). Even int.MaxValue such entries fit in a long.
                // Keep the sum in bytes: converting that maximum total to bits could overflow an unsigned 64-bit count.
                total += size;
            }

            reader.AlignToByte();

            // A complete table remains usable when image payload bytes are missing. The payload owner
            // decides whether recovery is allowed; syntax, permutation and padding errors above always remain fatal.
            return (order, total);
        }
        catch
        {
            order?.Dispose();
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
        // JPEG XL coefficient orders and section orders share eight contexts. ceil(log2(value+1)) is the bit length
        // of value, including zero. Computing it without value+1 also avoids overflow at the unsigned endpoint.
        using JxlEntropyDecoder codes = JxlEntropyDecoder.Read(ref reader, allocator, 8, true);
        JxlEntropyDecoder.Reader symbols = codes.CreateReader(ref reader);
        int context = Math.Min(32 - BitOperations.LeadingZeroCount((uint)sectionCount), 7);
        uint end = symbols.ReadUInt32(ref reader, context, [], 0);
        if (end > (uint)sectionCount)
        {
            throw new InvalidImageContentException("The JPEG XL section permutation exceeds its table size.");
        }

        if (end == 0)
        {
            symbols.CheckFinalState();
            return null;
        }

        // Only an LZ77-enabled permutation needs retained symbol history. Rent once at this table boundary, never
        // inside the symbol loop. Include the end symbol and round to a power of two for masked circular indexing.
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
            uint highestBit = 0;
            if (!workspace.IsEmpty)
            {
                orderOwner = allocator.Allocate<uint>(sectionCount);
                order = orderOwner.Memory.Span;
                highestBit = 1U << BitOperations.Log2((uint)sectionCount);

                // Every section starts unused. A one-based Fenwick node contains the number of entries covered by
                // its lowest set bit. The sizes buffer is overwritten by actual size entries after this tree is no longer needed.
                for (int i = 0; i < workspace.Length; i++)
                {
                    if ((i & (CancellationInterval - 1)) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    uint position = (uint)i + 1;
                    workspace[i] = position & (~position + 1);
                }
            }

            uint previous = 0;
            for (int i = 0; i < (int)end; i++)
            {
                if ((i & (CancellationInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                context = Math.Min(32 - BitOperations.LeadingZeroCount(previous), 7);
                uint value = symbols.ReadUInt32(ref reader, context, history, (uint)i + 1);
                if (value >= (uint)(sectionCount - i))
                {
                    throw new InvalidImageContentException("The JPEG XL section permutation contains an invalid Lehmer digit.");
                }

                previous = value;
                if (!order.IsEmpty)
                {
                    order[i] = TakeRank(workspace, value + 1, highestBit);
                }
            }

            symbols.CheckFinalState();
            if (!order.IsEmpty)
            {
                // Unencoded Lehmer digits are zero. Select the first remaining physical section for each trailing entry.
                for (int i = (int)end; i < order.Length; i++)
                {
                    if ((i & (CancellationInterval - 1)) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    order[i] = TakeRank(workspace, 1, highestBit);
                }
            }

            return orderOwner;
        }
        catch
        {
            orderOwner?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Removes and returns the physical section with the specified one-based rank among unused sections.
    /// </summary>
    /// <param name="tree">The Fenwick counts for the physical section indices.</param>
    /// <param name="rank">The validated one-based unused-section rank.</param>
    /// <param name="highestBit">The highest power of two no greater than the section count.</param>
    /// <returns>The selected zero-based physical section index.</returns>
    private static uint TakeRank(Span<uint> tree, uint rank, uint highestBit)
    {
        uint length = (uint)tree.Length;
        uint next = 0;

        // Search from the largest block down. Skip a block only when it contains fewer unused entries than the requested rank.
        // Exact-size storage needs the candidate bound; no padded tree or second allocation is required.
        for (uint bit = highestBit; bit != 0; bit >>= 1)
        {
            uint candidate = next + bit;
            if (candidate <= length && tree[(int)candidate - 1] < rank)
            {
                rank -= tree[(int)candidate - 1];
                next = candidate;
            }
        }

        // The validated rank selects an existing entry. Decrement every containing node to remove it.
        // Unsigned positions can reach 2^31 when leaving an int-sized tree; they do not wrap or become negative indices.
        for (uint position = next + 1; position <= length; position += position & (~position + 1))
        {
            tree[(int)position - 1]--;
        }

        return next;
    }
}
