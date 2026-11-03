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
        if (this.isDisposed)
        {
            return;
        }

        this.order?.Dispose();
        this.sizes.Dispose();
        this.isDisposed = true;
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
