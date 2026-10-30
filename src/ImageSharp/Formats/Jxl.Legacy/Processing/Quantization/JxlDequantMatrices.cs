// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;

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

        JxlQuantizerEncoding[] definitions =
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

        // These library definitions contain fixed parameters only. Value copies make each returned descriptor independent;
        // frame disposal never reaches the shared defaults used by table preparation.
        for (int i = 0; i < definitions.Length; i++)
        {
            JxlQuantizerEncoding source = definitions[i];
            definitions[i] = new JxlQuantizerEncoding
            {
                Mode = source.Mode,
                Predefined = source.Predefined,
                DctParameters = source.DctParameters,
                DctParametersAfv4x4 = source.DctParametersAfv4x4,
                ModeWeights = source.ModeWeights
            };
        }

        return definitions;

        [Conditional("DEBUG")]
        static void Verify(int expected, JxlQuantTable actual)
        {
            if (expected != (byte)actual)
            {
                throw new InvalidOperationException("Quantizer modes were partially updated; this method needs to be updated too");
            }
        }
    }

    /// <summary>Reads a complete matrix header into this frame's existing descriptor slots.</summary>
    /// <param name="configuration">The configuration that owns any raw table samples.</param>
    /// <param name="reader">The reader positioned at the all-default flag.</param>
    /// <param name="modularFrameDecoder">The frame modular context, or null for a standalone table stream.</param>
    /// <returns>Whether all descriptors were read successfully.</returns>
    /// <remarks>
    /// A new header invalidates prepared views and releases earlier raw payloads. Failed reads release partial raw payloads too.
    /// The descriptor slots and coefficient allocation remain owned by this instance for later preparation.
    /// </remarks>
    public bool Decode(Configuration configuration, JxlBitReader reader, JxlModularFrameDecoder? modularFrameDecoder)
    {
        this.computedMask = 0;
        this.preparedKinds = 0;
        this.preparedStorage = default;

        // Every header replaces all seventeen modes. Reuse their fixed storage; inactive parameter banks need no clearing.
        foreach (JxlQuantizerEncoding encoding in this.encodings)
        {
            encoding.Dispose();
            encoding.Mode = JxlQuantMode.Library;
            encoding.Predefined = 0;
        }

        bool success = false;
        try
        {
            if (!reader.ReadBoolean())
            {
                for (int i = 0; i < this.encodings.Length; i++)
                {
                    if (!JxlQuantWeights.Decode(configuration, reader, this.encodings[i], RequiredSizeX[i], RequiredSizeY[i], i, modularFrameDecoder))
                    {
                        return false;
                    }
                }
            }

            success = true;
            return true;
        }
        finally
        {
            if (!success)
            {
                // No caller can use a partial matrix header. Release raw tables read before the failing field or truncated input.
                foreach (JxlQuantizerEncoding encoding in this.encodings)
                {
                    encoding.Dispose();
                    encoding.Mode = JxlQuantMode.Library;
                    encoding.Predefined = 0;
                }
            }
        }
    }

    /// <summary>Reads the three DC multipliers, restoring format defaults when the default flag is set.</summary>
    /// <param name="reader">The reader positioned at the DC all-default flag.</param>
    /// <returns>Whether all custom fields are finite and at least 1e-8 after scaling.</returns>
    /// <remarks>A failed read leaves all three previous multipliers unchanged. DC changes do not invalidate the separate AC matrices.</remarks>
    public bool DecodeDc(JxlBitReader reader)
    {
        InlineArray3<float> dc = default;
        InlineArray3<float> inverseDc = default;
        if (reader.ReadBoolean())
        {
            // Native frame setup starts from these defaults. Reset explicitly when the same owner reads another default header.
            for (int c = 0; c < 3; c++)
            {
                dc[c] = JxlQuantizerConstants.DcQuant[c];
                inverseDc[c] = JxlQuantizerConstants.InverseDcQuant[c];
            }
        }
        else
        {
            for (int c = 0; c < 3; c++)
            {
                if (!JxlF16Coder.Read(reader, ref dc[c]))
                {
                    return false;
                }

                // The wire stores each multiplier scaled by 128. Validate after undoing that scale, as required by the format.
                dc[c] *= 1F / JxlQuantizerConstants.DcWeightScale;
                if (dc[c] < JxlQuantizerConstants.MinimumWeight)
                {
                    return false;
                }

                inverseDc[c] = 1F / dc[c];
            }
        }

        // Publish both directions only after every field succeeds. An invalid later channel cannot leave mixed old and new values.
        this.dcQuant = dc;
        this.inverseDcQuant = inverseDc;
        return true;
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
        foreach (JxlQuantizerEncoding previous in this.encodings)
        {
            previous.Dispose();
        }

        this.encodings = encodings;
        this.computedMask = 0;
        this.preparedKinds = 0;
        this.preparedStorage = default;
    }

    /// <summary>Borrows the frame's quantization descriptors for serialization or JPEG reconstruction.</summary>
    /// <returns>The fixed set of table descriptors, which callers must not modify.</returns>
    public ReadOnlySpan<JxlQuantizerEncoding> GetEncodings() => this.encodings;

    /// <summary>Releases the coefficient allocation and every transferred descriptor's raw storage.</summary>
    public void Dispose()
    {
        foreach (JxlQuantizerEncoding encoding in this.encodings)
        {
            encoding.Dispose();
        }

        this.encodings = [];
        this.tableOwner?.Dispose();
        this.tableOwner = null;
        this.allocation = default;
        this.preparedStorage = default;
        this.computedMask = 0;
        this.preparedKinds = 0;
    }
}
