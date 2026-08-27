// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Stores and decodes the luma and chroma intra prediction modes for one HEVC still picture.
/// </summary>
internal sealed class HevcIntraPredictionState : IDisposable
{
    /// <summary>
    /// The base-two logarithm of the minimum luma prediction-block size.
    /// </summary>
    private const int MinPredictionBlockLog2 = 2;

    /// <summary>
    /// The planar intra prediction mode.
    /// </summary>
    private const byte PlanarMode = 0;

    /// <summary>
    /// The DC intra prediction mode.
    /// </summary>
    private const byte DcMode = 1;

    /// <summary>
    /// The horizontal intra prediction mode.
    /// </summary>
    private const byte HorizontalMode = 10;

    /// <summary>
    /// The vertical intra prediction mode.
    /// </summary>
    private const byte VerticalMode = 26;

    /// <summary>
    /// The replacement chroma mode used when an explicit chroma candidate equals the luma mode.
    /// </summary>
    private const byte ChromaReplacementMode = 34;

    /// <summary>
    /// The chroma mode that derives its direction from the colocated luma prediction block.
    /// </summary>
    private const byte DerivedChromaMode = 36;

    /// <summary>
    /// The luma intra mode at minimum-prediction-block resolution.
    /// </summary>
    private readonly Buffer2D<byte> lumaModes;

    /// <summary>
    /// The coded chroma intra mode at minimum-prediction-block resolution in luma coordinates.
    /// </summary>
    private readonly Buffer2D<byte> chromaModes;

    /// <summary>
    /// The resolved chroma intra mode at minimum-prediction-block resolution in luma coordinates.
    /// </summary>
    private readonly Buffer2D<byte> effectiveChromaModes;

    /// <summary>
    /// Whether derived chroma prediction selects the colocated luma prediction block.
    /// </summary>
    private readonly bool derivedChromaUsesColocatedLuma;

    /// <summary>
    /// The mask selecting a luma coordinate within its coding-tree block.
    /// </summary>
    private readonly int codingTreeBlockMask;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcIntraPredictionState"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the image memory allocator.</param>
    /// <param name="sequenceParameterSet">The coded luma picture dimensions.</param>
    public HevcIntraPredictionState(Configuration configuration, HevcSequenceParameterSet sequenceParameterSet)
    {
        this.WidthInMinPredictionBlocks = DivideCeilingByPowerOfTwo(
            sequenceParameterSet.Width,
            MinPredictionBlockLog2);

        this.HeightInMinPredictionBlocks = DivideCeilingByPowerOfTwo(
            sequenceParameterSet.Height,
            MinPredictionBlockLog2);

        this.lumaModes = configuration.MemoryAllocator.Allocate2D<byte>(
            this.WidthInMinPredictionBlocks,
            this.HeightInMinPredictionBlocks);

        this.chromaModes = configuration.MemoryAllocator.Allocate2D<byte>(
            this.WidthInMinPredictionBlocks,
            this.HeightInMinPredictionBlocks);

        this.effectiveChromaModes = configuration.MemoryAllocator.Allocate2D<byte>(
            this.WidthInMinPredictionBlocks,
            this.HeightInMinPredictionBlocks);

        this.derivedChromaUsesColocatedLuma = sequenceParameterSet.ChromaFormat == 3;
        this.codingTreeBlockMask = (1 << sequenceParameterSet.CodingTreeBlockLog2) - 1;
    }

    /// <summary>
    /// Gets the map width in minimum luma prediction blocks.
    /// </summary>
    public int WidthInMinPredictionBlocks { get; }

    /// <summary>
    /// Gets the map height in minimum luma prediction blocks.
    /// </summary>
    public int HeightInMinPredictionBlocks { get; }

    /// <summary>
    /// Decodes and records the luma intra modes of one leaf coding unit.
    /// </summary>
    /// <param name="reader">The current entropy-substream syntax reader.</param>
    /// <param name="x">The coding-unit left coordinate in luma samples.</param>
    /// <param name="y">The coding-unit top coordinate in luma samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square coding-unit size.</param>
    /// <param name="usesNxNPartitions">A value indicating whether the coding unit has four square prediction blocks.</param>
    /// <param name="leftAvailable">A value indicating whether the external left prediction block is available.</param>
    /// <param name="aboveAvailable">A value indicating whether the external above prediction block is available.</param>
    public void DecodeLumaModes(
        ref HevcCabacSyntaxReader reader,
        int x,
        int y,
        int log2Size,
        bool usesNxNPartitions,
        bool leftAvailable,
        bool aboveAvailable)
    {
        int predictionBlockLog2 = usesNxNPartitions ? log2Size - 1 : log2Size;
        int predictionBlockSize = 1 << predictionBlockLog2;
        int predictionBlockCount = usesNxNPartitions ? 4 : 1;
        InlineArray4<byte> mostProbableFlags = default;

        // HEVC codes every prev_intra_luma_pred_flag before any associated mode suffix. Preserve that two-pass
        // ordering because decoding one complete mode at a time would consume a different CABAC bit sequence.
        for (int index = 0; index < predictionBlockCount; index++)
        {
            mostProbableFlags[index] = reader.ReadPreviousIntraLumaPredictionFlag() ? (byte)1 : (byte)0;
        }

        InlineArray4<byte> mostProbableModes = default;
        Span<byte> mostProbableModeSpan = mostProbableModes[..3];

        for (int index = 0; index < predictionBlockCount; index++)
        {
            int offsetX = (index & 1) * predictionBlockSize;
            int offsetY = (index >> 1) * predictionBlockSize;
            int predictionX = x + offsetX;
            int predictionY = y + offsetY;
            bool predictionLeftAvailable = offsetX != 0 || leftAvailable;

            // Luma MPM derivation treats an above prediction unit across a CTB boundary as unavailable. This is
            // narrower than sample reconstruction availability and keeps the candidate order synchronized with CABAC.
            bool predictionAboveAvailable = (predictionY & this.codingTreeBlockMask) != 0 && (offsetY != 0 || aboveAvailable);

            this.GetMostProbableLumaModes(
                predictionX,
                predictionY,
                predictionLeftAvailable,
                predictionAboveAvailable,
                mostProbableModeSpan);

            int mode;
            if (mostProbableFlags[index] != 0)
            {
                mode = mostProbableModeSpan[reader.ReadMostProbableIntraLumaPredictionIndex()];
            }
            else
            {
                SortThree(mostProbableModeSpan);
                mode = reader.ReadRemainingIntraLumaPredictionMode();
                for (int candidate = 0; candidate < mostProbableModeSpan.Length; candidate++)
                {
                    // The remaining-mode code omits the three probable values, so each candidate at or below the
                    // provisional result advances the decoded mode over that omitted slot.
                    mode += mode >= mostProbableModeSpan[candidate] ? 1 : 0;
                }
            }

            this.SetMode(this.lumaModes, predictionX, predictionY, predictionBlockLog2, (byte)mode);
        }
    }

    /// <summary>
    /// Decodes and records the chroma intra modes of one leaf coding unit.
    /// </summary>
    /// <param name="reader">The current entropy-substream syntax reader.</param>
    /// <param name="x">The coding-unit left coordinate in luma samples.</param>
    /// <param name="y">The coding-unit top coordinate in luma samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square coding-unit size.</param>
    /// <param name="usesNxNPartitions">Whether the coding unit contains four luma prediction units.</param>
    public void DecodeChromaModes(ref HevcCabacSyntaxReader reader, int x, int y, int log2Size, bool usesNxNPartitions)
    {
        bool usesFourChromaPredictionUnits = this.derivedChromaUsesColocatedLuma && usesNxNPartitions;
        int predictionBlockLog2 = usesFourChromaPredictionUnits ? log2Size - 1 : log2Size;
        int predictionBlockSize = 1 << predictionBlockLog2;
        int predictionBlockCount = usesFourChromaPredictionUnits ? 4 : 1;
        for (int index = 0; index < predictionBlockCount; index++)
        {
            int predictionX = x + ((index & 1) * predictionBlockSize);
            int predictionY = y + ((index >> 1) * predictionBlockSize);
            int selector = reader.ReadChromaPredictionModeIndex();
            byte mode;
            if (selector < 0)
            {
                mode = DerivedChromaMode;
            }
            else
            {
                ReadOnlySpan<byte> candidates = [PlanarMode, VerticalMode, HorizontalMode, DcMode];
                mode = candidates[selector];
                if (mode == this.GetLumaMode(predictionX, predictionY))
                {
                    mode = ChromaReplacementMode;
                }
            }

            // Combined 4:4:4 follows the four luma prediction partitions of an NxN coding unit. Subsampled formats
            // carry one chroma mode for the coding unit and derive it from the top-left luma partition when requested.
            this.SetMode(this.chromaModes, predictionX, predictionY, predictionBlockLog2, mode);
            byte effectiveMode = mode == DerivedChromaMode ? this.GetLumaMode(predictionX, predictionY) : mode;
            this.SetMode(this.effectiveChromaModes, predictionX, predictionY, predictionBlockLog2, effectiveMode);
        }
    }

    /// <summary>
    /// Gets the luma intra mode at a luma sample coordinate.
    /// </summary>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns>The luma intra mode in the inclusive range zero through thirty-four.</returns>
    public byte GetLumaMode(int x, int y)
        => this.lumaModes.DangerousGetRowSpan(y >> MinPredictionBlockLog2)[x >> MinPredictionBlockLog2];

    /// <summary>
    /// Gets the coded chroma intra mode at a luma sample coordinate.
    /// </summary>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns>An explicit chroma direction or the derived-mode value.</returns>
    public byte GetChromaMode(int x, int y)
        => this.chromaModes.DangerousGetRowSpan(y >> MinPredictionBlockLog2)[x >> MinPredictionBlockLog2];

    /// <summary>
    /// Gets the effective chroma intra mode at a luma sample coordinate.
    /// </summary>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns>The explicit chroma mode, or the colocated luma mode when chroma uses derived mode.</returns>
    public byte GetEffectiveChromaMode(int x, int y)
        => this.effectiveChromaModes.DangerousGetRowSpan(y >> MinPredictionBlockLog2)[x >> MinPredictionBlockLog2];

    /// <summary>
    /// Releases the owned intra-mode maps.
    /// </summary>
    public void Dispose()
    {
        this.lumaModes.Dispose();
        this.chromaModes.Dispose();
        this.effectiveChromaModes.Dispose();
    }

    /// <summary>
    /// Derives the three most-probable luma intra modes from available spatial neighbors.
    /// </summary>
    /// <param name="x">The prediction-block left coordinate in luma samples.</param>
    /// <param name="y">The prediction-block top coordinate in luma samples.</param>
    /// <param name="leftAvailable">A value indicating whether the left prediction block is available.</param>
    /// <param name="aboveAvailable">A value indicating whether the above prediction block is available.</param>
    /// <param name="modes">The three-element destination span.</param>
    private void GetMostProbableLumaModes(
        int x,
        int y,
        bool leftAvailable,
        bool aboveAvailable,
        Span<byte> modes)
    {
        byte leftMode = leftAvailable ? this.GetLumaMode(x - 1, y) : DcMode;
        byte aboveMode = aboveAvailable ? this.GetLumaMode(x, y - 1) : DcMode;
        if (leftMode == aboveMode)
        {
            if (leftMode > DcMode)
            {
                modes[0] = leftMode;
                modes[1] = (byte)(((leftMode + 29) % 32) + 2);
                modes[2] = (byte)(((leftMode - 1) % 32) + 2);
            }
            else
            {
                modes[0] = PlanarMode;
                modes[1] = DcMode;
                modes[2] = VerticalMode;
            }

            return;
        }

        modes[0] = leftMode;
        modes[1] = aboveMode;
        if (leftMode != PlanarMode && aboveMode != PlanarMode)
        {
            modes[2] = PlanarMode;
        }
        else
        {
            modes[2] = leftMode + aboveMode < 2 ? VerticalMode : DcMode;
        }
    }

    /// <summary>
    /// Records one prediction mode over a square luma-coordinate region.
    /// </summary>
    /// <param name="map">The luma or chroma mode map.</param>
    /// <param name="x">The region left coordinate in luma samples.</param>
    /// <param name="y">The region top coordinate in luma samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square region size.</param>
    /// <param name="mode">The prediction mode.</param>
    private void SetMode(Buffer2D<byte> map, int x, int y, int log2Size, byte mode)
    {
        int unitX = x >> MinPredictionBlockLog2;
        int unitY = y >> MinPredictionBlockLog2;
        int unitCount = 1 << (log2Size - MinPredictionBlockLog2);
        int endX = Math.Min(unitX + unitCount, this.WidthInMinPredictionBlocks);
        int endY = Math.Min(unitY + unitCount, this.HeightInMinPredictionBlocks);
        for (int row = unitY; row < endY; row++)
        {
            map.DangerousGetRowSpan(row)[unitX..endX].Fill(mode);
        }
    }

    /// <summary>
    /// Sorts three intra-mode values into ascending order.
    /// </summary>
    /// <param name="values">The three-element mode span.</param>
    private static void SortThree(Span<byte> values)
    {
        if (values[0] > values[1])
        {
            byte value = values[0];
            values[0] = values[1];
            values[1] = value;
        }

        if (values[0] > values[2])
        {
            byte value = values[0];
            values[0] = values[2];
            values[2] = value;
        }

        if (values[1] > values[2])
        {
            byte value = values[1];
            values[1] = values[2];
            values[2] = value;
        }
    }

    /// <summary>
    /// Divides a nonnegative sample count by a power of two with upward rounding.
    /// </summary>
    /// <param name="value">The sample count.</param>
    /// <param name="shift">The base-two divisor logarithm.</param>
    /// <returns>The upward-rounded quotient.</returns>
    private static int DivideCeilingByPowerOfTwo(int value, int shift) => (value + (1 << shift) - 1) >> shift;
}
