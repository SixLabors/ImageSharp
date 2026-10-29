// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// Owns one frame's quantization parameters and the prepared coefficient matrices used by its transform strategies.
/// </summary>
/// <remarks>
/// Prepare the complete search or decode strategy mask before processing blocks. Borrow the prepared storage once at that entry point.
/// Preparation can replace the allocation, so borrowed spans must not survive a later preparation or parameter change.
/// </remarks>
internal sealed class JxlDequantMatrices : IDisposable
{
    /// <summary>The shared default parameters. Preparation reads these descriptors without modifying them.</summary>
    public static readonly JxlQuantizerEncoding[] Library = GetLibrary();

    private IMemoryOwner<float>? tableOwner;
    private Memory<float> allocation;
    private Memory<float> preparedStorage;
    private uint computedMask;
    private uint preparedKinds;
    private int halfLength;

    // Strategies that differ only by orientation share a table kind. Seventeen base offsets avoid duplicating those tables.
    private InlineArray17<int> tableOffsets;
    private InlineArray3<float> dcQuant;
    private InlineArray3<float> inverseDcQuant;
    private JxlQuantizerEncoding[] encodings;

    /// <summary>Creates default quantization parameters without allocating coefficient matrices.</summary>
    public JxlDequantMatrices()
    {
        for (int c = 0; c < 3; c++)
        {
            this.dcQuant[c] = JxlQuantizerConstants.DcQuant[c];
            this.inverseDcQuant[c] = JxlQuantizerConstants.InverseDcQuant[c];
        }

        this.encodings = new JxlQuantizerEncoding[JxlQuantizerConstants.NumberOfQuantizerTables];
        for (int i = 0; i < this.encodings.Length; i++)
        {
            this.encodings[i] = JxlQuantizerEncoding.Library(0);
        }
    }

    /// <summary>
    /// Gets a lookup which represents required widths for each quantizer.
    /// </summary>
    public static ReadOnlySpan<int> RequiredSizeX => [1, 1, 1, 1, 2, 4, 1, 1, 2, 1, 1, 8, 4, 16, 8, 32, 16];

    /// <summary>
    /// Gets a lookup which represents required heights for each quantizer.
    /// </summary>
    public static ReadOnlySpan<int> RequiredSizeY => [1, 1, 1, 1, 2, 4, 2, 4, 4, 1, 1, 8, 8, 16, 16, 32, 32];

    /// <summary>
    /// Returns the default library with quantizer encodings for all transforms
    /// used by the JPEG XL codec.
    /// </summary>
    /// <returns>Encodings for all kinds of transforms.</returns>
    /// <exception cref="InvalidOperationException">Used when quantization constants were partially updated.</exception>
    public static JxlQuantizerEncoding[] GetLibrary()
    {
        if (JxlQuantizerConstants.NumberOfQuantizerTables != 17)
        {
            throw new InvalidOperationException("This function should be updated when adding new quantization types");
        }

        if (JxlQuantWeights.NumPredefinedTables != 1)
        {
            throw new InvalidOperationException("This function should be updated when adding new quantization matrices to the library");
        }

        Verify(0, JxlQuantTable.DCT);
        Verify(1, JxlQuantTable.IDENTITY);
        Verify(2, JxlQuantTable.DCT2X2);
        Verify(3, JxlQuantTable.DCT4X4);
        Verify(4, JxlQuantTable.DCT16X16);
        Verify(5, JxlQuantTable.DCT32X32);
        Verify(6, JxlQuantTable.DCT8X16);
        Verify(7, JxlQuantTable.DCT8X32);
        Verify(8, JxlQuantTable.DCT16X32);
        Verify(9, JxlQuantTable.DCT4X8);
        Verify(10, JxlQuantTable.AFV0);
        Verify(11, JxlQuantTable.DCT64X64);
        Verify(12, JxlQuantTable.DCT32X64);
        Verify(13, JxlQuantTable.DCT128X128);
        Verify(14, JxlQuantTable.DCT64X128);
        Verify(15, JxlQuantTable.DCT256X256);
        Verify(16, JxlQuantTable.DCT128X256);

        return
        [
            JxlQuantWeights.Dct,
            JxlQuantWeights.Identity,
            JxlQuantWeights.Dct2x2,
            JxlQuantWeights.Dct4x4,
            JxlQuantWeights.Dct16x16,
            JxlQuantWeights.Dct32x32,
            JxlQuantWeights.Dct8x16,
            JxlQuantWeights.Dct8x32,
            JxlQuantWeights.Dct16x32,
            JxlQuantWeights.Dct4x8,
            JxlQuantWeights.Afv,
            JxlQuantWeights.Dct64x64,
            JxlQuantWeights.Dct32x64,
            JxlQuantWeights.Dct128x128,
            JxlQuantWeights.Dct64x128,
            JxlQuantWeights.Dct256x256,
            JxlQuantWeights.Dct128x256
        ];

        [Conditional("DEBUG")]
        static void Verify(int expected, JxlQuantTable actual)
        {
            if (expected != (byte)actual)
            {
                throw new InvalidOperationException("Quantizer modes were partially updated; this method needs to be updated too");
            }
        }
    }

    /// <summary>Prepares exactly the table kinds required by a complete strategy mask.</summary>
    /// <param name="configuration">The configuration that owns coefficient storage.</param>
    /// <param name="strategyMask">The complete set of strategies used by the next search or decode operation.</param>
    /// <returns>Whether every requested table was generated successfully.</returns>
    public bool EnsureComputed(Configuration configuration, uint strategyMask)
    {
        uint kinds = 0;
        for (int i = 0; i < JxlAcStrategy.NumberOfValidStrategies; i++)
        {
            if ((strategyMask & (1U << i)) != 0)
            {
                kinds |= 1U << (int)JxlQuantizerConstants.AcStrategyToQuantTableMap[i];
            }
        }

        // A new orientation can reuse an already prepared kind. No allocation or table calculation is needed.
        if ((kinds & ~this.preparedKinds) == 0)
        {
            this.computedMask |= strategyMask;
            return true;
        }

        InlineArray17<int> offsets = default;
        int length = 0;
        for (int i = 0; i < JxlQuantizerConstants.NumberOfQuantizerTables; i++)
        {
            if ((kinds & (1U << i)) != 0)
            {
                offsets[i] = length;
                length += RequiredSizeX[i] * RequiredSizeY[i] * JxlFrameDimensions.DctBlockSize * 3;
            }
        }

        int storageLength = 2 * length;
        IMemoryOwner<float>? replacement = null;
        Memory<float> nextAllocation = this.allocation;
        try
        {
            if (storageLength > nextAllocation.Length)
            {
                replacement = configuration.MemoryAllocator.Allocate<float>(storageLength);
                nextAllocation = replacement.Memory;
            }
            else
            {
                // Reuse occurs only at the preparation boundary, after all previous borrowers have finished.
                // A failed regeneration must not expose partially overwritten tables as prepared matrices.
                this.computedMask = 0;
                this.preparedKinds = 0;
                this.preparedStorage = default;
            }

            Span<float> storage = nextAllocation.Span[..storageLength];
            Span<float> dequantization = storage[..length];
            Span<float> quantization = storage[length..];
            for (int i = 0; i < JxlQuantizerConstants.NumberOfQuantizerTables; i++)
            {
                if ((kinds & (1U << i)) == 0)
                {
                    continue;
                }

                JxlQuantizerEncoding encoding = this.encodings[i];
                if (encoding.Mode == JxlQuantMode.Library)
                {
                    encoding = Library[i];
                }

                int offset = offsets[i];
                if (!JxlQuantWeights.ComputeQuantTable(encoding, dequantization, quantization, (JxlQuantTable)i, ref offset))
                {
                    return false;
                }

                DebugGuard.IsTrue(
                    offset == offsets[i] + (RequiredSizeX[i] * RequiredSizeY[i] * JxlFrameDimensions.DctBlockSize * 3),
                    "The quantizer table did not fill its prepared region.");
            }

            if (replacement is not null)
            {
                this.tableOwner?.Dispose();
                this.tableOwner = replacement;
                replacement = null;
                this.allocation = nextAllocation;
            }

            this.tableOffsets = offsets;
            this.halfLength = length;
            this.preparedStorage = nextAllocation[..storageLength];
            this.preparedKinds = kinds;
            this.computedMask = strategyMask;
            return true;
        }
        finally
        {
            replacement?.Dispose();
        }
    }

    /// <summary>Borrows all prepared coefficient storage once for a group or strategy search.</summary>
    /// <returns>The dequantization table region followed by the quantization table region.</returns>
    public ReadOnlySpan<float> GetPreparedStorage() => this.preparedStorage.Span;

    /// <summary>Selects the requested channel and remaining channels within one prepared dequantization table kind.</summary>
    /// <param name="storage">The complete storage borrowed at the group or search entry point.</param>
    /// <param name="transformType">The prepared transform strategy.</param>
    /// <param name="channelIndex">The first XYB channel to return.</param>
    /// <returns>Exactly three, two or one channel planes. Channel zero includes the complete X, Y and B table.</returns>
    public ReadOnlySpan<float> GetMatrix(ReadOnlySpan<float> storage, JxlAcStrategyType transformType, int channelIndex)
    {
        DebugGuard.IsTrue((this.computedMask & (1U << (int)transformType)) != 0, "The strategy matrices were not prepared.");
        int kind = (int)JxlQuantizerConstants.AcStrategyToQuantTableMap[(int)transformType];
        int count = RequiredSizeX[kind] * RequiredSizeY[kind] * JxlFrameDimensions.DctBlockSize;
        return storage.Slice(this.tableOffsets[kind] + (channelIndex * count), (3 - channelIndex) * count);
    }

    /// <summary>Selects the requested channel and remaining channels within one prepared quantization table kind.</summary>
    /// <param name="storage">The complete storage borrowed at the group or search entry point.</param>
    /// <param name="transformType">The prepared transform strategy.</param>
    /// <param name="channelIndex">The first XYB channel to return.</param>
    /// <returns>Exactly three, two or one channel planes, without entering another table kind.</returns>
    public ReadOnlySpan<float> GetInverseMatrix(ReadOnlySpan<float> storage, JxlAcStrategyType transformType, int channelIndex)
    {
        DebugGuard.IsTrue((this.computedMask & (1U << (int)transformType)) != 0, "The strategy matrices were not prepared.");
        int kind = (int)JxlQuantizerConstants.AcStrategyToQuantTableMap[(int)transformType];
        int count = RequiredSizeX[kind] * RequiredSizeY[kind] * JxlFrameDimensions.DctBlockSize;
        return storage.Slice(this.halfLength + this.tableOffsets[kind] + (channelIndex * count), (3 - channelIndex) * count);
    }

    /// <summary>
    /// Returns the DC quantization multiplier for a channel.
    /// </summary>
    /// <param name="channelIndex">The DC quantizer index.</param>
    /// <returns>DC quant for index <paramref name="channelIndex"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetDcQuant(int channelIndex) => this.dcQuant[channelIndex];

    /// <summary>
    /// Returns all DC quantizers. See also <seealso cref="GetDcQuant(int)"/>.
    /// </summary>
    /// <returns>Span that covers all DC quantizers.</returns>
    public Span<float> GetDcQuants() => this.dcQuant;

    /// <summary>
    /// Returns the inverse DC quantization multiplier for a channel.
    /// </summary>
    /// <param name="channelIndex">The inverse DC quantizer index.</param>
    /// <returns>Inverse DC quant for index <paramref name="channelIndex"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetInverseDcQuant(int channelIndex) => this.inverseDcQuant[channelIndex];

    /// <summary>
    /// Applies the specified DC quantizer.
    /// </summary>
    /// <param name="dcQuantizationValues">DC quantizer to apply to the dequantization matrices.</param>
    public void SetDcQuant(InlineArray3<float> dcQuantizationValues)
    {
        for (int channelIndex = 0; channelIndex < 3; channelIndex++)
        {
            this.dcQuant[channelIndex] = 1f / dcQuantizationValues[channelIndex];
            this.inverseDcQuant[channelIndex] = dcQuantizationValues[channelIndex];
        }
    }

    /// <summary>Transfers quantization descriptors to this owner and invalidates prepared matrices.</summary>
    /// <param name="encodings">The complete descriptor array. The caller must not modify it after transfer.</param>
    public void SetEncodings(JxlQuantizerEncoding[] encodings)
    {
        this.encodings = encodings;
        this.computedMask = 0;
        this.preparedKinds = 0;
        this.preparedStorage = default;
    }

    /// <summary>Borrows the frame's quantization descriptors for serialization or JPEG reconstruction.</summary>
    /// <returns>The fixed set of table descriptors, which callers must not modify.</returns>
    public ReadOnlySpan<JxlQuantizerEncoding> GetEncodings() => this.encodings;

    /// <summary>Releases the prepared coefficient allocation.</summary>
    public void Dispose()
    {
        this.tableOwner?.Dispose();
        this.tableOwner = null;
        this.allocation = default;
        this.preparedStorage = default;
        this.computedMask = 0;
        this.preparedKinds = 0;
    }
}
