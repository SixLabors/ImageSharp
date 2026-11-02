// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

/// <summary>
/// Owns the quantization, block contexts and color correlation needed to reconstruct one VarDCT frame.
/// </summary>
internal sealed partial class JxlVarDctDecoder : IDisposable
{
    // The thirteen coefficient-order families share contexts across three color components.
    private const int OrderCount = 13;
    private const int MaximumThresholds = 15;
    private const int ThresholdStride = MaximumThresholds + 1;
    private const int MaximumContextCombinations = 64;
    private const int MaximumBlockContexts = 16;
    private const int NonzeroContextCount = 37;
    private const int ZeroDensityContextCount = 458;
    private const int GlobalScaleDenominator = 1 << 16;
    private const int DefaultColorFactor = 84;
    private const float MaximumBaseCorrelation = 4F;

    private InlineArray64<int> dcThresholds;
    private InlineArray4<int> dcThresholdCounts;
    private InlineArray16<uint> quantizationThresholds;
    private int quantizationThresholdCount;
    private IMemoryOwner<byte>? blockContextMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlVarDctDecoder"/> class with the frame's preceding DC factors.
    /// </summary>
    /// <param name="dcFactors">The validated DC matrix factors in X, Y, B component order.</param>
    /// <param name="sampleBitDepth">The image precision used by DC and extra-channel modular transforms.</param>
    private JxlVarDctDecoder(Vector3 dcFactors, int sampleBitDepth)
    {
        this.DcFactors = dcFactors;
        this.sampleBitDepth = sampleBitDepth;
    }

    /// <summary>
    /// Gets the DC matrix factors before global and DC quantizer scaling, in X, Y, B order.
    /// </summary>
    public Vector3 DcFactors { get; }

    /// <summary>
    /// Gets the encoded positive global quantization scale.
    /// </summary>
    public int GlobalScale { get; private set; }

    /// <summary>
    /// Gets the encoded positive DC quantizer.
    /// </summary>
    public int DcQuantizer { get; private set; }

    /// <summary>
    /// Gets 65536 divided by the encoded global scale, prepared once for coefficient dequantization.
    /// </summary>
    public float InverseGlobalScale { get; private set; }

    /// <summary>
    /// Gets the DC factors multiplied by the inverse global scale and divided by the DC quantizer.
    /// </summary>
    public Vector3 DcMultipliers { get; private set; }

    /// <summary>
    /// Gets the product of the three DC threshold bucket counts.
    /// </summary>
    public int DcContextCount { get; private set; }

    /// <summary>
    /// Gets the number of distinct contexts selected by the block map.
    /// </summary>
    public int BlockContextCount { get; private set; }

    /// <summary>
    /// Gets the number of AC symbol contexts in each histogram set.
    /// </summary>
    public int AcContextCount => this.BlockContextCount * (NonzeroContextCount + ZeroDensityContextCount);

    /// <summary>
    /// Gets the block context map. Borrow this span once before traversing blocks.
    /// </summary>
    /// <remarks>
    /// The index groups component, coefficient order, quantization bucket and DC bucket, from outermost to innermost.
    /// Component indices exchange X and Y because the encoded modular channel order is Y, X, B.
    /// </remarks>
    public ReadOnlySpan<byte> BlockContextMap => this.blockContextMap is null ? DefaultBlockContextMap : this.blockContextMap.Memory.Span;

    /// <summary>
    /// Gets the AC quantization thresholds in encoded order. A sample enters the next bucket only when it exceeds a threshold.
    /// </summary>
    public ReadOnlySpan<uint> QuantizationThresholds => this.quantizationThresholds[..this.quantizationThresholdCount];

    /// <summary>
    /// Gets the multiplier that converts a signed color-correlation map entry to a ratio adjustment.
    /// </summary>
    public float ColorScale { get; private set; }

    /// <summary>
    /// Gets the base ratio used to restore X from Y and its coded residual.
    /// </summary>
    public float BaseCorrelationX { get; private set; }

    /// <summary>
    /// Gets the base ratio used to restore B from Y and its coded residual.
    /// </summary>
    public float BaseCorrelationB { get; private set; }

    /// <summary>
    /// Gets the complete Y-to-X ratio for DC reconstruction.
    /// </summary>
    public float DcCorrelationX { get; private set; }

    /// <summary>
    /// Gets the complete Y-to-B ratio for DC reconstruction.
    /// </summary>
    public float DcCorrelationB { get; private set; }

    /// <summary>
    /// Gets the format's default block map. The large coefficient-order families share the same context.
    /// </summary>
    private static ReadOnlySpan<byte> DefaultBlockContextMap =>
    [
        0, 1, 2, 2, 3, 3, 4, 5, 6, 6, 6, 6, 6,
        7, 8, 9, 9, 10, 11, 12, 13, 14, 14, 14, 14, 14,
        7, 8, 9, 9, 10, 11, 12, 13, 14, 14, 14, 14, 14
    ];

    /// <summary>
    /// Reads the VarDCT fields between the frame's DC matrix factors and its global modular tree.
    /// </summary>
    /// <param name="reader">The bounded DC-global section reader, immediately after the DC matrix factors.</param>
    /// <param name="image">The image-wide color and sample declarations.</param>
    /// <param name="frame">The frame geometry and sampling layout.</param>
    /// <param name="allocator">The tracked allocator for frame samples, scratch and the variable block context map.</param>
    /// <param name="dcFactors">The preceding DC matrix factors in X, Y, B order.</param>
    /// <param name="dcReference">
    /// Three saved native float planes, or empty for coded DC. The caller keeps these planes alive until this owner is disposed.
    /// </param>
    /// <returns>The frame metadata, DC regions and local scratch owner. Failure releases every local allocation.</returns>
    public static JxlVarDctDecoder ReadGlobal(
        ref JxlBitReader reader,
        JxlHeader image,
        JxlFrameHeader frame,
        MemoryAllocator allocator,
        Vector3 dcFactors,
        ReadOnlySpan<Buffer2D<float>> dcReference)
    {
        JxlVarDctDecoder result = new(dcFactors, image.Metadata.BitDepth.BitsPerSample);
        try
        {
            // Both U32 fields include positive offsets. Their syntax excludes a zero divisor without another guard.
            result.GlobalScale = (int)reader.ReadU32((11, 1), (11, 2049), (12, 4097), (16, 8193));
            result.DcQuantizer = (int)reader.ReadU32((0, 16), (5, 1), (8, 1), (16, 1));
            result.InverseGlobalScale = (float)GlobalScaleDenominator / result.GlobalScale;
            float inverseDcQuantizer = result.InverseGlobalScale / result.DcQuantizer;
            result.DcMultipliers = dcFactors * inverseDcQuantizer;

            if (reader.ReadBoolean())
            {
                // The default map has no thresholds: one DC bucket and one AC quantization bucket.
                // Its static bytes need no per-frame allocation and remain valid until this owner is disposed.
                result.DcContextCount = 1;
                result.BlockContextCount = 15;
            }
            else
            {
                int combinations = 1;
                for (int c = 0; c < 3; c++)
                {
                    int start = c * ThresholdStride;
                    int count = ReadDcThresholds(ref reader, result.dcThresholds[start..(start + MaximumThresholds)]);
                    result.dcThresholdCounts[c] = count;
                    combinations *= count + 1;
                }

                result.DcContextCount = combinations;
                result.quantizationThresholdCount = (int)reader.ReadBits(4);
                for (int i = 0; i < result.quantizationThresholdCount; i++)
                {
                    result.quantizationThresholds[i] = reader.ReadU32((2, 0), (3, 4), (5, 12), (8, 44)) + 1;
                }

                combinations *= result.quantizationThresholdCount + 1;
                if (combinations > MaximumContextCombinations)
                {
                    throw new InvalidImageContentException("The JPEG XL block context map has too many threshold combinations.");
                }

                // Validate the product before allocating. The format permits at most 3 * 13 * 64 = 2496 map bytes.
                // The shared entropy decoder consumes the complete map and validates its cluster numbering.
                result.blockContextMap = allocator.Allocate<byte>(3 * OrderCount * combinations);
                Span<byte> map = result.blockContextMap.Memory.Span;
                JxlEntropyDecoder.ReadContextMap(ref reader, allocator, map);
                int contexts = 1;
                foreach (byte context in map)
                {
                    contexts = Math.Max(contexts, context + 1);
                }

                if (contexts > MaximumBlockContexts)
                {
                    throw new InvalidImageContentException("The JPEG XL block context map selects more than sixteen contexts.");
                }

                result.BlockContextCount = contexts;
            }

            // Correlation syntax follows the block map. Only XYB's default B residual needs an added Y component.
            // Explicit fields replace both base ratios, regardless of the frame's color representation.
            int colorFactor = DefaultColorFactor;
            int dcX = 0;
            int dcB = 0;
            result.BaseCorrelationB = image.UsesXybTransform ? 1F : 0F;
            if (!reader.ReadBoolean())
            {
                colorFactor = (int)reader.ReadU32((0, DefaultColorFactor), (0, 256), (8, 2), (16, 258));
                result.BaseCorrelationX = reader.ReadHalf();
                result.BaseCorrelationB = reader.ReadHalf();
                if (MathF.Abs(result.BaseCorrelationX) > MaximumBaseCorrelation || MathF.Abs(result.BaseCorrelationB) > MaximumBaseCorrelation)
                {
                    throw new InvalidImageContentException("The JPEG XL base color correlation is outside its permitted range.");
                }

                dcX = (int)reader.ReadBits(8) - 128;
                dcB = (int)reader.ReadBits(8) - 128;
            }

            result.ColorScale = 1F / colorFactor;
            result.DcCorrelationX = result.BaseCorrelationX + (dcX * result.ColorScale);
            result.DcCorrelationB = result.BaseCorrelationB + (dcB * result.ColorScale);

            // Metadata bounds and map validation precede sample allocations. All DC groups reuse this frame storage.
            result.PrepareDcStorage(frame, allocator, dcReference);
            result.PrepareAcStorage(allocator);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Borrows one component's DC thresholds without allocating or changing their encoded order.
    /// </summary>
    /// <param name="channel">The component index: zero for X, one for Y, or two for B.</param>
    /// <returns>The thresholds used to classify that component's signed quantized DC values.</returns>
    public ReadOnlySpan<int> GetDcThresholds(int channel)
        => this.dcThresholds[(channel * ThresholdStride)..((channel * ThresholdStride) + this.dcThresholdCounts[channel])];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (AcPass pass in this.acPasses)
        {
            pass.Dispose();
        }

        this.DisposeQuantizationMatrices();
        this.DisposeOrderWorkspace();
        this.DisposeCoefficientStorage();
        this.DisposeReconstruction();

        // Only locally allocated DC planes enter this list. Borrowed LF planes stay with the decoder's saved-slot owner.
        foreach (Buffer2D<float> channel in this.dcChannels)
        {
            channel.Dispose();
        }

        for (int c = 0; c < 3; c++)
        {
            this.dcScratch[c].Dispose();
        }

        this.dcContexts?.Dispose();
        this.acRecords.Dispose();
        foreach (Buffer2D<sbyte> map in this.correlationMaps)
        {
            map.Dispose();
        }

        this.strategies?.Dispose();
        this.quantization?.Dispose();
        this.sharpness?.Dispose();
        this.blockContextMap?.Dispose();
    }

    /// <summary>
    /// Reads one signed DC threshold list into the owner's fixed component range.
    /// </summary>
    /// <param name="reader">The reader at the four-bit threshold count.</param>
    /// <param name="destination">Space for the syntax's maximum fifteen thresholds.</param>
    /// <returns>The number of thresholds read.</returns>
    private static int ReadDcThresholds(ref JxlBitReader reader, scoped Span<int> destination)
    {
        int count = (int)reader.ReadBits(4);
        for (int i = 0; i < count; i++)
        {
            // The last U32 choice adds its offset in unsigned 32-bit units, as in native code.
            // Signed unpacking maps even values to nonnegative thresholds and odd values to negative thresholds.
            uint value = reader.ReadU32((4, 0), (8, 16), (16, 272), (32, 65808));
            destination[i] = (int)(value >> 1) ^ -(int)(value & 1);
        }

        return count;
    }
}
