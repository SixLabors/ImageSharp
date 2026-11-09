// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Memory;
using static SixLabors.ImageSharp.Formats.Jxl.Components.VarDct.JxlQuantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    private readonly int sampleBitDepth;
    private IMemoryOwner<float>? quantizationMatrices;
    private InlineArray64<int> quantizationMatrixOffsets;
    private int quantizationMatrixLength;

    /// <summary>
    /// Gets the retained decoder factors for the quantization families used by this frame.
    /// </summary>
    /// <remarks>
    /// Each family stores three contiguous component planes. Rectangular planes use the larger dimension as their row stride.
    /// Borrow this span once before traversing AC groups. The owner retains the allocation until disposal.
    /// </remarks>
    public ReadOnlySpan<float> QuantizationMatrices
        => this.quantizationMatrices is null ? [] : this.quantizationMatrices.Memory.Span[..this.quantizationMatrixLength];

    /// <summary>
    /// Gets the fifty-one component offsets into <see cref="QuantizationMatrices"/>. Unused families have offset minus one.
    /// </summary>
    public ReadOnlySpan<int> QuantizationMatrixOffsets => this.quantizationMatrixOffsets[..(MatrixComponentCount * QuantizationFamilyCount)];

    /// <summary>
    /// Reads all matrix descriptions and retains decoder factors only for families used by this frame.
    /// </summary>
    /// <param name="reader">The AC-global reader at the all-default matrix bit.</param>
    /// <param name="modular">The existing frame modular decoder and its shared tree and workspace.</param>
    /// <param name="allocator">The tracked allocator that owns retained matrix storage and temporary RAW samples.</param>
    /// <param name="cancellationToken">The token checked between descriptions, components and rows.</param>
    public void ReadQuantizationMatrices(
        ref JxlBitReader reader, JxlModularDecoder modular, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        uint usedFamilies = 0;
        for (int strategy = 0; strategy < JxlCoefficientOrder.StrategyCount; strategy++)
        {
            if ((this.UsedStrategies & (1U << strategy)) != 0)
            {
                usedFamilies |= 1U << QuantizationFamilies[strategy];
            }
        }

        // Matrix dimensions come from the seventeen format families, so the complete used set fits 394,752 floats.
        // One allocation holds only decoder factors. Encoder reciprocal tables are not part of decoding.
        Span<int> offsets = this.quantizationMatrixOffsets;
        offsets.Fill(-1);
        int length = 0;
        for (int family = 0; family < QuantizationFamilyCount; family++)
        {
            if ((usedFamilies & (1U << family)) != 0)
            {
                int area = BlockSize * BlockSize * MatrixBlockWidths[family] * MatrixBlockHeights[family];
                for (int component = 0; component < MatrixComponentCount; component++)
                {
                    offsets[(family * MatrixComponentCount) + component] = length;
                    length += area;
                }
            }
        }

        this.quantizationMatrices = allocator.Allocate<float>(length);
        this.quantizationMatrixLength = length;
        Span<float> matrices = this.quantizationMatrices.Memory.Span[..length];
        bool allDefault = reader.ReadBits(1) != 0;
        InlineArray128<float> parameterStorage = default;
        Span<float> parameters = parameterStorage;
        InlineArray4<JxlModularChannel> rawStorage = default;
        Span<JxlModularChannel> rawChannels = rawStorage;
        Size rawCapacity = Size.Empty;

        try
        {
            for (int family = 0; family < QuantizationFamilyCount; family++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int mode = allDefault ? 0 : (int)reader.ReadBits(3);
                MatrixDescription description = mode == 0
                    ? GetLibraryMatrix(family, parameters)
                    : ReadMatrixDescription(ref reader, family, (byte)mode, parameters);

                int offset = offsets[family * MatrixComponentCount];
                int width = BlockSize * MatrixBlockWidths[family];
                int height = BlockSize * MatrixBlockHeights[family];
                int area = width * height;
                Span<float> destination = offset < 0 ? [] : matrices.Slice(offset, MatrixComponentCount * area);
                if (description.Mode == RawMatrixMode)
                {
                    // Descriptions appear in wire order. The next RAW dimensions are not known before its mode is read.
                    // Grow sample scratch only at this metadata boundary, without copying discarded samples. Later fitting tables reuse it.
                    if (width > rawCapacity.Width || height > rawCapacity.Height)
                    {
                        Size size = new(Math.Max(width, rawCapacity.Width), Math.Max(height, rawCapacity.Height));
                        for (int component = 0; component < MatrixComponentCount; component++)
                        {
                            JxlModularChannel replacement = JxlModularChannel.Allocate(allocator, new JxlModularChannelLayout(size, 0, 0));
                            rawChannels[component].Dispose();
                            rawChannels[component] = replacement;
                        }

                        rawCapacity = size;
                    }

                    InlineArray4<JxlModularChannel> targetStorage = default;
                    Span<JxlModularChannel> targets = targetStorage;
                    Rectangle bounds = new(0, 0, width, height);
                    for (int component = 0; component < MatrixComponentCount; component++)
                    {
                        targets[component] = rawChannels[component].Borrow(bounds);
                    }

                    // RAW is an eight-bit modular image even when the decoded picture has another precision.
                    // Passing precision to the existing inverse-transform path also gives implicit palettes the correct units.
                    modular.ReadChannels(ref reader, targets[..MatrixComponentCount], 1 + (3 * this.dcGroupCount) + family, 8, cancellationToken);
                    InlineArray256<float> unusedStorage = default;
                    Span<float> unusedRow = unusedStorage;
                    for (int component = 0; component < MatrixComponentCount; component++)
                    {
                        Buffer2DRegion<int> raw = targets[component].Region;
                        for (int y = 0; y < height; y++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Span<float> row = destination.IsEmpty ? unusedRow[..width] : destination.Slice((component * area) + (y * width), width);
                            ConvertRawQuantizationRow(raw.DangerousGetRowSpan(y), row, parameters[SpecialWeightsOffset]);
                        }
                    }
                }
                else if (!destination.IsEmpty)
                {
                    GenerateMatrix(description, parameters, width, height, destination, cancellationToken);
                }

                // Unused descriptions still consume and validate their encoded fields, including positive RAW integers.
                // Derived weight limits apply when a description is used, as in libjxl's EnsureComputed boundary.
                if (!destination.IsEmpty)
                {
                    InvertQuantizationWeights(destination);
                }
            }
        }
        finally
        {
            for (int component = 0; component < MatrixComponentCount; component++)
            {
                rawChannels[component].Dispose();
            }
        }
    }

    /// <summary>
    /// Releases retained matrix factors with the frame owner.
    /// </summary>
    private void DisposeQuantizationMatrices()
    {
        this.quantizationMatrices?.Dispose();
        this.quantizationMatrices = null;
    }

    /// <summary>
    /// Reads one explicitly encoded description without retaining unused tables or parameter objects.
    /// </summary>
    /// <param name="reader">The reader after the three-bit mode.</param>
    /// <param name="family">The family that establishes the permitted dimensions.</param>
    /// <param name="mode">The encoded nonlibrary mode.</param>
    /// <param name="parameters">The reused primary, secondary and special-weight storage.</param>
    /// <returns>The encoded mode and radial band counts.</returns>
    private static MatrixDescription ReadMatrixDescription(ref JxlBitReader reader, int family, byte mode, Span<float> parameters)
    {
        // Identity, small DCT and AFV descriptions occupy one eight-by-eight coefficient block.
        // The generic radial and RAW modes also support the larger and rectangular families.
        if (mode < DctMatrixMode && MatrixBlockWidths[family] * MatrixBlockHeights[family] != 1)
        {
            throw new InvalidImageContentException("The JPEG XL quantization mode requires an eight-by-eight matrix.");
        }

        int primaryCount = 0;
        int secondaryCount = 0;
        if (mode == RawMatrixMode)
        {
            float denominator = reader.ReadHalf();
            if (denominator < MinimumQuantizationWeight)
            {
                throw new InvalidImageContentException("The JPEG XL RAW quantization denominator is too small.");
            }

            parameters[SpecialWeightsOffset] = denominator;
        }
        else
        {
            int specialCount = GetSpecialWeightCount(mode);
            for (int component = 0; component < MatrixComponentCount; component++)
            {
                for (int i = 0; i < specialCount; i++)
                {
                    float value = reader.ReadHalf();
                    if (mode != AfvMatrixMode && MathF.Abs(value) < MinimumQuantizationWeight)
                    {
                        throw new InvalidImageContentException("A JPEG XL quantization weight or multiplier is too small.");
                    }

                    // Identity and DCT2 weights, and the first six AFV weights, use units of one sixty-fourth.
                    // DCT4 multipliers and AFV band deltas are dimensionless.
                    if (mode <= Dct2MatrixMode || (mode == AfvMatrixMode && i < 6))
                    {
                        value *= BlockSize * BlockSize;
                    }

                    parameters[SpecialWeightsOffset + (component * SpecialWeightStride) + i] = value;
                }
            }

            if (mode >= Dct4MatrixMode)
            {
                primaryCount = ReadDistanceBands(ref reader, parameters[..SecondaryBandsOffset]);
                if (mode == AfvMatrixMode)
                {
                    secondaryCount = ReadDistanceBands(ref reader, parameters.Slice(SecondaryBandsOffset, SecondaryBandsOffset));
                }
            }
        }

        return new MatrixDescription(mode, primaryCount, secondaryCount);
    }

    /// <summary>
    /// Reads the three component band sequences, retaining their encoded deltas and expanded seed units.
    /// </summary>
    /// <param name="reader">The reader at the four-bit band count minus one.</param>
    /// <param name="parameters">Three sixteen-value band slots.</param>
    /// <returns>The number of bands per component, from one through sixteen.</returns>
    private static int ReadDistanceBands(ref JxlBitReader reader, Span<float> parameters)
    {
        int count = (int)reader.ReadBits(4) + 1;
        for (int component = 0; component < MatrixComponentCount; component++)
        {
            Span<float> bands = parameters.Slice(component * MaximumDistanceBands, count);
            for (int i = 0; i < count; i++)
            {
                bands[i] = reader.ReadHalf();
            }

            if (bands[0] < MinimumQuantizationWeight)
            {
                throw new InvalidImageContentException("The JPEG XL distance-band seed is too small.");
            }

            bands[0] *= BlockSize * BlockSize;
        }

        return count;
    }
}
