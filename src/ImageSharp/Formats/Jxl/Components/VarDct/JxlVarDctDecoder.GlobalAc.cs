// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    private readonly List<AcPass> acPasses = [];

    /// <summary>
    /// Gets the number of histogram sets selectable by each AC group.
    /// </summary>
    public uint AcHistogramSetCount { get; private set; }

    /// <summary>
    /// Gets the conservative hybrid-integer width represented by this frame's AC entropy tables.
    /// </summary>
    public int MaximumAcValueBits { get; private set; }

    /// <summary>
    /// Gets a value indicating whether shifted coefficients from all passes fit the native conservative sixteen-bit storage rule.
    /// </summary>
    public bool Uses16BitCoefficients { get; private set; }

    /// <summary>
    /// Gets the number of completed AC pass descriptions.
    /// </summary>
    public int AcPassCount => this.acPasses.Count;

    /// <summary>
    /// Reads matrices, histogram-set count, coefficient orders and entropy tables in AC-global wire order.
    /// </summary>
    /// <param name="reader">The bounded AC-global section reader, or the continuing reader of a combined section.</param>
    /// <param name="frame">The validated frame geometry and progressive pass declarations.</param>
    /// <param name="modular">The retained modular decoder used by RAW matrix descriptions.</param>
    /// <param name="allocator">The tracked allocator for matrices, orders, entropy tables and reusable scratch.</param>
    /// <param name="cancellationToken">The token checked between passes and during table construction.</param>
    /// <remarks>All DC groups and their AC metadata must be complete, so the used transform strategies are known.</remarks>
    public void ReadGlobalAc(
        ref JxlBitReader reader, JxlFrameHeader frame, JxlModularDecoder modular, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        this.ReadQuantizationMatrices(ref reader, modular, allocator, cancellationToken);

        // The padded block grid cannot cross an additional AC-group boundary. Its group count therefore
        // matches the frame's section table, which already bounded the complete group/pass product.
        uint columns = (uint)(((this.dcSize.Width - 1) / this.acGroupBlockDimension) + 1);
        uint rows = (uint)(((this.dcSize.Height - 1) / this.acGroupBlockDimension) + 1);
        uint groups = columns * rows;
        int histogramBits = 32 - BitOperations.LeadingZeroCount(groups - 1);
        this.AcHistogramSetCount = reader.ReadBits(histogramBits) + 1;

        // A non-power-of-two group count can encode extra histogram sets. Both references permit them.
        // Bound the derived span length, not the number of sets: the allocator and context map use int-sized lengths.
        ulong contextCount = (ulong)this.AcHistogramSetCount * (uint)this.AcContextCount;
        if (contextCount > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL AC context map exceeds the supported buffer length.");
        }

        this.acPasses.EnsureCapacity(frame.PassCount);
        int maximumShiftedBits = 0;
        for (int pass = 0; pass < frame.PassCount; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InlineArray64<int> offsets = default;
            IMemoryOwner<uint> orders = this.ReadCoefficientOrders(
                ref reader, allocator, this.UsedStrategies, offsets[..CoefficientOrderCount], cancellationToken);

            JxlEntropyDecoder? entropy = null;
            try
            {
                entropy = JxlEntropyDecoder.Read(ref reader, allocator, (int)contextCount, true);

                // Capacity is reserved before any pass allocation. Transfer only complete order/table pairs;
                // a failed parser or pass-object allocation releases the current pair and leaves earlier passes owned here.
                this.acPasses.Add(new AcPass(orders, offsets, entropy, frame.PassShifts[pass]));
            }
            catch
            {
                entropy?.Dispose();
                orders.Dispose();
                throw;
            }

            this.MaximumAcValueBits = Math.Max(this.MaximumAcValueBits, entropy.MaximumValueBits);
            maximumShiftedBits = Math.Max(maximumShiftedBits, entropy.MaximumValueBits + frame.PassShifts[pass]);
        }

        // Pair each pass's integer width with that pass's shift. Combining unrelated maxima would select
        // wider storage unnecessarily. Adding ceil(log2(pass count)) then bounds progressive accumulation.
        // Keep the native strict comparison against the sixteen-bit storage width.
        int accumulationBits = 32 - BitOperations.LeadingZeroCount((uint)frame.PassCount - 1);
        this.Uses16BitCoefficients = maximumShiftedBits + accumulationBits < sizeof(short) * 8;
        this.PrepareCoefficientStorage(allocator);
    }

    /// <summary>
    /// Borrows one completed pass's coefficient orders. Cache its span before entering AC block loops.
    /// </summary>
    /// <param name="pass">The validated progressive pass index.</param>
    /// <returns>The retained orders shared by that pass's groups.</returns>
    public ReadOnlyMemory<uint> GetCoefficientOrders(int pass) => this.acPasses[pass].Orders.Memory;

    /// <summary>
    /// Borrows one pass's thirty-nine component offsets. Unused order families have offset minus one.
    /// </summary>
    /// <param name="pass">The validated progressive pass index.</param>
    /// <returns>The offsets into <see cref="GetCoefficientOrders"/>.</returns>
    public ReadOnlySpan<int> GetCoefficientOrderOffsets(int pass) => this.acPasses[pass].Offsets;

    /// <summary>
    /// Starts one AC group's entropy state while borrowing its completed pass tables.
    /// </summary>
    /// <param name="pass">The validated progressive pass index.</param>
    /// <param name="reader">The bounded group reader after its histogram-set selector.</param>
    /// <returns>The symbol reader. This frame owner must outlive its use.</returns>
    public JxlEntropyDecoder.Reader CreateAcReader(int pass, ref JxlBitReader reader)
        => this.acPasses[pass].Entropy.CreateReader(ref reader);

    /// <summary>
    /// Owns one complete coefficient-order and entropy-table pair for a progressive pass.
    /// </summary>
    private sealed class AcPass : IDisposable
    {
        private InlineArray64<int> offsets;

        /// <summary>
        /// Initializes a new instance of the <see cref="AcPass"/> class and takes ownership of its completed tables.
        /// </summary>
        /// <param name="orders">The retained natural or encoded orders.</param>
        /// <param name="offsets">The thirty-nine initialized component offsets; remaining inline entries are unused.</param>
        /// <param name="entropy">The completed tables shared by the pass's groups.</param>
        /// <param name="shift">The progressive left shift applied to this pass's signed coefficient values.</param>
        public AcPass(IMemoryOwner<uint> orders, InlineArray64<int> offsets, JxlEntropyDecoder entropy, byte shift)
        {
            this.Orders = orders;
            this.offsets = offsets;
            this.Entropy = entropy;
            this.Shift = shift;
        }

        /// <summary>
        /// Gets the owned coefficient-order buffer.
        /// </summary>
        public IMemoryOwner<uint> Orders { get; }

        /// <summary>
        /// Gets the owned entropy tables.
        /// </summary>
        public JxlEntropyDecoder Entropy { get; }

        /// <summary>
        /// Gets the progressive left shift applied before coefficient accumulation.
        /// </summary>
        public byte Shift { get; }

        /// <summary>
        /// Gets the initialized component offsets without another managed array.
        /// </summary>
        public ReadOnlySpan<int> Offsets => this.offsets[..CoefficientOrderCount];

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Entropy.Dispose();
            this.Orders.Dispose();
        }
    }
}
