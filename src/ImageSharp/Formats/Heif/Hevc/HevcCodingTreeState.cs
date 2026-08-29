// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Stores the spatial coding-unit state required by later HEVC still-picture syntax and reconstruction stages.
/// </summary>
internal sealed class HevcCodingTreeState : IDisposable
{
    /// <summary>
    /// The coding-unit flag indicating transform and quantization bypass.
    /// </summary>
    private const byte TransquantBypassFlag = 1 << 0;

    /// <summary>
    /// The coding-unit flag indicating pulse-code-modulated samples.
    /// </summary>
    private const byte PcmFlag = 1 << 1;

    /// <summary>
    /// The decoded coding-unit depth at minimum-coding-block resolution.
    /// </summary>
    private readonly Buffer2D<byte> depths;

    /// <summary>
    /// The effective luma quantization parameter at minimum-coding-block resolution.
    /// </summary>
    private readonly Buffer2D<sbyte> quantizationParameters;

    /// <summary>
    /// The combined picture, slice, and coding-unit Cb quantization offsets at minimum-coding-block resolution.
    /// </summary>
    private readonly Buffer2D<sbyte> chromaBlueQuantizationOffsets;

    /// <summary>
    /// The combined picture, slice, and coding-unit Cr quantization offsets at minimum-coding-block resolution.
    /// </summary>
    private readonly Buffer2D<sbyte> chromaRedQuantizationOffsets;

    /// <summary>
    /// The packed bypass and PCM flags at minimum-coding-block resolution.
    /// </summary>
    private readonly Buffer2D<byte> flags;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCodingTreeState"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the image memory allocator.</param>
    /// <param name="sequenceParameterSet">The coded picture and minimum coding-block geometry.</param>
    public HevcCodingTreeState(Configuration configuration, HevcSequenceParameterSet sequenceParameterSet)
    {
        this.MinCodingBlockLog2 = sequenceParameterSet.MinCodingBlockLog2;
        this.WidthInMinCodingBlocks = DivideCeilingByPowerOfTwo(
            sequenceParameterSet.Width,
            this.MinCodingBlockLog2);

        this.HeightInMinCodingBlocks = DivideCeilingByPowerOfTwo(
            sequenceParameterSet.Height,
            this.MinCodingBlockLog2);

        Buffer2D<byte>? depths = null;
        Buffer2D<sbyte>? quantizationParameters = null;
        Buffer2D<sbyte>? chromaBlueQuantizationOffsets = null;
        Buffer2D<sbyte>? chromaRedQuantizationOffsets = null;
        Buffer2D<byte>? flags = null;
        try
        {
            depths = configuration.MemoryAllocator.Allocate2D<byte>(
                this.WidthInMinCodingBlocks,
                this.HeightInMinCodingBlocks);

            quantizationParameters = configuration.MemoryAllocator.Allocate2D<sbyte>(
                this.WidthInMinCodingBlocks,
                this.HeightInMinCodingBlocks);

            chromaBlueQuantizationOffsets = configuration.MemoryAllocator.Allocate2D<sbyte>(
                this.WidthInMinCodingBlocks,
                this.HeightInMinCodingBlocks);

            chromaRedQuantizationOffsets = configuration.MemoryAllocator.Allocate2D<sbyte>(
                this.WidthInMinCodingBlocks,
                this.HeightInMinCodingBlocks);

            flags = configuration.MemoryAllocator.Allocate2D<byte>(
                this.WidthInMinCodingBlocks,
                this.HeightInMinCodingBlocks);

            this.depths = depths;
            this.quantizationParameters = quantizationParameters;
            this.chromaBlueQuantizationOffsets = chromaBlueQuantizationOffsets;
            this.chromaRedQuantizationOffsets = chromaRedQuantizationOffsets;
            this.flags = flags;
        }
        catch
        {
            flags?.Dispose();
            chromaRedQuantizationOffsets?.Dispose();
            chromaBlueQuantizationOffsets?.Dispose();
            quantizationParameters?.Dispose();
            depths?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the base-two logarithm of the state map's luma sample unit.
    /// </summary>
    public int MinCodingBlockLog2 { get; }

    /// <summary>
    /// Gets the state-map width in minimum coding blocks.
    /// </summary>
    public int WidthInMinCodingBlocks { get; }

    /// <summary>
    /// Gets the state-map height in minimum coding blocks.
    /// </summary>
    public int HeightInMinCodingBlocks { get; }

    /// <summary>
    /// Gets the split-flag context derived from available left and above coding units.
    /// </summary>
    /// <param name="x">The current coding-unit left coordinate in luma samples.</param>
    /// <param name="y">The current coding-unit top coordinate in luma samples.</param>
    /// <param name="depth">The current coding-tree depth.</param>
    /// <param name="leftAvailable">A value indicating whether the left coding unit is available for prediction.</param>
    /// <param name="aboveAvailable">A value indicating whether the above coding unit is available for prediction.</param>
    /// <returns>The split context in the inclusive range zero through two.</returns>
    public int GetSplitContext(int x, int y, int depth, bool leftAvailable, bool aboveAvailable)
    {
        int unitX = x >> this.MinCodingBlockLog2;
        int unitY = y >> this.MinCodingBlockLog2;
        int context = 0;
        if (leftAvailable && this.depths.DangerousGetRowSpan(unitY)[unitX - 1] > depth)
        {
            context++;
        }

        if (aboveAvailable && this.depths.DangerousGetRowSpan(unitY - 1)[unitX] > depth)
        {
            context++;
        }

        return context;
    }

    /// <summary>
    /// Records the state shared by every minimum coding block covered by one leaf coding unit.
    /// </summary>
    /// <param name="x">The coding-unit left coordinate in luma samples.</param>
    /// <param name="y">The coding-unit top coordinate in luma samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square coding-unit size.</param>
    /// <param name="depth">The coding-tree depth.</param>
    /// <param name="quantizationParameter">The effective luma quantization parameter.</param>
    /// <param name="chromaBlueQuantizationOffset">The combined Cb quantization-parameter offset.</param>
    /// <param name="chromaRedQuantizationOffset">The combined Cr quantization-parameter offset.</param>
    /// <param name="transquantBypass">A value indicating whether transform and quantization are bypassed.</param>
    /// <param name="pcm">A value indicating whether the coding unit contains pulse-code-modulated samples.</param>
    public void SetCodingUnit(
        int x,
        int y,
        int log2Size,
        int depth,
        int quantizationParameter,
        int chromaBlueQuantizationOffset,
        int chromaRedQuantizationOffset,
        bool transquantBypass,
        bool pcm)
    {
        int unitX = x >> this.MinCodingBlockLog2;
        int unitY = y >> this.MinCodingBlockLog2;
        int unitCount = 1 << (log2Size - this.MinCodingBlockLog2);
        int endX = Math.Min(unitX + unitCount, this.WidthInMinCodingBlocks);
        int endY = Math.Min(unitY + unitCount, this.HeightInMinCodingBlocks);
        byte packedFlags = (byte)((transquantBypass ? TransquantBypassFlag : 0) | (pcm ? PcmFlag : 0));

        // Edge coding units still cover a complete power-of-two block in syntax, but the state map contains only
        // displayed picture coordinates. Clipping here keeps later neighbor lookup within the owned picture state.
        for (int row = unitY; row < endY; row++)
        {
            this.depths.DangerousGetRowSpan(row)[unitX..endX].Fill((byte)depth);
            this.quantizationParameters.DangerousGetRowSpan(row)[unitX..endX].Fill((sbyte)quantizationParameter);
            this.chromaBlueQuantizationOffsets.DangerousGetRowSpan(row)[unitX..endX].Fill((sbyte)chromaBlueQuantizationOffset);
            this.chromaRedQuantizationOffsets.DangerousGetRowSpan(row)[unitX..endX].Fill((sbyte)chromaRedQuantizationOffset);
            this.flags.DangerousGetRowSpan(row)[unitX..endX].Fill(packedFlags);
        }
    }

    /// <summary>
    /// Gets the recorded coding-tree depth at a luma sample coordinate.
    /// </summary>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns>The leaf coding-unit depth.</returns>
    public int GetDepth(int x, int y)
        => this.depths.DangerousGetRowSpan(y >> this.MinCodingBlockLog2)[x >> this.MinCodingBlockLog2];

    /// <summary>
    /// Gets the effective luma quantization parameter at a luma sample coordinate.
    /// </summary>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns>The effective luma quantization parameter.</returns>
    public int GetQuantizationParameter(int x, int y)
        => this.quantizationParameters.DangerousGetRowSpan(y >> this.MinCodingBlockLog2)[x >> this.MinCodingBlockLog2];

    /// <summary>
    /// Gets the combined chroma quantization-parameter offset at a luma sample coordinate.
    /// </summary>
    /// <param name="plane">The Cb or Cr reconstruction plane.</param>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns>The selected picture, slice, and coding-unit offset.</returns>
    public int GetChromaQuantizationOffset(HevcPlane plane, int x, int y)
        => plane == HevcPlane.Cb
            ? this.chromaBlueQuantizationOffsets.DangerousGetRowSpan(y >> this.MinCodingBlockLog2)[x >> this.MinCodingBlockLog2]
            : this.chromaRedQuantizationOffsets.DangerousGetRowSpan(y >> this.MinCodingBlockLog2)[x >> this.MinCodingBlockLog2];

    /// <summary>
    /// Gets a value indicating whether the coding unit at a luma sample coordinate bypasses transform and quantization.
    /// </summary>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns><see langword="true"/> when bypass is enabled; otherwise, <see langword="false"/>.</returns>
    public bool IsTransquantBypass(int x, int y)
        => (this.flags.DangerousGetRowSpan(y >> this.MinCodingBlockLog2)[x >> this.MinCodingBlockLog2]
            & TransquantBypassFlag) != 0;

    /// <summary>
    /// Gets a value indicating whether the coding unit at a luma sample coordinate contains PCM samples.
    /// </summary>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns><see langword="true"/> for pulse-code-modulated samples; otherwise, <see langword="false"/>.</returns>
    public bool IsPcm(int x, int y)
        => (this.flags.DangerousGetRowSpan(y >> this.MinCodingBlockLog2)[x >> this.MinCodingBlockLog2]
            & PcmFlag) != 0;

    /// <summary>
    /// Releases the owned coding-tree state maps.
    /// </summary>
    public void Dispose()
    {
        this.depths.Dispose();
        this.quantizationParameters.Dispose();
        this.chromaBlueQuantizationOffsets.Dispose();
        this.chromaRedQuantizationOffsets.Dispose();
        this.flags.Dispose();
    }

    /// <summary>
    /// Divides a nonnegative sample count by a power of two with upward rounding.
    /// </summary>
    /// <param name="value">The sample count.</param>
    /// <param name="shift">The base-two divisor logarithm.</param>
    /// <returns>The upward-rounded quotient.</returns>
    private static int DivideCeilingByPowerOfTwo(int value, int shift) => (value + (1 << shift) - 1) >> shift;
}
